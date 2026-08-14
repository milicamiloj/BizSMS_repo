# 08 — Audit logging (struktuiran + korelacija)

## Svrha

Zameniti legacy log4net + custom `Helpers.Logger` sa struktuiranim logging pipeline-om baziranim
na **`Microsoft.Extensions.Logging`** i **Serilog**. Definisati **`IAuditService`** za domenski
audit (ko/šta/kada/nad čim) i pokazati kako se piše u postojeću tabelu **`BST_LOG`**, koja
šema mora ostati nepromenjena.

Sve audit-relevantne akcije iz problem statement-a:

- login attempts (success/fail/lockout),
- delta job (start/end/error, statistika added/deactivated),
- zabrane brojeva (deny/allow + razlog),
- slanja/zakazivanja/otkazivanja SMS poruka,
- izmene klijenata (kreiranje/otkazivanje ugovora, promena cenovnika),
- izmene korisnika (kreiranje, brisanje, reset password, lock/unlock, promena telefona),
- svaki export izveštaja,
- svaka administratorska „privileged“ akcija.

## Legacy stanje (za referencu)

`Helpers/Logger.cs`:

```csharp
public class Logger
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(...);
    public void SetControllerAction(string c, string a) { GlobalContext.Properties[...] = c; ... }
    public void Error(string m)  { log.Error(m); }
    public void Info(string m)   { log.Info(m); }
    public void Warn(string m)   { log.Warn(m); }
}
```

- `GlobalContext.Properties` je **thread-local** — u async svetu se lako izgubi kontekst.
- Svi audit zapisi idu kao slobodan tekst u `Log_Message`, bez struktuiranih polja.
- `BST_LOG` šema (kolone) postoji ali se ne koristi konzistentno.

## Ciljna arhitektura

```
Kontroler / Servis
      |
      | _audit.LogAsync("EventType", "OK", payload)
      v
+---------------------+
|   IAuditService     |----> Domain event -> DB (BST_LOG)
|  (transakcijski)    |----> ILogger<T>   -> Serilog
+---------------------+
        |
        +--> HttpContext:  User, IP, UA, CorrelationId
        +--> ITenantContext: ClientId
        +--> DateTime.UtcNow, MachineName
```

Idealno: **jedan sinkroni put** koji zapisuje u SQL (sinkroni, u transakciji) + **paralelni**
strukturisani Serilog zapis (za centralno pretraživanje, npr. Seq/Elastic). To ne dopušta
gubitak audit reda u slučaju rušenja Kestrel-a.

## BST_LOG šema — koje kolone koristimo

Iz legacy schema:

- `Log_Date` — timestamp,
- `Log_Level` — INFO / WARN / ERROR,
- `Log_Source` — MachineName / komponentu (npr. `BizSMS.Web`, `BizSMS.Jobs`),
- `User` — username ili "System",
- `Controller` — MVC controller ili domenski agregat (`Client`, `Delta`),
- `Action` — event type / method,
- `Log_Message` — struktuirani JSON payload (compact, jednorede).

Ne menjamo šemu. Struktura JSON-a u `Log_Message` je **novi ugovor** koji dokumentujemo interno.

## IAuditService — API

`src/BizSMS.Application/Abstractions/IAuditService.cs`:

```csharp
namespace BizSMS.Application.Abstractions;

public interface IAuditService
{
    Task LogAsync(string eventType, string outcome, object payload, CancellationToken ct,
                  AuditLevel level = AuditLevel.Info);
    Task LogAsync(AuditEntry entry, CancellationToken ct);
}

public enum AuditLevel { Info, Warn, Error }

public sealed record AuditEntry(
    string EventType,
    string Outcome,
    string? UserName,
    string? Controller,
    string? Action,
    object? Payload,
    AuditLevel Level = AuditLevel.Info);
```

## Implementacija — transakcijski insert u BST_LOG

`src/BizSMS.Infrastructure/Auditing/AuditService.cs`:

```csharp
using System.Text.Json;
using BizSMS.Application.Abstractions;
using BizSMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BizSMS.Infrastructure.Auditing;

internal sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _log;

    public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> log)
        => (_db, _http, _log) = (db, http, log);

    public Task LogAsync(string eventType, string outcome, object payload, CancellationToken ct, AuditLevel level = AuditLevel.Info)
        => LogAsync(new AuditEntry(eventType, outcome, null, null, null, payload, level), ct);

    public async Task LogAsync(AuditEntry entry, CancellationToken ct)
    {
        var ctx = _http.HttpContext;
        var user = entry.UserName ?? ctx?.User?.Identity?.Name ?? "System";
        var controller = entry.Controller ?? (ctx?.GetRouteValue("controller") as string);
        var action = entry.Action ?? (ctx?.GetRouteValue("action") as string);
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var ua = ctx?.Request?.Headers.UserAgent.ToString();
        var corr = ctx?.Items[BizSMS.Web.Middleware.CorrelationIdMiddleware.HeaderName] as string;

        var envelope = new
        {
            corr,
            ip,
            ua,
            entry.Outcome,
            payload = entry.Payload
        };

        var row = new Domain.Entities.Log
        {
            LogDate = DateTime.UtcNow,
            LogLevel = entry.Level switch
            {
                AuditLevel.Warn  => "WARN",
                AuditLevel.Error => "ERROR",
                _                => "INFO"
            },
            LogSource = Environment.MachineName,
            User = user,
            Controller = controller ?? string.Empty,
            Action = action ?? entry.EventType,
            LogMessage = JsonSerializer.Serialize(envelope, Json)
        };

        _db.Logs.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Ako audit padne (npr. DB nedostupan), ne prekidamo poslovni tok, ali logujemo lokalno.
            _log.LogError(ex, "Audit persist failed for {EventType}", entry.EventType);
        }

        // Dodatno, uvek strukturirano u Serilog (za centralnu pretragu)
        var msg = "AUDIT {EventType} outcome={Outcome} user={User} controller={Controller} action={Action}";
        switch (entry.Level)
        {
            case AuditLevel.Warn:  _log.LogWarning(msg, entry.EventType, entry.Outcome, user, controller, action); break;
            case AuditLevel.Error: _log.LogError  (msg, entry.EventType, entry.Outcome, user, controller, action); break;
            default:               _log.LogInformation(msg, entry.EventType, entry.Outcome, user, controller, action); break;
        }
    }
}
```

## Korišćenje u domenu

Login (v. poglavlje 04):

```csharp
await _audit.LogAsync("LoginSucceeded", "OK", new { user.UserName }, ct);
await _audit.LogAsync("LoginFailed",    "BadCredentials", new { userName = model.Username }, ct, AuditLevel.Warn);
await _audit.LogAsync("LoginLockedOut", "Lockout", new { user.UserName }, ct, AuditLevel.Warn);
```

Delta job (v. poglavlje 06):

```csharp
await _audit.LogAsync("DeltaJobStart", "OK", new { Scope = "AllClients" }, ct);
await _audit.LogAsync("DeltaContractDone", "OK",
    new { c.ContractId, outcome.AffectedNumbers, DurationMs = outcome.Duration.TotalMilliseconds }, ct);
await _audit.LogAsync("DeltaContractFailed", "Error",
    new { c.ContractId, error = ex.Message }, ct, AuditLevel.Error);
```

Zabrana broja:

```csharp
await _audit.LogAsync("NumberDenied", "OK",
    new { NumberId = id, Number = number.Number, Reason = reason, By = user }, ct);
```

Slanje SMS-a (uspešno / neuspešno):

```csharp
await _audit.LogAsync("SmsSent", "OK",
    new { MessageId = msg.Id, Recipients = msg.Numbers.Count, ClientId = tenant.ClientId }, ct);
await _audit.LogAsync("SmsSendFailed", "Error",
    new { MessageId = msg.Id, error = ex.Message }, ct, AuditLevel.Error);
await _audit.LogAsync("SmsScheduled", "OK",
    new { MessageId = msg.Id, msg.ScheduledFor, Recipients = msg.Numbers.Count }, ct);
await _audit.LogAsync("SmsCanceled", "OK",
    new { MessageId = msg.Id, Reason = reason }, ct);
```

Izmene klijenata / korisnika:

