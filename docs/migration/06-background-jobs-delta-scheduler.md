# 06 — Background jobs, delta sync (SP-only) i scheduler zakazanih poruka

## Svrha

Definisati **job sistem** za .NET 10, dati **dve varijante implementacije poziva postojeće SP
`sp_RefreshNumbers`** (ADO.NET i EF Core raw SQL), pokazati **diff/UPSERT algoritam** koji
poredi rezultat SP-a sa `BST_NUMBERS` tabelom, i pokazati kako se implementira **obrada
„Zakazanih“ SMS poruka** i **ručno okidanje delte** iz admin UI-a.

Ključni zahtev: **integracija sa CRM/Siebel ostaje isključivo preko postojeće SQL stored procedure**
(`sp_RefreshNumbers`). Nikakav direktan API poziv iz .NET aplikacije ka CRM/Siebel.

## Izbor tehnologije — trade-off

| Kriterijum                                 | `IHostedService` + `Channel<T>` | Hangfire                                  | Quartz.NET                               |
|--------------------------------------------|----------------------------------|-------------------------------------------|------------------------------------------|
| Persistencija poslova                      | Ne (in-memory)                   | Da (SQL Server / Redis)                   | Da (ADO.NET / RavenDB)                   |
| Dashboard / monitoring                     | Ne (samo logovi)                 | Da (`/hangfire`, prilagodljivo)           | Ne (Quartzmin je eksterni)               |
| Retries / dead letter                      | Ručno                            | Automatski (broj + backoff)               | Automatski (`SchedulerBuilder`)          |
| Cron schedule                              | Ručno + `PeriodicTimer`          | Da (`RecurringJob.AddOrUpdate("cron")`)   | Da (`CronScheduleBuilder`)               |
| Distribuisan izvršni model                 | Ne                               | Da (više workera, single lease po jobu)   | Da (clustered scheduler)                 |
| Zavisnost od DB šeme                        | Nema                             | Kreira sopstvene tabele u BizSMS bazi     | Kreira sopstvene tabele                  |
| Learning curve                             | Nizak                            | Nizak-srednji                             | Srednji-visok                            |
| Preporuka                                  | Za lightweight worker-ove        | **Za delta sync i „Zakazano“**            | Ako treba enterprise cron / calendar     |

**Preporučeni miks za BizSMS**:

- **Hangfire** kao primarni scheduler za: dnevnu delta sync (cron), obradu zakazanih SMS-ova
  (recurring), ručno okidanje admin komandi (fire-and-forget), i eventualne retry-je za SMS
  slanje.
- **`IHostedService`** samo za lightweight periodičan cleanup (npr. brisanje starih temp
  upload fajlova), tj. tamo gde ne treba persistencija.

## Registracija Hangfire-a

`src/BizSMS.Infrastructure/DependencyInjection.cs` (dodatak):

```csharp
public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration cfg)
{
    var connString = cfg.GetConnectionString("BizSms")!;

    services.AddHangfire(config => config
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connString, new Hangfire.SqlServer.SqlServerStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(5),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

    services.AddHangfireServer(o =>
    {
        o.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 8);
        o.Queues = new[] { "delta", "sms", "default" };
        o.ServerName = $"BizSMS-{Environment.MachineName}";
    });

    // Domenski job-ovi (DI konzumeri Hangfire-a)
    services.AddScoped<IDeltaSyncJob, DeltaSyncJob>();
    services.AddScoped<IScheduledSmsProcessorJob, ScheduledSmsProcessorJob>();

    return services;
}
```

`Program.cs` (dodatak):

