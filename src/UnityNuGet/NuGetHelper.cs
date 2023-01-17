using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace UnityNuGet
{
    static class NuGetHelper
    {
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
