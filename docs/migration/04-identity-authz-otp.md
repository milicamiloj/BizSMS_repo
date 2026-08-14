# 04 — ASP.NET Core Identity, role, 2FA/OTP + potvrda pre slanja/zakazivanja

## Svrha

Prevesti legacy autentifikaciju (OWIN + `Microsoft.AspNet.Identity`) na **ASP.NET Core Identity**,
uz sve regulatorno-bezbednosne zahteve:

- role `Administrator` i `BusinessUser` (max 5 naloga po klijentu),
- login username/password + **2FA/OTP** (SMS token provider),
- **OTP potvrda pre slanja/zakazivanja SMS-a** (drugi „gate“ pored samog login-a),
- lockout, reset password, „force change password on first login“,
- mapiranje korisnika na klijenta bez menjanja šeme (`ClientId` već postoji na
  `ApplicationUser` — zadržavamo ga).

## Konceptualni tok (ASCII dijagram)

```
        +---------------+          +-----------------------+
        | Login page    |          | AccountController      |
        |  (POST creds) +---------->  Login(model)          |
        +---------------+          |                        |
                                   |  SignInManager         |
                                   |    .PasswordSignInAsync|
                                   |    (lockout on fail)   |
                                   +---+--------------------+
                                       |
                        succeeds needsTwoFactor
                                       |
                                       v
                              +-----------------+
                              | LoginWith2fa    |
                              | (SMS token)     |
                              +--------+--------+
                                       |
                                       v
                              user cookie + ClaimsPrincipal
                                       |
                                       v
                     ---------- ready to use app ----------

                          When user tries to SEND SMS:
                                       |
                                       v
                        +-------------------------------+
                        |  [RequireOtpConfirmed]        |
                        |  filter attribute             |
                        +------------+------------------+
                                     |
                        session has valid OTP ticket?
                                     |
                            yes --------- no
                             |            |
                             v            v
                     proceed to     redirect to
                     ISmsService    /Account/OtpChallenge?returnUrl=...
                                     |
                                     v
                          user gets fresh 6-digit code
                                     |
                                     v
                    submit code -> validate -> stamp ticket in Session
                                     |
                                     v
                             proceed to ISmsService
```

## Zahtevi po komponentama

- **Password hashing**: default PBKDF2 v3, ne diramo (ali stari OWIN hash-evi nisu kompatibilni →
  „reset password on first login“ obrazac, v. sekciju „Migracija postojećih naloga“).
- **Cookie**: `SameSite=Strict`, `HttpOnly`, `Secure`, expiration 30 min sliding.
- **Lockout**: 5 pokušaja, zaključavanje 15 min.
- **Roles**: seed na startupu iz `IdentityRoleSeeder`.
- **OTP**: 6-cifreni SMS kod, TTL 60s, max 5 pokušaja, tag u sesiji „OtpConfirmedUntil=<utc>“
  važi 5 minuta (konfigurabilno).
- **Cap per client**: business rule „max 5 BusinessUser po klijentu“ — validira se u
  `IUserProvisioningService`, ne u kontroleru.

## Koraci migracije

1. **Odbaci OWIN** — ukloni `Microsoft.AspNet.Identity.*`, `Microsoft.Owin.*`, `Startup.Auth.cs`,
   `App_Start/IdentityConfig.cs`.
2. **Dodaj pakete** u `BizSMS.Infrastructure` i `BizSMS.Web`:
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
   - `Microsoft.AspNetCore.Authentication.Cookies`
3. **Definiši `ApplicationUser`** u `BizSMS.Infrastructure/Identity/ApplicationUser.cs` sa istim
   dodatnim poljima (`IsCanceled`, `IsDeleted`, `ClientID`, `PhoneCodeSentAt`).
4. **Registruj Identity** kroz DI extension `AddIdentityWithOtp`.
5. **Custom `SmsTokenProvider`** (koristi `ISmsGateway`, ne SmsService iz starog Identity-ja).
6. **Napravi `IOtpConfirmationService`** za „OTP pre slanja SMS-a“.
7. **Napravi `RequireOtpConfirmedAttribute`** filter i primeni ga na `SendSms`/`ScheduleSms`
   akcije.
8. **Seed** rola i inicijalnog Administrator naloga (samo prvi put).
9. **Force password reset** za sve legacy korisnike (v. sekciju „Migracija postojećih naloga“).

