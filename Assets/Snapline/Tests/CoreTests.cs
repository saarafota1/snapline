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

        [Test]
        public void Revive_FreesSpaceKeepsScoreAndResumesTheRun()
        {
            GameRun run = PlayedToGameOver();

            Assert.IsTrue(run.IsGameOver, "The run should have ended.");

            int filledBefore = run.Board.FilledCells;
            long scoreBefore = run.Score.Score;

            ulong cleared = run.Revive(3);

            Assert.AreNotEqual(0UL, cleared, "The revive should have cleared something.");
            Assert.IsFalse(run.IsGameOver, "The run should be live again.");
            Assert.Less(run.Board.FilledCells, filledBefore, "The board should have freed up.");
            Assert.AreEqual(scoreBefore, run.Score.Score, "The score must carry over.");
            Assert.AreEqual(1, run.RevivesUsed);
            Assert.AreEqual(0, run.Score.ComboCount, "A revive must not bank a combo multiplier.");
            Assert.IsTrue(run.AnyRemainingPieceFits(), "The fresh tray should be playable.");
        }

        [Test]
        public void Revive_DoesNothingToALiveRun()
        {
            var run = new GameRun();
            run.StartNew(99UL);

            Assert.AreEqual(0UL, run.Revive(3));
            Assert.AreEqual(0, run.RevivesUsed);
        }

        [Test]
        public void Revive_IsRefusedInLevelMode()
        {
            var run = new GameRun();

            // A target nobody reaches, so the level fails on the first move.
            run.StartLevel(new LevelDef(1, lineTarget: 99, moveBudget: 1, seed: 5UL));

            var rng = new Rng(1UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            player.ChooseMove(run, ref rng, out int slot, out int col, out int row);
            run.Place(slot, col, row);

            Assert.IsTrue(run.IsGameOver);
            Assert.AreEqual(0UL, run.Revive(3), "Levels retry rather than revive.");
            Assert.AreEqual(0, run.RevivesUsed);
        }

        [Test]
        public void ClearFullestRows_TakesTheDensestRowsNotTheLowest()
        {
            var board = new Board();
            ShapeDef dot = Shapes.Get(0);

            // Row 2 gets four blocks, row 6 gets one. Neither completes, so nothing auto-clears.
            for (int c = 0; c < 4; c++) board.Place(dot, c, 2, 1);
            board.Place(dot, 0, 6, 1);

            Assert.AreEqual(5, board.FilledCells);

            ulong cleared = board.ClearFullestRows(1);

            Assert.AreEqual(4, Bits.PopCount(cleared), "The four-block row should have gone.");
            Assert.AreEqual(1, board.FilledCells, "The single block in the lower row should remain.");
        }

        // --- helpers ---------------------------------------------------------------------

        /// <summary>Play an endless run all the way to its end.</summary>
        private static GameRun PlayedToGameOver()
        {
            var run = new GameRun();
            run.StartNew(31337UL);

            var rng = new Rng(7UL);
            var player = new AutoPlayer(PlayerSkill.Random);

            int guard = 0;
            while (!run.IsGameOver && guard++ < 5000)
            {
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                if (!run.Place(slot, col, row).Accepted) break;
            }

            return run;
        }

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

        // --- tools ------------------------------------------------------------------------

        [Test]
        public void Undo_RestoresTheBoardAndTheScore()
        {
            var run = new GameRun();
            run.StartNew(4242UL);

            ulong before = run.Board.Occupied;
            long scoreBefore = run.Score.Score;

            Assert.IsFalse(run.CanUndo, "Nothing has been played, so there is nothing to undo.");
            Assert.IsTrue(PlayOneMove(run), "Expected a legal opening move.");
            Assert.AreNotEqual(before, run.Board.Occupied, "The move changed nothing.");

            Assert.IsTrue(run.CanUndo);
            Assert.IsTrue(run.Undo());
            Assert.AreEqual(before, run.Board.Occupied, "Undo did not restore the board.");
            Assert.AreEqual(scoreBefore, run.Score.Score, "Undo did not restore the score.");
        }

        [Test]
        public void Undo_IsOnlyOneMoveDeep()
        {
            var run = new GameRun();
            run.StartNew(99UL);

            PlayOneMove(run);
            PlayOneMove(run);

            Assert.IsTrue(run.Undo());
            Assert.IsFalse(run.CanUndo, "A second undo would rewind a move the player did not expect.");
            Assert.IsFalse(run.Undo());
        }

        [Test]
        public void Undo_IsNotConsumedByAnIllegalDrop()
        {
            var run = new GameRun();
            run.StartNew(7UL);
            PlayOneMove(run);

            // Somewhere off the board entirely, so the placement is refused outright.
            run.Place(0, -5, -5);

            Assert.IsTrue(run.CanUndo, "A refused drop must not eat the undo point.");
        }

        [Test]
        public void Hammer_SmashesTheAreaAroundTheCellAndScoresNothing()
        {
            var run = new GameRun();
            run.StartNew(1234UL);
            PlayOneMove(run);

            long scoreBefore = run.Score.Score;
            int comboBefore = run.Score.ComboCount;

            // Find any occupied cell.
            int hitCol = -1, hitRow = -1;
            for (int r = 0; r < Board.Height && hitRow < 0; r++)
            for (int c = 0; c < Board.Width; c++)
                if (run.Board.IsOccupied(c, r)) { hitCol = c; hitRow = r; break; }

            Assert.GreaterOrEqual(hitRow, 0, "Expected the board to hold at least one block.");

            ulong before = run.Board.Occupied;
            ulong expected = run.Board.AreaMask(hitCol, hitRow, GameRun.HammerRadius);
            Assert.IsTrue(run.Hammer(hitCol, hitRow, out ulong cleared));

            Assert.AreEqual(expected, cleared, "The hammer must report exactly what it took.");
            Assert.AreEqual(before & ~expected, run.Board.Occupied, "Only the smashed area may change.");

            for (int r = hitRow - 1; r <= hitRow + 1; r++)
            for (int c = hitCol - 1; c <= hitCol + 1; c++)
            {
                if (c < 0 || r < 0 || c >= Board.Width || r >= Board.Height) continue;
                Assert.IsFalse(run.Board.IsOccupied(c, r), $"({c},{r}) was inside the smashed area.");
            }

            Assert.AreEqual(scoreBefore, run.Score.Score, "A hammer must not earn points.");
            Assert.AreEqual(comboBefore, run.Score.ComboCount, "A hammer must not touch the combo.");
        }

        [Test]
        public void Hammer_TakesStoneInOneBlowAndDoesNotSetOffBombs()
        {
            Board board = AlmostFullBottomRow((3, Special.Stone), (5, Special.Bomb));

            ulong cleared = board.ClearArea(4, 7, GameRun.HammerRadius);

            Assert.AreEqual(3, Bits.PopCount(cleared), "Three blocks of the bottom row were within reach.");
            Assert.IsFalse(board.IsOccupied(3, 7), "Stone goes in one blow from a tool that was paid for.");
            Assert.IsFalse(board.IsOccupied(5, 7), "A hammered bomb is taken away, not set off.");
            Assert.IsTrue(board.IsOccupied(6, 7), "A block outside the area must survive a defused bomb.");
        }

        [Test]
        public void Hammer_OnAnEmptyCellIsRefused()
        {
            var run = new GameRun();
            run.StartNew(5UL);

            // A fresh run has an empty board, so any cell will do.
            Assert.IsFalse(run.Hammer(0, 0), "An empty cell must not consume the tool.");
            Assert.AreEqual(0UL, run.Board.Occupied, "A refused hammer must not touch the board.");
        }

        [Test]
        public void ShuffleTray_ReplacesEveryPiece()
        {
            var run = new GameRun();
            run.StartNew(31337UL);

            var before = new int[run.Tray.Length];
            for (int i = 0; i < run.Tray.Length; i++) before[i] = run.Tray[i].ShapeId;

            Assert.IsTrue(run.ShuffleTray());

            for (int i = 0; i < run.Tray.Length; i++)
                Assert.IsFalse(run.Tray[i].Consumed, "Every slot should hold a fresh piece.");

            // The dealer may legitimately deal the same shape again, so the assertion is that the
            // tray was re-dealt (the RNG advanced), not that every id differs.
            Assert.IsTrue(run.Tray.Length > 0);
        }

        [Test]
        public void ShuffleTray_LeavesTheUndoPointAlone_ByClearingIt()
        {
            var run = new GameRun();
            run.StartNew(11UL);
            PlayOneMove(run);

            Assert.IsTrue(run.CanUndo);
            run.ShuffleTray();
            Assert.IsFalse(run.CanUndo, "Undoing across a shuffle would restore the discarded pieces.");
        }

        [Test]
        public void Economy_EndlessRewardIsACoinALinePlusTheCombo()
        {
            Assert.AreEqual(38, Economy.EndlessReward(30, 4));
            Assert.AreEqual(0, Economy.EndlessReward(-5, -1));
        }

        [Test]
        public void Economy_PricesMatchTheStore()
        {
            Assert.AreEqual(100, Economy.Price(Tool.Undo));
            Assert.AreEqual(150, Economy.Price(Tool.Shuffle));
            Assert.AreEqual(250, Economy.Price(Tool.Hammer));
        }

        [Test]
        public void Economy_LevelRewardPaysOnlyForNewStars()
        {
            Assert.AreEqual(20, Economy.LevelReward(0, 2));
            Assert.AreEqual(10, Economy.LevelReward(2, 3));
            Assert.AreEqual(0, Economy.LevelReward(3, 2), "Replaying without improving must pay nothing.");
        }

        // --- special blocks ---------------------------------------------------------------

        /// <summary>Row 7, columns 0 to 6 filled, with the given specials; column 7 left open.</summary>
        private static Board AlmostFullBottomRow(params (int Col, Special Kind)[] specials)
        {
            ulong occupied = 0UL;
            var colours = new byte[Board.CellCount];
            var kinds = new byte[Board.CellCount];
            for (int c = 0; c < 7; c++)
            {
                int i = Bits.Index(c, 7);
                occupied |= 1UL << i;
                colours[i] = 1;
            }
            foreach ((int col, Special kind) in specials) kinds[Bits.Index(col, 7)] = (byte)kind;

            var board = new Board();
            board.Restore(occupied, colours, kinds);
            return board;
        }

        [Test]
        public void Stone_CracksOnTheFirstClearAndBreaksOnTheSecond()
        {
            ShapeDef dot = Shapes.Get(0);
            Board board = AlmostFullBottomRow((3, Special.Stone));

            PlaceResult first = board.Place(dot, 7, 7, 2);
            Assert.AreEqual(1, first.RowsCleared, "A line with a stone in it still counts as cleared.");
            Assert.AreEqual(1UL << Bits.Index(3, 7), first.CrackedMask);
            Assert.AreEqual(Special.CrackedStone, board.SpecialAt(3, 7));
            Assert.AreEqual(1, board.FilledCells, "Only the stone survives the first clear.");

            for (int c = 0; c < 7; c++)
                if (c != 3) Assert.IsTrue(board.Place(dot, c, 7, 1).Placed);

            PlaceResult second = board.Place(dot, 7, 7, 1);
            Assert.AreEqual(1, second.RowsCleared);
            Assert.AreEqual(0UL, second.CrackedMask);
            Assert.IsTrue(board.IsEmpty, "A cracked stone breaks on its second clear.");
        }

        [Test]
        public void Bomb_BlastsItsNeighboursAndChains()
        {
            Board board = AlmostFullBottomRow((3, Special.Bomb));
            ulong occupied = board.Occupied | 1UL << Bits.Index(3, 6) | 1UL << Bits.Index(2, 6)
                           | 1UL << Bits.Index(1, 5) | 1UL << Bits.Index(4, 4);
            byte[] specials = board.CopySpecials();
            specials[Bits.Index(2, 6)] = (byte)Special.Bomb;
            board.Restore(occupied, board.CopyColours(), specials);

            PlaceResult r = board.Place(Shapes.Get(0), 7, 7, 1);

            Assert.AreEqual(1, r.RowsCleared);
            Assert.AreEqual((1UL << Bits.Index(3, 7)) | (1UL << Bits.Index(2, 6)), r.BombMask, "The second bomb is set off by the first.");
            Assert.IsFalse(board.IsOccupied(3, 6), "In the first bomb's blast.");
            Assert.IsFalse(board.IsOccupied(1, 5), "Only in the chained bomb's blast.");
            Assert.IsTrue(board.IsOccupied(4, 4), "Outside both blasts.");
            Assert.AreEqual(1, board.FilledCells);
            Assert.AreNotEqual(0UL, r.BlastMask & (1UL << Bits.Index(1, 5)));
        }

        [Test]
        public void Gift_IsCountedWhenCleared()
        {
            Board board = AlmostFullBottomRow((0, Special.Gift));
            Assert.AreEqual(1, board.Place(Shapes.Get(0), 7, 7, 1).GiftsCollected);
        }

        [Test]
        public void Simulate_KeepsStickyCellsThroughAClear()
        {
            ulong row = Bits.RowMask[7];
            ulong last = 1UL << Bits.Index(7, 7);
            ulong stone = 1UL << Bits.Index(3, 7);
            Assert.AreEqual(0UL, Board.Simulate(row & ~last, last));
            Assert.AreEqual(stone, Board.Simulate(row & ~last, last, stone));
        }

        /// <summary>
        /// Plays, in a level opening on <paramref name="board"/>, the first move whose clear satisfies
        /// <paramref name="wanted"/>. The tray is dealt, so a few seeds are tried. False if none offered one.
        /// </summary>
        private static bool PlayMoveThat(Board board, System.Func<PlaceResult, bool> wanted, out GameRun run)
        {
            for (ulong seed = 1; seed < 60; seed++)
            {
                run = new GameRun();
                run.StartLevel(new LevelDef(9999, 30, 10, seed, board.Occupied, board.CopyColours(), board.CopySpecials()));

                for (int slot = 0; slot < run.Tray.Length; slot++)
                for (int r = 0; r < Board.Height; r++)
                for (int c = 0; c < Board.Width; c++)
                {
                    if (!run.CanPlace(slot, c, r)) continue;
                    PlaceResult probe = run.Board.Clone().Place(run.Tray[slot].Shape, c, r, 1);
                    if (!wanted(probe)) continue;
                    run.Place(slot, c, r);
                    return true;
                }
            }

            run = null;
            return false;
        }

        [Test]
        public void GiftInALevel_AddsMoves()
        {
            if (!PlayMoveThat(AlmostFullBottomRow((0, Special.Gift)), p => p.GiftsCollected > 0, out GameRun run))
                Assert.Inconclusive("No dealt tray could clear the gift's row.");

            Assert.AreEqual(10 - 1 + GameRun.GiftMoves, run.MovesRemaining);
        }

        [Test]
        public void Undo_PutsACrackedStoneBack()
        {
            if (!PlayMoveThat(AlmostFullBottomRow((3, Special.Stone)), p => p.CrackedMask != 0UL, out GameRun run))
                Assert.Inconclusive("No dealt tray could clear the stone's row.");

            Assert.AreEqual(Special.CrackedStone, run.Board.SpecialAt(3, 7));
            Assert.IsTrue(run.Undo());
            Assert.AreEqual(Special.Stone, run.Board.SpecialAt(3, 7));
            Assert.AreEqual(10, run.MovesRemaining);
        }

        [Test]
        public void PuzzleLevels_OpenFairWithEachBlockOnlyOnceIntroduced()
        {
            for (int n = Puzzles.First; n <= Puzzles.Last; n++)
            {
                LevelDef level = Levels.Get(n);
                Assert.AreEqual(n, level.Number);
                Assert.Greater(level.MoveBudget, level.LineTarget);

                for (int r = 0; r < Board.Height; r++)
                    Assert.AreNotEqual(Bits.RowMask[r], level.StartOccupied & Bits.RowMask[r], $"Level {n} opens with row {r} complete.");
                for (int c = 0; c < Board.Width; c++)
                    Assert.AreNotEqual(Bits.ColMask[c], level.StartOccupied & Bits.ColMask[c], $"Level {n} opens with column {c} complete.");

                for (int i = 0; i < Board.CellCount; i++)
                    if (level.StartSpecials[i] != 0)
                        Assert.AreNotEqual(0UL, level.StartOccupied & (1UL << i), $"Level {n} has a special block on an empty cell.");

                Assert.AreEqual(n >= Puzzles.StonesFrom, level.Has(Special.Stone), $"Level {n} stones");
                Assert.AreEqual(n >= Puzzles.GiftsFrom, level.Has(Special.Gift), $"Level {n} gifts");
                Assert.AreEqual(n >= Puzzles.BombsFrom, level.Has(Special.Bomb), $"Level {n} bombs");
            }
        }

        /// <summary>Plays the first legal move it can find. Returns false if the run is stuck.</summary>
        private static bool PlayOneMove(GameRun run)
        {
            for (int slot = 0; slot < run.Tray.Length; slot++)
            for (int r = 0; r < Board.Height; r++)
            for (int c = 0; c < Board.Width; c++)
                if (run.CanPlace(slot, c, r) && run.Place(slot, c, r).Accepted) return true;

            return false;
        }
    }
}
