## Svrha
Operativni plan prelaska sa legacy BizSMS na .NET 10 MVC uz minimalan rizik i bez promene SQL šeme.

## Koraci migracije
1. Priprema: završiti parity testove i rollback proceduru.
2. Dry-run: pokrenuti .NET 10 aplikaciju na staging-u sa produkcionom kopijom baze.
3. Paralelni audit: uporediti ključne metrike (login, send, schedule, delta).
4. Cutover prozor: preusmeravanje saobraćaja uz monitoring.
5. Post-cutover: pojačan nadzor i brza stabilizacija.

## ASCII dijagram
```text
Legacy PROD -> (readiness gate) -> Staging parity OK
      |                                  |
      +------------ rollback <-----------+
                       |
                    Cutover
                       |
                 .NET 10 PROD
```

## Before/After primer
### Before (legacy ručni tokovi)
```csharp
// Admin ručno pokreće import/refresh kroz postojeći API endpoint
public IHttpActionResult ImportNumbers(int Id) { ... }
```

### After (.NET 10 kontrolisan cutover tok)
```csharp
[Authorize(Roles = "Administrator")]
[HttpPost("ops/delta/run")]
public async Task<IActionResult> RunDelta([FromBody] RunDeltaRequest request, CancellationToken ct)
{
    var result = await _deltaSyncService.RunForClientAsync(request.ClientId, ct);
    return Ok(result);
}
```

## Code snippets
### Readiness check endpoint
```csharp
[ApiController]
[Route("ops")]
public class OpsController : ControllerBase
{
    [HttpGet("ready")]
    public IActionResult Ready() => Ok(new { Status = "Ready", Utc = DateTime.UtcNow });
}
```

### Feature flag za OTP action gate
```csharp
public bool IsSendOtpGateEnabled(IConfiguration cfg)
    => cfg.GetValue<bool>("FeatureFlags:SendOtpConfirmation");
```

## Checklist za code review
- [ ] Postoji jasan rollback (DNS/app slot/reverse proxy preklop).
- [ ] Monitoring i alerting su definisani za auth, SMS, delta i DB greške.
- [ ] Admin runbook za incident je pripremljen.
- [ ] Post-cutover verifikacije su automatizovane gde je moguće.

## Najčešće greške i kako ih izbeći
- Cutover bez freeze perioda i bez rollback owner-a.
- Nedovoljno praćenje OTP/SMS failure rate-a prvih 24h.
