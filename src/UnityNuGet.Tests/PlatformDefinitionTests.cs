using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class PlatformDefinitionTests
    {
        [Test]
        public void CanFindDefinitions()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();

            // Look-up by OS should return the most general configuration
            var win = platformDefs.Find(UnityOs.Windows);
            Assert.That(win, Is.Not.Null);
            Assert.That(win!.Cpu, Is.EqualTo(UnityCpu.AnyCpu));

            // Look-up explicit configuration
            var win64 = platformDefs.Find(UnityOs.Windows, UnityCpu.X64);
            Assert.Multiple(() =>
            {
                Assert.That(win64, Is.Not.Null);
                Assert.That(win.Os, Is.EqualTo(win64!.Os));
            });
            Assert.Multiple(() =>
            {
                Assert.That(win64.Cpu, Is.EqualTo(UnityCpu.X64));
                Assert.That(win.Children, Does.Contain(win64));
            });

            // Look-up invalid configuration
            var and = platformDefs.Find(UnityOs.Android, UnityCpu.None);
            Assert.That(and, Is.Null);
        }

        [Test]
        public void RemainingPlatforms_NoneVisited()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var visited = new HashSet<PlatformDefinition>();

            // If no platform was visited, the remaining platforms should be the (AnyOS, AnyCPU) config.
            var remaining = platformDefs.GetRemainingPlatforms(visited);
            Assert.That(remaining, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(remaining, Has.Count.EqualTo(1));
                Assert.That(platformDefs, Is.EqualTo(remaining.First()));
            });
        }

        [Test]
        public void RemainingPlatforms_OneVisited()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            
            foreach (var child in platformDefs.Children)
            {
                var visited = new HashSet<PlatformDefinition>() { child };
                var remaining = platformDefs.GetRemainingPlatforms(visited);

                // We should get all other children, except the one already visited
                Assert.That(remaining.Count + 1, Is.EqualTo(platformDefs.Children.Count));
                foreach (var r in remaining)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(child, Is.Not.EqualTo(r));
                        Assert.That(platformDefs.Children, Does.Contain(r));
                    });
                }
            }
        }

        [Test]
        public void RemainingPlatforms_LeafVisited()
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var win64 = platformDefs.Find(UnityOs.Windows, UnityCpu.X64);
            var visited = new HashSet<PlatformDefinition>() { win64! };

            // The remaining platforms should be all non-windows, as well as all !x64 windows
            var expected = platformDefs.Children
                .Except(new[] { win64!.Parent })
                .Concat(
                    win64.Parent!.Children
                        .Except(new[] { win64 }))
                .ToHashSet();
            var actual = platformDefs.GetRemainingPlatforms(visited);
            Assert.That(expected.SetEquals(actual), Is.True);
        }

        [TestCase("")]
        [TestCase("base")]
        public void TestConfigPath_Root(string basePath)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var file = new PlatformFile("a/b/c.dll", platformDefs);

            // We don't use extra paths for the (AnyOS, AnyCPU) configuration
            var actual = file.GetDestinationPath(basePath);
            var expected = Path.Combine(
                basePath,
                Path.GetFileName(file.SourcePath));
            Assert.That(expected, Is.EqualTo(actual));
        }

        [TestCase("")]
        [TestCase("base")]
        public void TestConfigPath_OsOnly(string basePath)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var win = platformDefs.Find(UnityOs.Windows);
            var file = new PlatformFile("a/b/c.dll", win!);

            var actual = file.GetDestinationPath(basePath);
            var expected = Path.Combine(
                basePath,
                "Windows",
                Path.GetFileName(file.SourcePath));
            Assert.That(expected, Is.EqualTo(actual));
        }

        [TestCase("")]
        [TestCase("base")]
        public void TestConfigPath_Full(string basePath)
        {
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var win64 = platformDefs.Find(UnityOs.Windows, UnityCpu.X64);
            var file = new PlatformFile("a/b/c.dll", win64!);

            var actual = file.GetDestinationPath(basePath);
            var expected = Path.Combine(
                basePath,
                "Windows",
                "x86_64",
                Path.GetFileName(file.SourcePath));
            Assert.That(expected, Is.EqualTo(actual));
        }
    }
}
