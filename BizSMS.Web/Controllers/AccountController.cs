using BizSMS.Web.Models;
using BizSMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISmsSender _smsSender;
        private readonly ILogger<AccountController> _logger;

        // Podrazumevani telefonski token provajder koji registruje AddDefaultTokenProviders() ("Phone").
        private static readonly string PhoneProvider = TokenOptions.DefaultPhoneProvider;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ISmsSender smsSender,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _smsSender = smsSender;
            _logger = logger;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Neispravno korisničko ime ili lozinka.");
                return View(model);
            }

            if (user.IsCanceled)
            {
                _logger.LogWarning("Nalog je otkazan: {User}", user.UserName);
                return View("Lockout");
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            _logger.LogInformation("Login rezultat za {User}: {Result}", user.UserName, result);

            if (result.Succeeded)
                return RedirectToLocal(returnUrl);

            if (result.RequiresTwoFactor)
            {
                try
                {
                    await SendPhoneCodeAsync(user);
                    return RedirectToAction(nameof(VerifyPhoneNumber), new { rememberMe = model.RememberMe, returnUrl });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Greška pri slanju verifikacionog koda za {User}", user.UserName);
                    ModelState.AddModelError(string.Empty, "Slanje verifikacionog koda nije uspelo. Pokušajte ponovo.");
                    return View(model);
                }
            }

            if (result.IsLockedOut)
                return View("Lockout");

            ModelState.AddModelError(string.Empty, "Neispravno korisničko ime ili lozinka.");
            return View(model);
        }

        // GET: /Account/VerifyPhoneNumber — unos SMS koda
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyPhoneNumber(bool rememberMe = false, string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
                return RedirectToAction(nameof(Login));

            return View(new VerifyPhoneNumberViewModel
            {
                RememberMe = rememberMe,
                ReturnUrl = returnUrl,
                PhoneCodeSentAt = user.PhoneCodeSentAt
            });
        }

        // POST: /Account/VerifyPhoneNumber — verifikacija SMS koda
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
                return RedirectToAction(nameof(Login));

            model.PhoneCodeSentAt = user.PhoneCodeSentAt;

            if (!ModelState.IsValid)
                return View(model);

            // Aplikativni rok trajanja koda: 2 minuta (kao u legacy toku).
            if (user.PhoneCodeSentAt == null || user.PhoneCodeSentAt < DateTime.Now.AddMinutes(-2))
            {
                ModelState.AddModelError(string.Empty, "Kod je istekao. Zatražite novi.");
                return View(model);
            }

            var result = await _signInManager.TwoFactorSignInAsync(
                PhoneProvider, model.Code, isPersistent: model.RememberMe, rememberClient: false);

            if (result.Succeeded)
                return RedirectToLocal(model.ReturnUrl);

            if (result.IsLockedOut)
                return View("Lockout");

            ModelState.AddModelError(string.Empty, "Uneti kod nije validan.");
            return View(model);
        }

        // POST: /Account/ResendCode — ponovno slanje SMS koda (rate-limit 30s)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(VerifyPhoneNumberViewModel model)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (user.PhoneCodeSentAt != null && user.PhoneCodeSentAt > DateTime.Now.AddSeconds(-30))
            {
                TempData["Message"] = "Sačekajte 30s pre ponovnog slanja.";
                return RedirectToAction(nameof(VerifyPhoneNumber), new { model.RememberMe, model.ReturnUrl });
            }

            try
            {
                await SendPhoneCodeAsync(user);
                TempData["Message"] = "Novi kod je poslat.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri ponovnom slanju verifikacionog koda za {User}", user.UserName);
                TempData["Message"] = "Slanje koda nije uspelo. Pokušajte ponovo.";
            }

            return RedirectToAction(nameof(VerifyPhoneNumber), new { model.RememberMe, model.ReturnUrl });
        }

        // POST: /Account/Logoff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logoff()
        {
            _logger.LogInformation("Odjava korisnika: {User}", User.Identity?.Name);
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        // Generise 2FA kod, belezi vreme slanja i salje SMS preko SDP-a.
        private async Task SendPhoneCodeAsync(ApplicationUser user)
        {
            var code = await _userManager.GenerateTwoFactorTokenAsync(user, PhoneProvider);
            user.PhoneCodeSentAt = DateTime.Now;
            await _userManager.UpdateAsync(user);
            await _smsSender.SendAsync(user.PhoneNumber!, "Vas aktivacioni BizSMS kod je: " + code);
            _logger.LogInformation("Verifikacioni kod poslat korisniku {UserId}", user.Id);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
