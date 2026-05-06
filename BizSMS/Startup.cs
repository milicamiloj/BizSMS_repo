using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(BizSMS.Startup))]
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BizSMS
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
