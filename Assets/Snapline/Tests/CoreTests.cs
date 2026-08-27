using NUnit.Framework;
using Snapline.Core;
using Snapline.Core.Sim;

namespace Snapline.Tests
{
    /// <summary>
    /// Tests for the rules engine.
    ///
    /// These run in the editor, but nothing here touches UnityEngine — the same assertions run far
    /// faster in Tools/Bench. This suite exists so a regression is caught by anyone who opens the
    /// project and hits Run All, without needing the console harness.
    /// </summary>
    public class CoreTests
    {
        [Test]
        public void ShapeCatalogue_IdsMatchIndices()
        {
            for (int i = 0; i < Shapes.Count; i++)
                Assert.AreEqual(i, Shapes.All[i].Id, $"Shape at index {i} has a mismatched id.");
        }

        [Test]
        public void ShapeCatalogue_PlacementMasksNeverWrapRows()
        {
            foreach (ShapeDef s in Shapes.All)
            {
                foreach (ulong mask in s.PlacementMasks)
                    Assert.AreEqual(s.CellCount, Bits.PopCount(mask),
                                    $"Shape {s.Name} lost cells when shifted; a placement wrapped a row.");
            }
        }

        [Test]
        public void CompletingARow_ClearsIt()
        {
            var board = new Board();
            ShapeDef bar4 = Shapes.Get(5); // I4H

            board.Place(bar4, 0, 0, 1);
            PlaceResult result = board.Place(bar4, 4, 0, 2);

            Assert.AreEqual(1, result.RowsCleared);
            Assert.AreEqual(0, result.ColsCleared);
            Assert.IsTrue(board.IsEmpty);
        }

        [Test]
        public void CompletingARowAndColumnTogether_CountsSharedCellOnce()
        {
            var board = new Board();
            ShapeDef dot = Shapes.Get(0);

            for (int c = 0; c < 7; c++) board.Place(dot, c, 7, 1);
            for (int r = 0; r < 7; r++) board.Place(dot, 7, r, 1);
            Assert.AreEqual(14, board.FilledCells);

            PlaceResult result = board.Place(dot, 7, 7, 2);

            Assert.AreEqual(1, result.RowsCleared);
            Assert.AreEqual(1, result.ColsCleared);
            Assert.AreEqual(15, Bits.PopCount(result.ClearedMask), "The shared corner was counted twice.");
            Assert.IsTrue(board.IsEmpty);
        }

        [Test]
        public void Simulate_MatchesPlace()
        {
            var board = new Board();
            ShapeDef bar4 = Shapes.Get(5);
            board.Place(bar4, 0, 3, 1);

            ulong predicted = Board.Simulate(board.Occupied, bar4.BaseMask << Bits.Index(4, 3));

            Board copy = board.Clone();
            copy.Place(bar4, 4, 3, 2);

            Assert.AreEqual(copy.Occupied, predicted);
        }

        [Test]
        public void ComboMultiplier_AppliesFromTheSecondClear()
        {
            var state = new ScoreState();
            var rules = new ScoreRules();
            var clear = new PlaceResult { Placed = true, CellsPlaced = 4, RowsCleared = 1 };

            ScoreDelta first = Scoring.Apply(state, rules, clear, false);
            Assert.AreEqual(1.0, first.ComboMultiplier, 1e-9, "The opening clear must not get a free multiplier.");

            ScoreDelta second = Scoring.Apply(state, rules, clear, false);
            Assert.AreEqual(1.0 + rules.ComboStep, second.ComboMultiplier, 1e-9);
        }

        [Test]
        public void DryMoves_BreakTheComboOnceGraceIsUsedUp()
        {
            var state = new ScoreState();
            var rules = new ScoreRules();

            Scoring.Apply(state, rules, new PlaceResult { Placed = true, CellsPlaced = 4, RowsCleared = 1 }, false);
            Assert.AreEqual(1, state.ComboCount);

            // The shipped rules forgive one dry move, so it takes ComboGraceMoves + 1 to break.
            for (int i = 0; i <= rules.ComboGraceMoves; i++)
                Scoring.Apply(state, rules, new PlaceResult { Placed = true, CellsPlaced = 3 }, false);

            Assert.AreEqual(0, state.ComboCount);
        }

        [Test]
        public void SaveRoundTrip_PreservesTheRun()
        {
            GameRun run = PlayedRun(30);
            RunSnapshot snapshot = run.Snapshot();

            RunSnapshot decoded = SaveCodec.Decode(SaveCodec.Encode(snapshot));

            Assert.IsNotNull(decoded);
            Assert.AreEqual(snapshot.Occupied, decoded.Occupied);
            Assert.AreEqual(snapshot.Score, decoded.Score);
            Assert.AreEqual(snapshot.RngState, decoded.RngState);
        }

