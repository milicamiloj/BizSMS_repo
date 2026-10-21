# Migracija BizSMS: .NET Framework 4.5 / MVC5 → .NET 10 / ASP.NET Core MVC

Vodič za migraciju monolitne BizSMS aplikacije sa **.NET Framework 4.5, ASP.NET MVC5, EF6, ASP.NET Identity 2.x (OWIN)** na **.NET 10, ASP.NET Core MVC, EF Core 10, ASP.NET Core Identity**.

Baza ostaje **isti SQL Server**. Migracija se radi kao **novi SDK-style projekat** (side-by-side), jer FW→Core ne podržava in-place upgrade.

> **Konvencija:** blokovi `// PRE (FW)` prikazuju postojeći kod, `// POSLE (.NET 10)` cilj. Uz svaki snippet je naveden izvorni fajl.

---

## Sadržaj

1. [Pregled i strategija](#1-pregled-i-strategija)
2. [Priprema projekta i NuGet paketi](#2-priprema-projekta-i-nuget-paketi)
3. [Konfiguracija: Web.config → appsettings.json + secrets](#3-konfiguracija-webconfig--appsettingsjson--secrets)
4. [Bootstrap i pipeline: Global.asax + OWIN → Program.cs](#4-bootstrap-i-pipeline-globalasax--owin--programcs)
5. [DbContext i EF Core](#5-dbcontext-i-ef-core)
6. [Autentikacija: Identity 2.x → ASP.NET Core Identity](#6-autentikacija-identity-2x--aspnet-core-identity)
7. [Filteri i atributi → filteri/middleware](#7-filteri-i-atributi--filterimiddleware)
8. [Kontroleri i Views](#8-kontroleri-i-views)
9. [Logovanje i background poslovi (log4net, Hangfire)](#9-logovanje-i-background-poslovi)
10. [Eksterne integracije (SOAP, Oracle, ostalo)](#10-eksterne-integracije)
11. [Testovi i deployment](#11-testovi-i-deployment)
12. [Redosled izvođenja i checklist](#12-redosled-izvođenja-i-checklist)

---

## 1. Pregled i strategija

### Trenutni vs ciljni stack

| Komponenta | Trenutno (FW) | Cilj (.NET 10) |
|---|---|---|
| Runtime | .NET Framework 4.5 | .NET 10 |
| Web framework | ASP.NET MVC 5 + Web API 2 | ASP.NET Core MVC (objedinjeni kontroleri) |
| ORM | Entity Framework 6.5.1 | EF Core 10 |
| Baza | SQL Server | SQL Server (isti) |
| Auth | ASP.NET Identity 2.x na OWIN | ASP.NET Core Identity |
| Bootstrap | `Global.asax` + `App_Start/*` + OWIN `Startup` | `Program.cs` |
| Konfiguracija | `Web.config` | `appsettings.json` + User Secrets / env |
| DI | `OwinContext` / ručno | ugrađeni MS DI kontejner |
| Bundling | `System.Web.Optimization` | WebOptimizer / LibMan |
| Logovanje | log4net (AdoNet appender → SP) | log4net (radi na Core) ili Serilog |
| Background | Hangfire (OWIN) | Hangfire.AspNetCore |
| SOAP klijenti | WSE3 + Service/Web References | `dotnet-svcutil` / CoreWCF |

### Pristup

**Kreiraj nov projekat**, ne pokušavaj in-place. Preporučeno:

```
BizSMS.sln
 ├─ BizSMS/            (STARI FW projekat — ostaje dok se ne završi migracija)
 └─ BizSMS.Web/        (NOVI .NET 10 projekat)
```

Faze (detaljno u sekcijama ispod):
1. Skelet projekta + konfiguracija + `Program.cs`
2. EF Core `DbContext` nad postojećom bazom (baseline je **opcioni** korak posle gašenja legacy aplikacije — sekcija 5.3)
3. Identity (uključujući **kompatibilnost hash-eva lozinki**)
4. Filteri/atributi → filteri/middleware
5. Kontroleri + Views
6. log4net, Hangfire, SOAP/Oracle integracije
7. Testovi + deployment

### Ključni rizici

- **Hash lozinki**: Identity v2 i Core koriste različit default format. Bez mера, **postojeći korisnici ne mogu da se uloguju**. Rešenje u sekciji 6.
- **SOAP + WSE3**: `Microsoft.Web.Services3` (WSE) **ne postoji na .NET Core**. Custom `UsernameToken`/header politika se radi ručno. Sekcija 10.
- **EF6 → EF Core razlike**: lazy loading zahteva eksplicitan paket; neke EF6 konvencije se razlikuju. Sekcija 5.
- **Hardkodovani kredencijali**: `BizSMS/Web.config` sadrži produkcione DB/SDP/Oracle lozinke u čistom tekstu. **Izvuci ih iz koda i rotiraj.** Sekcija 3.

---

## 2. Priprema projekta i NuGet paketi

### 2.1 SDK-style .csproj

Stari `BizSMS/BizSMS.csproj` je verbozan, sa `packages.config`, `<Reference HintPath>` unosima i `TargetFrameworkVersion v4.5`. Novi projekat je kratak:

```xml
<!-- POSLE (.NET 10): BizSMS.Web/BizSMS.Web.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>BizSMS.Web</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="10.0.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.*" />
    <PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
    <PackageReference Include="Hangfire.SqlServer" Version="1.8.*" />
    <PackageReference Include="log4net" Version="3.*" />
    <PackageReference Include="LigerShark.WebOptimizer.Core" Version="3.*" />
    <PackageReference Include="ClosedXML" Version="0.105.*" />
    <PackageReference Include="RestSharp" Version="112.*" />
    <PackageReference Include="Otp.NET" Version="1.4.*" />
    <PackageReference Include="Oracle.ManagedDataAccess.Core" Version="23.*" />
    <PackageReference Include="System.ServiceModel.Http" Version="8.*" />
    <PackageReference Include="System.ServiceModel.Primitives" Version="8.*" />
  </ItemGroup>

</Project>
```

> U SDK-style projektu **svi `.cs` fajlovi u folderu se automatski uključuju** — nema više `<Compile Include>`. `bin/`, `obj/`, `packages/` više nisu deo projekta.

### 2.2 Mapiranje foldera

| Staro | Novo |
|---|---|
| `Global.asax` + `App_Start/*.cs` | `Program.cs` |
| `Web.config` (app deo) | `appsettings.json` + `Program.cs` |
| `Controllers/`, `Models/`, `Views/` | isto (uz izmene u kodu) |
| `Controllers/API/` | spojeno u `Controllers/` (objedinjeni kontroleri) |
| `Attributes/` | `Filters/` (+ middleware gde ima smisla) |
| `Content/`, `Scripts/`, `fonts/`, `images/` | `wwwroot/` (statika ide u wwwroot!) |
| `Migrations/` (EF6) | nova `Migrations/` (EF Core) — vidi sekciju 5 |

### 2.3 Tabela mapiranja NuGet paketa

| Stari paket | Novi paket / napomena |
|---|---|
| `EntityFramework` 6.5.1 | `Microsoft.EntityFrameworkCore.SqlServer` |
| `Microsoft.AspNet.Identity.Core/EntityFramework/Owin` | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| `Microsoft.AspNet.Mvc`, `Razor`, `WebPages` | ugrađeno u ASP.NET Core |
| `Microsoft.AspNet.WebApi.*`, `Owin`, `Microsoft.Owin.*` | ugrađeno u ASP.NET Core |
| `Microsoft.AspNet.Web.Optimization`, `WebGrease` | `LigerShark.WebOptimizer.Core` ili LibMan |
| `Hangfire.Core/SqlServer` | `Hangfire.AspNetCore` + `Hangfire.SqlServer` |
| `log4net` | `log4net` (radi na Core) ili migracija na Serilog |
| `EPPlus` 4.1.1 | **`ClosedXML`** (MIT) — EPPlus v5+ zahteva plaćenu komercijalnu licencu (sekcija 10.3) |
| `RestSharp` 105 | `RestSharp` 112.x (API se promenio) |
| `Otp.NET` | `Otp.NET` (radi na Core) |
| Oracle (`Oracle.ManagedDataAccess`) | `Oracle.ManagedDataAccess.Core` |
| `Microsoft.Web.Services3` (WSE) | **nema na Core** → CoreWCF / ručni SOAP header |
| `JWT` 1.3 | `System.IdentityModel.Tokens.Jwt` ili zadržati ako radi |
| `Newtonsoft.Json` | `System.Text.Json` (ili zadržati Newtonsoft) |

---

## 3. Konfiguracija: Web.config → appsettings.json + secrets

`BizSMS/Web.config` sadrži connection strings, `appSettings`, log4net sekciju, `system.serviceModel` klijent i security headere. Deli se na više mesta.

### 3.1 Connection strings + appSettings

```jsonc
// POSLE: BizSMS.Web/appsettings.json
{
  "ConnectionStrings": {
    "BizSMS": "Data Source=kavas22db.it.telekom.yu;Database=BizSMS;Integrated Security=False;User ID=bizsms;Password=__SECRET__;TrustServerCertificate=True",
    "HangfireDB": "Data Source=kavas22db.it.telekom.yu;Database=HangfireDB;Integrated Security=False;User ID=hangfire;Password=__SECRET__;TrustServerCertificate=True",
    "TIS": "..."
  },
  "Sdp": {
    "Url": "http://vassdpqa.telekom.rs:27010/ParlayXSmsAccess/services/SendSms",
    "Username": "S-UE1K1ZY7481_S-UE1K1ZY7481-1_2@P-NA46AFR7369",
    "Alphanumeric": "MTS"
  },
  "Unsubscribe": {
    "TextMtsFirstPart": "Odjava STOP ",
    "TextMtsLastPart": " na 4020, 0RSD",
    "TextNotInMtsFirstPart": "ODJAVA pozvati 0800 200 400 i ukucati: ",
    "TextNotInMtsLastPart": ", 0RSD"
  },
  "IntegrationAuth": { "BizSms": "__SECRET__" },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

> **Napomena o `providerName`**: u `Web.config` connection string je imao `providerName="System.Data.SqlClient"`. U EF Core provider se bira kodom (`UseSqlServer`), pa `providerName` više nije potreban. Dodaj `TrustServerCertificate=True` ako je SQL bez validnog sertifikata (nova `Microsoft.Data.SqlClient` podrazumeva enkripciju).

### 3.2 Secrets — NIKAD lozinke u appsettings.json u repou

`Web.config` ima lozinke u čistom tekstu (`Password=...` za BizSMS/HangfireDB, `SdpPassword=...`, Oracle `Password=...`). U .NET 10:

**Development** — User Secrets (nije u repou):
```bash
cd BizSMS.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:BizSMS" "Data Source=...;Password=PRAVA_LOZINKA;..."
dotnet user-secrets set "Sdp:Password" "PRAVA_LOZINKA"
```

**Production** — environment varijable (dvostruki underscore = nesting):
```bash
ConnectionStrings__BizSMS="Data Source=...;Password=...;"
Sdp__Password="..."
```

`IConfiguration` automatski spaja `appsettings.json` + User Secrets + env varijable (env ima prioritet).

> **Bezbednost:** izloženi produkcioni kredencijali u `Web.config` treba da se **rotiraju** (promeni lozinke na DB/SDP/Oracle) jer su bili u verzionisanom kodu.

### 3.3 Strongly-typed options

```csharp
// POSLE: BizSMS.Web/Configuration/SdpOptions.cs
public sealed class SdpOptions
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Alphanumeric { get; set; } = "MTS";
}
```
```csharp
// Program.cs
builder.Services.Configure<SdpOptions>(builder.Configuration.GetSection("Sdp"));
// upotreba: konstruktor prima IOptions<SdpOptions> opt; opt.Value.Url
```

### 3.4 Security headeri (customHeaders) i cookie zahtevi

`Web.config` je imao `X-Frame-Options: DENY`, `Content-Security-Policy: frame-ancestors 'none';` i `httpCookies requireSSL httpOnly`. U .NET 10 to ide kao middleware:

```csharp
// Program.cs (posle app = builder.Build();)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none';";
    await next();
});
```
Cookie `Secure`/`HttpOnly` se podešava u `ConfigureApplicationCookie` (sekcija 6).

---

## 4. Bootstrap i pipeline: Global.asax + OWIN → Program.cs

Sve iz `Global.asax.cs` (`Application_Start`, `Application_Error`), `App_Start/*` i OWIN `Startup.Auth.cs` seli se u **jedan `Program.cs`**.

### 4.1 Kompletan Program.cs (skelet)

```csharp
// POSLE: BizSMS.Web/Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using BizSMS.Web.Data;
using BizSMS.Web.Models;
using Hangfire;
using WebOptimizer;

var builder = WebApplication.CreateBuilder(args);

// --- log4net (zamena za log4net.Config.XmlConfigurator iz Application_Start) ---
var logRepo = log4net.LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly());
log4net.Config.XmlConfigurator.Configure(logRepo, new FileInfo("log4net.config"));

// --- DbContext (zamena za ApplicationDbContext.Create iz OWIN-a) ---
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseLazyLoadingProxies()
     .UseSqlServer(builder.Configuration.GetConnectionString("BizSMS")));

// --- Identity (zamena za CreatePerOwinContext u Startup.Auth) ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    o.SlidingExpiration = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.HttpOnly = true;
    o.Events.OnRedirectToLogin = ctx =>   // API grana → 401 umesto redirecta
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// custom Identity servisi (sekcija 6)
builder.Services.AddScoped<ISmsSender, SdpSmsSender>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, LegacyCompatPasswordHasher>();

// --- MVC + globalni filteri (zamena za FilterConfig) ---
builder.Services.AddControllersWithViews(o =>
{
    // RequireHttpsAttribute je bio globalni filter; na Core koristimo UseHttpsRedirection (dole)
    o.Filters.Add<DefaultLoggingFilter>();          // vidi sekciju 7
})
.AddViewLocalization();

// --- Bundling (zamena za BundleConfig) ---
builder.Services.AddWebOptimizer(pipeline =>
{
    pipeline.AddJavaScriptBundle("/bundles/custom.js", "js/Custom/send.sms.js");
    pipeline.AddCssBundle("/Content/css.css", "css/bootstrap.min.css", "css/site.css", "css/flag-icon.min.css");
    // ostali bundle-ovi po ugledu na BundleConfig.cs
});

// --- Hangfire (zamena za UseHangfireServer/Dashboard) ---
builder.Services.AddHangfire(c => c.UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireDB")));
builder.Services.AddHangfireServer();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// --- Middleware red (bitno je REDOSLED) ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/General");   // zamena za Application_Error
    app.UseStatusCodePagesWithReExecute("/Error/Http{0}");
    app.UseHsts();
}
app.UseHttpsRedirection();                        // zamena za RequireHttpsAttribute

// security headeri (sekcija 3.4)
app.Use(async (ctx, next) => { ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none';"; await next(); });

app.UseWebOptimizer();
app.UseStaticFiles();

// kultura (zamena za BeginExecuteCore u BaseController)
var supported = new[] { new CultureInfo("sr-Latn-RS"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("sr-Latn-RS"),
    SupportedCultures = supported,
    SupportedUICultures = supported,
    RequestCultureProviders = { new CookieRequestCultureProvider { CookieName = "_culture" } }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard();

// --- Rute (zamena za RouteConfig) ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
```

### 4.2 Application_Error → UseExceptionHandler + ErrorController

```csharp
// PRE (FW): Global.asax.cs → Application_Error mapira status kode na ErrorController akcije
//   500→General, 400→Http400, 401→Http401, 403→Http403, 404→Http404 i loguje ex.
```
Na Core to rade dve linije iz `Program.cs` gore (`UseExceptionHandler` + `UseStatusCodePagesWithReExecute`). Logovanje greške ide u sam `ErrorController`:

```csharp
// POSLE: Controllers/ErrorController.cs
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _log;
    public ErrorController(ILogger<ErrorController> log) => _log = log;

    [Route("Error/General")]
    public IActionResult General()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is { } ex) _log.LogError(ex, "Unhandled exception");
        Response.StatusCode = 500;
        return View();
    }

    [Route("Error/Http{code:int}")]
    public IActionResult Http(int code) { Response.StatusCode = code; return View($"Http{code}"); }
}
```

### 4.3 Šta se briše bez zamene

- `AreaRegistration.RegisterAllAreas()` — Areas se u Core rutiraju automatski.
- `AntiForgeryConfig.SuppressXFrameOptionsHeader` — X-Frame-Options sada kontrolišemo sami (sekcija 3.4).
- `ClientDataTypeModelValidatorProvider.ResourceClassKey` — validacione poruke se lokalizuju drugačije (`AddViewLocalization` + resursi).

---

## 5. DbContext i EF Core

### 5.1 ApplicationDbContext

```csharp
// PRE (FW): Models/IdentityModels.cs
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>   // System.Data.Entity + Microsoft.AspNet.Identity.EntityFramework
{
    public ApplicationDbContext() : base("BIZSMS", throwIfV1Schema: false)
    {
        Database.SetInitializer<ApplicationDbContext>(null);
    }
    public static ApplicationDbContext Create() => new ApplicationDbContext();

    public DbSet<GroupModel> Group { get; set; }
    // ... ostali DbSet-ovi

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Log>().MapToStoredProcedures();
    }
}
```

```csharp
// POSLE (.NET 10): Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Nema parameterless konstruktora ni Create() — DI ubacuje opcije (Program.cs AddDbContext)
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<GroupModel> Group { get; set; } = null!;
    public DbSet<ClientModel> Client { get; set; } = null!;
    public DbSet<MessageModel> Message { get; set; } = null!;
    public DbSet<NumbersModel> Numbers { get; set; } = null!;
    public DbSet<MessageNumberModel> MessagesNumbers { get; set; } = null!;
    public DbSet<MessageCostModel> MessageCost { get; set; } = null!;
    public DbSet<NumberTypeModel> NumberType { get; set; } = null!;
    public DbSet<AlphanumericModel> Alphanumeric { get; set; } = null!;
    public DbSet<GroupNumberModel> GroupNumbers { get; set; } = null!;
    public DbSet<Log> Logs { get; set; } = null!;
    public DbSet<TempImport> TempImport { get; set; } = null!;
    public DbSet<ClientContractsModel> ClientContract { get; set; } = null!;
    public DbSet<DenySendingReasonModel> DenySendingReason { get; set; } = null!;
    public DbSet<ScheduledSmsModel> ScheduledSms { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Zadrži postojeća imena Identity tabela ako se u bazi ne zovu AspNet* (vidi sekciju 6.4)
        // modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");

        // MapToStoredProcedures ekvivalent — Log se INSERT-uje preko SP-a:
        modelBuilder.Entity<Log>().InsertUsingStoredProcedure("Log_Insert", sp => { /* mapiraj parametre */ });
        // Alternativa ako je SP netrivijalan: ne mapiraj kroz EF, već pozovi SP raw SQL-om.
    }
}
```

**Ključne promene:**
- Namespace: `System.Data.Entity` → `Microsoft.EntityFrameworkCore`.
- Konstruktor sa imenom connection stringa → `DbContextOptions` (konekcija dolazi iz `Program.cs`).
- `Database.SetInitializer(null)` — **briše se**, EF Core nema initializere (ne pravi bazu sam osim ako pozoveš `EnsureCreated`/`Migrate`).
- `DbModelBuilder` → `ModelBuilder`.
- `MapToStoredProcedures()` → `InsertUsingStoredProcedure(...)` (EF Core 7+) ili raw SQL.

### 5.2 Entiteti — anotacije uglavnom ostaju

`Models/BizSMSModels.cs` koristi `[Table("BST_GROUPS")]`, `[Key]`, `[Column("Group_ID")]`, `[Required]`, `[StringLength]` — **sve to postoji i u EF Core**, kod entiteta uglavnom ostaje isti. Jedina česta izmena je lazy loading:

- `public virtual ClientModel Client { get; set; }` — `virtual` navigacije rade lazy loading **samo** uz `Microsoft.EntityFrameworkCore.Proxies` + `UseLazyLoadingProxies()` (već dodato u `Program.cs`). Ako ne želiš proxy, izbaci `virtual` i koristi eksplicitni `Include()`.

### 5.3 EF6 migracije → EF Core baseline (OPCIONO — posle gašenja legacy aplikacije)

Imaš **110 EF6 migracija** u `Migrations/`. **Ne portuju se.** Baza već postoji sa svim tabelama.

> **Odluka: baseline se NE radi tokom side-by-side perioda.** `BizSMS.Web` radi nad postojećom bazom bez baseline-a — EF Core ne proverava `__EFMigrationsHistory` pri pokretanju, a aplikacija ne poziva `Database.Migrate()` niti `EnsureCreated()` (i ne sme da ih poziva). Dok legacy FW aplikacija i `BizSMS.Web` dele istu bazu, izmene šeme se rade **ručnim idempotentnim SQL skriptama** u `BizSMS.Web/Data/Sql/` (primer: `bst-log-ip-address.sql`), kompatibilnim sa obe aplikacije (npr. nove kolone `NULL`, novi SP parametri sa podrazumevanom vrednošću). Svaku takvu izmenu odmah preneti i u EF Core model (anotacije/`OnModelCreating`).
>
> **Kada uraditi baseline:** tek kada se legacy aplikacija ugasi i odluči se da šemom od tada upravlja EF Core. Bez baseline-a, prva `dotnet ef migrations add ...` bi sadržala `CREATE TABLE` za **sve** postojeće tabele.

Cilj baseline-a: EF Core da zna da je „sve već primenjeno", da ne pokušava da kreira tabele.

Koraci (kada se baseline radi):

```bash
# 1. Generiši EF Core "Initial" migraciju iz trenutnog modela (opisuje CELU šemu)
cd BizSMS.Web
dotnet ef migrations add InitialBaseline

# 2. Generiši SQL skriptu te migracije da je PROVERIŠ (ne izvršava DDL na prod bazi!)
dotnet ef migrations script -o baseline.sql
```

EF Core prati primenjene migracije u tabeli `__EFMigrationsHistory`. Da markiraš `InitialBaseline` kao već primenjenu **bez izvršavanja DDL-a**, ubaci samo red u istoriju:

```sql
-- Pokreni RUČNO na postojećoj bazi (ne kroz `dotnet ef database update`):
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

-- MigrationId = timestamp + ime iz Migrations/ foldera (npr. 20260915120000_InitialBaseline)
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915120000_InitialBaseline', N'10.0.0');
```

Od tada, svaka **nova** izmena šeme ide normalno:
```bash
dotnet ef migrations add DodajKolonuX
dotnet ef database update      # primenjuje SAMO nove migracije
```

> **Validacija baseline-a:** uporedi `baseline.sql` sa stvarnom šemom (imena tabela `BST_*`, `AspNet*`, tipovi kolona, uključujući sve izmene uvedene ručnim skriptama iz `Data/Sql/`, npr. `BST_LOG.IP_Address`). Ako se EF Core model ne poklapa 100% sa bazom, prilagodi anotacije/`OnModelCreating` **pre** nego što ubaciš red u `__EFMigrationsHistory`. Stara EF6 `__MigrationHistory` tabela se može ostaviti (ignoriše je EF Core) ili obrisati posle provere.

---

## 6. Autentikacija: Identity 2.x → ASP.NET Core Identity

Najkompleksniji deo. Trenutno: `Startup.Auth.cs` + `IdentityConfig.cs` na OWIN-u, sa `CreatePerOwinContext`, custom `ApplicationUserManager`/`ApplicationSignInManager`, 2FA telefonom, `SmsService` preko SOAP-a.

### 6.1 ApplicationUser

```csharp
// PRE (FW): Models/IdentityModels.cs
public class ApplicationUser : IdentityUser   // Microsoft.AspNet.Identity.EntityFramework
{
    [Column("Is_Canceled")] public bool IsCanceled { get; set; }
    [Column("Client_ID")]  public virtual int ClientID { get; set; }
    [Column("PhoneCodeSentAt")] public DateTime? PhoneCodeSentAt { get; set; }
    public virtual ClientModel Client { get; set; }
    public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager) { ... }
}
```

```csharp
// POSLE (.NET 10): Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;   // <-- Core Identity

public class ApplicationUser : IdentityUser   // string Id kao i pre
{
    [Column("Is_Canceled")] public bool IsCanceled { get; set; }
    [Column("Is_Deleted")]  public bool IsDeleted { get; set; }
    [Column("Client_ID")]   public int ClientID { get; set; }
    [Column("PhoneCodeSentAt")] public DateTime? PhoneCodeSentAt { get; set; }

    public virtual ClientModel? Client { get; set; }
    public virtual ICollection<MessageModel>? Messages { get; set; }
    public virtual ICollection<DenySendingReasonModel>? DenySendingReasons { get; set; }
    // GenerateUserIdentityAsync se BRIŠE — custom claim-ove radi ClaimsPrincipalFactory (6.3)
}
```

### 6.2 UserManager/SignInManager — nema više Create() fabrika

`ApplicationUserManager.Create` i `ApplicationSignInManager.Create` iz `IdentityConfig.cs` se **brišu**. Sva ta konfiguracija (password/lockout/2FA opcije) sada je u `AddIdentity(...)` u `Program.cs` (sekcija 4.1). `UserManager<ApplicationUser>` i `SignInManager<ApplicationUser>` se dobijaju **preko DI** (konstruktor kontrolera), umesto `HttpContext.GetOwinContext().GetUserManager<...>()`.

### 6.3 Custom claims: GenerateUserIdentityAsync → ClaimsPrincipalFactory

```csharp
// POSLE: Identity/AppClaimsPrincipalFactory.cs
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options) : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim("ClientID", user.ClientID.ToString()));  // custom claim-ovi ovde
        return id;
    }
}
```
(Registrovano u `Program.cs`.)

### 6.4 KRITIČNO: kompatibilnost hash-eva lozinki

ASP.NET Identity **v2** i ASP.NET Core Identity koriste različit default PBKDF2 format. Ako ništa ne uradiš, **svi postojeći korisnici dobiju „pogrešna lozinka"**. Postoje dve opcije:

**Opcija A (najčešće dovoljna) — CompatibilityMode:**
Ako su lozinke pravljene Identity v3+ formatom, dovoljno je:
```csharp
// Program.cs
builder.Services.Configure<PasswordHasherOptions>(o =>
    o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2);
