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
        /// same opening.
        /// </summary>
        public readonly ulong Seed;

        /// <summary>Blocks already on the board when the level opens. Empty for the first sixty.</summary>
        public readonly ulong StartOccupied;

        /// <summary>Colours of <see cref="StartOccupied"/>, one per cell. Null when the board opens empty.</summary>
        public readonly byte[] StartColours;

        /// <summary>Special blocks in the opening board, one <see cref="Special"/> per cell. Null for none.</summary>
        public readonly byte[] StartSpecials;

        public LevelDef(int number, int lineTarget, int moveBudget, ulong seed,
                        ulong startOccupied = 0UL, byte[] startColours = null, byte[] startSpecials = null)
        {
            Number = number;
            LineTarget = lineTarget;
            MoveBudget = moveBudget;
            Seed = seed;
            StartOccupied = startOccupied;
            StartColours = startColours;
            StartSpecials = startSpecials;
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

        /// <summary>True if the opening board holds at least one block of this kind.</summary>
        public bool Has(Special kind)
        {
            if (StartSpecials == null) return false;
            for (int i = 0; i < StartSpecials.Length; i++)
                if (StartSpecials[i] == (byte)kind && (StartOccupied & (1UL << i)) != 0UL) return true;
            return false;
        }
    }

    /// <summary>
    /// The level ladder: sixty open-board levels, then two hundred puzzle levels (see <see cref="Puzzles"/>)
    /// that open half built and bring in stones, gifts and bombs.
    ///
    /// The first sixty are generated from a curve rather than hand-authored, because a curve can be
    /// swept and verified. `dotnet run -- levels` plays every level with the heuristic autoplayer
    /// and reports the beat rate, so a level that is accidentally impossible shows up as a number
    /// rather than as a one-star review. The first version of this curve had 20 unbeatable levels.
    ///
    /// Shipped curve for the first sixty, measured:
    ///
    ///   level   lines   moves   beat rate   avg stars
    ///      1       4      22       100 %       3.00
    ///     20      11      42       100 %       2.06
    ///     40      18      57        97 %       1.93
    ///     50      22      63        95 %       1.43
    ///     60      25      66        73 %       1.16
    /// </summary>
    public static class Levels
    {
        /// <summary>Open-board levels, from the curve below.</summary>
        public const int LadderCount = 60;

        /// <summary>Every level in the game.</summary>
        public const int Count = LadderCount + Puzzles.Count;

        private static readonly LevelDef[] Ladder = Build();

        public static LevelDef Get(int number)
        {
            if (number < 1 || number > Count)
                throw new ArgumentOutOfRangeException(nameof(number), number, "No such level.");
            return number <= LadderCount ? Ladder[number - 1] : Puzzles.Get(number);
        }

        private static LevelDef[] Build()
        {
            var levels = new LevelDef[LadderCount];

            for (int i = 0; i < LadderCount; i++)
            {
                float t = i / (float)(LadderCount - 1);

                int lineTarget = FirstTarget + (int)Math.Round(i * TargetGrowth);

                // StartupMoves is not padding: a level opens on an empty board, so the first several
                // pieces cannot possibly complete a line. Costing levels purely per-line made the whole
                // back third unbeatable — the ladder harness caught it, at 0% beat rate on 20 levels.
                double movesPerLine = MovesPerLine - (MovesPerLine - MovesPerLineFinal) * t;
                int moveBudget = (int)Math.Ceiling(StartupMoves + lineTarget * movesPerLine);

                ulong seed = unchecked((ulong)(i + 1) * 0x9E3779B97F4A7C15UL) ^ 0x5A17E1UL;

                levels[i] = new LevelDef(i + 1, lineTarget, moveBudget, seed);
            }

            return levels;
        }

        // --- curve parameters for the first sixty, tuned from the harness --------------------

        private const int FirstTarget = 4;
        private const double TargetGrowth = 0.36;
        private const double StartupMoves = 8.0;
        private const double MovesPerLine = 3.4;
        private const double MovesPerLineFinal = 2.3;
    }
}
