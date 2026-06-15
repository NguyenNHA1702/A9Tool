using Core.Models;
using Core.Models.Entities;
using System.Text.Json.Serialization;

public class CharacterDetail : Character
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("displayNumber")]
    public string? DisplayNumber { get; set; }

    [JsonPropertyName("subProfessionId")]
    public string? SubProfessionId { get; set; }

    [JsonPropertyName("phases")]
    public List<Phase> Phases { get; set; } = new();

    [JsonPropertyName("potentialRanks")]
    [JsonConverter(typeof(FlexibleListConverter<PotentialRank>))]
    public List<PotentialRank> PotentialRanks { get; set; } = new();

    [JsonPropertyName("talents")]
    [JsonConverter(typeof(FlexibleListConverter<Talent>))]
    public List<Talent> Talents { get; set; } = new();

    [JsonPropertyName("favorKeyFrames")]
    public List<FavorBonus> TrustBonus { get; set; } = new();

    [JsonPropertyName("skills")]
    [JsonConverter(typeof(FlexibleListConverter<Skill>))]
    public List<Skill> Skills { get; set; } = new();
}