```

**Opcija B (siguran put) — custom hasher koji prepoznaje stari format i re-hešuje:**
```csharp
// POSLE: Identity/LegacyCompatPasswordHasher.cs
public class LegacyCompatPasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly PasswordHasher<ApplicationUser> _core = new();

    public string HashPassword(ApplicationUser user, string password)
        => _core.HashPassword(user, password);   // novi upisi = novi (Core) format

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        // 1. probaj Core format
        var result = _core.VerifyHashedPassword(user, hashedPassword, providedPassword);
        if (result != PasswordVerificationResult.Failed) return result;

        // 2. probaj STARI Identity v2 PBKDF2 format
        if (VerifyLegacyV2(hashedPassword, providedPassword))
            return PasswordVerificationResult.SuccessRehashNeeded; // signal da re-hešuje

        return PasswordVerificationResult.Failed;
    }

    // Reprodukcija Identity v2 Crypto.VerifyHashedPassword (PBKDF2-HMAC-SHA1, 1000 iter, 16B salt, 32B subkey)
    private static bool VerifyLegacyV2(string hashedPassword, string password)
    {
        byte[] hashedBytes;
        try { hashedBytes = Convert.FromBase64String(hashedPassword); } catch { return false; }
        if (hashedBytes.Length != 49 || hashedBytes[0] != 0x00) return false;

        var salt = new byte[16];
        Buffer.BlockCopy(hashedBytes, 1, salt, 0, 16);
        var storedSubkey = new byte[32];
        Buffer.BlockCopy(hashedBytes, 17, storedSubkey, 0, 32);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 1000, HashAlgorithmName.SHA1);
        var actualSubkey = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(actualSubkey, storedSubkey);
    }
}
```
`SuccessRehashNeeded` tera `SignInManager` da automatski upiše lozinku u novom formatu pri sledećem uspešnom loginu — postepena migracija bez reseta lozinki.

**Imena Identity tabela:** ako se u bazi ne zovu `AspNetUsers`/`AspNetRoles`/... (u `IdentityModels.cs` je zakomentarisano mapiranje na `BST_USERS`/`BSL_ROLES`), zadrži postojeća imena u `OnModelCreating` (sekcija 5.1) da EF Core čita prave tabele.

### 6.5 2FA i SMS servis

`IdentityConfig.cs` registruje `PhoneNumberTokenProvider` i `SmsService : IIdentityMessageService` (šalje kod preko SOAP `SendSmsService`). Na Core:

- 2FA telefon token provider dolazi kroz `AddDefaultTokenProviders()` (`PhoneNumberTokenProvider` je uključen).
- `IIdentityMessageService` **ne postoji** na Core → napravi svoj interfejs, zadrži SOAP logiku:

```csharp
// POSLE: Services/ISmsSender.cs
public interface ISmsSender { Task SendAsync(string phoneNumber, string message); }

