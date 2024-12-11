using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using UnityNuGet.Npm;

namespace UnityNuGet.Tests
{
    public class NpmPackageListAllResponseConverterTests
    {
        [Test]
        public void Read_Write_Success()
        {
            string json = @"{""TestPackage"":{""name"":""TestPackage"",""description"":null,""maintainers"":[],""versions"":{},""time"":null,""keywords"":[],""author"":null}}";

            NpmPackageListAllResponse response = JsonSerializer.Deserialize(json, UnityNugetJsonSerializerContext.Default.NpmPackageListAllResponse)!;

            Assert.That(response.Packages, Is.EquivalentTo(new Dictionary<string, NpmPackageInfo>
            {
                { "TestPackage", new NpmPackageInfo { Name = "TestPackage" } }
            }).Using(new NpmPackageInfoEqualityComparer()));

            string newJson = JsonSerializer.Serialize(response, UnityNugetJsonSerializerContext.Default.NpmPackageListAllResponse)!;

            Assert.That(newJson, Is.EqualTo(json));
        }

        private sealed class NpmPackageInfoEqualityComparer : IEqualityComparer<NpmPackageInfo>
        {
            public bool Equals(NpmPackageInfo? p1, NpmPackageInfo? p2)
            {
                return p1 != null && p2 != null && p1.Name == p2.Name;
            }

            public int GetHashCode(NpmPackageInfo obj)
            {
                return obj.Name!.GetHashCode();
            }
        }
    }
}
