# 07 — Izveštaji, Excel export i autorizacija

## Svrha

Definisati servisni sloj i endpoint-e za tri legacy izveštaja iz `ReportController`:

- **mesečni troškovi** po klijentu / periodu,
- **poslato / zakazano** (SMS pregled),
- **arhiva** klijenta (istorijski zapisi).

Uz njih ide **Excel** export (glavno) i **CSV fallback** (za slučaj da paket nije dostupan). Sve
mora biti iza `[Authorize]` sa politikom da klijentski korisnik vidi **samo svog klijenta**.

## Ciljni tehnički stack

- **Excel**: `ClosedXML` (MIT licenca, streamable za srednje veličine).
  Alternativa: `EPPlus` v8 (komercijalna licenca za produkciju).
- **CSV**: `CsvHelper` (BSD-3).
- **PDF štampa**: koristiti browser `window.print()` sa printerski-optimizovanim CSS-om (`@media print`)
  — nema potrebe za novom biblioteku.

## Koraci migracije

1. Prebaci sve query-je za izveštaje iz `ReportController` u `BizSMS.Application/Reports/*`.
2. Definiši read modele (DTO/VM) i asinhrone servise.
3. Ubaci `IExcelExporter` i `ICsvExporter` interfejse.
4. Napravi kontroler sa akcijama koje vraćaju `FileContentResult` (Excel/CSV) ili `View` (HTML).
5. Postavi autorizaciju: `Administrator` vidi sve, `BusinessUser` samo sopstvenog `ClientId`.
6. Dodaj rate-limiting (opcioni) za velike export operacije.

## Application sloj — read modeli i servis

`src/BizSMS.Application/Reports/MonthlyCostReport.cs`:

```csharp
namespace BizSMS.Application.Reports;

public sealed record MonthlyCostRow(
    int ClientId,
    string ClientName,
    string ContractId,
    int SentCount,
    int ScheduledCount,
    int VpnCount,
    int NonVpnCount,
    decimal TotalCost);

public sealed record MonthlyCostReport(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<MonthlyCostRow> Rows,
    decimal Grand);
```

`src/BizSMS.Application/Reports/IReportService.cs`:

```csharp
namespace BizSMS.Application.Reports;

public interface IReportService
{
    Task<MonthlyCostReport> GetMonthlyCostAsync(int? clientId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<SendActivityReport> GetSendActivityAsync(int? clientId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<ClientArchiveReport> GetClientArchiveAsync(int clientId, CancellationToken ct);
}

public sealed record SendActivityRow(
    int MessageId, DateTime SendDate, string Sender, int Total, int Delivered, int Failed, string Status);

public sealed record SendActivityReport(
    DateOnly PeriodFrom, DateOnly PeriodTo, IReadOnlyList<SendActivityRow> Rows);

public sealed record ClientArchiveReport(
    int ClientId, string ClientName, IReadOnlyList<ArchiveEntry> Entries);

public sealed record ArchiveEntry(DateTime When, string Action, string Detail, string ByUser);
```