## ApplicationUser

`src/BizSMS.Infrastructure/Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsCanceled { get; set; }
    public bool IsDeleted { get; set; }
    public int ClientID { get; set; }                  // FK ka BST_CLIENTS.Client_ID
    public DateTime? PhoneCodeSentAt { get; set; }
    public bool MustChangePassword { get; set; }        // koristi se na prvom loginu
    public DateTime? LastLoginUtc { get; set; }
}
```

`ApplicationUserConfiguration.cs` (Fluent API):

```csharp
using BizSMS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizSMS.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.IsCanceled).HasColumnName("Is_Canceled");
        b.Property(x => x.IsDeleted).HasColumnName("Is_Deleted");
        b.Property(x => x.ClientID).HasColumnName("Client_ID");
        b.Property(x => x.PhoneCodeSentAt).HasColumnName("PhoneCodeSentAt");
        b.Property(x => x.MustChangePassword).HasColumnName("MustChangePassword")
            .HasDefaultValue(true);
        b.Property(x => x.LastLoginUtc).HasColumnName("LastLoginUtc");

        b.HasQueryFilter(u => !u.IsDeleted); // svuda automatski krije obrisane
    }
}
```

## DI: AddIdentityWithOtp

`src/BizSMS.Infrastructure/Identity/IdentityConfiguration.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Infrastructure.Identity;
using BizSMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BizSMS.Infrastructure.Identity;

public static class IdentityConfiguration
{
    public const string SmsProviderName = "Phone";

    public static IServiceCollection AddIdentityWithOtp(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            // Password
            o.Password.RequiredLength = 10;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = true;
            o.Password.RequiredUniqueChars = 4;

            // Lockout
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.AllowedForNewUsers = true;

            // User
            o.User.RequireUniqueEmail = false;      // ostajemo na username-based prijavi
            o.SignIn.RequireConfirmedAccount = false;

            // 2FA
            o.Tokens.ChangePhoneNumberTokenProvider = TokenOptions.DefaultPhoneProvider;
            o.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders()
        // SMS OTP provider (2FA + „OTP pre slanja“)
        .AddTokenProvider<SmsAuthenticatorTokenProvider>(SmsProviderName);

        services.Configure<DataProtectionTokenProviderOptions>(o =>
        {
            o.TokenLifespan = TimeSpan.FromHours(1);
        });

        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/Account/Login";
            o.LogoutPath = "/Account/Logout";
            o.AccessDeniedPath = "/Error/Http403";
            o.SlidingExpiration = true;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            o.Cookie.Name = ".BizSMS.Auth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });

        // Servisi
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IOtpConfirmationService, OtpConfirmationService>();
        services.AddScoped<ITenantContext, TenantContext>();

        // Policy za OTP i role
        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthPolicies.OtpConfirmed, p =>
                p.RequireAuthenticatedUser()
                 .AddRequirements(new OtpConfirmedRequirement()));
            o.AddPolicy(AuthPolicies.Admin, p =>
                p.RequireRole(Roles.Administrator));
            o.AddPolicy(AuthPolicies.Business, p =>
                p.RequireRole(Roles.BusinessUser));
        });

        services.AddScoped<IAuthorizationHandler, OtpConfirmedHandler>();

        return services;
    }
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string BusinessUser  = "BusinessUser";
}

public static class AuthPolicies
{
    public const string OtpConfirmed = nameof(OtpConfirmed);
    public const string Admin        = nameof(Admin);
    public const string Business     = nameof(Business);
}
```

## SmsAuthenticatorTokenProvider (SMS OTP)

Legacy je koristio `PhoneNumberTokenProvider<ApplicationUser>` iz `Microsoft.AspNet.Identity`.
U .NET 10 pišemo minimalni provider koji generiše 6-cifreni kod i šalje ga kroz `ISmsGateway`.

`src/BizSMS.Infrastructure/Identity/SmsAuthenticatorTokenProvider.cs`:

```csharp
using BizSMS.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Infrastructure.Identity;

public sealed class SmsAuthenticatorTokenProvider : PhoneNumberTokenProvider<ApplicationUser>
{
    public override async Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var token = await base.GenerateAsync(purpose, manager, user);
        // Cache "PhoneCodeSentAt" da bismo znali koliko je star
        user.PhoneCodeSentAt = DateTime.UtcNow;
        await manager.UpdateAsync(user);
        return token;
    }
}
```

