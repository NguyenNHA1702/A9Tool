using Core.DTOs.Response;
using Core.Enums;
using Core.Github.DTOs.Response;
using Core.Interfaces;
using Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Services
{
    /// <summary>
    /// Core domain service for managing and processing operator character data,
    /// including skills, modules (equipment), battle stats, and ranges.
    /// </summary>
    public class CharacterService
    {
        private readonly IGameDataRepository _gameDataRepository;
        private readonly ILogger<CharacterService> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="CharacterService"/> with the repository and logger.
        /// </summary>
        public CharacterService(IGameDataRepository gameDataRepository, ILogger<CharacterService> logger)
        {
            _gameDataRepository = gameDataRepository;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the list of all available operators.
        /// </summary>
        /// <returns>A list of <see cref="Character"/> objects.</returns>
        /// <exception cref="Exception">Thrown when character data cannot be retrieved from the repository.</exception>
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

        /// <summary>
        /// Retrieves detailed information for a specific operator by their ID.
        /// </summary>
        /// <param name="id">The unique operator ID (e.g., "char_002_amiya").</param>
        /// <returns>The <see cref="CharacterDetail"/> if found; otherwise, null.</returns>
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

        /// <summary>
        /// Resolves and formats all skills associated with an operator, parsing dynamic blackboard variables and cleaning markup.
        /// </summary>
        /// <param name="op">The detailed operator model.</param>
        /// <param name="count">Default level index used to select the primary skill type.</param>
        /// <returns>A list of formatted <see cref="SkillsForOperatorResponse"/> objects.</returns>
        public async Task<List<SkillsForOperatorResponse>> GetSkillsForOperatorAsync(CharacterDetail op, int count = 0)
        {
            var list = new List<SkillsForOperatorResponse>();
            var skillTable = await _gameDataRepository.GetSkillTableAsync();

            if (skillTable == null || op.Skills == null)
            {
                _logger.LogWarning("[GetSkillsForOperatorAsync] SkillTable is null or operator has no skills.");
                return list;
            }

            foreach (var skill in op.Skills)
            {
                _logger.LogInformation("Skill ID from op: {SkillId}", skill.SkillId);

                if (string.IsNullOrEmpty(skill.SkillId) || !skillTable.TryGetValue(skill.SkillId, out var returnSkill))
                {
                    _logger.LogWarning("Skill {SkillId} not found in skill table", skill.SkillId);
                    continue;
                }

                var returnLevels = returnSkill.Levels;
                var skillLevelDtoList = new List<SkillLevelResponse>();

                if (returnLevels != null)
                {
                    foreach (var skillLevel in returnLevels)
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

        /// <summary>
        /// Retrieves all modules (equipment) assigned to an operator, ordered by unlock sequence, excluding initial badge slots.
        /// </summary>
        /// <param name="id">The unique operator ID.</param>
        /// <returns>A list of <see cref="UniEquipData"/> modules.</returns>
        public async Task<List<UniEquipData>> GetModulesByOperatorAsync(string id)
        {
            var list = new List<UniEquipData>();
            if (string.IsNullOrEmpty(id)) return list;

            var uniEquipData = await _gameDataRepository.GetUniEquipTableAsync();

            if (uniEquipData == null || uniEquipData.Count == 0) 
            {
                return list;
            }

            var shortCode = id.Contains('_') ? id.Split('_').Last() : id;

            list = uniEquipData.Values
                .Where(x => x.CharId != null && string.Equals(x.CharId, id, StringComparison.OrdinalIgnoreCase))
                // Exclude INITIAL (default badge / ORIGINAL slot) — it is already represented by slot 0 in the UI.
                .Where(x => x.Type != UniEquipType.INITIAL)
                .OrderBy(x => x.CharEquipOrder)
                .ThenBy(x => x.UniEquipId)
                .ToList();

            _logger.LogInformation("[GetModulesByOperatorAsync] For operator '{Id}' (shortCode: '{ShortCode}') found {Count} module(s).", id, shortCode, list.Count);
            foreach (var m in list)
            {
                _logger.LogInformation(
                    "  Module: UniEquipId='{UniEquipId}', Name='{Name}', TypeName='{TypeName}', CharId='{CharId}'",
                    m.UniEquipId, m.UniEquipName, m.TypeName1, m.CharId);
            }
            return list;
        }

        /// <summary>
        /// Retrieves the battle equipment configuration pack (stages, stats, buffs) for a specific equipment ID.
        /// </summary>
        /// <param name="id">The equipment pack ID (e.g., "uniequip_002_amiya").</param>
        /// <returns>The <see cref="BattleEquipPack"/> if found; otherwise, null.</returns>
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

        /// <summary>
        /// Retrieves the list of detailed unlock missions for a specific operator module.
        /// </summary>
        /// <param name="module">The operator module entity.</param>
        /// <returns>A list of <see cref="UniEquipMissionData"/> ordered by sort index.</returns>
        public async Task<List<UniEquipMissionData>> GetMissionsForModuleAsync(UniEquipData? module)
        {
            var result = new List<UniEquipMissionData>();
            if (module == null || module.MissionList == null || module.MissionList.Count == 0)
            {
                return result;
            }

            var missionTable = await _gameDataRepository.GetUniEquipMissionTableAsync();
            if (missionTable == null || missionTable.Count == 0)
            {
                return result;
            }

            foreach (var missionId in module.MissionList)
            {
                if (string.IsNullOrEmpty(missionId)) continue;

                if (missionTable.TryGetValue(missionId, out var missionData))
                {
                    result.Add(missionData);
                }
                else
                {
                    // Fallback using raw mission ID if details are not present
                    result.Add(new UniEquipMissionData
                    {
                        UniEquipMissionId = missionId,
                        Desc = missionId
                    });
                }
            }

            return result.OrderBy(m => m.UniEquipMissionSort).ToList();
        }

        /// <summary>
        /// Interpolates dynamic formula variables within description templates using values from the blackboard dataset.
        /// </summary>
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

        /// <summary>
        /// Formats numeric blackboard values into clean decimal or integer display strings.
        /// </summary>
        private string FormatValue(double value)
        {
            return value % 1 == 0
                ? ((int)value).ToString()
                : value.ToString("0.##");
        }

        /// <summary>
        /// Removes HTML/XML style markup tags from raw description strings.
        /// </summary>
        private static string? CleanDescription(string? description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            return Regex.Replace(description, @"<[^>]+>", "");
        }

        /// <summary>
        /// Clears memory and disk caches in the underlying game data repository.
        /// </summary>
        public async Task ResetCache()
        {
            await _gameDataRepository.ResetCache();
        }

        /// <summary>
        /// Retrieves the attack range grid layout for a given range ID.
        /// </summary>
        /// <param name="rangeId">The unique range identifier.</param>
        /// <returns>The <see cref="RangeData"/> if found; otherwise, null.</returns>
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