```csharp
app.UseHangfireDashboard("/admin/jobs", new Hangfire.DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthorization() }, // custom, samo Administrator
    IsReadOnlyFunc = _ => false,
    DisplayStorageConnectionString = false
});

using (var scope = app.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<Hangfire.IRecurringJobManager>();

    recurring.AddOrUpdate<IDeltaSyncJob>(
        recurringJobId: "delta-sync-daily",
        methodCall: j => j.RunAllClientsAsync(CancellationToken.None),
        cronExpression: builder.Configuration["Delta:CronExpression"] ?? "0 0 3 * * ?",
        options: new Hangfire.RecurringJobOptions { TimeZone = TimeZoneInfo.Local, Queue = "delta" });

    recurring.AddOrUpdate<IScheduledSmsProcessorJob>(
        recurringJobId: "scheduled-sms",
        methodCall: j => j.ProcessDueAsync(CancellationToken.None),
        cronExpression: "*/1 * * * *",  // svaki minut
        options: new Hangfire.RecurringJobOptions { TimeZone = TimeZoneInfo.Local, Queue = "sms" });
}
```

`HangfireAdminAuthorization`:

```csharp
public sealed class HangfireAdminAuthorization : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User?.Identity?.IsAuthenticated == true
               && http.User.IsInRole(BizSMS.Infrastructure.Identity.Roles.Administrator);
    }
}
```

## Delta sync — arhitektura

```
+-----------------------------------------------------+
|  IDeltaSyncJob (Hangfire recurring @ 03:00 daily)   |
|-----------------------------------------------------|
| 1. Za svaki aktivan ClientContract (Is_Canceled=0): |
|    - poziv IDeltaSyncRepository.RefreshAsync(cid)   |
|      (u okviru execution strategy + tx)             |
| 2. Sakupi statistiku (added / deactivated / errors) |
| 3. Audit log START + END po klijentu               |
+---------------------+-------------------------------+
                      |
                      v
+-----------------------------------------------------+
| IDeltaSyncRepository.RefreshAsync(contractId)       |
|-----------------------------------------------------|
|  Varijanta A: ADO.NET DbCommand + OUTPUT parametar  |
|  Varijanta B: EF Core FromSqlInterpolated + ExecSql |
+---------------------+-------------------------------+
                      |
                      v
+-----------------------------------------------------+
|          SQL Server: sp_RefreshNumbers              |
|  (nepromenjeno; svu logiku odrađuje SP internally)  |
+-----------------------------------------------------+
```

`sp_RefreshNumbers` iz repozitorijuma:

- ulaz: `@nContractID NVARCHAR(50)`
- izlaz: `@iAffectedNumbersCount INT OUTPUT`
- unutar SP-a se radi UPSERT/deaktivacija u `BST_NUMBERS` tabeli i log u `BST_LOG`.

**Znači deo logike UPSERT-a je već u SP-u**. Naš job **ne dupla** taj rad — samo pripremi listu
ugovora, pozove SP po ugovoru, sakupi rezultate i audituje. Diff/UPSERT algoritam iz sekcije
niže je za slučaj kad `sp_RefreshNumbers` vraća „raw“ tabelu bez pisanja, tj. za nove SP-ove
(npr. `sp_ListVpnNumbersForContract`) — dat je jer je zahtev tražio kompletan uputni obrazac.

## Varijanta A — ADO.NET (DbConnection/DbCommand/DbDataReader)

Ova varijanta se koristi kada:

- SP ima **OUTPUT parametre** (kao naš `@iAffectedNumbersCount`),
- treba fina kontrola nad tipovima i timeout-om,
- SP vraća više result set-ova.

`src/BizSMS.Application/Abstractions/IDeltaSyncRepository.cs`:

```csharp
namespace BizSMS.Application.Abstractions;

public interface IDeltaSyncRepository
{
    Task<DeltaSyncOutcome> RefreshAsync(string contractId, CancellationToken ct);
    Task<IReadOnlyList<string>> ListVpnNumbersAsync(string contractId, CancellationToken ct);
}

public sealed record DeltaSyncOutcome(int AffectedNumbers, TimeSpan Duration);
```

`src/BizSMS.Infrastructure/DeltaSync/DeltaSyncRepository.cs`:

```csharp
using System.Data;
using System.Diagnostics;
using BizSMS.Application.Abstractions;
using BizSMS.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizSMS.Infrastructure.DeltaSync;

internal sealed class DeltaSyncRepository : IDeltaSyncRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeltaSyncRepository> _log;

    public DeltaSyncRepository(AppDbContext db, ILogger<DeltaSyncRepository> log)
        => (_db, _log) = (db, log);

    public async Task<DeltaSyncOutcome> RefreshAsync(string contractId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        var sw = Stopwatch.StartNew();
        var conn = (SqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "dbo.sp_RefreshNumbers";
        cmd.CommandTimeout = 300; // 5 min; SP može biti spor

        cmd.Parameters.Add(new SqlParameter("@nContractID", SqlDbType.NVarChar, 50) { Value = contractId });
        var outParam = new SqlParameter("@iAffectedNumbersCount", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outParam);

        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        cmd.Transaction = (SqlTransaction)tx;

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            var affected = outParam.Value is DBNull ? 0 : Convert.ToInt32(outParam.Value);
            await tx.CommitAsync(ct);

            _log.LogInformation("Delta sync for contract {Contract} completed. Affected={Affected}", contractId, affected);
            return new DeltaSyncOutcome(affected, sw.Elapsed);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.LogError(ex, "Delta sync for contract {Contract} failed", contractId);
            throw;
        }
    }

    // Varijanta koja vraća samo listu brojeva iz SP-a (npr. novi SP koji ne piše ništa)
    public async Task<IReadOnlyList<string>> ListVpnNumbersAsync(string contractId, CancellationToken ct)
    {
        var conn = (SqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "dbo.sp_ListVpnNumbersForContract";
        cmd.CommandTimeout = 120;

        cmd.Parameters.Add(new SqlParameter("@nContractID", SqlDbType.NVarChar, 50) { Value = contractId });

        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetString(reader.GetOrdinal("BROJ_TELEFONA")));
        }
        return result;
    }
}
```

## Varijanta B — EF Core raw SQL (`FromSql` / `ExecuteSql`)

Ova varijanta se koristi kada:

- SP vraća **jedan result set** koji se lepo mapira na entitet / keyless entity,
- Nema OUTPUT parametara ili nam nisu presudno važni,
- Želimo da rezultat direktno uleti u EF Core query pipeline (npr. za dalje filtriranje/join-ovanje).

Za slučaj **poziv sa OUTPUT parametrom + bez result set-a** koristimo `ExecuteSqlRawAsync`:

```csharp
public async Task<int> RefreshViaEfAsync(string contractId, CancellationToken ct)
{
    var outParam = new SqlParameter("@iAffectedNumbersCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

    await _db.Database.ExecuteSqlRawAsync(
        "EXEC dbo.sp_RefreshNumbers @nContractID = {0}, @iAffectedNumbersCount = {1} OUTPUT",
        parameters: new object[] { contractId, outParam },
        cancellationToken: ct);

    return outParam.Value is DBNull ? 0 : Convert.ToInt32(outParam.Value);
}
```

Za slučaj **SP koji vraća listu brojeva** mapirano na keyless entity:

```csharp
[Keyless]
public sealed class VpnNumberRow
{
    public string PRODAJNI_UGOVOR_ID { get; set; } = default!;
    public string BROJ_TELEFONA { get; set; } = default!;
    public string AKCIJA { get; set; } = default!;
    public DateTime DATUM { get; set; }
}

// U AppDbContext:
public DbSet<VpnNumberRow> VpnRows => Set<VpnNumberRow>();
protected override void OnModelCreating(ModelBuilder mb)
{
    base.OnModelCreating(mb);
    mb.Entity<VpnNumberRow>().HasNoKey().ToView(null);
}

// U repository:
public async Task<IReadOnlyList<VpnNumberRow>> ListVpnRowsAsync(string contractId, CancellationToken ct)
    => await _db.VpnRows
        .FromSqlInterpolated($"EXEC dbo.sp_ListVpnNumbersForContract @nContractID = {contractId}")
        .AsNoTracking()
        .ToListAsync(ct);
```

**Kada NE koristiti EF Core raw**: kad SP vraća **više** result set-ova → tada obavezno ADO.NET
(`reader.NextResultAsync`). EF Core `FromSql` mapira samo prvi result set.