`src/BizSMS.Infrastructure/Reports/ReportService.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.Reports;
using BizSMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Infrastructure.Reports;

internal sealed class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ReportService(AppDbContext db, ITenantContext tenant) => (_db, _tenant) = (db, tenant);

    public async Task<MonthlyCostReport> GetMonthlyCostAsync(int? clientId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        EnsureAccess(clientId);
        var effClient = _tenant.IsAdministrator ? clientId : _tenant.ClientId;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt   = to.ToDateTime(TimeOnly.MaxValue);

        var query =
            from mn in _db.MessageNumbers.AsNoTracking()
            join m in _db.Messages.AsNoTracking() on mn.MessageID equals m.MessageID
            join c in _db.Clients.AsNoTracking() on m.ClientID equals c.ClientID
            join mc in _db.MessageCosts.AsNoTracking() on mn.MessageCostId equals mc.Id into mcj
            from mc in mcj.DefaultIfEmpty()
            where m.SendDate >= fromDt && m.SendDate <= toDt
                  && (effClient == null || m.ClientID == effClient)
            select new
            {
                m.ClientID,
                c.Name,
                m.ContractId,
                mn.NumberType,
                Cost = mc == null ? 0m : mc.PricePerMessage,
                Scheduled = m.Status == "Scheduled" ? 1 : 0,
                Sent = m.Status == "Sent" ? 1 : 0,
                IsVpn = mn.NumberType == "VPN" ? 1 : 0,
                IsNonVpn = mn.NumberType != "VPN" ? 1 : 0,
            };

        var grouped = await query
            .GroupBy(x => new { x.ClientID, x.Name, x.ContractId })
            .Select(g => new MonthlyCostRow(
                g.Key.ClientID,
                g.Key.Name,
                g.Key.ContractId,
                g.Sum(x => x.Sent),
                g.Sum(x => x.Scheduled),
                g.Sum(x => x.IsVpn),
                g.Sum(x => x.IsNonVpn),
                g.Sum(x => x.Cost)))
            .OrderBy(r => r.ClientName)
            .ToListAsync(ct);

        return new MonthlyCostReport(from, to, grouped, grouped.Sum(r => r.TotalCost));
    }

    public async Task<SendActivityReport> GetSendActivityAsync(int? clientId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        EnsureAccess(clientId);
        var effClient = _tenant.IsAdministrator ? clientId : _tenant.ClientId;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var q = _db.Messages.AsNoTracking()
            .Where(m => m.SendDate >= fromDt && m.SendDate <= toDt)
            .Where(m => effClient == null || m.ClientID == effClient)
            .Select(m => new SendActivityRow(
                m.MessageID,
                m.SendDate,
                m.Sender,
                m.MessageNumbers.Count,
                m.MessageNumbers.Count(mn => mn.Delivered == 1),
                m.MessageNumbers.Count(mn => mn.Delivered == 0 && mn.Sent),
                m.Status));

        var rows = await q.OrderByDescending(r => r.SendDate).ToListAsync(ct);
        return new SendActivityReport(from, to, rows);
    }

    public async Task<ClientArchiveReport> GetClientArchiveAsync(int clientId, CancellationToken ct)
    {
        EnsureAccess(clientId);
        var c = await _db.Clients.AsNoTracking().FirstAsync(x => x.ClientID == clientId, ct);

        var entries = await _db.Logs.AsNoTracking()
            .Where(l => l.LogMessage.Contains($"ClientID={clientId}"))
            .OrderByDescending(l => l.LogDate)
            .Take(5000)
            .Select(l => new ArchiveEntry(l.LogDate, l.Action, l.LogMessage, l.User))
            .ToListAsync(ct);

        return new ClientArchiveReport(clientId, c.Name, entries);
    }

    private void EnsureAccess(int? requestedClientId)
    {
        if (_tenant.IsAdministrator) return;
        if (requestedClientId is null || requestedClientId == _tenant.ClientId) return;
        throw new UnauthorizedAccessException("Nemate pravo pristupa izveštaju drugog klijenta.");
    }
}
```

## Excel exporter (ClosedXML)

`src/BizSMS.Application/Abstractions/IExcelExporter.cs`:

```csharp
namespace BizSMS.Application.Abstractions;

public interface IExcelExporter
{
    byte[] ExportMonthlyCost(MonthlyCostReport report);
    byte[] ExportSendActivity(SendActivityReport report);
}
```

