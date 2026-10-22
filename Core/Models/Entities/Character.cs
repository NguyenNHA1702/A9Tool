using System.Text.Json.Serialization;

public class Character
{
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rarity")]
    public string? Rarity { get; set; }

    [JsonPropertyName("profession")]
    public string? Profession { get; set; }

    [JsonPropertyName("subProfessionId")]
    public string? SubProfessionId { get; set; }

    [JsonPropertyName("nationId")]
    public string? NationId { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("tagList")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("itemObtainApproach")]
    public string? ItemObtainApproach { get; set; }
}