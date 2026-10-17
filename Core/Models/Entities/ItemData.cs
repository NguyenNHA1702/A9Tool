using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    public class ItemData
    {
        [JsonPropertyName("itemId")]
        public string? ItemId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("rarity")]
        public object? RawRarity { get; set; } // Game/FlatBuffers can output "TIER_1".."TIER_5" or integers 0..4

        [JsonPropertyName("iconId")]
        public string? IconId { get; set; }

        [JsonPropertyName("sortId")]
        public int SortId { get; set; }

        [JsonPropertyName("usage")]
        public string? Usage { get; set; }

        [JsonPropertyName("obtainApproach")]
        public string? ObtainApproach { get; set; }

        [JsonPropertyName("classifyType")]
        public object? ClassifyType { get; set; }

        [JsonPropertyName("itemType")]
        public object? ItemType { get; set; }

        /// <summary>
        /// Normalized rarity tier from 1 to 5 (or 6).
        /// </summary>
        [JsonIgnore]
        public int RarityTier
        {
            get
            {
                if (RawRarity == null) return 1;
                var str = RawRarity.ToString();
                if (int.TryParse(str, out var num)) return num + 1;
                return str switch
                {
                    "TIER_1" => 1,
                    "TIER_2" => 2,
                    "TIER_3" => 3,
                    "TIER_4" => 4,
                    "TIER_5" => 5,
                    "TIER_6" => 6,
                    _ => 1
                };
            }
        }
    }
}
