using Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Core.Models.Entities;

public class UniEquipTable
{
    public Dictionary<string, UniEquipData> EquipDict { get; set; } = [];
    public Dictionary<string, UniEquipMissionData> MissionList { get; set; } = [];
    public Dictionary<string, SubProfessionData> SubProfDict { get; set; } = [];
    public Dictionary<string, int> SubProfToProfDict { get; set; } = [];
    public Dictionary<string, List<string>> CharEquip { get; set; } = [];
    public List<UniEquipTypeInfo> EquipTypeInfos { get; set; } = [];
    public Dictionary<string, UniEquipTimeInfo> EquipTrackDict { get; set; } = [];
}

public class UniEquipData
{
    public string? UniEquipId { get; set; }
    public string? UniEquipName { get; set; }
    public string? UniEquipIcon { get; set; }
    public string? UniEquipDesc { get; set; }

    public string? TypeIcon { get; set; }
    public string? TypeName1 { get; set; }
    public string? TypeName2 { get; set; }
    public string? EquipShiningColor { get; set; }

    public EvolvePhase ShowEvolvePhase { get; set; }
    public EvolvePhase UnlockEvolvePhase { get; set; }

    public string? CharId { get; set; }
    public string? TmplId { get; set; }

    public int ShowLevel { get; set; }
    public int UnlockLevel { get; set; }

    public List<string> MissionList { get; set; } = [];

    [JsonPropertyName("itemCost")]
    public List<StageCostEntry>? ItemCost { get; set; } = [];

    public UniEquipType Type { get; set; }

    public long UniEquipGetTime { get; set; }
    public long UniEquipShowEnd { get; set; }

    public int CharEquipOrder { get; set; }

    public bool HasUnlockMission { get; set; }
    public bool IsSpecialEquip { get; set; }

    public string? SpecialEquipDesc { get; set; }
    public string? SpecialEquipColor { get; set; }
    public string? CharColor { get; set; }

    [JsonIgnore]
    public List<Dictionary<string, int>> UnlockFavors { get; set; } = [];
}

public class StageCostEntry
{
    [JsonPropertyName("key")]
    public int Stage { get; set; }

    [JsonPropertyName("value")]
    public List<ItemBundle> Items { get; set; } = [];
}