```csharp
await _audit.LogAsync("ClientCreated", "OK", new { ClientId = client.ClientID, Name = client.Name }, ct);
await _audit.LogAsync("UserCreated",   "OK", new { UserId = user.Id, ClientId = user.ClientID, Role = "BusinessUser" }, ct);
await _audit.LogAsync("UserLocked",    "OK", new { UserId = user.Id, Until = untilUtc }, ct, AuditLevel.Warn);
await _audit.LogAsync("UserUnlocked",  "OK", new { UserId = user.Id }, ct);
await _audit.LogAsync("PasswordReset", "OK", new { UserId = user.Id, By = admin }, ct);
```

Cenovnik:

```csharp
await _audit.LogAsync("MessageCostChanged", "OK",
    new { CostId = mc.Id, mc.Category, mc.PriceFrom, mc.PriceTo, mc.PricePerMessage }, ct);
```

## Serilog konfiguracija

`appsettings.json`:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Hangfire": "Warning"
      }
    },
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId", "WithProcessId" ],
    "Properties": { "Application": "BizSMS" },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/bizsms-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "shared": true,
          "outputTemplate": "{Timestamp:o} [{Level:u3}] cid={CorrelationId} u={User} {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

`Program.cs` (već pokazano u poglavlju 02):

```csharp
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(sp)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "BizSMS"));
```

## Log EF Core entitet + konfiguracija

`src/BizSMS.Domain/Entities/Log.cs`:

```csharp
namespace BizSMS.Domain.Entities;

public sealed class Log
{
    public int Id { get; set; }
    public DateTime LogDate { get; set; }
    public string LogLevel { get; set; } = "INFO";
    public string LogSource { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string LogMessage { get; set; } = string.Empty;
}
```

`src/BizSMS.Infrastructure/Persistence/Configurations/LogConfiguration.cs`:

```csharp
internal sealed class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> b)
    {
        b.ToTable("BST_LOG");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("Log_ID");
        b.Property(x => x.LogDate).HasColumnName("Log_Date").HasColumnType("datetime");
        b.Property(x => x.LogLevel).HasColumnName("Log_Level").HasMaxLength(10).IsRequired();
        b.Property(x => x.LogSource).HasColumnName("Log_Source").HasMaxLength(100).IsRequired();
        b.Property(x => x.User).HasColumnName("User").HasMaxLength(200).IsRequired();
        b.Property(x => x.Controller).HasColumnName("Controller").HasMaxLength(200).IsRequired();
        b.Property(x => x.Action).HasColumnName("Action").HasMaxLength(200).IsRequired();
        b.Property(x => x.LogMessage).HasColumnName("Log_Message").HasMaxLength(4000).IsRequired();

        b.HasIndex(x => x.LogDate);
        b.HasIndex(x => new { x.Controller, x.Action });
    }
}
```

## Before / After — poziv audit logovanja

**Legacy:**

```csharp
readonly Logger logger = new Logger();

public ActionResult Cancel(int id)
{
    logger.SetControllerAction("ClientManage", "Cancel");
    var client = context.Client.Find(id);
    client.IsCanceled = true;
    context.SaveChanges();
    logger.Info("Client " + id + " canceled by " + User.Identity.Name);
    return RedirectToAction("Index");
}
```

**.NET 10:**

```csharp
public sealed class ClientManageController : Controller
{
    private readonly IClientService _clients;
    private readonly IAuditService _audit;
    public ClientManageController(IClientService clients, IAuditService audit)
        => (_clients, _audit) = (clients, audit);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken ct)
    {
        await _clients.CancelAsync(id, reason, ct);
        await _audit.LogAsync("ClientCanceled", "OK",
            new { ClientId = id, Reason = reason }, ct);
        return RedirectToAction(nameof(Index));
    }
}
```

Prednosti:

- Struktuiran payload → lako pretraživo (`SELECT * FROM BST_LOG WHERE JSON_VALUE(Log_Message, '$.payload.ClientId') = 42`).
- Automatski korelacioni ID + user + controller kroz DI.
- Ne mora se pamtiti da se pozove `SetControllerAction`.
- Neuspeh audita ne ruši poslovni tok.

## SQL upiti za pretragu (primeri)

