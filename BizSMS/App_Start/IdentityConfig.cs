using BizSMS.Models;
using BizSMS.SDPSendSms;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Web.Services3.Design;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using RestSharp;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Services;

namespace BizSMS
{
    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Plug in your email service here to send an email.
            return Task.FromResult(0);
        }
    }

    public class SmsService : IIdentityMessageService
    {
        //public Task SendAsync(IdentityMessage message)
        //{
        //    // Plug in your SMS service here to send a text message.
        //    return Task.FromResult(0);
        //}

        //public async Task SendAsync(IdentityMessage message)
        //{

        //}

        //public async Task SendAsync(IdentityMessage message)
        //{
        //    //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        //    var h2s = new http2sms.http2sms();
        //    string alphanumeric = "MTS"; //"BizSMS"; // Replace with actual alphanumeric value
        //    string msgId = Send(alphanumeric, message.Body, h2s, message.Destination);

        //    Console.WriteLine($"SMS sent with msgId: {msgId}");
        //    //await Task.CompletedTask;
        //}


        public async Task SendAsync(IdentityMessage message)
        {
            // ovde zelim da pozovem sendSms metodu koja koristi SOAP servis, a ne http2sms
            string alphanumeric = "MTS"; //"BizSMS";

            var result = sendSms(alphanumeric, message.Body, message.Destination); //message.Destination, message.Body, alphanumeric);
            //string msgId = Send(alphanumeric, message.Body, h2s, message.Destination);

            Console.WriteLine($"SMS sent with msgId: {result}");
        }

        private string sendSms(string shortNumber, string message, string sessionMSISDN) //, string userName, string password)
        {

            //UsernameToken userToken;
            //userToken = new UsernameToken(userName, password);
            try
            {
                //SendSmsService ssmss = new SendSmsService();
                //ssmss.RequestSoapContext.Security.Tokens.Add(userToken);
                //string result = ssmss.sendSms(new string[] { "tel:"+sessionMSISDN }, shortNumber, new ChargingInformation(), message);
                //return result;

                SendSmsService ssmss = new SendSmsService();
                Policy policy = new Policy();
                policy.Assertions.Add(new CustomHeadersAssertion());
                ssmss.SetPolicy(policy);

                string formatNumber = sessionMSISDN.Substring(1);

                string result = ssmss.sendSms(new string[] { "tel:381" + sessionMSISDN.Substring(1) }, "MTS", new ChargingInformation(), message);
                return result;
            }
            catch (Exception exc)
            {
                return exc.Message;
            }
        }


        private string Send(string alphanumeric, string message, http2sms.http2sms h2s, string number)
        {
            string msgId = "-1";

            //var phoneNumberFormat = @"^(06\d{7,8})";
            //if (Regex.Match(alphanumeric, phoneNumberFormat).Success)
            //{
            //    alphanumeric = "381" + alphanumeric.Remove(0, 1).Trim();
            //}

            string SendToNumber = "381" + number.Remove(0, 1).Trim();
            //string SendToNumber = number;

            try
            {
                msgId = h2s.Send(alphanumeric, new string[] { SendToNumber }, message,
                    "BizSMS", "conBizsms");
                msgId = msgId.Trim();
            }
            catch
            {
                msgId = "-1";
            }

            return msgId;
        }

    }

    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            // Configure validation logic for usernames
            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = false
            };

            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
            };

            // Configure user lockout defaults
            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = 5;

            // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            // You can write your own provider and plug it in here.
            manager.RegisterTwoFactorProvider("Phone Code", new PhoneNumberTokenProvider<ApplicationUser>//"SMS", new PhoneNumberTokenProvider<ApplicationUser>
            {
                MessageFormat = "Your security code is {0}"
            });
            foreach (var provider in manager.TwoFactorProviders)
            {
                Console.WriteLine($"Registered Provider: {provider.Key}");
            }
            //manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<ApplicationUser>
            //{
            //    Subject = "Security Code",
            //    BodyFormat = "Your security code is {0}"
            //});
            //manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider =
                    new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    // Configure the application sign-in manager which is used in this application.
    public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(ApplicationUser user)
        {
            return user.GenerateUserIdentityAsync((ApplicationUserManager)UserManager);
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
        }
    }
}
