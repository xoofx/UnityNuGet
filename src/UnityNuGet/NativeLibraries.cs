using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging;
using Scriban;

namespace UnityNuGet
{
    static class NativeLibraries
    {
        public static async IAsyncEnumerable<(string file, string[] folders, string platform, string architecture)> GetSupportedNativeLibsAsync(PackageReaderBase packageReader, ILogger logger)
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

                var platform = system[0][..3] switch
                {
                    "lin" => "Linux",
                    "osx" => "OSX",
                    "win" => "Windows",
                    _ => null
                };

                if (platform is null)
                {
                    logger.LogInformation($"Skipping file for unsupported platform: {file} ...");
                    continue;
                }

                var architecture = system[1] switch
                {
                    "x86" => "x86",
                    "x64" => "x86_64",
                    "arm64" => "ARM64",
                    _ => null
                };

                if (architecture is null)
                {
                    logger.LogInformation($"Skipping file for unsupported architecture: {file} ...");
                    continue;
                }

                yield return (file, folders, platform, architecture);
            }
        }

        private readonly struct PlatformEnables
        {
            public readonly int Editor, Linux64, OSXUniversal, Win, Win64;

            public PlatformEnables(int editor, int linux64, int oSXUniversal, int win, int win64)
            {
                Editor = editor;
                Linux64 = linux64;
                OSXUniversal = oSXUniversal;
                Win = win;
                Win64 = win64;
            }
        }

        public static string? GetMetaForNative(Guid guid, string platform, string architecture, string[] labels)
        {
            // TODO: Support other platforms
            PlatformEnables? enables = (platform, architecture) switch
            {
                ("Linux", _) => new(1, 1, 0, 0, 0),
                ("OSX", _) => new(1, 0, 1, 0, 0),
                ("Windows", "x86") => new(0, 0, 0, 1, 0),
                ("Windows", "x86_64") => new(1, 0, 0, 0, 1),
                _ => null,
            };

            if (enables is null) return null;

            const string text = @"{{ cpu(x) = x == 1 ? architecture : ""None"" }}fileFormatVersion: 2
guid: {{ guid }}
{{ labels }}PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: {{ 1 - enables.Editor }}
        Exclude Linux64: {{ 1 - enables.Linux64 }}
        Exclude OSXUniversal: {{ 1 - enables.OSXUniversal }}
        Exclude Win: {{ 1 - enables.Win }}
        Exclude Win64: {{ 1 - enables.Win64 }}
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: {{ enables.Editor }}
      settings:
        CPU: {{ cpu enables.Editor }}
        DefaultValueInitialized: true
        OS: {{ enables.Editor == 1 ? platform : ""None"" }}
  - first:
      Standalone: Linux64
    second:
      enabled: {{ enables.Linux64 }}
      settings:
        CPU: {{ cpu enables.Linux64 }}
  - first:
      Standalone: OSXUniversal
    second:
      enabled: {{ enables.OSXUniversal }}
      settings:
        CPU: {{ cpu enables.OSXUniversal }}
  - first:
      Standalone: Win
    second:
      enabled: {{ enables.Win }}
      settings:
        CPU: {{ cpu enables.Win }}
  - first:
      Standalone: Win64
    second:
      enabled: {{ enables.Win64 }}
      settings:
        CPU: {{ cpu enables.Win64 }}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";

            return Template
                .Parse(text)
                .Render(new
                {
                    guid = guid.ToString("N"),
                    enables,
                    platform,
                    architecture,
                    labels = labels.Length == 0
                    ? string.Empty
                    : $"labels:\n{string.Concat(labels.Select(l => $"  - {l}\n"))}",
                })
                .Replace("\r\n", "\n");
        }
    }
}
