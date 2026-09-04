using BizSMS.Poc.Net10.Data;
using BizSMS.Poc.Net10.Middleware;
using BizSMS.Poc.Net10.Models;
using BizSMS.Poc.Net10.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<BizSmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BIZSMS")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Lockout.MaxFailedAccessAttempts = 5;
        opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        opt.SignIn.RequireConfirmedPhoneNumber = true;
        opt.Tokens.ChangePhoneNumberTokenProvider = TokenOptions.DefaultPhoneProvider;
        opt.Tokens.ProviderMap["SendActionOtp"] =
            new TokenProviderDescriptor(typeof(PhoneNumberTokenProvider<ApplicationUser>));
    })
    .AddEntityFrameworkStores<BizSmsDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>("SendActionOtp");

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Home/Privacy";
    opt.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    opt.SlidingExpiration = true;
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPocDbVerificationService, PocDbVerificationService>();
builder.Services.AddScoped<ISmsWorkflowService, SmsWorkflowService>();
builder.Services.AddScoped<IActionOtpService, ActionOtpService>();
builder.Services.AddScoped<IOtpSender, LoggingOtpSender>();
builder.Services.AddHostedService<ScheduledSmsWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestAuditMiddleware>();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
    try
    {
        await IdentitySeeder.SeedRolesAsync(roleManager);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Role seeding skipped. DB may be unavailable in local PoC environment.");
    }
}

app.Run();
