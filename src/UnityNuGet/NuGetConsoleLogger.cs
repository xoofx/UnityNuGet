using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace UnityNuGet
{
    /// <summary>
    /// A default NuGet console logger.
    /// </summary>
    public class NuGetConsoleLogger : LoggerBase
    {
        public override void Log(ILogMessage message)
        {
            if (message.Level == LogLevel.Error)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}