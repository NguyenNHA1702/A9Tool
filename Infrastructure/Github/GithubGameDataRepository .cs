using Core.Interfaces;
using Core.Models.Entities;
using Infrastructure.Github.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Json;
using System.Text;

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

            try
            {
                Dictionary<string, Character> respone;
                if (File.Exists(localpath) && File.GetLastWriteTime(localpath) > DateTime.Now.AddDays(-1))
                {
                    _logger.LogInformation("Loading character data from local file: {FilePath}", localpath);
                    var json = await File.ReadAllTextAsync(localpath, Encoding.UTF8);
                    respone = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Character>>(json);
                }

                else
                {
                    _logger.LogInformation("Fetching character data from GitHub: {Url}", url);
                    respone = await _httpClient.GetFromJsonAsync<Dictionary<string, Character>>(url);

                    var json = System.Text.Json.JsonSerializer.Serialize(respone, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(localpath, json, Encoding.UTF8);
                }


                _logger.LogInformation("Fetched character data: {Count} characters", respone.Count);

                return respone
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching character data from GitHub");
                throw;
            }
        }

        public async Task<CharacterDetail?> GetCharacterDetailAsync(string id)
        {
            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_table_full.json"); // ← file riêng
            var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/character_table.json";

            try
            {
                Dictionary<string, CharacterDetail>? response;

                if (File.Exists(localpath) && File.GetLastWriteTime(localpath) > DateTime.Now.AddDays(-1))
                {
                    var json = await File.ReadAllTextAsync(localpath, Encoding.UTF8);
                    response = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, CharacterDetail>>(json);
                }
                else
                {
                    response = await _httpClient.GetFromJsonAsync<Dictionary<string, CharacterDetail>>(url);
                    var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(localpath, json, Encoding.UTF8); // ← lưu cache mới
                }

                if (response == null || !response.ContainsKey(id)) return null;

                var raw = response[id];
                raw.Id = id;
                return raw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching character detail for {Id}", id);
                throw;
            }
        }

        public async Task<Dictionary<string, SkillDetail>?> GetSkillTableAsync()
        {
            string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skill_table.json");
            var url = "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/skill_table.json";

            try
            {
                if (File.Exists(localpath) && File.GetLastWriteTime(localpath) > DateTime.Now.AddDays(-1))
                {
                    var json = await File.ReadAllTextAsync(localpath, Encoding.UTF8);
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, SkillDetail>>(json);
                }

                var response = await _httpClient.GetFromJsonAsync<Dictionary<string, SkillDetail>>(url);
                var jsonOut = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(localpath, jsonOut, Encoding.UTF8);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching skill table");
                throw;
            }
        }
    }
}
