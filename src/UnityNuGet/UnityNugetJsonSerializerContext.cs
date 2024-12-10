using System.Collections.Generic;
using System.Text.Json.Serialization;
using UnityNuGet.Npm;

namespace UnityNuGet
{
    [JsonSourceGenerationOptions(Converters = [typeof(NpmPackageListAllResponseConverter), typeof(VersionRangeConverter)])]
    [JsonSerializable(typeof(NpmError))]
    [JsonSerializable(typeof(NpmPackage))]
    [JsonSerializable(typeof(NpmPackageCacheEntry))]
    [JsonSerializable(typeof(NpmPackageListAllResponse))]
    [JsonSerializable(typeof(Registry))]
    [JsonSerializable(typeof(UnityAsmdef))]
    [JsonSerializable(typeof(UnityPackage))]
    [JsonSerializable(typeof(IDictionary<string, RegistryEntry>))]
    public sealed partial class UnityNugetJsonSerializerContext : JsonSerializerContext
    {
    }
}
