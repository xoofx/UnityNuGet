using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using UnityNuGet.Server;

namespace Microsoft.AspNetCore.Builder
{
    public static class EndpointRouteBuilderExtensions
    {
        public static void MapStatus(this IEndpointRouteBuilder builder)
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
    }
}
