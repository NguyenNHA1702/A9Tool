using Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Services
{
    /// <summary>
    /// Core domain service responsible for fetching, structuring, and parsing Arknights story content,
    /// timeline hierarchies, character operator records, and multimedia playback assets (BGM & PVs).
    /// </summary>
    public class StoryService
    {
        private readonly HttpClient _httpClient;
        private readonly CharacterService _characterService;
        private readonly ILogger<StoryService> _logger;

        // CDN Base URLs for visual and audio media assets (Routed through fast jsdelivr edge cache to bypass ISP restrictions)
        private const string AVATAR_CDN_BASE = "https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/avatars";
        private const string BG_MAIN_BANNER_BASE = "https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/ui/stage/banner/main";
        private const string CHAR_SPLASH_BASE = "https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/characters";
        private const string AVG_BG_CDN_BASE = "https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/avg/backgrounds";
        private const string AVG_IMAGE_CDN_BASE = "https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/avg/images";

        // Remote script repositories (YoStar EN & Upstream CN, jsdelivr first for speed & reliability)
        private static readonly string[] ScriptBaseUrls =
        [
            "https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData_YoStar@main/en_US/gamedata/story",
            "https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData@master/zh_CN/gamedata/story",
            "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/main/en_US/gamedata/story",
            "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/story"
        ];

        // Verified Official YouTube PV Video IDs
        private static readonly Dictionary<string, string> EventYouTubePvMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["act13side"] = "s_6iLn-my9I", // Darknights Memoir
            ["act9d0"] = "jfBBEoJ_e4A",    // Code of Brawl
            ["act17side"] = "U_laI-G21fo", // Under Tides
            ["act20side"] = "7qS2N8P-OMQ", // Near Light
            ["act28side"] = "OKNlnmqGd9A", // Il Siracusano
            ["act30side"] = "U_laI-G21fo", // Lone Trail
            ["act34side"] = "ptTcDSQF8nw"  // Babel
        };

        // Official High-Speed Monster Siren Records (MSR) Audio Streams
        // Working Arknights Canonical Audio CDN Streams (via raw.githubusercontent.com with Byte-Range audio support)
        private static readonly Dictionary<string, (string Url, string Title)> BgmTracks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["$babel_loop"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/a001/m_dia_path_loop.wav", "Babel - Path to Dawn (Awakening)"),
            ["$ekg_loop"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/avg/m_avg_calm_loop.wav", "Emergency Resuscitation (Heartbeat & EKG)"),
            ["$darkness01_loop"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act9d0d0/m_avg_plot_loop.wav", "Chernobog Ruins - Dark Suspense"),
            ["$darkness01_intro"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act9d0d0/m_avg_plot_loop.wav", "Chernobog Ruins - Dark Suspense"),
            ["$default_ambient"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/avg/m_avg_normal_loop.wav", "Rhodes Island - Ambient Routine"),
            ["$escape_loop"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/static/m_avg/m_avg_exciting01_loop.wav", "Escape from Chernobog (Gearing Up)"),
            ["$escape_intro"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/static/m_avg/m_avg_exciting01_loop.wav", "Escape from Chernobog (Gearing Up)"),
            ["$battle_01"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act9d2d0/m_bat_chernobog_loop.wav", "Boiling Blood (Chernobog Combat)"),
            ["$loneliness"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/static/m_avg/m_avg_loneliness_loop.wav", "Reminiscence (Tears of Sorrow)"),
            ["$act13side_01"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act13side/m_bat_act13side_01_loop.wav", "Darknights Memoir (Babel Chronicles)"),
            ["$act17side_01"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act17side/m_bat_act17side_01_loop.wav", "Under Tides (Abyssal Hymn)"),
            ["$suspense"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act9d0d0/m_avg_distressed_loop.wav", "Impending Peril (Distress)"),
            ["$nervous"] = ("https://raw.githubusercontent.com/PseudoMon/arknights-audio/global-server-voices/music/act9d0d0/m_avg_nervous_loop.wav", "Chernobog Ambush (High Alert)")
        };

        public StoryService(HttpClient httpClient, CharacterService characterService, ILogger<StoryService> logger)
        {
            _httpClient = httpClient;
            _characterService = characterService;
            _logger = logger;
        }

        #region Phase 1: Timeline Hierarchy Generation

        /// <summary>
        /// Retrieves the comprehensive 3-tier timeline hierarchy grouped by Act (Main Theme -> Linked Events -> Operator Records).
        /// </summary>
        public async Task<List<StoryActGroup>> GetTimelineHierarchyAsync()
        {
            _logger.LogInformation("[Story Timeline] Initializing 3-tier story timeline hierarchy generation.");

            var (specialOps, publicAffairs) = await LoadOfficialStoryReviewDataAsync();

            // Deduplicate: remove curated events that already belong to Acts 1, 2, and 3
            var curatedActEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "act13side", "act9d0", "act17side", "act16side", "act11d0", "act30side", "act34side", "act28side", "act20side"
            };
            var filteredSpecialOps = specialOps.Where(ev => !curatedActEventIds.Contains(ev.Id)).ToList();
            var filteredPublicAffairs = publicAffairs.Where(ev => !curatedActEventIds.Contains(ev.Id)).ToList();

            var actGroups = new List<StoryActGroup>
            {
                BuildAct1Group(),
                BuildAct2Group(),
                BuildAct3Group(),
                new StoryActGroup
                {
                    ActId = "act_special_ops",
                    ActTitle = "Special Operation: Side Stories & Intermezzi (RECORDS_S)",
                    ActDescription = $"Complete catalog of {filteredSpecialOps.Count} canonical expeditions, intermezzi, and strategic operations across Terra (RECORDS_S).",
                    MainNodes = [],
                    LinkedEvents = filteredSpecialOps
                },
                new StoryActGroup
                {
                    ActId = "act_public_affairs",
                    ActTitle = "Public Affairs: Vignettes & Story Collections (RECORDS_P)",
                    ActDescription = $"Confidential personnel chronicles, psychological vignettes, and Doctor's pre-amnesia memory archives ({filteredPublicAffairs.Count} collections).",
                    MainNodes = [],
                    LinkedEvents = filteredPublicAffairs
                },
                BuildGameModesLoreGroup()
            };

            // Enrich linked Operator Records with live character metadata from CharacterService
            var allOps = await _characterService.GetAllAsync();
            var opLookup = allOps.ToDictionary(o => o.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (var act in actGroups)
            {
                foreach (var ev in act.LinkedEvents)
                {
                    foreach (var record in ev.ChildRecords)
                    {
                        if (!string.IsNullOrEmpty(record.RelatedOperatorId) &&
                            opLookup.TryGetValue(record.RelatedOperatorId, out var op))
                        {
                            record.RelatedOperatorName = op.Name;
                            record.RelatedOperatorAvatar = $"{AVATAR_CDN_BASE}/{op.Id}.png";
                        }
                    }
                }
            }

            _logger.LogInformation("[Story Timeline] Constructed {ActCount} acts with {MainCount} chapters and {EventCount} linked events.",
                actGroups.Count, actGroups.Sum(a => a.MainNodes.Count), actGroups.Sum(a => a.LinkedEvents.Count));

            return actGroups;
        }

        /// <summary>
        /// Retrieves personnel archives grouped by faction for operators who are not part of the main campaign.
        /// </summary>
        public async Task<List<FactionStoryArchive>> GetFactionStoryArchivesAsync()
        {
            _logger.LogInformation("[Story Record] Generating Faction Personnel Archives for independent operators.");

            var allOps = await _characterService.GetAllAsync();

            var groups = allOps
                .Where(o => !string.IsNullOrEmpty(o.NationId) 
                         && !string.IsNullOrWhiteSpace(o.Name) 
                         && o.Name != "???" 
                         && !o.Name.StartsWith("???"))
                .GroupBy(o => o.NationId!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new FactionStoryArchive
                {
                    FactionId = g.Key,
                    FactionName = FormatFactionName(g.Key),
                    OperatorRecords = g.Select(op => new StoryNode
                    {
                        Id = $"record_{op.Id}",
                        Code = "RECORD",
                        Title = $"{op.Name}'s Archive",
                        SubTitle = $"Personnel Record: {op.Profession}",
                        Description = $"Personal chronicle and confidential medical profile of {op.Name} registered at Rhodes Island.",
                        NodeType = StoryNodeType.OperatorRecord,
                        RelatedFaction = g.Key,
                        RelatedOperatorId = op.Id,
                        RelatedOperatorName = op.Name,
                        RelatedOperatorAvatar = $"{AVATAR_CDN_BASE}/{op.Id}.png",
                        BannerUrl = $"{CHAR_SPLASH_BASE}/{op.Id}_1.png",
                        Episodes =
                        [
                            new StoryEpisodeInfo
                            {
                                StoryId = $"story_{op.Id}_1",
                                StoryCode = "Part 1",
                                StoryName = "Operator Record",
                                StoryGroup = "Personnel Record",
                                StoryPath = $"obt/memory/story_{(op.Id != null && op.Id.Contains('_') ? op.Id.Substring(op.Id.LastIndexOf('_') + 1).ToLowerInvariant() : (op.Id ?? "unknown").ToLowerInvariant())}_1_1"
                            }
                        ]
                    }).ToList()
                })
                .ToList();

            _logger.LogInformation("[Story Record] Assembled {FactionCount} faction archives containing {TotalRecords} operator records.",
                groups.Count, groups.Sum(g => g.OperatorRecords.Count));

            return groups;
        }

        #endregion

        #region Phase 2: Script Fetching & Dialogue Parsing

        /// <summary>
        /// Fetches the raw story script and parses all dialogue, tags, CGs, and BGM into a structured bundle.
        /// </summary>
        private static readonly Dictionary<string, string> WellKnownSpeakerMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Special story entities & NPCs
            ["Doctor"] = "npc_001_doctor",
            ["Dr. {@nickname}"] = "npc_001_doctor",
            ["Dr. {nickname}"] = "npc_001_doctor",
            ["PRTS"] = "char_504_rguard",
            ["Medic"] = "char_506_rmedic",
            ["RI Medic"] = "char_506_rmedic",
            ["Guard"] = "char_504_rguard",
            ["RI Guard"] = "char_504_rguard",
            ["Heavily-Armed Man"] = "char_504_rguard",
            ["Heavily-Armed Man (Guard)"] = "char_504_rguard",
            ["Reunion Member"] = "bavg_enemy_common",
            ["Reunion Soldier"] = "bavg_enemy_common",
            ["Reunion"] = "bavg_enemy_common",
            ["Ursus Guard"] = "char_508_aguard",
            ["Ace"] = "npc_007_ace",
            ["Closure"] = "npc_007_closure",
            ["Talulah"] = "npc_011_talula",
            ["Patriot"] = "npc_025_patriot",
            ["Wei Yenwu"] = "npc_2005_wywu",
            ["Fumizuki"] = "npc_2006_fmzuki",

            // Main & Recurring Operators
            ["Amiya"] = "char_002_amiya",
            ["Kal'tsit"] = "char_003_kalts",
            ["Dobermann"] = "char_130_doberm",
            ["Ch'en"] = "char_010_chen",
            ["Chen"] = "char_010_chen",
            ["Hoshiguma"] = "char_106_franka",
            ["Swire"] = "char_308_swire",
            ["Exusiai"] = "char_103_angel",
            ["Texas"] = "char_102_texas",
            ["Lappland"] = "char_140_whitmn",
            ["SilverAsh"] = "char_172_silver",
            ["Nearl"] = "char_148_nearl",
            ["Specter"] = "char_143_ghost",
            ["Skadi"] = "char_263_skadi",
            ["Gladiia"] = "char_472_pasngr",
            ["W"] = "char_113_cqbw",
            ["Ines"] = "char_4087_ines",
            ["Hoederer"] = "char_4088_hoeder",
            ["Theresa"] = "char_1033_theresa",
            ["Muelsyse"] = "char_242_otter",
            ["Siege"] = "char_112_siege",
            ["Gavial"] = "char_187_cchef",
            ["Tomimi"] = "char_411_tomimi",
            ["Eunectes"] = "char_416_zumama",
            ["Caper"] = "char_4100_caper",

            // Ave Mujica / BanG Dream & Recent Collab Operators
            ["Sakiko"] = "char_4182_oblvns",
            ["Sakiko Togawa"] = "char_4182_oblvns",
            ["丰川祥子"] = "char_4182_oblvns",
            ["祥子"] = "char_4182_oblvns",
            ["Oblivionis"] = "char_4182_oblvns",
            ["Morpheus"] = "npc_1942_1",
            ["莫菲丝"] = "npc_1942_1",
            ["Vigna"] = "char_290_vigna",
            ["红豆"] = "char_290_vigna",
            ["Bena"] = "char_369_bena",
            ["贝娜"] = "char_369_bena",
            ["Iris"] = "char_338_iris",
            ["爱丽丝"] = "char_338_iris",
            ["Umiri"] = "char_4186_tmoris",
            ["八幡海铃"] = "char_4186_tmoris",
            ["Wakabi"] = "char_4185_amoris",
            ["祐天寺若麦"] = "char_4185_amoris",
            ["FrostNova"] = "enemy_1042_frostd",
            ["霜星"] = "enemy_1042_frostd",
            ["Mephisto"] = "enemy_1507_mephi",
            ["梅菲斯特"] = "enemy_1507_mephi",
            ["Faust"] = "enemy_1508_faust",
            ["浮士德"] = "enemy_1508_faust",
            ["Crownslayer"] = "enemy_1502_crowns",
            ["弑君者"] = "enemy_1502_crowns",
            ["Skullshatterer"] = "enemy_1500_skulsr",
            ["碎骨"] = "enemy_1500_skulsr",
            ["Alex"] = "enemy_1500_skulsr",
            ["Big Bob"] = "enemy_1001_bigbo",
            ["大鲍勃"] = "enemy_1001_bigbo",
            ["Mudrock"] = "enemy_1511_mdrock",
            ["泥岩"] = "enemy_1511_mdrock",
            ["Mandragora"] = "enemy_1523_mandra",
            ["蔓德拉"] = "enemy_1523_mandra",
            ["Eblana"] = "enemy_1540_wdncr",
            ["爱布拉娜"] = "enemy_1540_wdncr",
            ["Kaschey"] = "npc_011_talula_black",
            ["Koschei"] = "npc_011_talula_black",
            ["Deathless Black Snake"] = "npc_011_talula_black",
            ["Nine"] = "npc_012_hunter",
            ["九"] = "npc_012_hunter",
            ["Misha"] = "char_504_rguard",
            ["米莎"] = "char_504_rguard",
            ["Outcast"] = "char_504_rguard",
            ["Misery"] = "char_504_rguard",
            ["Scout"] = "char_504_rguard",
            ["阿米娅"] = "char_002_amiya",
            ["凯尔希"] = "char_003_kalts",
            ["陈"] = "char_010_chen",
            ["博士"] = "npc_001_doctor"
        };

        public async Task<StoryScriptBundle> GetStoryScriptAsync(string storyId, string? storyPath = null, string? title = null)
        {
            _logger.LogInformation("[Story Parser] Requesting story script. StoryId: '{StoryId}', Path: '{Path}'", storyId, storyPath);

            string scriptText = string.Empty;

            if (!string.IsNullOrEmpty(storyPath))
            {
                scriptText = await TryFetchScriptTextAsync(storyPath);
            }

            // For Stage 0-0 (Awakening), combine Before Operation (0_welcome_to_guide) & After Operation (2_guide_to_home) matching wiki.gg
            if (storyId == "0_0" || (storyPath?.Contains("0_welcome_to_guide") ?? false))
            {
                var afterScript = await TryFetchScriptTextAsync("obt/guide/beg/2_guide_to_home");
                if (!string.IsNullOrWhiteSpace(afterScript))
                {
                    scriptText += "\r\n[Dialog]\r\n<─── After Operation ───>\r\n" + afterScript;
                }
            }

            // Fallback generation if network or file is not found
            if (string.IsNullOrWhiteSpace(scriptText))
            {
                _logger.LogWarning("[Story Parser Warning] Remote script unavailable for '{StoryId}'. Generating curated profile script.", storyId);
                scriptText = GenerateCuratedFallbackScript(storyId, title);
            }

            var bundle = ParseScript(scriptText, storyId, title ?? "Story Narrative");

            // Curated canon summaries from [uc]info or fallback for key stages
            if (storyId == "0_0" || (storyPath?.Contains("0_welcome_to_guide") ?? false))
            {
                bundle.Summary = "In the heart of Chernobog, a young girl awakens the person within the coffin – the Doctor. The girl, named Amiya, tries to explain the situation to the Doctor, but the Reunion Movement suddenly attacks them at this time.";
            }
            else if (!string.IsNullOrEmpty(storyPath))
            {
                var infoSummary = await TryFetchInfoTextAsync(storyPath);
                if (!string.IsNullOrWhiteSpace(infoSummary))
                {
                    bundle.Summary = infoSummary.Trim();
                }
            }

            // Enrich each dialogue line with the exact avatar of the SPEAKER
            try
            {
                var allOps = await _characterService.GetAllAsync();
                var opByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var op in allOps)
                {
                    if (!string.IsNullOrEmpty(op.Name) && !string.IsNullOrEmpty(op.Id))
                    {
                        opByName.TryAdd(op.Name, op.Id);
                    }
                }

                foreach (var line in bundle.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.SpeakerName)) continue;

                    string? opId = null;
                    if (opByName.TryGetValue(line.SpeakerName, out var foundId))
                    {
                        opId = foundId;
                    }
                    else if (WellKnownSpeakerMap.TryGetValue(line.SpeakerName, out var wellKnownId))
                    {
                        opId = wellKnownId;
                    }

                    if (!string.IsNullOrEmpty(opId))
                    {
                        if (opId.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase))
                        {
                            line.SpeakerAvatarUrl = $"https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/enemy/{opId}.png";
                        }
                        else
                        {
                            line.SpeakerAvatarUrl = $"{AVATAR_CDN_BASE}/{opId}.png";
                        }
                    }
                }

                // Populate characters appearing showcase (filtered to named characters only)
                bundle.CharactersAppearing = bundle.Lines
                    .Where(l => !string.IsNullOrWhiteSpace(l.SpeakerName) && IsValidCharacterName(l.SpeakerName))
                    .GroupBy(l => l.SpeakerName!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new StoryCharacterShowcase
                    {
                        Name = g.Key,
                        AvatarUrl = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.SpeakerAvatarUrl))?.SpeakerAvatarUrl
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Story Parser] Error enriching speaker avatars.");
            }

            _logger.LogInformation("[Story Parser] Successfully parsed {Count} dialogue lines for '{StoryId}'.", bundle.TotalLines, storyId);

            return bundle;
        }

        /// <summary>
        /// Core parser that processes raw Arknights script tags into structured dialogue lines.
        /// </summary>
        public StoryScriptBundle ParseScript(string rawScript, string storyId, string title)
        {
            _logger.LogDebug("[Story Parser] Parsing raw script text (Length: {Length} characters).", rawScript?.Length ?? 0);

            var bundle = new StoryScriptBundle
            {
                StoryId = storyId,
                Title = title
            };

            if (string.IsNullOrWhiteSpace(rawScript))
            {
                return bundle;
            }

            var lines = rawScript.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            string? currentSpeaker = null;
            string? currentBg = null;
            string? previousEmittedBg = null;
            string? currentBgmKey = null;
            string? leftCharId = null;
            string? rightCharId = null;
            string activeSpeakerSide = "left";

            var nameRegex = new Regex(@"\[name=""([^""]+)""\]", RegexOptions.Compiled);
            var charNameRegex = new Regex(@"\[Character\([^)]*name=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var charClearRegex = new Regex(@"\[Character(?:\([^)]*fadetime=[^)]*\)|\s*)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var charslotRegex = new Regex(@"\[charslot\([^)]*name=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var charslotClearRegex = new Regex(@"\[charslot(?:\(\s*\)|\s*)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var bgRegex = new Regex(@"\[Background\([^)]*image=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var musicRegex = new Regex(@"\[PlayMusic\([^)]*key=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var shakeRegex = new Regex(@"\[CameraShake(?:[^\]]*)\]", RegexOptions.Compiled);
            var fadeRegex = new Regex(@"\[(Blocker|Fade|ScreenFade)(?:[^\]]*)\]", RegexOptions.Compiled);

            int lineIndex = 0;
            string? pendingIllustration = null;
            int currentDecisionGroup = 0;
            string? currentPredicateRefs = null;
            List<string> currentDecisionValues = [];

            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Ignore internal engine header tags and metadata (e.g. [HEADER(...) ] 初始引导)
                if (trimmed.StartsWith("[HEADER", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("[Header", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check for inline Image illustration command (e.g. [Image(image="avg_0_1")])
                var imgMatch = Regex.Match(trimmed, @"\[Image\([^)]*image=""([^""]+)""", RegexOptions.IgnoreCase);
                if (imgMatch.Success)
                {
                    string imgName = imgMatch.Groups[1].Value;
                    if (!string.IsNullOrEmpty(imgName))
                    {
                        pendingIllustration = ResolveImageUrl(imgName);
                    }
                    continue;
                }

                // Check for Decision command (Doctor interactive choices)
                var decMatch = Regex.Match(trimmed, @"\[Decision\([^)]*options=""([^""]+)""", RegexOptions.IgnoreCase);
                if (decMatch.Success)
                {
                    var rawOpts = decMatch.Groups[1].Value;
                    var opts = rawOpts.Split(';').Select(o => o.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToList();
                    if (opts.Count > 0)
                    {
                        currentDecisionGroup++;
                        currentPredicateRefs = null;

                        var valMatch = Regex.Match(trimmed, @"values=""([^""]+)""", RegexOptions.IgnoreCase);
                        currentDecisionValues = valMatch.Success
                            ? valMatch.Groups[1].Value.Split(';').Select(v => v.Trim()).ToList()
                            : Enumerable.Range(1, opts.Count).Select(i => i.ToString()).ToList();

                        lineIndex++;
                        bool isDecisionSceneChange = !string.IsNullOrEmpty(currentBg) && currentBg != previousEmittedBg;
                        if (isDecisionSceneChange) previousEmittedBg = currentBg;

                        bundle.Lines.Add(new StoryDialogueLine
                        {
                            Index = lineIndex,
                            SpeakerName = "Doctor",
                            SpeakerAvatarUrl = $"{AVATAR_CDN_BASE}/npc_001_doctor.png",
                            DialogueText = string.Join(" / ", opts),
                            IsDecision = true,
                            DecisionOptions = opts,
                            DecisionValues = currentDecisionValues,
                            DecisionIndex = currentDecisionGroup,
                            PredicateReferences = null,
                            IllustrationUrl = pendingIllustration,
                            BackgroundCgUrl = ResolveBgUrl(currentBg),
                            IsSceneChange = isDecisionSceneChange,
                            BgmTrackKey = currentBgmKey,
                            BgmTrackTitle = ResolveBgmTitle(currentBgmKey),
                            BgmAudioUrl = ResolveBgmUrl(currentBgmKey)
                        });
                        pendingIllustration = null;
                        continue;
                    }
                }

                // Check for Predicate branching command (e.g. [Predicate(references="1")] or [Predicate()])
                var predMatch = Regex.Match(trimmed, @"\[Predicate\([^)]*references=""([^""]*)""", RegexOptions.IgnoreCase);
                if (predMatch.Success)
                {
                    var rawPred = predMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(rawPred))
                    {
                        currentPredicateRefs = null;
                    }
                    else
                    {
                        var tokens = rawPred.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
                        // If the predicate contains all decision values or the decision only had 1 option, it is a common trunk line
                        if (currentDecisionValues.Count <= 1 || (currentDecisionValues.Count > 0 && currentDecisionValues.All(v => tokens.Contains(v))))
                        {
                            currentPredicateRefs = null;
                        }
                        else
                        {
                            currentPredicateRefs = rawPred;
                        }
                    }
                    continue;
                }
                else if (trimmed.StartsWith("[Predicate", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.StartsWith("[Dialog", StringComparison.OrdinalIgnoreCase))
                {
                    currentPredicateRefs = null;
                    if (trimmed.StartsWith("[Predicate", StringComparison.OrdinalIgnoreCase)) continue;
                }

                // Check for StopMusic command (silence scene)
                if (trimmed.StartsWith("[StopMusic", StringComparison.OrdinalIgnoreCase))
                {
                    currentBgmKey = null;
                }

                // Check for BGM command
                var musicMatch = musicRegex.Match(trimmed);
                if (musicMatch.Success)
                {
                    currentBgmKey = musicMatch.Groups[1].Value;
                    if (string.IsNullOrEmpty(bundle.InitialBgmUrl))
                    {
                        bundle.InitialBgmUrl = ResolveBgmUrl(currentBgmKey);
                        bundle.InitialBgmTitle = ResolveBgmTitle(currentBgmKey);
                    }
                }

                // Check for Background command
                var bgMatch = bgRegex.Match(trimmed);
                if (bgMatch.Success)
                {
                    currentBg = bgMatch.Groups[1].Value;
                    if (string.IsNullOrEmpty(bundle.InitialBackgroundUrl))
                    {
                        bundle.InitialBackgroundUrl = ResolveBgUrl(currentBg);
                    }
                }

                // Check for Character appearance or clearance command (supports both [Character] and [charslot])
                if ((charClearRegex.IsMatch(trimmed) && !trimmed.Contains("name=", StringComparison.OrdinalIgnoreCase)) ||
                    (charslotClearRegex.IsMatch(trimmed) && !trimmed.Contains("name=", StringComparison.OrdinalIgnoreCase)))
                {
                    leftCharId = null;
                    rightCharId = null;
                    activeSpeakerSide = "none";
                }
                else if (trimmed.StartsWith("[Character", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.StartsWith("[charslot", StringComparison.OrdinalIgnoreCase))
                {
                    var m1 = Regex.Match(trimmed, @"name=""([^""]+)""", RegexOptions.IgnoreCase);
                    var m2 = Regex.Match(trimmed, @"name2=""([^""]+)""", RegexOptions.IgnoreCase);
                    var mf = Regex.Match(trimmed, @"focus=(-?\d+)", RegexOptions.IgnoreCase);

                    if (m1.Success)
                    {
                        leftCharId = CleanCharacterId(m1.Groups[1].Value);
                    }
                    if (m2.Success)
                    {
                        rightCharId = CleanCharacterId(m2.Groups[1].Value);
                    }
                    else if (!trimmed.Contains("name2=", StringComparison.OrdinalIgnoreCase) && m1.Success)
                    {
                        rightCharId = null;
                    }

                    if (mf.Success && int.TryParse(mf.Groups[1].Value, out int focusVal))
                    {
                        if (focusVal == 1) activeSpeakerSide = "left";
                        else if (focusVal == 2) activeSpeakerSide = "right";
                        else if (focusVal == -1) activeSpeakerSide = "none";
                    }
                    else
                    {
                        activeSpeakerSide = !string.IsNullOrEmpty(leftCharId) ? "left" : "none";
                    }
                }

                // Check for visual effects
                var effect = StoryVisualEffect.None;
                if (shakeRegex.IsMatch(trimmed))
                {
                    effect = StoryVisualEffect.CameraShake;
                }
                else if (fadeRegex.IsMatch(trimmed))
                {
                    effect = StoryVisualEffect.ScreenFlashWhite;
                }

                // Check for dialogue speaker name tag
                var nameMatch = nameRegex.Match(trimmed);
                if (nameMatch.Success)
                {
                    currentSpeaker = nameMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(currentSpeaker))
                    {
                        currentSpeaker = null;
                    }
                    else if (!string.IsNullOrEmpty(rightCharId) &&
                             (currentSpeaker.Contains(rightCharId, StringComparison.OrdinalIgnoreCase) ||
                              (currentSpeaker.Length >= 4 && rightCharId.Contains(currentSpeaker[..4], StringComparison.OrdinalIgnoreCase))))
                    {
                        activeSpeakerSide = "right";
                    }
                    else if (!string.IsNullOrEmpty(leftCharId) &&
                             (currentSpeaker.Contains(leftCharId, StringComparison.OrdinalIgnoreCase) ||
                              (currentSpeaker.Length >= 4 && leftCharId.Contains(currentSpeaker[..4], StringComparison.OrdinalIgnoreCase))))
                    {
                        activeSpeakerSide = "left";
                    }
                }

                // Extract spoken text (remove bracket tags)
                string spokenText = Regex.Replace(trimmed, @"\[[^\]]+\]", "").Trim();

                if (!string.IsNullOrWhiteSpace(spokenText))
                {
                    bool isStageDir = (spokenText.StartsWith("<") && spokenText.EndsWith(">")) ||
                                      spokenText.StartsWith("An EKG", StringComparison.OrdinalIgnoreCase) ||
                                      spokenText.StartsWith("The EKG", StringComparison.OrdinalIgnoreCase) ||
                                      spokenText.StartsWith("The sound of", StringComparison.OrdinalIgnoreCase);

                    spokenText = spokenText.Replace("{@nickname}", "Doctor");

                    lineIndex++;
                    bool isSceneChange = !string.IsNullOrEmpty(currentBg) && currentBg != previousEmittedBg;
                    if (isSceneChange) previousEmittedBg = currentBg;

                    var dialogueLine = new StoryDialogueLine
                    {
                        Index = lineIndex,
                        SpeakerName = isStageDir ? null : currentSpeaker,
                        DialogueText = spokenText,
                        IsStageDirection = isStageDir,
                        IllustrationUrl = pendingIllustration,
                        LeftCharacterId = leftCharId,
                        LeftCharacterAvatarUrl = !string.IsNullOrEmpty(leftCharId) ? $"{AVATAR_CDN_BASE}/{leftCharId}.png" : null,
                        LeftCharacterSpriteUrl = !string.IsNullOrEmpty(leftCharId) ? $"{CHAR_SPLASH_BASE}/{leftCharId}_1.png" : null,
                        RightCharacterId = rightCharId,
                        RightCharacterAvatarUrl = !string.IsNullOrEmpty(rightCharId) ? $"{AVATAR_CDN_BASE}/{rightCharId}.png" : null,
                        RightCharacterSpriteUrl = !string.IsNullOrEmpty(rightCharId) ? $"{CHAR_SPLASH_BASE}/{rightCharId}_1.png" : null,
                        ActiveSpeakerSide = activeSpeakerSide,
                        BackgroundCgUrl = ResolveBgUrl(currentBg),
                        IsSceneChange = isSceneChange,
                        BgmTrackKey = currentBgmKey,
                        BgmTrackTitle = ResolveBgmTitle(currentBgmKey),
                        BgmAudioUrl = ResolveBgmUrl(currentBgmKey),
                        Effect = effect,
                        DecisionIndex = currentDecisionGroup,
                        DecisionValues = currentDecisionValues,
                        PredicateReferences = currentPredicateRefs
                    };

                    bundle.Lines.Add(dialogueLine);
                    pendingIllustration = null;
                }
            }

            return bundle;
        }

        private static string CleanCharacterId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string normalized = raw;
            if (normalized.StartsWith("avg_", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "char_" + normalized[4..];
            }
            var match = Regex.Match(normalized, @"(char_\d+_[a-zA-Z0-9]+)");
            if (match.Success)
            {
                string id = match.Groups[1].Value;
                if (id.EndsWith("_ex", StringComparison.OrdinalIgnoreCase))
                {
                    id = id[..^3];
                }
                return id;
            }
            return normalized.Split('#')[0].Split('$')[0].Trim();
        }

        private static readonly HashSet<string> IgnoredSpeakerNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "???", "??", "?", "Distant Voice", "Voice", "Voices", "Unfamiliar Voice", 
            "Familiar Voice", "Mysterious Voice", "Unknown Voice", "Strange Voice",
            "Radio", "Intercom", "Broadcast", "Announcement", "System", "Computer",
            "Someone", "Everyone", "Nobody", "Unknown", "Whisper", "Echo", "Shadow"
        };

        public static bool IsValidCharacterName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            if (trimmed.Contains('?') || trimmed.Contains('？')) return false;
            if (IgnoredSpeakerNames.Contains(trimmed)) return false;
            if (trimmed.EndsWith("Voice", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("Distant", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        #endregion

        #region Phase 3: Media & CDN Resolution Helpers

        public string? ResolveBgmUrl(string? bgmKey)
        {
            if (string.IsNullOrEmpty(bgmKey))
            {
                return null;
            }

            if (BgmTracks.TryGetValue(bgmKey, out var track))
            {
                return track.Url;
            }

            if (bgmKey.Contains("babel", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$babel_loop"].Url;
            }

            if (bgmKey.Contains("ekg", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("calm", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$ekg_loop"].Url;
            }

            if (bgmKey.Contains("dark", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("nervous", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("danger", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("suspense", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("tense", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$darkness01_loop"].Url;
            }

            if (bgmKey.Contains("escape", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("excite", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("bat", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("fight", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$escape_loop"].Url;
            }

            if (bgmKey.Contains("action", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("crisis", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$battle_01"].Url;
            }

            return BgmTracks["$default_ambient"].Url;
        }

        public string ResolveBgmTitle(string? bgmKey)
        {
            if (string.IsNullOrEmpty(bgmKey))
            {
                return "Silence (Immersive Ambience)";
            }

            if (BgmTracks.TryGetValue(bgmKey, out var track))
            {
                return track.Title;
            }

            if (bgmKey.Contains("babel", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$babel_loop"].Title;
            }

            if (bgmKey.Contains("ekg", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("calm", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$ekg_loop"].Title;
            }

            if (bgmKey.Contains("dark", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("nervous", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("danger", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("suspense", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("tense", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$darkness01_loop"].Title;
            }

            if (bgmKey.Contains("escape", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("excite", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("bat", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("fight", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$escape_loop"].Title;
            }

            if (bgmKey.Contains("action", StringComparison.OrdinalIgnoreCase) ||
                bgmKey.Contains("crisis", StringComparison.OrdinalIgnoreCase))
            {
                return BgmTracks["$battle_01"].Title;
            }

            return $"Track: {bgmKey.TrimStart('$')}";
        }

        public string ResolveBgUrl(string? bgName)
        {
            if (string.IsNullOrEmpty(bgName))
            {
                return $"{AVG_BG_CDN_BASE}/bg_black.png";
            }

            string clean = Path.GetFileNameWithoutExtension(bgName.Trim()).ToLowerInvariant();
            if (clean.StartsWith("avg_") || clean.StartsWith("bg_0_"))
            {
                return $"https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/avg/images/{clean}.png";
            }
            return $"{AVG_BG_CDN_BASE}/{clean}.png";
        }

        public static string? ResolveImageUrl(string? imgName)
        {
            if (string.IsNullOrWhiteSpace(imgName)) return null;
            string clean = Path.GetFileNameWithoutExtension(imgName.Trim()).ToLowerInvariant();
            return $"https://cdn.jsdelivr.net/gh/Aceship/Arknight-Images@main/avg/images/{clean}.png";
        }

        public string? GetYouTubePvUrl(string? storyId)
        {
            if (string.IsNullOrEmpty(storyId)) return null;

            if (EventYouTubePvMap.TryGetValue(storyId, out var videoId))
            {
                return $"https://www.youtube-nocookie.com/embed/{videoId}?autoplay=1&rel=0";
            }

            return null;
        }

        private static readonly string StoryCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Stories");

        private async Task<string> TryFetchScriptTextAsync(string storyPath)
        {
            string cleanPath = storyPath.TrimStart('/').Replace(".txt", "").Replace(".json", "");
            string localCacheFile = Path.Combine(StoryCacheDir, cleanPath.Replace('/', '_') + ".txt");

            // 1. Check local disk cache for instant offline access
            try
            {
                if (File.Exists(localCacheFile))
                {
                    string localContent = await File.ReadAllTextAsync(localCacheFile, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(localContent))
                    {
                        _logger.LogInformation("[Story Cache Hit] Loaded offline script from disk: {Path}", localCacheFile);
                        return localContent;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Story Cache] Error reading local script cache.");
            }

            var candidatePaths = new List<string>();
            if (cleanPath.EndsWith("_beg") || cleanPath.EndsWith("_end"))
            {
                candidatePaths.Add(cleanPath);
            }
            else
            {
                candidatePaths.Add(cleanPath);
                candidatePaths.Add($"{cleanPath}_beg");
                candidatePaths.Add($"{cleanPath}_end");
            }

            // Automatic expansion for Operator Records (Memory):
            // e.g. "obt/record/record_char_4100_caper_1" or "obt/memory/story_caper_1_1"
            var charMatch = Regex.Match(cleanPath, @"char_\d+_([a-zA-Z0-9]+)");
            if (charMatch.Success)
            {
                string shortId = charMatch.Groups[1].Value.ToLowerInvariant();
                candidatePaths.Add($"obt/memory/story_{shortId}_1_1");
                candidatePaths.Add($"obt/memory/story_{shortId}_1_2");
                candidatePaths.Add($"obt/memory/story_{shortId}_2_1");
            }
            else if (cleanPath.Contains("memory", StringComparison.OrdinalIgnoreCase))
            {
                string shortId = cleanPath.Split('_')[^1].ToLowerInvariant();
                candidatePaths.Add($"obt/memory/story_{shortId}_1_1");
            }

            foreach (var baseUrl in ScriptBaseUrls)
            {
                foreach (var candidate in candidatePaths)
                {
                    string url = $"{baseUrl}/{candidate}.txt";
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var text = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _logger.LogInformation("[Story Parser] Successfully fetched script ({Bytes} bytes) from: {Url}", text.Length, url);

                                // Save to local disk cache for offline access
                                try
                                {
                                    if (!Directory.Exists(StoryCacheDir))
                                    {
                                        Directory.CreateDirectory(StoryCacheDir);
                                    }
                                    await File.WriteAllTextAsync(localCacheFile, text, System.Text.Encoding.UTF8);
                                }
                                catch (Exception cEx)
                                {
                                    _logger.LogDebug(cEx, "[Story Cache] Failed to write offline cache file.");
                                }

                                return text;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[Story Parser] Candidate failed: {Url}", url);
                    }
                }
            }

            _logger.LogWarning("[Story Parser Warning] All remote script attempts exhausted for '{Path}'.", storyPath);
            return string.Empty;
        }

        /// <summary>
        /// Clears all offline cached story scripts from local disk.
        /// </summary>
        public Task ClearStoryOfflineCacheAsync()
        {
            try
            {
                if (Directory.Exists(StoryCacheDir))
                {
                    Directory.Delete(StoryCacheDir, true);
                    Directory.CreateDirectory(StoryCacheDir);
                    _logger.LogInformation("[Story Cache] Successfully purged all offline story files.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Story Cache] Error clearing story cache.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns total size of cached story scripts on disk in bytes.
        /// </summary>
        public long GetStoryCacheSizeBytes()
        {
            try
            {
                if (!Directory.Exists(StoryCacheDir)) return 0;
                var dirInfo = new DirectoryInfo(StoryCacheDir);
                return dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _summaryCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fetches the official in-game synopsis/skip summary text for a story chapter from [uc]info archives.
        /// </summary>
        public async Task<string> GetStorySummaryAsync(string? storyPath)
        {
            if (string.IsNullOrWhiteSpace(storyPath))
                return string.Empty;

            string cleanPath = storyPath.TrimStart('/').Replace(".txt", "").Replace(".json", "");
            if (_summaryCache.TryGetValue(cleanPath, out var cachedSummary))
            {
                return cachedSummary;
            }

            string normPath = cleanPath;
            if (normPath.StartsWith("[uc]info/", StringComparison.OrdinalIgnoreCase))
                normPath = normPath.Substring(9);
            else if (normPath.StartsWith("info/", StringComparison.OrdinalIgnoreCase))
                normPath = normPath.Substring(5);

            var pathsToTry = new List<string> { normPath };
            if (normPath.EndsWith("_beg", StringComparison.OrdinalIgnoreCase))
                pathsToTry.Add(normPath[..^4]);
            else if (normPath.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
                pathsToTry.Add(normPath[..^4]);
            else
            {
                pathsToTry.Add($"{normPath}_beg");
                pathsToTry.Add($"{normPath}_end");
            }

            var urls = new List<string>();
            foreach (var p in pathsToTry)
            {
                urls.Add($"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/main/en_US/gamedata/story/[uc]info/{p}.txt");
                urls.Add($"https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData_YoStar@main/en_US/gamedata/story/[uc]info/{p}.txt");
                urls.Add($"https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/story/[uc]info/{p}.txt");
                urls.Add($"https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData@master/zh_CN/gamedata/story/[uc]info/{p}.txt");
            }

            foreach (var url in urls)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            string summary = content.Trim();
                            _summaryCache[cleanPath] = summary;
                            return summary;
                        }
                    }
                }
                catch
                {
                    // Try next candidate
                }
            }

            return string.Empty;
        }

        private async Task<string> TryFetchInfoTextAsync(string storyPath)
        {
            return await GetStorySummaryAsync(storyPath);
        }

        #endregion

        #region Phase 4: Curated Act & Comprehensive Chapter Catalog

        private static List<StoryEpisodeInfo> BuildChapter0Episodes()
        {
            var list = new List<StoryEpisodeInfo>
            {
                new StoryEpisodeInfo
                {
                    StoryId = "0_0",
                    StoryCode = "0-0",
                    StoryName = "Awakening",
                    StoryPath = "obt/guide/beg/0_welcome_to_guide"
                }
            };
            list.AddRange(GenerateChapterEpisodes(0, 1, 11));
            return list;
        }

        private StoryActGroup BuildAct1Group()
        {
            return new StoryActGroup
            {
                ActId = "act_1",
                ActTitle = "Act 1: Hour of an Awakening",
                ActDescription = "Chapters 0 - 3: The Doctor awakens amidst the ruins of Chernobog. Rhodes Island breaks through the Reunion siege.",
                MainNodes =
                [
                    new StoryNode
                    {
                        Id = "main_ch0",
                        Code = "Chapter 0",
                        Title = "Prologue: Evil Time Part 1",
                        SubTitle = "Chernobog Sarkaz Awakening",
                        Description = "The rescue operation to retrieve the Doctor from the research facility sarcophagus in Chernobog.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_0",
                        ActNumber = "1",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep00.png",
                        Episodes = GetMainChapterEpisodes("main_0", BuildChapter0Episodes)
                    },
                    new StoryNode
                    {
                        Id = "main_ch1",
                        Code = "Chapter 1",
                        Title = "Evil Time Part 2",
                        SubTitle = "Escape from Chernobog",
                        Description = "A desperate escape under the onslaught of the Catastrophe and the Reunion movement.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_1",
                        ActNumber = "1",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep01.png",
                        Episodes = GetMainChapterEpisodes("main_1", () => GenerateChapterEpisodes(1, 1, 12))
                    },
                    new StoryNode
                    {
                        Id = "main_ch2",
                        Code = "Chapter 2",
                        Title = "Separate Paths",
                        SubTitle = "Lungmen Underworld Infiltration",
                        Description = "Rhodes Island establishes an alliance with the Lungmen Guard Department (LGD) and Chief Inspector Wei Yenwu.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_2",
                        ActNumber = "1",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep02.png",
                        Episodes = GetMainChapterEpisodes("main_2", () => GenerateChapterEpisodes(2, 1, 10))
                    },
                    new StoryNode
                    {
                        Id = "main_ch3",
                        Code = "Chapter 3",
                        Title = "Stinging Shock",
                        SubTitle = "The Tragedy of Skullshatterer",
                        Description = "The tragedy of Alex (Skullshatterer) and Misha, leading to Amiya's emotional awakening.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_3",
                        ActNumber = "1",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep03.png",
                        Episodes = GetMainChapterEpisodes("main_3", () => GenerateChapterEpisodes(3, 1, 8))
                    }
                ],
                LinkedEvents =
                [
                    new StoryNode
                    {
                        Id = "act13side",
                        Code = "INTERLUDE",
                        Title = "Darknights Memoir",
                        SubTitle = "Babel & The Mercenary's Past",
                        Description = "The past of W, Ines, Hoederer, and the fall of Queen Theresa aboard the landship Babel.",
                        NodeType = StoryNodeType.Intermezzi,
                        BannerUrl = GetEventBannerUrl("act13side"),
                        DefaultBgmKey = "$act13side_01",
                        YouTubePvId = "s_6iLn-my9I",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "dm_1", StoryCode = "DM-1", StoryName = "Nameless Mercenaries", StoryPath = "activities/act13side/level_act13side_01" },
                            new StoryEpisodeInfo { StoryId = "dm_2", StoryCode = "DM-2", StoryName = "The Sarkaz March", StoryPath = "activities/act13side/level_act13side_02" },
                            new StoryEpisodeInfo { StoryId = "dm_4", StoryCode = "DM-4", StoryName = "Blood and Mud", StoryPath = "activities/act13side/level_act13side_04" },
                            new StoryEpisodeInfo { StoryId = "dm_6", StoryCode = "DM-6", StoryName = "Babel's Shadow", StoryPath = "activities/act13side/level_act13side_06" },
                            new StoryEpisodeInfo { StoryId = "dm_8", StoryCode = "DM-8", StoryName = "Before Dawn", StoryPath = "activities/act13side/level_act13side_08" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_w", Code = "RECORD", Title = "W: The Smile of Sarkaz", RelatedOperatorId = "char_113_cqbw", NodeType = StoryNodeType.OperatorRecord },
                            new StoryNode { Id = "rec_scout", Code = "RECORD", Title = "Scout: The Silent Pledge", RelatedOperatorId = "char_002_amiya", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act9d0",
                        Code = "SIDE STORY",
                        Title = "Code of Brawl",
                        SubTitle = "Penguin Logistics Gang War",
                        Description = "A chaotic night between the Lungmen Mafia families and Emperor's Penguin Logistics.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act9d0"),
                        YouTubePvId = "jfBBEoJ_e4A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "cb_1", StoryCode = "CB-1", StoryName = "Midnight Delivery", StoryPath = "activities/act9d0/level_act9d0_01" },
                            new StoryEpisodeInfo { StoryId = "cb_5", StoryCode = "CB-5", StoryName = "Crossfire", StoryPath = "activities/act9d0/level_act9d0_05" },
                            new StoryEpisodeInfo { StoryId = "cb_10", StoryCode = "CB-10", StoryName = "Morning Light", StoryPath = "activities/act9d0/level_act9d0_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_texas", Code = "RECORD", Title = "Texas: Rain in Lungmen", RelatedOperatorId = "char_102_texas", NodeType = StoryNodeType.OperatorRecord },
                            new StoryNode { Id = "rec_exu", Code = "RECORD", Title = "Exusiai: Apple Pie & Guns", RelatedOperatorId = "char_103_angel", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    }
                ]
            };
        }

        private StoryActGroup BuildAct2Group()
        {
            return new StoryActGroup
            {
                ActId = "act_2",
                ActTitle = "Act 2: Shatter of a Star",
                ActDescription = "Chapters 4 - 8: The Chernobog-Lungmen war. Confrontation with Talulah, FrostNova, Patriot, and the secret of the Deathless Black Snake.",
                MainNodes =
                [
                    new StoryNode
                    {
                        Id = "main_ch4",
                        Code = "Chapter 4",
                        Title = "Burning Run",
                        SubTitle = "The Frozen Battlefield",
                        Description = "Rhodes Island enters the untamed wilderness and clashes with Mephisto and the infected Herd.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_4",
                        ActNumber = "2",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep04.png",
                        Episodes = GetMainChapterEpisodes("main_4", () => GenerateChapterEpisodes(4, 1, 10))
                    },
                    new StoryNode
                    {
                        Id = "main_ch5",
                        Code = "Chapter 5",
                        Title = "Necessary Solutions",
                        SubTitle = "Defense of Lungmen Core",
                        Description = "Wei Yenwu activates the Shadow Guard to eradicate the Reunion threat within Lungmen.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_5",
                        ActNumber = "2",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep05.png",
                        Episodes = GetMainChapterEpisodes("main_5", () => GenerateChapterEpisodes(5, 1, 10))
                    },
                    new StoryNode
                    {
                        Id = "main_ch6",
                        Code = "Chapter 6",
                        Title = "Partial Necrosis",
                        SubTitle = "The Tragedy of FrostNova",
                        Description = "The tragedy of the Ice Princess FrostNova and her final promise to the Doctor amidst the snow.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_6",
                        ActNumber = "2",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep06.png",
                        Episodes = GetMainChapterEpisodes("main_6", () => GenerateChapterEpisodes(6, 1, 16))
                    },
                    new StoryNode
                    {
                        Id = "main_ch7",
                        Code = "Chapter 7",
                        Title = "The Birth of Tragedy",
                        SubTitle = "Patriot's Unbroken Shield",
                        Description = "A fateful clash with Patriot — the last pureblood Wendigo warrior of Terra.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_7",
                        ActNumber = "2",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep07.png",
                        Episodes = GetMainChapterEpisodes("main_7", () => GenerateChapterEpisodes(7, 1, 18))
                    },
                    new StoryNode
                    {
                        Id = "main_ch8",
                        Code = "Chapter 8",
                        Title = "Roaring Flare",
                        SubTitle = "The Fall of the Immortal Black Snake",
                        Description = "The decisive battle atop the Chernobog control tower to liberate Talulah from Koschei's possession.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_8",
                        ActNumber = "2",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep08.png",
                        Episodes = GetMainChapterEpisodes("main_8", () => GenerateChapterEpisodes(8, 1, 16))
                    }
                ],
                LinkedEvents =
                [
                    new StoryNode
                    {
                        Id = "act17side",
                        Code = "SIDE STORY",
                        Title = "Under Tides",
                        SubTitle = "The Abyssal Call",
                        Description = "Gladiia and Specter journey to a remote Iberian coastal town to confront the emerging Seaborn swarm.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act17side"),
                        DefaultBgmKey = "$act17side_01",
                        YouTubePvId = "U_laI-G21fo",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "ut_1", StoryCode = "SV-1", StoryName = "The Mist", StoryPath = "activities/act17side/level_act17side_01" },
                            new StoryEpisodeInfo { StoryId = "ut_5", StoryCode = "SV-5", StoryName = "Church of the Deep", StoryPath = "activities/act17side/level_act17side_05" },
                            new StoryEpisodeInfo { StoryId = "ut_9", StoryCode = "SV-9", StoryName = "Tide's End", StoryPath = "activities/act17side/level_act17side_09" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_gladiia", Code = "RECORD", Title = "Gladiia: The Ocean's Call", RelatedOperatorId = "char_472_pasngr", NodeType = StoryNodeType.OperatorRecord },
                            new StoryNode { Id = "rec_specter", Code = "RECORD", Title = "Specter: The Red Stained Nun", RelatedOperatorId = "char_143_ghost", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act16side",
                        Code = "INTERLUDE",
                        Title = "A Walk in the Dust",
                        SubTitle = "Kal'tsit's Century Long Odyssey",
                        Description = "Kal'tsit's century-long journey across the hostile lands of Sargon and Ursus.",
                        NodeType = StoryNodeType.Intermezzi,
                        BannerUrl = GetEventBannerUrl("act16side"),
                        YouTubePvId = "ptTcDSQF8nw",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "wd_1", StoryCode = "WD-1", StoryName = "The Wanderer", StoryPath = "activities/act16side/level_act16side_01" },
                            new StoryEpisodeInfo { StoryId = "wd_8", StoryCode = "WD-8", StoryName = "Dust of Ages", StoryPath = "activities/act16side/level_act16side_08" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_kal_dust", Code = "RECORD", Title = "Passenger: The Sandstorm", RelatedOperatorId = "char_472_pasngr", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act11d0",
                        Code = "STORY COLLECTION",
                        Title = "Children of Ursus",
                        SubTitle = "Nightmare at Peterheim High",
                        Description = "Haunting memories sealed inside Peterheim Middle School among the Ursus student council.",
                        NodeType = StoryNodeType.StoryCollection,
                        BannerUrl = GetEventBannerUrl("act11d0"),
                        YouTubePvId = "jfBBEoJ_e4A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "cu_1", StoryCode = "SV-1", StoryName = "White Flower", StoryPath = "activities/act11d0/level_act11d0_01" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_rosa", Code = "RECORD", Title = "Rosa: The Broken Noble", RelatedOperatorId = "char_196_sunbr", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    }
                ]
            };
        }

        private StoryActGroup BuildAct3Group()
        {
            return new StoryActGroup
            {
                ActId = "act_3",
                ActTitle = "Act 3: Shadow of a Rapid Star",
                ActDescription = "Chapters 9 - 14: War erupts in the Victorian Empire. The Sarkaz uprising and Columbia's conquest of the skies.",
                MainNodes =
                [
                    new StoryNode
                    {
                        Id = "main_ch9",
                        Code = "Chapter 9",
                        Title = "Stormwatch",
                        SubTitle = "The Flames of County Hillock",
                        Description = "An uprising in County Hillock ignites the rebellion of the Dublinn forces and their leader Eblana.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_9",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep09.png",
                        Episodes = GetMainChapterEpisodes("main_9", () => GenerateChapterEpisodes(9, 1, 19))
                    },
                    new StoryNode
                    {
                        Id = "main_ch10",
                        Code = "Chapter 10",
                        Title = "Rebel's Blade",
                        SubTitle = "Londinium Under Occupation",
                        Description = "Rhodes Island and the Glasgow Gang infiltrate the occupied capital city of Londinium.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_10",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep10.png",
                        Episodes = GetMainChapterEpisodes("main_10", () => GenerateChapterEpisodes(10, 1, 17))
                    },
                    new StoryNode
                    {
                        Id = "main_ch11",
                        Code = "Chapter 11",
                        Title = "Return to Mist",
                        SubTitle = "The Steam Knight Rises",
                        Description = "The awakening of the last Steam Knight of Victoria to shield the people of Londinium.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_11",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep11.png",
                        Episodes = GetMainChapterEpisodes("main_11", () => GenerateChapterEpisodes(11, 1, 20))
                    },
                    new StoryNode
                    {
                        Id = "main_ch12",
                        Code = "Chapter 12",
                        Title = "All Quiet Under the Thunder",
                        SubTitle = "The Damazti Cluster",
                        Description = "Confronting the Damazti Cluster — an ancient shapeshifting organism threatening the allied resistance.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_12",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep12.png",
                        Episodes = GetMainChapterEpisodes("main_12", () => GenerateChapterEpisodes(12, 1, 20))
                    },
                    new StoryNode
                    {
                        Id = "main_ch13",
                        Code = "Chapter 13",
                        Title = "The Root of Suffering",
                        SubTitle = "The Shard and The Sky",
                        Description = "The activation of the colossal war machine 'The Shard', threatening to obliterate surface civilization.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_13",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_IMAGE_CDN_BASE}/avg_ep13.png",
                        Episodes = GetMainChapterEpisodes("main_13", () => GenerateChapterEpisodes(13, 1, 21))
                    },
                    new StoryNode
                    {
                        Id = "main_ch14",
                        Code = "Chapter 14",
                        Title = "Absolution Before the Sun",
                        SubTitle = "The Awakening of Babel",
                        Description = "An ancient war machine awakens beneath Londinium. The truth of the Doctor, Theresa, and the origin of humanity.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_14",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_BG_CDN_BASE}/32_g10_kingrestcore.png",
                        Episodes = GetMainChapterEpisodes("main_14", () => GenerateChapterEpisodes(14, 1, 21))
                    },
                    new StoryNode
                    {
                        Id = "main_ch15",
                        Code = "Chapter 15",
                        Title = "Dissociative Recombination",
                        SubTitle = "Post-War Terra Frontier",
                        Description = "The post-Londinium geopolitical aftermath and the rising new frontier crisis across Terra.",
                        NodeType = StoryNodeType.MainTheme,
                        ChapterId = "ch_15",
                        ActNumber = "3",
                        BannerUrl = $"{AVG_BG_CDN_BASE}/32_g10_kingrestcore.png",
                        Episodes = GetMainChapterEpisodes("main_15", () => GenerateChapterEpisodes(15, 1, 25))
                    }
                ],
                LinkedEvents =
                [
                    new StoryNode
                    {
                        Id = "act30side",
                        Code = "SIDE STORY",
                        Title = "Lone Trail",
                        SubTitle = "Columbia Space Program",
                        Description = "Kristen Wright launches a space station, piercing Terra's false sky to open the cosmic frontier.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act30side"),
                        DefaultBgmKey = "$act30side_01",
                        YouTubePvId = "U_laI-G21fo",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "cw_1", StoryCode = "CW-1", StoryName = "First Flight", StoryPath = "activities/act30side/level_act30side_01" },
                            new StoryEpisodeInfo { StoryId = "cw_5", StoryCode = "CW-5", StoryName = "The Barrier", StoryPath = "activities/act30side/level_act30side_05" },
                            new StoryEpisodeInfo { StoryId = "cw_10", StoryCode = "CW-10", StoryName = "Into the Stars", StoryPath = "activities/act30side/level_act30side_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_muel", Code = "RECORD", Title = "Muelsyse: Water and Sky", RelatedOperatorId = "char_242_otter", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act34side",
                        Code = "INTERLUDE",
                        Title = "Babel",
                        SubTitle = "Origin of the Doctor",
                        Description = "The origins of Babel, exploring the ancient Doctor, Queen Theresa, and Kal'tsit.",
                        NodeType = StoryNodeType.Intermezzi,
                        BannerUrl = GetEventBannerUrl("act34side"),
                        DefaultBgmKey = "$babel_01",
                        YouTubePvId = "ptTcDSQF8nw",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "bb_1", StoryCode = "BB-1", StoryName = "The Black Crown", StoryPath = "activities/act34side/level_act34side_01" },
                            new StoryEpisodeInfo { StoryId = "bb_5", StoryCode = "BB-5", StoryName = "Sarkaz Tears", StoryPath = "activities/act34side/level_act34side_05" },
                            new StoryEpisodeInfo { StoryId = "bb_10", StoryCode = "BB-10", StoryName = "The Sarcophagus", StoryPath = "activities/act34side/level_act34side_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_kal", Code = "RECORD", Title = "Kal'tsit: The Thousand Years", RelatedOperatorId = "char_003_kalts", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act28side",
                        Code = "SIDE STORY",
                        Title = "Il Siracusano",
                        SubTitle = "The Wolf and The Mafia",
                        Description = "The power struggle among the twelve Mafia families of the subterranean city-state of Siracusa.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act28side"),
                        YouTubePvId = "OKNlnmqGd9A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is_1", StoryCode = "IS-1", StoryName = "Famiglia", StoryPath = "activities/act28side/level_act28side_01" },
                            new StoryEpisodeInfo { StoryId = "is_10", StoryCode = "IS-10", StoryName = "The Lone Wolf", StoryPath = "activities/act28side/level_act28side_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_tex_alt", Code = "RECORD", Title = "Texas: The Omertà", RelatedOperatorId = "char_102_texas", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act20side",
                        Code = "SIDE STORY",
                        Title = "Near Light",
                        SubTitle = "The Radiant Knight",
                        Description = "The pinnacle of the Kazimierz Major tournament and Nearl's radiance liberating the Infected.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act20side"),
                        YouTubePvId = "7qS2N8P-OMQ",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "nl_1", StoryCode = "NL-1", StoryName = "Knight's Arena", StoryPath = "activities/act20side/level_act20side_01" },
                            new StoryEpisodeInfo { StoryId = "nl_10", StoryCode = "NL-10", StoryName = "Radiance", StoryPath = "activities/act20side/level_act20side_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_nearl", Code = "RECORD", Title = "Nearl: The Radiant Knight", RelatedOperatorId = "char_148_nearl", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    }
                ]
            };
        }

        private StoryActGroup BuildLimitedEventsGroup()
        {
            return new StoryActGroup
            {
                ActId = "act_limited",
                ActTitle = "Celebration & Limited Events",
                ActDescription = "Major festival and anniversary operations across Terra: Iberian abyssal waters, Durin underground realms, Siesta volcanoes, Leithanien twin spires, and Yan sovereign domains.",
                MainNodes = [],
                LinkedEvents =
                [
                    new StoryNode
                    {
                        Id = "act18side",
                        Code = "LIMITED EVENT",
                        Title = "Stultifera Navis",
                        SubTitle = "The Golden Age Will Return Again",
                        Description = "The expedition to find the lost Iberian dreadnought Stultifera Navis and uncover the truth behind the First Profound Silence.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_1023_ghost2_2.png",
                        DefaultBgmKey = "$act17side_01",
                        YouTubePvId = "U_laI-G21fo",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "sn_1", StoryCode = "SN-1", StoryName = "Iberian Coast", StoryPath = "activities/act18side/level_act18side_01" },
                            new StoryEpisodeInfo { StoryId = "sn_5", StoryCode = "SN-5", StoryName = "Dreadnought's Wake", StoryPath = "activities/act18side/level_act18side_05" },
                            new StoryEpisodeInfo { StoryId = "sn_10", StoryCode = "SN-10", StoryName = "The Golden Age", StoryPath = "activities/act18side/level_act18side_10" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_ghost2", Code = "RECORD", Title = "Specter the Unchained: The Golden Dream", RelatedOperatorId = "char_1023_ghost2", NodeType = StoryNodeType.OperatorRecord },
                            new StoryNode { Id = "rec_irene", Code = "RECORD", Title = "Irene: Inquisitor's Lantern", RelatedOperatorId = "char_4009_irene", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act21side",
                        Code = "LIMITED EVENT",
                        Title = "Ideal City: Endless Carnival",
                        SubTitle = "The Underground Durin Realm",
                        Description = "Gavial, Eunectes, and Tomimi visit the underground Durin city of Zeruertza for a vibrant mechanical symposium and carnival.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_1026_gvial2_2.png",
                        DefaultBgmKey = "$default_ambient",
                        YouTubePvId = "s_6iLn-my9I",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "ic_1", StoryCode = "IC-1", StoryName = "Welcome to Zeruertza", StoryPath = "activities/act21side/level_act21side_01" },
                            new StoryEpisodeInfo { StoryId = "ic_5", StoryCode = "IC-5", StoryName = "Underground Waterway", StoryPath = "activities/act21side/level_act21side_05" },
                            new StoryEpisodeInfo { StoryId = "ic_9", StoryCode = "IC-9", StoryName = "Endless Celebration", StoryPath = "activities/act21side/level_act21side_09" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_gvial2", Code = "RECORD", Title = "Gavial the Invincible: Chief of Acahualla", RelatedOperatorId = "char_1026_gvial2", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act27side",
                        Code = "LIMITED EVENT",
                        Title = "So Long, Adele",
                        SubTitle = "The Gentle Sheep and the Hot Spring",
                        Description = "Eyjafjalla visits Siesta's volcanic region to research the mysterious Dolly and discover the musical echoes left by her late parents.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_1031_slbell_2.png",
                        DefaultBgmKey = "$loneliness",
                        YouTubePvId = "OKNlnmqGd9A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "sl_1", StoryCode = "SL-1", StoryName = "Summer Breeze", StoryPath = "activities/act27side/level_act27side_01" },
                            new StoryEpisodeInfo { StoryId = "sl_5", StoryCode = "SL-5", StoryName = "Volcanic Melody", StoryPath = "activities/act27side/level_act27side_05" },
                            new StoryEpisodeInfo { StoryId = "sl_8", StoryCode = "SL-8", StoryName = "Farewell, Adele", StoryPath = "activities/act27side/level_act27side_08" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_eyja2", Code = "RECORD", Title = "Eyjafjalla: The Gentle Echo", RelatedOperatorId = "char_1031_slbell", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act29side",
                        Code = "LIMITED EVENT",
                        Title = "Zwillingstürme im Herbst",
                        SubTitle = "The Spire of Twilight & The Empress",
                        Description = "In the heart of Leithanien, the twin spires resound with the lingering melody of the Witch King, awakening ancient Originium arts.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_4098_vvana_2.png",
                        DefaultBgmKey = "$suspense",
                        YouTubePvId = "7qS2N8P-OMQ",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "zt_1", StoryCode = "ZT-1", StoryName = "Autumn Sonata", StoryPath = "activities/act29side/level_act29side_01" },
                            new StoryEpisodeInfo { StoryId = "zt_5", StoryCode = "ZT-5", StoryName = "Discordant Notes", StoryPath = "activities/act29side/level_act29side_05" },
                            new StoryEpisodeInfo { StoryId = "zt_9", StoryCode = "ZT-9", StoryName = "The Final Movement", StoryPath = "activities/act29side/level_act29side_09" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_viviana", Code = "RECORD", Title = "Viviana: The Candle Knight's Elegy", RelatedOperatorId = "char_4098_vvana", NodeType = StoryNodeType.OperatorRecord },
                            new StoryNode { Id = "rec_virtuosa", Code = "RECORD", Title = "Virtuosa: Requiem of Free Will", RelatedOperatorId = "char_245_cello", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act22side",
                        Code = "LIMITED EVENT",
                        Title = "Where Vernal Winds Will Never Blow",
                        SubTitle = "The Martial Grandeur of Yumen",
                        Description = "Chongyue, the eldest Sui sibling, defends the nomadic fortress of Yumen against invading collapsals and sovereign beasts.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_2024_chyue_2.png",
                        DefaultBgmKey = "$battle_01",
                        YouTubePvId = "jfBBEoJ_e4A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "wb_1", StoryCode = "WB-1", StoryName = "Winds of Yumen", StoryPath = "activities/act22side/level_act22side_01" },
                            new StoryEpisodeInfo { StoryId = "wb_5", StoryCode = "WB-5", StoryName = "Clash of Blades", StoryPath = "activities/act22side/level_act22side_05" },
                            new StoryEpisodeInfo { StoryId = "wb_9", StoryCode = "WB-9", StoryName = "Endless Horizon", StoryPath = "activities/act22side/level_act22side_08" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_chongyue", Code = "RECORD", Title = "Chongyue: The Way of the Blade", RelatedOperatorId = "char_2024_chyue", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act32side",
                        Code = "LIMITED EVENT",
                        Title = "Here A People Sows",
                        SubTitle = "The Granary of Great Yan",
                        Description = "The harvest cycle of Dahuang and the sacred grain rites of the Sui sibling Shu to ensure fertile soil for the common people.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_2025_shu_2.png",
                        DefaultBgmKey = "$loneliness",
                        YouTubePvId = "U_laI-G21fo",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "hs_1", StoryCode = "HS-1", StoryName = "Spring Plow", StoryPath = "activities/act32side/level_act32side_01" },
                            new StoryEpisodeInfo { StoryId = "hs_5", StoryCode = "HS-5", StoryName = "The Great Flood", StoryPath = "activities/act32side/level_act32side_05" },
                            new StoryEpisodeInfo { StoryId = "hs_9", StoryCode = "HS-9", StoryName = "Golden Harvest", StoryPath = "activities/act32side/level_act32side_09" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_shu", Code = "RECORD", Title = "Shu: Seeds of Life", RelatedOperatorId = "char_2025_shu", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "act15side",
                        Code = "LIMITED EVENT",
                        Title = "Dossoles Holiday",
                        SubTitle = "The Tequila & Cantabile Conspiracy",
                        Description = "Ch'en leaves Lungmen to spend a summer vacation in Dossoles, only to be embroiled in Mayor Candela Sanchez's political machinations.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = $"{CHAR_SPLASH_BASE}/char_010_chen_2.png",
                        DefaultBgmKey = "$default_ambient",
                        YouTubePvId = "ptTcDSQF8nw",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "dh_1", StoryCode = "DH-1", StoryName = "Holiday in the Sun", StoryPath = "activities/act15side/level_act15side_01" },
                            new StoryEpisodeInfo { StoryId = "dh_5", StoryCode = "DH-5", StoryName = "Waterfight Carnival", StoryPath = "activities/act15side/level_act15side_05" },
                            new StoryEpisodeInfo { StoryId = "dh_9", StoryCode = "DH-9", StoryName = "Sunset Cocktail", StoryPath = "activities/act15side/level_act15side_09" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_chen2", Code = "RECORD", Title = "Ch'en: Lungmen's Vanguard", RelatedOperatorId = "char_010_chen", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    }
                ]
            };
        }

        private StoryActGroup BuildGameModesLoreGroup()
        {
            return new StoryActGroup
            {
                ActId = "act_gamemodes",
                ActTitle = "Game Modes & Alternate Timelines (IS & RA)",
                ActDescription = "Alternate chronicles, deep subterranean explorations, Seaborn futures, northern demons, and territorial conquest across Terra.",
                MainNodes = [],
                LinkedEvents =
                [
                    new StoryNode
                    {
                        Id = "is_1_ceobe",
                        Code = "IS #1",
                        Title = "Ceobe's Fungimist",
                        SubTitle = "The Honey Biscuit Odyssey",
                        Description = "Ceobe accidentally consumes hallucinogenic wild mushrooms in the jungle and embarks on a whimsical, mythical journey through Terra's lore.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act12d0"),
                        DefaultBgmKey = "$default_ambient",
                        YouTubePvId = "jfBBEoJ_e4A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is1_1", StoryCode = "IS1-1", StoryName = "Fungus Illusion", StoryPath = "activities/act12d0/level_act12d0_01" },
                            new StoryEpisodeInfo { StoryId = "is1_end1", StoryCode = "IS1-ED", StoryName = "The Golden Sky", StoryPath = "activities/act12d0/level_act12d0_06" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_ceobe", Code = "RECORD", Title = "Ceobe: Honey Biscuit Dream", RelatedOperatorId = "char_2013_cerber", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "is_2_phantom",
                        Code = "IS #2",
                        Title = "Phantom & Crimson Solitaire",
                        SubTitle = "The Phantom of the Opera & Crimson Troupe",
                        Description = "Lucian (Phantom) descends into the twisted Gothic depths of the Crimson Troupe's castle, haunted by memories of his past.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act15side"),
                        DefaultBgmKey = "$suspense",
                        YouTubePvId = "7qS2N8P-OMQ",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is2_ed1", StoryCode = "Ending 1", StoryName = "Curtain Call: The Final Soliloquy", StoryPath = "obt/roguelike/ro1/level_rogue1_ending_1" },
                            new StoryEpisodeInfo { StoryId = "is2_ed2", StoryCode = "Ending 2", StoryName = "A Midsummer Night's Nightmare", StoryPath = "obt/roguelike/ro1/level_rogue1_ending_2" },
                            new StoryEpisodeInfo { StoryId = "is2_ed3", StoryCode = "Ending 3", StoryName = "The Grand Puppeteer", StoryPath = "obt/roguelike/ro1/level_rogue1_ending_3" },
                            new StoryEpisodeInfo { StoryId = "is2_ed4", StoryCode = "Ending 4", StoryName = "Silent Reverie", StoryPath = "obt/roguelike/ro1/level_rogue1_ending_4" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_phantom", Code = "RECORD", Title = "Phantom: The Scarlet Velvet", RelatedOperatorId = "char_250_phatom", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "is_3_mizuki",
                        Code = "IS #3",
                        Title = "Mizuki & Caerula Arbor",
                        SubTitle = "The Many Fates of the Seaborn Invasion",
                        Description = "An alternate future exploring the total sublimation of Terra under the Seaborn swarm and Mizuki's ascendance as an Abyssal Deity.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act18side"),
                        DefaultBgmKey = "$act17side_01",
                        YouTubePvId = "U_laI-G21fo",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is3_ed1", StoryCode = "Ending 1", StoryName = "Sublimation: The Sleeping City", StoryPath = "obt/roguelike/ro2/level_rogue2_ending_1" },
                            new StoryEpisodeInfo { StoryId = "is3_ed2", StoryCode = "Ending 2", StoryName = "The Silent Cavern", StoryPath = "obt/roguelike/ro2/level_rogue2_ending_2" },
                            new StoryEpisodeInfo { StoryId = "is3_ed3", StoryCode = "Ending 3", StoryName = "Echoes of the Blue", StoryPath = "obt/roguelike/ro2/level_rogue2_ending_3" },
                            new StoryEpisodeInfo { StoryId = "is3_ed4", StoryCode = "Ending 4", StoryName = "The Prophet's Departure", StoryPath = "obt/roguelike/ro2/level_rogue2_ending_4" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_mizuki", Code = "RECORD", Title = "Mizuki: The Ocean's Heart", RelatedOperatorId = "char_437_mizuki", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "is_4_sami",
                        Code = "IS #4",
                        Title = "Expeditions Joklumarkar",
                        SubTitle = "The Collapsals of the Frozen North",
                        Description = "Magallan, Typhon, and the Sami Snowpriests venture deep into the icy wasteland to battle otherworldly Collapsals and demons.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act29side"),
                        DefaultBgmKey = "$loneliness",
                        YouTubePvId = "s_6iLn-my9I",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is4_ed1", StoryCode = "Ending 1", StoryName = "Beyond the Icecap", StoryPath = "obt/roguelike/ro3/level_rogue3_ending_1" },
                            new StoryEpisodeInfo { StoryId = "is4_ed2", StoryCode = "Ending 2", StoryName = "The Whispering Star", StoryPath = "obt/roguelike/ro3/level_rogue3_ending_2" },
                            new StoryEpisodeInfo { StoryId = "is4_ed3", StoryCode = "Ending 3", StoryName = "Endless Snowfield", StoryPath = "obt/roguelike/ro3/level_rogue3_ending_3" },
                            new StoryEpisodeInfo { StoryId = "is4_ed4", StoryCode = "Ending 4", StoryName = "The Stargazer's Grave", StoryPath = "obt/roguelike/ro3/level_rogue3_ending_4" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_typhon", Code = "RECORD", Title = "Typhon: Hunter of the Joklumarkar", RelatedOperatorId = "char_2012_typhon", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "is_5_sarkaz",
                        Code = "IS #5",
                        Title = "Sarkaz's Furnaceside Fables",
                        SubTitle = "The Eternal Hearth of Kazdel",
                        Description = "The ancient lore of the Sarkaz race, their tragic migrations, their royal courts, and the mythical fire of Kazdel.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act38side"),
                        DefaultBgmKey = "$babel_loop",
                        YouTubePvId = "ptTcDSQF8nw",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "is5_entry", StoryCode = "Prologue", StoryName = "Fables of the Hearth", StoryPath = "obt/roguelike/ro4/level_rogue4_entry" },
                            new StoryEpisodeInfo { StoryId = "is5_ed1", StoryCode = "Ending 1", StoryName = "The Broken Crown", StoryPath = "obt/roguelike/ro4/level_rogue4_ending_1" },
                            new StoryEpisodeInfo { StoryId = "is5_ed2", StoryCode = "Ending 2", StoryName = "Song of the Mercenary", StoryPath = "obt/roguelike/ro4/level_rogue4_ending_2" },
                            new StoryEpisodeInfo { StoryId = "is5_ed3", StoryCode = "Ending 3", StoryName = "The Immortal Flame", StoryPath = "obt/roguelike/ro4/level_rogue4_ending_3" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_hoederer", Code = "RECORD", Title = "Hoederer: The Old Veteran", RelatedOperatorId = "char_4088_hoeder", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    },
                    new StoryNode
                    {
                        Id = "ra_1_desert",
                        Code = "RECLAMATION ALGORITHM",
                        Title = "Tales Within the Sand",
                        SubTitle = "Territorial Survival in Sargon's Desert",
                        Description = "Establishing forward operational bases, gathering raw minerals, and defending nomadic settlements against ferocious desert bandits.",
                        NodeType = StoryNodeType.SideStory,
                        BannerUrl = GetEventBannerUrl("act23side"),
                        DefaultBgmKey = "$act13side_01",
                        YouTubePvId = "jfBBEoJ_e4A",
                        Episodes =
                        [
                            new StoryEpisodeInfo { StoryId = "ra_1", StoryCode = "RA-1", StoryName = "Desert Encampment", StoryPath = "activities/act23side/level_act23side_01" },
                            new StoryEpisodeInfo { StoryId = "ra_5", StoryCode = "RA-5", StoryName = "Bandit Incursion", StoryPath = "activities/act23side/level_act23side_05" }
                        ],
                        ChildRecords =
                        [
                            new StoryNode { Id = "rec_eunectes", Code = "RECORD", Title = "Eunectes: Desert Metallurgy", RelatedOperatorId = "char_416_zumama", NodeType = StoryNodeType.OperatorRecord }
                        ]
                    }
                ]
            };
        }

        private static List<StoryNode>? _cachedSpecialOps = null;
        private static List<StoryNode>? _cachedPublicAffairs = null;
        private static readonly Dictionary<string, List<StoryEpisodeInfo>> _cachedMainChapterEpisodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _storyReviewLock = new(1, 1);

        private static List<StoryEpisodeInfo> GetMainChapterEpisodes(string chapterKey, Func<List<StoryEpisodeInfo>> fallbackFactory)
        {
            if (_cachedMainChapterEpisodes.TryGetValue(chapterKey, out var eps) && eps.Count > 0)
            {
                return eps;
            }
            return fallbackFactory();
        }

        private static bool ContainsChinese(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
            }
            return false;
        }

        private static readonly Dictionary<string, (string EnTitle, string EnSubtitle)> KnownEventTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["act19mini"] = ("Mirrors of the Past", "Reflections of Ancient Yan & Memory Fragments"),
            ["act20mini"] = ("Crossroads", "Siracusa Underworld & Modernization Chronicles"),
            ["act21mini"] = ("Jungle Crux", "Acahualla Rainforest & Sargon Expedition"),
            ["act22mini"] = ("Beyond the Clouds", "Columbia Aerospace & Skyward Odyssey"),
            ["act23mini"] = ("Echoes of History", "Victoria Archives & Forgotten Heroes"),
            ["act44side"] = ("In the Dust", "Columbia Scientific Expedition"),
            ["act45side"] = ("Castle of Dreams", "BanG Dream! Ave Mujica Collaboration"),
            ["act46side"] = ("Snowbound Ascent 1101", "Kjerag Modernization & Mountain Chronicle"),
            ["act47side"] = ("Promised Land", "Rim Billiton Frontier Campaign"),
            ["act48side"] = ("Vengeance of Janus", "Laterano & Siracusa Chronicle"),
            ["act49side"] = ("Farewell to Sui", "Great Yan Sui Sibling Odyssey"),
            ["act50side"] = ("Thunder and Bubbles", "Higashi Internal Conflict"),
            ["act51side"] = ("We, the People", "Victoria Worker Resistance"),
            ["act52side"] = ("Yesterday's Sea", "Iberia Deep Sea Voyage"),
            ["act53side"] = ("Until Terra Becomes an Orange", "Columbia Frontier Expedition"),
            ["act54side"] = ("Walking on Water in Moonlight", "Yan Floating Isle Legend")
        };

        private static readonly Dictionary<string, string> KnownEpisodeTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            // act19mini: Mirrors of the Past
            ["activities/act19mini/level_act19mini_st01"] = "Master Hu",
            ["activities/act19mini/level_act19mini_st02"] = "Mr. Pu",
            ["activities/act19mini/level_act19mini_st03"] = "The Story of the Stone",
            ["activities/act19mini/level_act19mini_st04"] = "White Brocade",
            ["activities/act19mini/level_act19mini_st05"] = "The Lost Swordsman",
            ["activities/act19mini/level_act19mini_st06"] = "Reaching the Peak",
            ["activities/act19mini/level_act19mini_st07"] = "Chronicles of the Town",

            // act20mini: Crossroads
            ["activities/act20mini/level_act20mini_st01"] = "The Red Wine Murder Case",
            ["activities/act20mini/level_act20mini_st02"] = "Business Acumen",
            ["activities/act20mini/level_act20mini_st03"] = "Witness",
            ["activities/act20mini/level_act20mini_st04"] = "Collision",
            ["activities/act20mini/level_act20mini_st05"] = "Unheard Melody",
            ["activities/act20mini/level_act20mini_st06"] = "The New Order",

            // act21mini: Jungle Crux
            ["activities/act21mini/level_act21mini_st01"] = "Soil Pathology",
            ["activities/act21mini/level_act21mini_st02"] = "Degenerative Hound Disease",
            ["activities/act21mini/level_act21mini_st03"] = "Banana Hemorrhagic Fever",
            ["activities/act21mini/level_act21mini_st04"] = "Obligate Parasitism",
            ["activities/act21mini/level_act21mini_st05"] = "Fever and the City",
            ["activities/act21mini/level_act21mini_st06"] = "Pandemic",
            ["activities/act21mini/level_act21mini_st07"] = "Wanderlust",

            // act45side: Castle of Dreams (Ave Mujica)
            ["activities/act45side/level_act45side_st01"] = "Crossroads Traffic Light",
            ["activities/act45side/level_act45side_01_beg"] = "Dream by the Grille Window",
            ["activities/act45side/level_act45side_01_end"] = "Dream by the Grille Window",
            ["activities/act45side/level_act45side_02_beg"] = "Both Ends of the Silent Corridor",
            ["activities/act45side/level_act45side_02_end"] = "Both Ends of the Silent Corridor",
            ["activities/act45side/level_act45side_03_beg"] = "Radiance in the Music Room",
            ["activities/act45side/level_act45side_03_end"] = "Radiance in the Music Room",
            ["activities/act45side/level_act45side_04_beg"] = "Labyrinth of the Guest Room",
            ["activities/act45side/level_act45side_04_end"] = "Labyrinth of the Guest Room",
            ["activities/act45side/level_act45side_05_beg"] = "Invitation to the Ball",
            ["activities/act45side/level_act45side_05_end"] = "Invitation to the Ball",
            ["activities/act45side/level_act45side_06_beg"] = "Echoing Hall",
            ["activities/act45side/level_act45side_06_end"] = "Echoing Hall",
            ["activities/act45side/level_act45side_07_beg"] = "Eternal & Splendid Collection",
            ["activities/act45side/level_act45side_07_end"] = "Eternal & Splendid Collection",
            ["activities/act45side/level_act45side_08_beg"] = "The Opening of the Fairy Tale",
            ["activities/act45side/level_act45side_08_end"] = "The Opening of the Fairy Tale"
        };

        private async Task<(List<StoryNode> SpecialOps, List<StoryNode> PublicAffairs)> LoadOfficialStoryReviewDataAsync()
        {
            if (_cachedSpecialOps != null && _cachedPublicAffairs != null)
            {
                return (_cachedSpecialOps, _cachedPublicAffairs);
            }

            await _storyReviewLock.WaitAsync();
            try
            {
                if (_cachedSpecialOps != null && _cachedPublicAffairs != null)
                {
                    return (_cachedSpecialOps, _cachedPublicAffairs);
                }

                var specialOps = new List<StoryNode>();
                var publicAffairs = new List<StoryNode>();

                // 1. Fetch EN table for English localized names and episode info
                string enJson = string.Empty;
                string[] enUrls =
                [
                    "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData_YoStar/main/en_US/gamedata/excel/story_review_table.json",
                    "https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData_YoStar@main/en_US/gamedata/excel/story_review_table.json"
                ];

                foreach (var url in enUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            enJson = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(enJson)) break;
                        }
                    }
                    catch { }
                }

                // 2. Fetch CN table for complete master catalog (contains all 485+ events including latest act45side etc.)
                string cnJson = string.Empty;
                string[] cnUrls =
                [
                    "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/story_review_table.json",
                    "https://cdn.jsdelivr.net/gh/Kengxxiao/ArknightsGameData@master/zh_CN/gamedata/excel/story_review_table.json"
                ];

                foreach (var url in cnUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            cnJson = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(cnJson)) break;
                        }
                    }
                    catch { }
                }

                // Parse EN lookups if available
                var enEventNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var enEpisodes = new Dictionary<string, (string Code, string Name, string AvgTag)>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(enJson))
                {
                    try
                    {
                        using var enDoc = JsonDocument.Parse(enJson);
                        foreach (var prop in enDoc.RootElement.EnumerateObject())
                        {
                            var item = prop.Value;
                            if (item.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString()))
                            {
                                enEventNames[prop.Name] = n.GetString()!;
                            }

                            if (item.TryGetProperty("infoUnlockDatas", out var datas) && datas.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var epElem in datas.EnumerateArray())
                                {
                                    string sCode = epElem.TryGetProperty("storyCode", out var c) ? c.GetString() ?? "" : "";
                                    string sName = epElem.TryGetProperty("storyName", out var nm) ? nm.GetString() ?? "" : "";
                                    string sTxt = epElem.TryGetProperty("storyTxt", out var t) ? t.GetString() ?? "" : "";
                                    string sTag = epElem.TryGetProperty("avgTag", out var tg) ? tg.GetString() ?? "" : "";

                                    if (!string.IsNullOrEmpty(sTxt))
                                    {
                                        enEpisodes[sTxt] = (sCode, sName, sTag);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Story Parser] Error parsing EN story_review_table.json");
                    }
                }

                // Choose master JSON (prefer CN for complete list, fallback to EN)
                string masterJson = !string.IsNullOrWhiteSpace(cnJson) ? cnJson : enJson;

                if (!string.IsNullOrWhiteSpace(masterJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(masterJson);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var item = prop.Value;
                            string actType = item.TryGetProperty("actType", out var at) ? at.GetString() ?? "" : "";
                            string rawName = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            string id = prop.Name;

                            // Handle canonical Main Theme Chapters: main_0, main_1, ..., main_15
                            if (actType == "MAIN_STORY" || id.StartsWith("main_", StringComparison.OrdinalIgnoreCase))
                            {
                                var mainEps = new List<StoryEpisodeInfo>();
                                if (item.TryGetProperty("infoUnlockDatas", out var mainDatas) && mainDatas.ValueKind == JsonValueKind.Array)
                                {
                                    int epIdx = 1;
                                    foreach (var epElem in mainDatas.EnumerateArray())
                                    {
                                        string storyId = epElem.TryGetProperty("storyId", out var sId) ? sId.GetString() ?? "" : "";
                                        string storyCode = epElem.TryGetProperty("storyCode", out var sCode) ? sCode.GetString() ?? "" : "";
                                        string storyName = epElem.TryGetProperty("storyName", out var sName) ? sName.GetString() ?? "" : "";
                                        string storyTxt = epElem.TryGetProperty("storyTxt", out var sTxt) ? sTxt.GetString() ?? "" : "";
                                        string avgTag = epElem.TryGetProperty("avgTag", out var aTag) ? aTag.GetString() ?? "" : "";

                                        // Localize episode info if EN has it
                                        if (enEpisodes.TryGetValue(storyTxt, out var enEp))
                                        {
                                            if (!string.IsNullOrWhiteSpace(enEp.Code) && !ContainsChinese(enEp.Code)) storyCode = enEp.Code;
                                            if (!string.IsNullOrWhiteSpace(enEp.Name) && !ContainsChinese(enEp.Name)) storyName = enEp.Name;
                                            if (!string.IsNullOrWhiteSpace(enEp.AvgTag) && !ContainsChinese(enEp.AvgTag)) avgTag = enEp.AvgTag;
                                        }
                                        else if (KnownEpisodeTranslations.TryGetValue(storyTxt, out var knownEpName))
                                        {
                                            storyName = knownEpName;
                                        }

                                        // Strictly sanitize any remaining Chinese strings
                                        if (ContainsChinese(storyName) || string.IsNullOrWhiteSpace(storyName))
                                        {
                                            storyName = KnownEpisodeTranslations.TryGetValue(storyTxt, out var trEp) ? trEp : $"Operation {(string.IsNullOrWhiteSpace(storyCode) ? $"{epIdx}" : storyCode)}";
                                        }

                                        if (ContainsChinese(storyCode))
                                        {
                                            storyCode = "";
                                        }

                                        // Determine tactical avg tag if not provided in JSON or Chinese
                                        if (ContainsChinese(avgTag) || string.IsNullOrWhiteSpace(avgTag))
                                        {
                                            if (storyTxt.EndsWith("_beg", StringComparison.OrdinalIgnoreCase))
                                                avgTag = "Before Operation";
                                            else if (storyTxt.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
                                                avgTag = "After Operation";
                                            else if (storyTxt.Contains("_st", StringComparison.OrdinalIgnoreCase) || storyTxt.Contains("guide", StringComparison.OrdinalIgnoreCase))
                                                avgTag = "Story Interlude";
                                            else
                                                avgTag = "Story Node";
                                        }

                                        if (string.IsNullOrWhiteSpace(storyCode))
                                        {
                                            if (storyTxt.Contains("guide", StringComparison.OrdinalIgnoreCase))
                                                storyCode = $"0-0{(epIdx == 1 ? "A" : "B")}";
                                            else
                                                storyCode = $"ST-{epIdx}";
                                        }

                                        if (!string.IsNullOrWhiteSpace(storyTxt))
                                        {
                                            mainEps.Add(new StoryEpisodeInfo
                                            {
                                                StoryId = string.IsNullOrEmpty(storyId) ? $"{id}_{epIdx}" : storyId,
                                                StoryCode = storyCode,
                                                StoryName = storyName,
                                                StoryPath = storyTxt,
                                                AvgTag = avgTag
                                            });
                                            epIdx++;
                                        }
                                    }
                                }

                                if (mainEps.Count > 0)
                                {
                                    _cachedMainChapterEpisodes[id] = mainEps;
                                }
                                continue;
                            }

                            if (actType != "ACTIVITY_STORY" && actType != "MINI_STORY")
                                continue;

                            // Determine localized title
                            string name = rawName;
                            string subtitle = actType == "MINI_STORY" ? "Confidential Personnel Vignette" : "Intermezzi & Canon Expedition";

                            if (enEventNames.TryGetValue(id, out var enName) && !string.IsNullOrWhiteSpace(enName) && !ContainsChinese(enName))
                            {
                                name = enName;
                            }
                            else if (KnownEventTranslations.TryGetValue(id, out var tr))
                            {
                                name = tr.EnTitle;
                                subtitle = tr.EnSubtitle;
                            }
                            else if (ContainsChinese(name))
                            {
                                if (id.StartsWith("act", StringComparison.OrdinalIgnoreCase) && id.EndsWith("mini", StringComparison.OrdinalIgnoreCase))
                                {
                                    name = $"Public Affairs Record #{id.Replace("act", "", StringComparison.OrdinalIgnoreCase).Replace("mini", "", StringComparison.OrdinalIgnoreCase)}";
                                }
                                else if (id.StartsWith("act", StringComparison.OrdinalIgnoreCase) && id.EndsWith("side", StringComparison.OrdinalIgnoreCase))
                                {
                                    name = $"Special Operation #{id.Replace("act", "", StringComparison.OrdinalIgnoreCase).Replace("side", "", StringComparison.OrdinalIgnoreCase)}";
                                }
                                else
                                {
                                    name = $"Operation {id.ToUpperInvariant()}";
                                }
                            }

                            var episodes = new List<StoryEpisodeInfo>();
                            if (item.TryGetProperty("infoUnlockDatas", out var datas) && datas.ValueKind == JsonValueKind.Array)
                            {
                                int epIdx = 1;
                                foreach (var epElem in datas.EnumerateArray())
                                {
                                    string storyId = epElem.TryGetProperty("storyId", out var sId) ? sId.GetString() ?? "" : "";
                                    string storyCode = epElem.TryGetProperty("storyCode", out var sCode) ? sCode.GetString() ?? "" : "";
                                    string storyName = epElem.TryGetProperty("storyName", out var sName) ? sName.GetString() ?? "" : "";
                                    string storyTxt = epElem.TryGetProperty("storyTxt", out var sTxt) ? sTxt.GetString() ?? "" : "";

                                    // Localize episode info if EN has it
                                    if (enEpisodes.TryGetValue(storyTxt, out var enEp))
                                    {
                                        if (!string.IsNullOrWhiteSpace(enEp.Code) && !ContainsChinese(enEp.Code)) storyCode = enEp.Code;
                                        if (!string.IsNullOrWhiteSpace(enEp.Name) && !ContainsChinese(enEp.Name)) storyName = enEp.Name;
                                    }
                                    else if (KnownEpisodeTranslations.TryGetValue(storyTxt, out var knownEpName))
                                    {
                                        storyName = knownEpName;
                                    }

                                    // Strictly sanitize any remaining Chinese strings
                                    if (ContainsChinese(storyName) || string.IsNullOrWhiteSpace(storyName))
                                    {
                                        storyName = KnownEpisodeTranslations.TryGetValue(storyTxt, out var trEp) ? trEp : $"Operation {(string.IsNullOrWhiteSpace(storyCode) ? $"ST-{epIdx}" : storyCode)}";
                                    }

                                    if (ContainsChinese(storyCode) || string.IsNullOrWhiteSpace(storyCode))
                                    {
                                        storyCode = $"ST-{epIdx}";
                                    }

                                    // Determine tactical avg tag (Before / After Operation / Interlude)
                                    string avgTag = "";
                                    if (storyTxt.EndsWith("_beg", StringComparison.OrdinalIgnoreCase))
                                        avgTag = "Before Operation";
                                    else if (storyTxt.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
                                        avgTag = "After Operation";
                                    else if (storyTxt.Contains("_st", StringComparison.OrdinalIgnoreCase))
                                        avgTag = "Story Interlude";

                                    if (!string.IsNullOrWhiteSpace(storyTxt))
                                    {
                                        episodes.Add(new StoryEpisodeInfo
                                        {
                                            StoryId = string.IsNullOrEmpty(storyId) ? $"{id}_{epIdx}" : storyId,
                                            StoryCode = storyCode,
                                            StoryName = string.IsNullOrEmpty(storyName) ? $"Episode {epIdx}" : storyName,
                                            StoryPath = storyTxt,
                                            AvgTag = avgTag
                                        });
                                        epIdx++;
                                    }
                                }
                            }

                            if (episodes.Count == 0)
                                continue;

                            var node = new StoryNode
                            {
                                Id = id,
                                Code = actType == "MINI_STORY" ? "PUBLIC AFFAIRS" : "SPECIAL OPERATION",
                                Title = name,
                                SubTitle = subtitle,
                                Description = actType == "MINI_STORY"
                                    ? $"Rhodes Island archival vignette and personal memoir ({name}) with {episodes.Count} canon episodes."
                                    : $"Major tactical operation and military campaign recorded in Arknights archives ({name}) with {episodes.Count} story stages.",
                                NodeType = actType == "MINI_STORY" ? StoryNodeType.StoryCollection : StoryNodeType.SideStory,
                                BannerUrl = GetEventBannerUrl(id),
                                YouTubePvId = EventYouTubePvMap.TryGetValue(id, out var pv) ? pv : null,
                                Episodes = episodes
                            };

                            if (actType == "MINI_STORY")
                            {
                                publicAffairs.Add(node);
                            }
                            else
                            {
                                specialOps.Add(node);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Story Parser] Error parsing story_review_table.json");
                    }
                }

                if (specialOps.Count == 0)
                {
                    specialOps = BuildLimitedEventsGroup().LinkedEvents;
                }

                _cachedPublicAffairs = publicAffairs;
                _cachedSpecialOps = specialOps;

                _logger.LogInformation("[Story Review] Successfully loaded {SpecialCount} Special Operations and {PublicCount} Public Affairs vignettes from official archives.",
                    _cachedSpecialOps.Count, _cachedPublicAffairs.Count);

                return (_cachedSpecialOps, _cachedPublicAffairs);
            }
            finally
            {
                _storyReviewLock.Release();
            }
        }

        private static readonly Dictionary<string, string> FallbackBanners = new(StringComparer.OrdinalIgnoreCase)
        {
            ["act20mini"] = $"{AVG_BG_CDN_BASE}/33_g4_srctheater.png",
            ["act21mini"] = $"{AVG_BG_CDN_BASE}/bg_jungle_1.png",
            ["act22mini"] = $"{AVG_BG_CDN_BASE}/27_g26_dusk_wild.png",
            ["act23mini"] = $"{AVG_BG_CDN_BASE}/37_g10_wildbattlefield.png",
            ["act46side"] = $"{AVG_BG_CDN_BASE}/25_g05_mountaincity_d.png",
            ["act47side"] = $"{AVG_BG_CDN_BASE}/42_g1_mine.png",
            ["act48side"] = $"{AVG_BG_CDN_BASE}/26_g1_laterano_cathedralfront.png",
            ["act49side"] = $"{AVG_BG_CDN_BASE}/25_g01_yanstreet_d.png",
            ["act50side"] = $"{AVG_BG_CDN_BASE}/bg_thundercloud.png",
            ["act51side"] = $"{AVG_BG_CDN_BASE}/21_G4_victoria_street_d_ruins.png",
            ["act52side"] = $"{AVG_BG_CDN_BASE}/27_g24_cloudy_sea.png",
            ["act53side"] = $"{AVG_BG_CDN_BASE}/27_g26_dusk_wild.png",
            ["act54side"] = $"{AVG_BG_CDN_BASE}/25_g03_yandowntown_n.png"
        };

        private static string GetEventBannerUrl(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return $"{BG_MAIN_BANNER_BASE}/zone_page_main_0.png";

            if (FallbackBanners.TryGetValue(eventId, out var fallback))
                return fallback;

            // Primary source: Official Arknights in-game event cover collage banner
            return $"https://r2.m31ns.top/img/banners/{eventId.ToLowerInvariant()}.png";
        }

        private static List<StoryEpisodeInfo> GenerateChapterEpisodes(int chapterNum, int startStage, int endStage)
        {
            var list = new List<StoryEpisodeInfo>();
            string chStr = chapterNum.ToString("D2");

            for (int s = startStage; s <= endStage; s++)
            {
                string stageStr = s.ToString("D2");
                string code = $"{chapterNum}-{s}";
                list.Add(new StoryEpisodeInfo
                {
                    StoryId = $"{chapterNum}_{s}",
                    StoryCode = code,
                    StoryName = $"Operation {code}",
                    StoryPath = $"obt/main/level_main_{chStr}-{stageStr}"
                });
            }

            return list;
        }

        private string GenerateCuratedFallbackScript(string storyId, string? title)
        {
            string opName = !string.IsNullOrEmpty(title) ? title.Split('-')[0].Trim() : "Operator";
            return $@"[PlayMusic(key=""$default_ambient"", volume=0.7)]
[Background(image=""bg_rhodescom"")]
[charslot]
[name=""PRTS""] Accessing Rhodes Island Human Resources confidential archives...
[name=""PRTS""] Decrypting personnel service logs and psychological evaluations for {opName}.
[name=""Kal'tsit""] File clearance granted by the Medical Department.
[name=""Kal'tsit""] Throughout our campaigns, every operator brings their unique resolve and trials to Rhodes Island.
[name=""Kal'tsit""] Read through this record carefully, Doctor. Understanding our comrades is the foundation of our tactical success.";
        }

        private static string FormatFactionName(string factionId) => factionId.ToUpperInvariant() switch
        {
            "LUNGMEN" => "Lungmen Special Administrative Region",
            "RHODES" => "Rhodes Island Pharmaceuticals",
            "KAZDEL" => "Kazdel Sarkaz Court",
            "URSUS" => "Ursus Empire",
            "COLUMBIA" => "Columbia Union",
            "VICTORIA" => "Kingdom of Victoria",
            "IBERIA" => "Kingdom of Iberia",
            "KJERAG" => "Holy City of Kjerag",
            "YAN" => "Great Yan Empire",
            "LATERANO" => "Laterano Pontifical State",
            "SIRACUSA" => "Siracusa Mafia Families",
            "HIGASHI" => "Far East Higashi",
            _ => $"Region / Organization: {factionId}"
        };

        #endregion
    }
}
