using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Interfaces
{
    public interface IGameDataRepository
    {
        Task<Dictionary<string, Character>> GetCharactersAsync();
        Task DownloadAllAvatarAsync(List<Character> characters, string webRootPath);
    }
}
