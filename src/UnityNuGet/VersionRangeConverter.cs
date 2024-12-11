using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace UnityNuGet
{
    /// <summary>
    /// Converter for <see cref="VersionRange"/> NuGet
    /// </summary>
    internal sealed class VersionRangeConverter : JsonConverter<VersionRange>
    {
        public override VersionRange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? s = reader.GetString();
            return VersionRange.Parse(s!);
        }

        public override void Write(Utf8JsonWriter writer, VersionRange value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString());
        }
    }
}
