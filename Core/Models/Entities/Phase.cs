using System.Text.Json.Serialization;

public class Phase
{
    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("attributesKeyFrames")]
    public List<AttributeKeyFrame> AttributesKeyFrames { get; set; } = new();
}

public class AttributeKeyFrame
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("data")]
    public Stats? Data { get; set; }
}