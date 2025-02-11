using System.Collections.Generic;
using System.Text.Json;
using NuGet.Versioning;
using NUnit.Framework;

namespace UnityNuGet.Tests
{
    public class VersionRangeConverterTests
    {
        [Test]
        public void Read_Write_Success()
        {
            string json = @"{""ignore"":false,""listed"":false,""version"":""[1.2.3, )"",""defineConstraints"":[],""analyzer"":false,""includePrerelease"":false,""includeUnlisted"":false}";

            RegistryEntry registryEntry = JsonSerializer.Deserialize(json, UnityNugetJsonSerializerContext.Default.RegistryEntry)!;

            Assert.That(registryEntry, Is.EqualTo(new RegistryEntry
            {
                Version = new VersionRange(new NuGetVersion(1, 2, 3))
            }).Using(new RegistryEntryEqualityComparer()));

            string newJson = JsonSerializer.Serialize(registryEntry, UnityNugetJsonSerializerContext.Default.RegistryEntry)!;

            Assert.That(newJson, Is.EqualTo(json));
        }

        private sealed class RegistryEntryEqualityComparer : IEqualityComparer<RegistryEntry>
        {
            public bool Equals(RegistryEntry? r1, RegistryEntry? r2)
            {
                return r1 != null && r2 != null && r1.Version?.MinVersion == r2.Version?.MinVersion;
            }

            public int GetHashCode(RegistryEntry obj)
            {
                return obj.Version!.GetHashCode();
            }
        }
    }
}
