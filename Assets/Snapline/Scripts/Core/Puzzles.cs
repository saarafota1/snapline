using System;
using System.Collections.Generic;

namespace Snapline.Core
{
    /// <summary>
    /// Levels 4 to 260: puzzle levels that open on a part-built board, which is the game this game is
    /// actually about - a board with something to solve on the first move, rather than an empty grid
    /// and a line quota. They used to start at 61, and the owner found the game more fun from there.
    ///
    /// Levels 1 to 3 stay open boards (see <see cref="Levels"/>): the first thing to learn is that a
    /// full row disappears, and a cluttered board is a poor place to learn it.
    ///
    /// The clutter grows with the ladder - two rows at level 4, six by 260 - as does the line target,
    /// so an early puzzle is a tidy little problem and a late one is a siege. Which levels hold
    /// special blocks is decided by <see cref="Specials"/>.
    ///
    /// Each level is generated from its number, a variant and a move adjustment. The variant and the
    /// adjustment come from <see cref="LevelTable"/>, which the console harness writes by actually
    /// playing candidates (`dotnet run -- levels-gen`) and keeping, per level, the one whose beat rate
    /// sits closest to a smooth difficulty curve. So the table is not a design guess: it is the
    /// record of which boards were played and found fair.
    /// </summary>
    public static class Puzzles
    {
        public const int Count = 257;
        public const int First = Levels.LadderCount + 1;
        public const int Last = First + Count - 1;

        private static readonly Dictionary<int, LevelDef> Cache = new Dictionary<int, LevelDef>();

        public static LevelDef Get(int number)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(number, out LevelDef cached)) return cached;
            }

            LevelDef level;
            if (LevelTable.TryLookup(number, out int variant, out int adjust))
            {
                level = Build(number, variant, adjust);
            }
            else
            {
                // No measured entry: take the first variant that passes the board rules.
                level = null;
                for (int v = 0; v < 32 && level == null; v++)
                {
                    LevelDef candidate = Build(number, v, 0);
                    if (IsFair(candidate)) level = candidate;
                }
                level ??= Build(number, 0, 4);
            }

            lock (Cache) Cache[number] = level;
            return level;
        }

        /// <summary>0 at the first puzzle level, 1 at level 260.</summary>
        private static double Progress(int number) => Math.Max(0.0, Math.Min(1.0, (number - First) / (double)(Count - 1)));

        /// <summary>3 lines at level 4, 14 by level 260.</summary>
        public static int LineTarget(int number) => 3 + (int)Math.Round(Progress(number) * 11.0);

        /// <summary>
        /// The starting move budget the harness tunes from. The first guess, 2.9 moves a line plus 4,
        /// was so generous that the strong autoplayer beat 97% of levels even at the tightest setting
        /// tried; the half-built board is a head start the ladder's curve does not have.
        /// </summary>
        public static int BaseMoves(int number)
        {
            double t = Progress(number);
            double perLine = 3.0 - 1.0 * t;
            return (int)Math.Ceiling(LineTarget(number) * perLine + 2.0);
        }

        /// <summary>
        /// Rows of clutter, from the bottom: two at level 4, six by the end. Two rows is enough to
        /// give a level 4 something to solve without burying a player who has cleared three lines in
        /// their life.
        /// </summary>
        private static int ClutterRows(int number) => 2 + (int)Math.Round(Progress(number) * 4.0);

        /// <summary>
        /// The opening board has to be worth solving: enough blocks to matter, not so many that it is
        /// a wall, a row already close to clearing, and no hole a piece could never reach. The bounds
        /// scale with the clutter, since a fixed floor of 14 blocks is most of a two-row opening.
        /// </summary>
        public static bool IsFair(LevelDef level)
        {
            int rows = ClutterRows(level.Number);
            return Daily.LooksFair(level.StartOccupied, Math.Max(6, 3 * rows), 6 * rows);
        }

        public static LevelDef Build(int number, int variant, int adjust)
        {
            ulong seed = unchecked(((ulong)number * 0x9E3779B97F4A7C15UL) ^ ((ulong)(variant + 1) * 0xC2B2AE3D27D4EB4FUL) ^ 0x70A2UL);
            var rng = new Rng(seed);
            var colours = new byte[Board.CellCount];
            var specials = new byte[Board.CellCount];

            ulong occupied = 0UL;
            int rows = ClutterRows(number);

            for (int row = Board.Height - rows; row < Board.Height; row++)
            {
                int blocks = 3 + rng.NextInt(4);
                int start = rng.NextInt(Board.Width - blocks + 1);
                byte colour = (byte)rng.NextInt(6);

                for (int k = 0; k < blocks; k++)
                {
                    int index = Bits.Index(start + k, row);
                    occupied |= 1UL << index;
                    colours[index] = colour;
                    if (rng.NextInt(4) == 0) colour = (byte)rng.NextInt(6);
                }

                if (rng.NextInt(3) == 0)
                {
                    int stray = Bits.Index(rng.NextInt(Board.Width), row);
                    occupied |= 1UL << stray;
                    colours[stray] = (byte)rng.NextInt(6);
                }
            }

            occupied = Daily.BreakFullLines(occupied, colours);

            foreach (Special kind in Specials.Kinds)
                Scatter(ref rng, occupied, specials, kind, Specials.Count(number, kind));

            int lines = LineTarget(number);
            int moves = Math.Max(lines + 4, BaseMoves(number) + adjust);
            return new LevelDef(number, lines, moves, rng.NextULong(), occupied, colours, specials);
        }

        /// <summary>Turns some ordinary opening blocks into special ones.</summary>
        private static void Scatter(ref Rng rng, ulong occupied, byte[] specials, Special kind, int count)
        {
            if (count <= 0) return;

            var candidates = new List<int>(Board.CellCount);
            ulong m = occupied;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;
                if (specials[idx] == 0) candidates.Add(idx);
            }

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = rng.NextInt(candidates.Count);
                specials[candidates[pick]] = (byte)kind;
                candidates.RemoveAt(pick);
            }
        }
    }
}