// Services/SdpSmsSender.cs  (zadržava sendSms SOAP poziv iz starog SmsService)
public class SdpSmsSender : ISmsSender
{
    private readonly SdpOptions _opt;
    public SdpSmsSender(IOptions<SdpOptions> opt) => _opt = opt.Value;

    public Task SendAsync(string phoneNumber, string message)
    {
        // SOAP klijent generisan dotnet-svcutil (sekcija 10):
        var client = SoapClients.CreateSendSms(_opt);
        var to = new[] { "tel:381" + phoneNumber.Substring(1) };
        client.sendSms(to, _opt.Alphanumeric, new ChargingInformation(), message);
        return Task.CompletedTask;
    }
}
```
U toku slanja 2FA koda: `var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Phone"); await _smsSender.SendAsync(user.PhoneNumber, $"Your security code is {code}");`

---

## 7. Filteri i atributi → filteri/middleware

Folder `Attributes/`. Ovo je deo koji si eksplicitno tražio (FilterAttribute → middleware).

### 7.1 AuthorizeUserAttribute (proverava IsCanceled)

```csharp
// PRE (FW): Attributes/AuthorizeAttributes.cs
public class AuthorizeUserAttribute : AuthorizeAttribute   // System.Web.Mvc
{
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
        var userId = httpContext.User.Identity.GetUserId();
        var user = HttpContext.Current.GetOwinContext()
            .GetUserManager<ApplicationUserManager>().FindById(userId);
        if (user == null || user.IsCanceled) return false;
        return base.AuthorizeCore(httpContext);
    }
    protected override void HandleUnauthorizedRequest(AuthorizationContext ctx)
    {
        if (!ctx.HttpContext.User.Identity.IsAuthenticated)
            ctx.Result = new RedirectResult("~/Account/Login?sessionExpired=true");
        else
            ctx.Result = new HttpStatusCodeResult(403);
    }
}
```

