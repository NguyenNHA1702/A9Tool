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

        [JsonPropertyName("levels")]
        public List<SkillLevel> Levels { get; set; } = new();
    }

    public class SkillLevel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("skillType")]
        public string? SkillType { get; set; }

        [JsonPropertyName("spData")]
        public SkillSpData? SpData { get; set; }
    }

    public class SkillSpData
    {
        [JsonPropertyName("spType")]
        public JsonElement SpType { get; set; }

        [JsonPropertyName("spCost")]
        public int SpCost { get; set; }

        [JsonPropertyName("initSp")]
        public int InitSp { get; set; }
    }
}