        [Test]
        public void CorruptSave_IsRejectedRatherThanPartlyLoaded()
        {
            GameRun run = PlayedRun(12);
            string encoded = SaveCodec.Encode(run.Snapshot());

            Assert.IsNull(SaveCodec.Decode(encoded.Substring(0, encoded.Length - 4)), "Truncated save was accepted.");
            Assert.IsNull(SaveCodec.Decode("not a save"));
            Assert.IsNull(SaveCodec.Decode(""));
            Assert.IsNull(SaveCodec.Decode(null));
        }

        [Test]
        public void Version1Save_StillLoadsAfterTheFormatGrew()
        {
            // A player mid-run when the update lands has a version 1 payload in PlayerPrefs. It has
            // one fewer field than version 2, and must still restore rather than silently starting
            // them on a fresh board.
            // Byte fields are continuous hex with no separators; only the int list uses commas.
            const string body =
                "SNAP1|1234567890|00010203|0,1,2|000102|100|9876543210|4200|3|5|17|42|2|0";

            string v1 = body + "|" + Fnv1a(body).ToString("X8");

            RunSnapshot decoded = SaveCodec.Decode(v1);

            Assert.IsNotNull(decoded, "A version 1 save was rejected outright.");
            Assert.AreEqual(1, decoded.Version);
            Assert.AreEqual(1234567890UL, decoded.Occupied);
            Assert.AreEqual(4200, decoded.Score);
            Assert.AreEqual(9876543210UL, decoded.RngState);
            Assert.AreEqual(0, decoded.DryMovesSinceClear, "The field added in v2 should default to 0.");
        }

        [Test]
        public void ComboGrace_LetsAStreakSurviveOneDryMove()
        {
            var rules = new ScoreRules { ComboGraceMoves = 1 };
            var state = new ScoreState();

            var clear = new PlaceResult { Placed = true, CellsPlaced = 4, RowsCleared = 1 };
            var dry = new PlaceResult { Placed = true, CellsPlaced = 3 };

            Scoring.Apply(state, rules, clear, false);
            Scoring.Apply(state, rules, clear, false);
            Assert.AreEqual(2, state.ComboCount);

            Scoring.Apply(state, rules, dry, false);
            Assert.AreEqual(2, state.ComboCount, "One dry move should be forgiven at grace 1.");

            Scoring.Apply(state, rules, dry, false);
            Assert.AreEqual(0, state.ComboCount, "A second dry move should break the streak.");
        }

        [Test]
        public void ComboGraceZero_BreaksOnTheFirstDryMove()
        {
            var rules = new ScoreRules { ComboGraceMoves = 0 };
            var state = new ScoreState();

            Scoring.Apply(state, rules, new PlaceResult { Placed = true, CellsPlaced = 4, RowsCleared = 1 }, false);
            Assert.AreEqual(1, state.ComboCount);

            Scoring.Apply(state, rules, new PlaceResult { Placed = true, CellsPlaced = 3 }, false);
            Assert.AreEqual(0, state.ComboCount);
        }

        /// <summary>Mirrors SaveCodec's private checksum so a fixture payload can be built by hand.</summary>
        private static uint Fnv1a(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        [Test]
        public void RestoredRun_ContinuesIdenticallyToAnUninterruptedOne()
        {
            GameRun original = PlayedRun(24);

            var restored = new GameRun();
            restored.Restore(SaveCodec.Decode(SaveCodec.Encode(original.Snapshot())));

            Assert.AreEqual(Continue(original, 25), Continue(restored, 25),
                            "A resumed run diverged from one that was never interrupted.");
        }

        [Test]
        public void WholeTraySurvivable_NeverDealsAnUnplayableTray()
        {
            DealerConfig config = DealerConfig.Default();
            Assert.AreEqual(TrayGuarantee.WholeTraySurvivable, config.Guarantee);

            int checkedTrays = 0;

            for (ulong seed = 0; seed < 12; seed++)
            {
                var run = new GameRun(config, new ScoreRules());
                var rng = new Rng(seed);
                var player = new AutoPlayer(PlayerSkill.Heuristic);
                run.StartNew(seed);

                int placed = 0;
                while (!run.IsGameOver && placed < 200)
                {
                    if (run.TrayFullyDealt())
                    {
                        var ids = new int[run.Tray.Length];
                        for (int i = 0; i < ids.Length; i++) ids[i] = run.Tray[i].ShapeId;

                        var probe = new Dealer(config);
                        Assert.IsTrue(probe.IsTraySurvivable(run.Board.Occupied, ids, ids.Length),
                                      $"Dealt an unplayable tray on seed {seed} after {placed} pieces.");
                        checkedTrays++;
                    }

                    if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                    if (!run.Place(slot, col, row).Accepted) break;
                    placed++;
                }
            }

            Assert.Greater(checkedTrays, 100, "The audit did not actually inspect many trays.");
        }

        [Test]
        public void SameSeed_ProducesTheSameRun()
        {
            RunOutcome a = Simulator.PlayOne(DealerConfig.Default(), new ScoreRules(), PlayerSkill.Heuristic, 77UL);
            RunOutcome b = Simulator.PlayOne(DealerConfig.Default(), new ScoreRules(), PlayerSkill.Heuristic, 77UL);

            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.PiecesPlaced, b.PiecesPlaced);
        }

