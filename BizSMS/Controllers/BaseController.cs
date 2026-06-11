using BizSMS.Helpers;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    public class BaseController : Controller
    {
        private UserManager<ApplicationUser> _userManager;
        private ApplicationDbContext _db;
        private Logger logger = new Logger();
        public BaseController()
        {
            db = new ApplicationDbContext();
        }

        public BaseController(UserManager<ApplicationUser> userManager)
        {
            db = new ApplicationDbContext();
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager
        {
            get
            {
                //return _userManager ?? new UserManager<ApplicationUser>(new Microsoft.AspNet.Identity.EntityFramework.UserStore<ApplicationUser>(db));
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();


            }
            private set
            {
                _userManager = value;
            }
        }

        protected ApplicationDbContext db
        {
            get
            {
                _db = _db ?? new ApplicationDbContext();
                return _db;
            }
            private set
            {
                _db = value;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controllerName = filterContext.RouteData.Values["controller"].ToString();
            var actionName = filterContext.RouteData.Values["action"].ToString();
            logger.SetControllerAction(controllerName, actionName);
            logger.Info("Default logging");

            base.OnActionExecuting(filterContext);
        }

        protected override IAsyncResult BeginExecuteCore(AsyncCallback callback, object state)
        {
            string cultureName = null;

            // Attempt to read the culture cookie from Request
            HttpCookie cultureCookie = Request.Cookies["_culture"];
            if (cultureCookie != null)
                cultureName = cultureCookie.Value;
            else
                cultureName = "sr-YU";
            //ovo je bilo u else pre harkodovanja na "sr-YU" (03/04/2020):
            //cultureName = Request.UserLanguages != null && Request.UserLanguages.Length > 0 ?
            //Request.UserLanguages[0] :  // obtain it from HTTP header AcceptLanguages
            //                    null;

            // Validate culture name
            cultureName = CultureHelper.GetImplementedCulture(cultureName); // This is safe

            // Modify current thread's cultures       
            System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo(cultureName);
            ci.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;
            
            return base.BeginExecuteCore(callback, state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            if (disposing && _db != null)
            {
                _db.Dispose();
                _db = null;
            }

            base.Dispose(disposing);
        }
    }
}