`src/BizSMS.Infrastructure/Reports/ClosedXmlExcelExporter.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.Reports;
using ClosedXML.Excel;

namespace BizSMS.Infrastructure.Reports;

internal sealed class ClosedXmlExcelExporter : IExcelExporter
{
    public byte[] ExportMonthlyCost(MonthlyCostReport report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Mesečni troškovi");

        ws.Cell(1, 1).Value = $"Period: {report.PeriodFrom:dd.MM.yyyy} - {report.PeriodTo:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 8).Merge().Style.Font.SetBold();

        var header = new[] { "Klijent", "Ugovor", "Poslato", "Zakazano", "VPN", "Van VPN", "Ukupno", "Trošak (RSD)" };
        for (int i = 0; i < header.Length; i++)
        {
            var cell = ws.Cell(3, i + 1);
            cell.Value = header[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        int r = 4;
        foreach (var row in report.Rows)
        {
            ws.Cell(r, 1).Value = row.ClientName;
            ws.Cell(r, 2).Value = row.ContractId;
            ws.Cell(r, 3).Value = row.SentCount;
            ws.Cell(r, 4).Value = row.ScheduledCount;
            ws.Cell(r, 5).Value = row.VpnCount;
            ws.Cell(r, 6).Value = row.NonVpnCount;
            ws.Cell(r, 7).Value = row.SentCount + row.ScheduledCount;
            ws.Cell(r, 8).Value = row.TotalCost;
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            r++;
        }

        ws.Cell(r, 1).Value = "UKUPNO";
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 8).Value = report.Grand;
        ws.Cell(r, 8).Style.Font.Bold = true;
        ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportSendActivity(SendActivityReport report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Aktivnost slanja");
        ws.Cell(1, 1).Value = $"Period: {report.PeriodFrom:dd.MM.yyyy} - {report.PeriodTo:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 6).Merge().Style.Font.SetBold();

        var header = new[] { "ID poruke", "Datum slanja", "Pošiljalac", "Ukupno", "Isporučeno", "Neuspešno" };
        for (int i = 0; i < header.Length; i++)
        {
            var c = ws.Cell(3, i + 1);
            c.Value = header[i];
            c.Style.Font.Bold = true;
        }

        int r = 4;
        foreach (var row in report.Rows)
        {
            ws.Cell(r, 1).Value = row.MessageId;
            ws.Cell(r, 2).Value = row.SendDate;
            ws.Cell(r, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            ws.Cell(r, 3).Value = row.Sender;
            ws.Cell(r, 4).Value = row.Total;
            ws.Cell(r, 5).Value = row.Delivered;
            ws.Cell(r, 6).Value = row.Failed;
            r++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
```

Registracija:

```csharp
services.AddScoped<IReportService, ReportService>();
services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
services.AddSingleton<ICsvExporter, CsvHelperExporter>();
```

## CSV fallback (ako Excel biblioteka nije dostupna)

`src/BizSMS.Application/Abstractions/ICsvExporter.cs`:

```csharp
namespace BizSMS.Application.Abstractions;

public interface ICsvExporter
{
    byte[] Export<T>(IEnumerable<T> rows, string delimiter = ";");
}
```

`src/BizSMS.Infrastructure/Reports/CsvHelperExporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using BizSMS.Application.Abstractions;
using CsvHelper;
using CsvHelper.Configuration;

namespace BizSMS.Infrastructure.Reports;

internal sealed class CsvHelperExporter : ICsvExporter
{
    public byte[] Export<T>(IEnumerable<T> rows, string delimiter = ";")
    {
        var cfg = new CsvConfiguration(new CultureInfo("sr-Latn-RS"))
        {
            Delimiter = delimiter,
            HasHeaderRecord = true
        };

        using var ms = new MemoryStream();
        // UTF-8 sa BOM da Excel korektno prepozna encoding
        using (var writer = new StreamWriter(ms, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, cfg))
        {
            csv.WriteRecords(rows);
        }
        return ms.ToArray();
    }
}
```

## Kontroler

