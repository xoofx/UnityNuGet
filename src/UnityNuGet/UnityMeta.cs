using System;
using System.Collections.Generic;
using System.Linq;
using Scriban;

namespace UnityNuGet
{
    /// <summary>
    /// Helper methods to create Unity .meta files
    /// </summary>
    internal static class UnityMeta
    {
        public static string? GetMetaForExtension(Guid guid, string extension)
        {
            switch (extension)
            {
                case ".pdb":
                    break;
                case ".asmdef":
                case ".cs":
                case ".json":
                case ".md":
                case ".txt":
                case ".xml":
                    return GetMetaForText(guid);
            }

            return null;
        }

        public static string GetMetaForDll(
            Guid guid,
            PlatformDefinition platformDef,
            IEnumerable<string> labels,
            IEnumerable<string> defineConstraints)
        {
            const string text = @"fileFormatVersion: 2
guid: {{ guid }}
{{ labels }}PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
{{ constraints }}  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:{{exclude_platforms}}
  - first:
      Any:
    second:
      enabled: {{ all_enabled }}
      settings: {}{{ per_platform_settings }}
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";

            var allLabels = labels.ToList();
            var allConstraints = defineConstraints.ToList();

            static string FormatList(IEnumerable<string> items) => string.Join(
                string.Empty,
                items.Select(d => $"  - {d}\n"));

            string excludePlatforms = string.Empty;
            string perPlatformSettings = @"
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true";

            // Render the per-platform settings
            if (platformDef.Os != UnityOs.AnyOs)
            {
                // Determine which configurations are enabled
                var platWin = platformDef.Find(UnityOs.Windows, UnityCpu.X86);
                var platWin64 = platformDef.Find(UnityOs.Windows, UnityCpu.X64);
                var platLinux64 = platformDef.Find(UnityOs.Linux, UnityCpu.X64);
                var platOsx = platformDef.Find(UnityOs.OSX);
                var platAndroid = platformDef.Find(UnityOs.Android);
                var platWasm = platformDef.Find(UnityOs.WebGL);
                var platIos = platformDef.Find(UnityOs.iOS);
                var platEditor = platformDef.FindEditor();

                var dict = new
                {
                    enablesWin = (platWin != null) ? 1 : 0,
                    enablesWin64 = (platWin64 != null) ? 1 : 0,
                    enablesLinux64 = (platLinux64 != null) ? 1 : 0,
                    enablesOsx = (platOsx != null) ? 1 : 0,
                    enablesAndroid = (platAndroid != null) ? 1 : 0,
                    enablesWasm = (platWasm != null) ? 1 : 0,
                    enablesIos = (platIos != null) ? 1 : 0,
                    enablesEditor = (platEditor != null) ? 1 : 0,

                    cpuWin = (platWin?.Cpu ?? UnityCpu.None).GetName(),
                    cpuWin64 = (platWin64?.Cpu ?? UnityCpu.None).GetName(),
                    cpuLinux64 = (platLinux64?.Cpu ?? UnityCpu.None).GetName(),
                    cpuOsx = (platOsx?.Cpu ?? UnityCpu.None).GetName(),
                    cpuAndroid = (platAndroid?.Cpu ?? UnityCpu.None).GetName(),
                    cpuIos = (platIos?.Cpu ?? UnityCpu.None).GetName(),
                    cpuEditor = (platEditor?.Cpu ?? UnityCpu.None).GetName(),

                    osEditor = (platEditor?.Os ?? UnityOs.AnyOs).GetName(),
                };

                const string excludePlatformsText = @"
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Android: {{ 1 - enables_android }}
        Exclude Editor: {{ 1 - enables_editor }}
        Exclude Linux64: {{ 1 - enables_linux64 }}
        Exclude OSXUniversal: {{ 1 - enables_osx }}
        Exclude WebGL: {{ 1 - enables_wasm }}
        Exclude Win: {{ 1 - enables_win }}
        Exclude Win64: {{ 1 - enables_win64 }}
        Exclude iOS: {{ 1 - enables_ios }}";

                const string perPlatformSettingsText = @"
  - first:
      Android: Android
    second:
      enabled: {{ enables_android }}
      settings:
        CPU: {{ cpu_android }}
  - first:
      Editor: Editor
    second:
      enabled: {{ enables_editor }}
      settings:
        CPU: {{ cpu_editor }}
        DefaultValueInitialized: true
        OS: {{ os_editor }}
  - first:
      Standalone: Linux64
    second:
      enabled: {{ enables_linux64 }}
      settings:
        CPU: {{ cpu_linux64 }}
  - first:
      Standalone: OSXUniversal
    second:
      enabled: {{ enables_osx }}
      settings:
        CPU: {{ cpu_osx }}
  - first:
      Standalone: Win
    second:
      enabled: {{ enables_win }}
      settings:
        CPU: {{ cpu_win }}
  - first:
      Standalone: Win64
    second:
      enabled: {{ enables_win64 }}
      settings:
        CPU: {{ cpu_win64 }}
  - first:
      WebGL: WebGL
    second:
      enabled: {{ enables_wasm }}
      settings: {}
  - first:
      iPhone: iOS
    second:
      enabled: {{ enables_ios }}
      settings:
        AddToEmbeddedBinaries: false
        CPU: {{ cpu_ios }}
        CompileFlags: 
        FrameworkDependencies: ";

                excludePlatforms = Template
                    .Parse(excludePlatformsText)
                    .Render(dict);

                perPlatformSettings = Template
                    .Parse(perPlatformSettingsText)
                    .Render(dict);
            }

            bool allPlatformsEnabled = (platformDef.Os == UnityOs.AnyOs) && (platformDef.Cpu == UnityCpu.AnyCpu);

            return Template
                .Parse(text)
                .Render(new
                {
                    excludePlatforms,
                    perPlatformSettings,
                    guid = guid.ToString("N"),
                    allEnabled = allPlatformsEnabled ? "1" : "0",
                    labels = allLabels.Count == 0
                        ? string.Empty
                        : $"labels:\n{FormatList(allLabels)}",
                    constraints = allConstraints.Count == 0
                        ? string.Empty
                        : $"  defineConstraints:\n{FormatList(allConstraints)}"
                })
                .StripWindowsNewlines();
        }

        public static string GetMetaForFolder(Guid guid)
        {
            return $@"fileFormatVersion: 2
guid: {guid:N}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant:
".StripWindowsNewlines();
        }

        private static string GetMetaForText(Guid guid)
        {
            return $@"fileFormatVersion: 2
guid: {guid:N}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
".StripWindowsNewlines();
        }

        private static string StripWindowsNewlines(this string input) => input.Replace("\r\n", "\n");
    }
}