## Diff / UPSERT algoritam (ako SP vraća samo listu)

Ovo je „C# implementacija“ istog algoritma koji trenutno radi `sp_RefreshNumbers` (za slučaj da
budući SP samo vraća listu bez pisanja). Radi na parovima: „VPN skup iz SP-a“ ↔ „BST_NUMBERS
skup u aplikaciji za taj klijent+ugovor+VPN tip“.

```csharp
public sealed class NumberSyncPlan
{
    public required IReadOnlyList<string> ToAdd { get; init; }
    public required IReadOnlyList<string> ToDeactivate { get; init; }
    public required IReadOnlyList<string> Unchanged { get; init; }
}

public static class NumberSyncPlanner
{
    public static NumberSyncPlan Build(
        IEnumerable<string> desiredFromCrm,
        IEnumerable<string> currentActiveInBizSms)
    {
        var desired = new HashSet<string>(desiredFromCrm, StringComparer.Ordinal);
        var current = new HashSet<string>(currentActiveInBizSms, StringComparer.Ordinal);

        return new NumberSyncPlan
        {
            ToAdd        = desired.Except(current).ToArray(),
            ToDeactivate = current.Except(desired).ToArray(),
            Unchanged    = desired.Intersect(current).ToArray()
        };
    }
}
```

Primena u repository sloju (transakcijski, idempotentno):

```csharp
public async Task<DeltaSyncOutcome> ApplyPlanAsync(int clientId, string contractId, NumberSyncPlan plan, CancellationToken ct)
{
    var strategy = _db.Database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync(async () =>
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // UPSERT (samo INSERT za nove — VPN brojevi ne menjaju svoju semantiku)
        if (plan.ToAdd.Count > 0)
        {
            var toInsert = plan.ToAdd.Select(n => new NumbersModel
            {
                Number = n,
                SendAllowed = true,
                CheckDate = DateTime.UtcNow,
                NumberTypeID = 1,               // VPN
                ClientID = clientId,
                Active = true,
                InsertDate = DateTime.UtcNow,
                ContractID = contractId
            }).ToList();
            _db.Numbers.AddRange(toInsert);
        }

        // Deaktivacija (nikad brisanje istorijske veze)
        if (plan.ToDeactivate.Count > 0)
        {
            await _db.Numbers
                .Where(n => n.ClientID == clientId
                            && n.ContractID == contractId
                            && n.NumberTypeID == 1
                            && n.Active
                            && plan.ToDeactivate.Contains(n.Number))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(n => n.Active, false)
                    .SetProperty(n => n.CheckDate, DateTime.UtcNow), ct);
        }

        // Timestamp na ugovoru
        await _db.ClientContracts
            .Where(c => c.ContractId == contractId && c.ClientId == clientId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.SynchronizationDate, DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new DeltaSyncOutcome(plan.ToAdd.Count + plan.ToDeactivate.Count, TimeSpan.Zero);
    });
}
```

**Idempotentnost**: ako se plan primeni dva puta u istom danu, `ExecuteUpdate` će pogoditi 0
redova drugi put; `AddRange` sa istim brojevima će pokušati insert i baciti unique constraint —
zato pre pravljenja plana svaki put povuci **sveži** current skup:

```csharp
var current = await _db.Numbers.AsNoTracking()
    .Where(n => n.ClientID == clientId && n.ContractID == contractId && n.Active && n.NumberTypeID == 1)
    .Select(n => n.Number)
    .ToListAsync(ct);
```

## Job orkestrator: DeltaSyncJob

`src/BizSMS.Application/DeltaSync/IDeltaSyncJob.cs`:

```csharp
namespace BizSMS.Application.DeltaSync;

public interface IDeltaSyncJob
{
    Task RunAllClientsAsync(CancellationToken ct);
    Task RunSingleContractAsync(string contractId, string triggeredBy, CancellationToken ct);
}
```

