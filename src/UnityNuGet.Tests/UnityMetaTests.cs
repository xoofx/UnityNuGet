using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
#pragma warning disable CA1861 // Avoid constant arrays as arguments
    public class UnityMetaTests
    {
        [Test]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithoutConstraints()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), Array.Empty<string>());
            Assert.That(output, Does.Not.Contain("defineConstraints"));

            // This is on the same line in the template, so ensure it's intact
            Assert.That(output, Does.Contain("\n  isPreloaded: 0\n"));
        }

        [Test]
        public void GetMetaForDll_FormatsLabelsProperly_WithoutLabels()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), Array.Empty<string>());
            Assert.That(output, Does.Not.Contain("labels"));

            // This is on the same line in the template, so ensure it's intact
            Assert.That(output, Does.Contain("\nPluginImporter:\n"));
        }

        [TestCase(new[] { "FIRST" }, "\n  defineConstraints:\n  - FIRST\n")]
        [TestCase(new[] { "FIRST", "SECOND" }, "\n  defineConstraints:\n  - FIRST\n  - SECOND\n")]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithConstraints(
            string[] constraints, string expected)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), constraints);

            Assert.That(output, Does.Contain(expected));

            // This is on the same line in the template, so ensure it's intact
            Assert.That(output, Does.Contain("\n  isPreloaded: 0\n"));
        }

        [TestCase(new[] { "FIRST" }, "\nlabels:\n  - FIRST\n")]
        [TestCase(new[] { "FIRST", "SECOND" }, "\nlabels:\n  - FIRST\n  - SECOND\n")]
        public void GetMetaForDll_FormatsLabelsProperly_WithLabels(
            string[] labels, string expected)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, labels, Array.Empty<string>());

            Assert.That(output, Does.Contain(expected));

            // This is on the same line in the template, so ensure it's intact
            Assert.That(output, Does.Contain("\nPluginImporter:\n"));
        }

        [TestCase(true, "1")]
        [TestCase(false, "0")]
        public void GetMetaForDll_FormatsAnyPlatformEnabledProperly(bool value, string expected)
        {
            PlatformDefinition? platformDef;

            if (value)
            {
                var platformDefs = PlatformDefinition.CreateAllPlatforms();
                platformDef = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            }
            else
            {
                platformDef = new PlatformDefinition(UnityOs.AnyOs, UnityCpu.None, isEditorConfig: false);
            }

            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), platformDef!, Array.Empty<string>(), Array.Empty<string>());

            Assert.That(output, Does.Contain($"\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: {expected}\n"));
        }

        [Test]
        public void GetMetaForDll_ContainsNoWindowsNewlines()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), new[] { "TEST" });
            Assert.That(output, Does.Not.Contain("\r"));
        }

        [TestCase(UnityOs.Android, "Android", "Android")]
        [TestCase(UnityOs.WebGL, "WebGL", "WebGL")]
        [TestCase(UnityOs.iOS, "iPhone", "iOS")]
        public void GetMetaForDll_NonEditor(UnityOs os, string platformName, string osName)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var output = UnityMeta.GetMetaForDll(
                Guid.NewGuid(),
                platformDefs.Find(os)!,
                Array.Empty<string>(),
                Array.Empty<string>());

            // There should be a single 'Exclude Android: 0' match
            var excludeRegex = new Regex("Exclude (.*): 0");
            var excludeMatches = excludeRegex.Matches(output);
            Assert.That(excludeMatches, Is.Not.Null);
            Assert.That(excludeMatches, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(excludeMatches.Single().Groups, Has.Count.EqualTo(2));
                Assert.That(osName, Is.EqualTo(excludeMatches.Single().Groups[1].Value));
            });

            // There should be a single 'enabled: 1' match
            var enableRegex = new Regex("enabled: 1");
            var enableMatches = enableRegex.Matches(output);
            Assert.That(enableMatches, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(enableMatches, Has.Count.EqualTo(1));

                Assert.That(output, Does.Contain($"- first:\n      {platformName}: {osName}\n    second:\n      enabled: 1\n"));
            });
        }

        [TestCase(UnityOs.Windows, new[] { "Win", "Win64" })]
        [TestCase(UnityOs.Linux, new[] { "Linux64" })]
        [TestCase(UnityOs.OSX, new[] { "OSXUniversal" })]
        public void GetMetaForDll_Editor(UnityOs os, string[] osNames)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var pDef = platformDefs.Find(os);
            var output = UnityMeta.GetMetaForDll(
                Guid.NewGuid(),
                pDef!,
                Array.Empty<string>(),
                Array.Empty<string>());

            // There should be only 'Exclude Editor: 0' and 'Exclude {{ osName }}: 0' matches
            var excludeRegex = new Regex("Exclude (.*): 0");
            var excludeMatches = excludeRegex.Matches(output);
            Assert.That(excludeMatches, Is.Not.Null);
            var actualExcludes = excludeMatches
                .Select(match => match.Groups[1].Value)
                .ToHashSet();

            var expectedExcludes = osNames
                .Append("Editor")
                .ToHashSet();
            Assert.That(actualExcludes.SetEquals(expectedExcludes), Is.True);

            // There should be as many 'enabled: 1' matches as exclude matches
            var enableRegex = new Regex("enabled: 1");
            var enableMatches = enableRegex.Matches(output);
            Assert.Multiple(() =>
            {
                Assert.That(enableMatches, Is.Not.Null);
                Assert.That(excludeMatches, Has.Count.EqualTo(enableMatches.Count));
            });

            foreach (var osName in actualExcludes)
            {
                var platformName = (osName == "Editor") ? osName : "Standalone";
                Assert.That(output, Does.Contain($"- first:\n      {platformName}: {osName}\n    second:\n      enabled: 1\n"));
            }
        }
    }
#pragma warning restore CA1861 // Avoid constant arrays as arguments
}
