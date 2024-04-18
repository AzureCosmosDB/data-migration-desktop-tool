using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Text;

namespace Cosmos.DataTransfer.CosmosExtension;

/// <summary>
/// Serializer for Cosmos allowing access to internal JsonSerializer settings.
/// </summary>
/// <remarks>
/// Defaults to disabling metadata handling to allow passthrough of recognized properties like "$type".
/// </remarks>
public class RawJsonCosmosSerializer : CosmosSerializer
{
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);

    public static JsonSerializerSettings GetDefaultSettings() =>
        new()
        {
            DateParseHandling = DateParseHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            ContractResolver = null,
            MaxDepth = 64,
        };

    public JsonSerializerSettings SerializerSettings { get; } = GetDefaultSettings();

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var serializer = JsonSerializer.Create(SerializerSettings);
            return serializer.Deserialize<T>(jsonReader);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream, DefaultEncoding, bufferSize: 1024, leaveOpen: true))
        {
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                jsonWriter.Formatting = Formatting.None;
                var serializer = JsonSerializer.Create(SerializerSettings);
                serializer.Serialize(jsonWriter, input);
                jsonWriter.Flush();
                streamWriter.Flush();
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}