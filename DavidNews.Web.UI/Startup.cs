using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(DavidNews.Web.UI.Startup))]
namespace DavidNews.Web.UI
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
