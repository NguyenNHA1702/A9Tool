using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    public class Character
    {
        public string Id { get; set; }           // char_285_medic2
        public string Name { get; set; }         // Lancet-2
        public string Rarity { get; set; }          // 1 (map from TIER_1)
        public string Profession { get; set; }   // MEDIC / WARRIOR
        public string NationId { get; set; }     // rhodes
        public string Position { get; set; }     // RANGED / MELEE
        public List<string> Tags { get; set; }   // ["Robot", "Healing"]
    }
}
