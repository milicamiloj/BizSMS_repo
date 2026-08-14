# 02 — .NET 10 hosting model + MVC pipeline

## Svrha

Objasniti kako prevesti `Global.asax` + `App_Start/*` iz legacy MVC-a u novi
**minimal hosting model** (`Program.cs`) za .NET 10, uz zadržavanje MVC arhitekture
(Controllers + Views). Definisati kompletan middleware pipeline, konfiguraciju,
lokalizaciju, sesiju, antiforgery, error handling, HTTPS/HSTS i statičke fajlove.

## Koraci migracije

1. **Ukloni** `Global.asax`, `Global.asax.cs`, `App_Start/*`, `Web.config` (osim vrednosti koje ideš da
   prebaciš u `appsettings.json`).
2. **Kreiraj `Program.cs`** kao jedini entry point. Konfiguraciju uzimaj iz `appsettings.json` +
   `appsettings.{Environment}.json` + `IConfiguration` DI-a.
3. **Registruj servise** kroz DI extension metode (`AddInfrastructure`, `AddIdentityWithOtp`,
   `AddBackgroundJobs`, `AddApplication`) da bi `Program.cs` ostao pregledan.
4. **Podesi middleware redosled** — redosled u pipeline-u je kritičan (v. dijagram niže).
5. **Konfiguriši MVC** sa view-ovima, tag helper-ima, model binding-om, i **globalnim filterima**
   (Antiforgery, RequireHttps).
6. **Statika** (`wwwroot`) — kopiraj `Content/`, `Scripts/`, `favicon.ico` u `wwwroot/`.
7. **Lokalizacija** (`sr-Latn-RS`) — koristi `RequestLocalizationOptions` umesto legacy
   `CultureHelper`.
8. **Session** — koristi je samo ako je nužno (OTP flow). Legacy `Session["OtpPending"]`
   preseljavaj u trajniji mehanizam (v. poglavlje 04).
9. **Antiforgery** — global filter `AutoValidateAntiforgeryToken`, plus AJAX pattern sa
   `RequestVerificationToken` header-om.
10. **Error handling** — globalni exception middleware + status code re-execution na
    `/Error/{0}`.

## Middleware pipeline (redosled je važan)

```
+---------- REQUEST ----------+
| ForwardedHeaders            |   (iza IIS/Kestrel reverse proxy)
| Serilog request logging     |
| CorrelationId middleware    |   (v. poglavlje 05)
| Exception handler           |
| HSTS + HttpsRedirection     |
| StaticFiles                 |
| Routing                     |
| RequestLocalization         |
| Session (opciono)           |
| Authentication              |   (Identity + cookies)
| Authorization               |
| AntiforgeryValidation       |   (kroz global filter)
| Audit middleware            |   (piše ko/šta/kada)
| MVC endpoints               |
+---------- RESPONSE ---------+
```

## Program.cs — potpun primer

```csharp
using BizSMS.Application;
using BizSMS.Infrastructure;
using BizSMS.Web;
using BizSMS.Web.Filters;
using BizSMS.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracija (User Secrets u Dev, Env Vars u Prod)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging (Serilog)
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(sp)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "BizSMS"));

// Forwarded Headers (kad je iza IIS/Nginx-a)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// DI po slojevima
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityWithOtp(builder.Configuration);      // v. poglavlje 04
builder.Services.AddBackgroundJobs(builder.Configuration);       // v. poglavlje 06
builder.Services.AddAuditPipeline();                             // v. poglavlje 08

// MVC + globalni filteri
builder.Services.AddControllersWithViews(options =>
{
    // svaka POST/PUT/DELETE forma automatski validira antiforgery token
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // svi endpoint-i po defaultu zahtevaju auth (osim [AllowAnonymous])
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();

// FluentValidation (dodatno)
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

// Antiforgery za AJAX POST-ove
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

// Session (koristimo za privremeni OTP state; TTL kratak)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".BizSMS.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.IdleTimeout = TimeSpan.FromMinutes(15);
});

// HSTS + HTTPS
builder.Services.AddHttpsRedirection(o => o.HttpsPort = 443);
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});

// Lokalizacija (sr-Latn)
var supportedCultures = new[] { new CultureInfo("sr-Latn-RS") };
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture("sr-Latn-RS");
    o.SupportedCultures = supportedCultures;
    o.SupportedUICultures = supportedCultures;
});

// Health checks (Kubernetes/IIS ready)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BizSMS.Infrastructure.Persistence.AppDbContext>("db");

var app = builder.Build();

// ------------------- PIPELINE -------------------

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();     // v. poglavlje 05

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error/General");
    app.UseStatusCodePagesWithReExecute("/Error/Http{0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRequestLocalization();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AuditRequestMiddleware>();      // v. poglavlje 08

// endpoint mapiranje
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // samo liveness, ne dira DB
});
app.MapHealthChecks("/health/ready");

app.Run();
```

