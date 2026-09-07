using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Entities
{
    public class EquipTraitDataBundle
    {
        public List<EquipTraitData> Candidates { get; set; } = [];
    }

    public class EquipTraitData
    {
        public string? AdditionalDescription { get; set; }

        public UnlockCondition? UnlockCondition { get; set; }

        public int RequiredPotentialRank { get; set; }

        public List<BlackboardDataPair> Blackboard { get; set; } = [];

        // Giữ nguyên spelling theo schema
        public string? OverrideDescripton { get; set; }

        public string? PrefabKey { get; set; }

        public string? RangeId { get; set; }
    }
}
