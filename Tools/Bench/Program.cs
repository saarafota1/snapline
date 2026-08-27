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
