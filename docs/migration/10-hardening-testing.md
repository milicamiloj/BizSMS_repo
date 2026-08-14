# 10 — Hardening + testiranje

## Svrha

Prikupiti na jednom mestu **bezbednosne** kontrole koje uvodimo u .NET 10 verziju i **strategiju
testiranja** (unit / integration / end-to-end / load) pre cutover-a. Cilj je da produkcija dobije
verziju koja je bezbednija i pouzdanija od legacy-ja, sa merljivim testovima.

## Bezbednosni checklist (hardening)

### 1. Transport i cookies

- **HTTPS**: `UseHttpsRedirection` + `UseHsts` (max-age 365d, includeSubDomains, preload).
- **Kestrel** samo TLS 1.2 i TLS 1.3.
- Cookies: `HttpOnly`, `Secure`, `SameSite=Strict`, kratak sliding timeout (30 min).
- **Antiforgery** globalno kroz `AutoValidateAntiforgeryToken` (v. poglavlje 02).
- `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.

### 2. Security headers middleware

`src/BizSMS.Web/Middleware/SecurityHeadersMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace BizSMS.Web.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +   // ako Views koriste inline JS; postepeno migriraj na nonces
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'";
        await _next(ctx);
    }
}
```

Registruj u `Program.cs`:

```csharp
app.UseMiddleware<SecurityHeadersMiddleware>();
```

### 3. Autentifikacija / autorizacija

- Sve akcije globalno traže `[Authorize]` kroz fallback policy (v. poglavlja 02 i 04).
- Login stranica ima `[AllowAnonymous]` + `[ResponseCache(NoStore=true)]`.
- 2FA/OTP obavezan za sve business korisnike.
- `RequireOtpConfirmed` na svakoj SMS send/schedule akciji.
- Lockout: 5 pokušaja / 15 min (v. poglavlje 04).
- Session `IdleTimeout` maks 15 min za OTP state.

### 4. Input validation

- FluentValidation na svim VM-ovima.
- `RequestSizeLimit` na upload endpoint-ima.
- CSP + antiforgery + secure cookie kombinovano nudi obavezan XSS/CSRF nivo.
- Sve JSON API endpoint-e obeleži `[Produces("application/json")]` + `[Consumes("application/json")]`.
- Regeks-ove drži sa `RegexOptions.NonBacktracking` i timeout-om.

### 5. Rate limiting

`Program.cs`:

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        }));

    o.AddPolicy("otp", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.User.Identity?.Name ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(2)
        }));
});
```

I na akciji:

```csharp
[EnableRateLimiting("login")]
[HttpPost, AllowAnonymous]
public Task<IActionResult> Login(...) { ... }

[EnableRateLimiting("otp")]
[HttpGet]
public Task<IActionResult> OtpChallenge(...) { ... }
```

### 6. Secrets

- **Nema** connection stringova u `appsettings.json` u produkciji.
- Dev: `dotnet user-secrets`.
- Prod: env vars ili `Azure Key Vault` / `Vault` / `Secrets Manager`.
- Rotacija: kredencijali za SQL i SMS provider min. 1x godišnje.

### 7. Data protection

- Persist DP ključeva na disk (ili DB) preko `AddDataProtection().PersistKeysToFileSystem(...)`.
- Na više instanci: `PersistKeysToDbContext<AppDbContext>()` + `SetApplicationName("BizSMS")`.
- Bez toga, restart aplikacije invalidira sve cookies + antiforgery tokens.

```csharp
services.AddDataProtection()
    .SetApplicationName("BizSMS")
    .PersistKeysToDbContext<AppDbContext>();
```

### 8. SQL injection

- Sve LINQ upiti su parametrizovani (EF Core to radi automatski).
- `FromSqlRaw` samo sa `SqlParameter` argumentima — nikada string konkatenacija.
- `ExecuteSqlRawAsync` isto pravilo.
- Naziv tabele / kolone ne interpolisati.

### 9. Dependency scanning

- CI korak: `dotnet list package --vulnerable` i `dotnet list package --outdated`.
- Za NPM zavisnosti (frontend): `npm audit`.
- Dependabot ili Renovate za auto-PR na sigurnosne patcheve.

### 10. Logging & PII

- Sanitizuj payload (v. poglavlje 08).
- Ne loguj OTP kodove, lozinke, sesijski token.
- Za PII: loguj samo user id / username (ne email ili telefon).

## Strategija testiranja

