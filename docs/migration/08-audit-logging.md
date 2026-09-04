## Svrha
Standardizuje audit logovanje za bezbednosno i poslovno kritične akcije.

## Koraci migracije
1. Definisati audit event tipove (login, OTP, delta, send/schedule/cancel, deny/allow broj, izmene klijenata).
2. Uvesti `IAuditService` i centralnu serializaciju događaja.
3. Dodati correlation-id u svaki audit zapis.
4. Maskirati osetljive podatke (OTP, tokeni, lozinke).

## Before/After primer
### Before (legacy logger)
```csharp
logger.SetControllerAction("AdminManageController", "CreateClient");
logger.Info("Load VPN numbers");
logger.Error(ex.Message);
```

### After (struktuirani audit)
```csharp
await _audit.LogAsync("CLIENT_UPDATED", new
{
    ClientId = model.ClientID,
    UserId = userId,
    CorrelationId = httpContext.TraceIdentifier,
    At = DateTime.UtcNow
});
```

## Code snippets
### Audit servis
```csharp
public interface IAuditService
{
    Task LogAsync(string eventType, object payload, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly BizSmsDbContext _db;

    public AuditService(BizSmsDbContext db) => _db = db;

    public async Task LogAsync(string eventType, object payload, CancellationToken ct = default)
    {
        _db.Logs.Add(new Log
        {
            LogDate = DateTime.UtcNow,
            LogLevel = "INFO",
            LogSource = eventType,
            LogMessage = JsonSerializer.Serialize(payload)
        });

        await _db.SaveChangesAsync(ct);
    }
}
```

### Primeri događaja
```csharp
await _audit.LogAsync("LOGIN_ATTEMPT", new { Username = model.Username, Success = success });
await _audit.LogAsync("OTP_SEND_CONFIRM", new { UserId = user.Id, Action = "SendOrSchedule" });
await _audit.LogAsync("SCHEDULE_CANCEL", new { MessageId = id, CancelBy = userId });
```

## Checklist za code review
- [ ] Svi kritični tokovi imaju audit događaje.
- [ ] Correlation-id je upisan.
- [ ] Osetljivi podaci nisu logovani.
- [ ] Error događaji imaju dovoljno konteksta za incident analizu.

## Najčešće greške i kako ih izbeći
- Logovanje čistog exception stack-a bez poslovnog konteksta.
- Logovanje OTP vrednosti (strogo zabranjeno).
