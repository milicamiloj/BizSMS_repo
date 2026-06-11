using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using BizSMS.Models;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNet.Identity.EntityFramework;

namespace BizSMS
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the db context, user manager and signin manager to use a single instance per request
            app.CreatePerOwinContext(ApplicationDbContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                ExpireTimeSpan = TimeSpan.FromMinutes(15),
                SlidingExpiration = true, //false,

                CookieSecure = CookieSecureOption.Always,
                CookieHttpOnly = true,

                Provider = new CookieAuthenticationProvider
                {
                    OnApplyRedirect = context =>
                    {
                        if (context.Request.Path.StartsWithSegments(new PathString("/api")))
                        {
                            context.Response.StatusCode = 401;
                            return;
                        }
                        // Ako nije login već neka druga stranica
                        //if (!context.Request.Path.StartsWithSegments(new PathString("/Account/Login")))
                        //{
                        context.Response.Redirect(context.RedirectUri);
                        //}
                    },
                    // Enables the application to validate the security stamp when the user logs in.
                    // This is a security feature which is used when you change a password or add an external login to your account.  
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                        validateInterval: TimeSpan.FromMinutes(15),
                        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                }
            });

            app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(2)); // zapamti na 2 minuta da je korisnik prošao lozinku i čeka otp
            app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            var options = new SqlServerStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(60)
            };

            GlobalConfiguration.Configuration.UseSqlServerStorage("HangfireDB");
            app.UseHangfireDashboard();
            app.UseHangfireServer();
        }
    }
}