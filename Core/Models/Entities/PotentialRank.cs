using System.Text.Json.Serialization;
using System.Text.Json;

public class PotentialRank
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("buff")]
    public JsonElement? Buff { get; set; }

    [JsonPropertyName("equivalentCost")]
    public JsonElement? EquivalentCost { get; set; }
}