Na Core: custom `AuthorizeAttribute` + `IAsyncAuthorizationFilter` (dobija `UserManager` iz DI-a):

```csharp
// POSLE: Filters/AuthorizeUserAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeUserAttribute : TypeFilterAttribute
{
    public AuthorizeUserAttribute() : base(typeof(Impl)) { }

    private class Impl : IAsyncAuthorizationFilter
    {
        private readonly UserManager<ApplicationUser> _users;
        public Impl(UserManager<ApplicationUser> users) => _users = users;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
        {
            var principal = ctx.HttpContext.User;
            if (principal?.Identity is not { IsAuthenticated: true })
            {
                ctx.Result = new RedirectResult("~/Account/Login?sessionExpired=true");
                return;
            }
            var user = await _users.GetUserAsync(principal);
            if (user is null || user.IsCanceled)
            {
                ctx.Result = new StatusCodeResult(403);
                return;
            }
        }
    }
}
```
Upotreba na kontroleru je ista: `[AuthorizeUser]`.

### 7.2 AuthorizeApiUserAttribute → objedinjeni [Authorize] + JSON 401/403

Na Core su MVC i Web API **jedan** framework, pa nema odvojenog `System.Web.Http.AuthorizeAttribute`. Za API rute koje treba da vraćaju **JSON status umesto redirecta**, to smo već rešili u `Program.cs` (`OnRedirectToLogin` za `/api`). Za 403 na API-ju:

```csharp
// Program.cs (u ConfigureApplicationCookie)
o.Events.OnRedirectToAccessDenied = ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
    else
        ctx.Response.Redirect(ctx.RedirectUri);
    return Task.CompletedTask;
};
```
Na API kontrolerima koristi standardni `[Authorize]`.

### 7.3 RequireHttpsAttribute → UseHttpsRedirection + HSTS

```csharp
// PRE (FW): Attributes/RequireHttpsAttribute.cs — custom, vraća 403 ako scheme != https
//           registrovan globalno u FilterConfig.cs
```
Na Core se ne piše ništa custom — sve rešava pipeline (već u `Program.cs`):
```csharp
app.UseHttpsRedirection();   // + app.UseHsts() u produkciji
```
Ako baš treba per-akcija, postoji ugrađen `[RequireHttps]`.

### 7.4 DefaultApiLoggingAttribute + BaseController logging → IActionFilter

```csharp
// PRE (FW): Attributes/DefaultApiLoggingAttribute.cs i BaseController.OnActionExecuting
//           postavljaju controller/action u log4net i loguju "Default logging"
```
```csharp
// POSLE: Filters/DefaultLoggingFilter.cs  (globalno registrovan u Program.cs)
public class DefaultLoggingFilter : IActionFilter
{
    private readonly ILogger<DefaultLoggingFilter> _log;
    public DefaultLoggingFilter(ILogger<DefaultLoggingFilter> log) => _log = log;

    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        var rd = ctx.RouteData.Values;
        // log4net properties (za AdoNet appender koji upisuje Controller/Action) — vidi sekciju 9
        log4net.ThreadContext.Properties["Controller"] = rd["controller"];
        log4net.ThreadContext.Properties["Action"] = rd["action"];
        _log.LogInformation("Default logging");
    }
    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
```
> Napomena: `GlobalContext` iz starog `Logger`-a je globalan po aplikaciji (nije thread-safe za per-request); na Core koristi `ThreadContext`/`LogicalThreadContext`.