`src/BizSMS.Infrastructure/DeltaSync/DeltaSyncJob.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.DeltaSync;
using BizSMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizSMS.Infrastructure.DeltaSync;

internal sealed class DeltaSyncJob : IDeltaSyncJob
{
    private readonly AppDbContext _db;
    private readonly IDeltaSyncRepository _repo;
    private readonly IAuditService _audit;
    private readonly ILogger<DeltaSyncJob> _log;

    public DeltaSyncJob(AppDbContext db, IDeltaSyncRepository repo, IAuditService audit, ILogger<DeltaSyncJob> log)
        => (_db, _repo, _audit, _log) = (db, repo, audit, log);

    public async Task RunAllClientsAsync(CancellationToken ct)
    {
        await _audit.LogAsync("DeltaJobStart", "OK", new { Scope = "AllClients" }, ct);

        var contracts = await _db.ClientContracts
            .AsNoTracking()
            .Where(c => !c.IsCanceled)
            .Select(c => new { c.ContractId, c.ClientId })
            .ToListAsync(ct);

        var added = 0; var deactivated = 0; var errors = 0;

        foreach (var c in contracts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var outcome = await _repo.RefreshAsync(c.ContractId, ct);
                added += outcome.AffectedNumbers;   // SP kombinuje add+deactivate u jedan count;
                                                    // ako je potreban raspored, dodaj OUTPUT param
                await _audit.LogAsync("DeltaContractDone", "OK",
                    new { c.ContractId, outcome.AffectedNumbers, DurationMs = outcome.Duration.TotalMilliseconds }, ct);
            }
            catch (Exception ex)
            {
                errors++;
                _log.LogError(ex, "Delta failed for {ContractId}", c.ContractId);
                await _audit.LogAsync("DeltaContractFailed", "Error",
                    new { c.ContractId, Error = ex.Message }, ct);
            }
        }

        await _audit.LogAsync("DeltaJobEnd", "OK", new { Contracts = contracts.Count, added, deactivated, errors }, ct);
    }

    public async Task RunSingleContractAsync(string contractId, string triggeredBy, CancellationToken ct)
    {
        await _audit.LogAsync("DeltaManualStart", "OK", new { contractId, triggeredBy }, ct);
        var outcome = await _repo.RefreshAsync(contractId, ct);
        await _audit.LogAsync("DeltaManualEnd", "OK",
            new { contractId, triggeredBy, outcome.AffectedNumbers, DurationMs = outcome.Duration.TotalMilliseconds }, ct);
    }
}
```

## Ručno okidanje delte iz admin UI-a

Controller:

```csharp
[Area("Admin")]
[Authorize(Roles = Roles.Administrator)]
public sealed class DeltaSyncController : Controller
{
    private readonly Hangfire.IBackgroundJobClient _jobs;
    private readonly IAuditService _audit;
    public DeltaSyncController(Hangfire.IBackgroundJobClient jobs, IAuditService audit)
        => (_jobs, _audit) = (jobs, audit);

    [HttpPost("Admin/DeltaSync/RunAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAll(CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var jobId = _jobs.Enqueue<IDeltaSyncJob>(j => j.RunAllClientsAsync(CancellationToken.None));
        await _audit.LogAsync("DeltaManualEnqueue", "OK", new { scope = "all", user, jobId }, ct);
        TempData["Info"] = $"Job {jobId} zakazan.";
        return RedirectToAction("Index");
    }

    [HttpPost("Admin/DeltaSync/RunContract/{contractId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunContract(string contractId, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "unknown";
        var jobId = _jobs.Enqueue<IDeltaSyncJob>(j =>
            j.RunSingleContractAsync(contractId, user, CancellationToken.None));
        await _audit.LogAsync("DeltaManualEnqueue", "OK", new { contractId, user, jobId }, ct);
        TempData["Info"] = $"Job {jobId} zakazan za ugovor {contractId}.";
        return RedirectToAction("Index");
    }
}
```

## Obrada zakazanih SMS poruka

Legacy model `ScheduledSmsModel` + kolona `SendDate` u `BST_MESSAGES` (`Send_Date`).

`src/BizSMS.Application/DeltaSync/IScheduledSmsProcessorJob.cs`:

```csharp
namespace BizSMS.Application.DeltaSync;

public interface IScheduledSmsProcessorJob
{
    Task ProcessDueAsync(CancellationToken ct);
}
```

`src/BizSMS.Infrastructure/DeltaSync/ScheduledSmsProcessorJob.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.DeltaSync;
using BizSMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizSMS.Infrastructure.DeltaSync;

internal sealed class ScheduledSmsProcessorJob : IScheduledSmsProcessorJob
{
    private static readonly TimeSpan LookaheadWindow = TimeSpan.FromMinutes(2);

    private readonly AppDbContext _db;
    private readonly ISmsGateway _sms;
    private readonly IAuditService _audit;
    private readonly ILogger<ScheduledSmsProcessorJob> _log;

    public ScheduledSmsProcessorJob(AppDbContext db, ISmsGateway sms, IAuditService audit, ILogger<ScheduledSmsProcessorJob> log)
        => (_db, _sms, _audit, _log) = (db, sms, audit, log);

    public async Task ProcessDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await _db.ScheduledSms
            .Where(s => !s.Sent
                        && !s.Canceled
                        && s.ScheduledFor <= now.Add(LookaheadWindow))
            .OrderBy(s => s.ScheduledFor)
            .Take(200)
            .ToListAsync(ct);

        foreach (var item in due)
        {
            if (ct.IsCancellationRequested) break;

            // „Optimistic lease“ da izbegnemo dupli send u multi-worker setup-u
            var claimed = await _db.ScheduledSms
                .Where(s => s.Id == item.Id && !s.Sent && !s.Canceled && s.LeasedUntil < now)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LeasedUntil, now.AddMinutes(2))
                    .SetProperty(s => s.LeaseOwner, Environment.MachineName), ct);
            if (claimed == 0) continue;

            try
            {
                await _sms.SendAsync(item.PhoneNumber, item.MessageText, ct);
                await _db.ScheduledSms
                    .Where(s => s.Id == item.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(s => s.Sent, true)
                        .SetProperty(s => s.SentAt, DateTime.UtcNow), ct);
                await _audit.LogAsync("ScheduledSmsSent", "OK", new { item.Id, item.PhoneNumber }, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Send failed for scheduled sms {Id}", item.Id);
                await _audit.LogAsync("ScheduledSmsFailed", "Error", new { item.Id, error = ex.Message }, ct);
                // ostavljamo LeasedUntil da istekne — Hangfire pokušava ponovo za par minuta
            }
        }
    }
}
```

> Ako `BST_SCHEDULED_SMS` nema `LeasedUntil`/`LeaseOwner`, oslanjaj se na `SERIALIZABLE`
> transakciju + `WITH (UPDLOCK, READPAST)` hint kroz raw SQL. Ovo je business odluka: hoće li se
> šema proširiti ili ne. Ako **ne** — koristi „single Hangfire worker“ na `sms` queue-u
> (`WorkerCount=1` za tu queue) da ne bi bilo trke.

Otkazivanje zakazane poruke (admin/klijent):

```csharp
public async Task<Result> CancelAsync(int scheduledId, string userId, string reason, CancellationToken ct)
{
    var affected = await _db.ScheduledSms
        .Where(s => s.Id == scheduledId && !s.Sent && !s.Canceled)
        .ExecuteUpdateAsync(u => u
            .SetProperty(s => s.Canceled, true)
            .SetProperty(s => s.CanceledAt, DateTime.UtcNow)
            .SetProperty(s => s.CanceledByUserId, userId)
            .SetProperty(s => s.CancelReason, reason), ct);

    if (affected == 0) return Result.Fail("Poruka je već poslata ili je već otkazana.");
    await _audit.LogAsync("ScheduledSmsCanceled", "OK", new { scheduledId, userId, reason }, ct);
    return Result.Ok();
}
```

## Before / After — legacy „Task.Run/QueueBackgroundWorkItem“

**Legacy** (obično u kontroleru posle POST akcije):

