using Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    public class BattleEquipTable
    {
        public Dictionary<string, BattleEquipPack> Equips { get; set; } = [];
    }
    public class BattleEquipPack
    {
        public List<BattleEquipPerLevelPack> Phases { get; set; } = [];
    }
    public class BattleEquipPerLevelPack
    {
        public int EquipLevel { get; set; }

        public List<BattleUniEquipData> Parts { get; set; } = [];

        public List<BlackboardDataPair> AttributeBlackboard { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, List<BlackboardDataPair>> TokenAttributeBlackboard { get; set; } = [];
    }
    public class BattleUniEquipData
    {
        public string? ResKey { get; set; }

        public UniEquipTarget Target { get; set; }

        public bool IsToken { get; set; }

        public string? ValidInGameTag { get; set; }
        public string? ValidInMapTag { get; set; }

        public EquipTalentDataBundle? AddOrOverrideTalentDataBundle { get; set; }

        public EquipTraitDataBundle? OverrideTraitDataBundle { get; set; }
    }
}
