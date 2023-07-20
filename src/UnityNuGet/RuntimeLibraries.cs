using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace UnityNuGet
{
    static class RuntimeLibraries
    {
        public static async IAsyncEnumerable<(string file, string[] folders, UnityOs, UnityCpu)> GetSupportedRuntimeLibsAsync(
            PackageReaderBase packageReader,
            NuGetFramework targetFramework,
            ILogger logger)
        {
            var versions = await packageReader.GetItemsAsync(PackagingConstants.Folders.Runtimes, CancellationToken.None);
            var files = versions.SelectMany(v => v.Items);

            foreach (var file in files)
            {
                var folderPath = Path.GetDirectoryName(file)!;
                var folders = folderPath.Split(Path.DirectorySeparatorChar);

                if (folders.Length != 4 || !folders[2].Equals("lib", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation($"Skipping native library file located in the runtimes folder: {file} ...");
                    continue;
                }

                var framework = NuGetFramework.Parse(folders[3]);
                if (framework != targetFramework)
                {
                    logger.LogInformation($"Skipping runtime library targeting other frameworks: {file} ...");
                    continue;
                }

                var system = folders[1].Split('-');

                if (system.Length < 1)
                {
                    logger.LogInformation($"Skipping file located in the runtime folder that does not specify platform: {file} ...");
                    continue;
                }

                UnityOs? os = system[0][..3] switch
                {
                    "lin" => UnityOs.Linux,
                    "osx" => UnityOs.OSX,
                    "win" => UnityOs.Windows,
                    _ => null
                };

                if (os is null)
                {
                    logger.LogInformation($"Skipping runtime library for unsupported OS: {file} ...");
                    continue;
                }

                UnityCpu? cpu = UnityCpu.AnyCpu;
                if (system.Length > 1)
                {
                    cpu = system[1] switch
                    {
                        "x86" => UnityCpu.X86,
                        "x64" => UnityCpu.X64,
                        "arm64" => UnityCpu.ARM64,
                        _ => null
                    };

                    if (cpu is null)
                    {
                        logger.LogInformation($"Skipping runtime library for unsupported CPU: {file} ...");
                        continue;
                    }
                }

                yield return (file, folders, os.Value, cpu.Value);
            }
        }
    }
}
