using Core.DTOs.Response;
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

                if (!skillTable.TryGetValue(skill.SkillId, out var returnSkill))
                {
                    _logger.LogWarning("Skill {SkillId} not found in skill table", skill.SkillId);
                    continue;
                }

                var returnLevels = returnSkill.Levels;

                var skillLevelDtoList = new List<SkillLevelResponse>();

                foreach(var skillLevel in returnSkill.Levels)
                {
                    var parsedDesc = ParseBlackboard(skillLevel.Description, skillLevel.Blackboard);
                    var cleanDesc = CleanDescription(parsedDesc);

                    var skillLevelDto = new SkillLevelResponse
                    {
                        Name = skillLevel.Name,
                        Description = cleanDesc,
                        SpCost = skillLevel.SpData?.SpCost ?? 0,
                        InitSp = skillLevel.SpData?.InitSp ?? 0,
                        Duration = skillLevel.Duration,
                        RangeId = skillLevel.RangeId
                    };

                    skillLevelDtoList.Add(skillLevelDto);
                }

                var dto = new SkillsForOperatorResponse 
                {
                    SkillId = returnSkill.SkillId,
                    IconId = returnSkill.IconId,
                    SkillType = returnLevels[count].SkillType,
                    Levels = skillLevelDtoList

                };

                list.Add(dto);
            }

                return list;
        }

        private string? ParseBlackboard(string description, List<SkillBlackboard> blackboard)
        {
            throw new NotImplementedException();
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
