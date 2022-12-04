using System.Linq;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class RegistryTests
    {
        [Test]
        public void TestSort()
        {
            var registry = Registry.GetInstance();
            var originalPackageNames = registry.Select(r => r.Key).ToArray();
            var sortedPackageNames = originalPackageNames.OrderBy(p => p).ToArray();

            Assert.AreEqual(sortedPackageNames, originalPackageNames);
        }

        [Test]
        public void TestBuiltInPackages()
        {
            var registry = Registry.GetInstance();
            var packageNames = registry.Select(r => r.Key).Where(DotNetHelper.IsNetStandard20Assembly).ToArray();

            Assert.IsEmpty(packageNames);
        }
    }
}