## appsettings.json — polazni

```json
{
  "ConnectionStrings": {
    "BizSms": "Server=SQL01;Database=BizSMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=BizSMS.Web"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ],
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "MSSqlServer",
        "Args": {
          "connectionString": "BizSms",
          "sinkOptionsSection": {
            "tableName": "BST_LOG",
            "autoCreateSqlTable": false
          }
        }
      }
    ]
  },
  "Otp": {
    "Length": 6,
    "TimeStepSeconds": 60,
    "MaxAttempts": 5
  },
  "Delta": {
    "CronExpression": "0 0 3 * * ?",
    "MaxParallelClients": 4,
    "CommandTimeoutSeconds": 300
  },
  "SmsGateway": {
    "BaseUrl": "https://sms.example.com/",
    "Alphanumeric": "MTS",
    "TimeoutSeconds": 30
  }
}
```

## Before / After — Global.asax → Program.cs

**Legacy (`Global.asax.cs`, skraćeno):**

```csharp
protected void Application_Start()
{
    AreaRegistration.RegisterAllAreas();
    GlobalConfiguration.Configure(WebApiConfig.Register);
    log4net.Config.XmlConfigurator.Configure(new FileInfo(Server.MapPath("~/Web.config")));
    FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
    RouteConfig.RegisterRoutes(RouteTable.Routes);
    BundleConfig.RegisterBundles(BundleTable.Bundles);
}

protected void Application_Error(object sender, EventArgs e) { ... }
```

**.NET 10 (`Program.cs`):**

- `AreaRegistration.RegisterAllAreas()` → `MapControllerRoute("areas", "{area:exists}/…")`
- `WebApiConfig.Register` → nije potrebno (API kontroleri koriste atribute rute)
- `log4net.Config.XmlConfigurator.Configure` → `builder.Host.UseSerilog(...)`
- `FilterConfig.RegisterGlobalFilters` → `AddControllersWithViews(options => options.Filters.Add(...))`
- `RouteConfig.RegisterRoutes` → `MapControllerRoute(...)`
- `BundleConfig` → `UseStaticFiles()` + WebOptimizer / build pipeline
- `Application_Error` → `UseExceptionHandler("/Error/General")`

## RouteConfig → attribute + conventional routing

Legacy `App_Start/RouteConfig.cs`:

```csharp
public static void RegisterRoutes(RouteCollection routes)
{
    routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
    routes.MapMvcAttributeRoutes();
    routes.MapRoute(
        name: "Default",
        url: "{controller}/{action}/{id}",
        defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
    );
}
```

Ekvivalent u .NET 10 `Program.cs`:

```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

`IgnoreRoute` za `.axd` više nije potreban. Attribute rutiranje radi automatski u
`app.MapControllers()` (ili implicitno kroz `MapControllerRoute` + `[Route]` na akciji).

## ErrorController u .NET 10

Legacy kontroler je imao akcije `General`, `Http400`, `Http401`, `Http403`, `Http404`. Zadrži to,
samo prilagodi potpise:

```csharp
[AllowAnonymous]
public sealed class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;
    public ErrorController(ILogger<ErrorController> logger) => _logger = logger;

    [HttpGet("/Error/General")]
    public IActionResult General()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        _logger.LogError(feature?.Error, "Unhandled error on {Path}", feature?.Path);
        return View();
    }

    [HttpGet("/Error/Http{code:int}")]
    public IActionResult HttpStatus(int code)
    {
        HttpContext.Response.StatusCode = code;
        return code switch
        {
            400 => View("Http400"),
            401 => View("Http401"),
            403 => View("Http403"),
            404 => View("Http404"),
            _   => View("General")
        };
    }
}
```

## AJAX antiforgery obrazac

U layout view-u (npr. `_Layout.cshtml`) uključi hidden token:

```cshtml
@Html.AntiForgeryToken()
<meta name="csrf-token" content="@Html.AntiForgeryToken()" />
```

JavaScript (u `wwwroot/js/site.js`):

```javascript
document.addEventListener("submit", (e) => { /* form-ovima ide automatski */ });

