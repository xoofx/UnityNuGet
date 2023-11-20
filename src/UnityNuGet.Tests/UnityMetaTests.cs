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
            StringAssert.DoesNotContain("defineConstraints", output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\n  isPreloaded: 0\n", output);
        }

        [Test]
        public void GetMetaForDll_FormatsLabelsProperly_WithoutLabels()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), Array.Empty<string>());
            StringAssert.DoesNotContain("labels", output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\nPluginImporter:\n", output);
        }

        [TestCase(new[] { "FIRST" }, "\n  defineConstraints:\n  - FIRST\n")]
        [TestCase(new[] { "FIRST", "SECOND" }, "\n  defineConstraints:\n  - FIRST\n  - SECOND\n")]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithConstraints(
            string[] constraints, string expected)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), constraints);

            StringAssert.Contains(expected, output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\n  isPreloaded: 0\n", output);
        }

        [TestCase(new[] { "FIRST" }, "\nlabels:\n  - FIRST\n")]
        [TestCase(new[] { "FIRST", "SECOND" }, "\nlabels:\n  - FIRST\n  - SECOND\n")]
        public void GetMetaForDll_FormatsLabelsProperly_WithLabels(
            string[] labels, string expected)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, labels, Array.Empty<string>());

            StringAssert.Contains(expected, output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\nPluginImporter:\n", output);
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

            StringAssert.Contains($"\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: {expected}\n", output);
        }

        [Test]
        public void GetMetaForDll_ContainsNoWindowsNewlines()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs!, Array.Empty<string>(), new[] { "TEST" });
            StringAssert.DoesNotContain("\r", output);
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
            Assert.IsNotNull(excludeMatches);
            Assert.AreEqual(excludeMatches.Count, 1);
            Assert.AreEqual(excludeMatches.Single().Groups.Count, 2);
            Assert.AreEqual(excludeMatches.Single().Groups[1].Value, osName);

            // There should be a single 'enabled: 1' match
            var enableRegex = new Regex("enabled: 1");
            var enableMatches = enableRegex.Matches(output);
            Assert.IsNotNull(enableMatches);
            Assert.AreEqual(enableMatches.Count, 1);

            StringAssert.Contains($"- first:\n      {platformName}: {osName}\n    second:\n      enabled: 1\n", output);
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
            Assert.IsNotNull(excludeMatches);
            var actualExcludes = excludeMatches
                .Select(match => match.Groups[1].Value)
                .ToHashSet();

            var expectedExcludes = osNames
                .Append("Editor")
                .ToHashSet();
            Assert.IsTrue(actualExcludes.SetEquals(expectedExcludes));

            // There should be as many 'enabled: 1' matches as exclude matches
            var enableRegex = new Regex("enabled: 1");
            var enableMatches = enableRegex.Matches(output);
            Assert.IsNotNull(enableMatches);
            Assert.AreEqual(enableMatches.Count, excludeMatches.Count);

            foreach (var osName in actualExcludes)
            {
                var platformName = (osName == "Editor") ? osName : "Standalone";
                StringAssert.Contains($"- first:\n      {platformName}: {osName}\n    second:\n      enabled: 1\n", output);
            }
        }
    }
#pragma warning restore CA1861 // Avoid constant arrays as arguments
}
