using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    public class RangeData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("grids")]
        public List<RangeGridCell> Grids { get; set; } = new();
    }

    public class RangeGridCell
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("col")]
        public int Col { get; set; }
    }
}