```csharp
[HttpPost]
public ActionResult Refresh()
{
    HostingEnvironment.QueueBackgroundWorkItem(async token =>
    {
        using (var ctx = new ApplicationDbContext())
        {
            // dugačak job unutar HTTP pipeline-a — može biti prekinut recycle-om
        }
    });
    return RedirectToAction("Index");
}
```

**.NET 10**:

```csharp
[HttpPost]
public IActionResult Refresh([FromServices] IBackgroundJobClient jobs)
{
    var jobId = jobs.Enqueue<IDeltaSyncJob>(j => j.RunAllClientsAsync(CancellationToken.None));
    TempData["Info"] = $"Job {jobId} zakazan.";
    return RedirectToAction(nameof(Index));
}
```

Prednosti: job preživljava restart, ima retry-je, ima dashboard, može da se otkaže.

## Health check za Hangfire

```csharp
services.AddHealthChecks()
    .AddCheck<HangfireHealthCheck>("hangfire");
```

Ili koristi `AspNetCore.HealthChecks.Hangfire` NuGet paket:

```csharp
services.AddHealthChecks()
    .AddHangfire(o => { o.MaximumJobsFailed = 5; o.MinimumAvailableServers = 1; });
```

## Checklist za code review

- [ ] Nijedan poziv SP-a nema SQL injection surfaceove (svi parametri su `SqlParameter`).
- [ ] SP pozivi imaju eksplicitan `CommandTimeout`.
- [ ] Delta job je registrovan kao `RecurringJob` sa cron-om iz konfiguracije.
- [ ] Ručno okidanje je iza `[Authorize(Roles = Administrator)]` i `[ValidateAntiForgeryToken]`.
- [ ] Svaki job start/end (ili greška) rezultuje audit zapisom.
- [ ] Job koristi `IServiceScopeFactory` kada je registrovan izvan Hangfire konteksta (npr. za
      `IHostedService`); u Hangfire-u DI radi automatski po pozivu.
- [ ] `ScheduledSmsProcessorJob` ima „lease“ mehanizam ili je izolovan na single worker po queue-u.
- [ ] Idempotentnost: pokretanje istog joba dvaput ne šalje SMS dvaput.
- [ ] Hangfire dashboard je iza role-check-a `Administrator`.

## Najčešće greške i kako ih izbeći

1. **Duplo slanje pri paralelnim workerima** — bez „lease“ mehanizma ili single-worker queue-a,
   dve instance mogu pokupiti istu zakazanu poruku.
2. **Zaboravljanje `CommandTimeout`** — `sp_RefreshNumbers` može trajati minutima; default 30s
   ruši job pri velikim klijentima.
3. **Držanje otvorene EF konekcije duže vreme** — u ADO.NET putu, koristi `await using` na
   `SqlConnection`/`SqlCommand` i pusti EF Core connection resiliency da radi svoje.
4. **Poziv SP-a bez `ExecutionStrategy` u tranzakciji** — sa `EnableRetryOnFailure` će baciti
   `InvalidOperationException`. Reši sa `strategy.ExecuteAsync(async () => { … })`.
5. **`FromSqlInterpolated` sa nesigurnim ulazom** — interpolisani parametri se propagiraju kao
   `SqlParameter`, ali **nazive tabela/kolona** nikada ne interpoluj (nisu parametri) — to je
   klasična ranjivost.
6. **Ostavljanje starih naloga bez retry logike** — Hangfire ima `AutomaticRetry` po defaultu; ako
   ne želiš retry (npr. slanje jednog SMS-a), dekoriši metodu sa `[AutomaticRetry(Attempts = 0)]`.
7. **Cron zonu ne postaviti** — bez `TimeZone = TimeZoneInfo.Local`, cron „0 0 3 * * ?“ radi u
   UTC-u; job kreće u 4 ili 5 ujutru po lokalnom vremenu, što otvara audit pitanja.
8. **Pisanje audit zapisa unutar `catch (OperationCanceledException)`** — potroši token; testiraj
   sa `--ct` scenarijem da bi bio siguran da se pravilno raspetljava.
