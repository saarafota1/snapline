using System;
using System.Collections.Generic;
using Snapline.Core;
using Snapline.Core.Sim;

namespace Snapline.Bench
{
    /// <summary>
    /// Console harness for the rules engine.
    ///
    ///   dotnet run -- check     correctness assertions over the engine
    ///   dotnet run -- measure   compare dealer policies at three skill levels
    ///   dotnet run -- sweep     sweep the congestion weighting
    ///   dotnet run             all of the above
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

            if (mode == "check" || mode == "all") SelfCheck();
            if (mode == "measure" || mode == "all") Measure();
            if (mode == "sweep" || mode == "all") Sweep();
            if (mode == "validate" || mode == "all") Validate();
            if (mode == "combo" || mode == "all") Combos();
            if (mode == "levels" || mode == "all") LevelLadder();
            if (mode == "daily" || mode == "all") DailyChallenges();
            if (mode == "economy" || mode == "all") EconomyReport();
            if (mode == "levels-gen") GenerateLevelTable(args.Length > 1 ? args[1] : "Assets/Snapline/Scripts/Core/LevelTable.cs");
            if (mode == "specials" || mode == "all") SpecialsMap();

            if (_failures > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"FAILED: {_failures} assertion(s)");
                return 1;
            }

            return 0;
        }

        // --- correctness ----------------------------------------------------------------

        private static void Assert(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Console.WriteLine($"  FAIL  {what}");
        }

        private static void SelfCheck()
        {
            Console.WriteLine("=== engine self-check ===");

            // Shape catalogue integrity.
            for (int i = 0; i < Shapes.Count; i++)
            {
                ShapeDef s = Shapes.All[i];
                Assert(s.Id == i, $"shape {s.Name} id matches index");
                Assert(s.CellCount == Bits.PopCount(s.BaseMask), $"shape {s.Name} cell count matches mask");
                Assert(s.Width <= Board.Width && s.Height <= Board.Height, $"shape {s.Name} fits the board");
                Assert(s.PlacementMasks.Length == (Board.Width - s.Width + 1) * (Board.Height - s.Height + 1),
                       $"shape {s.Name} placement count");

                // The shifted-mask identity is only valid if no placement wraps a row. Verify by
                // checking every placement mask still has the same popcount as the base.
                for (int m = 0; m < s.PlacementMasks.Length; m++)
                    Assert(Bits.PopCount(s.PlacementMasks[m]) == s.CellCount,
                           $"shape {s.Name} placement {m} does not wrap");
            }
            Console.WriteLine($"  {Shapes.Count} shapes, max {Shapes.MaxCellCount} cells");

            // Row clear.
            var b = new Board();
            ShapeDef bar4 = Shapes.Get(5);  // I4H
            b.Place(bar4, 0, 0, 1);
            PlaceResult r = b.Place(bar4, 4, 0, 2);
            Assert(r.RowsCleared == 1, "filling a row clears exactly one row");
            Assert(r.ColsCleared == 0, "filling a row clears no columns");
            Assert(b.IsEmpty, "board empty after the only row clears");

            // Column clear.
            b.Clear();
            ShapeDef bar4v = Shapes.Get(6);  // I4V
            b.Place(bar4v, 3, 0, 1);
            r = b.Place(bar4v, 3, 4, 2);
            Assert(r.ColsCleared == 1, "filling a column clears exactly one column");
            Assert(b.IsEmpty, "board empty after the only column clears");

            // Simultaneous row + column: the shared cell must not be double counted.
            b.Clear();
            ShapeDef dot = Shapes.Get(0);
            for (int c = 0; c < 8; c++) if (c != 7) b.Place(dot, c, 7, 1);
            for (int row = 0; row < 7; row++) b.Place(dot, 7, row, 1);
            Assert(b.FilledCells == 14, "L of 14 cells before the joining piece");
            r = b.Place(dot, 7, 7, 2);
            Assert(r.RowsCleared == 1 && r.ColsCleared == 1, "one piece clears a row and a column together");
            Assert(r.LinesCleared == 2, "two lines counted");
            Assert(Bits.PopCount(r.ClearedMask) == 15, "15 distinct cells cleared, shared corner counted once");
            Assert(b.IsEmpty, "board empty after the cross clear");

            // Simulate() must agree with Place().
            b.Clear();
            b.Place(bar4, 0, 3, 1);
            ulong viaSimulate = Board.Simulate(b.Occupied, bar4.BaseMask << Bits.Index(4, 3));
            Board copy = b.Clone();
            copy.Place(bar4, 4, 3, 2);
            Assert(viaSimulate == copy.Occupied, "Simulate matches Place");

            // Enclosed-cell counting. Built as a raw bitboard rather than by placing pieces,
            // because placing them would complete lines and clear them before the state is reached.
            ulong almostFull = ulong.MaxValue & ~(1UL << Bits.Index(3, 4));
            Assert(AutoPlayer.CountEnclosedEmptyCells(almostFull) == 1, "single hole counts as enclosed");
            Assert(AutoPlayer.CountEnclosedEmptyCells(0UL) == 0, "empty board has no enclosed cells");

            // Edges are walls, not open space: a hole in the corner is still enclosed.
            ulong cornerHole = ulong.MaxValue & ~1UL;
            Assert(AutoPlayer.CountEnclosedEmptyCells(cornerHole) == 1, "corner hole counts as enclosed");

            // Scoring: combo multiplier applies from the second clearing move, not the first.
            var state = new ScoreState();
            var rules = new ScoreRules();
            var oneLine = new PlaceResult { Placed = true, CellsPlaced = 4, RowsCleared = 1 };
            ScoreDelta d1 = Scoring.Apply(state, rules, oneLine, false);
            Assert(Math.Abs(d1.ComboMultiplier - 1.0) < 1e-9, "first clear scores at 1.0x");
            ScoreDelta d2 = Scoring.Apply(state, rules, oneLine, false);
            Assert(Math.Abs(d2.ComboMultiplier - (1.0 + rules.ComboStep)) < 1e-9, "second consecutive clear steps up");
            // Default rules allow one dry move of grace, so it takes two to break a streak.
            var dry = new PlaceResult { Placed = true, CellsPlaced = 3 };
            Scoring.Apply(state, rules, dry, false);
            Assert(state.ComboCount == 2, "one dry move is forgiven at the default grace of 1");
            Scoring.Apply(state, rules, dry, false);
            Assert(state.ComboCount == 0, "a second dry move breaks the combo");

            // And with grace turned off, the strict rule still holds.
            var strict = new ScoreRules { ComboGraceMoves = 0 };
            var strictState = new ScoreState();
            Scoring.Apply(strictState, strict, oneLine, false);
            Scoring.Apply(strictState, strict, dry, false);
            Assert(strictState.ComboCount == 0, "at grace 0 a single dry move breaks the combo");

            // Save round-trip, including a mid-tray state.
            var run = new GameRun();
            run.StartNew(4242UL);
            for (int i = 0; i < 25 && !run.IsGameOver; i++)
            {
                var rng = new Rng(7UL);
                var ap = new AutoPlayer(PlayerSkill.Heuristic);
                if (!ap.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                run.Place(slot, col, row);
            }

            RunSnapshot snap = run.Snapshot();
            string encoded = SaveCodec.Encode(snap);
            RunSnapshot decoded = SaveCodec.Decode(encoded);
            Assert(decoded != null, "save decodes");
            Assert(decoded.Occupied == snap.Occupied, "save round-trips the board");
            Assert(decoded.Score == snap.Score, "save round-trips the score");
            Assert(decoded.RngState == snap.RngState, "save round-trips the RNG state");

            Assert(SaveCodec.Decode(encoded.Substring(0, encoded.Length - 5)) == null, "truncated save is rejected");
            Assert(SaveCodec.Decode("garbage") == null, "garbage save is rejected");
            Assert(SaveCodec.Decode(null) == null, "null save is rejected");

            // A restored run must continue identically to one that was never interrupted.
            var restored = new GameRun();
            restored.Restore(decoded);
            Assert(restored.Board.Occupied == run.Board.Occupied, "restored board matches");
            Assert(restored.Score.Score == run.Score.Score, "restored score matches");

            string continuedA = PlayOutToString(run, 30);
            string continuedB = PlayOutToString(restored, 30);
            Assert(continuedA == continuedB, "a restored run continues identically to an uninterrupted one");

            // Determinism: same seed, same result.
            RunOutcome o1 = Simulator.PlayOne(DealerConfig.Default(), new ScoreRules(), PlayerSkill.Heuristic, 99UL);
            RunOutcome o2 = Simulator.PlayOne(DealerConfig.Default(), new ScoreRules(), PlayerSkill.Heuristic, 99UL);
            Assert(o1.Score == o2.Score && o1.PiecesPlaced == o2.PiecesPlaced, "same seed gives the same run");

            // The survivability guarantee must actually hold: every tray it deals is placeable in full.
            int checkedTrays = 0, violations = 0;
            var cfg = DealerConfig.Default();
            for (ulong seed = 0; seed < 40; seed++)
            {
                var g = new GameRun(cfg, new ScoreRules());
                var rng = new Rng(seed);
                g.StartNew(seed);
                var ap = new AutoPlayer(PlayerSkill.Heuristic);

                int placed = 0;
                while (!g.IsGameOver && placed < 400)
                {
                    if (g.TrayFullyDealt())
                    {
                        var ids = new List<int>();
                        for (int t = 0; t < g.Tray.Length; t++)
                            if (!g.Tray[t].IsEmpty) ids.Add(g.Tray[t].ShapeId);

                        if (ids.Count == g.Tray.Length)
                        {
                            checkedTrays++;
                            var probe = new Dealer(cfg);
                            if (!probe.IsTraySurvivable(g.Board.Occupied, ids.ToArray(), ids.Count))
                                violations++;
                        }
                    }

                    if (!ap.ChooseMove(g, ref rng, out int slot, out int col, out int row)) break;
                    if (!g.Place(slot, col, row).Accepted) break;
                    placed++;
                }
            }
            Console.WriteLine($"  survivable-tray audit: {checkedTrays} full trays checked, {violations} violations");
            Assert(violations == 0, "every full tray dealt under WholeTraySurvivable really is survivable");

            Console.WriteLine(_failures == 0 ? "  all checks passed" : $"  {_failures} failures");
            Console.WriteLine();
        }

        private static string PlayOutToString(GameRun run, int moves)
        {
            var rng = new Rng(555UL);
            var ap = new AutoPlayer(PlayerSkill.Heuristic);
            for (int i = 0; i < moves && !run.IsGameOver; i++)
            {
                if (!ap.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                if (!run.Place(slot, col, row).Accepted) break;
            }
            return run.Board.Occupied.ToString("X16") + ":" + run.Score.Score;
        }

        // --- measurement ----------------------------------------------------------------

        private static void Measure()
        {
            Console.WriteLine("=== dealer policy comparison ===");
            const int runs = 400;

            var policies = new (string Label, DealerConfig Config)[]
            {
                ("pure random (control)", DealerConfig.PureRandom()),
                ("congestion weighting only", new DealerConfig
                {
                    Guarantee = TrayGuarantee.None,
                }),
                ("at-least-one-fits", new DealerConfig
                {
                    Guarantee = TrayGuarantee.AtLeastOneFits,
                }),
                ("whole-tray survivable", new DealerConfig
                {
                    Guarantee = TrayGuarantee.WholeTraySurvivable,
                }),
            };

            foreach (PlayerSkill skill in new[] { PlayerSkill.Random, PlayerSkill.Greedy, PlayerSkill.Heuristic })
            {
                Console.WriteLine();
                Console.WriteLine($"### player skill: {skill}");
                foreach (var p in policies)
                {
                    SimReport report = Simulator.Measure(p.Label, p.Config, new ScoreRules(), skill, runs);
                    Console.Write(report);
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== is congestion weighting actually doing anything? ===");
            Console.WriteLine("  default config:");
            Console.Write(Simulator.MeasureSizeByCongestion(DealerConfig.Default(), PlayerSkill.Heuristic, 60));
            Console.WriteLine("  congestion disabled:");
            Console.Write(Simulator.MeasureSizeByCongestion(
                new DealerConfig { CongestionBias = 0.0, PlacementAwareness = 0.0 }, PlayerSkill.Heuristic, 60));
            Console.WriteLine();
        }

        // --- sweep ----------------------------------------------------------------------

        private static void Sweep()
        {
            Console.WriteLine("=== congestion bias sweep (whole-tray survivable, heuristic player) ===");
            const int runs = 300;

            foreach (double bias in new[] { 0.0, 1.0, 2.2, 3.5, 5.0 })
            {
                foreach (double awareness in new[] { 0.0, 0.6, 1.2 })
                {
                    var cfg = new DealerConfig
                    {
                        Guarantee = TrayGuarantee.WholeTraySurvivable,
                        CongestionBias = bias,
                        PlacementAwareness = awareness,
                    };
                    SimReport r = Simulator.Measure($"bias {bias:F1} / awareness {awareness:F1}",
                                                    cfg, new ScoreRules(), PlayerSkill.Heuristic, runs);
                    Console.WriteLine($"  bias {bias,4:F1}  awareness {awareness,4:F1}   " +
                                      $"median {r.MedianPieces,4} pieces   p05 {r.P05Pieces,4}   " +
                                      $"mean score {r.MeanScore,9:F0}   under-20 {r.ShareUnder20Pieces,6:P2}");
                }
            }
            Console.WriteLine();
        }

        // --- level ladder ----------------------------------------------------------------

        /// <summary>
        /// Play every level with the autoplayer and report how often it is beaten.
        ///
        /// This is the whole reason the ladder is generated from a curve instead of hand-authored:
        /// a level that is accidentally impossible, or trivially free, shows up here as a number
        /// rather than as a one-star review. A level nobody can beat is a bug, and it is silent.
        /// </summary>
        /// <summary>
        /// Play a season of daily challenges and report how many are beatable, for the shipped
        /// objective and for a few candidates around it.
        ///
        /// A daily opens on a board that is already half built, so the ladder's move curve does not
        /// apply to it — the clutter is both a head start on lines and a way to run out of room. The
        /// only honest way to pick a line target and a move budget is to play them.
        /// </summary>
        private static void DailyChallenges()
        {
            Console.WriteLine("=== daily challenge ===");
            const int days = 90;
            const int attempts = 30;
            int firstDay = Daily.DayIndex(new DateTime(2026, 9, 1));

            (int Lines, int Moves)[] candidates =
            {
                (Daily.LineTarget, Daily.MoveBudget), (6, 18), (8, 20), (8, 24), (10, 26),
            };

            foreach ((int lines, int moves) in candidates)
            {
                var rates = new double[days];
                int impossible = 0;
                double sum = 0, stars = 0;
                int wins = 0;

                for (int d = 0; d < days; d++)
                {
                    LevelDef daily = Daily.ForDay(firstDay + d);
                    var level = new LevelDef(daily.Number, lines, moves, daily.Seed, daily.StartOccupied, daily.StartColours);

                    int dayWins = 0;
                    for (int a = 0; a < attempts; a++)
                    {
                        var run = new GameRun(DealerConfig.Default(), new ScoreRules());
                        var player = new AutoPlayer(PlayerSkill.Heuristic);
                        var rng = new Rng(unchecked((ulong)(d * 7919 + a) * 0x2545F4914F6CDD1DUL));

                        run.StartLevel(level);
                        while (!run.IsGameOver)
                        {
                            if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                            if (!run.Place(slot, col, row).Accepted) break;
                        }

                        if (!run.LevelComplete) continue;
                        dayWins++;
                        stars += level.StarsFor(run.MovesRemaining);
                    }

                    wins += dayWins;
                    rates[d] = dayWins / (double)attempts;
                    sum += rates[d];
                    if (dayWins == 0)
                    {
                        impossible++;
                        if (impossible <= 2 && lines == Daily.LineTarget && moves == Daily.MoveBudget)
                        {
                            Console.WriteLine($"  never beaten, day {firstDay + d}:\n{new Board().Also(level)}");
                            var trace = new GameRun(DealerConfig.Default(), new ScoreRules());
                            var tracer = new AutoPlayer(PlayerSkill.Heuristic);
                            var trng = new Rng(99UL);
                            trace.StartLevel(level);
                            Console.WriteLine($"  at start: gameOver={trace.IsGameOver} failed={trace.LevelFailed} " +
                                              $"tray={trace.Tray[0].ShapeId},{trace.Tray[1].ShapeId},{trace.Tray[2].ShapeId}");
                            while (!trace.IsGameOver)
                            {
                                if (!tracer.ChooseMove(trace, ref trng, out int ts, out int tc, out int tr))
                                {
                                    Console.WriteLine("  autoplayer found no move");
                                    break;
                                }
                                if (!trace.Place(ts, tc, tr).Accepted) { Console.WriteLine("  move rejected"); break; }
                            }
                            Console.WriteLine($"  ended: moves={trace.MovesUsed} lines={trace.Score.TotalLinesCleared} " +
                                              $"failed={trace.LevelFailed}\n{trace.Board}");
                        }
                    }
                }

                Array.Sort(rates);
                string shipped = lines == Daily.LineTarget && moves == Daily.MoveBudget ? "  <- shipped" : "";
                Console.WriteLine($"  {lines,2} lines in {moves,2} moves: mean beat {sum / days,6:P0}, " +
                                  $"worst day {rates[0],5:P0}, 10th pct {rates[days / 10],5:P0}, " +
                                  $"median {rates[days / 2],5:P0}, avg stars {(wins == 0 ? 0 : stars / wins):F2}, " +
                                  $"never beaten {impossible}{shipped}");

                if (shipped.Length > 0 && impossible > 0)
                {
                    _failures++;
                    Console.WriteLine($"  FAIL  {impossible} daily challenge(s) were never beaten in {attempts} attempts");
                }
            }
        }

        /// <summary>Fraction of attempts the heuristic autoplayer beats a level in.</summary>
        private static double BeatRate(LevelDef level, int attempts, ulong salt)
        {
            int wins = 0;
            for (int a = 0; a < attempts; a++)
            {
                var run = new GameRun(DealerConfig.Default(), new ScoreRules());
                var player = new AutoPlayer(PlayerSkill.Heuristic);
                var rng = new Rng(unchecked((ulong)(a + 1) * 0x2545F4914F6CDD1DUL ^ salt));

                run.StartLevel(level);
                while (!run.IsGameOver)
                {
                    if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                    if (!run.Place(slot, col, row).Accepted) break;
                }

                if (run.LevelComplete) wins++;
            }
            return wins / (double)attempts;
        }

        /// <summary>
        /// Chooses, for every puzzle level, the candidate board and move budget whose played beat rate
        /// sits closest to a smooth curve — 90% at level 61 easing to 60% at level 260 — and writes the
        /// choices out as LevelTable.cs.
        ///
        /// Played, not estimated: the special blocks make a board's difficulty impossible to read off
        /// its block count, and a generated level nobody can beat is a bug that ships silently.
        /// </summary>
        private static void GenerateLevelTable(string outputPath)
        {
            Console.WriteLine("=== puzzle level generation ===");
            const int attempts = 10;
            const int fairVariantsTried = 4;
            // Tightest first: the first budget that reaches the curve is the closest one to it.
            int[] adjusts = { -10, -8, -6, -4, -2, 0, 2, 4, 7, 10 };

            var data = new List<sbyte>();
            double blockSum = 0;
            int blockCount = 0, impossible = 0;

            for (int n = Puzzles.First; n <= Puzzles.Last; n++)
            {
                double t = (n - Puzzles.First) / (double)(Puzzles.Count - 1);
                double target = 0.90 - 0.30 * t;

                int bestVariant = 0, bestAdjust = 10;
                double bestRate = -1, bestScore = double.MaxValue;
                int fair = 0;

                for (int v = 0; v < 60 && fair < fairVariantsTried; v++)
                {
                    if (!Puzzles.IsFair(Puzzles.Build(n, v, 0))) continue;
                    fair++;

                    foreach (int adj in adjusts)
                    {
                        double rate = BeatRate(Puzzles.Build(n, v, adj), attempts, (ulong)(n * 131 + v));
                        double score = Math.Abs(rate - target) + 0.004 * Math.Abs(adj) + (rate < 0.3 ? 1.0 : 0.0);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestVariant = v;
                            bestAdjust = adj;
                            bestRate = rate;
                        }

                        // More moves only make it easier; once at or past the curve, stop adding.
                        if (rate >= target) break;
                    }
                }

                if (bestRate <= 0) impossible++;
                data.Add((sbyte)bestVariant);
                data.Add((sbyte)bestAdjust);

                blockSum += bestRate;
                blockCount++;
                if ((n - Puzzles.First + 1) % 20 == 0)
                {
                    LevelDef shown = Puzzles.Build(n, bestVariant, bestAdjust);
                    Console.WriteLine($"  levels {n - 19,3}-{n,3}: mean beat {blockSum / blockCount,5:P0} " +
                                      $"(target {target,4:P0}); level {n}: {shown.LineTarget} lines in {shown.MoveBudget} moves, " +
                                      $"stones {Specials.Count(n, Special.Stone)} gifts {Specials.Count(n, Special.Gift)} " +
                                      $"bombs {Specials.Count(n, Special.Bomb)}");
                    blockSum = 0;
                    blockCount = 0;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("namespace Snapline.Core");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Which candidate board, and how many moves more or fewer than the base budget, each puzzle level");
            sb.AppendLine("    /// uses. WRITTEN BY `dotnet run -- levels-gen` from played measurements — do not edit by hand.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class LevelTable");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Pairs of (variant, move adjustment), one pair per level from <see cref=\"Puzzles.First\"/>.</summary>");
            sb.AppendLine("        private static readonly sbyte[] Data =");
            sb.AppendLine("        {");
            for (int i = 0; i < data.Count; i += 20)
            {
                sb.Append("            ");
                for (int j = i; j < Math.Min(i + 20, data.Count); j++) sb.Append(data[j]).Append(", ");
                sb.AppendLine();
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryLookup(int number, out int variant, out int adjust)");
            sb.AppendLine("        {");
            sb.AppendLine("            int i = (number - Puzzles.First) * 2;");
            sb.AppendLine("            if (i < 0 || i + 1 >= Data.Length)");
            sb.AppendLine("            {");
            sb.AppendLine("                variant = 0;");
            sb.AppendLine("                adjust = 0;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            variant = Data[i];");
            sb.AppendLine("            adjust = Data[i + 1];");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            System.IO.File.WriteAllText(outputPath, sb.ToString().Replace("\r\n", "\n"));
            Console.WriteLine($"  wrote {data.Count / 2} levels to {outputPath}; never beaten at the chosen setting: {impossible}");
            if (impossible > 0) _failures++;
        }

        /// <summary>
        /// What the economy pays against what it charges, for endless runs at three skill levels —
        /// the check behind the prices in Economy.cs.
        /// </summary>
        private static void EconomyReport()
        {
            Console.WriteLine("=== economy ===");
            const int runs = 150;

            foreach (PlayerSkill skill in new[] { PlayerSkill.Random, PlayerSkill.Greedy, PlayerSkill.Heuristic })
            {
                var coins = new int[runs];
                for (int i = 0; i < runs; i++)
                {
                    var run = new GameRun(DealerConfig.Default(), new ScoreRules());
                    var player = new AutoPlayer(skill);
                    var rng = new Rng(unchecked((ulong)(i + 1) * 0x9E3779B97F4A7C15UL));
                    run.StartNew((ulong)(i * 7919 + 13));

                    int moves = 0;
                    while (!run.IsGameOver && moves++ < 700)
                    {
                        if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                        if (!run.Place(slot, col, row).Accepted) break;
                    }

                    coins[i] = Economy.EndlessReward(run.Score.TotalLinesCleared, run.Score.BestCombo);
                }

                Array.Sort(coins);
                int median = coins[runs / 2];
                Console.WriteLine($"  {skill,-9} endless run pays median {median,4} (10th pct {coins[runs / 10],4}, " +
                                  $"90th {coins[runs * 9 / 10],4}); runs per undo {Economy.UndoPrice / (double)Math.Max(1, median):F1}, " +
                                  $"shuffle {Economy.ShufflePrice / (double)Math.Max(1, median):F1}, " +
                                  $"hammer {Economy.HammerPrice / (double)Math.Max(1, median):F1}");
            }

            Console.WriteLine($"  a store video pays {Economy.AdReward} ({Economy.AdRewardsPerDay} a day): " +
                              $"{Economy.HammerPrice / (double)Economy.AdReward:F1} videos per hammer; " +
                              $"a first 3-star level pays {Economy.LevelReward(0, 3)}; a daily pays {Economy.DailyReward} + the weekday reward");
        }

        /// <summary>A board holding a level's opening position, for printing.</summary>
        private static Board Also(this Board board, LevelDef level)
        {
            board.Restore(level.StartOccupied, level.StartColours, level.StartSpecials);
            return board;
        }

        /// <summary>
        /// Which levels hold which special blocks. The policy is random-but-fixed, so the only way to
        /// know what a player actually meets, and how often, is to print it.
        /// </summary>
        private static void SpecialsMap()
        {
            Console.WriteLine("=== special blocks by level ===");

            var totals = new Dictionary<Special, int>();
            var firstSeen = new Dictionary<Special, int>();
            foreach (Special kind in Specials.Kinds) { totals[kind] = 0; firstSeen[kind] = 0; }

            var line = new System.Text.StringBuilder();
            for (int n = 1; n <= Levels.Count; n++)
            {
                LevelDef level = Levels.Get(n);
                string cell = "";

                foreach (Special kind in Specials.Kinds)
                {
                    int count = Specials.Count(n, kind);
                    if (count <= 0) continue;

                    totals[kind]++;
                    if (firstSeen[kind] == 0) firstSeen[kind] = n;
                    cell += kind.ToString()[0] + count.ToString();
                    if (!level.Has(kind)) cell += "!";
                }

                if (n <= 60)
                {
                    line.Append($"{n,3}:{cell,-8}");
                    if (n % 6 == 0) { Console.WriteLine("  " + line); line.Clear(); }
                }
            }

            if (line.Length > 0) Console.WriteLine("  " + line);

            Console.WriteLine();
            foreach (Special kind in Specials.Kinds)
                Console.WriteLine($"  {kind,-6} debut {Specials.Debut(kind),4}, first built {firstSeen[kind],4}, " +
                                  $"on {totals[kind],4} of {Levels.Count} levels " +
                                  $"({totals[kind] * 100.0 / Levels.Count:F0} %)");

            int bare = 0;
            for (int n = 1; n <= Levels.Count; n++)
            {
                bool any = false;
                foreach (Special kind in Specials.Kinds) any |= Specials.Count(n, kind) > 0;
                if (!any) bare++;
            }

            Console.WriteLine($"  levels with no special block at all: {bare} of {Levels.Count}");
            Assert(firstSeen[Special.Stone] == Specials.StoneDebut, "stones debut where the policy says");
            Assert(bare > 20, "plenty of levels are still plain");
        }

        private static void LevelLadder()
        {
            Console.WriteLine("=== level ladder ===");
            const int attemptsPerLevel = 120;

            int impossible = 0, trivialTail = 0;
            double sumRate = 0;

            Console.WriteLine($"  {"lvl",4} {"lines",6} {"moves",6} {"beat",8} {"avg stars",10} {"avg spare",10}");

            for (int n = 1; n <= Levels.Count; n++)
            {
                LevelDef level = Levels.Get(n);

                int wins = 0, stars = 0, spare = 0;

                for (int a = 0; a < attemptsPerLevel; a++)
                {
                    var run = new GameRun(DealerConfig.Default(), new ScoreRules());
                    var player = new AutoPlayer(PlayerSkill.Heuristic);
                    var rng = new Rng(unchecked((ulong)(n * 7919 + a) * 0x2545F4914F6CDD1DUL));

                    run.StartLevel(level);

                    while (!run.IsGameOver)
                    {
                        if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                        if (!run.Place(slot, col, row).Accepted) break;
                    }

                    if (!run.LevelComplete) continue;

                    wins++;
                    spare += run.MovesRemaining;
                    stars += level.StarsFor(run.MovesRemaining);
                }

                double rate = wins / (double)attemptsPerLevel;
                sumRate += rate;

                if (wins == 0) impossible++;
                if (n > Levels.Count / 2 && rate > 0.97) trivialTail++;

                // Print a sample rather than all 60 lines, plus anything alarming.
                bool notable = n <= 3 || n % 10 == 0 || n == Levels.Count || wins == 0;
                if (notable)
                {
                    Console.WriteLine($"  {n,4} {level.LineTarget,6} {level.MoveBudget,6} {rate,8:P0} " +
                                      $"{(wins == 0 ? 0 : stars / (double)wins),10:F2} " +
                                      $"{(wins == 0 ? 0 : spare / (double)wins),10:F1}");
                }
            }

            Console.WriteLine($"  mean beat rate across the ladder: {sumRate / Levels.Count:P1}");

            if (impossible > 0)
            {
                _failures++;
                Console.WriteLine($"  FAIL  {impossible} level(s) were never beaten in {attemptsPerLevel} attempts");
            }
            else
            {
                Console.WriteLine("  every level was beaten at least once");
            }

            if (trivialTail > 0)
                Console.WriteLine($"  note: {trivialTail} level(s) in the back half are beaten >97% of the time");

            Console.WriteLine();
        }

        // --- combo grace ----------------------------------------------------------------

        /// <summary>
        /// How often does a player actually see a combo?
        ///
        /// With no grace a streak needs a clear on literally every move, so combos are rare and the
        /// feature is invisible to most players. Grace lets a streak survive a few dry moves. The
        /// question is how much is needed to make combos a regular event without turning the score
        /// into nonsense, and that is measurable rather than arguable.
        /// </summary>
        private static void Combos()
        {
            Console.WriteLine("=== combo grace ===");
            const int runs = 400;

            Console.WriteLine($"  {"grace",6} {"mean best",10} {"reach x2",9} {"reach x3",9} {"reach x5",9} " +
                              $"{"combo moves",12} {"mean score",12} {"median run",11}");

            foreach (int grace in new[] { 0, 1, 2, 3 })
            {
                var rules = new ScoreRules { ComboGraceMoves = grace };

                int reach2 = 0, reach3 = 0, reach5 = 0;
                long comboMoves = 0, totalMoves = 0;
                double sumBest = 0, sumScore = 0;
                var lengths = new int[runs];

                for (int i = 0; i < runs; i++)
                {
                    var run = new GameRun(DealerConfig.Default(), rules);
                    var player = new AutoPlayer(PlayerSkill.Heuristic);
                    var rng = new Rng((7000UL + (ulong)i) ^ 0xA5A5A5A5A5A5A5A5UL);
                    run.StartNew(7000UL + (ulong)i);

                    int placed = 0;
                    while (!run.IsGameOver && placed < Simulator.MaxPiecesPerRun)
                    {
                        if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                        if (!run.Place(slot, col, row).Accepted) break;
                        placed++;

                        // A move "inside a combo" is one scored at better than 1x.
                        if (run.Score.ComboCount >= 2) comboMoves++;
                        totalMoves++;
                    }

                    lengths[i] = placed;
                    sumBest += run.Score.BestCombo;
                    sumScore += run.Score.Score;

                    if (run.Score.BestCombo >= 2) reach2++;
                    if (run.Score.BestCombo >= 3) reach3++;
                    if (run.Score.BestCombo >= 5) reach5++;
                }

                Array.Sort(lengths);

                Console.WriteLine($"  {grace,6} {sumBest / runs,10:F2} {reach2 / (double)runs,9:P1} " +
                                  $"{reach3 / (double)runs,9:P1} {reach5 / (double)runs,9:P1} " +
                                  $"{comboMoves / (double)Math.Max(1, totalMoves),12:P1} " +
                                  $"{sumScore / runs,12:F0} {lengths[runs / 2],11}");
            }

            Console.WriteLine();
        }

        // --- candidate validation -------------------------------------------------------

        /// <summary>
        /// The sweep optimises against the heuristic autoplayer, which is a model of a good player,
        /// not a real one. A config that only looks good there would be tuned to a fiction, so every
        /// candidate is re-checked across the whole skill range before anything is chosen.
        /// </summary>
        private static void Validate()
        {
            Console.WriteLine("=== candidate validation across skill levels ===");
            const int runs = 400;

            var candidates = new (string Label, double Bias, double Awareness)[]
            {
                ("conservative  b1.0 a0.6", 1.0, 0.6),
                ("moderate      b2.2 a1.2", 2.2, 1.2),
                ("generous      b3.5 a1.2", 3.5, 1.2),
                ("very generous b5.0 a1.2", 5.0, 1.2),
            };

            Console.WriteLine($"  {"candidate",-26} {"skill",-10} {"p05",5} {"median",8} {"p95",8} {"under20",9} {"mean score",12}");
            foreach (var c in candidates)
            {
                foreach (PlayerSkill skill in new[] { PlayerSkill.Greedy, PlayerSkill.Heuristic })
                {
                    var cfg = new DealerConfig
                    {
                        Guarantee = TrayGuarantee.WholeTraySurvivable,
                        CongestionBias = c.Bias,
                        PlacementAwareness = c.Awareness,
                    };
                    SimReport r = Simulator.Measure(c.Label, cfg, new ScoreRules(), skill, runs);
                    Console.WriteLine($"  {c.Label,-26} {skill,-10} {r.P05Pieces,5} {r.MedianPieces,8} {r.P95Pieces,8} " +
                                      $"{r.ShareUnder20Pieces,9:P2} {r.MeanScore,12:F0}");
                }
            }
            Console.WriteLine();
        }
    }
}