async function postJson(url, payload) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const resp = await fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token
        },
        body: JSON.stringify(payload)
    });
    return resp.json();
}
```

Server obrazac je već pokriven kroz `AutoValidateAntiforgeryTokenAttribute` (registrovan
globalno) + `AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`.

## Lokalizacija (sr-Latn-RS)

Legacy je koristio `CultureHelper`. U .NET 10 se koristi middleware, uz `.resx` fajlove u
`BizSMS.Web/Resources/Views/…`. Ako trenutno ne koristimo prevode, dovoljno je postaviti kulturu
(za formatiranje brojeva/datuma):

```csharp
app.Use(async (ctx, next) =>
{
    CultureInfo.CurrentCulture = new CultureInfo("sr-Latn-RS");
    CultureInfo.CurrentUICulture = new CultureInfo("sr-Latn-RS");
    await next();
});
```

## Health checks & IIS integracija

- U kontejneru: expose 8080 + Kestrel direktno.
- Iza IIS-a: doda `web.config` sa `AspNetCoreModuleV2` i `processPath="dotnet"` (skeleton generiše
  `dotnet publish`).
- Health check `/health/ready` konektuje se na DB (radi kao load balancer probe).

## Checklist za code review

- [ ] Nema `System.Web.Mvc` referenci.
- [ ] Sve akcije su `async Task<IActionResult>` (osim čistih view render-a).
- [ ] Filteri i middleware su registrovani u `Program.cs`, ne u kontrolerima.
- [ ] `AutoValidateAntiforgeryTokenAttribute` je globalno postavljen.
- [ ] `RequireHttps` više nije potreban kao attribute (koristimo `UseHttpsRedirection` + `UseHsts`).
- [ ] Konfiguracioni ključevi ne postoje u `appsettings.json` (odnosno idu u User Secrets/env).
- [ ] `UseAuthentication` je pre `UseAuthorization`, oba su pre `MapController*`.
- [ ] Ne postoji `System.Web.HttpContext.Current`; koristi se `IHttpContextAccessor` samo tamo
      gde je zaista neophodno.

## Najčešće greške i kako ih izbeći

1. **Pogrešan redosled middleware-a** — `UseRouting` mora doći pre `UseAuthorization`. Ako želiš
   auth pre statika (retko), gubiš mogućnost cache-a za statiku.
2. **`app.UseSession` bez `AddDistributedMemoryCache`** — bacaće runtime exception.
   Za produkciju razmotri Redis / SQL session provider.
3. **Zaboravljanje `UseForwardedHeaders`** — iza reverse proxy-ja `HttpContext.Request.Scheme`
   ostaje „http“, pa se HTTPS redirect vrti u petlji.
4. **Registracija filtera kao instance sa DI zavisnostima** — koristi
   `options.Filters.Add<AuditActionFilter>()` (bez `new`) da bi DI radio.
5. **Global `[Authorize]` bez `[AllowAnonymous]` na loginu** — svi zaboravljaju da postave
   `[AllowAnonymous]` na `AccountController.Login` posle globalne autorizacije.
6. **`GlobalConfiguration.Configure` za Web API** — više ne postoji. Za Web API kontrolere
   koristi atributnu rutu i `MapControllers()`.
7. **Bundling & minifikacija** — u .NET Core nema `BundleConfig`. Nemoj tražiti; koristi
   `WebOptimizer`, `Vite`, ili unapred minifikovane fajlove.
8. **Custom errors u `Web.config`** — više ne rade. Koristi `UseStatusCodePagesWithReExecute`.
