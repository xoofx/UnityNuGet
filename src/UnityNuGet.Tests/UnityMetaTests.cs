using System;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class UnityMetaTests
    {
        [Test]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithoutConstraints()
        {
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), Array.Empty<string>());
            StringAssert.DoesNotContain("defineConstraints", output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\n  isPreloaded: 0\n", output);
        }

        [TestCase(new[] { "FIRST" }, "\n  defineConstraints:\n  - FIRST\n")]
        [TestCase(new[] { "FIRST", "SECOND" }, "\n  defineConstraints:\n  - FIRST\n  - SECOND\n")]
        public void GetMetaForDll_FormatsDefineConstraintsProperly_WithConstraints(
            string[] constraints, string expected)
        {
            var output = UnityMeta.GetMetaForDll(Guid.NewGuid(), constraints);

            StringAssert.Contains(expected, output);

            // This is on the same line in the template, so ensure it's intact
            StringAssert.Contains("\n  isPreloaded: 0\n", output);
        }
    }
}
