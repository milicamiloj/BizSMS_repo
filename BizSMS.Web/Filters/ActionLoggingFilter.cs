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

            // Svi logger-i u ovom zahtevu (i u API kontrolerima bez SetControllerAction) čitaju ovo.
            context.HttpContext.Items[Logger.ControllerItemKey] = controllerName;
            context.HttpContext.Items[Logger.ActionItemKey] = actionName;

            new Logger().Info("Default logging");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
