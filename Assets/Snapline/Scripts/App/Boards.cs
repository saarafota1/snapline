using GameKit;
using UnityEngine;

namespace Snapline.App
{
    /// <summary>
    /// The global leaderboards: where a score goes to be compared with other players.
    ///
    /// Everything here is fire-and-forget and refuses to matter. A board id that was never filled in,
    /// a player with no network, a game whose Unity project was never linked - each ends as a log
    /// line and nothing else. Nobody loses a run because a leaderboard was unreachable.
    ///
    /// The ids come from GameKitConfig rather than from here, because they have to match what the
    /// owner created in the Unity dashboard, character for character.
    /// </summary>
    public static class Boards
    {
        public static string BestScoreId => GameKitRuntime.Config != null ? GameKitRuntime.Config.leaderboardBestScore : null;
        public static string LevelsSolvedId => GameKitRuntime.Config != null ? GameKitRuntime.Config.leaderboardLevelsSolved : null;
        public static string DailyId => GameKitRuntime.Config != null ? GameKitRuntime.Config.leaderboardDailyChallenge : null;

        /// <summary>True when a board can be read or written at all. False offline, and in the editor.</summary>
        public static bool Available =>
            GameKitRuntime.Leaderboards != null && GameKitRuntime.Leaderboards.IsAvailable;

        /// <summary>An endless run that has just ended.</summary>
        public static void SubmitBestScore(long score) => Submit(BestScoreId, score, "best score");

        /// <summary>How many levels the player has finished, after finishing one.</summary>
        public static void SubmitLevelsSolved(int levels) => Submit(LevelsSolvedId, levels, "levels solved");

        /// <summary>A finished daily challenge, scored.</summary>
        public static void SubmitDaily(long score) => Submit(DailyId, score, "daily challenge");

        private static async void Submit(string id, double value, string what)
        {
            if (string.IsNullOrEmpty(id) || !Available) return;

            bool sent = await GameKitRuntime.Leaderboards.SubmitAsync(id, value);
            Debug.Log(sent
                ? $"[Snapline] leaderboard {what}: submitted {value}"
                : $"[Snapline] leaderboard {what}: submit failed, keeping the local score");
        }
    }
}
