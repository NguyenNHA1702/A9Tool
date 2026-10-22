using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Models.Entities
{
    /// <summary>
    /// Type of storyline entry within Arknights lore.
    /// </summary>
    public enum StoryNodeType
    {
        MainTheme,          // Main storyline (Chapters 0 - 14)
        Intermezzi,         // Major canon lore interludes (Darknights Memoir, A Walk in the Dust)
        SideStory,          // Full-scale seasonal event storylines
        StoryCollection,    // Vignettes / Anthology short stories
        OperatorRecord      // Personal character-specific chronicles (Truyện ký riêng)
    }

    /// <summary>
    /// Visual effect triggered during dialogue transitions.
    /// </summary>
    public enum StoryVisualEffect
    {
        None,
        CameraShake,        // Rung chấn màn hình
        ScreenFlashWhite,   // Chớp sáng trắng
        ScreenFlashBlack,   // Đen màn hình (Fade out)
        CameraFocus         // Thu phóng tâm điểm
    }

    /// <summary>
    /// Display mode preference for reading stories.
    /// </summary>
    public enum StoryDisplayMode
    {
        VisualNovel,        // Trình đọc VN tương tác toàn màn hình (chuẩn game)
        LightNovel          // Trình đọc LN cuộn dọc kèm hình minh họa CG
    }

    /// <summary>
    /// Represents a single node on the 3-tier timeline tree or archive catalog.
    /// </summary>
    public class StoryNode
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public StoryNodeType NodeType { get; set; }
        public string? ChapterId { get; set; }
        public string? ActNumber { get; set; }
        public string? RelatedFaction { get; set; }
        public string? RelatedOperatorId { get; set; }
        public string? RelatedOperatorName { get; set; }
        public string? RelatedOperatorAvatar { get; set; }
        public string? BannerUrl { get; set; }
        public string? DefaultBgmKey { get; set; }
        public string? YouTubePvId { get; set; }
        public int SortOrder { get; set; }

        public List<StoryEpisodeInfo> Episodes { get; set; } = [];
        public List<StoryNode> ChildRecords { get; set; } = [];
    }

    /// <summary>
    /// Represents an individual story episode or stage narrative part.
    /// </summary>
    public class StoryEpisodeInfo
    {
        public string StoryId { get; set; } = string.Empty;
        public string StoryCode { get; set; } = string.Empty;
        public string StoryName { get; set; } = string.Empty;
        public string StoryGroup { get; set; } = string.Empty;
        public string? StoryPath { get; set; }
        public string? AvgTag { get; set; } // Before / After / Combat
        public string? Summary { get; set; }
    }

    /// <summary>
    /// Parsed dialogue line with speaker metadata, avatars, background CG, BGM, and visual fx.
    /// </summary>
    public class StoryDialogueLine
    {
        public int Index { get; set; }
        public string? SpeakerName { get; set; }
        public string? SpeakerAvatarUrl { get; set; }
        public string DialogueText { get; set; } = string.Empty;
        public bool IsNarrator => string.IsNullOrWhiteSpace(SpeakerName);

        // Player decision choice (Doctor dialogue options)
        public bool IsDecision { get; set; }
        public List<string> DecisionOptions { get; set; } = [];
        public List<string> DecisionValues { get; set; } = [];
        public int DecisionIndex { get; set; }
        public string? PredicateReferences { get; set; }

        // Ambient stage direction or sound cues (e.g. "<Background black>", "An EKG beeps...")
        public bool IsStageDirection { get; set; }

        // Inline CG Illustration / Event art
        public string? IllustrationUrl { get; set; }

        // Character presentation
        public string? LeftCharacterId { get; set; }
        public string? LeftCharacterAvatarUrl { get; set; }
        public string? LeftCharacterSpriteUrl { get; set; }
        public string? RightCharacterId { get; set; }
        public string? RightCharacterAvatarUrl { get; set; }
        public string? RightCharacterSpriteUrl { get; set; }
        public string ActiveSpeakerSide { get; set; } = "left"; // "left", "right", or "none"

        // Scene environmental audio & visuals
        public string? BackgroundCgUrl { get; set; }
        public bool IsSceneChange { get; set; }
        public string? BgmTrackKey { get; set; }
        public string? BgmTrackTitle { get; set; }
        public string? BgmAudioUrl { get; set; }
        public string? SfxUrl { get; set; }
        public StoryVisualEffect Effect { get; set; } = StoryVisualEffect.None;
    }

    /// <summary>
    /// Character appearing in a story chapter for the header showcase.
    /// </summary>
    public class StoryCharacterShowcase
    {
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    /// <summary>
    /// Complete parsed bundle for a story chapter ready for VN/LN presentation.
    /// </summary>
    public class StoryScriptBundle
    {
        public string StoryId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? InitialBgmUrl { get; set; }
        public string? InitialBgmTitle { get; set; }
        public string? InitialBackgroundUrl { get; set; }
        public List<StoryDialogueLine> Lines { get; set; } = [];
        public List<StoryCharacterShowcase> CharactersAppearing { get; set; } = [];
        public int TotalLines => Lines.Count;
    }

    /// <summary>
    /// Act group for organizing main theme timeline sections.
    /// </summary>
    public class StoryActGroup
    {
        public string ActId { get; set; } = string.Empty;
        public string ActTitle { get; set; } = string.Empty;
        public string ActDescription { get; set; } = string.Empty;
        public List<StoryNode> MainNodes { get; set; } = [];
        public List<StoryNode> LinkedEvents { get; set; } = [];
    }

    /// <summary>
    /// Faction group for organizing personnel archives (Operator Records).
    /// </summary>
    public class FactionStoryArchive
    {
        public string FactionId { get; set; } = string.Empty;
        public string FactionName { get; set; } = string.Empty;
        public List<StoryNode> OperatorRecords { get; set; } = [];
    }

    /// <summary>
    /// User reading progress bookmark.
    /// </summary>
    public class StoryBookmark
    {
        public string StoryId { get; set; } = string.Empty;
        public string? StoryPath { get; set; }
        public string Title { get; set; } = string.Empty;
        public int LineIndex { get; set; }
        public int TotalLines { get; set; }
        public string? LineExcerpt { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}
