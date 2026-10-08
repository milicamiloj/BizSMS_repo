using System.Security.Claims;
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.BaseController (§8.1).
    // DI umesto ručnog new ApplicationDbContext()/OWIN UserManager; kultura je zaseban pipeline korak.
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext db;
        protected readonly UserManager<ApplicationUser> UserManager;
        protected readonly Logger logger = new Logger();

        protected BaseController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            this.db = db;
            UserManager = userManager;
        }

        // ClientID iz claim-a (AppClaimsPrincipalFactory) — bez dodatnog upita ka bazi.
        protected int CurrentClientId => int.Parse(User.FindFirstValue("ClientID") ?? "0");

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Univerzalni "Default logging" je premešten u globalni ActionLoggingFilter (§7.4).
            // Ovde ostaje samo seedovanje konteksta per-instance logger-a koji eksplicitni
            // logger.Info/Warn/Error pozivi u izvedenim kontrolerima koriste za prefiks.
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
            var actionName = context.RouteData.Values["action"]?.ToString() ?? "";
            logger.SetControllerAction(controllerName, actionName);

            base.OnActionExecuting(context);
        }
    }
}
