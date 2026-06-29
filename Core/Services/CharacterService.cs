using Core.Github.DTOs.Response;
using Core.Interfaces;
using Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;


namespace Core.Services
{
    public class CharacterService
    {
        private readonly IGameDataRepository _gameDataRepository;
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(IGameDataRepository gameDataRepository, ILogger<CharacterService> logger)
        {
            _gameDataRepository = gameDataRepository;
            _logger = logger;
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

        public async Task<List<SkillsForOperatorResponse>> GetSkillsForOperatorAsync(CharacterDetail op, int count = 0)
        {
            var list = new List<SkillsForOperatorResponse>();
            var skillTable = await _gameDataRepository.GetSkillTableAsync();

            foreach (var skill in op.Skills)
            {
                _logger.LogInformation("Skill ID from op: {SkillId}", skill.SkillId);
                var returnSkill = skillTable[skill.SkillId];

                var returnLevels = new List<SkillLevel>();
                returnLevels = returnSkill.Levels;

                var dto = new SkillsForOperatorResponse 
                {
                    SkillId = returnSkill.SkillId,
                    SkillName = returnLevels[count].Name,
                    SkillDescription = CleanDescription(returnLevels[count].Description),
                    SkillType = returnLevels[count].SkillType
                };

                list.Add(dto);
            }

                return list;
        }

        private static string? CleanDescription(string? description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            return Regex.Replace(description, @"<[^>]+>", "");
        }

        public async Task ResetCache()
        {
            await _gameDataRepository.ResetCache();
        }
    }
}