### 7.5 ChangeFirstPasswordAttribute

Stari je prazan (`OnActionExecuting` bez tela). Skica na Core (`IActionFilter`) ako treba da tera promenu lozinke pri prvom loginu:
```csharp
// POSLE: Filters/ChangeFirstPasswordFilter.cs
public class ChangeFirstPasswordAttribute : TypeFilterAttribute
{
    public ChangeFirstPasswordAttribute() : base(typeof(Impl)) { }
    private class Impl : IAsyncActionFilter
    {
        private readonly UserManager<ApplicationUser> _users;
        public Impl(UserManager<ApplicationUser> users) => _users = users;
        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            // npr. proveri flag na useru i redirektuj na /Manage/ChangePassword
            await next();
        }
    }
}
```

---

## 8. Kontroleri i Views

### 8.1 BaseController

```csharp
// PRE (FW): Controllers/BaseController.cs
public class BaseController : Controller   // System.Web.Mvc
{
    private ApplicationDbContext _db;
    public BaseController() { db = new ApplicationDbContext(); }
    public UserManager<ApplicationUser> UserManager =>
        _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
    protected override void Dispose(bool disposing) { /* ručno dispose db i userManager */ }
    protected override IAsyncResult BeginExecuteCore(...) { /* postavlja kulturu iz _culture cookie */ }
}
```

```csharp
// POSLE (.NET 10): Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

public abstract class BaseController : Controller
{
    protected readonly ApplicationDbContext Db;
    protected readonly UserManager<ApplicationUser> UserManager;

    protected BaseController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        Db = db;                    // DI ubacuje i upravlja lifecycle-om
        UserManager = userManager;  // nema više OwinContext.GetUserManager
    }
    // Dispose se BRIŠE — DI dispose-uje scoped servise
    // BeginExecuteCore/kultura se BRIŠE — radi UseRequestLocalization (Program.cs)
}
```
Izvedeni kontroleri prosleđuju zavisnosti kroz `base(...)`:
```csharp
public class HomeController : BaseController
{
    public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> um) : base(db, um) { }
}
```

