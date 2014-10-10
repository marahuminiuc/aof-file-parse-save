using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(AOFBatchTest.Startup))]
namespace AOFBatchTest
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
