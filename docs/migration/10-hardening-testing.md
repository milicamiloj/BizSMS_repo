## Svrha
Bezbednosno i funkcionalno učvršćivanje .NET 10 migracije, sa fokusom na auth, OTP, jobove i data access.

## Koraci migracije
1. Uključiti secure headers, anti-forgery, HSTS i cookie hardening.
2. Uvesti rate limit za login/OTP resend.
3. Pokriti unit/integration testovima: auth, OTP confirm, delta diff, upload validacije.
4. Uvesti monitoring health-check endpointe.

## Before/After primer
### Before (legacy OTP resend interval)
```csharp
if (user.PhoneCodeSentAt != null && user.PhoneCodeSentAt > DateTime.Now.AddSeconds(-30))
{
    return RedirectToAction("VerifyPhoneNumber");
}
```

### After (.NET 10 rate limiting)
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("otp", httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        var userSegment = path.StartsWith("/otp/resend/", StringComparison.OrdinalIgnoreCase)
            ? path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            : null;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userSegment
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "otp-anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});
```

```csharp
[EnableRateLimiting("otp")]
[HttpPost("otp/resend/{username}")]
public async Task<IActionResult> ResendOtp(string username)
{
    await _otpService.ResendAsync(username);
    return Ok();
}
```

## Code snippets
### Security hardening
```csharp
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        ctx.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self';";
        return Task.CompletedTask;
    });
    await next();
});
```

### Test primer (OTP potvrda pre slanja)
```csharp
[Fact]
public async Task Send_Should_Fail_When_ActionOtp_NotConfirmed()
{
    var userId = "u1";
    var svc = new MessageSendingService(...);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        svc.SendNowAsync(userId, new SendSmsCommand(), CancellationToken.None));
}
```

### Test primer (delta idempotentnost)
```csharp
[Fact]
public async Task Delta_Should_Be_Idempotent()
{
    var first = await service.ApplyDeltaAsync(clientId, rows, ct);
    var second = await service.ApplyDeltaAsync(clientId, rows, ct);

    Assert.Equal(0, second.Inserted);
    Assert.Equal(0, second.Deactivated);
}
```

## Checklist za code review
- [ ] Anti-forgery i secure cookies aktivni.
- [ ] Rate limit postoji za login/OTP.
- [ ] Postoje testovi za OTP gate i delta idempotentnost.
- [ ] Health check i osnovni observability su uvedeni.

## Najčešće greške i kako ih izbeći
- Fokus samo na “kompajlira” bez bezbednosnih testova.
- Izostavljen rollback plan za kritične auth promene.
