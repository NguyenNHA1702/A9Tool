using Core.Models.Entities;
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

public class TalentData
{
    public UnlockCondition? UnlockCondition { get; set; }

    public int RequiredPotentialRank { get; set; }

    public string? PrefabKey { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? RangeId { get; set; }

    public List<BlackboardDataPair> Blackboard { get; set; } = [];

    public string? TokenKey { get; set; }

    public bool IsHideTalent { get; set; }
}

public class EquipTalentDataBundle
{
    public List<EquipTalentData> Candidates { get; set; } = [];
}

public class EquipTalentData
{
    public bool DisplayRangeId { get; set; }

    public string? UpgradeDescription { get; set; }

    public int TalentIndex { get; set; }

    public List<int> ValidModeIndices { get; set; } = [];

    public UnlockCondition? UnlockCondition { get; set; }

    public int RequiredPotentialRank { get; set; }

    public string? PrefabKey { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? RangeId { get; set; }

    public List<BlackboardDataPair> Blackboard { get; set; } = [];

    public string? TokenKey { get; set; }

    public bool IsHideTalent { get; set; }
}