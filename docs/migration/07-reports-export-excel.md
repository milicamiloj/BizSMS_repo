## Svrha
Migracija izveštaja i export-a (Excel/CSV fallback) kroz servisni sloj i autorizovane endpoint-e.

## Koraci migracije
1. Izdvojiti report logiku iz kontrolera (`ReportController`, `HomeController`) u `IReportService`.
2. Obezbediti role i client scoping.
3. Dodati export endpoint (`.xlsx` ili CSV fallback).
4. Zadržati postojeće obračune (mesečni troškovi, poslato/zakazano).

## Before/After primer
### Before (legacy `ReportController`)
```csharp
var model = (from numbers in db.Numbers
             join numbers_messages in db.MessagesNumbers
             on numbers.NumberID equals numbers_messages.NumberID
             where numbers.ClientID == client.ClientID && numbers_messages.Sent == true
             group numbers_messages by new { numbers_messages.NumberTypeID, numbers_messages.Charged, numbers_messages.SendDate.Month, numbers_messages.SendDate.Year }
             into report
             select new { ... }).ToList();
```

### After (servis)
```csharp
public async Task<IReadOnlyList<SentSmsReportDto>> GetMonthlyReportAsync(int clientId, CancellationToken ct)
{
    return await _db.MessagesNumbers
        .Where(x => x.NumbersModel.ClientID == clientId && x.Sent)
        .GroupBy(x => new { x.NumberTypeID, x.Charged, x.SendDate.Year, x.SendDate.Month })
        .Select(g => new SentSmsReportDto
        {
            Year = g.Key.Year,
            Month = g.Key.Month,
            NumberTypeId = g.Key.NumberTypeID,
            SentCount = g.Count(x => x.Sent),
            DeliveredCount = g.Count(x => x.Delivered == 1)
        })
        .ToListAsync(ct);
}
```

## Code snippets
### Autorizovani endpoint
```csharp
[Authorize(Roles = "Administrator,BusinessUser")]
public async Task<IActionResult> MonthlyReport(int clientId, CancellationToken ct)
{
    var allowedClientId = ResolveClientScope(clientId);
    var data = await _reportService.GetMonthlyReportAsync(allowedClientId, ct);
    return View(data);
}

private int ResolveClientScope(int requestedClientId)
{
    if (!User.IsInRole("BusinessUser"))
        return requestedClientId;

    var ownClientId = int.Parse(User.FindFirst("client_id")!.Value);
    if (ownClientId != requestedClientId)
        throw new UnauthorizedAccessException("BusinessUser nema pristup drugom klijentu.");

    return ownClientId;
}
```

### Excel (EPPlus) + CSV fallback
```csharp
public byte[] BuildExcelWithEpplus(IEnumerable<SentSmsReportDto> rows)
{
    using var p = new ExcelPackage();
    var ws = p.Workbook.Worksheets.Add("Report");
    ws.Cells[1, 1].Value = "Year";
    ws.Cells[1, 2].Value = "Month";
    ws.Cells[1, 3].Value = "Sent";
    ws.Cells[1, 4].Value = "Delivered";

    var r = 2;
    foreach (var row in rows)
    {
        ws.Cells[r, 1].Value = row.Year;
        ws.Cells[r, 2].Value = row.Month;
        ws.Cells[r, 3].Value = row.SentCount;
        ws.Cells[r, 4].Value = row.DeliveredCount;
        r++;
    }

    return p.GetAsByteArray();
}

[Authorize(Roles = "Administrator,BusinessUser")]
public async Task<IActionResult> ExportExcel(int clientId, CancellationToken ct)
{
    var allowedClientId = ResolveClientScope(clientId);
    var rows = await _reportService.GetMonthlyReportAsync(allowedClientId, ct);
    var bytes = BuildExcelWithEpplus(rows);
    return File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"bizsms-report-{allowedClientId}.xlsx");
}

public byte[] BuildCsv(IEnumerable<SentSmsReportDto> rows)
{
    var sb = new StringBuilder();
    sb.AppendLine("Year,Month,Sent,Delivered");
    foreach (var r in rows)
        sb.AppendLine($"{r.Year},{r.Month},{r.SentCount},{r.DeliveredCount}");
    return Encoding.UTF8.GetBytes(sb.ToString());
}

[Authorize(Roles = "Administrator,BusinessUser")]
public async Task<IActionResult> ExportCsv(int clientId, CancellationToken ct)
{
    var allowedClientId = ResolveClientScope(clientId);
    var rows = await _reportService.GetMonthlyReportAsync(allowedClientId, ct);
    var bytes = _exportService.BuildCsv(rows);
    return File(bytes, "text/csv", $"bizsms-report-{allowedClientId}.csv");
}
```

```csharp
// Program.cs (jednom pri startup-u)
var epplusMode = builder.Configuration["Epplus:LicenseContext"] ?? "Commercial";
ExcelPackage.LicenseContext = Enum.Parse<LicenseContext>(epplusMode, ignoreCase: true);
```

## Checklist za code review
- [ ] Report query ostaje funkcionalno jednaka legacy logici.
- [ ] Endpointi su role-protected.
- [ ] Export ne curi podatke drugih klijenata.
- [ ] CSV fallback postoji i radi.

## Najčešće greške i kako ih izbeći
- Izveštaj bez tenant filtera (`ClientID`) -> data leak.
- Dupliranje obračuna po mesecu usled pogrešnog group key-a.
