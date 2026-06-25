using Core.Interfaces;
using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Core.Services
{
    public class CharacterService
    {
        private readonly IGameDataRepository _gameDataRepository;

        public CharacterService(IGameDataRepository gameDataRepository)
        {
            _gameDataRepository = gameDataRepository;
        }

        public async Task<List<Character>> GetAllAsync()
        {
            var charactersData = await _gameDataRepository.GetCharactersAsync();

            if (charactersData == null)
            {
                throw new Exception("Failed to retrieve character data.");
            }

            return charactersData.Values.ToList();
        }

        public async Task<CharacterDetail?> GetByIdAsync(string id)
        {
            return await _gameDataRepository.GetCharacterDetailAsync(id);
        }

        public async Task<List<SkillDetail>> GetSkillsForOperatorAsync(CharacterDetail op)
        {
            var skillTable = await _gameDataRepository.GetSkillTableAsync();
            if (skillTable == null) return new();

            return op.Skills
                .Where(s => s.SkillId != null && skillTable.ContainsKey(s.SkillId))
                .Select(s => {
                    var detail = skillTable[s.SkillId!];
                    detail.SkillId = s.SkillId;
                    return detail;
                })
                .ToList();
        }

        public async Task ResetCache()
        {
            await _gameDataRepository.ResetCache();
        }
    }
}
