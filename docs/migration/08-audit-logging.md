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

public static class AuditMetrics
{
    private static long _droppedEvents;
    public static long DroppedEvents => Interlocked.Read(ref _droppedEvents);
    public static void IncrementDropped() => Interlocked.Increment(ref _droppedEvents);
}

public sealed class AuditService : IAuditService
{
    private readonly Channel<Log> _channel;

    public AuditService(Channel<Log> channel) => _channel = channel;

    public Task LogAsync(string eventType, object payload, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var written = _channel.Writer.TryWrite(new Log
        {
            LogDate = DateTime.UtcNow,
            LogLevel = "INFO",
            LogSource = eventType,
            LogMessage = JsonSerializer.Serialize(payload)
        });
        if (!written) AuditMetrics.IncrementDropped();
        return Task.CompletedTask;
    }
}

public sealed class AuditWriterWorker : BackgroundService
{
    private readonly Channel<Log> _channel;
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditWriterWorker(Channel<Log> channel, IServiceScopeFactory scopeFactory)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BizSmsDbContext>();
            db.Logs.Add(item);
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
```

### Primeri događaja
```csharp
using System.Security.Cryptography;
using System.Text;

var userHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model.Username)));
await _audit.LogAsync("LOGIN_ATTEMPT", new { UsernameHash = userHash, Success = success });
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
