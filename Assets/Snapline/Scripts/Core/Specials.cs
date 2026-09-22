using System;

namespace Snapline.Core
{
    /// <summary>
    /// Which levels hold which special blocks, for the whole ladder.
    ///
    /// They used to belong to the back half of the game: stones from level 81, gifts from 111, bombs
    /// from 141, and every level past the threshold had them. A player met nothing new for the first
    /// eighty levels, which is a long time to be shown the same puzzle.
    ///
    /// Now they start at level 5 and appear on a level only some of the time, so a special block is
    /// an event rather than furniture. Which levels get them is random but fixed: it is derived from
    /// the level number, so a level is the same for everyone and a retry brings back the same board.
    ///
    /// Each kind debuts on a level of its own, in teaching order - stone, gift, bomb - because the
    /// first level to hold one raises the NEW BLOCK! card that explains it, and meeting two at once
    /// would explain neither. After its debut, a kind shows up by chance, more often deeper in.
    /// </summary>
    public static class Specials
    {
        /// <summary>The level that first holds a stone, and the earliest special block in the game.</summary>
        public const int StoneDebut = 5;

        /// <summary>The level that first holds a gift.</summary>
        public const int GiftDebut = 8;

        /// <summary>The level that first holds a bomb.</summary>
        public const int BombDebut = 12;

        /// <summary>The three kinds that appear in levels, in the order they are introduced.</summary>
        public static readonly Special[] Kinds = { Special.Stone, Special.Gift, Special.Bomb };

        public static int Debut(Special kind) => kind switch
        {
            Special.Stone => StoneDebut,
            Special.Gift => GiftDebut,
            Special.Bomb => BombDebut,
            _ => int.MaxValue,
        };

        /// <summary>
        /// How many blocks of this kind level <paramref name="number"/> opens with. Zero on a level
        /// that does not hold this kind, which is most of them early on.
        ///
        /// The debut level always holds exactly one: a player meeting a stone for the first time
        /// should meet one stone, with the card that explains it, not four at once.
        /// </summary>
        public static int Count(int number, Special kind)
        {
            int debut = Debut(kind);
            if (number < debut) return 0;
            if (number == debut) return 1;

            double t = Progress(number);
            var rng = new Rng(Seed(number, kind));

            // A fifth of levels per kind early, two thirds by the end: about half the early ladder
            // holds something and nearly all of the late game does. The first pass ran 32% to 82%,
            // which put a special block on 90% of the game - furniture again, and a lot to meet while
            // still learning the plain game.
            if (rng.NextDouble() >= 0.20 + 0.45 * t) return 0;

            int most = kind switch
            {
                Special.Stone => 1 + (int)Math.Round(t * 7.0),
                Special.Gift => 1 + (int)Math.Round(t * 1.0),
                _ => 1 + (int)Math.Round(t * 2.0),
            };

            return 1 + rng.NextInt(Math.Max(1, most));
        }

        /// <summary>True if this is the first level in the ladder to hold that kind.</summary>
        public static bool IsDebut(int number, Special kind) => number == Debut(kind);

        /// <summary>
        /// Puts this level's special blocks onto an open board - the first sixty levels, which have
        /// no clutter for <see cref="Puzzles.Scatter"/> to convert.
        ///
        /// They go in the lower rows, spread across the width, each on its own cell: near enough to
        /// the action to matter on a board that fills from the bottom, and far too few to complete a
        /// line by themselves. Returns the occupied mask, which is empty when the level holds none.
        /// </summary>
        public static ulong Place(int number, ulong seed, byte[] colours, byte[] specials)
        {
            ulong occupied = 0UL;
            var rng = new Rng(seed ^ 0xB5297A4DUL);

            foreach (Special kind in Kinds)
            {
                int count = Count(number, kind);
                for (int i = 0; i < count; i++)
                {
                    // A handful of tries is plenty on a board this empty; give up rather than loop.
                    for (int attempt = 0; attempt < 16; attempt++)
                    {
                        int col = rng.NextInt(Board.Width);
                        int row = Board.Height - 1 - rng.NextInt(4);
                        int index = Bits.Index(col, row);
                        if ((occupied & (1UL << index)) != 0UL) continue;

                        occupied |= 1UL << index;
                        colours[index] = (byte)rng.NextInt(6);
                        specials[index] = (byte)kind;
                        break;
                    }
                }
            }

            return occupied;
        }

        /// <summary>
        /// The first level from <paramref name="from"/> upwards holding every one of these kinds.
        /// The screenshot harnesses use it to find a level worth photographing, now that no fixed
        /// level number is guaranteed to hold anything.
        /// </summary>
        public static int FirstLevelHolding(int from, params Special[] kinds)
        {
            for (int n = Math.Max(1, from); n <= Levels.Count; n++)
            {
                bool all = true;
                foreach (Special kind in kinds)
                    if (Count(n, kind) <= 0) { all = false; break; }

                if (all) return n;
            }

            return from;
        }

        /// <summary>0 at a kind's debut, 1 at the last level in the game.</summary>
        private static double Progress(int number)
        {
            double span = Math.Max(1, Levels.Count - StoneDebut);
            return Math.Max(0.0, Math.Min(1.0, (number - StoneDebut) / span));
        }

        /// <summary>Fixed per level and kind, so the same levels always hold the same blocks.</summary>
        private static ulong Seed(int number, Special kind) =>
            unchecked((ulong)number * 0x9E3779B97F4A7C15UL ^ ((ulong)kind + 1UL) * 0xD1B54A32D192ED03UL ^ 0x5EC1A1UL);
    }
}
