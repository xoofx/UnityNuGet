using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using UnityNuGet.Npm;

namespace UnityNuGet.Tests
{
    public class RegistryCacheTests
    {
        [Test]
        public async Task TestBuild()
        {
            bool errorsTriggered = false;

            var hostEnvironmentMock = new Mock<IHostEnvironment>();
            hostEnvironmentMock.Setup(h => h.EnvironmentName).Returns(Environments.Development);

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new FakeLoggerProvider());

            string unityPackages = Path.Combine(Path.GetDirectoryName(typeof(RegistryCacheTests).Assembly.Location)!, "unity_packages");
            var registry = new Registry(hostEnvironmentMock.Object, loggerFactory, Options.Create(new RegistryOptions { RegistryFilePath = "registry.json" }));

            await registry.StartAsync(CancellationToken.None);

            var registryCache = new RegistryCache(
                registry,
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

            NpmPackageListAllResponse allResult = registryCache.All();
            Assert.That(allResult.Packages, Has.Count.GreaterThanOrEqualTo(3));
            string allResultJson = await allResult.ToJson(UnityNugetJsonSerializerContext.Default.NpmPackageListAllResponse);

            Assert.That(allResultJson, Does.Contain("org.nuget.scriban"));
            Assert.That(allResultJson, Does.Contain("org.nuget.system.runtime.compilerservices.unsafe"));

            NpmPackage? scribanPackage = registryCache.GetPackage("org.nuget.scriban");
            Assert.That(scribanPackage, Is.Not.Null);
            string scribanPackageJson = await scribanPackage!.ToJson(UnityNugetJsonSerializerContext.Default.NpmPackage);
            Assert.That(scribanPackageJson, Does.Contain("org.nuget.scriban"));
            Assert.That(scribanPackageJson, Does.Contain("2.1.0"));
        }
    }
}