### 8.2 Web API kontroleri (Controllers/API/)

```csharp
// PRE (FW): ApiController + System.Web.Http atributi
public class UserController : ApiController { public IHttpActionResult Get() { ... } }
```
```csharp
// POSLE: ControllerBase + [ApiController]
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]/[action]")]   // odgovara staroj ruti api/{controller}/{action}/{id}
public class UserController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(/* ... */);
}
```
Mapiranja: `IHttpActionResult`→`IActionResult`; `Ok()/BadRequest()/NotFound()` postoje i ovde; `Request.CreateResponse(...)` → `return StatusCode(...)`/`Ok(...)`.

### 8.3 HttpContext.Current

Bilo gde u kodu gde koristiš `HttpContext.Current` (npr. u helperima) → injektuj `IHttpContextAccessor` (registrovan u `Program.cs`):
```csharp
// POSLE
public class NekiHelper
{
    private readonly IHttpContextAccessor _accessor;
    public NekiHelper(IHttpContextAccessor accessor) => _accessor = accessor;
    private HttpContext Ctx => _accessor.HttpContext!;
}
```

### 8.4 Views

- `_ViewStart.cshtml`, `_Layout.cshtml`, partial-i (`_LoginPartial`) **uglavnom rade** uz manje izmene.
- Dodaj **`Views/_ViewImports.cshtml`**:
  ```cshtml
  @using BizSMS.Web
  @using BizSMS.Web.Models
  @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
  ```
- `@Scripts.Render("~/bundles/custom")` / `@Styles.Render("~/Content/css")` → WebOptimizer tag ili obični tagovi:
  ```cshtml
  <link rel="stylesheet" href="/Content/css.css" />
  <script src="/bundles/custom.js"></script>
  ```
- `@Html.*` helperi postoje; preporuka je prelazak na **tag helpers** (`asp-for`, `asp-action`, `asp-controller`) tamo gde je zgodno.
- Statika: sve iz `Content/`, `Scripts/`, `fonts/`, `images/` seli se u **`wwwroot/`** (npr. `wwwroot/css`, `wwwroot/js`, `wwwroot/lib`), jer se samo `wwwroot` servira kao statika.
- Anti-forgery: `@Html.AntiForgeryToken()` → forme sa tag helper-ima automatski ubacuju token; validacija `[ValidateAntiForgeryToken]` radi isto.

---

## 9. Logovanje i background poslovi

### 9.1 log4net

log4net radi na .NET Core, ali konfiguracija **više nije u `Web.config`** (koji ne postoji). Prebaci `<log4net>` sekciju iz `Web.config` u zaseban **`log4net.config`** (Copy to Output Directory = Copy if newer). Inicijalizacija je u `Program.cs` (sekcija 4.1).

`AdoNetAppender` koji upisuje u SP `Log_Insert` (sa parametrima `@log_date`, `@log_level`, `@controller`, `@action`, ...) ostaje **nepromenjen** — samo prebaci XML. `connectionStringName="BizSMS"` u log4net-u ne čita `appsettings.json`; navedi pun connection string u `log4net.config` ili koristi `ConnectionString` element.

`Helpers/Logger.cs` možeš zadržati (fasada oko `log4net.ILog`) ili postepeno preći na `ILogger<T>`. Za `%property{Controller}`/`%property{Action}` koristi `log4net.ThreadContext.Properties[...]` (sekcija 7.4) umesto `GlobalContext`.

### 9.2 Hangfire

```csharp
// PRE (FW): Startup.Auth.cs
GlobalConfiguration.Configuration.UseSqlServerStorage("HangfireDB");
app.UseHangfireDashboard();
app.UseHangfireServer();
```
```csharp
// POSLE (.NET 10): Program.cs (već u sekciji 4.1)
builder.Services.AddHangfire(c => c.UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireDB")));
builder.Services.AddHangfireServer();
// ...
app.UseHangfireDashboard();   // opciono: authorization filter za dashboard u produkciji
```
Recurring/enqueue pozivi (`BackgroundJob.Enqueue`, `RecurringJob.AddOrUpdate`) rade isto.

---

## 10. Eksterne integracije

### 10.1 SOAP servisi (LDAPCheck, http2sms, SDP SendSms) + WSE3

`Service References/` i `Web References/` (stari WCF/ASMX proxy-ji) i `Microsoft.Web.Services3` (WSE) **ne rade na .NET Core**. Regeneriši klijente:

```bash
dotnet tool install --global dotnet-svcutil
# za svaki WSDL/servis:
dotnet-svcutil http://iib.telekom.rs:7950/SelfCareWS/LdapCheck?wsdl --outputFile Connected/LdapCheck.cs --namespace "*,BizSMS.Web.Soap.Ldap"
dotnet-svcutil <SDP_SendSms_WSDL> --outputFile Connected/SendSms.cs --namespace "*,BizSMS.Web.Soap.Sdp"
```
Endpoint konfiguracija (bila u `system.serviceModel` u `Web.config`) sada se pravi kodom:
```csharp
// POSLE: Soap/SoapClients.cs
var binding = new System.ServiceModel.BasicHttpBinding();
var endpoint = new System.ServiceModel.EndpointAddress(_opt.Url);
var client = new SendSmsServiceClient(binding, endpoint);
```

**WSE `UsernameToken` / custom header politika** (stari `IdentityConfig.SmsService.sendSms` koristi `Policy`/`CustomHeadersAssertion`): WSE nema zamenu. SDP koristi Basic/username token — dodaj SOAP header ili HTTP Basic ručno:
```csharp
// HTTP Basic (ako SDP to prihvata)
client.ClientCredentials.UserName.UserName = _opt.Username;
client.ClientCredentials.UserName.Password = _opt.Password;
// ili custom SOAP header preko IClientMessageInspector / OperationContextScope
```
> Ovo zahteva verifikaciju kako tačno SDP očekuje autentikaciju (WS-Security UsernameToken vs Basic). Testirati na QA endpoint-u (`vassdpqa...`).

