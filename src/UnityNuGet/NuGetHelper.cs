using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace UnityNuGet
{
    static partial class NuGetHelper
    {
        // https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support
        [GeneratedRegex(@"/roslyn(\d+)\.(\d+)\.?(\d*)/")]
        private static partial Regex RoslynVersion();

        // https://docs.unity3d.com/Manual/roslyn-analyzers.html
        private static readonly Version unityRoslynSupportedVersion = new(3, 8, 0);

        // https://github.com/dotnet/sdk/blob/2838d93742658300698b2194882d57fd978fb168/src/Tasks/Microsoft.NET.Build.Tasks/NuGetUtils.NuGet.cs#L50
        public static bool IsApplicableAnalyzer(string file) => IsApplicableAnalyzer(file, "C#");

        private static bool IsApplicableAnalyzer(string file, string projectLanguage)
        {
            // This logic is preserved from previous implementations.
            // See https://github.com/NuGet/Home/issues/6279#issuecomment-353696160 for possible issues with it.
            bool IsAnalyzer()
            {
                return file.StartsWith("analyzers", StringComparison.Ordinal)
                    && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase);
            }

            bool CS() => file.Contains("/cs/", StringComparison.OrdinalIgnoreCase);
            bool VB() => file.Contains("/vb/", StringComparison.OrdinalIgnoreCase);

            bool FileMatchesProjectLanguage()
            {
                return projectLanguage switch
                {
                    "C#" => CS() || !VB(),
                    "VB" => VB() || !CS(),
                    _ => false,
                };
            }

            return IsAnalyzer() && FileMatchesProjectLanguage();
        }

        public static bool IsApplicableAnalyzerResource(string file)
        {
            bool IsResource()
            {
                return file.StartsWith("analyzers", StringComparison.Ordinal)
                    && file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase);
            }

            bool CS() => file.Contains("/cs/", StringComparison.OrdinalIgnoreCase);
            bool VB() => file.Contains("/vb/", StringComparison.OrdinalIgnoreCase);

            // Czech locale is cs, catch /vb/cs/
            return IsResource() && ((!CS() && !VB()) || (CS() && !VB()));
        }

        public static bool IsApplicableUnitySupportedRoslynVersionFolder(string file)
        {
            var roslynVersionMatch = RoslynVersion().Match(file);

            bool hasRoslynVersionFolder = roslynVersionMatch.Success;
            bool hasUnitySupportedRoslynVersionFolder = hasRoslynVersionFolder &&
                                                        int.Parse(roslynVersionMatch.Groups[1].Value) == unityRoslynSupportedVersion.Major &&
                                                        int.Parse(roslynVersionMatch.Groups[2].Value) == unityRoslynSupportedVersion.Minor;

            return !hasRoslynVersionFolder || hasUnitySupportedRoslynVersionFolder;
        }

        public static IEnumerable<(FrameworkSpecificGroup, RegistryTargetFramework)> GetClosestFrameworkSpecificGroups(IEnumerable<FrameworkSpecificGroup> versions, IEnumerable<RegistryTargetFramework> targetFrameworks)
        {
            var result = new List<(FrameworkSpecificGroup, RegistryTargetFramework)>();

            foreach (var targetFramework in targetFrameworks)
            {
                var item = versions.Where(x => x.TargetFramework.Framework == targetFramework.Framework!.Framework && x.TargetFramework.Version <= targetFramework.Framework.Version).OrderByDescending(x => x.TargetFramework.Version)
                    .FirstOrDefault();

                if (item != null)
                {
                    result.Add((item, targetFramework));
                }
            }

            return result;
        }

        public static IEnumerable<PackageDependencyGroup> GetCompatiblePackageDependencyGroups(IEnumerable<PackageDependencyGroup> packageDependencyGroups, IEnumerable<RegistryTargetFramework> targetFrameworks, bool includeAny = true)
        {
            return packageDependencyGroups.Where(dependencySet => (includeAny && dependencySet.TargetFramework.IsAny) || targetFrameworks.Any(targetFramework => dependencySet.TargetFramework == targetFramework.Framework)).ToList();
        }

        public static PackageIdentity? GetMinimumCompatiblePackageIdentity(IEnumerable<IPackageSearchMetadata> packageSearchMetadataIt, IEnumerable<RegistryTargetFramework> targetFrameworks, bool includeAny = true)
        {
            foreach (var packageSearchMetadata in packageSearchMetadataIt)
            {
                var dependencyResolvedDependencyGroups = GetCompatiblePackageDependencyGroups(packageSearchMetadata.DependencySets, targetFrameworks, includeAny);

                if (dependencyResolvedDependencyGroups.Any())
                {
                    return packageSearchMetadata.Identity;
                }
            }

            return null;
        }
    }
}
