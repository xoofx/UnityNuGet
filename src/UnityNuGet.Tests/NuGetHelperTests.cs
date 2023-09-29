﻿using System;
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
        // Examples:
        // Meziantou.Analyzer -> analyzers/dotnet/roslyn3.8/cs/*
        // Microsoft.Unity.Analyzers -> analyzers/dotnet/cs/*
        // Microsoft.VisualStudio.Threading.Analyzers -> analyzers/cs/*
        // SonarAnalyzer.CSharp -> analyzers/*
        // StrongInject -> analyzers/dotnet/cs/* + analyzers/dotnet/roslyn3.8/cs/*
        [Test]
        [TestCase("analyzers/dotnet/roslyn3.8/cs/Test.dll")]
        [TestCase("analyzers/dotnet/roslyn3.8/Test.dll")]
        [TestCase("analyzers/dotnet/cs/Test.dll")]
        [TestCase("analyzers/dotnet/Test.dll")]
        [TestCase("analyzers/Test.dll")]
        public void IsApplicableUnityAnalyzer_Valid(string input)
        {
            Assert.True(NuGetHelper.IsApplicableUnityAnalyzer(input));
        }

        [Test]
        [TestCase("analyzers/dotnet/roslyn4.0/Test.dll")]
        [TestCase("analyzers/dotnet/vb/Test.dll")]
        [TestCase("analyzers/Test.resources.dll")]
        public void IsApplicableUnityAnalyzer_Invalid(string input)
        {
            Assert.False(NuGetHelper.IsApplicableUnityAnalyzer(input));
        }

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
