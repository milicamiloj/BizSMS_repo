# 05 — FilterAttribute → Middleware / Global Filters

## Svrha

Objasniti kada se legacy `System.Web.Mvc` filter (npr. `AuthorizeAttribute`,
`ActionFilterAttribute`, `HandleErrorAttribute`, custom logging attribute) preslikava u:

- **middleware** (globalno, izvan MVC pipeline-a — npr. correlation-id, exception, audit
  request),
- **MVC filter** (`IAsyncActionFilter`, `IAsyncAuthorizationFilter`, `IAsyncResourceFilter`,
  `IAsyncExceptionFilter`),
- **policy** (autorizacija — v. poglavlje 04),
- **atribut sa `IAuthorizationFilter` implementacijom** (za akcijski-specifični use-case, npr.
  `RequireOtpConfirmed`).

## Cheat sheet — mapiranje legacy filtera

| Legacy filter                                          | Novi pristup u .NET 10                                      |
|--------------------------------------------------------|-------------------------------------------------------------|
| `RequireHttpsAttribute` (globalno)                     | `UseHttpsRedirection()` + `UseHsts()`                       |
| `AuthorizeAttribute`                                   | `[Authorize]` (standardni) + policy                         |
| `AuthorizeUserAttribute` (custom)                      | Auth policy + `IAuthorizationHandler` (v. poglavlje 04)     |
| `ChangeFirstPasswordAttribute` (redirekcija)           | `IAsyncActionFilter` ili middleware                         |
| `DefaultApiLoggingAttribute` (logging around actions)  | Serilog + middleware (`UseSerilogRequestLogging`)           |
| `HandleErrorAttribute`                                 | `UseExceptionHandler` + `IExceptionFilter` za MVC-only case |
| `ValidateAntiForgeryToken` (svuda)                     | Global `AutoValidateAntiforgeryTokenAttribute`              |
| `OutputCache(NoStore=true)` (na login-u)               | `[ResponseCache(NoStore=true)]`                             |
| `AuthorizeApiUserAttribute` (Web API)                  | Isti `[Authorize]` + policy (nema više Web API-a odvojeno)  |

Pravilo palca:

- Ako filter treba da radi **za svaki zahtev** (npr. correlation-id, logging, exception) → **middleware**.
- Ako je vezan za MVC akciju i menja model stanje, rezultat, ili radi kratku pre/post check-ovu → **MVC filter**.
- Ako je čista autorizaciona odluka → **policy** + handler.

## Correlation-id middleware

Legacy `Logger.SetControllerAction(controller, action)` pokušava da mapira „korelaciju“ preko
`log4net.GlobalContext.Properties`. To je „thread-local“ obrazac koji ne radi u async svetu.

.NET 10 koristi `Activity.Current` i log scope. Middleware:

`src/BizSMS.Web/Middleware/CorrelationIdMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BizSMS.Web.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString("n");

        ctx.Response.Headers[HeaderName] = correlationId;
        ctx.Items[HeaderName] = correlationId;
        System.Diagnostics.Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }
}
```

Sve loggere kroz Serilog će sada imati `CorrelationId` property automatski.

Pomoćnik za pristup iz servisa:

```csharp
public static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext ctx)
        => ctx.Items[CorrelationIdMiddleware.HeaderName] as string ?? string.Empty;
}
```

Registracija (v. poglavlje 02, u `Program.cs`):

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Global exception middleware (zamena za `HandleErrorAttribute`)

Legacy je koristio `Application_Error` u `Global.asax`:

```csharp
protected void Application_Error(object sender, EventArgs e)
{
    var ex = Server.GetLastError();
    log.Error(ex.Message);
    // rerouting na ErrorController...
}
```

U .NET 10:

- **Production**: `UseExceptionHandler("/Error/General")` — ASP.NET Core sam radi rerouting, mi
  samo logujemo.
- **Development**: `UseDeveloperExceptionPage()`.

Custom middleware za dodatni kontekst (i JSON odgovor za API-je):

`src/BizSMS.Web/Middleware/GlobalExceptionMiddleware.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BizSMS.Web.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _log;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
        => (_next, _log) = (next, log);

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception on {Path} ({Method})", ctx.Request.Path, ctx.Request.Method);

            if (ctx.Response.HasStarted) throw;

            // API poziv → JSON; browser poziv → redirect
            if (ctx.Request.Headers.Accept.Any(h => h?.Contains("application/json") == true)
                || ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                ctx.Response.ContentType = "application/problem+json";
                var payload = new
                {
                    type = "https://httpstatuses.com/500",
                    title = "Interna greška",
                    status = 500,
                    traceId = ctx.GetCorrelationId()
                };
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            else
            {
                ctx.Response.Redirect("/Error/General");
            }
        }
    }
}
```

Registracija: umesto `UseExceptionHandler`, možeš koristiti isključivo ovaj middleware — pazi
samo da bude **rano** u pipeline-u, ali **iza** correlation-id middleware-a.

