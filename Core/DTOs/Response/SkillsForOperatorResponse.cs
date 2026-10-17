using Core.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Github.DTOs.Response
{
    public class SkillsForOperatorResponse
    {
        public string? SkillId {  get; set; }
        public string? IconId { get; set; }
        public string? SkillType { get; set; }
        public List<SkillLevelResponse> Levels { get; set; } = new();
        public List<Core.Models.Entities.SpecializeLevelData>? LevelUpCostCond { get; set; } = new();
        public UnlockCondition? UnlockCond { get; set; }
    }
}
