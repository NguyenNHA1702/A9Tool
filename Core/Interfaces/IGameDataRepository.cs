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
        Task ResetCache();
    }
}
