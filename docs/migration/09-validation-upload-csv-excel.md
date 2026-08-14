# 09 — Validacije i CSV/Excel upload

## Svrha

Prevesti legacy „upload liste brojeva“ (CSV/Excel) sa robusnim, testabilnim validacijama:

- provera formata `06XXXXXXXX` (9 ili 10 cifara — v. napomenu),
- provera zaglavlja (kolone i njihov redosled/tolerantnost),
- validacija na nivou reda + grupisano prikazivanje grešaka po redu,
- validacija duplikata unutar fajla,
- validacija duplikata prema postojećim brojevima klijenta,
- sanitizacija (trim, ne-štampajući karakteri, zero-width),
- normalizacija (uvek na `06XXXXXXXX` bez razmaka/tačaka/crtica).

Koristimo **FluentValidation** za VM-ove i **CsvHelper**/**ClosedXML** za parser.

## Legacy stanje (za referencu)

- Upload je bio kroz `HttpPostedFileBase` u `ClientManageController` / `GroupController`.
- Legacy je koristio EPPlus 4.x za `.xlsx` i `System.IO.StreamReader` za `.csv`.
- Validacija formata brojeva se ponavljala inline kroz `Regex.IsMatch(@"^06\d{7,8}$", ...)`.
- Greške su se prikazivale zbirno u `TempData["Errors"]` bez konteksta reda.

## Pravila validacije (finalna)

- **Broj**: mora biti `^06\d{7,8}$` (10 cifara ukupno; MTS mobilni brojevi u Srbiji su 8 ili 9
  cifara iza `06`). **Zadrži** postojeće pravilo `06\d{7,8}` — ako trenutna produkcija pravi
  distinkciju, ostavljamo isto.
- **Header**: prihvati imena kolona `Number` ili `Broj` ili `Broj telefona` (case-insensitive,
  trimovano). Ako fajl nema header, prihvati prvu kolonu kao broj (opciono flag u UI).
- **Dužina fajla**: max 50.000 redova (konfigurabilno).
- **Encoding CSV-a**: UTF-8 (sa ili bez BOM-a). Fallback: Windows-1250 → probaj re-decode.
- **Delimiter CSV-a**: `;` (SR standard) → fallback `,`.
- **Duplikati**: unutar fajla → prikaži kao warning (prihvatiš prvi, ostatak baciš); prema DB →
  ignoriši postojeće aktivne, dodaj neaktivne kao aktivne.

## Application sloj — kontrakti

`src/BizSMS.Application/Numbers/INumberImportService.cs`:

```csharp
namespace BizSMS.Application.Numbers;

public interface INumberImportService
{
    Task<NumberImportResult> ImportAsync(int clientId, int groupId, Stream stream, string fileName, CancellationToken ct);
}

public sealed record NumberImportResult(
    int TotalRows,
    int ValidRows,
    int Inserted,
    int Duplicates,
    IReadOnlyList<RowError> Errors);

public sealed record RowError(int RowNumber, string RawValue, string Error);
```

## Parser: automatski detektuje CSV vs XLSX

```csharp
public interface INumberFileParser
{
    IAsyncEnumerable<ParsedRow> ParseAsync(Stream input, string fileName, CancellationToken ct);
}

public sealed record ParsedRow(int RowNumber, string RawNumber);
```

`src/BizSMS.Infrastructure/Numbers/NumberFileParser.cs`:

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace BizSMS.Infrastructure.Numbers;

internal sealed class NumberFileParser : INumberFileParser
{
    public async IAsyncEnumerable<ParsedRow> ParseAsync(Stream input, string fileName, [EnumeratorCancellation] CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".xlsx" or ".xlsm")
        {
            foreach (var r in ParseXlsx(input)) { ct.ThrowIfCancellationRequested(); yield return r; }
        }
        else
        {
            await foreach (var r in ParseCsv(input, ct)) yield return r;
        }
    }

    private static IEnumerable<ParsedRow> ParseXlsx(Stream input)
    {
        // ClosedXML zahteva seek-able stream
        if (!input.CanSeek)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            input = ms;
            input.Position = 0;
        }

        using var wb = new XLWorkbook(input);
        var ws = wb.Worksheets.First();

        // Detekcija header-a: prva ćelija koja izgleda kao „broj/number“
        var firstRow = ws.FirstRowUsed();
        var header = firstRow?.CellsUsed().Select(c => c.GetString().Trim()).ToList() ?? new();
        var hasHeader = header.Any(h => IsNumberHeader(h));
        var col = 1;
        if (hasHeader)
        {
            var idx = header.FindIndex(IsNumberHeader);
            if (idx >= 0) col = idx + 1;
        }

        var startRow = hasHeader ? 2 : 1;
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (int r = startRow; r <= lastRow; r++)
        {
            var raw = ws.Cell(r, col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            yield return new ParsedRow(r, raw);
        }
    }

    private static async IAsyncEnumerable<ParsedRow> ParseCsv(Stream input, [EnumeratorCancellation] CancellationToken ct)
    {
        // Automatska detekcija delimitera nad prvih ~4KB
        var buffer = new byte[4096];
        var read = await input.ReadAsync(buffer, ct);
        var head = Encoding.UTF8.GetString(buffer, 0, read);
        var delimiter = head.Count(c => c == ';') > head.Count(c => c == ',') ? ";" : ",";

        input.Position = 0;
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,     // ručno detektujemo
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        });

        int rowNum = 0;
        var firstRowChecked = false;
        var col = 0;

        while (await csv.ReadAsync())
        {
            rowNum++;
            var count = csv.Parser.Count;
            if (count == 0) continue;

            if (!firstRowChecked)
            {
                firstRowChecked = true;
                for (int i = 0; i < count; i++)
                {
                    var v = csv.GetField(i) ?? string.Empty;
                    if (IsNumberHeader(v)) { col = i; goto NextRow; }
                }
                // nema header-a — koristi prvu kolonu
                col = 0;
            }

            var raw = csv.GetField(col)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            yield return new ParsedRow(rowNum, raw);

        NextRow:
            ;
        }
    }

    private static bool IsNumberHeader(string s)
        => s.Equals("Broj", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Broj telefona", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Number", StringComparison.OrdinalIgnoreCase)
        || s.Equals("Phone", StringComparison.OrdinalIgnoreCase);
}
```

## Validacija formata (jedna metoda, više korisnika)

`src/BizSMS.Domain/ValueObjects/PhoneNumber.cs`:

```csharp
using System.Text.RegularExpressions;

namespace BizSMS.Domain.ValueObjects;

public readonly partial record struct PhoneNumber(string Value)
{
    [GeneratedRegex(@"^06\d{7,8}$")]
    private static partial Regex Pattern();

    public static bool TryParse(string? raw, out PhoneNumber pn)
    {
        pn = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var cleaned = Normalize(raw);
        if (!Pattern().IsMatch(cleaned)) return false;
        pn = new PhoneNumber(cleaned);
        return true;
    }

    public static string Normalize(string raw)
    {
        Span<char> buf = stackalloc char[raw.Length];
        int i = 0;
        foreach (var c in raw)
        {
            if (char.IsDigit(c)) buf[i++] = c;
        }
        var digits = new string(buf[..i]);
        // Ako počinje sa 3816 (npr. 381641234567) — prebaci u 06
        if (digits.StartsWith("3816") && digits.Length is 11 or 12)
            digits = "0" + digits[3..];
        return digits;
    }

    public override string ToString() => Value;
}
```

Testovi (v. poglavlje 10) treba da pokrivaju sve edge case-ove.

## Import service — implementacija sa grupisanim greškama

`src/BizSMS.Infrastructure/Numbers/NumberImportService.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.Numbers;
using BizSMS.Domain.Entities;
using BizSMS.Domain.ValueObjects;
using BizSMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Infrastructure.Numbers;

internal sealed class NumberImportService : INumberImportService
{
    private const int MaxRows = 50_000;

    private readonly INumberFileParser _parser;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public NumberImportService(INumberFileParser parser, AppDbContext db, IAuditService audit)
        => (_parser, _db, _audit) = (parser, db, audit);

    public async Task<NumberImportResult> ImportAsync(int clientId, int groupId, Stream stream, string fileName, CancellationToken ct)
    {
        var errors = new List<RowError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var toInsert = new List<string>();
        int total = 0;

        await foreach (var row in _parser.ParseAsync(stream, fileName, ct))
        {
            total++;
            if (total > MaxRows)
            {
                errors.Add(new RowError(row.RowNumber, row.RawNumber, $"Prekoračen maksimalan broj redova ({MaxRows})."));
                break;
            }

            if (!PhoneNumber.TryParse(row.RawNumber, out var pn))
            {
                errors.Add(new RowError(row.RowNumber, row.RawNumber, "Nevažeći format broja. Očekivano: 06XXXXXXXX."));
                continue;
            }

            if (!seen.Add(pn.Value))
            {
                errors.Add(new RowError(row.RowNumber, row.RawNumber, "Duplikat unutar fajla."));
                continue;
            }

            toInsert.Add(pn.Value);
        }

        // Duplikati prema DB
        var existing = await _db.Numbers.AsNoTracking()
            .Where(n => n.ClientID == clientId && toInsert.Contains(n.Number))
            .Select(n => new { n.NumberID, n.Number, n.Active })
            .ToListAsync(ct);
        var existingSet = existing.Select(e => e.Number).ToHashSet(StringComparer.Ordinal);

        var truToInsert = toInsert.Where(n => !existingSet.Contains(n)).ToList();
        var duplicates = toInsert.Count - truToInsert.Count;

        // Reaktiviraj inactive postojeće
        var toReactivate = existing.Where(e => !e.Active).Select(e => e.NumberID).ToList();
        if (toReactivate.Count > 0)
        {
            await _db.Numbers.Where(n => toReactivate.Contains(n.NumberID))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(n => n.Active, true)
                    .SetProperty(n => n.CheckDate, DateTime.UtcNow), ct);
        }

        // Insert novi
        if (truToInsert.Count > 0)
        {
            var now = DateTime.UtcNow;
            var rows = truToInsert.Select(n => new NumbersModel
            {
                Number = n,
                Active = true,
                ClientID = clientId,
                NumberTypeID = 2, // ne-VPN (korisničke); VPN se dodaje kroz delta sync
                InsertDate = now,
                CheckDate = now,
                SendAllowed = true
            });
            _db.Numbers.AddRange(rows);

            // Dodaj u grupu ako je zadata
            if (groupId > 0)
            {
                foreach (var num in rows)
                {
                    _db.GroupNumbers.Add(new GroupNumberModel { GroupID = groupId, Number = num });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("NumberImportCompleted", "OK", new
        {
            ClientId = clientId,
            GroupId = groupId,
            FileName = fileName,
            Total = total,
            Valid = toInsert.Count,
            Inserted = truToInsert.Count,
            Duplicates = duplicates,
            Errors = errors.Count
        }, ct);

        return new NumberImportResult(total, toInsert.Count, truToInsert.Count, duplicates, errors);
    }
}
```

## Kontroler + validacija fajla

`src/BizSMS.Web/Controllers/NumberImportController.cs`:

```csharp
using BizSMS.Application.Numbers;
using BizSMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.BusinessUser)]
public sealed class NumberImportController : Controller
{
    private static readonly string[] AllowedContentTypes =
    {
        "text/csv",
        "application/vnd.ms-excel",       // stariji xlsx-ovi šalju ovo
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    private static readonly string[] AllowedExtensions = { ".csv", ".xlsx", ".xlsm" };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    private readonly INumberImportService _import;
    public NumberImportController(INumberImportService import) => _import = import;

    [HttpGet]
    public IActionResult Upload(int groupId) => View(new UploadVm { GroupId = groupId });

    [HttpPost, RequestSizeLimit(MaxFileSize)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadVm vm, CancellationToken ct)
    {
        if (vm.File is null || vm.File.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.File), "Fajl je obavezan.");
            return View(vm);
        }
        if (vm.File.Length > MaxFileSize)
        {
            ModelState.AddModelError(nameof(vm.File), $"Maksimalna veličina je {MaxFileSize / (1024*1024)} MB.");
            return View(vm);
        }

        var ext = Path.GetExtension(vm.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(vm.File.ContentType))
        {
            ModelState.AddModelError(nameof(vm.File), "Dozvoljeni formati: .csv, .xlsx.");
            return View(vm);
        }

        await using var stream = vm.File.OpenReadStream();
        var result = await _import.ImportAsync(vm.ClientId, vm.GroupId, stream, vm.File.FileName, ct);

        vm.Result = result;
        return View("UploadResult", vm);
    }
}

public sealed class UploadVm
{
    public int ClientId { get; set; }
    public int GroupId { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? File { get; set; }
    public NumberImportResult? Result { get; set; }
}
```

`Views/NumberImport/Upload.cshtml`:

```cshtml
@model BizSMS.Web.Controllers.UploadVm
@{
    ViewData["Title"] = "Upload brojeva";
}

<h2>Upload brojeva</h2>
<form asp-action="Upload" enctype="multipart/form-data" method="post">
    <input type="hidden" asp-for="ClientId" />
    <input type="hidden" asp-for="GroupId" />

    <div class="form-group">
        <label asp-for="File">CSV ili XLSX fajl (max 10 MB)</label>
        <input asp-for="File" type="file" accept=".csv,.xlsx" class="form-control" />
        <span asp-validation-for="File" class="text-danger"></span>
    </div>
    <button class="btn btn-primary" type="submit">Uvezi</button>
</form>

<div class="alert alert-info mt-3">
    Očekivani format zaglavlja: <code>Broj</code> ili <code>Number</code>.
    Broj mora biti u obliku <code>06XXXXXXXX</code>.
</div>
```

`Views/NumberImport/UploadResult.cshtml`:

```cshtml
@model BizSMS.Web.Controllers.UploadVm

<h2>Rezultat uploada</h2>
<ul>
    <li>Ukupno redova: <strong>@Model.Result!.TotalRows</strong></li>
    <li>Validno: <strong>@Model.Result.ValidRows</strong></li>
    <li>Ubačeno: <strong>@Model.Result.Inserted</strong></li>
    <li>Duplikati (već postoje): <strong>@Model.Result.Duplicates</strong></li>
    <li>Grešaka: <strong>@Model.Result.Errors.Count</strong></li>
</ul>

@if (Model.Result.Errors.Count > 0)
{
    <table class="table table-sm">
        <thead>
            <tr><th>Red</th><th>Vrednost</th><th>Greška</th></tr>
        </thead>
        <tbody>
            @foreach (var e in Model.Result.Errors)
            {
                <tr>
                    <td>@e.RowNumber</td>
                    <td><code>@e.RawValue</code></td>
                    <td>@e.Error</td>
                </tr>
            }
        </tbody>
    </table>
}
```

## FluentValidation za VM-ove (npr. slanje SMS-a)

Legacy je koristio `DataAnnotations` uz custom `CompareAttributes`. Preporuka: FluentValidation.

`src/BizSMS.Web/Validators/SendSmsViewModelValidator.cs`:

```csharp
using BizSMS.Domain.ValueObjects;
using FluentValidation;

namespace BizSMS.Web.Validators;

public sealed class SendSmsViewModelValidator : AbstractValidator<SendSmsViewModel>
{
    public SendSmsViewModelValidator()
    {
        RuleFor(x => x.Sender)
            .NotEmpty()
            .MaximumLength(11)
            .WithMessage("Alfanumerički pošiljalac je obavezan (max 11 karaktera).");

        RuleFor(x => x.MessageText)
            .NotEmpty()
            .MaximumLength(765)
            .WithMessage("Sadržaj poruke je obavezan.");

        RuleFor(x => x.Recipients)
            .NotEmpty()
            .Must(r => r.All(n => PhoneNumber.TryParse(n, out _)))
            .WithMessage("Svi primaoci moraju biti u formatu 06XXXXXXXX.");

        RuleFor(x => x.ScheduledFor)
            .Must((vm, dt) => dt is null || dt > DateTime.UtcNow.AddMinutes(1))
            .WithMessage("Zakazano vreme mora biti u budućnosti (min. 1 min).");
    }
}
```

Registruj u `Program.cs`:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<SendSmsViewModelValidator>();
builder.Services.AddFluentValidationAutoValidation();
```

## Cenovnik: validacija preklapanja opsega

Legacy je proveravao overlap opsega VPN/mts/van mts u `AdminManageController`. Prebaci u
servisni sloj:

```csharp
public interface IMessageCostService
{
    Task<Result> UpsertAsync(MessageCostDto dto, CancellationToken ct);
}

internal sealed class MessageCostService : IMessageCostService
{
    public async Task<Result> UpsertAsync(MessageCostDto dto, CancellationToken ct)
    {
        // Preklapanje: postoji [a,b] i novi [x,y] takvi da a<=y && x<=b
        var overlap = await _db.MessageCosts
            .Where(c => c.Category == dto.Category
                        && c.Id != dto.Id
                        && c.StartDate == dto.StartDate)
            .Where(c => c.PriceFrom <= dto.PriceTo && dto.PriceFrom <= c.PriceTo)
            .AnyAsync(ct);

        if (overlap)
            return Result.Fail($"Opseg [{dto.PriceFrom}-{dto.PriceTo}] za kategoriju {dto.Category} " +
                               "se preklapa sa postojećim cenovnikom.");

        // upsert...
        return Result.Ok();
    }
}
```

## STOP_ID pravilo za ne-VPN brojeve

Kontrolisano centralno u `IMessageComposer`:

```csharp
public sealed class MessageComposer : IMessageComposer
{
    private const string StopIdSuffix = " STOP: pošaljite STOP na 6666";

    public string ComposeForRecipient(string body, bool isVpn)
        => isVpn ? body : body.TrimEnd() + StopIdSuffix;
}
```

Test: „za ne-VPN broj, poruka mora sadržavati STOP_ID“ (poglavlje 10).

## Before / After

**Legacy**:

```csharp
public ActionResult Upload(HttpPostedFileBase file, int groupId)
{
    var errors = new List<string>();
    using (var package = new ExcelPackage(file.InputStream))
    {
        var ws = package.Workbook.Worksheets[1];
        for (int r = 2; r <= ws.Dimension.End.Row; r++)
        {
            var raw = ws.Cells[r, 1].Text.Trim();
            if (!Regex.IsMatch(raw, @"^06\d{7,8}$"))
                errors.Add($"Row {r}: '{raw}' nije validan.");
            else
                context.Numbers.Add(new NumbersModel { Number = raw, ClientID = ... });
        }
        context.SaveChanges();
    }
    TempData["Errors"] = string.Join("\n", errors);
    return RedirectToAction("Index");
}
```

**.NET 10** — v. `NumberImportController` + `NumberImportService` gore. Prednosti:

- Streaming (radi na velikim fajlovima).
- Testabilan servis, testabilan parser.
- Struktuiran rezultat (broj redova / uspesnih / greske / duplikati).
- Audit trail.
- STOP_ID centralno.

## Checklist za code review

- [ ] `PhoneNumber.TryParse` je JEDINO mesto gde se validira format broja.
- [ ] `NumberImportService.ImportAsync` je `async` i vraća strukturisani rezultat.
- [ ] Grupisano prikazivanje grešaka po redu (row_number + raw_value + poruka).
- [ ] Kontroler ne procesuje fajl inline — samo prihvata i prosleđuje servisu.
- [ ] `RequestSizeLimit` je postavljen (max 10MB); `MultipartBodyLengthLimit` je usklađen na Kestrel/IIS nivou.
- [ ] Content-type i ekstenzija se **oba** proveravaju.
- [ ] Antiforgery je uključen.
- [ ] Audit se pravi (import completed).
- [ ] FluentValidation je registrovan globalno (nema više parcijalnih `[Compare]`).
- [ ] STOP_ID se dodaje samo u compose sloju, ne u UI-u.

## Najčešće greške i kako ih izbeći

1. **`file.OpenReadStream()` bez `await using`** — može ostati otvoren pri exception-u.
2. **Nemogućnost `ClosedXML` da čita non-seekable stream** — kopiraj u `MemoryStream` pre parse-a
   (u parser-u je već rešeno).
3. **BOM i encoding CSV-a** — `StreamReader` mora imati `detectEncodingFromByteOrderMarks: true`,
   inače prvi red ima „ï»¿“ prefiks.
4. **Ne validirati Content-Type** — napadač šalje `.csv.exe` — proveri ekstenziju **i** content type.
5. **Direktan `AddRange` bez batch-a** — za >10k redova koristi `AddRange` + jedan `SaveChanges`,
   ili `EF Core bulk` (npr. `EFCore.BulkExtensions`) ako je performanse pitanje.
6. **Ostavljanje neaktivnih brojeva netaknuto** — bez logike „reactivate on re-import“ korisnik
   dobija duplikate u DB. Naš servis reaktivira postojeće.
7. **STOP_ID dodavan više puta** — ako se compose poziva iterativno, proveri idempotentnost
   (`if !body.EndsWith(StopIdSuffix)`).
8. **Regeks bez timeout-a** — `Regex.IsMatch` bez timeout-a može biti napad (ReDoS). Koristi
   `RegexOptions.Compiled | RegexOptions.NonBacktracking` i `TimeSpan.FromMilliseconds(250)`.