## MVC filter: RequireOtpConfirmed (pokrivamo u poglavlju 04)

Ovaj filter je već pokazan u poglavlju 04. Ovde ga referenciramo kao primer kada je „action
filter“ ispravan izbor: potrebno je da odluka zavisi od trenutne akcije i da može da uradi
`RedirectToAction`.

## MVC filter: ChangeFirstPasswordAttribute (redirekcija)

Legacy verzija je bila prazna. Prevešćemo je u koristan filter koji hvata svaku akciju osim
`Manage/ChangePassword` i tera korisnika sa `MustChangePassword=true` da promeni šifru:

`src/BizSMS.Web/Filters/ForceChangePasswordFilter.cs`:

```csharp
using BizSMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Filters;

public sealed class ForceChangePasswordFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _users;

    public ForceChangePasswordFilter(UserManager<ApplicationUser> users) => _users = users;

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        // Preskoči za anonimne i za samu stranicu promene lozinke
        if (ctx.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var routeAllowed = ctx.RouteData.Values["controller"]?.ToString() == "Manage"
                        && (ctx.RouteData.Values["action"]?.ToString() is "ChangePassword" or "ChangePasswordConfirmation");
        if (routeAllowed || ctx.RouteData.Values["controller"]?.ToString() == "Account")
        {
            await next();
            return;
        }

        var user = await _users.GetUserAsync(ctx.HttpContext.User);
        if (user is { MustChangePassword: true })
        {
            ctx.Result = new RedirectToActionResult("ChangePassword", "Manage", null);
            return;
        }

        await next();
    }
}
```

Registracija — kao **globalni** filter kroz DI:

```csharp
builder.Services.AddScoped<ForceChangePasswordFilter>();

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.AddService<ForceChangePasswordFilter>();       // DI resolved po request-u
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
});
```

## Audit request middleware

Prevod legacy pristupa gde je `Logger` beležio ime kontrolera/akcije preko globalnog konteksta.
Sada je to bezbedan async middleware koji beleži **završen** zahtev.

`src/BizSMS.Web/Middleware/AuditRequestMiddleware.cs`:

```csharp
using System.Diagnostics;
using BizSMS.Application.Abstractions;

namespace BizSMS.Web.Middleware;

public sealed class AuditRequestMiddleware
{
    private static readonly HashSet<string> IgnorePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health/live", "/health/ready", "/favicon.ico"
    };

    private readonly RequestDelegate _next;
    public AuditRequestMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, IAuditService audit)
    {
        if (IgnorePaths.Contains(ctx.Request.Path) || ctx.Request.Path.StartsWithSegments("/Content")
            || ctx.Request.Path.StartsWithSegments("/Scripts"))
        {
            await _next(ctx);
            return;
        }

        var sw = Stopwatch.StartNew();
        await _next(ctx);
        sw.Stop();

        // Loguj samo mutacione akcije + login flow (GET podaci nisu audit)
        var method = ctx.Request.Method;
        var status = ctx.Response.StatusCode;
        var isMutation = method is "POST" or "PUT" or "DELETE" or "PATCH";
        var isLogin = ctx.Request.Path.StartsWithSegments("/Account");
        if (!isMutation && !isLogin) return;

        await audit.LogAsync(
            eventType: $"{method} {ctx.Request.Path}",
            outcome: status < 400 ? "OK" : "Failed",
            payload: new
            {
                Status = status,
                DurationMs = sw.ElapsedMilliseconds,
                User = ctx.User?.Identity?.Name,
                CorrelationId = ctx.GetCorrelationId()
            },
            ct: ctx.RequestAborted);
    }
}
```

## MVC filter: DefaultApiLoggingAttribute (za detaljno logovanje API-ja)

Legacy je koristio `IActionFilter` da beleži tip pozvane API akcije. U .NET 10 pretpostavljamo da
`UseSerilogRequestLogging()` već pokriva request/response summary. Ako treba **payload logging**,
napravi `IAsyncActionFilter` koji radi to samo za određene rute:

```csharp
public sealed class ApiPayloadLoggingFilter : IAsyncActionFilter
{
    private readonly ILogger<ApiPayloadLoggingFilter> _log;
    public ApiPayloadLoggingFilter(ILogger<ApiPayloadLoggingFilter> log) => _log = log;

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        if (_log.IsEnabled(LogLevel.Debug))
        {
            _log.LogDebug("Invoking {Action} with {@Args}",
                ctx.ActionDescriptor.DisplayName, ctx.ActionArguments);
        }
        var result = await next();
        if (result.Exception is not null)
            _log.LogWarning(result.Exception, "Action {Action} threw", ctx.ActionDescriptor.DisplayName);
    }
}
```

Primeni samo tamo gde treba (npr. `[ServiceFilter(typeof(ApiPayloadLoggingFilter))]`).

