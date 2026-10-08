using BizSMS.Web.Helpers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.ErrorController + Global.asax Application_Error mapiranje.
    // Plain Controller (ne BaseController): error stranice moraju da rade i za neautentifikovane korisnike.
    // Vezan preko UseExceptionHandler (General) i UseStatusCodePagesWithReExecute (Http4xx) u Program.cs.
    public class ErrorController : Controller
    {
        private readonly Logger log = new Logger();

        public IActionResult General()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            log.Error("General: " + (feature?.Error.Message ?? "nepoznata greška"));
            return View();
        }

        public IActionResult Http400()
        {
            log.Error("Http400");
            return View();
        }

        public IActionResult Http401()
        {
            log.Error("Http401");
            return View();
        }

        public IActionResult Http403()
        {
            log.Error("Http403");
            return View();
        }

        public IActionResult Http404()
        {
            log.Error("Http404");
            return View();
        }
    }
}
