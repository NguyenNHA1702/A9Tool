using Core.Github.DTOs.Response;
using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    /// <summary>
    /// Contract for accessing Arknights game data entities and static definition tables,
    /// including operators, skills, attack ranges, and modules.
    /// </summary>
    public interface IGameDataRepository
    {
        /// <summary>
        /// Retrieves all basic operator metadata mapped by operator ID.
        /// </summary>
        /// <returns>A dictionary mapping operator IDs to <see cref="Character"/> objects.</returns>
        Task<Dictionary<string, Character>> GetCharactersAsync();

        /// <summary>
        /// Downloads and stores avatar images locally for the provided list of operators.
        /// </summary>
        /// <param name="characters">The list of operators whose avatars to download.</param>
        /// <param name="webRootPath">The local target directory path for saving avatar assets.</param>
        Task DownloadAllAvatarAsync(List<Character> characters, string webRootPath);

        /// <summary>
        /// Retrieves comprehensive details for a specific operator by ID.
        /// </summary>
        /// <param name="id">The unique operator ID (e.g., "char_002_amiya").</param>
        /// <returns>The <see cref="CharacterDetail"/> if found; otherwise, null.</returns>
        Task<CharacterDetail?> GetCharacterDetailAsync(string id);

        /// <summary>
        /// Retrieves the complete skill definition table.
        /// </summary>
        /// <returns>A dictionary mapping skill IDs to <see cref="SkillDetail"/> objects, or null if unavailable.</returns>
        Task<Dictionary<string, SkillDetail>?> GetSkillTableAsync();

        /// <summary>
        /// Retrieves the attack range definition table.
        /// </summary>
        /// <returns>A dictionary mapping range IDs to <see cref="RangeData"/> objects, or null if unavailable.</returns>
        Task<Dictionary<string, RangeData>?> GetRangeTableAsync();

        /// <summary>
        /// Retrieves operator module (UniEquip) metadata mapping.
        /// </summary>
        /// <returns>A dictionary mapping module IDs to <see cref="UniEquipData"/> objects, or null if unavailable.</returns>
        Task<Dictionary<string, UniEquipData>?> GetUniEquipTableAsync();

        /// <summary>
        /// Retrieves battle equipment data packs containing module stat upgrades and trait modifiers.
        /// </summary>
        /// <returns>A dictionary mapping battle equipment IDs to <see cref="BattleEquipPack"/> objects, or null if unavailable.</returns>
        Task<Dictionary<string, BattleEquipPack>?> GetBattleEquipTableAsync();

        /// <summary>
        /// Retrieves operator module unlock missions dictionary.
        /// </summary>
        /// <returns>A dictionary mapping mission IDs to <see cref="UniEquipMissionData"/> objects, or null if unavailable.</returns>
        Task<Dictionary<string, UniEquipMissionData>?> GetUniEquipMissionTableAsync();

        /// <summary>
        /// Clears local and in-memory caches, synchronizing fresh game data from the remote source.
        /// </summary>
        Task ResetCache();
    }
}
