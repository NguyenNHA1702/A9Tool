using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Github.DTOs.Response
{
    public class SkillsForOperatorResponse
    {
        public string? SkillId {  get; set; }
        public string? SkillName { get; set; } = string.Empty;
        public string? SkillDescription { get; set; }
        public string? IconId { get; set; }
        public string? SkillType { get; set; }
    }
}
