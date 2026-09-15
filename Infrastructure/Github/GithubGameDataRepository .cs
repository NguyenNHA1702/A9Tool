using Core.Github.DTOs.Response;
using Core.Interfaces;
using Core.Models.Entities;
using Infrastructure.FlatBuffers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Infrastructure.Github
{
    public class GithubGameDataRepository : IGameDataRepository
    {
        private readonly HttpClient _httpClient;
        private readonly string _filePath;
        private readonly ILogger<GithubGameDataRepository> _logger;
        private readonly FlatBuffersConverter _flatBuffersConverter;

        // Reference Data Source URLs:
        // - Primary new repo base: https://github.com/ArknightsAssets/ArknightsAssets2/tree/en/assets/dyn/gamedata/excel
        private const string GITHUB_API_EXCEL_CONTENTS_URL = "https://api.github.com/repos/ArknightsAssets/ArknightsAssets2/contents/assets/dyn/gamedata/excel?ref=en";

        // Legacy / Fallback Reference URLs:
        // - Character table (EN): https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/character_table.json
        // - Character table (CN): https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/character_table.json
        // - Skill table (EN): https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/skill_table.json
        // - Range table (EN): https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/range_table.json

        public GithubGameDataRepository(HttpClient httpClient, ILogger<GithubGameDataRepository> logger, string filePath)
        {
            _httpClient = httpClient;
            _logger = logger;
            _filePath = filePath;
            _flatBuffersConverter = new FlatBuffersConverter(logger);

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "A9Tool-ArknightsDataFetcher/1.0");
            }
        }

        private string GetSchemaPath(string schemaFileName)
        {
            // Primary path in build output directory
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FlatBuffers", "Schemas", schemaFileName);
            if (File.Exists(basePath))
            {
                return basePath;
            }

            // Fallback path in project workspace directory
            string workspacePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Infrastructure", "FlatBuffers", "Schemas", schemaFileName);
            string fullWorkspacePath = Path.GetFullPath(workspacePath);
            if (File.Exists(fullWorkspacePath))
            {
                return fullWorkspacePath;
            }

            return basePath;
        }

        private static List<GitHubContentsItem>? _memoryDirectoryCache;
        private static DateTime _memoryDirectoryCacheTime = DateTime.MinValue;

        private async Task<List<GitHubContentsItem>?> GetExcelDirectoryContentsAsync()
        {
            // 1. Check in-memory cache (valid for 30 minutes)
            if (_memoryDirectoryCache != null && (DateTime.Now - _memoryDirectoryCacheTime).TotalMinutes < 30)
            {
                _logger.LogInformation("[GitHub API Cache Hit] Using in-memory directory listing cache (Age: {Age:F1} mins)",
                    (DateTime.Now - _memoryDirectoryCacheTime).TotalMinutes);
                return _memoryDirectoryCache;
            }

            // 2. Check local disk cache (valid for 1 hour)
            string diskCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "github_excel_contents_cache.json");
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (File.Exists(diskCachePath) && File.GetLastWriteTime(diskCachePath) > DateTime.Now.AddHours(-1))
            {
                try
                {
                    _logger.LogInformation("[GitHub API Cache Hit] Loading directory listing from local disk cache: {DiskCachePath}", diskCachePath);
                    var json = await File.ReadAllTextAsync(diskCachePath, Encoding.UTF8);
                    var cachedItems = JsonSerializer.Deserialize<List<GitHubContentsItem>>(json, opts);
                    if (cachedItems != null && cachedItems.Count > 0)
                    {
                        _memoryDirectoryCache = cachedItems;
                        _memoryDirectoryCacheTime = File.GetLastWriteTime(diskCachePath);
                        return cachedItems;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GitHub API Cache] Error reading disk cache for directory contents.");
                }
            }

            // 3. Query GitHub API
            _logger.LogInformation("[GitHub API] Querying GitHub Contents API at {ApiUrl}", GITHUB_API_EXCEL_CONTENTS_URL);

            try
            {
                var items = await _httpClient.GetFromJsonAsync<List<GitHubContentsItem>>(GITHUB_API_EXCEL_CONTENTS_URL, opts);

                if (items != null && items.Count > 0)
                {
                    _memoryDirectoryCache = items;
                    _memoryDirectoryCacheTime = DateTime.Now;

                    try
                    {
                        var json = JsonSerializer.Serialize(items, opts);
                        await File.WriteAllTextAsync(diskCachePath, json, Encoding.UTF8);
                        _logger.LogInformation("[GitHub API Cache] Saved fresh directory listing ({Count} items) to disk cache.", items.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[GitHub API Cache] Failed to save directory listing disk cache.");
                    }

                    return items;
                }
            }
            catch (HttpRequestException httpe) when (httpe.StatusCode == System.Net.HttpStatusCode.Forbidden || httpe.Message.Contains("403") || httpe.Message.Contains("rate limit"))
            {
                _logger.LogWarning("[GitHub API Rate Limit] GitHub API rate limit (403) reached. Attempting fallback to stale disk cache if available...");

                if (File.Exists(diskCachePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(diskCachePath, Encoding.UTF8);
                        var staleItems = JsonSerializer.Deserialize<List<GitHubContentsItem>>(json, opts);
                        if (staleItems != null && staleItems.Count > 0)
                        {
                            _logger.LogInformation("[GitHub API Rate Limit Fallback] Using stale disk cache containing {Count} items.", staleItems.Count);
                            return staleItems;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[GitHub API Rate Limit Fallback] Failed to load stale directory disk cache.");
                    }
                }

                _logger.LogError("[GitHub API Rate Limit] 403 Rate Limit Exceeded and no directory cache available on disk.");
                throw;
            }

            return null;
        }

        private async Task<string?> FindLatestBytesDownloadUrlAsync(string filePrefix)
        {
            try
            {
                var items = await GetExcelDirectoryContentsAsync();

                if (items == null || items.Count == 0)
                {
                    _logger.LogWarning("[GitHub API] Directory listing returned no items.");
                    return null;
                }

                _logger.LogInformation("[GitHub API] Searching among {Count} directory items for prefix '{Prefix}'...", items.Count, filePrefix);

                var matchingItems = items
                    .Where(x => x.Name.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase) && x.Name.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Size)
                    .ToList();

                if (matchingItems.Count == 0)
                {
                    _logger.LogWarning("[GitHub API] No matching .bytes files found for prefix '{Prefix}'.", filePrefix);
                    return null;
                }

                var selected = matchingItems.First();
                _logger.LogInformation("[GitHub API] Selected latest binary file for '{Prefix}': {Name} (Size: {Size} bytes) -> Download URL: {Url}",
                    filePrefix, selected.Name, selected.Size, selected.DownloadUrl);

                return selected.DownloadUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GitHub API] Error finding latest bytes download URL for prefix '{Prefix}'.", filePrefix);
                throw;
            }
        }

        private static Dictionary<string, T> ParseFlatBuffersJsonToDictionary<T>(string rawJson, JsonSerializerOptions opts)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new Dictionary<string, T>();
            }

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        return ConvertKVArrayToDictionary<T>(prop.Value, opts);
                    }
                }

                // Standard dictionary JSON format
                return JsonSerializer.Deserialize<Dictionary<string, T>>(rawJson, opts) ?? new Dictionary<string, T>();
            }

            return new Dictionary<string, T>();
        }

        private static Dictionary<string, T> ConvertKVArrayToDictionary<T>(JsonElement arrayElement, JsonSerializerOptions opts)
        {
            var dict = new Dictionary<string, T>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("key", out var keyProp) && item.TryGetProperty("value", out var valProp))
                {
                    string? key = keyProp.GetString();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var valObj = JsonSerializer.Deserialize<T>(valProp.GetRawText(), opts);
                        if (valObj != null)
                        {
                            dict[key] = valObj;
                        }
                    }
                }
            }
            return dict;
        }

        private static Dictionary<string, Character>? _cachedCharacters;
        private static Dictionary<string, CharacterDetail>? _cachedCharacterDetails;
        private static Dictionary<string, SkillDetail>? _cachedSkillTable;
        private static Dictionary<string, RangeData>? _cachedRangeTable;
        private static Dictionary<string, UniEquipData>? _cachedUniEquipData;
        private static Dictionary<string, BattleEquipPack>? _cachedBattleEquipData;

        private async Task<Dictionary<string, T>?> LoadTableAsync<T>(string localPath, string filePrefix, string schemaFileName)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // FlatBuffers JSON outputs enums as either strings (e.g., "PHASE_2") or integers.
                // JsonStringEnumConverter(namingPolicy: null) preserves original enum names while allowIntegerValues allows reading numeric representations.
                Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true) }
            };

            try
            {
                // 1. Check local cache
                if (File.Exists(localPath) && File.GetLastWriteTime(localPath) > DateTime.Now.AddDays(-1))
                {
                    _logger.LogInformation("[Cache Hit] Loading local cache for '{Prefix}' from: {LocalPath} (Last modified: {LastWrite})",
                        filePrefix, localPath, File.GetLastWriteTime(localPath));
                    var json = await File.ReadAllTextAsync(localPath, Encoding.UTF8);
                    return ParseFlatBuffersJsonToDictionary<T>(json, opts);
                }

                // 2. Fetch latest FlatBuffers binary from GitHub
                _logger.LogInformation("[Cache Miss/Expired] Cache for '{Prefix}' at '{LocalPath}' is missing or older than 1 day. Fetching fresh data from GitHub...", filePrefix, localPath);

                string? downloadUrl = await FindLatestBytesDownloadUrlAsync(filePrefix);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new InvalidOperationException($"Could not find latest .bytes file URL for '{filePrefix}' on GitHub.");
                }

                _logger.LogInformation("[Download] Downloading FlatBuffers binary data from {Url}...", downloadUrl);
                byte[] bytesData = await _httpClient.GetByteArrayAsync(downloadUrl);
                _logger.LogInformation("[Download] Successfully downloaded {ByteCount} bytes for '{Prefix}'.", bytesData.Length, filePrefix);

                // 3. Convert FlatBuffers binary to JSON using schema
                string schemaPath = GetSchemaPath(schemaFileName);
                _logger.LogInformation("[FlatBuffers] Using schema file at '{SchemaPath}' to convert binary to JSON...", schemaPath);

                string? convertedJson = await _flatBuffersConverter.ConvertToJsonAsync(bytesData, schemaPath);

                if (string.IsNullOrEmpty(convertedJson))
                {
                    throw new InvalidOperationException($"FlatBuffers conversion returned empty JSON for '{filePrefix}'.");
                }

                _logger.LogInformation("[FlatBuffers] Conversion successful for '{Prefix}'. Parsing JSON and saving cache to '{LocalPath}'...", filePrefix, localPath);

                // 4. Parse converted JSON into C# dictionary model
                var result = ParseFlatBuffersJsonToDictionary<T>(convertedJson, opts);

                // 5. Save normalized JSON dictionary format to local cache
                var normalizedCacheJson = JsonSerializer.Serialize(result, opts);
                await File.WriteAllTextAsync(localPath, normalizedCacheJson, Encoding.UTF8);

                _logger.LogInformation("[Success] Loaded {Count} entries for '{Prefix}'.", result?.Count ?? 0, filePrefix);

                return result;
            }
            catch (HttpRequestException httpe)
            {
                _logger.LogError(httpe, "[Network Error] HTTP request failed while fetching '{Prefix}'. Checking local cache fallback...", filePrefix);

                if (File.Exists(localPath))
                {
                    _logger.LogWarning("[Fallback] Network unavailable. Loading existing local cache for '{Prefix}' from: {LocalPath}", filePrefix, localPath);
                    try { File.SetLastWriteTime(localPath, DateTime.Now); } catch { }
                    var json = await File.ReadAllTextAsync(localPath, Encoding.UTF8);
                    return ParseFlatBuffersJsonToDictionary<T>(json, opts);
                }

                _logger.LogError("[Fallback Error] No local cache found at '{LocalPath}' for '{Prefix}'.", localPath, filePrefix);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Exception occurred while loading table for '{Prefix}' ({Type}).", filePrefix, typeof(T).Name);

                if (File.Exists(localPath))
                {
                    _logger.LogWarning("[Fallback] Exception encountered. Loading available local cache for '{Prefix}' from: {LocalPath}", filePrefix, localPath);
                    try { File.SetLastWriteTime(localPath, DateTime.Now); } catch { }
                    var json = await File.ReadAllTextAsync(localPath, Encoding.UTF8);
                    return ParseFlatBuffersJsonToDictionary<T>(json, opts);
                }

                throw;
            }
        }

        public async Task DownloadAllAvatarAsync(List<Character> characters, string webRootPath)
        {
            var folderPath = Path.Combine(webRootPath, "images", "avatars");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            _logger.LogInformation("[Avatars] Starting avatar download for {Count} operators into '{FolderPath}'...", characters.Count, folderPath);

            foreach (var op in characters)
            {
                if (string.IsNullOrEmpty(op.Id)) continue;

                var imgId = op.Id.ToLower();
                string fileFullPath = Path.Combine(folderPath, $"{imgId}.png");

                if (File.Exists(fileFullPath)) continue;

                try
                {
                    // New ArknightsAssets2 character avatar URL format:
                    string url = $"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/en/assets/dyn/arts/characters/{imgId}/{imgId}_1%2B.png";

                    _logger.LogDebug("[Avatars] Downloading avatar for {Name} ({Id}) from {Url}", op.Name, op.Id, url);
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(fileFullPath, imageBytes);

                    _logger.LogInformation("[Avatars] Saved offline avatar: {Name}", op.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Avatars] Failed to download avatar for {Name}: {Message}", op.Name, ex.Message);
                }
            }
        }

        public async Task<Dictionary<string, Character>> GetCharactersAsync()
        {
            if (_cachedCharacters != null)
            {
                return _cachedCharacters;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table.json");

            var response = await LoadTableAsync<Character>(localpath, "character_table", "character_table.fbs");

            if (response == null)
            {
                _logger.LogWarning("[GetCharactersAsync] Response returned null.");
                return new Dictionary<string, Character>();
            }

            _logger.LogInformation("[GetCharactersAsync] Fetched total character raw entries: {Count}", response.Count);

            var filtered = response
                .Where(x => x.Key.StartsWith("char_"))
                .ToDictionary(x => x.Key, x => new Character
                {
                    Id = x.Key,
                    Name = x.Value.Name,
                    Rarity = string.IsNullOrEmpty(x.Value.Rarity) ? "TIER_1" : x.Value.Rarity,
                    Profession = x.Value.Profession,
                    NationId = x.Value.NationId,
                    Position = x.Value.Position,
                    Tags = x.Value.Tags
                });

            _logger.LogInformation("[GetCharactersAsync] Filtered operator entries (char_*): {Count}", filtered.Count);
            _cachedCharacters = filtered;
            return filtered;
        }

        public async Task<CharacterDetail?> GetCharacterDetailAsync(string id)
        {
            if (_cachedCharacterDetails != null && _cachedCharacterDetails.TryGetValue(id, out var cachedDetail))
            {
                return cachedDetail;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table_full.json");

            var response = await LoadTableAsync<CharacterDetail>(localpath, "character_table", "character_table.fbs");

            if (response == null || !response.ContainsKey(id))
            {
                _logger.LogWarning("[GetCharacterDetailAsync] Operator details not found for ID: {Id}", id);
                return null;
            }

            _cachedCharacterDetails = response;

            var raw = response[id];
            raw.Id = id;
            if (string.IsNullOrEmpty(raw.Rarity)) raw.Rarity = "TIER_1";
            _logger.LogInformation("[GetCharacterDetailAsync] Loaded operator detail for {Name} ({Id})", raw.Name, id);
            return raw;
        }

        public async Task<Dictionary<string, SkillDetail>?> GetSkillTableAsync()
        {
            if (_cachedSkillTable != null)
            {
                return _cachedSkillTable;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");

            var response = await LoadTableAsync<SkillDetail>(localpath, "skill_table", "skill_table.fbs");
            _logger.LogInformation("[GetSkillTableAsync] Loaded skill table entries: {Count}", response?.Count ?? 0);
            _cachedSkillTable = response;
            return response;
        }

        public async Task<Dictionary<string, RangeData>?> GetRangeTableAsync()
        {
            if (_cachedRangeTable != null)
            {
                return _cachedRangeTable;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "range_table.json");

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (File.Exists(localpath))
            {
                try
                {
                    _logger.LogInformation("[GetRangeTableAsync] Loading range table directly from local cache: {LocalPath}", localpath);
                    var json = await File.ReadAllTextAsync(localpath, Encoding.UTF8);
                    var rangeDict = ParseFlatBuffersJsonToDictionary<RangeData>(json, opts);
                    if (rangeDict != null && rangeDict.Count > 0)
                    {
                        _logger.LogInformation("[GetRangeTableAsync] Successfully loaded {Count} range entries.", rangeDict.Count);
                        _cachedRangeTable = rangeDict;
                        return rangeDict;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GetRangeTableAsync] Failed to read local range_table.json cache.");
                }
            }

            // 1. Primary Source: Try fetching from ArknightsAssets2 via FlatBuffers
            try
            {
                _logger.LogInformation("[GetRangeTableAsync] Attempting to fetch range_table from primary source (ArknightsAssets2)...");
                var response = await LoadTableAsync<RangeData>(localpath, "range_table", "range_table.fbs");
                if (response != null && response.Count > 0)
                {
                    _logger.LogInformation("[GetRangeTableAsync] Successfully loaded {Count} range entries from ArknightsAssets2.", response.Count);
                    _cachedRangeTable = response;
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetRangeTableAsync] Primary source (ArknightsAssets2) flatc conversion/download failed. Trying raw JSON fallback...");
            }

            // 2. Fallback Source: Raw JSON fetch
            try
            {
                string rawJsonUrl = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/range_table.json";
                _logger.LogInformation("[GetRangeTableAsync] Fetching raw JSON fallback: {Url}", rawJsonUrl);
                var json = await _httpClient.GetStringAsync(rawJsonUrl);
                var rangeDict = JsonSerializer.Deserialize<Dictionary<string, RangeData>>(json, opts);
                if (rangeDict != null && rangeDict.Count > 0)
                {
                    await File.WriteAllTextAsync(localpath, json, Encoding.UTF8);
                    _cachedRangeTable = rangeDict;
                    _logger.LogInformation("[GetRangeTableAsync] Successfully fetched and cached {Count} range entries via fallback.", rangeDict.Count);
                    return rangeDict;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetRangeTableAsync] Failed to fetch raw range_table.json fallback.");
            }

            return null;
        }

        public async Task<Dictionary<string, UniEquipData>?> GetUniEquipTableAsync()
        {
            if (_cachedUniEquipData != null)
            {
                return _cachedUniEquipData;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uniequip_table.json");

            var response = await LoadTableAsync<UniEquipData>(localpath, "uniequip_table", "uniequip_table.fbs");
            _logger.LogInformation("[GetUniEquipTableAsync] Loaded {Count} UniEquip records. IDs: {Ids}", response?.Count, string.Join(", ", response?.Keys?.Take(5)));
            _cachedUniEquipData = response;
            return response;
        }

        public async Task<Dictionary<string, BattleEquipPack>?> GetBattleEquipTableAsync()
        {
            if (_cachedBattleEquipData != null)
            {
                return _cachedBattleEquipData;
            }

            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battle_equip_table.json");

            var response = await LoadTableAsync<BattleEquipPack>(localpath, "battle_equip_table", "battle_equip_table.fbs");

            _logger.LogInformation(
                "[GetBattleEquipTableAsync] Loaded {Count} BattleEquip records. IDs: {Ids}", response?.Count, string.Join(", ", response?.Keys?.Take(5) ?? []));

            _cachedBattleEquipData = response;
            return response;
        }

        public async Task ResetCache()
        {
            _cachedCharacters = null;
            _cachedCharacterDetails = null;
            _cachedSkillTable = null;
            _cachedRangeTable = null;
            _cachedUniEquipData = null;
            _cachedBattleEquipData = null;
            _memoryDirectoryCache = null;

            string localpath1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table.json");
            string localpath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table_full.json");
            string localpath3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");
            string localpath4 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uniequip_table.json");
            string localpath5 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battle_equip_table.json");
            // NOTE: range_table.json contains static game definitions (72 entries, invariant across game updates).
            // Newer .bytes files cannot be decoded via flatc, so the static local cache must NOT be deleted.

            _logger.LogInformation("[ResetCache] Resetting local data cache (character, skill). Range table is static and will NOT be cleared.");

            if (File.Exists(localpath1))
            {
                File.Delete(localpath1);
                _logger.LogInformation("[ResetCache] Deleted local cache: character_table.json");
            }
            if (File.Exists(localpath2))
            {
                File.Delete(localpath2);
                _logger.LogInformation("[ResetCache] Deleted local cache: character_table_full.json");
            }
            if (File.Exists(localpath3))
            {
                File.Delete(localpath3);
                _logger.LogInformation("[ResetCache] Deleted local cache: skill_table.json");
            }
            if (File.Exists(localpath4))
            {
                File.Delete(localpath4);
                _logger.LogInformation("[ResetCache] Deleted local cache: uniequip_table.json");
            }
            if (File.Exists(localpath5))
            {
                File.Delete(localpath5);
                _logger.LogInformation("[ResetCache] Deleted local cache: battle_equip_table.json");
            }

            _logger.LogInformation("[ResetCache] Fetching fresh data from GitHub...");
            await GetCharactersAsync();
            await GetSkillTableAsync();
            await GetUniEquipTableAsync();
            await GetBattleEquipTableAsync();
            // NOTE: Do not call GetRangeTableAsync() here because range_table relies on static local cache
            // and avoids flatc execution to prevent crash exit code -1073741819.
            _logger.LogInformation("[ResetCache] Cache reset complete.");
        }
    }
}
