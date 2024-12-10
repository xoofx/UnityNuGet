using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityNuGet.Npm;

namespace UnityNuGet
{
    /// <summary>
    /// Converter for <see cref="NpmPackageListAllResponse"/> NuGet
    /// </summary>
    internal sealed class NpmPackageListAllResponseConverter : JsonConverter<NpmPackageListAllResponse>
    {
        public override NpmPackageListAllResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            NpmPackageListAllResponse result = new();

            string? currentPropertyName = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        result.Packages.Add(currentPropertyName!, JsonSerializer.Deserialize(ref reader, UnityNugetJsonSerializerContext.Default.NpmPackageInfo)!);
                        break;
                    case JsonTokenType.PropertyName:
                        currentPropertyName = reader.GetString();
                        break;
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, NpmPackageListAllResponse value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (KeyValuePair<string, NpmPackageInfo> kvp in value.Packages)
            {
                writer.WritePropertyName(kvp.Key);

                JsonSerializer.Serialize(writer, kvp.Value, UnityNugetJsonSerializerContext.Default.NpmPackageInfo);
            }

            writer.WriteEndObject();
        }
    }
}
