using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class RegistryCacheTests
    {
        [Test]
        public void TestRegistry()
        {
            var registry = Registry.GetInstance();
            Assert.GreaterOrEqual(3, registry.Count);
            Assert.True(registry.ContainsKey("Scriban"));
        }
        
        [Test]
        public async Task TestBuild()
        {
            var unityPackages = Path.Combine(Path.GetDirectoryName(typeof(RegistryCacheTests).Assembly.Location), "unity_packages");
            var registryCache = new RegistryCache(unityPackages, "http://localhost");

            await registryCache.Build();
            
            Assert.False(registryCache.HasErrors, "The registry failed to build, check the logs");
            
            var allResult = registryCache.All();
            Assert.GreaterOrEqual(3, allResult.Packages.Count);
            var allResultJson = allResult.ToJson();
            
            StringAssert.Contains("nuget.Scriban", allResultJson);
            StringAssert.Contains("nuget.System.Runtime.CompilerServices.Unsafe", allResultJson);

            var scribanPackage = registryCache.GetPackage("nuget.Scriban");
            Assert.NotNull(scribanPackage);
            var scribanPackageJson = scribanPackage.ToJson();
            StringAssert.Contains("nuget.Scriban", scribanPackageJson);
            StringAssert.Contains("2.1.0", scribanPackageJson);
        }
    }
}