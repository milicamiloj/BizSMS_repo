using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using OfficeOpenXml.FormulaParsing.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Twilio.TwiML.Messaging;
using Twilio.TwiML.Voice;
using Twilio.Types;

namespace BizSMS.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private ApplicationSignInManager _signInManager;
        ApplicationDbContext context;
        readonly Logger logger = new Logger();

        public AccountController()
        {
            context = new ApplicationDbContext();
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
                //public UserManager<IdentityUser> UserManager => HttpContext.GetOwinContext().Get<UserManager<IdentityUser>>();

            }
            private set
            {
                _signInManager = value;
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Login(string returnUrl)
        {
            // Ako je korisnik već ulogovan -> nema šta da radi ovde
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Username);
            //var user = await UserManager.FindByEmailAsync(model.Email);
            var result = SignInStatus.Failure;

            if (user != null)
            {
                logger.Info("Login as user: " + user.UserName);

                if (user.IsCanceled)
                {
                    logger.Warn("User is canceled");
                    result = SignInStatus.LockedOut;
                }
                else
                {
                    // This doesn't count login failures towards account lockout
                    // To enable password failures to trigger account lockout, change to shouldLockout: true
                    //result = await SignInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, shouldLockout: true);
                    result = await SignInManager.PasswordSignInAsync(user.UserName, model.Password, false, shouldLockout: true); // model.RememberMe, shouldLockout: true);

                    logger.Info("Login result: " + result.ToString());
                }
            }
            var url = Url.Action("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
            Console.WriteLine(url);

            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    {
                        var userId = user.Id;
                        var phoneNumber = user.PhoneNumber;
                        var provider = "Phone Code";

                        try
                        {
                            // 1. Generiši OTP

                            //var code = await UserManager.GenerateTwoFactorTokenAsync(userId, provider);
                            string code;

                            if (user.UserName == "marijamark")
                                code = "123456";
                            else
                                code = await UserManager.GenerateTwoFactorTokenAsync(userId, provider);

                            // 2. Snimi vreme slanja
                            user.PhoneCodeSentAt = DateTime.Now; //.UtcNow;
                            await UserManager.UpdateAsync(user);

                            // 3. Pošalji SMS
                            if (UserManager.SmsService != null)
                            {
                                await UserManager.SmsService.SendAsync(new IdentityMessage
                                {
                                    Destination = phoneNumber,
                                    Body = "Vas aktivacioni BizSMS kod je: " + code
                                });
                            }

                            logger.Info($"OTP sent to userId: {userId}");

                            // 4. Redirect na Verify
                            return RedirectToAction("VerifyPhoneNumber", new
                            {
                                //phoneNumber,
                                provider
                            });
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Error during OTP generation/sending" + ex);
                            return RedirectToAction("Login");
                        }
                    }
                ////return RedirectToAction("ChooseProvider", new { ReturnUrl = url, RememberMe = model.RememberMe });
                ////return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                //var userId = user.Id;
                //var phoneNumber = user.PhoneNumber;
                ////return RedirectToAction("VerifyPhoneNumber", new { provider = "Phone Code" }); //, ReturnUrl = returnUrl, model.RememberMe }); //VerifyCode
                //var provider = "Phone Code"; // Definišite provajdera ovde
                ////Session["OtpPending"] = true;
                //return RedirectToAction("VerifyPhoneNumber", new { phoneNumber, provider });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", Resources.Resources.InvalidLogin);
                    return View(model);
            }
        }

        //[HttpGet]
        //[AllowAnonymous]
        public ActionResult AddPhoneNumber()
        {
            return View();
        }

        //
        // POST: /Manage/AddPhoneNumber
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        //   [AuthorizeUser(Roles = "Administrator")]
        public async Task<ActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // Generate the token and send it
            // var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), model.Number);

            var userId = await SignInManager.GetVerifiedUserIdAsync(); //User.Identity.GetUserId();
            var user = await UserManager.FindByIdAsync(userId);

            var phoneNumber = user.PhoneNumber;

            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(userId, phoneNumber);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "Your security code is: " + code
                };
                await UserManager.SmsService.SendAsync(message);
            }
            return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });
        }

        [HttpGet]
        [AllowAnonymous]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public async Task<ActionResult> VerifyPhoneNumber(string provider) //string phoneNumber, string provider)
        {
            // 1. već ulogovan -> nema OTP
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            // 2. nema 2FA konteksta -> expired
            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
            {
                //TempData["Error"] = "Sesija je istekla. Prijavite se ponovo.";
                ModelState.AddModelError("", "Sesija je istekla. Prijavite se ponovo.");
                return RedirectToAction("Login");
            }

            var user = await UserManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] = "Korisnik nije pronađen.";
                return RedirectToAction("Login");
            }
            //ViewBag.PhoneCodeSentAt = user.PhoneCodeSentAt?.ToString("o"); // da li ova linija treba da bude tu?

            return View(new VerifyPhoneNumberViewModel
            {
                UserId = userId,
                Provider = "Phone Code",
                PhoneCodeSentAt = user.PhoneCodeSentAt
                //PhoneNumber = phoneNumber
            });
        }
        public async Task<ActionResult> VerifyPhoneNumberOld(string phoneNumber, string provider)
        {
            logger.Info($"Generating 2FA code for provider: {provider}");

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
            {
                // Ako je već ulogovan -> idi na Home
                if (User.Identity.IsAuthenticated)
                    return RedirectToAction("Index", "Home");

                // Inače -> nema validan 2FA flow
                return RedirectToAction("Login");
            }

            //var user = await UserManager.FindByIdAsync(userId);

            //if (user == null)
            //    return RedirectToAction("Login");
            if (string.IsNullOrEmpty(provider))
            {
                return RedirectToAction("Login");
            }

            return View(new VerifyPhoneNumberViewModel
            {
                PhoneNumber = phoneNumber,
                Provider = provider
            });
            //var code = await UserManager.GenerateChangePhoneNumberTokenAsync(userId, phoneNumber);
            //var code = await UserManager.GenerateTwoFactorTokenAsync(userId, provider);

            //user.PhoneCodeSentAt = DateTime.UtcNow;
            //await UserManager.UpdateAsync(user);

            //ViewBag.PhoneCodeSentAt = user.PhoneCodeSentAt?.ToString("o");

            //logger.Info($"Generated code: {code} for userId: {userId} and phoneNumber: {phoneNumber}");

            //    if (UserManager.SmsService != null)
            //    {
            //        var message = new IdentityMessage
            //        {
            //            Destination = phoneNumber,
            //            Body = "Vas aktivacioni BizSMS kod je: " + code
            //        };
            //        await UserManager.SmsService.SendAsync(message);
            //    }

            //    return View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber, Provider = provider, UserId = user.Id });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            // return View("VerifyPhoneNumber", model);
            {
                //TempData["Error"] = "Neispravan unos.";
                ModelState.AddModelError("", "Neispravan unos.");
                return RedirectToAction("VerifyPhoneNumber");
            }

            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
                return RedirectToAction("Login");

            var user = await UserManager.FindByIdAsync(userId);
            //ViewBag.PhoneCodeSentAt = user.PhoneCodeSentAt?.ToString("o");

            if (user == null)
                return RedirectToAction("Login");

            if (user.PhoneCodeSentAt == null ||
                user.PhoneCodeSentAt < DateTime.Now.AddMinutes(-2)) //UtcNow.AddMinutes(-2))
            {
                TempData["Error"] = "Kod je istekao. Zatražite novi!";
                ModelState.AddModelError("", "Kod je istekao. Zatražite novi.");
                return RedirectToAction("VerifyPhoneNumber");
            }
            // TEST KORISNIK - ZA LAKŠE TESTIRANJE OTP FLOW-A
            bool isTestUser = user.UserName == "marijamark";
            if (isTestUser && model.Code == "123456")
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                return RedirectToAction("Index", "Home");
            }

            var result = await SignInManager.TwoFactorSignInAsync(
                "Phone Code",
                model.Code,
                isPersistent: false,
                rememberBrowser: false
            );

            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToAction("Index", "Home");

                case SignInStatus.LockedOut:
                    return View("Lockout");

                case SignInStatus.Failure:
                default:
                    TempData["Error"] = "Uneti kod nije validan!";
                    ModelState.AddModelError("", "Uneti kod nije validan.");
                    return RedirectToAction("VerifyPhoneNumber");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResendCode(VerifyPhoneNumberViewModel model)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
                return RedirectToAction("Login");

            var user = await UserManager.FindByIdAsync(userId);

            if (user == null)
                return RedirectToAction("Login");

            // RATE LIMIT PORUKA
            if (user.PhoneCodeSentAt != null &&
                user.PhoneCodeSentAt > DateTime.Now.AddSeconds(-30)) //UtcNow.AddSeconds(-30))
            {
                TempData["MessageResend"] = "Sačekajte 30s pre ponovnog slanja.";
                ModelState.AddModelError("", "Sačekajte 30s pre ponovnog slanja.");
                return RedirectToAction("VerifyPhoneNumber");
            }

            //var code = await UserManager.GenerateTwoFactorTokenAsync(userId, model.Provider);
            string code;

            if (user.UserName == "marijamark")
                code = "123456";
            else
                code = await UserManager.GenerateTwoFactorTokenAsync(userId, model.Provider);

            user.PhoneCodeSentAt = DateTime.Now; //.UtcNow;
            await UserManager.UpdateAsync(user);

            if (UserManager.SmsService != null)
            {
                await UserManager.SmsService.SendAsync(new IdentityMessage
                {
                    Destination = user.PhoneNumber,
                    Body = "Vas aktivacioni BizSMS kod je: " + code
                });
            }

            // PORUKA ZA USPEH
            ModelState.AddModelError("", "Novi kod je poslat.");
            TempData["Message"] = "Novi kod je poslat.";

            //ViewBag.PhoneCodeSentAt = user.PhoneCodeSentAt?.ToString("o");

            //return View("VerifyPhoneNumber", model);
            return RedirectToAction("VerifyPhoneNumber");
        }
        public async Task<ActionResult> ResendCodeOld(VerifyPhoneNumberViewModel model)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
                return RedirectToAction("Login");

            await UserManager.GenerateTwoFactorTokenAsync(userId, model.Provider);

            ViewBag.Status = "Kod je ponovo poslat";
            ViewBag.PhoneCodeSentAt = DateTime.UtcNow;

            return View("VerifyPhoneNumber", model);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        public async Task<ActionResult> VerifyPhoneNumberOld(VerifyPhoneNumberViewModel model, string returnUrl, string action)
        {
            //if (!ModelState.IsValid)
            //{
            //    return View(model);
            //}
            var userId = await SignInManager.GetVerifiedUserIdAsync();

            if (userId == null)
            {
                if (User.Identity.IsAuthenticated)
                    return RedirectToAction("Index", "Home");

                return RedirectToAction("Login");
            }

            var user = await UserManager.FindByIdAsync(userId); //(model.UserId);

            if (user == null)
                return RedirectToAction("Login");

            //var sessionUserId = await SignInManager.GetVerifiedUserIdAsync();
            //var userId = sessionUserId ?? model.UserId;

            logger.Info($"Verifying code with provider: {model.Provider} and {model.Code} for userId: {userId}");

            if (action == "verify")
            {
                if (user.PhoneCodeSentAt == null || user.PhoneCodeSentAt < DateTime.UtcNow.AddMinutes(-2))
                {
                    logger.Warn($"Expired OTP for userId: {userId}");
                    ModelState.AddModelError("", "Kod je istekao. Zatražite novi.");
                    return View(model);
                }

                //var isValidCode = await UserManager.VerifyTwoFactorTokenAsync(userId, "Phone Code", model.Code);
                //if (!isValidCode)
                //{
                //    logger.Warn($"Invalid code: {model.Code} for userId: {userId}");
                //    ModelState.AddModelError("", "Uneti kod nije validan.");
                //    return View(model);
                //}
                var result = await SignInManager.TwoFactorSignInAsync("Phone Code", model.Code, isPersistent: false, rememberBrowser: false);
                switch (result)
                {
                    case SignInStatus.Success:
                        //Session["OtpPending"] = null;
                        return RedirectToAction("Index", "Home");
                    case SignInStatus.LockedOut:
                        return View("Lockout");
                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Uneti kod nije validan.");
                        return View(model);
                }
            }
            if (action == "resend")
            {
                if (userId == null)
                {
                    return RedirectToAction("Login");
                }
                try
                {
                    if (user.PhoneCodeSentAt != null && user.PhoneCodeSentAt > DateTime.UtcNow.AddSeconds(-30))
                    {
                        ModelState.AddModelError("", "Sačekajte 30s pre ponovnog slanja.");
                        return View(model);
                    }
                    var code = await UserManager.GenerateTwoFactorTokenAsync(userId, model.Provider); // model.UserId

                    user.PhoneCodeSentAt = DateTime.UtcNow;
                    await UserManager.UpdateAsync(user);

                    ViewBag.PhoneCodeSentAt = user.PhoneCodeSentAt?.ToString("o");

                    if (UserManager.SmsService != null)
                    {
                        await UserManager.SmsService.SendAsync(new IdentityMessage
                        {
                            Destination = model.PhoneNumber,
                            Body = "Vas aktivacioni BizSMS kod je: " + code
                        });
                    }

                    ModelState.AddModelError("", "Novi kod je poslat.");
                    return View(model);
                }
                catch (Exception ex)
                {
                    logger.Error("OTP resend failed " + ex);
                    return RedirectToAction("Login");
                }
            }
            else
            {
                ModelState.AddModelError("", "Invalid action.");
                return View(model);
            }
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        public async Task<ActionResult> VerifyPhoneNumberOldOld(VerifyPhoneNumberViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = await SignInManager.GetVerifiedUserIdAsync();
            logger.Info($"Verifying code with provider: {model.Provider} and {model.Code} for userId: {userId}");

            var user = await UserManager.FindByIdAsync(userId); //model.UserId); 

            if (user.PhoneCodeSentAt == null ||
                user.PhoneCodeSentAt < DateTime.UtcNow.AddMinutes(-2))
            {
                logger.Warn($"Expired OTP for userId: {userId}");
                ModelState.AddModelError("", "Kod je istekao. Zatražite novi.");
                return RedirectToAction("Login"); //return View(model);
            }

            var isValidCode = await UserManager.VerifyTwoFactorTokenAsync(userId, "Phone Code", model.Code);
            if (!isValidCode)
            {
                logger.Warn($"Invalid code: {model.Code} for userId: {userId}");
                ModelState.AddModelError("", "Uneti kod nije validan.");
                return View(model);
            }

            var result = await SignInManager.TwoFactorSignInAsync("Phone Code", model.Code, isPersistent: false, rememberBrowser: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToAction("Index", "Home");
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Failed to verify phone");
                    return View(model);
            }
        }

        //[HttpGet]
        //[AllowAnonymous]
        //public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        //{
        //    var result = await SignInManager.SendTwoFactorCodeAsync("SMS");
        //    if (!result)
        //    {
        //        return View("Error");
        //    }

        //    return View(new VerifyCodeViewModel { Provider = "SMS", ReturnUrl = returnUrl, RememberMe = rememberMe });
        //}

        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }

        //    var userId = await SignInManager.GetVerifiedUserIdAsync();
        //    if (userId == null)
        //    {
        //        return View("Error");
        //    }

        //    var isValid = await UserManager.VerifyTwoFactorTokenAsync(userId, model.Provider, model.Code);
        //    Console.WriteLine($"Validacija korisničkog unosa: {isValid}");

        //    if (!isValid)
        //    {
        //        ModelState.AddModelError("", "Uneti kod nije validan.");
        //        //return View(model);
        //    }

        //    var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, true, model.RememberBrowser);//model.RememberMe, model.RememberBrowser);

        //    var isTestCode = model.Code == "123456"; // Zamena za testni kod
        //    if (isTestCode)
        //    {
        //        //return SignInStatus.Success;
        //        return RedirectToLocal(model.ReturnUrl);
        //    }

        //    switch (result)
        //    {
        //        case SignInStatus.Success:
        //            return RedirectToLocal(model.ReturnUrl);
        //        case SignInStatus.LockedOut:
        //            return View("Lockout");
        //        default:
        //            ModelState.AddModelError("", "Invalid code.");
        //            return View(model);
        //    }
        //}

        //[HttpGet]
        //[AllowAnonymous]
        public async Task<ActionResult> ChooseProvider(string returnUrl, bool rememberMe)//bool? rememberMe = false)//SendCode(string returnUrl, bool rememberMe)
        {

            // Dobijanje ID-ja trenutno prijavljenog korisnika
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await UserManager.FindByIdAsync(userId);

            if (user.PhoneNumberConfirmed && user.TwoFactorEnabled)
            {
                // Korisnik nema potvrđen broj telefona
                Console.WriteLine("Phone number is registered and confirmed.");
            }

            // Dobijanje liste dostupnih 2FA provajdera za korisnika
            var providers = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            //var providers = await manager.GetValidTwoFactorProvidersAsync(userId);

            if (!providers.Any())
            {
                ModelState.AddModelError("", "No two-factor providers available.");
                return RedirectToAction("Login");
            }

            // Popunjavanje ViewModel-a
            //var model = new SendCodeViewModel
            var model = new ChooseProviderModel
            {
                Providers = providers.Select(provider => new SelectListItem
                {
                    Text = provider,
                    Value = provider
                }).ToList(),
                ReturnUrl = returnUrl,
                RememberMe = rememberMe //?? false
            };

            return View(model);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        public async Task<ActionResult> ChooseProvider(ChooseProviderModel model)//SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Slanje 2FA koda putem izabranog provajdera
            var result = await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider);
            if (!result)
            {
                ModelState.AddModelError("", "Failed to send verification code.");
                return View(model);
            }
            return RedirectToAction("TwoFactor", new { provider = model.SelectedProvider });
            // Redirekcija na verifikaciju koda
            //return RedirectToAction("VerifyCode", new
            //return RedirectToAction("TwoFactor", new
            //{
            //    Provider = model.SelectedProvider,
            //    ReturnUrl = model.ReturnUrl,
            //    RememberMe = model.RememberMe
            //});
        }

        //[HttpGet]
        //[AllowAnonymous]
        //public async Task<ActionResult> ChooseProvider1(string returnUrl, bool rememberMe)
        //{
        //    var userId = await SignInManager.GetVerifiedUserIdAsync();
        //    var providers = await UserManager.GetValidTwoFactorProvidersAsync(userId);
        //    //var providers = await applicationUser.GetValidTwoFactorProvidersAsync(userId);

        //    return View(new ChooseProviderModel { Providers = providers.ToList() });
        //}

        //[HttpPost]
        //public async Task<ActionResult> ChooseProvider(ChooseProviderModel model)
        //{
        //    await SignInManager.SendTwoFactorCodeAsync(model.ChosenProvider);
        //    return RedirectToAction("TwoFactor", "Account", new { provider = model.ChosenProvider });
        //}

        //[HttpGet]
        //[AllowAnonymous]
        public ActionResult TwoFactor(string provider)//, string returnUrl, bool rememberMe)
        {
            //return View(new TwoFactorModel { Provider = provider });
            return View(new TwoFactorModel { Provider = provider });//, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        public async Task<ActionResult> TwoFactor(TwoFactorModel model)
        {
            var signInStatus = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, model.RememberMe, model.RememberBrowser);//true, model.RememberBrowser);
            switch (signInStatus)
            {
                case SignInStatus.Success:
                    return RedirectToAction("Index", "Home");
                default:
                    ModelState.AddModelError("", "Invalid Credentials");
                    return View(model);
            }
        }

        //public class ChooseProviderModel
        //{
        //    public List<string> Providers { get; set; }
        //    public string ChosenProvider { get; set; }
        //}

        //public class TwoFactorModel
        //{
        //    public string Provider { get; set; }
        //    public string Code { get; set; }
        //    public bool RememberBrowser { get; set; }
        //}


        //GET
        // GET: /Account/ResetPassword/{UserID}32decb45-68c6-4a0e-b873-dc77d9c5b7eb
        [AuthorizeUser(Roles = "Administrator")]
        public ActionResult ResetPassword(string id)
        {
            //var logedUserId = User.Identity.GetUserId();
            //int logedClientId = db.Users.Where(u => u.Id == logedUserId).FirstOrDefault().ClientID;
            logger.Info("Reset password for userId: " + id);
            if (id == null)
            {
                logger.Warn("userId is null");
                throw new HttpException(400, "Bad request");
            }

            //var userId = SignInManager.GetVerifiedUserIdAsync(); //User.Identity.GetUserId();
            //var user = UserManager.FindByIdAsync(userId.Result).Result;

            var user = db.Users.Where(u => u.Id == id).FirstOrDefault();

            if (user == null)
            {
                logger.Warn("user not found");
                throw new HttpException(404, "Not Found");
            }

            ResetPasswordViewModel model = new ResetPasswordViewModel()
            {
                ClientID = user.ClientID,
                UserID = user.Id,
                Username = user.UserName
            };

            return View(model);
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeUser(Roles = "Administrator")]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            logger.Info("Reset password for userId: " + model.UserID);
            // var user = db.Users.Find(model.UserID);
            // var userId = SignInManager.GetVerifiedUserIdAsync(); //User.Identity.GetUserId();
            var user = UserManager.FindByIdAsync(model.UserID).Result;

            if (user == null)
            {
                logger.Warn("user not found");
                throw new HttpException(400, "Bad request");
            }
            logger.Info("Reseting password for user: " + user.UserName);
            user.PasswordHash = UserManager.PasswordHasher.HashPassword(model.Password);

            var result = await UserManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                logger.Info("Password successfully changed");
                TempData["StatusMessage"] = Resources.Resources.ResetPasswordSuccess;
                return RedirectToAction("ClientUsers", "AdminManage", new { id = user.ClientID });
            }
            logger.Warn("Reset password resulted with error: " + result.ToString());
            AddErrors(result);

            return View();
        }

        //GET
        // GET: /Account/ResetPassword/{ClientID}
        [AuthorizeUser(Roles = "Administrator")]
        public async Task<ActionResult> ResetClientPassword(int id)
        {
            var client = db.Client.Find(id);

            if (client == null)
                throw new HttpException(404, "Not Found");

            ApplicationUser user = await GetClientUser(client);

            ResetPasswordViewModel model = new ResetPasswordViewModel()
            {
                UserID = user.Id,
                Username = user.UserName
            };

            return View(model);
        }

        //[AuthorizeUser(Roles = "Administrator")]
        //public async Task<ActionResult> ResetMyPassword()
        //{
        //    var users = db.Users.Where(u => u.UserName == "sonja100").ToList();

        //    foreach (var user in users)
        //    {
        //        user.PasswordHash = UserManager.PasswordHasher.HashPassword(user.UserName);

        //        var result = await UserManager.UpdateAsync(user);
        //    }

        //    return new HttpStatusCodeResult(200);
        //}

        //[AuthorizeUser(Roles = "Administrator")]
        //public async Task<ActionResult> ResetAllClientPassword()
        //{
        //    var users = db.Users.Where(u => u.Id != "4b9c9074-77b1-4da9-a0fe-d34f2973b05e").ToList();

        //    foreach(var user in users)
        //    {
        //        user.PasswordHash = UserManager.PasswordHasher.HashPassword(user.UserName);

        //        var result = await UserManager.UpdateAsync(user);
        //    }

        //    return new HttpStatusCodeResult(200);
        //}

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            logger.Info("Log off user: " + User.Identity.GetUserName());
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

            Session.Clear();
            Session.Abandon();

            //return RedirectToAction("Index", "Home");
            return RedirectToAction("Login", "Account");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public ActionResult Index()
        {
            return Content("OKOKOK");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        private async Task<ApplicationUser> GetClientUser(ClientModel client)
        {
            var users = UserManager.Users.Where(c => c.ClientID == client.ClientID).ToList();
            ApplicationUser selectedUser = null;
            try
            {
                foreach (var user in users)
                {
                    if (await UserManager.IsInRoleAsync(user.Id, "Client"))
                    {
                        selectedUser = user;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }

            return selectedUser;
        }
        #endregion
    }
}