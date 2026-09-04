## Svrha
Migracija upload i validacionog toka za CSV/Excel sa pravilima formata i prikazom grešaka.

## Koraci migracije
1. Izdvojiti parser/validator iz kontrolera (`GroupController`, `AdminManageController`).
2. Podržati CSV i Excel, sa validacijom header-a.
3. Validirati broj formatom `06XXXXXXXX` (tačno 10 cifara, po zahtevu).
4. Vratiti listu grešaka po redu (`row`, `column`, `message`).

## Before/After primer
### Before (legacy regex)
```csharp
Match m = Regex.Match(numberFromFile, @"^(06\d{7,8})$", RegexOptions.IgnoreCase);
if (!m.Success)
{
    badNumbers += numberFromFile + ", ";
}
```

### After (stroga validacija 06XXXXXXXX)
```csharp
private static readonly Regex SrbMobileRegex = new(@"^06\d{8}$", RegexOptions.Compiled);

public static bool IsValidMsisdn(string value)
    => !string.IsNullOrWhiteSpace(value) && SrbMobileRegex.IsMatch(value.Trim());
```

## Code snippets
### DTO za greške
```csharp
public sealed record UploadValidationError(int Row, string Field, string Message);

public sealed record UploadValidationResult(
    IReadOnlyList<ImportRowDto> ValidRows,
    IReadOnlyList<UploadValidationError> Errors);
```

### CSV parser + header check
```csharp
using Microsoft.VisualBasic.FileIO;

public UploadValidationResult ParseCsv(Stream fileStream)
{
    using var parser = new TextFieldParser(fileStream, Encoding.UTF8)
    {
        TextFieldType = FieldType.Delimited,
        HasFieldsEnclosedInQuotes = true
    };
    parser.SetDelimiters(",");

    var headerFields = parser.ReadFields() ?? Array.Empty<string>();
    if (headerFields.Length < 2 ||
        !string.Equals(headerFields[0].Trim(), "Number", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(headerFields[1].Trim(), "Name", StringComparison.OrdinalIgnoreCase))
    {
        return new UploadValidationResult(Array.Empty<ImportRowDto>(),
            new[] { new UploadValidationError(1, "Header", "Očekivan header: Number,Name") });
    }

    var valid = new List<ImportRowDto>();
    var errors = new List<UploadValidationError>();
    var row = 1;

    while (!parser.EndOfData)
    {
        row++;
        var parts = parser.ReadFields() ?? Array.Empty<string>();
        if (parts.Length < 2)
        {
            errors.Add(new UploadValidationError(row, "Row", "Nedostaju kolone Number/Name"));
            continue;
        }

        var number = parts[0].Trim();
        var name = parts[1].Trim();

        if (!IsValidMsisdn(number))
        {
            errors.Add(new UploadValidationError(row, "Number", $"Neispravan format: {number}"));
            continue;
        }

        valid.Add(new ImportRowDto(number, name));
    }

    return new UploadValidationResult(valid, errors);
}
```

### Excel (.xlsx) parser primer
```csharp
using OfficeOpenXml;

public UploadValidationResult ParseExcel(Stream fileStream)
{
    using var package = new ExcelPackage(fileStream);
    var ws = package.Workbook.Worksheets.First();

    if (ws.Dimension is null)
    {
        return new UploadValidationResult(Array.Empty<ImportRowDto>(),
            new[] { new UploadValidationError(1, "Sheet", "Excel fajl je prazan.") });
    }

    var header1 = ws.Cells[1, 1].Text?.Trim();
    var header2 = ws.Cells[1, 2].Text?.Trim();
    if (!string.Equals(header1, "Number", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(header2, "Name", StringComparison.OrdinalIgnoreCase))
    {
        return new UploadValidationResult(Array.Empty<ImportRowDto>(),
            new[] { new UploadValidationError(1, "Header", "Očekivan header: Number,Name") });
    }

    var valid = new List<ImportRowDto>();
    var errors = new List<UploadValidationError>();
    for (var row = 2; row <= ws.Dimension.End.Row; row++)
    {
        var number = ws.Cells[row, 1].Text?.Trim() ?? string.Empty;
        var name = ws.Cells[row, 2].Text?.Trim() ?? string.Empty;

        if (!IsValidMsisdn(number))
        {
            errors.Add(new UploadValidationError(row, "Number", $"Neispravan format: {number}"));
            continue;
        }

        valid.Add(new ImportRowDto(number, name));
    }

    return new UploadValidationResult(valid, errors);
}
```

### Kontroler prikaz grešaka
```csharp
using System.IO.Compression;

[HttpPost]
[Authorize(Roles = "Administrator,BusinessUser")]
public IActionResult Upload(IFormFile file)
{
    var ext = Path.GetExtension(file.FileName);
    var allowed = new[] { ".csv", ".xlsx" };
    if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
        return BadRequest("Podržani formati su samo .csv i .xlsx");

    var allowedContentTypes = new[]
    {
        "text/csv",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
    {
        // advisory check: ne odbacuj odmah jer neki klijenti šalju generički MIME
        Console.WriteLine($"Neočekivan Content-Type: {file.ContentType}");
    }

    using var stream = file.OpenReadStream();
    if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) && !LooksLikeXlsx(stream))
        return BadRequest("Neispravan XLSX format.");
    stream.Position = 0;

    var result = ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        ? _uploadService.ParseExcel(stream)
        : _uploadService.ParseCsv(stream);

    if (result.Errors.Count > 0)
        return BadRequest(result.Errors);

    return Ok(result.ValidRows);
}

private static bool LooksLikeXlsx(Stream stream)
{
    try
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbook = archive.GetEntry("xl/workbook.xml");
        var contentTypes = archive.GetEntry("[Content_Types].xml");
        return workbook is not null && contentTypes is not null;
    }
    catch (InvalidDataException)
    {
        return false;
    }
}
```

## Checklist za code review
- [ ] Header validacija postoji.
- [ ] Regex je usklađen sa zahtevom `06XXXXXXXX`.
- [ ] Greške su granularne po redu i polju.
- [ ] Duplikati i postojeći brojevi se tretiraju po poslovnom pravilu.

## Najčešće greške i kako ih izbeći
- Prihvatanje praznih redova kao validnih.
- Korišćenje labavog regex-a koji propušta nedozvoljene formate.
