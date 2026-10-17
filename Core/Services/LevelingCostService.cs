using Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Services;

public class LevelingCostResult
{
    public int FromLevel { get; set; }
    public int ToLevel { get; set; }
    public int TotalExp { get; set; }
    public int TotalLmd { get; set; }
}

public class LevelingCostData
{
    [JsonPropertyName("maxLevel")]
    public List<List<int>>? MaxLevel { get; set; }

    [JsonPropertyName("characterExpMap")]
    public List<List<int>>? CharacterExpMap { get; set; }

    [JsonPropertyName("characterUpgradeCostMap")]
    public List<List<int>>? CharacterUpgradeCostMap { get; set; }

    [JsonPropertyName("evolveGoldCost")]
    public List<List<int>>? EvolveGoldCost { get; set; }
}

public class LevelingCostService
{
    private static LevelingCostData? _cachedData;
    private static readonly object _lock = new();

    public LevelingCostService()
    {
        EnsureLoaded();
    }

    private void EnsureLoaded()
    {
        if (_cachedData != null) return;

        lock (_lock)
        {
            if (_cachedData != null) return;

            string[] candidatePaths = [
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "data", "aklevel.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aklevel.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "A9Tool", "wwwroot", "data", "aklevel.json")
            ];

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        _cachedData = JsonSerializer.Deserialize<LevelingCostData>(json);
                        if (_cachedData != null) break;
                    }
                    catch
                    {
                        // continue to next candidate
                    }
                }
            }
        }
    }

    public LevelingCostResult CalculateLevelUpCost(int elite, int fromLevel, int toLevel)
    {
        EnsureLoaded();
        var result = new LevelingCostResult
        {
            FromLevel = fromLevel,
            ToLevel = toLevel
        };

        if (toLevel <= fromLevel || elite < 0) return result;

        var expMap = _cachedData?.CharacterExpMap;
        var costMap = _cachedData?.CharacterUpgradeCostMap;

        if (expMap == null || costMap == null || elite >= expMap.Count || elite >= costMap.Count)
        {
            return result;
        }

        var eliteExp = expMap[elite];
        var eliteCost = costMap[elite];

        int totalExp = 0;
        int totalLmd = 0;

        int startIndex = Math.Max(0, fromLevel - 1);
        int endIndex = Math.Min(toLevel - 1, Math.Min(eliteExp.Count, eliteCost.Count));

        for (int i = startIndex; i < endIndex; i++)
        {
            totalExp += eliteExp[i];
            totalLmd += eliteCost[i];
        }

        result.TotalExp = totalExp;
        result.TotalLmd = totalLmd;
        return result;
    }

    public int GetPromotionLmdCost(string? rarity, int targetElite)
    {
        EnsureLoaded();
        if (targetElite <= 0 || targetElite > 2) return 0;

        int rarityIndex = rarity switch
        {
            "TIER_1" => 0,
            "TIER_2" => 1,
            "TIER_3" => 2,
            "TIER_4" => 3,
            "TIER_5" => 4,
            "TIER_6" => 5,
            _ => -1
        };

        if (rarityIndex >= 0 && _cachedData?.EvolveGoldCost != null && rarityIndex < _cachedData.EvolveGoldCost.Count)
        {
            var costs = _cachedData.EvolveGoldCost[rarityIndex];
            int costIdx = targetElite - 1;
            if (costIdx < costs.Count && costs[costIdx] > 0)
            {
                return costs[costIdx];
            }
        }

        // Fallback standard Arknights promotion LMD table
        return (rarityIndex, targetElite) switch
        {
            (2, 1) => 10000,
            (3, 1) => 15000,
            (3, 2) => 60000,
            (4, 1) => 20000,
            (4, 2) => 120000,
            (5, 1) => 30000,
            (5, 2) => 180000,
            _ => 0
        };
    }
}
