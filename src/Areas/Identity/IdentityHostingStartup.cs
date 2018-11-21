using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(AdvantageTool.Areas.Identity.IdentityHostingStartup))]
namespace AdvantageTool.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
            });
        }
    }
}