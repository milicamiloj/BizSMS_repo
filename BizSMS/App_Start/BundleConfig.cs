using System.Web;
using System.Web.Optimization;

namespace BizSMS
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-ui").Include(
                        "~/Scripts/jquery-ui-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-unobtrusive").Include(
                        "~/Scripts/jquery.unobtrusive-ajax.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-validate-unobtrusive").Include(
                        "~/Scripts/jquery.validate.unobtrusive.js"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js",
                      "~/Scripts/bootbox.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-datatables").Include(
                "~/Scripts/jquery.dataTables.js",
                "~/Scripts/dataTables.bootstrap.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-datatables-buttons").Include(
                "~/Scripts/dataTables.buttons.js",
                "~/Scripts/jszip.min.js",
                "~/Scripts/buttons.bootstrap.js",
                "~/Scripts/buttons.flash.js",
                "~/Scripts/vfs_fonts.js",
                "~/Scripts/buttons.html5.min.js",
                "~/Scripts/buttons.print.js"));

            bundles.Add(new ScriptBundle("~/bundles/custom").Include(
                   "~/Scripts/Custom/send.sms.js"));

            bundles.Add(new ScriptBundle("~/bundles/datetime-picker").Include(
                   "~/Scripts/jquery.datetimepicker.full.min.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.min.css",
                      "~/Content/site.css",
                      "~/Content/flag-icon.min.css"));

            bundles.Add(new StyleBundle("~/Content/datatable").Include(
                "~/Content/jquery.dataTables.css",
                "~/Content/dataTables.bootstrap.css",
                "~/Content/buttons.bootstrap.css"));

            bundles.Add(new StyleBundle("~/Content/datetime-picker").Include(
                "~/Content/jquery.datetimepicker.min.css"));
        }
    }
}
