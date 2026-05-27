mergeInto(LibraryManager.library, {
  // Called from C# on each new personal-best. Forwards to the page's
  // submitNinjaScore() helper which POSTs to /api/scores.
  // Signature: void SubmitScoreToLeaderboard(string nickname, int score)
  SubmitScoreToLeaderboard: function (nicknamePtr, score) {
    try {
      var nickname = UTF8ToString(nicknamePtr);
      if (typeof window.submitNinjaScore === "function") {
        window.submitNinjaScore({ nickname: nickname, score: score });
      }
    } catch (_) { /* never let the page break the game */ }
  },
});
