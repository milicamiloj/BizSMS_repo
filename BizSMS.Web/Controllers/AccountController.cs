using BizSMS.Web.Helpers;
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

        // Podrazumevani telefonski token provajder koji registruje AddDefaultTokenProviders() ("Phone").
        private static readonly string PhoneProvider = TokenOptions.DefaultPhoneProvider;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ISmsSender smsSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _smsSender = smsSender;
        }

        // Bezbednosni događaji prijave idu u BST_LOG (A09). Korisnik se navodi eksplicitno
        // jer HttpContext.User pre završene prijave nije autentifikovan.
        private static Logger AuthLog(string? userName)
        {
            var log = new Logger();
            log.SetUser(userName);
            return log;
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

            // Svaki neuspeh (nepoznato ime, pogrešna lozinka, zaključan nalog) daje isti odgovor u približno
            // istom vremenu, da se iz prijave ne može saznati koja korisnička imena postoje. Pravi razlog ide u log.
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                SimulatePasswordCheck(model.Password);
                AuthLog(model.Username).Warn("Neuspela prijava: nepoznato korisničko ime");
                return LoginFailed(model);
            }

            var log = AuthLog(user.UserName);

            // Provera lozinke bez prijave; broji neuspele pokušaje i zaključava nalog.
            var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!check.Succeeded)
            {
                if (check.IsLockedOut || check.IsNotAllowed)
                    SimulatePasswordCheck(model.Password);  // ove grane ne proveravaju lozinku

                if (check.IsLockedOut)
                    log.Warn("Neuspela prijava: nalog je zaključan (lockout)");
                else if (check.IsNotAllowed)
                    log.Warn("Neuspela prijava: prijava nije dozvoljena za nalog");
                else
                    log.Warn("Neuspela prijava: pogrešna lozinka (neuspelih pokušaja: "
                        + await _userManager.GetAccessFailedCountAsync(user) + ")");
                return LoginFailed(model);
            }

            // Otkazan nalog otkrivamo tek onome ko zna lozinku.
            if (user.IsCanceled)
            {
                log.Warn("Neuspela prijava: nalog je otkazan");
                return View("Lockout");
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                log.Info("Uspešna prijava (bez 2FA)");
                return RedirectToLocal(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                try
                {
                    await SendPhoneCodeAsync(user);
                    log.Info("Lozinka ispravna, poslat 2FA kod");
                    return RedirectToAction(nameof(VerifyPhoneNumber), new { rememberMe = model.RememberMe, returnUrl });
                }
                catch (Exception ex)
                {
                    log.Error("Slanje 2FA koda nije uspelo: " + ex.Message);
                    ModelState.AddModelError(string.Empty, "Slanje verifikacionog koda nije uspelo. Pokušajte ponovo.");
                    return View(model);
                }
            }

            log.Warn("Neuspela prijava posle ispravne lozinke (lockout: " + result.IsLockedOut
                + ", nije dozvoljena: " + result.IsNotAllowed + ")");
            return LoginFailed(model);
        }

        private IActionResult LoginFailed(LoginViewModel model)
        {
            ModelState.AddModelError(string.Empty,
                "Neispravno korisničko ime ili lozinka."); //Posle 5 neuspelih pokušaja nalog se zaključava na 5 minuta.
            return View(model);
        }

        private static readonly Lazy<string> DummyPasswordHash =
            new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

        // Heširanje traje desetine ms; bez ovoga se nepoznato ime prepoznaje po bržem odgovoru.
        private void SimulatePasswordCheck(string? password)
            => _userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password ?? "");

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

            var log = AuthLog(user.UserName);

            // Aplikativni rok trajanja koda: 2 minuta (kao u legacy toku).
            if (user.PhoneCodeSentAt == null || user.PhoneCodeSentAt < DateTime.Now.AddMinutes(-2))
            {
                log.Warn("Neuspela 2FA verifikacija: kod je istekao");
                ModelState.AddModelError(string.Empty, "Kod je istekao. Zatražite novi.");
                return View(model);
            }

            var result = await _signInManager.TwoFactorSignInAsync(
                PhoneProvider, model.Code, isPersistent: model.RememberMe, rememberClient: false);

            if (result.Succeeded)
            {
                log.Info("Uspešna prijava (2FA)");
                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                log.Warn("Neuspela 2FA verifikacija: nalog je zaključan (lockout)");
                return View("Lockout");
            }

            log.Warn("Neuspela 2FA verifikacija: pogrešan kod (neuspelih pokušaja: "
                + await _userManager.GetAccessFailedCountAsync(user) + ")");
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

            var log = AuthLog(user.UserName);

            if (user.PhoneCodeSentAt != null && user.PhoneCodeSentAt > DateTime.Now.AddSeconds(-30))
            {
                log.Warn("Ponovno slanje 2FA koda odbijeno: zahtev pre isteka 30s");
                TempData["Message"] = "Sačekajte 30s pre ponovnog slanja.";
                return RedirectToAction(nameof(VerifyPhoneNumber), new { model.RememberMe, model.ReturnUrl });
            }

            try
            {
                await SendPhoneCodeAsync(user);
                log.Info("Ponovo poslat 2FA kod");
                TempData["Message"] = "Novi kod je poslat.";
            }
            catch (Exception ex)
            {
                log.Error("Ponovno slanje 2FA koda nije uspelo: " + ex.Message);
                TempData["Message"] = "Slanje koda nije uspelo. Pokušajte ponovo.";
            }

            return RedirectToAction(nameof(VerifyPhoneNumber), new { model.RememberMe, model.ReturnUrl });
        }

        // POST: /Account/Logoff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logoff()
        {
            new Logger().Info("Odjava korisnika");
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
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