Odvojen servis koji **stvarno šalje SMS** za 2FA (koristi ga `AccountController`):

```csharp
public interface IOtpDispatcher
{
    Task DispatchLoginOtpAsync(ApplicationUser user, CancellationToken ct);
    Task DispatchActionOtpAsync(ApplicationUser user, CancellationToken ct);
}

internal sealed class OtpDispatcher : IOtpDispatcher
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ISmsGateway _sms;
    private readonly ILogger<OtpDispatcher> _log;

    public OtpDispatcher(UserManager<ApplicationUser> users, ISmsGateway sms, ILogger<OtpDispatcher> log)
        => (_users, _sms, _log) = (users, sms, log);

    public async Task DispatchLoginOtpAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await _users.GenerateTwoFactorTokenAsync(user, IdentityConfiguration.SmsProviderName);
        await _sms.SendAsync(user.PhoneNumber!, $"BizSMS prijava, kod: {token}", ct);
        _log.LogInformation("Login OTP sent to user {UserId}", user.Id);
    }

    public async Task DispatchActionOtpAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await _users.GenerateUserTokenAsync(user, IdentityConfiguration.SmsProviderName, "sms-action");
        await _sms.SendAsync(user.PhoneNumber!, $"BizSMS potvrda slanja, kod: {token}", ct);
        _log.LogInformation("Action OTP sent to user {UserId}", user.Id);
    }
}
```

## OtpConfirmationService (potvrda pre slanja)

Ovo je „session ticket“ koji označava da je korisnik u poslednjih X minuta uspešno potvrdio
OTP i sme da šalje/zakazuje SMS.

`src/BizSMS.Application/Otp/IOtpConfirmationService.cs`:

```csharp
namespace BizSMS.Application.Otp;

public interface IOtpConfirmationService
{
    Task RequestChallengeAsync(string userId, CancellationToken ct);
    Task<bool> ConfirmAsync(string userId, string code, CancellationToken ct);
    bool IsConfirmed(string userId);
    TimeSpan ConfirmationWindow { get; }
}
```

`src/BizSMS.Infrastructure/Identity/OtpConfirmationService.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Application.Otp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Infrastructure.Identity;

internal sealed class OtpConfirmationService : IOtpConfirmationService
{
    private const string SessionKey = "OtpConfirmedUntil";
    public TimeSpan ConfirmationWindow { get; } = TimeSpan.FromMinutes(5);

    private readonly UserManager<ApplicationUser> _users;
    private readonly IOtpDispatcher _dispatcher;
    private readonly IHttpContextAccessor _http;

    public OtpConfirmationService(UserManager<ApplicationUser> users, IOtpDispatcher dispatcher, IHttpContextAccessor http)
        => (_users, _dispatcher, _http) = (users, dispatcher, http);

    public async Task RequestChallengeAsync(string userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        await _dispatcher.DispatchActionOtpAsync(user, ct);
    }

    public async Task<bool> ConfirmAsync(string userId, string code, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found");
        var ok = await _users.VerifyUserTokenAsync(user, IdentityConfiguration.SmsProviderName, "sms-action", code);
        if (!ok) return false;

        var expiry = DateTime.UtcNow.Add(ConfirmationWindow);
        _http.HttpContext!.Session.SetString(SessionKey, expiry.ToString("O"));
        return true;
    }

    public bool IsConfirmed(string userId)
    {
        var raw = _http.HttpContext?.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(raw)) return false;
        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
               && dt > DateTime.UtcNow;
    }
}
```

## Policy + handler: OtpConfirmed

`src/BizSMS.Infrastructure/Identity/OtpConfirmedRequirement.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace BizSMS.Infrastructure.Identity;

public sealed class OtpConfirmedRequirement : IAuthorizationRequirement { }
```

`src/BizSMS.Infrastructure/Identity/OtpConfirmedHandler.cs`:

