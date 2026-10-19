using Hangfire.Dashboard;

namespace BizSMS.Web.Filters
{
    // Zaštita Hangfire dashboard-a (/hangfire): dostupan samo ulogovanom Administrator-u.
    // Legacy app.UseHangfireDashboard() nije imao nikakvu zaštitu (rizik iz plana, §9.2).
    public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            return http.User?.Identity?.IsAuthenticated == true
                && http.User.IsInRole("Administrator");
        }
    }
}
