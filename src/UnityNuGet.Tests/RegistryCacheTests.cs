using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class RegistryCacheTests
    {
        [Test]
        public async Task TestBuild()
        {
            var errorsTriggered = false;

            var unityPackages = Path.Combine(Path.GetDirectoryName(typeof(RegistryCacheTests).Assembly.Location)!, "unity_packages");
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
                OnError = message =>
                {
                    errorsTriggered = true;
                }
            };

            // Uncomment when testing locally
            // registryCache.Filter = "scriban|bcl\\.asyncinterfaces|compilerservices\\.unsafe";

            await registryCache.Build();

            Assert.That(errorsTriggered, Is.False, "The registry failed to build, check the logs");

            var allResult = registryCache.All();
            Assert.That(allResult.Packages, Has.Count.GreaterThanOrEqualTo(3));
            var allResultJson = allResult.ToJson();

            Assert.That(allResultJson, Does.Contain("org.nuget.scriban"));
            Assert.That(allResultJson, Does.Contain("org.nuget.system.runtime.compilerservices.unsafe"));

            var scribanPackage = registryCache.GetPackage("org.nuget.scriban");
            Assert.That(scribanPackage, Is.Not.Null);
            var scribanPackageJson = scribanPackage!.ToJson();
            Assert.That(scribanPackageJson, Does.Contain("org.nuget.scriban"));
            Assert.That(scribanPackageJson, Does.Contain("2.1.0"));
        }
    }
}