        [Test]
        public void EnclosedCellCounting_TreatsBoardEdgesAsWalls()
        {
            Assert.AreEqual(0, AutoPlayer.CountEnclosedEmptyCells(0UL));
            Assert.AreEqual(1, AutoPlayer.CountEnclosedEmptyCells(ulong.MaxValue & ~(1UL << Bits.Index(3, 4))));
            Assert.AreEqual(1, AutoPlayer.CountEnclosedEmptyCells(ulong.MaxValue & ~1UL),
                            "A hole in the corner is still enclosed.");
        }

        [Test]
        public void EveryLevel_HasASaneTargetAndBudget()
        {
            for (int n = 1; n <= Levels.Count; n++)
            {
                LevelDef level = Levels.Get(n);

                Assert.AreEqual(n, level.Number);
                Assert.Greater(level.LineTarget, 0, $"Level {n} asks for no lines.");
                Assert.Greater(level.MoveBudget, level.LineTarget,
                               $"Level {n} allows fewer moves than lines, which cannot be beaten.");
                Assert.Greater(level.ThreeStarSpare, level.TwoStarSpare,
                               $"Level {n} star thresholds are inverted.");
            }
        }

        [Test]
        public void LevelDifficulty_IncreasesDownTheLadder()
        {
            LevelDef first = Levels.Get(1);
            LevelDef last = Levels.Get(Levels.Count);

            Assert.Greater(last.LineTarget, first.LineTarget);
            Assert.Less(last.MoveBudget / (double)last.LineTarget,
                        first.MoveBudget / (double)first.LineTarget,
                        "Moves allowed per required line should tighten as levels go on.");
        }

        [Test]
        public void ReachingTheLineTarget_CompletesTheLevel()
        {
            var run = new GameRun();
            run.StartLevel(Levels.Get(1));

            Assert.AreEqual(GameMode.Level, run.Mode);
            Assert.IsFalse(run.LevelComplete);

            var rng = new Rng(4242UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            while (!run.IsGameOver)
            {
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                if (!run.Place(slot, col, row).Accepted) break;
            }

            Assert.IsTrue(run.LevelComplete, "The autoplayer should beat level 1.");
            Assert.IsFalse(run.LevelFailed);
            Assert.GreaterOrEqual(run.Score.TotalLinesCleared, run.Objective.LineTarget);
        }

        [Test]
        public void RunningOutOfMoves_FailsTheLevel()
        {
            var run = new GameRun();

            // A target no one can reach inside a single move.
            var level = new LevelDef(1, lineTarget: 99, moveBudget: 1, seed: 12345UL);
            run.StartLevel(level);

            var rng = new Rng(1UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            Assert.IsTrue(player.ChooseMove(run, ref rng, out int slot, out int col, out int row));
            run.Place(slot, col, row);

            Assert.IsTrue(run.LevelFailed);
            Assert.IsFalse(run.LevelComplete);
            Assert.IsTrue(run.IsGameOver);
            Assert.AreEqual(0, run.MovesRemaining);
        }

        [Test]
        public void EndlessRun_HasNoObjective()
        {
            var run = new GameRun();
            run.StartNew(7UL);

            Assert.AreEqual(GameMode.Endless, run.Mode);
            Assert.IsNull(run.Objective);
            Assert.IsFalse(run.LevelComplete);
            Assert.IsFalse(run.LevelFailed);
        }

        // --- helpers ---------------------------------------------------------------------

        private static GameRun PlayedRun(int moves)
        {
            var run = new GameRun();
            run.StartNew(4242UL);

            var rng = new Rng(11UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            for (int i = 0; i < moves && !run.IsGameOver; i++)
            {
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                if (!run.Place(slot, col, row).Accepted) break;
            }

            return run;
        }

        private static string Continue(GameRun run, int moves)
        {
            var rng = new Rng(555UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            for (int i = 0; i < moves && !run.IsGameOver; i++)
            {
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                if (!run.Place(slot, col, row).Accepted) break;
            }

            return run.Board.Occupied.ToString("X16") + ":" + run.Score.Score;
        }
    }
}
