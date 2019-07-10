using System;
using Scriban;

namespace UnityNuGet
{
    /// <summary>
    /// Helper methods to create Unity .meta files
    /// </summary>
    internal static class UnityMeta
    {
        public static string GetMetaForExtension(Guid guid, string extension)
        {
            switch (extension)
            {
                case ".dll":
                    return GetMetaForDll(guid);
                
                case ".pdb":
                    break;

                case ".json":
                case ".xml":
                case ".txt":
                case ".md":
                    return GetMetaForText(guid);
            }

            return null;
        }

        private static string GetMetaForDll(Guid guid)
        {
            const string text = @"fileFormatVersion: 2
guid: {{ guid }}
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints:
  - NET_STANDARD_2_0
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      '': Any
    second:
      enabled: 0
      settings:
        Exclude Editor: 0
        Exclude Linux: 0
        Exclude Linux64: 0
        Exclude LinuxUniversal: 0
        Exclude OSXUniversal: 0
        Exclude Win: 0
        Exclude Win64: 0
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Facebook: Win
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  - first:
      Facebook: Win64
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  - first:
      Standalone: Linux
    second:
      enabled: 1
      settings:
        CPU: x86
  - first:
      Standalone: Linux64
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      Standalone: LinuxUniversal
    second:
      enabled: 1
      settings: {}
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
  - first:
      Standalone: Win
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
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
            var meta = Template.Parse(text);
            return meta.Render(new {guid = guid.ToString("N")});
        }

        private static string GetMetaForText(Guid guid)
        {
            const string text = @"fileFormatVersion: 2
guid: {{ guid }}
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
            var meta = Template.Parse(text);
            return meta.Render(new {guid = guid.ToString("N")});
        }
    }
}