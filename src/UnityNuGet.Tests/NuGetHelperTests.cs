using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NUnit.Framework;
using static NuGet.Frameworks.FrameworkConstants;

namespace UnityNuGet.Tests
{
    public class NuGetHelperTests
    {
        [Test]
        public void GetCompatiblePackageDependencyGroups_SpecificSingleFramework()
        {
            IList<PackageDependencyGroup> packageDependencyGroups = new PackageDependencyGroup[]
            {
                new PackageDependencyGroup(CommonFrameworks.NetStandard13, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard16, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard20, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard21, Array.Empty<PackageDependency>())
            };

            IEnumerable<RegistryTargetFramework> targetFrameworks = new RegistryTargetFramework[] { new RegistryTargetFramework { Framework = CommonFrameworks.NetStandard20 } };

            IEnumerable<PackageDependencyGroup> compatibleDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(packageDependencyGroups, targetFrameworks);

            CollectionAssert.AreEqual(new PackageDependencyGroup[] { packageDependencyGroups[2] }, compatibleDependencyGroups);
        }

        [Test]
        public void GetCompatiblePackageDependencyGroups_SpecificMultipleFrameworks()
        {
            IList<PackageDependencyGroup> packageDependencyGroups = new PackageDependencyGroup[]
            {
                new PackageDependencyGroup(CommonFrameworks.NetStandard13, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard16, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard20, Array.Empty<PackageDependency>()),
                new PackageDependencyGroup(CommonFrameworks.NetStandard21, Array.Empty<PackageDependency>())
            };

            IEnumerable<RegistryTargetFramework> targetFrameworks = new RegistryTargetFramework[] { new RegistryTargetFramework { Framework = CommonFrameworks.NetStandard20 }, new RegistryTargetFramework { Framework = CommonFrameworks.NetStandard21 } };

            IEnumerable<PackageDependencyGroup> compatibleDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(packageDependencyGroups, targetFrameworks);

            CollectionAssert.AreEqual(new PackageDependencyGroup[] { packageDependencyGroups[2], packageDependencyGroups[3] }, compatibleDependencyGroups);
        }

        [Test]
        public void GetCompatiblePackageDependencyGroups_AnyFramework()
        {
            IList<PackageDependencyGroup> packageDependencyGroups = new PackageDependencyGroup[]
            {
                new PackageDependencyGroup(new NuGetFramework(SpecialIdentifiers.Any), Array.Empty<PackageDependency>())
            };

            IEnumerable<RegistryTargetFramework> targetFrameworks = new RegistryTargetFramework[] { new RegistryTargetFramework { Framework = CommonFrameworks.NetStandard20 } };

            IEnumerable<PackageDependencyGroup> compatibleDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(packageDependencyGroups, targetFrameworks);

            CollectionAssert.AreEqual(packageDependencyGroups, compatibleDependencyGroups);
        }
    }
}
