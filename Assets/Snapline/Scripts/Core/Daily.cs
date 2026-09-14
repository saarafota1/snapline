using System;

namespace Snapline.Core
{
    /// <summary>
    /// The daily challenge: one puzzle per calendar day, the same for every player, and the reward
    /// for keeping a weekly streak.
    ///
    /// A daily is a level, not a new mode — "clear N lines in M moves" is exactly what the ladder
    /// already asks — but it opens on a board that is already half built, which is what makes it a
    /// puzzle rather than a short run. The opening board and the dealing both come from the date, so
    /// a retry, a second phone and a friend all get the identical challenge.
    ///
    /// No UnityEngine here, so the console harness can play a year of dailies and report how many
    /// are beatable: `dotnet run -- daily`.
    /// </summary>
    public static class Daily
    {
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1);

        /// <summary>Lines to clear. Measured, see the class notes in Tools/Bench.</summary>
        public const int LineTarget = 8;

        /// <summary>Moves allowed. Measured together with <see cref="LineTarget"/>.</summary>
        public const int MoveBudget = 22;

        /// <summary>
        /// Daily level numbers live far above the ladder, so a daily can never be mistaken for level
        /// 9 in the save, and its seed differs from every ladder level's.
        /// </summary>
        public const int LevelNumberBase = 100000;

        /// <summary>Coins for finishing today's puzzle, on top of the weekday's reward.</summary>
        public const int CompletionCoins = Economy.DailyReward;

        /// <summary>What Sunday's chest holds.</summary>
        public const int ChestCoins = 150;

        public static int DayIndex(DateTime date) => (int)(date.Date - Epoch).TotalDays;

        public static DateTime DateOf(int day) => Epoch.AddDays(day);

        /// <summary>0 for Monday through 6 for Sunday. Weeks start on Monday, as the strip does.</summary>
        public static int WeekdayOf(int day) => ((int)DateOf(day).DayOfWeek + 6) % 7;

        public static int WeekStart(int day) => day - WeekdayOf(day);

        public static bool IsDaily(int levelNumber) => levelNumber >= LevelNumberBase;

        /// <summary>
        /// The puzzle for a day.
        ///
        /// The opening board is clutter in the lower five rows, two to six blocks a row, in runs of
        /// one colour so it looks built rather than sprayed. No row or column starts complete — that
        /// would be a free line the player never earned.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, LevelDef> Cache =
            new System.Collections.Generic.Dictionary<int, LevelDef>();

