using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Models
{
    public class FlexibleListConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return new List<T>();
            }

            if (reader.TokenType == JsonTokenType.Null)
                return new List<T>();

            var result = new List<T>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item != null) result.Add(item);
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, options);
    }
}