using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    public class Skill
    {
        [JsonPropertyName("skillId")]
        public string? SkillId { get; set; }

        [JsonPropertyName("unlockCond")]
        public UnlockCondition? UnlockCond { get; set; }
    }

    public class SkillDetail
    {
        [JsonPropertyName("skillId")]
        public string? SkillId { get; set; }

        [JsonPropertyName("iconId")]
        public string? IconId { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }

        [JsonPropertyName("levels")]
        public List<SkillLevel> Levels { get; set; } = new();
    }

    public class SkillLevel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("rangeId")]
        public string? RangeId { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("skillType")]
        public string? SkillType { get; set; }

        [JsonPropertyName("durationType")]
        public string? DurationType { get; set; }

        [JsonPropertyName("spData")]
        public SkillSpData? SpData { get; set; }

        [JsonPropertyName("prefabId")]
        public string? PrefabId { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("blackboard")]
        [JsonConverter(typeof(FlexibleListConverter<BlackboardDataPair>))]
        public List<BlackboardDataPair>? Blackboard { get; set; }
    }

    public class SkillSpData
    {
        [JsonPropertyName("spType")]
        public JsonElement SpType { get; set; }

        [JsonPropertyName("levelUpCost")]
        public string? LeveUpCost { get; set; }

        [JsonPropertyName("maxChargeTime")]
        public int MaxChargeTime { get; set; }

        [JsonPropertyName("spCost")]
        public int SpCost { get; set; }

        [JsonPropertyName("initSp")]
        public int InitSp { get; set; }

        [JsonPropertyName("increment")]
        public double Increment { get; set; }
    }

    public class BlackboardDataPair
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("valueStr")]
        public string? ValueStr { get; set; }
    }
}