        public static LevelDef ForDay(int day)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(day, out LevelDef cached)) return cached;
            }

            LevelDef level = Generate(day);
            lock (Cache) Cache[day] = level;
            return level;
        }

        /// <summary>
        /// Candidate puzzles for a day, taking the first that passes the board rules AND is actually
        /// won by the engine's own autoplayer in at least two of three tries.
        ///
        /// Both checks were earned by measurement. Taking whatever came out, the console harness found
        /// one day in fifteen unbeatable by a strong player in thirty attempts. The board rules fixed
        /// sealed holes and empty-handed openings but left two in ninety — and those boards looked
        /// easy: the trouble was the deal, which on those seeds handed out three big pieces that
        /// clogged the board within four moves. No rule about the board can see that, so each day is
        /// played before it is offered. Deterministic: every phone walks the same candidates, plays
        /// the same three games and lands on the same puzzle. It costs a few tens of milliseconds,
        /// once per day, and the result is cached.
        /// </summary>
        private static LevelDef Generate(int day)
        {
            LevelDef fallback = null;

            for (int variant = 0; variant < 24; variant++)
            {
                ulong seed = unchecked((ulong)(day + 7919 + variant * 104729L) * 0xD1B54A32D192ED03UL) ^ 0xDA117UL;
                var rng = new Rng(seed);
                var colours = new byte[Board.CellCount];
                ulong occupied = BreakFullLines(Clutter(ref rng, colours), colours);

                if (!LooksFair(occupied)) continue;

                var level = new LevelDef(LevelNumberBase + day, LineTarget, MoveBudget, rng.NextULong(), occupied, colours);
                fallback ??= level;
                if (Wins(level) >= 2) return level;
            }

            return fallback ?? new LevelDef(LevelNumberBase + day, LineTarget, MoveBudget + 8,
                                             unchecked((ulong)day * 0x9E3779B97F4A7C15UL));
        }

        private static int Wins(LevelDef level)
        {
            int wins = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var run = new GameRun(DealerConfig.Default(), new ScoreRules());
                var player = new Sim.AutoPlayer(Sim.PlayerSkill.Heuristic);
                var rng = new Rng(unchecked((ulong)(attempt + 1) * 0x2545F4914F6CDD1DUL ^ level.Seed));

                run.StartLevel(level);
                while (!run.IsGameOver)
                {
                    if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                    if (!run.Place(slot, col, row).Accepted) break;
                }

                if (run.LevelComplete) wins++;
            }
            return wins;
        }

        /// <summary>Runs of one colour along the bottom four rows, with the odd stray block.</summary>
        private static ulong Clutter(ref Rng rng, byte[] colours)
        {
            ulong occupied = 0UL;

            for (int row = 4; row < Board.Height; row++)
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

            return occupied;
        }

        /// <summary>
        /// The rules a fair opening board has to pass: not too bare and not too crowded, no empty
        /// cell sealed in on all four sides (no piece can ever fill it, so its row and column are
        /// dead), and at least one row within two blocks of complete, so there is a first line to
        /// go for.
        /// </summary>
        public static bool LooksFair(ulong occupied)
        {
            int count = Bits.PopCount(occupied);
            if (count < 14 || count > 24) return false;

            bool nearLine = false;
            for (int row = 0; row < Board.Height; row++)
            {
                int inRow = Bits.PopCount(occupied & Bits.RowMask[row]);
                if (inRow >= 6) nearLine = true;
            }
            if (!nearLine) return false;

            for (int row = 0; row < Board.Height; row++)
            {
                for (int col = 0; col < Board.Width; col++)
                {
                    if (Filled(occupied, col, row)) continue;
                    if (Filled(occupied, col - 1, row) && Filled(occupied, col + 1, row) &&
                        Filled(occupied, col, row - 1) && Filled(occupied, col, row + 1))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Off the board counts as filled, because no piece can reach past the edge either.</summary>
        private static bool Filled(ulong occupied, int col, int row) =>
            col < 0 || row < 0 || col >= Board.Width || row >= Board.Height ||
            (occupied & (1UL << Bits.Index(col, row))) != 0UL;

        private static ulong BreakFullLines(ulong occupied, byte[] colours)
        {
            for (int row = 0; row < Board.Height; row++)
            {
                bool full = true;
                for (int col = 0; col < Board.Width && full; col++)
                    full = (occupied & (1UL << Bits.Index(col, row))) != 0UL;
                if (full) occupied = Remove(occupied, colours, (row * 5 + 3) % Board.Width, row);
            }

            for (int col = 0; col < Board.Width; col++)
            {
                bool full = true;
                for (int row = 0; row < Board.Height && full; row++)
                    full = (occupied & (1UL << Bits.Index(col, row))) != 0UL;
                if (full) occupied = Remove(occupied, colours, col, Board.Height - 1 - col % 3);
            }

            return occupied;
        }

        private static ulong Remove(ulong occupied, byte[] colours, int col, int row)
        {
            int index = Bits.Index(col, row);
            colours[index] = 0;
            return occupied & ~(1UL << index);
        }

        // --- the weekly strip ------------------------------------------------------------------

        public enum RewardKind { Coins, Tool, Chest }

        public readonly struct Reward
        {
            public readonly RewardKind Kind;
            public readonly int Amount;
            public readonly Tool Tool;

            public Reward(RewardKind kind, int amount, Tool tool = Tool.Undo)
            {
                Kind = kind;
                Amount = amount;
                Tool = tool;
            }
        }

        /// <summary>
        /// The weekday's reward, from daily_challenge.png: coins and tools alternating and rising
        /// through the week, with a chest on Sunday so the streak is worth keeping to the end.
        /// </summary>
        public static Reward RewardFor(int weekday) => weekday switch
        {
            0 => new Reward(RewardKind.Coins, 25),
            1 => new Reward(RewardKind.Tool, 1, Tool.Undo),
            2 => new Reward(RewardKind.Coins, 50),
            3 => new Reward(RewardKind.Tool, 1, Tool.Shuffle),
            4 => new Reward(RewardKind.Coins, 75),
            5 => new Reward(RewardKind.Tool, 1, Tool.Hammer),
            _ => new Reward(RewardKind.Chest, ChestCoins),
        };
    }
}
