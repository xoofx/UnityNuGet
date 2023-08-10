using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Versioning;

namespace UnityNuGet
{
    /// <summary>
    /// Extension methods for serializing NPM JSON responses.
    /// </summary>
    public static class JsonCommonExtensions
    {
        public static string ToJson(this JsonObjectBase self) => JsonConvert.SerializeObject(self, Settings);

        /// <summary>
        /// Settings used for serializing JSON objects in this project.
        /// </summary>
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter {
                    DateTimeStyles = DateTimeStyles.AssumeUniversal
                },
                new VersionConverter(),
            },
        };

        /// <summary>
        /// Converter for <see cref="VersionRange"/> NuGet
        /// </summary>
        private class VersionConverter : JsonConverter<VersionRange>
        {
            public override void WriteJson(JsonWriter writer, VersionRange? value, JsonSerializer serializer)
            {
                writer.WriteValue(value?.ToString());
            }

            public override VersionRange? ReadJson(JsonReader reader, Type objectType, VersionRange? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string? s = (string?)reader.Value;
                return VersionRange.Parse(s!);
            }
        }
    }
}
