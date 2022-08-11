using System;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace UnityNuGet.Server
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Logs all request and response headers (used only in development)
        /// </summary>
        public static void LogRequestHeaders(this IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger("Request Headers");
            app.Use(async (context, next) =>
            {
                var builder = new StringBuilder(Environment.NewLine);
                foreach (var header in context.Request.Headers)
                {
                    builder.AppendLine($"{header.Key}:{header.Value}");
                }
                logger.LogInformation("Request: {Request}", builder.ToString());
                await next.Invoke();

                builder.Length = 0;
                builder.AppendLine();
                foreach (var header in context.Response.Headers)
                {
                    builder.AppendLine($"{header.Key}:{header.Value}");
                }
                logger.LogInformation("Response: {Response}", builder.ToString());
            });
        }
    }
}
