using BizSMS.Web.Helpers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Filters
{
    // Univerzalno "Default logging" za SVAKU akciju (§7.4) — zamena za legacy logging filter.
    // Registrovan globalno u Program.cs, pa pokriva i kontrolere koji NE nasleđuju BaseController
    // (Error, Account, Manage) i API kontrolere. Eksplicitni logger.X() pozivi u kontrolerima
    // i dalje koriste svoj per-instance kontekst (SetControllerAction).
    public class ActionLoggingFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
            var actionName = context.RouteData.Values["action"]?.ToString() ?? "";

            var logger = new Logger();
            logger.SetControllerAction(controllerName, actionName);
            logger.Info("Default logging");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
