using Core.Github.DTOs.Response;
using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface IGameDataRepository
    {
        Task<Dictionary<string, Character>> GetCharactersAsync();
        Task DownloadAllAvatarAsync(List<Character> characters, string webRootPath);
        Task<CharacterDetail?> GetCharacterDetailAsync(string id);
        Task<Dictionary<string, SkillDetail>?> GetSkillTableAsync();
        Task<Dictionary<string, RangeData>?> GetRangeTableAsync();
        Task<Dictionary<string, UniEquipData>?> GetUniEquipTableAsync();
        Task<Dictionary<string, BattleEquipPack>?> GetBattleEquipTableAsync();
        //Task<SkillsForOperatorResponse?> GetSkillByIdAsync(string id);
        Task ResetCache();
    }
}