`src/BizSMS.Web/Controllers/ReportController.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.Reports;
using BizSMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers;

[Authorize]
public sealed class ReportController : Controller
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string CsvMime  = "text/csv; charset=utf-8";

    private readonly IReportService _reports;
    private readonly IExcelExporter _xl;
    private readonly ICsvExporter _csv;
    private readonly IAuditService _audit;

    public ReportController(IReportService reports, IExcelExporter xl, ICsvExporter csv, IAuditService audit)
        => (_reports, _xl, _csv, _audit) = (reports, xl, csv, audit);

    // GET /Report/MonthlyCost
    [HttpGet]
    public async Task<IActionResult> MonthlyCost(int? clientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (f, t) = DefaultRange(from, to);
        var report = await _reports.GetMonthlyCostAsync(clientId, f, t, ct);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyCostExport(int? clientId, DateOnly? from, DateOnly? to, string format, CancellationToken ct)
    {
        var (f, t) = DefaultRange(from, to);
        var report = await _reports.GetMonthlyCostAsync(clientId, f, t, ct);

        await _audit.LogAsync("ReportExport", "OK",
            new { report = "MonthlyCost", clientId, from = f, to = t, format }, ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = _csv.Export(report.Rows);
            return File(bytes, CsvMime, FileName("mesecni-troskovi", f, t, "csv"));
        }
        else
        {
            var bytes = _xl.ExportMonthlyCost(report);
            return File(bytes, XlsxMime, FileName("mesecni-troskovi", f, t, "xlsx"));
        }
    }

    [HttpGet]
    public async Task<IActionResult> SendActivity(int? clientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (f, t) = DefaultRange(from, to);
        var report = await _reports.GetSendActivityAsync(clientId, f, t, ct);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> SendActivityExport(int? clientId, DateOnly? from, DateOnly? to, string format, CancellationToken ct)
    {
        var (f, t) = DefaultRange(from, to);
        var report = await _reports.GetSendActivityAsync(clientId, f, t, ct);
        await _audit.LogAsync("ReportExport", "OK",
            new { report = "SendActivity", clientId, from = f, to = t, format }, ct);
        var bytes = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? _csv.Export(report.Rows)
            : _xl.ExportSendActivity(report);
        var mime = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? CsvMime : XlsxMime;
        var ext  = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv"    : "xlsx";
        return File(bytes, mime, FileName("aktivnost-slanja", f, t, ext));
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.Admin)]
    public async Task<IActionResult> ClientArchive(int clientId, CancellationToken ct)
    {
        var report = await _reports.GetClientArchiveAsync(clientId, ct);
        return View(report);
    }

    private static (DateOnly, DateOnly) DefaultRange(DateOnly? from, DateOnly? to)
    {
        var t = to ?? DateOnly.FromDateTime(DateTime.Today);
        var f = from ?? new DateOnly(t.Year, t.Month, 1);
        if (f > t) throw new ArgumentException("Period 'od' mora biti pre perioda 'do'.");
        return (f, t);
    }

    private static string FileName(string prefix, DateOnly from, DateOnly to, string ext)
        => $"{prefix}_{from:yyyyMMdd}-{to:yyyyMMdd}.{ext}";
}
```

## Autorizacija — „vidi samo svog klijenta“

`ReportService.EnsureAccess(...)` je „server-side“ zaštita — čak i ako korisnik ručno pošalje
`?clientId=42`, servis odbija poziv. To je važnije od bilo koje UI blokade.

Ako želiš i **UI blokadu**, sakrij dropdown za izbor klijenta osim za administratora:

```cshtml
@if (User.IsInRole(Roles.Administrator))
{
    <select asp-for="ClientId" asp-items="Model.AvailableClients"></select>
}
```

## Štampa — CSS obrazac

U `_Layout.cshtml`:

```cshtml
<environment names="Development,Production">
    <link rel="stylesheet" href="~/css/print.css" media="print" />
</environment>
```

`wwwroot/css/print.css`:

```css
@media print {
    .no-print, nav, footer, form.search-panel, .btn { display: none !important; }
    body { font-size: 11px; color: #000; background: #fff; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border: 1px solid #333; padding: 4px 6px; }
    thead { display: table-header-group; }
    tr, td { page-break-inside: avoid; }
    h1, h2 { font-size: 14px; }
}
```

Dugme „Štampa“:

```html
<button type="button" class="btn btn-secondary no-print" onclick="window.print()">Štampa</button>
```

## Before / After — legacy export

