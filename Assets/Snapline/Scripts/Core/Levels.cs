using System;

namespace Snapline.Core
{
    /// <summary>Which way the game is being played.</summary>
    public enum GameMode
    {
        /// <summary>No objective. Play until nothing fits. Scored, and the only mode with a high score.</summary>
        Endless = 0,

        /// <summary>Clear a line target inside a move budget.</summary>
        Level = 1,
    }

    /// <summary>How a level is won and lost.</summary>
    public sealed class LevelObjective
    {
        public int LineTarget;
        public int MoveBudget;

        public LevelObjective(int lineTarget, int moveBudget)
        {
            LineTarget = lineTarget;
            MoveBudget = moveBudget;
        }
    }

    /// <summary>One level in the ladder.</summary>
    public sealed class LevelDef
    {
        /// <summary>1-based, and what the player sees.</summary>
        public readonly int Number;
        public readonly int LineTarget;
        public readonly int MoveBudget;

        /// <summary>
        /// Fixed per level, so a level is the same challenge for everyone and a retry deals the
        /// same opening. It also means the harness measurement of a level means something about
        /// *that* level rather than about levels in general.
        /// </summary>
        public readonly ulong Seed;

        /// <summary>
        /// Blocks already on the board when the level opens. Empty for the ladder; the daily
        /// challenge opens half built.
        /// </summary>
        public readonly ulong StartOccupied;

        /// <summary>Colours of <see cref="StartOccupied"/>, one per cell. Null when the board opens empty.</summary>
        public readonly byte[] StartColours;

        public LevelDef(int number, int lineTarget, int moveBudget, ulong seed,
                        ulong startOccupied = 0UL, byte[] startColours = null)
        {
            Number = number;
            LineTarget = lineTarget;
            MoveBudget = moveBudget;
            Seed = seed;
            StartOccupied = startOccupied;
            StartColours = startColours;
        }

        public LevelObjective ToObjective() => new LevelObjective(LineTarget, MoveBudget);

        /// <summary>Moves left over at the finish that still earn three stars.</summary>
        public int ThreeStarSpare => Math.Max(2, MoveBudget / 5);

        /// <summary>Moves left over at the finish that still earn two stars.</summary>
        public int TwoStarSpare => Math.Max(1, MoveBudget / 12);

        public int StarsFor(int movesRemaining)
        {
            if (movesRemaining >= ThreeStarSpare) return 3;
            if (movesRemaining >= TwoStarSpare) return 2;
            return 1;
        }
    }

    /// <summary>
    /// The level ladder.
    ///
    /// Generated from a curve rather than hand-authored, because a curve can be swept and verified.
    /// `dotnet run -- levels` plays all 60 levels 120 times each with the heuristic autoplayer and
    /// reports the beat rate, so a level that is accidentally impossible shows up as a number rather
    /// than as a one-star review. The first version of this curve had 20 unbeatable levels.
    ///
    /// Shipped curve, measured:
    ///
    ///   level   lines   moves   beat rate   avg stars
    ///      1       4      22       100 %       3.00
    ///     20      11      42       100 %       2.06
    ///     40      18      57        97 %       1.93
    ///     50      22      63        95 %       1.43
    ///     60      25      66        73 %       1.16
    ///   mean beat rate across the ladder: 88.7 %
    ///
    /// The intent is that a competent player always eventually wins but three stars gets steadily
    /// harder — progression through the star count rather than through a wall. Remember the
    /// autoplayer is a strong player, so real beat rates will be lower throughout.
    /// </summary>
    public static class Levels
    {
        public const int Count = 60;

        private static readonly LevelDef[] All = Build();

        public static LevelDef Get(int number)
        {
            if (number < 1 || number > Count)
                throw new ArgumentOutOfRangeException(nameof(number), number, "No such level.");
            return All[number - 1];
        }

        private static LevelDef[] Build()
        {
            var levels = new LevelDef[Count];

            for (int i = 0; i < Count; i++)
            {
                float t = Count == 1 ? 0f : i / (float)(Count - 1);

                // Line target grows steadily: a short first level, a long last one.
                int lineTarget = LineTargetFor(i);

                // Moves per line tightens as the ladder goes on. Early levels are generous enough
                // that a new player finishes without thinking about the budget at all.
                //
                // StartupMoves is not padding: a level opens on an empty board, so the first several
                // pieces cannot possibly complete a line no matter how well they are played. Costing
                // levels purely per-line ignored that and made the whole back third unbeatable — the
                // ladder harness caught it, at 0% beat rate on 20 levels.
                double movesPerLine = MovesPerLine - (MovesPerLine - MovesPerLineFinal) * t;
                int moveBudget = (int)Math.Ceiling(StartupMoves + lineTarget * movesPerLine);

                // Deterministic, well-spread seed. Not sequential, so neighbouring levels do not
                // open with near-identical trays.
                ulong seed = unchecked((ulong)(i + 1) * 0x9E3779B97F4A7C15UL) ^ 0x5A17E1UL;

                levels[i] = new LevelDef(i + 1, lineTarget, moveBudget, seed);
            }

            return levels;
        }

        private static int LineTargetFor(int index) => FirstTarget + (int)Math.Round(index * TargetGrowth);

        // --- curve parameters, tuned from the harness -------------------------------------

        /// <summary>Lines to clear on level 1.</summary>
        private const int FirstTarget = 4;

        /// <summary>Extra lines demanded per level.</summary>
        private const double TargetGrowth = 0.36;

        /// <summary>
        /// Moves allowed before the per-line budget starts counting. The board opens empty, so
        /// roughly this many pieces go down before any line can be completed at all.
        /// </summary>
        private const double StartupMoves = 8.0;

        /// <summary>Move budget per required line on level 1.</summary>
        private const double MovesPerLine = 3.4;

        /// <summary>Move budget per required line on the final level.</summary>
        private const double MovesPerLineFinal = 2.3;
    }
}
