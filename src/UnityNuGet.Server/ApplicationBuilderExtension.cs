using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace UnityNuGet.Server
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Logs all request and response headers (used only in development)
        /// </summary>
        public static void LogRequestHeaders(this IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger("Request Headers");
            app.Use(async (context, next) =>
            {
                var builder = new StringBuilder(Environment.NewLine);
                foreach (KeyValuePair<string, StringValues> header in context.Request.Headers)
                {
                    builder.AppendLine($"{header.Key}:{header.Value}");
                }
                logger.LogInformation("Request: {Request}", builder.ToString());
                await next.Invoke();

                builder.Length = 0;
                builder.AppendLine();
                foreach (KeyValuePair<string, StringValues> header in context.Response.Headers)
                {
                    builder.AppendLine($"{header.Key}:{header.Value}");
                }
                logger.LogInformation("Response: {Response}", builder.ToString());
            });
        }
    }
}
