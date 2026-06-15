using System.Text.Json.Serialization;

public class Talent
{
    [JsonPropertyName("candidates")]
    public List<TalentCandidate> Candidates { get; set; } = new();
}

public class TalentCandidate
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("unlockCondition")]
    public UnlockCondition? UnlockCondition { get; set; }

    [JsonPropertyName("requiredPotentialRank")]
    public int RequiredPotentialRank { get; set; }
}

public class UnlockCondition
{
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }
}