using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging;

namespace UnityNuGet
{
    static class NativeLibraries
    {
        public static async IAsyncEnumerable<(string file, string[] folders, UnityOs os, UnityCpu cpu)> GetSupportedNativeLibsAsync(
            PackageReaderBase packageReader,
            ILogger logger)
        {
            var versions = await packageReader.GetItemsAsync(PackagingConstants.Folders.Runtimes, CancellationToken.None);
            var files = versions.SelectMany(v => v.Items);

            foreach (var file in files)
            {
                var folderPath = Path.GetDirectoryName(file)!;
                var folders = folderPath.Split(Path.DirectorySeparatorChar);

                if (folders.Length != 3 || !folders[2].Equals("native", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation($"Skipping non-native library file located in the runtimes folder: {file} ...");
                    continue;
                }

                var system = folders[1].Split('-');

                if (system.Length != 2)
                {
                    logger.LogInformation($"Skipping file located in the runtime folder that does not specify platform and architecture: {file} ...");
                    continue;
                }

                UnityOs? os = system[0][..3] switch
                {
                    "lin" => UnityOs.Linux,
                    "osx" => UnityOs.OSX,
                    "win" => UnityOs.Windows,
                    "ios" => UnityOs.iOS,
                    _ => null
                };

                if (os is null)
                {
                    logger.LogInformation($"Skipping file for unsupported OS: {file} ...");
                    continue;
                }

                UnityCpu? cpu = system[1] switch
                {
                    "x86" => UnityCpu.X86,
                    "x64" => UnityCpu.X64,
                    "arm64" => UnityCpu.ARM64,
                    _ => null
                };

                if (cpu is null)
                {
                    logger.LogInformation($"Skipping file for unsupported CPU: {file} ...");
                    continue;
                }

                yield return (file, folders, os.Value, cpu.Value);
            }
        }
    }
}
