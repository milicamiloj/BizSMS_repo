using BizSMS.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    public class ErrorController : BaseController
    {
        Helpers.Logger log = new Helpers.Logger();       

        public ActionResult General(Exception exception)
        {
            Response.ContentType = "text/html";
            log.Error("General: " + exception.Message);
            return View();
        }

        public ActionResult Http400()
        {
            Response.ContentType = "text/html";
            log.Error("Not Found");
            return View();
        }

        public ActionResult Http401()
        {
            Response.ContentType = "text/html";
            log.Error("Http401");
            return View();
        }

        public ActionResult Http403()
        {
            Response.ContentType = "text/html";
            log.Error("Http403");
            return View();
        }

        public ActionResult Http404()
        {
            Response.ContentType = "text/html";
            log.Error("Http404");
            return View();
        }
    }
}