```csharp
using BizSMS.Application.Otp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Infrastructure.Identity;

public sealed class OtpConfirmedHandler : AuthorizationHandler<OtpConfirmedRequirement>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IOtpConfirmationService _otp;

    public OtpConfirmedHandler(UserManager<ApplicationUser> users, IOtpConfirmationService otp)
        => (_users, _otp) = (users, otp);

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx, OtpConfirmedRequirement req)
    {
        var userId = _users.GetUserId(ctx.User);
        if (userId is not null && _otp.IsConfirmed(userId))
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}
```

## RequireOtpConfirmed atribut (za lakše korišćenje na akcijama)

`src/BizSMS.Web/Filters/RequireOtpConfirmedAttribute.cs`:

```csharp
using BizSMS.Application.Otp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireOtpConfirmedAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var otp = ctx.HttpContext.RequestServices.GetRequiredService<IOtpConfirmationService>();
        var userId = ctx.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId is null || !otp.IsConfirmed(userId))
        {
            var returnUrl = ctx.HttpContext.Request.Path + ctx.HttpContext.Request.QueryString;
            ctx.Result = new RedirectToActionResult("OtpChallenge", "Account",
                new { returnUrl = returnUrl.ToString() });
        }
        return Task.CompletedTask;
    }
}
```

Upotreba na kontroleru:

```csharp
[Authorize(Roles = Roles.BusinessUser + "," + Roles.Administrator)]
public sealed class SendSmsController : Controller
{
    [HttpPost]
    [RequireOtpConfirmed]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendSmsViewModel model, CancellationToken ct) { ... }

    [HttpPost]
    [RequireOtpConfirmed]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(ScheduleSmsViewModel model, CancellationToken ct) { ... }
}
```

## AccountController — login sa 2FA + OTP challenge

Skraćeno; potpuna logika u `BizSMS.Application/Otp`:

```csharp
[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IOtpDispatcher _otpDispatcher;
    private readonly IOtpConfirmationService _otp;
    private readonly IAuditService _audit;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IOtpDispatcher otpDispatcher, IOtpConfirmationService otp, IAuditService audit)
        => (_users, _signIn, _otpDispatcher, _otp, _audit)
           = (users, signIn, otpDispatcher, otp, audit);

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByNameAsync(model.Username);
        if (user is null || user.IsCanceled || user.IsDeleted)
        {
            await _audit.LogAsync("LoginFailed", "User not found or canceled", new { model.Username }, ct);
            ModelState.AddModelError("", "Netačni podaci za prijavu.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            await _otpDispatcher.DispatchLoginOtpAsync(user, ct);
            return RedirectToAction(nameof(LoginWith2fa), new { returnUrl });
        }
        if (result.IsLockedOut)
        {
            await _audit.LogAsync("LoginLockedOut", "Lockout", new { user.UserName }, ct);
            return View("Lockout");
        }
        if (!result.Succeeded)
        {
            await _audit.LogAsync("LoginFailed", "Bad credentials", new { user.UserName }, ct);
            ModelState.AddModelError("", "Netačni podaci za prijavu.");
            return View(model);
        }

        // Ako je legacy nalog, forsiraj promenu lozinke
        if (user.MustChangePassword)
            return RedirectToAction("ChangePassword", "Manage");

        await _audit.LogAsync("LoginSucceeded", "OK", new { user.UserName }, ct);
        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet]
    public IActionResult LoginWith2fa(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginWith2faViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _signIn.TwoFactorSignInAsync(
            IdentityConfiguration.SmsProviderName, model.TwoFactorCode, isPersistent: false, rememberClient: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Netačan OTP kod.");
            return View(model);
        }
        return LocalRedirect(returnUrl ?? "/");
    }

    // Action-level OTP challenge (pre slanja SMS-a)
    [HttpGet, Authorize]
    public async Task<IActionResult> OtpChallenge(string returnUrl, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User) ?? throw new InvalidOperationException("No user");
        await _otp.RequestChallengeAsync(user.Id, ct);
        return View(new OtpChallengeViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> OtpChallenge(OtpChallengeViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.GetUserAsync(User) ?? throw new InvalidOperationException("No user");
        var ok = await _otp.ConfirmAsync(user.Id, model.Code, ct);
        if (!ok)
        {
            ModelState.AddModelError("", "Netačan OTP kod.");
            return View(model);
        }
        await _audit.LogAsync("OtpConfirmed", "OK", new { user.UserName }, ct);
        return LocalRedirect(model.ReturnUrl);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _signIn.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
```

