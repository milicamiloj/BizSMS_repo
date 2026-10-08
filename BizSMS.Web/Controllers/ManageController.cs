using System.Threading.Tasks;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.ManageController.
    // Portovan je samo tok promene lozinke (jedina Manage akcija koju UI zove — _LoginPartial "Promeni lozinku").
    // Ostatak legacy Manage-a (eksterni logini, AddPhoneNumber, Enable/Disable2FA, ManageLogins, SetPassword,
    // VerifyCode) je OWIN eksterna-autentifikacija koju Core deo ne koristi (nema eksternih provajdera)
    // i nije linkovan nigde u portovanom UI-ju -> izostavljeno.
    // AuthorizeUser (ne obican Authorize) radi pariteta sa legacy-jem: pored autentifikacije
    // blokira i otkazane (IsCanceled) korisnike, kao i svi ostali korisnicki endpointi.
    [AuthorizeUser]
    public class ManageController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly Logger _logger = new Logger();

        public ManageController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: /Manage/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            _logger.SetControllerAction("Manage", "ChangePassword");
            _logger.Info("Promena lozinke za korisnika: " + user.Id);

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

            if (result.Succeeded)
            {
                _logger.Info("Lozinka uspešno promenjena");
                await _signInManager.RefreshSignInAsync(user);
                return RedirectToAction("Index", "Home", new { message = ManageMessageId.ChangePasswordSuccess });
            }

            _logger.Warn("Promena lozinke nije uspela: " + result.ToString());
            foreach (var error in result.Errors)
            {
                // Identity vraća "PasswordMismatch" kad je stara lozinka pogrešna -> srpska poruka.
                ModelState.AddModelError(string.Empty,
                    error.Code == "PasswordMismatch" ? "Netačni podaci." : error.Description);
            }
            return View(model);
        }
    }
}
