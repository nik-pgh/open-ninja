using UnityEngine;

namespace OpenNinja
{
    public enum CubeRole
    {
        Normal = 0,
        Bonus = 1,
        Danger = 2,
    }

    /// <summary>
    /// Per-material data driving a Cube at spawn time. Each in-game material
    /// (Wood, Stone, Metal, Crystal, Spiked, Rubber) is an instance of this
    /// asset. Mass and impulse drive physics; basePoints and role drive
    /// scoring; renderMaterial / displayScale drive presentation; bounce and
    /// audio fields drive sense-of-mass feedback.
    /// </summary>
    [CreateAssetMenu(menuName = "OpenNinja/Cube Material", fileName = "CubeMaterial")]
    public class CubeMaterial : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public CubeRole role = CubeRole.Normal;
        public int basePoints = 1;

        [Header("Physics")]
        [Min(0.05f)] public float mass = 1f;
        [Range(0.2f, 2f)] public float displayScale = 1f;
        [Range(0.1f, 2f)] public float bouncinessMultiplier = 1f;
        /// <summary>If both components are 0, the spawner falls back to its default range.</summary>
        public Vector2 launchImpulseOverride;

        [Header("Presentation")]
        public Material renderMaterial;
        public Color burstTint = Color.white;

        [Header("Audio")]
        public AudioClip impactClip;
        [Range(0.1f, 4f)] public float audioPitchAtMassOne = 1f;
        [Range(0f, 1f)] public float audioVolume = 0.7f;

        public bool HasLaunchOverride =>
            launchImpulseOverride.x > 0f && launchImpulseOverride.y > 0f;
    }
}
