using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NuGet.Common.LogLevel;

namespace UnityNuGet.Server
{
    /// <summary>
    /// A NuGet logger redirecting to a <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// </summary>
    public class NuGetRedirectLogger : LoggerBase
    {
        private readonly ILogger _logger;

        public NuGetRedirectLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public override void Log(ILogMessage message)
        {
            LoggerExtensions.Log(_logger, GetLogLevel(message.Level), message.Message);
        }

        public override Task LogAsync(ILogMessage message)
        {
            LoggerExtensions.Log(_logger, GetLogLevel(message.Level), message.Message);
            return Task.CompletedTask;
        }

        private static Microsoft.Extensions.Logging.LogLevel GetLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    return Microsoft.Extensions.Logging.LogLevel.Debug;
                case LogLevel.Verbose:
                    return Microsoft.Extensions.Logging.LogLevel.Trace;
                case LogLevel.Information:
                    return Microsoft.Extensions.Logging.LogLevel.Information;
                case LogLevel.Minimal:
                    return Microsoft.Extensions.Logging.LogLevel.Information;
                case LogLevel.Warning:
                    return Microsoft.Extensions.Logging.LogLevel.Warning;
                case LogLevel.Error:
                    return Microsoft.Extensions.Logging.LogLevel.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }
}