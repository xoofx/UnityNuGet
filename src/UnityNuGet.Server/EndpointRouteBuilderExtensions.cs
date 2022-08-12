using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scriban;
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
                var registryCacheReport = context.RequestServices.GetRequiredService<RegistryCacheReport>();

                string message;

                if (registryCacheReport.Running)
                {
                    message = $"The server is currently updating ({registryCacheReport.Progress:F1}% completed)...";
                }
                else
                {
                    message = $"Time remaining for the next update: {(registryCacheReport.TimeRemainingForNextUpdate != null ? registryCacheReport.TimeRemainingForNextUpdate.Value.ToString(@"hh\:mm\:ss") : string.Empty)}";
                }

                string output = Template
                    .Parse(text)
                    .Render(new
                    {
                        message,
                        informationMessages = registryCacheReport.InformationMeessages,
                        warningMessages = registryCacheReport.WarningMessages,
                        errorMessages = registryCacheReport.ErrorMessages
                    });
                await context.Response.WriteAsync(output);
            });
        }
    }
}
