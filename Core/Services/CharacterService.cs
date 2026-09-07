using Core.DTOs.Response;
using Core.Github.DTOs.Response;
using Core.Interfaces;
using Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
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

            var list = charactersData.Values.ToList();
            _logger.LogInformation("[GetAllAsync] Loaded {Count} Characters from CharactersAsync.", list.Count);
            return list;
        }

        public async Task<CharacterDetail?> GetByIdAsync(string id)
        {
            var detail = await _gameDataRepository.GetCharacterDetailAsync(id);
            if (detail != null)
            {
                _logger.LogInformation(
                    "[GetByIdAsync] Operator: '{Name}' ({Id}) | Rarity: {Rarity} | Profession: {Profession} | SubProfession: {SubProf} | Phases: {Phases} | Skills: {Skills} | Talents: {Talents}",
                    detail.Name, id,
                    detail.Rarity,
                    detail.Profession,
                    detail.SubProfessionId,
                    detail.Phases?.Count ?? 0,
                    detail.Skills?.Count ?? 0,
                    detail.Talents?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning("[GetByIdAsync] No operator found for ID: {Id}", id);
            }
            return detail;
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

                foreach (var skillLevel in returnSkill.Levels)
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
                    SkillId = skill.SkillId,
                    IconId = returnSkill.IconId,
                    SkillType = (returnLevels != null && returnLevels.Count > count) ? returnLevels[count].SkillType : (returnLevels?.FirstOrDefault()?.SkillType),
                    Levels = skillLevelDtoList

                };

                _logger.LogInformation(
                    "[GetSkillsForOperatorAsync] Skill #{Index}: Id='{SkillId}', IconId='{IconId}', Type={SkillType}, Levels={LevelCount}",
                    list.Count, dto.SkillId, dto.IconId, dto.SkillType, dto.Levels?.Count ?? 0);

                list.Add(dto);
            }

            _logger.LogInformation("[GetSkillsForOperatorAsync] Total {Count} skills loaded for operator '{Name}'.", list.Count, op.Name);
            return list;
        }

        public async Task<List<UniEquipData>> GetModulesByOperatorAsync(string id)
        {
            var list = new List<UniEquipData>();
            if (string.IsNullOrEmpty(id)) return list;

            var uniEquipData = await _gameDataRepository.GetUniEquipTableAsync();

            if (uniEquipData == null || uniEquipData.Count == 0) 
            {
                return list;
            }

            list = uniEquipData.Values
                .Where(x => (x.CharId != null && string.Equals(x.CharId, id, StringComparison.OrdinalIgnoreCase)) ||
                            (x.UniEquipId != null && x.UniEquipId.Contains(id, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogInformation("[GetModulesByOperatorAsync] For operator '{Id}' found {Count} module(s).", id, list.Count);
            foreach (var m in list)
            {
                _logger.LogInformation(
                    "  Module: UniEquipId='{UniEquipId}', Name='{Name}', TypeName='{TypeName}', CharId='{CharId}'",
                    m.UniEquipId, m.UniEquipName, m.TypeName1, m.CharId);
            }
            return list;
        }

        public async Task<BattleEquipPack?> GetBattleEquipAsync(string id)
        {
            var battleEquipData = await _gameDataRepository.GetBattleEquipTableAsync();

            if (battleEquipData == null)
            {
                _logger.LogWarning("[GetBattleEquipAsync] BattleEquip table returned null.");
                return null;
            }

            var pack = battleEquipData.GetValueOrDefault(id);
            if (pack != null)
            {
                _logger.LogInformation(
                    "[GetBattleEquipAsync] Found BattleEquipPack for Id='{Id}': {PhaseCount} phase(s).",
                    id, pack.Phases?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning("[GetBattleEquipAsync] No BattleEquipPack found for Id='{Id}'.", id);
            }
            return pack;
        }

        private string? ParseBlackboard(string? description, List<BlackboardDataPair>? blackboard)
        {
            if (string.IsNullOrEmpty(description)) return description;

            if (blackboard == null || blackboard.Count == 0) return description;

            foreach (var bb in blackboard)
            {
                if (string.IsNullOrEmpty(bb.Key)) continue;

                string value;
                if (!string.IsNullOrEmpty(bb.ValueStr))
                {
                    value = bb.ValueStr;
                }
                else if (bb.Value.HasValue)
                {
                    value = FormatValue(bb.Value.Value);
                }
                else
                {
                    value = "";
                }

                description = description.Replace(bb.Key, value);
            }

            return description;
        }

        private string FormatValue(double value)
        {
            return value % 1 == 0
                ? ((int)value).ToString()
                : value.ToString("0.##");
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

        public async Task<RangeData?> GetRangeDataAsync(string? rangeId)
        {
            if (string.IsNullOrEmpty(rangeId)) return null;
            var table = await _gameDataRepository.GetRangeTableAsync();
            if (table != null && table.TryGetValue(rangeId, out var data))
            {
                _logger.LogInformation(
                    "[GetRangeDataAsync] RangeId='{RangeId}': {GridCount} grid point(s).",
                    rangeId, data.Grids?.Count ?? 0);
                return data;
            }
            _logger.LogWarning("[GetRangeDataAsync] RangeId='{RangeId}' not found in range table.", rangeId);
            return null;
        }
    }
}
