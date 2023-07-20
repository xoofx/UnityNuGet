using System;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class UnityMetaTests
    {
        [Test]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithoutConstraints()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs, Array.Empty<string>(), Array.Empty<string>());
            StringAssert.DoesNotContain("defineConstraints", output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\n  isPreloaded: 0\n", output);
        }

        [Test]
        public void GetMetaForDll_FormatsLabelsProperly_WithoutLabels()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs, Array.Empty<string>(), Array.Empty<string>());
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
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs, Array.Empty<string>(), constraints);

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
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs, labels, Array.Empty<string>());

            StringAssert.Contains(expected, output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\nPluginImporter:\n", output);
        }

        [TestCase(true, "1")]
        [TestCase(false, "0")]
        public void GetMetaForDll_FormatsAnyPlatformEnabledProperly(bool value, string expected)
        {
            PlatformDefinition platformDef;

            if (value)
            {
                var platformDefs = PlatformDefinition.CreateAllPlatforms();
                platformDef = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            }
            else
            {
                platformDef = new PlatformDefinition(UnityOs.AnyOs, UnityCpu.None, isEditorConfig: false);
            }

            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), platformDef, Array.Empty<string>(), Array.Empty<string>());

            StringAssert.Contains($"\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: {expected}\n", output);
        }

        [Test]
        public void GetMetaForDll_ContainsNoWindowsNewlines()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var anyOs = platformDefs.Find(UnityOs.AnyOs, UnityCpu.AnyCpu);
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), anyOs, Array.Empty<string>(), new[] { "TEST" });
            StringAssert.DoesNotContain("\r", output);
        }
    }
}
