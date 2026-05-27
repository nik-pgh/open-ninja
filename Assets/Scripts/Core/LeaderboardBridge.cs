using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Thin shim for the JS-side leaderboard submit. WebGL build links the
    /// real native function from Assets/Plugins/WebGL/Leaderboard.jslib;
    /// every other target gets a no-op so the game runs unchanged.
    /// </summary>
    public static class LeaderboardBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SubmitScoreToLeaderboard(string nickname, int score);
#endif

        public static void TrySubmit(string nickname, int score)
        {
            if (string.IsNullOrWhiteSpace(nickname) || score <= 0) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            SubmitScoreToLeaderboard(nickname, score);
#else
            // Editor / standalone: log so we can confirm wiring works in PlayMode.
            Debug.Log($"LeaderboardBridge (stub): would submit {nickname} → {score}");
#endif
        }
    }
}
