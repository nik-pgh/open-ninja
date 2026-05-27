using System;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// PlayerPrefs-backed app-wide session state. Static because there are no
    /// per-instance lifetimes; both scenes read/write the same backing store.
    /// </summary>
    public static class PlayerSession
    {
        private const string NicknameKey     = "OpenNinja.Nickname";
        private const string BestScoreKey    = "OpenNinja.BestScore";
        private const int    NicknameMaxChars = 16;

        public static event Action<int> OnBestScoreChanged;

        public static string Nickname
        {
            get => PlayerPrefs.GetString(NicknameKey, "");
            set
            {
                string sanitized = (value ?? string.Empty).Trim();
                if (sanitized.Length > NicknameMaxChars)
                    sanitized = sanitized.Substring(0, NicknameMaxChars);
                PlayerPrefs.SetString(NicknameKey, sanitized);
                PlayerPrefs.Save();
            }
        }

        public static int BestScore
        {
            get => PlayerPrefs.GetInt(BestScoreKey, 0);
            set
            {
                int clamped = Mathf.Max(0, value);
                PlayerPrefs.SetInt(BestScoreKey, clamped);
                PlayerPrefs.Save();
                OnBestScoreChanged?.Invoke(clamped);
            }
        }

        /// <summary>Only writes if the candidate strictly beats the current best.</summary>
        public static bool TrySetBestScore(int candidate)
        {
            if (candidate <= BestScore) return false;
            BestScore = candidate;
            // WebGL: forward new personal-bests to the leaderboard backend.
            // No-op on editor + standalone (see LeaderboardBridge).
            LeaderboardBridge.TrySubmit(Nickname, candidate);
            return true;
        }
    }
}
