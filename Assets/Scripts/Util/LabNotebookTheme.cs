using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Single source of truth for the lab-notebook visual identity. Read by
    /// Editor setup scripts at scene build time. Tuning the look = editing
    /// constants here and re-running the setup scripts.
    /// </summary>
    public static class LabNotebookTheme
    {
        // ---- Palette ----
        public static readonly Color PaperCream  = new Color(0.992f, 0.988f, 0.969f, 1f); // #fdfcf7
        public static readonly Color InkDark     = new Color(0.102f, 0.102f, 0.102f, 1f); // #1a1a1a
        public static readonly Color InkRed      = new Color(0.784f, 0.196f, 0.196f, 1f); // #c83232
        public static readonly Color InkGreen    = new Color(0.290f, 0.541f, 0.227f, 1f); // #4a8a3a
        public static readonly Color InkAmber    = new Color(0.784f, 0.616f, 0.227f, 1f); // #c89d3a
        public static readonly Color GridBlue    = new Color(0.157f, 0.353f, 0.549f, 0.10f);
        public static readonly Color MarginRed   = new Color(0.784f, 0.196f, 0.196f, 0.50f);
        public static readonly Color SubduedInk  = new Color(0.333f, 0.333f, 0.333f, 1f); // #555
        public static readonly Color GameOverDim = new Color(0f, 0f, 0f, 0.55f);

        // ---- Lab-room backdrop (gameplay scene) ----
        // Brighter than a stock "whiteboard grey" so the room reads as
        // well-lit and matches the cream paper of the start screen in
        // perceived luminance. Floor is only a hair darker so the
        // wall→floor horizon is a subtle hint rather than a dark band.
        public static readonly Color WhiteboardWall = new Color(0.992f, 0.988f, 0.969f, 1f); // matches PaperCream
        public static readonly Color RubberFloor    = new Color(0.945f, 0.945f, 0.945f, 1f); // #f1f1f1
        public static readonly Color MarkerBlue     = new Color(0.102f, 0.298f, 0.722f, 0.65f);
        public static readonly Color MarkerRed      = new Color(0.737f, 0.157f, 0.157f, 0.65f);
        public static readonly Color MarkerBlack    = new Color(0.176f, 0.176f, 0.176f, 0.40f);

        // ---- Typography (font sizes in canvas units; portrait ref 1080x1920) ----
        public const float TitleSize        = 144f;
        public const float SubtitleSize     = 36f;
        public const float BestSize         = 48f;
        public const float DividerSize      = 28f;
        public const float RowNameSize      = 56f;
        public const float RowPointsSize    = 48f;
        public const float BadgeSize        = 24f;
        public const float InputSize        = 64f;
        public const float ButtonSize       = 88f;
        public const float ArrowSize        = 36f;

        // HUD (gameplay sticky-note cards)
        public const float HudLabelSize     = 22f;
        public const float HudValueSize     = 60f;
        public const float HudNicknameSize  = 36f;
        public const float HudHeartSize     = 56f;
        public const float ComboBadgeSize   = 48f;
        public const float ComboPopupSize   = 64f;

        // Game-over panel
        public const float GameOverTitleSize  = 96f;
        public const float GameOverScoreSize  = 72f;
        public const float NewBestStampSize   = 40f;
        public const float GameOverButtonSize = 56f;

        // ---- Geometry ----
        public const float MarginLineX          = 110f;
        public const float TitleRotation        = -2f;
        public const float ButtonRotation       = -1.5f;
        public const float BadgeRotation        = -2f;
        public const float HudCardRotationLeft  = -3f;
        public const float HudCardRotationRight = 2f;
        public const float StampRotation        = -8f;
        public const float ShadowOffsetSmall    = 4f;
        public const float ShadowOffsetBig      = 8f;

        // ---- Asset paths ----
        public const string FontAssetPath        = "Assets/Fonts/Caveat-Bold SDF.asset";
        public const string GraphPaperSpritePath = "Assets/Textures/GraphPaper.png";
        public const string CaveatTtfPath        = "Assets/Fonts/Caveat-Bold.ttf";
    }
}
