using System.Security.Claims;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BizSMS.Web.Identity
{
    // Zamena za legacy ApplicationUser.GenerateUserIdentityAsync (MIGRACIJA.md §6.3).
    // Core sam ubacuje name/role claim-ove; ovde se dodaju aplikativni claim-ovi.
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public AppClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim("ClientID", user.ClientID.ToString()));
            return identity;
        }
    }
}