```
                       +-----------------------+
                       |  Manual smoke tests   |  <-- pre cutover-a
                       +-----------+-----------+
                                   ^
                                   |
                       +-----------+-----------+
                       |  E2E (Playwright)     |  <-- login, send, schedule, reports
                       +-----------+-----------+
                                   ^
                                   |
                       +-----------+-----------+
                       |  Integration tests    |  <-- WebAppFactory + Testcontainers
                       +-----------+-----------+
                                   ^
                                   |
                       +-----------+-----------+
                       |     Unit tests        |  <-- Domain, Application, Validators
                       +-----------------------+
```

### Unit testovi

Ciljevi:

- `PhoneNumber.TryParse` — pozitivni i negativni scenariji (v. poglavlje 09).
- `NumberSyncPlanner.Build` — diff algoritam (v. poglavlje 06).
- `MessageComposer.ComposeForRecipient` — STOP_ID za ne-VPN (v. poglavlje 09).
- `MessageCostService.UpsertAsync` — overlap detekcija.
- `UserProvisioningService.CreateBusinessUserAsync` — cap 5 po klijentu.
- `OtpConfirmationService` — expiry logika u sesiji.
- FluentValidation validatori.

Primer (xUnit):

```csharp
public class PhoneNumberTests
{
    [Theory]
    [InlineData("0641234567", "0641234567")]
    [InlineData("064 123 45 67", "0641234567")]
    [InlineData("+381641234567", "0641234567")]
    [InlineData("06/61234567", "0661234567")]
    public void Parses_common_variants(string input, string expected)
    {
        Assert.True(PhoneNumber.TryParse(input, out var pn));
        Assert.Equal(expected, pn.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("07x1234567")]
    [InlineData("06abc")]
    [InlineData("061234567891234")]  // predugačak
    public void Rejects_invalid(string input)
    {
        Assert.False(PhoneNumber.TryParse(input, out _));
    }
}

public class NumberSyncPlannerTests
{
    [Fact]
    public void Adds_new_deactivates_missing_keeps_common()
    {
        var current = new[] { "0641111111", "0642222222", "0643333333" };
        var desired = new[] { "0642222222", "0643333333", "0644444444" };

        var plan = NumberSyncPlanner.Build(desired, current);

        Assert.Equal(new[] { "0644444444" }, plan.ToAdd);
        Assert.Equal(new[] { "0641111111" }, plan.ToDeactivate);
        Assert.Equal(2, plan.Unchanged.Count);
    }
}
```

### Integracioni testovi (WebApplicationFactory + Testcontainers)

Ciljevi:

- Kompletan HTTP round-trip: login → 2FA → send SMS → validate audit red u BST_LOG.
- Delta sync poziv SP-a nad realnim SQL Server-om (Testcontainers).
- Upload endpoint sa realnim CSV/XLSX fajlom.

`tests/BizSMS.IntegrationTests/BizSmsApplicationFactory.cs`:

```csharp
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace BizSMS.IntegrationTests;

public sealed class BizSmsApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithPassword("Test-Pass-1234!")
        .Build();

    public string ConnectionString => _sql.GetConnectionString() + ";Database=BizSMS;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:BizSms", ConnectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Hangfire.IBackgroundJobClient>();
            services.AddSingleton<Hangfire.IBackgroundJobClient, TestBackgroundJobClient>();
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // Napravi šemu (uvezi legacy SQL skripte)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
        await db.Database.MigrateAsync();
        // Zatim, izvrši seed sql skripte iz repozitorijuma:
        //   SQL scripts/PhoneCodeSentAt.sql, SendSMSID_length50.sql, sp_RefreshNumbers.sql, itd.
    }

    public async Task DisposeAsync() => await _sql.DisposeAsync();
}
```

Login integracioni test:

```csharp
public class LoginTests : IClassFixture<BizSmsApplicationFactory>
{
    private readonly BizSmsApplicationFactory _factory;
    public LoginTests(BizSmsApplicationFactory f) => _factory = f;

    [Fact]
    public async Task Login_returns_2fa_challenge_when_credentials_ok()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgery = await client.GetAntiforgeryTokensAsync("/Account/Login");

        var payload = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Username", "seed-admin"),
            new KeyValuePair<string, string>("Password", "Password!23"),
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery.Field)
        });

        var resp = await client.PostAsync("/Account/Login", payload);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/Account/LoginWith2fa", resp.Headers.Location!.LocalPath);
    }
}
```

### E2E (Playwright)

Ciljevi:

- „Happy path“: login + 2FA + slanje SMS-a (mock SMS provider).
- Klijentski upload CSV sa 10 validnih i 3 nevalidna reda.
- Report page + Excel download → header valid.
- Session timeout.

