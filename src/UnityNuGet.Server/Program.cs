using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityNuGet;
using UnityNuGet.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add the registry
builder.Services.AddSingleton<Registry>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<Registry>());
// Add the registry cache initializer
builder.Services.AddHostedService<RegistryCacheInitializer>();
// Add the registry cache updater
builder.Services.AddHostedService<RegistryCacheUpdater>();
// Add the registry cache report
builder.Services.AddSingleton<RegistryCacheReport>();
builder.Services.AddSingleton<RegistryCacheSingleton>();

builder.Services.Configure<RegistryOptions>(builder.Configuration.GetSection("Registry"));
builder.Services.AddSingleton<IValidateOptions<RegistryOptions>, ValidateRegistryOptions>();
builder.Services.AddOptionsWithValidateOnStart<RegistryOptions, ValidateRegistryOptions>();

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.Configure<JsonOptions>(options =>
{
    foreach (JsonConverter converter in UnityNugetJsonSerializerContext.Default.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.LogRequestHeaders(app.Services.GetRequiredService<ILoggerFactory>());
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseRouting();
app.MapHealthChecks("/health");
app.MapUnityNuGetEndpoints();

app.Run();

public partial class Program
{
}
