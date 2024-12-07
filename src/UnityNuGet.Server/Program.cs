using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityNuGet;
using UnityNuGet.Server;

var builder = WebApplication.CreateBuilder(args);

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

// Also enable NewtonsoftJson serialization
builder.Services.AddControllers().AddNewtonsoftJson();

var app = builder.Build();

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
app.MapControllers();
app.MapStatus();

app.Run();
