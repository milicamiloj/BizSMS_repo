## Svrha
Definiše ciljanu migraciju BizSMS monolita sa ASP.NET MVC (.NET Framework 4.5) na ASP.NET Core MVC na .NET 10, bez promene SQL Server šeme.

## Koraci migracije
1. Napraviti novi .NET 10 MVC projekat (`net10.0`) i prebaciti postojeće module po vertikalama (Admin, Klijent, API).
2. Zadržati postojeće SQL tabele i kolone, prevesti EF6 mapiranje na EF Core Fluent API.
3. Uvesti ASP.NET Core Identity (role: `Administrator`, `BusinessUser`) umesto legacy auth flow-a.
4. Dodati 2FA/OTP za login i dodatni OTP challenge pre `Send`/`Schedule` akcija.
5. Zameniti `FilterAttribute` obrasce middleware/global filter pristupom.
6. Migrirati jobove: dnevni delta sync preko postojeće SQL SP + obrada zakazanih poruka.
7. Standardizovati audit logging i correlation-id.
8. Uvesti servisni sloj za izveštaje/export i upload validacije.
9. Uraditi hardening, testove i cutover po fazama.

## Before/After primer
### Before (legacy `Global.asax.cs` + OWIN)
```csharp
protected void Application_Start()
{
    AreaRegistration.RegisterAllAreas();
    GlobalConfiguration.Configure(WebApiConfig.Register);
    FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
    RouteConfig.RegisterRoutes(RouteTable.Routes);
}
```

### After (.NET 10 `Program.cs`)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BizSmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BIZSMS")));
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<BizSmsDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

## Code snippets
### Predlog ciljnih projekata (monolit ostaje monolit)
```csharp
// BizSMS.Web (MVC + API)
// BizSMS.Application (servisi/use-case)
// BizSMS.Infrastructure (EF Core, Identity, SP, jobovi)
```

### Minimalni migration bootstrap
```csharp
public static class MigrationConstants
{
    public const string AppName = "BizSMS";
    public const string AdminRole = "Administrator";
    public const string BusinessRole = "BusinessUser";
}
```

## Checklist za code review
- [ ] Svuda je ciljna platforma `.NET 10`.
- [ ] MVC obrazac ostaje (`Controllers + Views`).
- [ ] SQL Server šema nije menjana.
- [ ] EF Core koristi postojeće tabele/kolone.
- [ ] Identity + 2FA + OTP pre slanja/zakazivanja je pokriven.
- [ ] Delta sync je i dalje SP-only.

## Najčešće greške i kako ih izbeći
- Mešanje “big-bang” i parcijalne migracije bez plana: migrirati po modulima i feature flag-ovima.
- Refaktorisanje domenskih pravila tokom migracije: prvo parity, pa optimizacija.
- Zaboravljen regulatorni STOP_ID tok za non-VPN brojeve: tretirati kao obavezno pravilo u pipeline-u slanja.
