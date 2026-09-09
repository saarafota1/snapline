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
    /// the console harness against real simulated play rather than guessed at.
    ///
    /// Everything here is read off `Design/references/toolbox.png` and the result cards. Where a
    /// number was not in the references it is derived and labelled as such — the difference matters,
    /// because a fitted number is a guess with arithmetic on top.
    /// </summary>
    public static class Economy
    {
        // --- prices, all straight off toolbox.png -------------------------------------------

        public const int UndoPrice = 50;
        public const int ShufflePrice = 80;
        public const int HammerPrice = 120;

        /// <summary>Buying your way past a dead board, from popup_no_more_moves.png.</summary>
        public const int ContinuePrice = 80;

        // --- payouts -------------------------------------------------------------------------

        /// <summary>A finished level, from popup_level_complete.png.</summary>
        public const int LevelReward = 50;

        /// <summary>A finished daily challenge, from daily_challenge.png.</summary>
        public const int DailyReward = 100;

        /// <summary>One rewarded ad, from toolbox.png.</summary>
        public const int AdReward = 50;

        public static int Price(Tool tool) => tool switch
        {
            Tool.Undo => UndoPrice,
            Tool.Shuffle => ShufflePrice,
            Tool.Hammer => HammerPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(tool)),
        };

        /// <summary>
        /// What an endless run pays out.
        ///
        /// FITTED, not given. The references contain exactly one data point — popup_great_run.png
        /// shows 42 lines and a best combo of x6 paying 180 coins — and this is the simplest formula
        /// through it: 42*4 + 6*2 = 180. Any number of curves pass through one point, so treat this
        /// as a starting position to be measured, not as a designed economy.
        ///
        /// Lines carry it rather than score, because score already scales with the combo multiplier
        /// and paying on both would compound the same achievement twice.
        /// </summary>
        public static int EndlessReward(int linesCleared, int bestCombo) =>
            Math.Max(0, linesCleared) * 4 + Math.Max(0, bestCombo) * 2;

        /// <summary>
        /// How long a run's worth of coins takes to buy each tool, for sanity rather than for the
        /// game. A median run of ~40 lines pays about 172, which buys three undos, two shuffles or
        /// one hammer — the ordering the reference prices imply.
        /// </summary>
        public static int ToolsAffordable(int coins, Tool tool) => coins / Price(tool);
    }
}
