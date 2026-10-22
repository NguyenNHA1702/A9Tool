using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Services
{
    public class RecruitmentCombinationResult
    {
        public List<string> Tags { get; set; } = [];
        public List<Character> MatchedOperators { get; set; } = [];
        public int MinRarity { get; set; }
        public int MaxRarity { get; set; }

        public bool IsGuaranteed4Plus => MinRarity >= 4;
        public bool IsGuaranteed5Plus => MinRarity >= 5;
        public bool IsGuaranteed6 => MinRarity == 6;

        public bool Has5Star => MatchedOperators.Any(o => GetTier(o.Rarity) == 5);
        public bool Has6Star => MatchedOperators.Any(o => GetTier(o.Rarity) == 6);

        public int OperatorCount => MatchedOperators.Count;
        public bool IsSoleOperator => OperatorCount == 1;

        public int Star4Count => MatchedOperators.Count(o => GetTier(o.Rarity) == 4);
        public int Star5Count => MatchedOperators.Count(o => GetTier(o.Rarity) == 5);
        public int Star6Count => MatchedOperators.Count(o => GetTier(o.Rarity) == 6);
        public int Star3Count => MatchedOperators.Count(o => GetTier(o.Rarity) == 3);

        /// <summary>
        /// True nếu tổ hợp này 100% CHỈ ra 4★ (không có 5★, không có 3★). Rất có lợi để cày vé/cert.
        /// </summary>
        public bool IsPure4Star => IsGuaranteed4Plus && MaxRarity == 4;

        /// <summary>
        /// Tỉ lệ % tướng 4★ trong tổ hợp này (khi được bảo đảm 4★+).
        /// </summary>
        public double Star4Rate => OperatorCount > 0 ? Math.Round((double)Star4Count / OperatorCount * 100, 1) : 0;

        /// <summary>
        /// Average operator rarity in this combination.
        /// </summary>
        public double AverageRarity => OperatorCount > 0 ? Math.Round(MatchedOperators.Average(o => GetTier(o.Rarity)), 2) : 0;

        /// <summary>
        /// Tỉ lệ % cơ hội trúng một tướng cụ thể trong pool nếu combo là Guaranteed.
        /// </summary>
        public double TargetChance => (IsGuaranteed4Plus || Has6Star) && OperatorCount > 0
            ? Math.Round(100.0 / OperatorCount, 1)
            : 0;

        /// <summary>
        /// Visual probability badge displayed on the combination card.
        /// </summary>
        public string ProbabilityBadge
        {
            get
            {
                if (IsSoleOperator)
                {
                    var sole = MatchedOperators[0];
                    return $"🎯 100% SOLE TARGET ({GetTier(sole.Rarity)}★ {sole.Name})";
                }
                if (Has6Star && MinRarity == 6)
                {
                    return $"★ 100% 6★ TOP ({OperatorCount} operators)";
                }
                if (IsGuaranteed5Plus)
                {
                    return $"★ 100% 5★ SENIOR ({OperatorCount} operators)";
                }
                if (IsPure4Star)
                {
                    return $"🛡️ 100% PURE 4★ ({Star4Count} operators)";
                }
                if (IsGuaranteed4Plus)
                {
                    return $"★ 4★+ GUARANTEED ({Star4Count} 4★ ~ {Star4Rate}%, {Star5Count} 5★)";
                }
                return $"⚠️ CONTAINS 3★ (HIGH RISK)";
            }
        }

        public static int GetTier(string? r) => r switch
        {
            "TIER_6" => 6,
            "TIER_5" => 5,
            "TIER_4" => 4,
            "TIER_3" => 3,
            "TIER_2" => 2,
            "TIER_1" => 1,
            _ => 1
        };
    }

    public class RecruitmentService
    {
        private readonly CharacterService _characterService;
        private readonly ILogger<RecruitmentService> _logger;

        public RecruitmentService(CharacterService characterService, ILogger<RecruitmentService> logger)
        {
            _characterService = characterService;
            _logger = logger;
        }

        public async Task<List<RecruitmentCombinationResult>> CalculateCombinationsAsync(List<string> selectedTags)
        {
            if (selectedTags == null || selectedTags.Count == 0)
            {
                _logger.LogDebug("[Recruit] No tags provided. Returning empty combination list.");
                return [];
            }

            _logger.LogInformation("[Recruit] Calculating combinations for {Count} selected tags: [{Tags}]",
                selectedTags.Count, string.Join(", ", selectedTags));

            var op = await _characterService.GetAllAsync();
            var recruitPool = FilterRecruitmentPool(op);

            if (recruitPool.Count == 0)
            {
                _logger.LogWarning("[Recruit Warning] Filtered recruitment pool contains 0 operators! Check character data and itemObtainApproach.");
            }
            else
            {
                _logger.LogInformation("[Recruit] Loaded {Total} characters -> {PoolCount} operators in recruitment pool.", op.Count, recruitPool.Count);
            }

            var combos = GenerateCombinations(selectedTags, 3);
            _logger.LogDebug("[Recruit Combos] Generated {ComboCount} tag combinations (k = 1..3) from {TagCount} tags.", combos.Count, selectedTags.Count);

            var results = new List<RecruitmentCombinationResult>();

            foreach(var c in combos)
            {
                bool hasTopOp = c.Contains("Top Operator", StringComparer.OrdinalIgnoreCase);

                var matched = recruitPool.Where(op =>
                {
                    int tier = RecruitmentCombinationResult.GetTier(op.Rarity);

                    // Luật sống còn: 6★ chỉ ra khi có tag Top Operator
                    if (tier == 6 && !hasTopOp) return false;

                    var opTags = GetOperatorAllTags(op);
                    return c.All(tag => opTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
                }).ToList();

                if (matched.Count > 0)
                {
                    int minRarity = matched.Min(o => RecruitmentCombinationResult.GetTier(o.Rarity));
                    int maxRarity = matched.Max(o => RecruitmentCombinationResult.GetTier(o.Rarity));

                    results.Add(new RecruitmentCombinationResult
                    {
                        Tags = c,
                        MatchedOperators = matched.OrderByDescending(o => RecruitmentCombinationResult.GetTier(o.Rarity)).ToList(),
                        MinRarity = minRarity,
                        MaxRarity = maxRarity
                    });
                }
            }

            int top6Count = results.Count(r => r.Has6Star);
            int senior5Count = results.Count(r => r.IsGuaranteed5Plus);
            int pure4Count = results.Count(r => r.IsPure4Star);
            int guar4Count = results.Count(r => r.IsGuaranteed4Plus);
            int soleCount = results.Count(r => r.IsSoleOperator);

            _logger.LogInformation("[Recruit Results] Evaluated {TotalCombos} matched combinations (Sole: {SoleCount}, 6★: {Top6}, 5★: {Senior5}, Pure 4★: {Pure4}, 4★+: {Guar4}).",
                results.Count, soleCount, top6Count, senior5Count, pure4Count, guar4Count);

            return results
                .OrderByDescending(r => r.Has6Star)
                .ThenByDescending(r => r.IsGuaranteed5Plus)
                .ThenByDescending(r => r.IsGuaranteed4Plus)
                .ThenByDescending(r => r.IsSoleOperator)
                .ThenBy(r => r.OperatorCount)
                .ThenBy(r => r.Tags.Count)
                .ToList();
        }

        public List<List<string>> GenerateCombinations(List<string> list, int maxK)
        {
            var result = new List<List<string>>();
            int n = list.Count;
            int limit = Math.Min(n, maxK);

            for (int k = 1; k <= limit; k++)
            {
                GenerateKCombinations(list, 0, k, new List<string>(), result);
            }

            return result;
        }

        public void GenerateKCombinations(List<string> list, int start, int k, List<string> current, List<List<string>> result)
        {
            if (current.Count == k)
            {
                result.Add(new List<string>(current));
                return;
            }

            for (int i = start; i < list.Count; i++)
            {
                current.Add(list[i]);
                GenerateKCombinations(list, i + 1, k, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }
        private static long Combination(int n, int k)
        {
            long res = 1;
            for (int i = 1; i <= k; i++) res = res * (n - i + 1) / i;
            return res;
        }

        public List<Character> FilterRecruitmentPool(List<Character> allOps)
        {
            return allOps.Where(a => !string.IsNullOrEmpty(a.Name) && 
                                     !string.IsNullOrEmpty(a.ItemObtainApproach) && 
                                     (a.ItemObtainApproach.Contains("Recruitment", StringComparison.OrdinalIgnoreCase) || 
                                      a.ItemObtainApproach.Contains("公开招募"))).ToList();
        }

        public static HashSet<string> GetOperatorAllTags(Character op)
        {
            var tags = new HashSet<string>(op.Tags ?? [], StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(op.Position))
                tags.Add(FormatPosition(op.Position));

            if (!string.IsNullOrEmpty(op.Profession))
                tags.Add(FormatProfession(op.Profession));

            int tier = RecruitmentCombinationResult.GetTier(op.Rarity);
            if (tier == 6) tags.Add("Top Operator");
            if (tier == 5) tags.Add("Senior Operator");
            if (tier == 2) tags.Add("Starter");
            if (tier == 1) tags.Add("Robot");

            return tags;
        }

        private static string FormatPosition(string p) =>
            p.Equals("MELEE", StringComparison.OrdinalIgnoreCase) ? "Melee" : "Ranged";

        private static string FormatProfession(string p) => p switch
        {
            "PIONEER" => "Vanguard",
            "WARRIOR" => "Guard",
            "TANK" => "Defender",
            "SNIPER" => "Sniper",
            "CASTER" => "Caster",
            "MEDIC" => "Medic",
            "SUPPORT" => "Supporter",
            "SPECIAL" => "Specialist",
            _ => p
        };
    }
}
    