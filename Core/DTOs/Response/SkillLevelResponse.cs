using System;
using System.Collections.Generic;
using System.Text;

namespace Core.DTOs.Response
{
    public class SkillLevelResponse
    {
        public string? Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SpCost { get; set; }
        public int? InitSp { get; set; }
        public double Duration { get; set; }
        public string? RangeId { get; set; }
    }
}
