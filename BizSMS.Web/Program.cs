using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Identity;
using BizSMS.Web.Models;
using BizSMS.Web.Services;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// EPPlus v7 licenca (⚠ NonCommercial je privremeno — komercijalna app zahteva plaćenu licencu, Faza 5 §10.3)
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// DbContext (EF Core, SQL Server, lazy loading proxies — app zavisi od lazy load-a, nema .Include())
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseLazyLoadingProxies()
           .UseSqlServer(builder.Configuration.GetConnectionString("BizSMS")));

//builder.Services.Configure<SoapCredentialsOptions>(
//    builder.Configuration.GetSection("SoapCredentials"));

// Identity (zamena za OWIN CreatePerOwinContext iz Startup.Auth) — pravila iz IdentityConfig.cs
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

// Legacy-compat hasher: postojeći korisnici rade starim lozinkama (re-hash pri prvom loginu)
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, LegacyCompatPasswordHasher>();

// Zamena za legacy GenerateUserIdentityAsync — dodaje ClientID claim u identitet
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();

// SDP SMS (2FA kod) — zamena za legacy SmsService/SDPSendSms (WSE3) SOAP klijent
builder.Services.Configure<SdpOptions>(builder.Configuration.GetSection("Sdp"));
builder.Services.AddHttpClient<ISmsSender, SdpSmsSender>();

// LDAP provera tipa broja (U MTS / VAN MTS) preko SelfCareLdapWS SOAP-a (Faza 5, §10.1).
// Zamena za WCF LDAPCheckSoapClient — ručni SOAP 1.1 envelope + "Integration-Auth" header.
// Za dev/test bez pristupa iib.telekom.rs vrati se na LdapCheckerStub (vraća "VAN MTS").
builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection("Ldap"));
builder.Services.AddHttpClient<ILdapChecker, SelfCareLdapChecker>();
builder.Services.AddSingleton<ReceiptReq>();

// Oracle/TIS CRM klijent (Faza 5 §10.2) — autofill podataka klijenta po broju ugovora (GetClientData).
// Mora biti registrovan: AdminManageApiController ga traži u konstruktoru (bez njega pada ceo api/AdminManage).
// Bez TIS konfiguracije klijent samo vraća null, pa je bezbedan i u dev-u.
//builder.Services.AddScoped<ITisClient, TisClient>();

// Hangfire (zakazano kampanjsko slanje, §9.2). Posao slanja (SmsDispatchJob) registruje se uvek
// jer ga i trenutno (sinhrono) slanje poziva direktno; Hangfire storage/server se ožičava SAMO ako
// je HangfireDB stvarno konfigurisan (bez __placeholder__), da bi app mogla da se digne u dev-u bez te baze.
builder.Services.AddScoped<SmsDispatchJob>();

var hangfireConn = builder.Configuration.GetConnectionString("HangfireDB");
var hangfireEnabled = !string.IsNullOrWhiteSpace(hangfireConn) && !hangfireConn.Contains("__");
if (hangfireEnabled)
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireConn));
    builder.Services.AddHangfireServer();
}

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    o.SlidingExpiration = true;
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

// Add services to the container.
// PropertyNamingPolicy = null zadržava PascalCase u JSON-u (System.Text.Json podrazumevano camelCase-uje).
// Legacy DataTables JS koristi PascalCase ključeve (Number/Name/NumberType/EditLink) -> mora ostati PascalCase.
builder.Services.AddControllersWithViews(o =>
        // Globalni audit log svake akcije (§7.4) — pokriva i ne-BaseController i API kontrolere.
        o.Filters.Add<BizSMS.Web.Filters.ActionLoggingFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

// Logger facade (§9.1) čita ime ulogovanog korisnika per-request iz HttpContext-a (@user u SP-u).
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Poveži Logger facade sa DI (Faza 5 §9.1): konzolni ILogger + upis u SP dbo.Log_Insert.
// Conn. string "BizSMS"; ako je prazan ili sadrži __placeholder__ → SP upis se preskače (dev bez baze).
Logger.Configure(
    app.Services.GetRequiredService<ILoggerFactory>(),
    builder.Configuration.GetConnectionString("BizSMS") ?? "",
    app.Services.GetRequiredService<IHttpContextAccessor>());

// Kultura aplikacije: srpski, ali sa decimalnim separatorom "." (isto kao legacy sr-YU override).
// Bez fiksirane kulture model binding koristi kulturu OS-a (zarez = decimala, tačka = hiljade),
// pa unos "0.1" parsira kao "01" = 1. Grupni separator postavljamo na "" da ne bude isto što i decimalni.
var defaultCulture = new System.Globalization.CultureInfo("sr-Latn-RS");
defaultCulture.NumberFormat.NumberDecimalSeparator = ".";
defaultCulture.NumberFormat.NumberGroupSeparator = "";
var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCulture),
    SupportedCultures = new[] { defaultCulture },
    SupportedUICultures = new[] { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Zamena za legacy Global.asax Application_Error -> ErrorController.General
    app.UseExceptionHandler("/Error/General");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Mapiranje statusnih kodova (400/401/403/404) na ErrorController.Http4xx (legacy Application_Error mapiranje)
app.UseStatusCodePagesWithReExecute("/Error/Http{0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard (/hangfire) — posle autentifikacije/autorizacije da bi filter video ulogovanog
// korisnika. Dostupan samo Administrator-u (HangfireDashboardAuthFilter). Vidljiv samo kad je Hangfire ožičen.
if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthFilter() }
    });
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
