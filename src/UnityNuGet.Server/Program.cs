using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UnityNuGet.Server
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    //webBuilder.UseSetting("detailedErrors", "true");
                    webBuilder.ConfigureServices((context, services) =>
                    {
                        // Add the registry cache initializer
                        services.AddHostedService<RegistryCacheInitializer>();
                        // Add the registry cache updater
                        services.AddHostedService<RegistryCacheUpdater>();
                        // Add the registry cache report
                        services.AddSingleton<RegistryCacheReport>();
                        services.AddSingleton<RegistryCacheSingleton>();

                        services.AddOptions<RegistryOptions>()
                            .Bind(context.Configuration.GetSection("Registry"))
                            .ValidateDataAnnotations();
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
