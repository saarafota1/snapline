using System;

namespace Snapline.Core
{
    /// <summary>The three power-ups. Order is a save format: never renumber these.</summary>
    public enum Tool
    {
        Undo = 0,
        Shuffle = 1,
        Hammer = 2,
    }

    /// <summary>
    /// Coin prices and payouts, in one place and with no Unity in sight so they can be balanced in
    /// the console harness against simulated play rather than guessed at: `dotnet run -- economy`.
    ///
    /// Rebalanced on 14 Sep 2026. The first numbers were read straight off the mock-ups, and in play
    /// they made tools nearly free: an ordinary endless run paid 100-200 coins, a hammer every run,
    /// and the store's video paid 50 coins with no limit, so coins were unlimited for anyone willing
    /// to tap. The aim now is a tool every few runs, with a video worth about a third of a hammer and
    /// capped per day, so tools stay a treat and coins keep a value.
    /// </summary>
    public static class Economy
    {
        // --- prices --------------------------------------------------------------------------

        public const int UndoPrice = 100;
        public const int ShufflePrice = 150;
        public const int HammerPrice = 250;

        /// <summary>Buying your way past a dead board on the NO MORE MOVES card.</summary>
        public const int ContinuePrice = 150;

        // --- payouts -------------------------------------------------------------------------

        /// <summary>Coins per star, paid once per star: a first 2-star clear pays 20, raising it to 3 pays 10 more.</summary>
        public const int LevelRewardPerStar = 10;

        /// <summary>A finished daily challenge, on top of that weekday's reward.</summary>
        public const int DailyReward = 50;

        /// <summary>One rewarded video in the store.</summary>
        public const int AdReward = 40;

        /// <summary>
        /// Store videos that pay, per calendar day. Without a cap a video button is an unlimited coin
        /// tap, which empties the economy and trains players to farm ads rather than play.
        /// </summary>
        public const int AdRewardsPerDay = 5;

        /// <summary>What a brand-new player starts with: enough for one undo, not a hammer.</summary>
        public const int StartingCoins = 120;

        public static int Price(Tool tool) => tool switch
        {
            Tool.Undo => UndoPrice,
            Tool.Shuffle => ShufflePrice,
            Tool.Hammer => HammerPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(tool)),
        };

        /// <summary>
        /// What finishing a level pays: only for stars not earned before. Replaying a level without
        /// improving it pays nothing, so level 1 cannot be farmed.
        /// </summary>
        public static int LevelReward(int previousStars, int stars) =>
            Math.Max(0, stars - Math.Max(0, previousStars)) * LevelRewardPerStar;

        /// <summary>
        /// What an endless run pays: a coin a line, and two per step of the best combo, which rewards
        /// skill over sheer length. A 30-line run with a x4 combo pays 38.
        /// </summary>
        public static int EndlessReward(int linesCleared, int bestCombo) =>
            Math.Max(0, linesCleared) + Math.Max(0, bestCombo) * 2;
    }
}