```csharp
using Microsoft.Playwright;

[TestFixture]
public class E2ELogin
{
    [Test]
    public async Task Full_login_and_send_flow()
    {
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://localhost:5001/Account/Login");
        await page.FillAsync("#Username", "seed-admin");
        await page.FillAsync("#Password", "Password!23");
        await page.ClickAsync("button[type=submit]");

        // 2FA — testni provider vraća deterministički kod „000000“
        await page.WaitForURLAsync("**/Account/LoginWith2fa*");
        await page.FillAsync("#TwoFactorCode", "000000");
        await page.ClickAsync("button[type=submit]");

        await page.WaitForURLAsync("**/Home*");
        var text = await page.TextContentAsync("h1");
        StringAssert.Contains("Dobrodošli", text);
    }
}
```

### Load i stres

- **k6** ili **NBomber** za SMS send endpoint (100 req/s baseline).
- Baseline metrike: p95 < 250ms za GET izveštaje, < 500ms za POST send (bez actual SMS-a — mock).
- SQL query plan za `GetMonthlyCostAsync` — proveri da nema table scan-a na `BST_MESSAGES`.

## CI pipeline (skica GitHub Actions)

`.github/workflows/build.yml`:

```yaml
name: build-and-test
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      sql:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: "Y"
          SA_PASSWORD: "Test-Pass-1234!"
        ports: [ "1433:1433" ]
        options: >-
          --health-cmd "true" --health-interval 10s
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore --configuration Release
      - name: Test
        run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"
        env:
          ConnectionStrings__BizSms: "Server=localhost,1433;Database=BizSMS;User Id=sa;******;TrustServerCertificate=True"
      - name: Vulnerable packages
        run: dotnet list package --vulnerable --include-transitive
```

## Migracioni testovi (pre cutover-a)

- **Regression pack**: iste screenshotove/eksporte iz legacy-ja porediš sa novom verzijom.
- **Shadow read**: nova verzija čita iz iste baze u paraleli — proveri da izveštaji vraćaju
  identičan broj redova i sume kao legacy (za period od 7 dana unazad).
- **Dry-run delta**: pokreni delta sync u novoj verziji sa `--dry-run` flagom (samo loguj plan,
  ne piši) — uporedi sa produkcijskim SP izvršavanjem.

## Checklist za code review (bezbednosno + testing)

- [ ] Nema `TrustServerCertificate=True` u produkcijskim connection stringovima (osim ako je
      SQL cert baš validan).
- [ ] Nema hard-coded lozinki, tokena, ključeva u kodu.
- [ ] Svaka nova akcija ima [Authorize] i/ili policy.
- [ ] Antiforgery, HSTS, security headers su registrovani i pokriveni testom.
- [ ] `EnableSensitiveDataLogging` je false u produkciji.
- [ ] Rate limiter je uključen za login i OTP.
- [ ] Unit testovi za sve domenske invarijante (STOP_ID, cap korisnika, diff algoritam).
- [ ] Bar 1 integracioni test za svaki „high-risk“ endpoint (login/2FA, delta, upload).
- [ ] Playwright E2E za happy path.
- [ ] CI baca build ako ima vulnerable transitivnih paketa.

## Najčešće greške i kako ih izbeći

1. **Zaboraviti `UseHsts` u ne-Dev okruženju** — HTTPS je pretpostavka, HSTS je garantija.
2. **CSP-a koji ne dozvoljava `unsafe-inline`, a Views imaju inline `<script>`** — front-end
   se lomi. Postepeno prelazi na nonces + preneseni JS u eksterne fajlove.
3. **Testing sa in-memory SQLite umesto SQL Server-om** — sinonimi ponašanja se razlikuju
   (npr. `datetime2`, `ExecuteUpdate`). Za integracioni: Testcontainers SQL Server.
4. **Bez Data Protection persist mehanizma** — restart aplikacije lomi sve login sesije.
5. **Ignorisanje `dotnet list package --vulnerable`** — dobijaš CVE-ove kroz transitivne
   zavisnosti (npr. Serilog sinks). Ubaci u CI kao gate.
6. **Testovi bez CancellationToken-a** — ne testira se pravi `async` behavior.
7. **Blocking rate limitera na login endpoint bez feedback-a** — vrati `Retry-After` header
   sa `RateLimitPartition.GetFixedWindowLimiter` `QueueLimit=0` + custom
   `OnRejected` handler.
8. **Ostavljanje `EnableDetailedErrors` na true u produkciji** — može otkriti šemu bazi.
