using System.Threading.Tasks;
using NuGet.Common;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class NuGetConsoleTestLogger : LoggerBase
    {
        public override void Log(ILogMessage message)
        {
            if (message.Level == LogLevel.Error)
            {
                TestContext.Error.WriteLine(message);
            }
            else
            {
                TestContext.Progress.WriteLine(message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}
