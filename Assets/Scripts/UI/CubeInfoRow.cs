using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// One row of the start-screen cube info table. Built as a prefab whose
    /// fields are populated by Bind(CubeMaterial). The icon sprite is created
    /// at runtime from the material's albedo texture — no separate icon assets.
    /// </summary>
    public class CubeInfoRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text pointsLabel;
        [SerializeField] private TMP_Text roleBadge;
        [SerializeField] private Image roleBadgeBackground;

        private static readonly Color BadgeNormal = new Color(0.26f, 0.63f, 0.28f, 1f);
        private static readonly Color BadgeBonus  = new Color(0.98f, 0.75f, 0.18f, 1f);
        private static readonly Color BadgeDanger = new Color(0.90f, 0.22f, 0.21f, 1f);
        private static readonly Color PointsPositive = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color PointsNegative = new Color(1f, 0.4f, 0.4f, 1f);

        public void Bind(CubeMaterial mat)
        {
            if (mat == null) return;

            if (nameLabel != null) nameLabel.text = mat.displayName;
            if (pointsLabel != null)
            {
                pointsLabel.text = mat.role == CubeRole.Danger ? "-1 life" : $"+{mat.basePoints}";
                pointsLabel.color = mat.role == CubeRole.Danger ? PointsNegative : PointsPositive;
            }
            if (roleBadge != null) roleBadge.text = mat.role.ToString().ToUpperInvariant();
            if (roleBadgeBackground != null) roleBadgeBackground.color = ColorForRole(mat.role);
            if (icon != null) icon.sprite = SpriteFromMaterial(mat.renderMaterial);
        }

        private static Color ColorForRole(CubeRole role) => role switch
        {
            CubeRole.Bonus  => BadgeBonus,
            CubeRole.Danger => BadgeDanger,
            _               => BadgeNormal,
        };

        private static Sprite SpriteFromMaterial(Material renderMat)
        {
            if (renderMat == null) return null;
            var tex = renderMat.mainTexture as Texture2D;
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        }
    }
}
