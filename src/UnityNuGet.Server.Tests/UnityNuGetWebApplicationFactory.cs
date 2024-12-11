using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNuGet.Server.Tests
{
    internal class UnityNuGetWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string PackageName = "Newtonsoft.Json";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { WebHostDefaults.ServerUrlsKey, "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                services.Configure<RegistryOptions>(options =>
                {
                    options.Filter = PackageName;
                });
            });
        }
    }
}
