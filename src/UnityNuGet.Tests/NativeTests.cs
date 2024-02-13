using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    [Ignore("Ignore native libs tests")]
    public class NativeTests
    {
        [Test]
        public async Task TestBuild()
        {
            var unityPackages = Path.Combine(Path.GetDirectoryName(typeof(RegistryCacheTests).Assembly.Location)!, "unity_packages");
            Directory.Delete(unityPackages, true);

            var errorsTriggered = false;

            var registryCache = new RegistryCache(
                unityPackages,
                new Uri("http://localhost/"),
                "org.nuget",
                "2019.1",
                " (NuGet)",
                [
                    new() { Name = "netstandard2.1", DefineConstraints = ["UNITY_2021_2_OR_NEWER"] },
                    new() { Name = "netstandard2.0", DefineConstraints = ["!UNITY_2021_2_OR_NEWER"] },
                ],
                new NuGetConsoleTestLogger())
            {
                Filter = "rhino3dm",
                OnError = message =>
                {
                    errorsTriggered = true;
                }
            };

            await registryCache.Build();

            Assert.That(errorsTriggered, Is.False, "The registry failed to build, check the logs");
            var allResult = registryCache.All();
            var allResultJson = allResult.ToJson();

            Assert.That(allResultJson, Does.Contain("org.nuget.rhino3dm"));

            var rhinoPackage = registryCache.GetPackage("org.nuget.rhino3dm");
            Assert.That(rhinoPackage, Is.Not.Null);
            var rhinopackageJson = rhinoPackage!.ToJson();
            Assert.That(rhinopackageJson, Does.Contain("org.nuget.rhino3dm"));
            Assert.That(rhinopackageJson, Does.Contain("7.11.0"));
        }
    }
}
