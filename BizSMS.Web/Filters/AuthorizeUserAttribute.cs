using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BizSMS.Web.Filters
{
    // Zamena za legacy BizSMS.Attributes.AuthorizeUserAttribute (§7.1).
    // Autentifikovan + nije IsCanceled + (ako su navedene) ima jednu od rola.
    public class AuthorizeUserAttribute : TypeFilterAttribute
    {
        public AuthorizeUserAttribute() : base(typeof(AuthorizeUserFilter))
        {
            Arguments = new object[] { string.Empty };
        }

        private string _roles = string.Empty;
        public string Roles
        {
            get => _roles;
            set
            {
                _roles = value ?? string.Empty;
                Arguments = new object[] { _roles };
            }
        }
    }

    public class AuthorizeUserFilter : IAsyncAuthorizationFilter
    {
        private readonly string _roles;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthorizeUserFilter(string roles, UserManager<ApplicationUser> userManager)
        {
            _roles = roles ?? string.Empty;
            _userManager = userManager;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var principal = context.HttpContext.User;

            if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
            {
                // STANDARDNO ponašanje iz legacy-ja: redirect na login
                context.Result = new RedirectResult("~/Account/Login?sessionExpired=true");
                return;
            }

            var user = await _userManager.GetUserAsync(principal);

            if (user == null || user.IsCanceled)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            var roles = _roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roles.Length > 0)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!roles.Any(r => userRoles.Contains(r)))
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                    return;
                }
            }
        }
    }
}
