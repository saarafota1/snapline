using GameKit;

namespace Snapline.App
{
    /// <summary>
    /// Everything Snapline reports to an attribution network, in one place.
    ///
    /// Talks to GameKitRuntime.Analytics, never to a vendor SDK. With no adapter installed the kit
    /// hands back a null service and every call here is a no-op, so the game behaves identically
    /// whether or not Meta is wired up.
    ///
    /// Two rules from the playbook shape what is sent:
    ///
    ///   Use the GameEvents constants. The adapter maps them onto the network's *standard* events,
    ///   and that mapping is the whole point — Meta can optimise a campaign towards people likely
    ///   to fire fb_mobile_level_achieved, whereas the same thing under a custom name is only a
    ///   dashboard counter.
    ///
    ///   Report cumulative progress, not the level index. "This install reached 20 levels"
    ///   describes a player; "solved level 12" means nothing outside this game.
    /// </summary>
    public static class Telemetry
    {
        /// <summary>
        /// A level was finished. Value is how many levels this install has completed in total.
        /// </summary>
        public static void LevelCompleted()
        {
            int total = SaveSystem.LevelsCompleted();
            GameKitRuntime.Analytics.LogEvent(GameEvents.LevelCompleted, total);

            // Snapline has no separate tutorial; clearing the first level IS getting through the
            // opening, and it is the earliest signal that an install turned into a player.
            if (total == 1) GameKitRuntime.Analytics.LogEvent(GameEvents.TutorialCompleted);
        }

        /// <summary>
        /// A new personal best in endless. The clearest engagement milestone the game has, and the
        /// one worth asking a campaign to find more of.
        /// </summary>
        public static void NewHighScore(long score)
        {
            GameKitRuntime.Analytics.LogEvent(GameEvents.AchievementUnlocked, score);
        }

        /// <summary>The player chose a rewarded ad and watched it to the end.</summary>
        public static void RewardedAdWatched()
        {
            GameKitRuntime.Analytics.LogEvent(GameEvents.RewardedAdWatched);
        }

        /// <summary>
        /// Call on launch and on every resume. Attribution networks measure retention in sessions,
        /// and Android does not hand them a resume after a warm start.
        /// </summary>
        public static void ResumeSession()
        {
            GameKitRuntime.Analytics.ResumeSession();
        }
    }
}