## Before / After — HandleErrorAttribute vs middleware

**Legacy:**

```csharp
[HandleError]
public class HomeController : Controller { ... }

protected void Application_Error(object sender, EventArgs e)
{
    var ex = Server.GetLastError();
    log.Error(ex.Message);
    Response.Redirect("~/Error/General");
}
```

**.NET 10** (`Program.cs`):

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/General");
    app.UseStatusCodePagesWithReExecute("/Error/Http{0}");
}
app.UseMiddleware<GlobalExceptionMiddleware>();  // ako želiš JSON za API + custom fallback
```

Nema više `[HandleError]` — atribut je nasleđen iz System.Web.Mvc i ne postoji u ASP.NET Core.

## Before / After — RequireHttps global filter

**Legacy `FilterConfig`:**

```csharp
filters.Add(new RequireHttpsAttribute());
```

**.NET 10 `Program.cs`:**

```csharp
app.UseHttpsRedirection();
app.UseHsts();
```

Custom `RequireHttpsAttribute` iz legacy-ja (koji je dozvoljavao lokalni HTTP dev) više nije
potreban — Kestrel automatski koristi `applicationUrl` iz `launchSettings.json`, a HSTS se
uključuje samo u ne-Development okruženju.

## Before / After — AuthorizeApiUserAttribute (Web API)

Legacy je imao dve verzije (jednu za MVC, jednu za Web API) jer su Web API i MVC bili odvojeni.
U .NET 10 postoji **samo jedna** — `[Authorize]` policy-based, koji radi i za MVC i za API
kontrolere. Zameni obe legacy varijante sa:

```csharp
[Authorize(Policy = AuthPolicies.OtpConfirmed, Roles = Roles.BusinessUser)]
public sealed class SendSmsApiController : ControllerBase { ... }
```

## Order izvršavanja filtera i middleware-a (praktični saveti)

- **Autorizacija** je pre **action filter**-a: ako želiš da neka provera stvarno zaustavi izvršavanje
  pre nego što se model bind uradi, koristi `IAsyncAuthorizationFilter` (kao `RequireOtpConfirmed`).
- **Model binding** je pre `ActionFilter.OnActionExecuting`.
- **Result filter** (`OnResultExecuting`) trči nakon action-a, pre `Response.Body` writer-a.
- **Exception filter** hvata izuzetak iz action-a; **ne** hvata izuzetke iz middleware-a — za to
  postoji `UseExceptionHandler`.

## Checklist za code review

- [ ] Nema više `System.Web.Mvc.FilterAttribute` niti `System.Web.Mvc.AuthorizeAttribute`.
- [ ] Correlation-id se propagira kroz Serilog log scope.
- [ ] Exception handling je centralizovan u middleware-u (jedno mesto za logovanje 500-ova).
- [ ] Antiforgery se validira globalno kroz `AutoValidateAntiforgeryToken`.
- [ ] `RequireOtpConfirmed` postoji na svakom SMS send/schedule endpoint-u.
- [ ] `ForceChangePasswordFilter` je registrovan globalno i preskače login/change-password rute.
- [ ] Audit request middleware ne loguje statiku ni health check-ove.
- [ ] Filteri sa DI zavisnostima registrovani su kroz `AddService<T>` / `AddScoped<T>`, ne
      instanciraju se sa `new` u `Program.cs`.

## Najčešće greške i kako ih izbeći

1. **Middleware ne poziva `await _next(ctx)`** — request se zaglavi bez odgovora. Uvek u `try`
   grani pozovi next; u `catch` grani zaustavi eskalaciju samo ako svesno želiš.
2. **Middleware koristi scoped servis kroz konstruktor** — middleware je singleton po defaultu;
   scoped servisi se **prosleđuju kao parametri metode `Invoke`** (kao `IAuditService audit`
   u primeru gore).
3. **Rasporedjivanje globalnih filtera lokalno u kontroleru** — sve što je globalno registruj u
   `Program.cs`, ne po kontroleru; smanjuje šum i mogućnost zaborava.
4. **Mešanje MVC i Middleware odgovornosti** — ne pišite audit u action filter-u za sve akcije;
   pišite ga u middleware-u i uskoro ćete imati jedinstven format bez duplog rada.
5. **`HandleUnauthorizedRequest` u custom `AuthorizeAttribute`** — u ASP.NET Core to više nije
   isti model. Redirect na login se postiže preko `CookieAuthenticationOptions.LoginPath`, a 403
   preko `AccessDeniedPath`.
6. **`OutputCache` atribut na login stranici** — u .NET 10 koristi `[ResponseCache(NoStore=true, Duration=0)]`
   ili u middleware-u dodaj `Cache-Control: no-store` za `/Account/*`.
7. **`RedirectResult` sa spoljnim URL-om** — koristi `LocalRedirect` da izbegneš open redirect
   ranjivosti.
