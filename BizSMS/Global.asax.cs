using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Http;
using BizSMS.Controllers;
using System.Security.Authentication;
using BizSMS.Helpers;
using System.Threading;
using System.Globalization;

namespace BizSMS
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            //System.Web.Helpers.AntiForgeryConfig.RequireSsl = true;

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);

            System.Web.Helpers.AntiForgeryConfig.SuppressXFrameOptionsHeader = true;

            //default validation errors workaround
            ClientDataTypeModelValidatorProvider.ResourceClassKey = "Validation";
            DefaultModelBinder.ResourceClassKey = "Validation";

            //log4net
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo(Server.MapPath("~/Web.config")));

            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Helpers.Logger log = new Helpers.Logger();

            var httpContext = ((MvcApplication)sender).Context;

            var currentRouteData = RouteTable.Routes.GetRouteData(new HttpContextWrapper(httpContext));
            var currentController = " ";
            var currentAction = " ";

            if (currentRouteData != null)
            {
                if (currentRouteData.Values["controller"] != null &&
                    !String.IsNullOrEmpty(currentRouteData.Values["controller"].ToString()))
                {
                    currentController = currentRouteData.Values["controller"].ToString();
                }

                if (currentRouteData.Values["action"] != null &&
                    !String.IsNullOrEmpty(currentRouteData.Values["action"].ToString()))
                {
                    currentAction = currentRouteData.Values["action"].ToString();
                }
            }

            var ex = Server.GetLastError();

            if (ex != null)
            {
                string exMessage = ex.Message;

                if (ex.InnerException != null)
                {
                    exMessage += " inner ex: " + ex.InnerException.Message;
                }
                //Log error
                log.SetControllerAction(currentController, currentAction);
                log.Error(exMessage);
            }

            var controller = new ErrorController();
            var routeData = new RouteData();
            var action = "General";
            var statusCode = 500;

            if (ex is HttpException)
            {
                var httpEx = ex as HttpException;
                statusCode = httpEx.GetHttpCode();

                switch (httpEx.GetHttpCode())
                {
                    case 400:
                        action = "Http400";
                        break;

                    case 401:
                        action = "Http401";
                        break;

                    case 403:
                        action = "Http403";
                        break;

                    case 404:
                        action = "Http404";
                        break;

                    case 500:
                        action = "General";
                        break;

                    default:
                        action = "General";
                        break;
                }
            }
            else if (ex is AuthenticationException)
            {
                action = "Http403";
                statusCode = 403;
            }

            httpContext.ClearError();
            httpContext.Response.Clear();
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.TrySkipIisCustomErrors = true;
            routeData.Values["controller"] = "Error";
            routeData.Values["action"] = action;

            controller.ViewData.Model = new HandleErrorInfo(ex, currentController, currentAction);
            ((IController)controller).Execute(new RequestContext(new HttpContextWrapper(httpContext), routeData));
            
        }
    }
}