## Provisioning: „max 5 BusinessUser po klijentu“

Legacy je imao ovu proveru rasutu po `AdminManageController`. Prebaci je u
`UserProvisioningService`:

```csharp
public interface IUserProvisioningService
{
    Task<Result<ApplicationUser>> CreateBusinessUserAsync(CreateBusinessUserDto dto, CancellationToken ct);
    Task<Result> ForcePasswordResetAsync(string userId, CancellationToken ct);
    Task<Result> LockUserAsync(string userId, TimeSpan? until, CancellationToken ct);
    Task<Result> UnlockUserAsync(string userId, CancellationToken ct);
}

internal sealed class UserProvisioningService : IUserProvisioningService
{
    private const int MaxBusinessPerClient = 5;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public async Task<Result<ApplicationUser>> CreateBusinessUserAsync(CreateBusinessUserDto dto, CancellationToken ct)
    {
        var existing = await _db.Users.CountAsync(u =>
            u.ClientID == dto.ClientId && !u.IsDeleted, ct);
        if (existing >= MaxBusinessPerClient)
            return Result.Fail<ApplicationUser>($"Klijent može imati najviše {MaxBusinessPerClient} naloga.");

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            ClientID = dto.ClientId,
            TwoFactorEnabled = true,
            PhoneNumberConfirmed = true,
            MustChangePassword = true
        };

        var create = await _users.CreateAsync(user, dto.InitialPassword);
        if (!create.Succeeded) return Result.Fail<ApplicationUser>(string.Join("; ", create.Errors.Select(e => e.Description)));

        var role = await _users.AddToRoleAsync(user, Roles.BusinessUser);
        if (!role.Succeeded) return Result.Fail<ApplicationUser>(string.Join("; ", role.Errors.Select(e => e.Description)));

        await _audit.LogAsync("UserCreated", "OK", new { user.UserName, user.ClientID }, ct);
        return Result.Ok(user);
    }
    // ... ForcePasswordReset / Lock / Unlock analogno
}
```

## Migracija postojećih naloga (legacy hash → ASP.NET Core Identity)

- Legacy ASP.NET Identity koristio je PBKDF2 sa 1000 iteracija (V1) ili V2. .NET Core Identity
  koristi V3 (100.000 iteracija). Postoji custom `PasswordHasherCompatibilityMode = V2` opcija,
  ali **preporuka** je jednostavnija: **prisili sve postojeće korisnike da resetuju lozinku**.
- Postavi `MustChangePassword = true` za sve legacy korisnike jednom SQL komandom:
  ```sql
  UPDATE AspNetUsers SET MustChangePassword = 1, PasswordHash = NULL WHERE Is_Deleted = 0;
  ```
- U `AccountController.Login` proveri `MustChangePassword`. Ako je `true`, redirektuj na
  „Set new password“ tok koji koristi standardni Identity reset flow:
  ```csharp
  var token = await _users.GeneratePasswordResetTokenAsync(user);
  // pošalji SMS/email sa linkom /Account/SetInitialPassword?userId=…&token=…
  ```
- Na prvom login-u obavezno traži i **povezivanje telefona** (`VerifyPhoneNumberAsync`) da bi 2FA
  radio.

## Tenant kontekst (mapiranje korisnika na klijenta)

`src/BizSMS.Application/Abstractions/ITenantContext.cs`:

```csharp
namespace BizSMS.Application.Abstractions;

public interface ITenantContext
{
    string UserId { get; }
    int? ClientId { get; }
    bool IsAdministrator { get; }
    bool IsBusinessUser { get; }
}

internal sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;
    public TenantContext(IHttpContextAccessor http) => _http = http;

    public string UserId
        => _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public int? ClientId
    {
        get
        {
            var val = _http.HttpContext?.User.FindFirstValue("client_id");
            return int.TryParse(val, out var id) ? id : null;
        }
    }

    public bool IsAdministrator => _http.HttpContext?.User.IsInRole(Roles.Administrator) ?? false;
    public bool IsBusinessUser  => _http.HttpContext?.User.IsInRole(Roles.BusinessUser) ?? false;
}
```

Ubaci `client_id` claim u kuki tokom login-a preko `IUserClaimsPrincipalFactory`:

