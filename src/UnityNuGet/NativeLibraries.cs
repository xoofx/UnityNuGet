using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using NuGet.Packaging;
using NuGet.Common;
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
                var folderPath = Path.GetDirectoryName(file);
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

        public static string GetMetaForNative(Guid guid, string platform, string architecture, string[] labels)
        {
            // (Editor, Linux64, OSXUniversal, Win, Win64)
            var enables = (platform, architecture) switch
            {
                ("Linux", _) => (1, 1, 0, 0, 0),
                ("OSX", _) => (1, 0, 1, 0, 0),
                ("Windows", "x86") => (0, 0, 0, 1, 0),
                ("Windows", "x86_64") => (1, 0, 0, 0, 1),
                _ => throw new ArgumentException("Unsupported configuration")
            };

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
        Exclude Editor: {{ 1 - enables.item1 }}
        Exclude Linux64: {{ 1 - enables.item2 }}
        Exclude OSXUniversal: {{ 1 - enables.item3 }}
        Exclude Win: {{ 1 - enables.item4 }}
        Exclude Win64: {{ 1 - enables.item5 }}
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: {{ enables.item1 }}
      settings:
        CPU: {{ cpu enables.item1 }}
        DefaultValueInitialized: true
        OS: {{ enables.item1 == 1 ? platform : ""None"" }}
  - first:
      Standalone: Linux64
    second:
      enabled: {{ enables.item2 }}
      settings:
        CPU: {{ cpu enables.item2 }}
  - first:
      Standalone: OSXUniversal
    second:
      enabled: {{ enables.item3 }}
      settings:
        CPU: {{ cpu enables.item3 }}
  - first:
      Standalone: Win
    second:
      enabled: {{ enables.item4 }}
      settings:
        CPU: {{ cpu enables.item4 }}
  - first:
      Standalone: Win64
    second:
      enabled: {{ enables.item5 }}
      settings:
        CPU: {{ cpu enables.item5 }}
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
