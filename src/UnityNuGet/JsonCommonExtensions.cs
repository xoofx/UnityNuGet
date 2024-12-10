using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace UnityNuGet
{
    /// <summary>
    /// Extension methods for serializing NPM JSON responses.
    /// </summary>
    public static class JsonCommonExtensions
    {
        public static async Task<string> ToJson(this JsonObjectBase self, JsonTypeInfo jsonTypeInfo)
        {
            using var stream = new MemoryStream();

            await JsonSerializer.SerializeAsync(stream, self, jsonTypeInfo);

            stream.Position = 0;

            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }
    }
}
