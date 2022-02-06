using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class NativeTests
    {
        [Test]
        public async Task TestBuild()
        {
            var unityPackages = Path.Combine(Path.GetDirectoryName(typeof(RegistryCacheTests).Assembly.Location), "unity_packages");
            Directory.Delete(unityPackages, true);

            var registryCache = new RegistryCache(
                unityPackages,
                new Uri("http://localhost/"),
                "org.nuget",
                "2019.1",
                " (NuGet)",
                new RegistryTargetFramework[] {
                    new RegistryTargetFramework { Name = "netstandard2.1", DefineConstraints = new string[] { "UNITY_2021_2_OR_NEWER"} },
                    new RegistryTargetFramework { Name = "netstandard2.0", DefineConstraints = new string[] { "!UNITY_2021_2_OR_NEWER" } },
                },
                new NuGetConsoleLogger())
            {
                Filter = "rhino3dm"
            };

            await registryCache.Build();

            Assert.False(registryCache.HasErrors, "The registry failed to build, check the logs");
            var allResult = registryCache.All();
            var allResultJson = allResult.ToJson();

            StringAssert.Contains("org.nuget.rhino3dm", allResultJson);

            var rhinoPackage = registryCache.GetPackage("org.nuget.rhino3dm");
            Assert.NotNull(rhinoPackage);
            var rhinopackageJson = rhinoPackage.ToJson();
            StringAssert.Contains("org.nuget.rhino3dm", rhinopackageJson);
            StringAssert.Contains("7.11.0", rhinopackageJson);
        }
    }
}
