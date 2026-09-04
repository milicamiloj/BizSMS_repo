## Svrha
Prikazuje prelaz sa `Global.asax`/OWIN na .NET 10 hosting model (`Program.cs`) uz MVC i postojeće rute.

## Koraci migracije
1. Ukloniti `Global.asax` bootstrap i prebaciti konfiguraciju u `Program.cs`.
2. Konfigurisati `AddControllersWithViews` + anti-forgery + cookie policy.
3. Definisati pipeline (`UseExceptionHandler`, `UseAuthentication`, `UseAuthorization`).
4. Prevesti route pravila iz `RouteConfig`.

## Before/After primer
### Before (legacy `FilterConfig.cs` + OWIN cookie)
```csharp
filters.Add(new RequireHttpsAttribute());

app.UseCookieAuthentication(new CookieAuthenticationOptions
{
    AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
    LoginPath = new PathString("/Account/Login")
});
```

### After (.NET 10 `Program.cs`)
```csharp
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<BizSmsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Error/Http403";
        opt.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
```

## Code snippets
### Kompletan minimalni `Program.cs`
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/General");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.Run();
```

## Checklist za code review
- [ ] Nema više `Global.asax` runtime zavisnosti.
- [ ] HTTPS i secure cookie su uključeni.
- [ ] MVC route parity je očuvan.
- [ ] Error handling je centralizovan.

## Najčešće greške i kako ih izbeći
- Pogrešan redosled middleware-a (`UseRouting` pre auth obavezno).
- Nedostaje `UseAuthentication()` pa role politike ne rade.