**Legacy** (obično koristio EPPlus 4.x sa `LicenseContext.NonCommercial`):

```csharp
public FileContentResult ExportMonthly(int? clientId)
{
    using (var pkg = new ExcelPackage())
    {
        var ws = pkg.Workbook.Worksheets.Add("Report");
        ws.Cells["A1"].Value = "Klijent";
        // ...
        return File(pkg.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "report.xlsx");
    }
}
```

**.NET 10** koristi async servis + injektovani exporter (v. gore). Prednosti:

- Servisni sloj testabilan bez EPPlus zavisnosti.
- Konzistentno naimenovanje fajlova i autorizacije.
- Audit svakog exporta (ko/kad/koji parametri).

## Streaming velikih izveštaja

Za >100k redova, ne kreiraj `byte[]` (memorijski trošak). Vraćaj `FileStreamResult` sa
`ClosedXML.Excel.XLWorkbook.SaveAs(ms)` u pipe:

```csharp
public IActionResult BigExport(...)
{
    var stream = new MemoryStream();   // ClosedXML ne podržava strict streaming — v. napomena
    var report = _reports.GetMonthlyCost(...);
    _xl.WriteMonthlyCost(report, stream);
    stream.Position = 0;
    return new FileStreamResult(stream, XlsxMime) { FileDownloadName = "..." };
}
```

Napomena: ClosedXML drži ceo dokument u memoriji. Za **stotine hiljada** redova razmotri
`OpenXmlWriter` iz `DocumentFormat.OpenXml` — dokumentovano ali kompleksnije.

## Rate limiting za export (opciono)

```csharp
services.AddRateLimiter(o =>
{
    o.AddPolicy("report-export", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.User.Identity?.Name ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

app.UseRateLimiter();

[EnableRateLimiting("report-export")]
[HttpGet]
public async Task<IActionResult> MonthlyCostExport(...) { ... }
```

## Checklist za code review

- [ ] Ne postoji SQL/EF poziv za izveštaje unutar kontrolera — sve u `ReportService`.
- [ ] `ReportService` proverava `ITenantContext` i blokira cross-tenant pristup.
- [ ] Excel export vraća pravi `Content-Type` i `Content-Disposition` filename.
- [ ] CSV fajl je UTF-8 sa BOM (Excel neće rušiti dijakritike).
- [ ] Nema `EPPlus` licence exception-a (koristi ClosedXML ili plati EPPlus licencu).
- [ ] Datumi u UI se prikazuju u `sr-Latn-RS` kulturi; u fajlu u ISO ili SR formatu (dogovoreno).
- [ ] Audit log-uje svaki export sa parametrima.
- [ ] Endpoint prihvata i `csv` i `xlsx` kao alternative kroz `?format=`.

## Najčešće greške i kako ih izbeći

1. **EPPlus 5+ bez licence konteksta** — baca u produkciji izuzetak. Ili koristi ClosedXML,
   ili konfiguriši `ExcelPackage.LicenseContext = LicenseContext.NonCommercial`/`Commercial`.
2. **CSV bez BOM-a** — Excel ne prepoznaje UTF-8, prikazuje krakozjablje. Koristi
   `new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)`.
3. **Sinhroni EF pozivi u exportu** — `ToList()` umesto `ToListAsync()` blokira Kestrel niti;
   koristi async.
4. **Ne odseći range** — server bez granice može generisati Excel od 500MB. Ograniči na razuman
   range ili prisili grupu / batch.
5. **Vraćanje HTML-a umesto fajla** — ako se error dogodi, korisnik dobija exception page unutar
   fajla. Uvek try/catch pre poziva `File(...)`.
6. **Ignorisanje `ClientId` claim-a** — bez server-side zaštite, korisnik menja `?clientId=` u URL-u
   i vidi tuđe brojeve.
7. **Ignorisanje memorije** — za velike izveštaje čuvaj rezultat u temp fajl i vraćaj `PhysicalFile`
   sa cleanup-om na `Response.OnCompleted`.