### 10.2 Oracle (TIS konekcija)

`Oracle.ManagedDataAccess` → **`Oracle.ManagedDataAccess.Core`**. API je isti (`OracleConnection`, `OracleCommand`). Connection string ide u `appsettings.json`/secrets. `System.Data.OracleClient` (deprecated) se ne koristi.

### 10.3 Ostalo

- **EPPlus → ClosedXML (MIT)**: legacy EPPlus 4.1.1 je bio LGPL (besplatan), ali od v5 EPPlus je pod Polyform Noncommercial licencom — za komercijalnu app zahteva plaćenu licencu. Zato je u `BizSMS.Web` EPPlus zamenjen paketom **ClosedXML** (bez licencnog podešavanja u `Program.cs`). Jedina upotreba je čitanje uploadovanog `.xlsx` u `GroupController.GetDataFromExcel`:
  ```csharp
  // PRE (EPPlus)
  using var xl = new ExcelPackage(new FileInfo(filePath));
  var ws = xl.Workbook.Worksheets.First();
  var start = ws.Dimension.Start; var end = ws.Dimension.End;
  var v = ws.Cells[row, col].Value?.ToString() ?? "";

  // POSLE (ClosedXML)
  using var wb = new XLWorkbook(filePath);
  var ws = wb.Worksheets.First();
  var range = ws.RangeUsed();            // null za prazan list
  var v = ws.Cell(row, col).Value.ToString(); // prazna ćelija → ""
  ```
  Ponašanje je isto kao legacy: broj upisan u Excel kao broj (ne tekst) gubi vodeću nulu.
- **RestSharp**: 105 → 112 ima drugačiji API (`RestClient`/`RestRequest` konstruktori, async pozivi). Proveriti svaki poziv.
- **JWT / Otp.NET**: rade na Core; proveriti verzije.

---

## 11. Testovi i deployment

### 11.1 BizSMS.Tests

Konvertuj u SDK-style test projekat:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />           <!-- ili MSTest -->
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\BizSMS.Web\BizSMS.Web.csproj" />
  </ItemGroup>
</Project>
```
Za integracione testove kontrolera koristi `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`).

### 11.2 Deployment (IIS)

.NET 10 na IIS zahteva **ASP.NET Core Module (ANCM)** i **Hosting Bundle** na serveru. `web.config` za IIS se **generiše automatski** pri publish-u:
```xml
<!-- generisan web.config -->
<aspNetCore processPath="dotnet" arguments=".\BizSMS.Web.dll"
            hostingModel="inprocess" stdoutLogEnabled="false" />
```
Publish:
```bash
dotnet publish BizSMS.Web -c Release -o ./publish
```
- Preporuka: **in-process** hosting (najbrže na IIS).
- Statiku više ne servira IIS iz root-a nego ASP.NET Core iz `wwwroot`.
- HTTPS/HSTS je u aplikaciji (`UseHttpsRedirection`/`UseHsts`), IIS binding i dalje treba sertifikat.

---

## 12. Redosled izvođenja i checklist

Preporučeni redosled:

1. [ ] Kreiraj `BizSMS.Web` (SDK-style) uz stari projekat u istom solution-u (sekcija 2).
2. [ ] `appsettings.json` + User Secrets; **rotiraj izložene lozinke** (sekcija 3).
3. [ ] `Program.cs` skelet (bez Identity/Hangfire za početak) — da se digne prazna app (sekcija 4).
4. [ ] Prekopiraj entitete (`Models/*`) i `ApplicationDbContext` na EF Core (sekcija 5.1–5.2).
5. [ ] ~~EF Core baseline~~ — **odloženo, opciono posle gašenja legacy aplikacije** (sekcija 5.3); do tada izmene šeme ručnim skriptama u `BizSMS.Web/Data/Sql/`.
6. [ ] Identity: `ApplicationUser`, `AddIdentity`, cookie, **hash kompatibilnost** (sekcija 6).
7. [ ] Filteri/atributi → filteri/middleware (sekcija 7).
8. [ ] Prebaci kontrolere (MVC + API objedinjeno) i `BaseController` (sekcija 8).
9. [ ] Prebaci Views + statiku u `wwwroot`; WebOptimizer bundle-ovi (sekcija 8.4).
10. [ ] log4net (`log4net.config`) + Hangfire (sekcija 9).
11. [ ] SOAP klijenti (svcutil) + Oracle + ClosedXML (umesto EPPlus)/RestSharp (sekcija 10).
12. [ ] Testovi + IIS deployment (sekcija 11).

### Kontrolne tačke (verifikacija)

- [ ] `dotnet build` prolazi bez grešaka.
- [ ] *(Samo ako se radi baseline, posle gašenja legacy aplikacije)* `dotnet ef migrations script` baseline ne sadrži `DROP`/`CREATE` za postojeće `BST_*`/`AspNet*` tabele posle usklađivanja modela.
- [ ] **Postojeći korisnik se uloguje** starom lozinkom (hash kompatibilnost radi).
- [ ] Nakon logina, lozinka se re-hešuje u novi format (Opcija B).
- [ ] 2FA SMS kod stiže i verifikuje se.
- [ ] Neautorizovan API poziv vraća **401 JSON**, ne HTML redirect.
- [ ] Zabranjen (403) API poziv vraća **403**, ne redirect.
- [ ] Hangfire dashboard radi; log upisi stižu u SP `Log_Insert`.
- [ ] SOAP pozivi ka SDP (slanje SMS) i LDAP check rade na QA endpoint-ima.
- [ ] Kultura `sr-Latn-RS` i decimalni separator ispravni (RequestLocalization).

---

### Napomena o bezbednosti (prioritet)

`BizSMS/Web.config` je u repou sadržavao produkcione kredencijale u čistom tekstu (SQL `bizsms`/`hangfire`, SDP, Oracle `ebil`). Nezavisno od migracije:
1. **Rotiraj sve te lozinke.**
2. Ubuduće ih drži u User Secrets (dev) / env varijablama ili secret store-u (prod), nikad u verzionisanim fajlovima.
