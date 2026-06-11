using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using BizSMS.Helpers;

namespace BizSMS.Attributes
{
    public class DefaultApiLoggingAttribute : ActionFilterAttribute
    {
        private Logger logger = new Logger();
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var controllerName = actionContext.ControllerContext.ControllerDescriptor.ControllerName;
            var actionName = actionContext.ActionDescriptor.ActionName;
            logger.SetControllerAction(controllerName, actionName);
            logger.Info("Default API logging");
            base.OnActionExecuting(actionContext);
        }
    }
}