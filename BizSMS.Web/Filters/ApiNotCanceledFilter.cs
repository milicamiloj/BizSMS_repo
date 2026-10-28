using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Filters
{
    // Globalni filter za sve [ApiController] kontrolere: otkazan (IsCanceled) ili obrisan korisnik dobija 403
    // iako mu je auth cookie još važeći. MVC kontrolere ovo već pokriva AuthorizeUserFilter.
    public class ApiNotCanceledFilter : IAsyncAuthorizationFilter
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ApiNotCanceledFilter(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!context.ActionDescriptor.EndpointMetadata.OfType<ApiControllerAttribute>().Any())
                return;

            var principal = context.HttpContext.User;
            if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
                return;   // neautentifikovane obrađuje [Authorize] (401)

            var user = await _userManager.GetUserAsync(principal);
            if (user == null || user.IsCanceled)
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
