using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Plays a clip once at a world position with custom pitch and volume.
    /// AudioSource.PlayClipAtPoint can't change pitch, so this builds a
    /// throwaway AudioSource, configures it, and destroys it when done.
    /// No-op if the clip is null.
    /// </summary>
    public static class AudioOneShot
    {
        public static void Play(AudioClip clip, Vector3 position, float pitch = 1f, float volume = 1f)
        {
            if (clip == null) return;

            var go = new GameObject($"OneShot_{clip.name}");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, 0.1f, 4f);
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 0f; // 2D — the playfield is flat
            source.Play();

            // Account for pitch — at pitch != 1, real playback length scales.
            float lifetime = clip.length / Mathf.Max(0.01f, source.pitch);
            Object.Destroy(go, lifetime + 0.1f);
        }
    }
}
