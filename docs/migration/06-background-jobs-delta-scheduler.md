## Svrha
Detaljan plan za background obradu: dnevna delta sinhronizacija (SP-only), ručno okidanje od admina i obrada zakazanih poruka.

## Koraci migracije
1. Izabrati scheduler: `IHostedService`, Quartz ili Hangfire.
2. Implementirati `IDeltaSyncService` koji koristi postojeću SQL SP.
3. Implementirati diff algoritam (SP rezultat vs `BST_NUMBERS`) sa transakcijom.
4. Dodati audit događaje: start/end, added/updated/deactivated/errors.
5. Dodati admin endpoint za ručno pokretanje delte.
6. Migrirati obradu zakazanih poruka iz Hangfire flow-a.

## Tradeoff-i (sažeto)
- `IHostedService`: najjednostavniji, bez dodatne infrastrukture, slabiji retry/dashboard.
- `Quartz`: jak scheduling i triggeri, više konfiguracije.
- `Hangfire`: poznat u legacy (`BackgroundJob.Enqueue/Schedule`), dashboard + retry, zahteva job storage.

## Before/After primer
### Before (legacy SP i scheduling)
```csharp
using (var command = new SqlCommand("sp_RefreshNumbers", conn))
{
    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.Add("@nContractID", SqlDbType.VarChar).Value = contractId;
    command.ExecuteNonQuery();
}

var hangfireId = BackgroundJob.Schedule(() => sms.StartSendSMS(user, data.Alphanumeric, messageId, clientID), DateTime.Parse(data.ScheduledDateTime));
```

### After (.NET 10 servis + job)
```csharp
public sealed class DailyDeltaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyDeltaWorker(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var next = now.Date.AddDays(1).AddHours(2); // 02:00 dnevno
            await Task.Delay(next - now, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeltaSyncService>();
            await svc.RunFullDeltaAsync(stoppingToken);
        }
    }
}
```

## Code snippets
### (a) SP poziv varijanta 1: EF Core + DbCommand/ADO.NET
```csharp
public async Task<List<VpnSpRow>> GetVpnRowsViaCommandAsync(string contractId, CancellationToken ct)
{
    await using var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync(ct);

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "dbo.sp_RefreshNumbers";
    cmd.CommandType = CommandType.StoredProcedure;

    var p = cmd.CreateParameter();
    p.ParameterName = "@nContractID";
    p.Value = contractId;
    cmd.Parameters.Add(p);

    var rows = new List<VpnSpRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        rows.Add(new VpnSpRow
        {
            Number = reader.GetString(reader.GetOrdinal("Number")),
            ClientId = reader.GetInt32(reader.GetOrdinal("Client_ID"))
        });
    }
    return rows;
}
```

### (b) SP poziv varijanta 2: EF Core raw SQL (`FromSql`)
```csharp
[Keyless]
public sealed class VpnSpRow
{
    public int ClientId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
}

public async Task<List<VpnSpRow>> GetVpnRowsViaFromSqlAsync(string contractId, CancellationToken ct)
{
    return await _db.Set<VpnSpRow>()
        .FromSqlInterpolated($"EXEC dbo.sp_RefreshNumbers @nContractID={contractId}")
        .AsNoTracking()
        .ToListAsync(ct);
}
```

### Diff algoritam (UPSERT + deaktivacija)
```csharp
public async Task<DeltaResult> ApplyDeltaAsync(int clientId, IReadOnlyCollection<VpnSpRow> spRows, CancellationToken ct)
{
    var result = new DeltaResult();
    await using var tx = await _db.Database.BeginTransactionAsync(ct);

    var current = await _db.Numbers
        .Where(n => n.ClientID == clientId && n.Active)
        .ToDictionaryAsync(n => n.Number, ct);

    var incoming = spRows.Select(x => x.Number).ToHashSet(StringComparer.Ordinal);

    foreach (var number in incoming)
    {
        if (current.TryGetValue(number, out var existing))
        {
            // idempotent update ako treba
            if (!existing.SendAllowed)
            {
                existing.SendAllowed = true;
                result.Updated++;
            }
        }
        else
        {
            _db.Numbers.Add(new NumbersModel
            {
                Number = number,
                ClientID = clientId,
                Active = true,
                SendAllowed = true,
                InsertDate = DateTime.UtcNow
            });
            result.Inserted++;
        }
    }

    var toDeactivate = current.Values.Where(x => !incoming.Contains(x.Number)).ToList();
    foreach (var n in toDeactivate)
    {
        n.Active = false;
        result.Deactivated++;
    }

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return result;
}
```

### Audit za delta job
```csharp
await _audit.LogAsync("DELTA_START", new { ClientId = clientId, CorrelationId = correlationId });
try
{
    var res = await ApplyDeltaAsync(clientId, rows, ct);
    await _audit.LogAsync("DELTA_END", new { ClientId = clientId, res.Inserted, res.Updated, res.Deactivated });
}
catch (Exception ex)
{
    await _audit.LogAsync("DELTA_ERROR", new
    {
        ClientId = clientId,
        CorrelationId = correlationId,
        ErrorType = ex.GetType().Name
    });
    throw;
}
```

### (b) Ručno okidanje od admina
```csharp
[Authorize(Roles = "Administrator")]
[HttpPost]
public async Task<IActionResult> RunDelta([FromBody] RunDeltaRequest request, CancellationToken ct)
{
    var result = await _deltaSyncService.RunForClientAsync(request.ClientId, ct);
    return Ok(result);
}
```

### (c) Obrada zakazanih poruka
```csharp
public async Task ProcessScheduledMessagesAsync(CancellationToken ct)
{
    var due = await _db.ScheduledSms
        .Where(s => s.CancelDate == null)
        .Join(_db.Message.Where(m => m.Status == (int)MessageStatus.Scheduled &&
                                     m.SendDate <= DateTime.UtcNow),
              s => s.MessageID,
              m => m.MessageID,
              (s, m) => s)
        .ToListAsync(ct);

    foreach (var item in due)
    {
        await _messageSender.SendMessageAsync(item.MessageID, ct);
    }
}
```

## Checklist za code review
- [ ] Delta koristi isključivo postojeću SQL SP integraciju.
- [ ] Implementirane su obe SP varijante (DbCommand i FromSql).
- [ ] Diff je idempotentan.
- [ ] Sve kritične operacije su u transakciji.
- [ ] Postoje audit zapisi za start/end/error.
- [ ] Admin ručni trigger je role-protected.

## Najčešće greške i kako ih izbeći
- Korišćenje spoljnog CRM API-ja umesto SQL SP (nije dozvoljeno).
- Deaktivacija brojeva bez scoping-a po klijentu/ugovoru.
- Nema zaštite od paralelnog pokretanja istog delta job-a.
