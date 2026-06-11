using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;
using System.Net.Http;

namespace BizSMS.Attributes
{
    public class AuthorizeUserAttribute : AuthorizeAttribute
    {
        Helpers.Logger logger = new Helpers.Logger();
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.User == null || !httpContext.User.Identity.IsAuthenticated)
                return false;

            var userId = httpContext.User.Identity.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return false;

            var user = HttpContext.Current.GetOwinContext()
                .GetUserManager<ApplicationUserManager>()
                .FindById(userId);

            if (user == null)
                return false;

            if (user.IsCanceled)
                return false;

            //if (httpContext.Session["OtpPending"] != null)
            //    return false;

            return base.AuthorizeCore(httpContext);
        }
        //protected override bool AuthorizeCore(HttpContextBase httpContext)
        //{
        //    if (HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>().FindById(httpContext.User.Identity.GetUserId()).IsCanceled)
        //        return false;
        //    else
        //        return base.AuthorizeCore(httpContext);
        //}

        //protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        //{
        //    bool isAuthenicated = (System.Web.HttpContext.Current.User != null) && System.Web.HttpContext.Current.User.Identity.IsAuthenticated;
        //    logger.SetControllerAction(filterContext.Controller.ToString(), filterContext.ActionDescriptor.ActionName);

        //    if (!isAuthenicated)
        //    {
        //        logger.Error("Not Found");
        //        throw new HttpException(404, "Not Found");
        //    }
        //    else
        //    {
        //        logger.Error("Unauthorized");
        //        throw new HttpException(403, "Unauthorized");
        //    }
        //}

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            //if (filterContext.HttpContext.Session["OtpPending"] != null)
            //{
            //    filterContext.Result = new RedirectResult("~/Account/VerifyPhoneNumber");
            //    return;
            //}
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // redirect na login (STANDARDNO ponašanje)
                filterContext.Result = new RedirectResult("~/Account/Login?sessionExpired=true");
            }
            else
            {
                // ako je logovan ali nema prava
                filterContext.Result = new HttpStatusCodeResult(403);
            }
        }
    }

    public class AuthorizeApiUserAttribute : System.Web.Http.AuthorizeAttribute
    {
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Helpers.Logger logger = new Helpers.Logger();

        protected override void HandleUnauthorizedRequest(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            bool isAuthenicated = (System.Web.HttpContext.Current.User != null) && 
                System.Web.HttpContext.Current.User.Identity.IsAuthenticated;
           
            logger.SetControllerAction(actionContext.ControllerContext.Controller.ToString(), actionContext.ActionDescriptor.ActionName);

            if (!isAuthenicated)
            {
                logger.Error("Unauthenticated user");
                actionContext.Response = actionContext.Request.CreateResponse(System.Net.HttpStatusCode.Unauthorized);//new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                //throw new HttpException(404, "Not Found");
            }
            else
            {
                logger.Error("Unauthorized");
                actionContext.Response = actionContext.Request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                //throw new HttpException(403, "Unauthorized");
            }
        }
    }
}