```sql
-- Sve poruke poslate juce
SELECT Log_Date, [User], Log_Message
FROM BST_LOG
WHERE [Action] = 'SmsSent'
  AND Log_Date >= DATEADD(DAY, -1, GETDATE())
ORDER BY Log_Date DESC;

-- Neuspesni loginovi za korisnika X
SELECT Log_Date, JSON_VALUE(Log_Message, '$.ip') AS Ip
FROM BST_LOG
WHERE [Action] = 'LoginFailed'
  AND [User] = 'jsmith'
  AND Log_Date >= DATEADD(DAY, -7, GETDATE());

-- Delta sync greske po ugovorima
SELECT JSON_VALUE(Log_Message, '$.payload.ContractId') AS ContractId, COUNT(*) AS Errors
FROM BST_LOG
WHERE [Action] = 'DeltaContractFailed'
  AND Log_Date >= DATEADD(DAY, -30, GETDATE())
GROUP BY JSON_VALUE(Log_Message, '$.payload.ContractId')
ORDER BY Errors DESC;
```

## Retention politika

- `BST_LOG` može brzo narasti — postavi SQL Agent job koji arhivira redove starije od 12 meseci
  u `BST_LOG_ARCHIVE` (ista šema) i briše iz `BST_LOG`.
- Compliance (GDPR) — anonimizuj `User`, `ip` posle isteka roka.
- Ako se koristi Serilog MSSQL sink direktno, isključi ga da ne bi pisao **duplo** u istu tabelu.

## Sensitive data handling

- **Ne loguj lozinke, OTP kodove, PII adrese**. Tokom serializacije `payload`-a, koristi
  „sanitizer“:

```csharp
public static class AuditSanitizer
{
    private static readonly string[] SecretKeys = { "password", "code", "token", "otp", "cvv" };

    public static object? Redact(object? payload)
    {
        if (payload is null) return null;
        var dict = payload.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => (object?)p.GetValue(payload));
        foreach (var k in dict.Keys.ToList())
            if (SecretKeys.Any(s => k.Contains(s, StringComparison.OrdinalIgnoreCase)))
                dict[k] = "***";
        return dict;
    }
}
```

Poziv:

```csharp
await _audit.LogAsync("PasswordChanged", "OK",
    AuditSanitizer.Redact(new { UserId = user.Id, password = "abc" }), ct);
```

## Checklist za code review

- [ ] Nema `log4net` referenci u kodu.
- [ ] Nema `Helpers.Logger` klase; koristi se `ILogger<T>` + `IAuditService`.
- [ ] `IAuditService.LogAsync` se poziva u svakoj mutacionoj domenskoj akciji.
- [ ] Payload je serializovan kao JSON i ograničen na 4000 karaktera.
- [ ] Sensitive fields su sanitizovani (`AuditSanitizer` ili ručno).
- [ ] Neuspešan audit ne ruši glavni tok (try/catch u `AuditService`).
- [ ] Serilog i BST_LOG **ne pišu duplo** (jedan izvor istine — BST_LOG za audit; Serilog za operativno).
- [ ] `LogAsync` u pozadinskim jobovima koristi `IServiceScopeFactory` za sopstveni scope.

## Najčešće greške i kako ih izbeći

1. **Blokada poslovnog toka zbog audita** — SQL sink može biti spor. Uvek `try/catch` oko
   `SaveChangesAsync`; grešku samo logovati u Serilog.
2. **Logovanje osetljivih podataka** — sve „kredencijale“, `IdentityResult.Errors`, i tokene
   filtriraj kroz `AuditSanitizer`. Ne loguj celu `HttpRequest.Form`.
3. **Enormni payload-i** — ne pišite ceo `HttpContext` ili LINQ upit u payload; ograniči na
   ključne polja. Ako veličina raste preko 4000 karaktera, poseci ili prebaci u file sink.
4. **Korišćenje `HttpContext.Current`** — nemoj; `IHttpContextAccessor` je pravi API. U
   pozadinskim jobovima on je `null`, i to je OK — `AuditService` u tom slučaju stavlja
   „System“ za user.
5. **Async void** — `LogAsync` mora biti `Task`. Nikad ne pišite `async void` metode za audit.
6. **Ignorisanje transakcija** — ako je audit deo iste transakcije kao mutacija, uspešno se
   piše ili poništi zajedno. Ako je van (default u AuditService), moraš prihvatiti da postoji
   „race“ prozor gde je mutacija urađena ali audit nije. Poslovna odluka: veci compliance = ista tx.
7. **Preskakanje read akcija** — pratite koje **read** akcije treba audit (npr. eksport
   podataka, pregled tuđeg klijenta iz admin naloga). Nemoj audit svaki GET.
