using Core.Github.DTOs;
using Core.Github.DTOs.Response;
using Core.Interfaces;
using Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Github
{
    public class GithubGameDataRepository : IGameDataRepository
    {
        private readonly HttpClient _httpClient;
        private readonly string _filePath;
        private readonly ILogger<GithubGameDataRepository> _logger;

        public GithubGameDataRepository(HttpClient httpClient, ILogger<GithubGameDataRepository> logger, string filePath)
        {
            _httpClient = httpClient;
            _logger = logger;
            _filePath = filePath;
        }

        private async Task<Dictionary<string, T>?> LoadTableAsync<T>(string localPath, string url)
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                Dictionary<string, T> response;
                if (File.Exists(localPath) && File.GetLastWriteTime(localPath) > DateTime.Now.AddDays(-1))
                {
                    _logger.LogInformation("Loading cache: {file}", Path.GetFileName(localPath));
                    var json = await File.ReadAllTextAsync(localPath, Encoding.UTF8);
                    response = JsonSerializer.Deserialize<Dictionary<string, T>>(json, opts);
                }
                else
                {
                    _logger.LogInformation("Loading from git: {link}", url);
                    response = await _httpClient.GetFromJsonAsync<Dictionary<string, T>>(url, opts);

                    var json = JsonSerializer.Serialize(response, opts);
                    await File.WriteAllTextAsync(localPath, json, Encoding.UTF8);
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError("Error getting {Type}", typeof(T).Name);
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
            // Vẫn nên giữ User-Agent để GitHub không hiểu lầm code của bạn là công cụ tấn công DDoS
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            foreach (var op in characters)
            {
                if (string.IsNullOrEmpty(op.Id)) continue;

                var imgId = op.Id.ToLower();
                string fileFullPath = Path.Combine(folderPath, $"{imgId}.png");

                // Nếu đã có file offline rồi thì bỏ qua, không tải lại
                if (File.Exists(fileFullPath)) continue;

                try
                {
                    // Đảm bảo imgId của bạn có định dạng giống như: "char_002_amiya"
                    string url = $"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/en/assets/dyn/arts/characters/{imgId}/{imgId}_1%2B.png";

                    byte[] imageBytes = await httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(fileFullPath, imageBytes);

                    Console.WriteLine($"Đã lưu offline avatar: {op.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Bỏ qua {op.Name} do lỗi: {ex.Message}");
                }
            }
        }

        public async Task<Dictionary<string, Character>> GetCharactersAsync()
        {
            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table.json");

            //var url = "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/character_table.json";

            var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/character_table.json";

            var response = await LoadTableAsync<Character>(localpath, url);


            _logger.LogInformation("Fetched character data: {Count} characters", response.Count);

            return response
                .Where(x => x.Key.StartsWith("char_"))
                .ToDictionary(x => x.Key, x => new Character
                {
                    Id = x.Key,
                    Name = x.Value.Name,
                    Rarity = x.Value.Rarity,
                    Profession = x.Value.Profession,
                    NationId = x.Value.NationId,
                    Position = x.Value.Position,
                    Tags = x.Value.Tags
                });
        }

        public async Task<CharacterDetail?> GetCharacterDetailAsync(string id)
        {
            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table_full.json"); // ← file riêng
            var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/character_table.json";


            var response = await LoadTableAsync<CharacterDetail>(localpath, url);

            if (response == null || !response.ContainsKey(id)) return null;

            var raw = response[id];
            raw.Id = id;
            return raw;

        }

        public async Task<Dictionary<string, SkillDetail>?> GetSkillTableAsync()
        {
            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");
            var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/skill_table.json";

            var response = await LoadTableAsync<SkillDetail>(localpath, url);
            return response;
        }

        //public async Task<SkillsForOperatorResponse?> GetSkillByIdAsync(string id)
        //{

        //    string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");
        //    var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/skill_table.json";

        //    var response = await LoadTableAsync<SkillsForOperatorResponse>(localpath, url);
        //    return response[id];
        //}

        public async Task ResetCache()
        {
            string localpath1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table.json");
            string localpath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table_full.json");
            string localpath3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");

            if (File.Exists(localpath1))
            {
                File.Delete(localpath1);

                _logger.LogInformation("Fetching new character data");
                await GetCharactersAsync();
            }
            if (File.Exists(localpath2))
            {
                File.Delete(localpath2);

                _logger.LogInformation("Fetching new character detail data");
                await GetCharactersAsync();
            }
            if (File.Exists(localpath3))
            {
                File.Delete(localpath3);

                _logger.LogInformation("Fetching new character skill data");
                await GetSkillTableAsync();
            }
        }
    }
}