```csharp
public sealed class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm,
        IOptions<IdentityOptions> opts) : base(um, rm, opts) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("client_id", user.ClientID.ToString()));
        return identity;
    }
}
```

Registruj: `services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();`

Sve upite u `Application` sloju filtriraj kroz `ITenantContext.ClientId`.

## Before / After — AuthorizeUserAttribute

Legacy (`Attributes/AuthorizeAttributes.cs`):

```csharp
public class AuthorizeUserAttribute : AuthorizeAttribute
{
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
        var userId = httpContext.User.Identity.GetUserId();
        var user = HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>().FindById(userId);
        if (user == null || user.IsCanceled) return false;
        return base.AuthorizeCore(httpContext);
    }
}
```

.NET 10 (samo koristi standardne `[Authorize]` + Identity):

```csharp
// EF Core query filter već krije IsDeleted; IsCanceled proveravamo u policy handler-u
public sealed class ActiveUserRequirement : IAuthorizationRequirement { }

public sealed class ActiveUserHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly UserManager<ApplicationUser> _users;
    public ActiveUserHandler(UserManager<ApplicationUser> users) => _users = users;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext ctx, ActiveUserRequirement req)
    {
        var user = await _users.GetUserAsync(ctx.User);
        if (user is null) return;
        if (user.IsCanceled || user.IsDeleted) return;
        ctx.Succeed(req);
    }
}
```

Registruj kao policy „ActiveUser“ i dodaj u fallback:

```csharp
services.AddAuthorization(o =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new ActiveUserRequirement())
        .Build();
    o.DefaultPolicy = policy;
    o.FallbackPolicy = policy;
});
```

## Checklist za code review

- [ ] Nema referenci na `Microsoft.AspNet.Identity` (samo `Microsoft.AspNetCore.Identity`).
- [ ] Nema `OwinContext.GetUserManager` — svuda kroz DI.
- [ ] Sve akcije koje pošalju/zakazuju SMS imaju `[RequireOtpConfirmed]` ili
      `[Authorize(Policy = AuthPolicies.OtpConfirmed)]`.
- [ ] `TwoFactorEnabled` je `true` za svakog novokreiranog korisnika.
- [ ] `MustChangePassword` je `true` za sve migrirane korisnike i redirektuje na promenu.
- [ ] Password policy: min 10, mešane kategorije.
- [ ] Lockout parametri konfigurisani u DI, ne po kontroleru.
- [ ] `client_id` claim postoji na svakom login-ovanom korisniku i koristi se za filtriranje.
- [ ] Audit log-uje: login (uspeh/neuspeh), lockout, reset password, OTP challenge, OTP confirm.

## Najčešće greške i kako ih izbeći

1. **Zaboraviti `RequireAuthenticatedUser()` u policy-ju „OtpConfirmed“** — bez toga policy može
   biti evaluirana pre auth-a i propustiti neautentifikovanog usera.
2. **Session-based OTP ticket na više instanci bez sticky session-a / distributed cache-a** —
   koristi `AddStackExchangeRedisCache()` ili SQL Server session provider u produkciji.
3. **Slanje OTP-a u sinhronom stringu bez lokala korisnika** — koristi lokalizovani template.
4. **Slanje istog OTP koda kroz više kanala** — nemoj isto značenje koristiti za login i za
   „action confirm“. Zato koristimo drugi „purpose“ (`sms-action`) u `GenerateUserTokenAsync`.
5. **Regeneracija koda pri svakom refresh-u OtpChallenge stranice** — throttluj (`PhoneCodeSentAt`
   provera: ako je poslednji poslat pre <30s, ne šalji novi).
6. **`RequireOtpConfirmed` bez fallback-a na return URL** — korisnik završava na home stranici
   umesto na akciji koja je tražila potvrdu. Uvek prosleđuj `returnUrl`.
7. **Ostavljati legacy `IsCanceled` polje neaktivnim** — sada je pokriveno kroz Identity claim/handler,
   ali stara logika mora biti uklonjena iz kontrolera.
8. **Nedozvoljavanje adminu da resetuje 2FA** — dodaj admin akciju
   `POST /Admin/Users/{id}/ResetTwoFactor` koja poziva `_users.ResetAuthenticatorKeyAsync` i
   invalidira token providera.
