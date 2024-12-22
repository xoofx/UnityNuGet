using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using UnityNuGet;
using UnityNuGet.Npm;
using UnityNuGet.Server;

namespace Microsoft.AspNetCore.Builder
{
    public static class EndpointRouteBuilderExtensions
    {
        public static void MapUnityNuGetEndpoints(this IEndpointRouteBuilder builder)
        {
            builder.MapHome();
            builder.MapGetAll();
            builder.MapGetPackage();
            builder.MapDownloadPackage();
            builder.MapStatus();
        }

        private static void MapHome(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("/", () => Results.Redirect("/-/all"));
        }

        private static void MapGetAll(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("-/all", (RegistryCacheSingleton registryCacheSingleton, RegistryCacheReport registryCacheReport) =>
            {
                if (!TryGetInstance(registryCacheSingleton, registryCacheReport, out RegistryCache? instance, out NpmError? error))
                {
                    return Results.Json(error, UnityNugetJsonSerializerContext.Default);
                }

                NpmPackageListAllResponse? result = instance?.All();
                return Results.Json(result, UnityNugetJsonSerializerContext.Default);
            });
        }

        private static void MapGetPackage(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("{id}", (string id, RegistryCacheSingleton registryCacheSingleton, RegistryCacheReport registryCacheReport) =>
            {
                if (!TryGetInstance(registryCacheSingleton, registryCacheReport, out RegistryCache? instance, out NpmError? error))
                {
                    return Results.Json(error, UnityNugetJsonSerializerContext.Default);
                }

                NpmPackage? package = instance?.GetPackage(id);
                if (package == null)
                {
                    return Results.Json(NpmError.NotFound, UnityNugetJsonSerializerContext.Default);
                }

                return Results.Json(package, UnityNugetJsonSerializerContext.Default);
            });
        }

        private static void MapDownloadPackage(this IEndpointRouteBuilder builder)
        {
            builder.MapMethods("{id}/-/{file}", new[] { HttpMethod.Head.Method, HttpMethod.Get.Method }, handler: (string id, string file, HttpContext httpContext, RegistryCacheSingleton registryCacheSingleton, RegistryCacheReport registryCacheReport) =>
            {
                if (!TryGetInstance(registryCacheSingleton, registryCacheReport, out RegistryCache? instance, out NpmError? error))
                {
                    return Results.Json(error, UnityNugetJsonSerializerContext.Default);
                }

                NpmPackage? package = instance?.GetPackage(id);
                if (package == null)
                {
                    return Results.Json(NpmError.NotFound, UnityNugetJsonSerializerContext.Default);
                }

                if (!file.StartsWith(id + "-") || !file.EndsWith(".tgz"))
                {
                    return Results.Json(NpmError.NotFound, UnityNugetJsonSerializerContext.Default);
                }

                string? filePath = instance?.GetPackageFilePath(file);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return Results.Json(NpmError.NotFound, UnityNugetJsonSerializerContext.Default);
                }

                // This method can be called with HEAD request, so in that case we just calculate the content length
                if (httpContext.Request.Method.Equals(HttpMethod.Head.Method))
                {
                    httpContext.Response.ContentType = "application/octet-stream";
                    httpContext.Response.ContentLength = new FileInfo(filePath).Length;
                    return Results.Ok();
                }
                else
                {
                    return Results.File(filePath, "application/octet-stream", file);
                }
            });
        }

        private static void MapStatus(this IEndpointRouteBuilder builder)
        {
            const string text = @"
<!DOCTYPE HTML>
<html>
<head>
    <meta charset=""utf-8"" />
</head>
<body>
<h1>{{ message }}</h1>

{{ if !error_messages.empty? }}
    <h1>Error messages</h1>

    {{ for error_message in error_messages }}
<p>{{ error_message }}</p>
    {{ end }}
{{ end }}
{{ if !warning_messages.empty? }}
    <h1>Warning messages</h1>

    {{ for warning_message in warning_messages }}
<p>{{ warning_message }}</p>
    {{ end }}
{{ end }}
{{ if !information_messages.empty? }}
    <h1>Information messages</h1>

    {{ for information_message in information_messages }}
<p>{{ information_message }}</p>
    {{ end }}
{{ end }}
</body>
</html>
";
            builder.MapGet("/status", async context =>
            {
                RegistryCacheReport registryCacheReport = context.RequestServices.GetRequiredService<RegistryCacheReport>();

                string message;

                if (registryCacheReport.Running)
                {
                    message = $"The server is currently updating ({registryCacheReport.Progress:F1}% completed)...";
                }
                else
                {
                    message = $"Time remaining for the next update: {(registryCacheReport.TimeRemainingForNextUpdate != null ? registryCacheReport.TimeRemainingForNextUpdate.Value.ToString(@"hh\:mm\:ss") : string.Empty)}";
                }

                var model = new
                {
                    message,
                    informationMessages = registryCacheReport.InformationMeessages,
                    warningMessages = registryCacheReport.WarningMessages,
                    errorMessages = registryCacheReport.ErrorMessages
                };

                var template = Template
                    .Parse(text);

                var scriptObject = new ScriptObject();
                scriptObject.Import(model);

                TemplateContext templateContext = template.LexerOptions.Lang == ScriptLang.Liquid ? new LiquidTemplateContext() : new TemplateContext();
                templateContext.LoopLimit = 0;
                templateContext.PushGlobal(scriptObject);

                string output = Template
                    .Parse(text)
                    .Render(templateContext);
                await context.Response.WriteAsync(output);
            });
        }

        private static bool TryGetInstance(RegistryCacheSingleton registryCacheSingleton, RegistryCacheReport registryCacheReport, out RegistryCache? cacheInstance, out NpmError? npmError)
        {
            RegistryCache? instance = registryCacheSingleton.Instance;

            cacheInstance = instance;
            npmError = instance == null ? GetNpmError(registryCacheReport) : null;

            return instance != null;
        }

        private static NpmError GetNpmError(RegistryCacheReport registryCacheReport)
        {
            if (registryCacheReport.ErrorMessages.Any())
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Error initializing the server:");

                foreach (string error in registryCacheReport.ErrorMessages)
                {
                    stringBuilder.AppendLine(error);
                }

                return new NpmError("not_initialized", stringBuilder.ToString());
            }
            else
            {
                return new NpmError("not_initialized", $"The server is initializing ({registryCacheReport.Progress:F1}% completed). Please retry later...");
            }
        }
    }
}
