using Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Entities
{
    public class UniEquipMissionData
    {
        public string? Template { get; set; }
        public string? Desc { get; set; }
        public List<string> ParamList { get; set; } = [];

        public string? UniEquipMissionId { get; set; }
        public int UniEquipMissionSort { get; set; }

        public string? UniEquipId { get; set; }
        public string? JumpStageId { get; set; }
    }

    public class SubProfessionData
    {
        public string? SubProfessionId { get; set; }
        public string? SubProfessionName { get; set; }
        public int SubProfessionCatagory { get; set; }
    }

    public class UniEquipTypeInfo
    {
        public string? UniEquipTypeName { get; set; }
        public int SortId { get; set; }

        public bool IsSpecial { get; set; }
        public bool IsInitial { get; set; }
    }

    public class UniEquipTrack
    {
        public string? CharId { get; set; }
        public string? EquipId { get; set; }

        public UniEquipType Type { get; set; }

        public long ArchiveShowTimeEnd { get; set; }
    }

    public class UniEquipTimeInfo
    {
        public long TimeStamp { get; set; }

        public List<UniEquipTrack> TrackList { get; set; } = [];
    }
}
