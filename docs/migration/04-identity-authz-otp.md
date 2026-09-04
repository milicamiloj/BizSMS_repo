## Svrha
Migracija sa legacy auth na ASP.NET Core Identity + 2FA/OTP i dodatna OTP potvrda pre slanja/zakazivanja SMS.

## Koraci migracije
1. Uvesti `ApplicationUser : IdentityUser` sa postojećim poljima (`Client_ID`, `Is_Canceled`, `Is_Deleted`, `PhoneCodeSentAt`).
2. Konfigurisati Identity lockout, password policy, roles: `Administrator`, `BusinessUser`.
3. Migrirati login + 2FA tok iz `AccountController`.
4. Dodati “send confirmation OTP” tok koji je odvojen od login 2FA.
5. Uvesti autorizacione politike (`AdministratorOnly`, `BusinessUserOnly`).

## ASCII dijagram
```text
Username/Password
   -> Login OK
   -> 2FA OTP (login)
   -> Session Authenticated
   -> User klikne Send/Schedule
   -> OTP Confirm Action (kratkotrajan token)
   -> Dozvoli slanje/zakazivanje
```

## Before/After primer
### Before (legacy `AccountController.cs`)
```csharp
result = await SignInManager.PasswordSignInAsync(user.UserName, model.Password, false, shouldLockout: true);

if (result == SignInStatus.RequiresVerification)
{
    code = await UserManager.GenerateTwoFactorTokenAsync(userId, "Phone Code");
    user.PhoneCodeSentAt = DateTime.Now;
    await UserManager.UpdateAsync(user);
}
```

### After (.NET 10 Identity)
```csharp
var signIn = await _signInManager.PasswordSignInAsync(
    model.Username, model.Password, isPersistent: false, lockoutOnFailure: true);

if (signIn.RequiresTwoFactor)
{
    var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultPhoneProvider);
    user.PhoneCodeSentAt = DateTime.UtcNow;
    await _userManager.UpdateAsync(user);
    await _smsSender.SendAsync(user.PhoneNumber!, $"BizSMS OTP: {code}");
    return RedirectToAction(nameof(Verify2Fa));
}
```

## Code snippets
### Identity konfiguracija
```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    opt.SignIn.RequireConfirmedPhoneNumber = true;
    opt.Tokens.ChangePhoneNumberTokenProvider = TokenOptions.DefaultPhoneProvider;
    opt.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
})
.AddEntityFrameworkStores<BizSmsDbContext>()
.AddDefaultTokenProviders()
.AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>(TokenOptions.DefaultPhoneProvider)
.AddTokenProvider<PhoneNumberTokenProvider<ApplicationUser>>("SendActionOtp");
```

### Role seed
```csharp
public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    foreach (var role in new[] { "Administrator", "BusinessUser" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}
```

### Tenant-like mapiranje bez promene šeme
```csharp
public sealed class ApplicationUser : IdentityUser
{
    [Column("Client_ID")]
    public int ClientID { get; set; }

    [Column("Is_Canceled")]
    public bool IsCanceled { get; set; }
}
```

### OTP confirmation pre slanja/zakazivanja
```csharp
public async Task<bool> ConfirmSendOtpAsync(ApplicationUser user, string otpCode)
{
    var ok = await _userManager.VerifyUserTokenAsync(user, "SendActionOtp", "send-confirm", otpCode);

    if (!ok) return false;

    // 2 minuta važenja potvrde akcije
    _cache.Set($"send-otp:{user.Id}", true, TimeSpan.FromMinutes(2));
    return true;
}

public void EnsureSendOtpConfirmed(string userId)
{
    if (!_cache.TryGetValue($"send-otp:{userId}", out bool ok) || !ok)
        throw new UnauthorizedAccessException("OTP potvrda je obavezna pre slanja/zakazivanja.");
}

public async Task SendActionOtpAsync(ApplicationUser user)
{
    var code = await _userManager.GenerateUserTokenAsync(user, "SendActionOtp", "send-confirm");
    await _smsSender.SendAsync(user.PhoneNumber!, $"BizSMS potvrda slanja: {code}");
}
```

## Checklist za code review
- [ ] Role su `Administrator` i `BusinessUser`.
- [ ] Lockout/reset password tokovi su migrirani.
- [ ] 2FA login radi preko SMS OTP.
- [ ] Dodatni OTP gate postoji pre send/schedule akcija.
- [ ] `Client_ID` mapiranje korisnika ostaje bez izmene baze.

## Najčešće greške i kako ih izbeći
- Mešanje login OTP i action OTP u isti token/context.
- Predugo važenje OTP potvrde za slanje (držati kratko, npr. 2 min).
- Neproveravanje `IsCanceled/IsDeleted` pri sign-in.
