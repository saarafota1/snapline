using System.Collections;
using System.IO;
using UnityEngine;
using Snapline.Art;
using Snapline.Core;
using Snapline.Core.Sim;
using Snapline.UI;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Drives the real game automatically and captures every screen and card.
    ///
    /// This is the only way to check that the presentation layer actually works. Unit tests prove
    /// the rules are right and the console harness proves the dealing is fair, but neither can tell
    /// you the board rendered, the cards laid out, or the explosion fired. It plays through the
    /// ordinary placement path, so what it captures is what a player would see.
    ///
    /// Enabled with -snapline-shots on the player command line. Never active in a normal launch.
    /// </summary>
    public sealed class SmokeShots : MonoBehaviour
    {
        public const string EnableFlag = "-snapline-shots";
        private const string OutputFlag = "-snapline-shots-dir";

        private const int SkilledMoves = 26;
        private const int MaxMoves = 400;

        private GameController _controller;
        private DragController _drag;
        private Bootstrap _app;
        private string _outputDir;

        private int _dragMismatches;
        private int _dragsPerformed;
        private int _lastRequestedCol = -1;
        private int _lastRequestedRow = -1;

        public static bool RequestedOnCommandLine()
        {
            foreach (string arg in System.Environment.GetCommandLineArgs())
                if (arg == EnableFlag) return true;
            return false;
        }

        private static string ResolveOutputDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == OutputFlag) return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "shots");
        }

        public void Begin(GameController controller, DragController drag, Bootstrap app)
        {
            _controller = controller;
            _drag = drag;
            _app = app;
            _outputDir = ResolveOutputDir();
            Directory.CreateDirectory(_outputDir);

            _drag.PlacementRequested += (slot, col, row) =>
            {
                _lastRequestedCol = col;
                _lastRequestedRow = row;
            };

            Debug.Log($"[Snapline] SmokeShots writing to {_outputDir}");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Something in the wallet, so the store, the tools and the coin pills are shown holding
            // real numbers. This is the harness's own save on a desktop build, not a player's.
            if (Wallet.Coins < 600) Wallet.Grant(1180);
            if (Wallet.TotalTools == 0)
            {
                Wallet.GrantTool(Tool.Undo, 3);
                Wallet.GrantTool(Tool.Shuffle, 2);
                Wallet.GrantTool(Tool.Hammer, 1);
            }

            yield return new WaitForSeconds(1.8f);
            yield return Capture("00_menu");

            _app.OpenSettings();
            yield return new WaitForSeconds(1.0f);
            yield return Capture("00b_settings");

            yield return EndlessPhase();
            yield return PausePhase();
            yield return LevelPhase();
            yield return SpecialPhase();
            yield return StorePhase();
            yield return DailyPhase();

            _app.ShowMenu();
            yield return new WaitForSeconds(1.4f);
            yield return Capture("11_menu_returning");

            Debug.Log($"[Snapline] sound cues played: pop={Sound.PlayCount("pop")} stick={Sound.PlayCount("stick")} " +
                      $"tap={Sound.PlayCount("tap")} coin={Sound.PlayCount("coin")} star_earned={Sound.PlayCount("star_earned")} " +
                      $"win={Sound.PlayCount("win")} lose={Sound.PlayCount("lose")} bounce={Sound.PlayCount("bounce")} " +
                      $"of {Sound.ClipCount} loaded");

            yield return new WaitForSeconds(0.4f);
            Application.Quit(0);
        }

        // --- endless ---------------------------------------------------------------------------

        private IEnumerator EndlessPhase()
        {
            _app.StartNewGame();
            yield return new WaitForSeconds(1.2f);
            yield return Capture("01_start");

            var rng = new Rng(20260827UL);
            GameRun run = _controller.Run;
            var skilled = new AutoPlayer(PlayerSkill.Heuristic);
            var careless = new AutoPlayer(PlayerSkill.Random);
            int moves = 0;
            bool clearCaptured = false;

            while (!run.IsGameOver && moves < MaxMoves)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;

                AutoPlayer player = moves < SkilledMoves ? skilled : careless;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                int linesBefore = run.Score.TotalLinesCleared;
                _lastRequestedCol = -1;
                _lastRequestedRow = -1;
                _drag.SimulateDragTo(slot, col, row);
                _dragsPerformed++;

                if (_lastRequestedCol != col || _lastRequestedRow != row)
                {
                    _dragMismatches++;
                    Debug.LogError($"[Snapline] drag landed wrong: intended ({col},{row}), " +
                                   $"drag requested ({_lastRequestedCol},{_lastRequestedRow})");
                }

                moves++;

                if (!clearCaptured && moves > 4 && run.Score.TotalLinesCleared > linesBefore)
                {
                    clearCaptured = true;
                    yield return new WaitForSeconds(0.14f);
                    yield return Capture("02b_clear_effects");
                }

                yield return new WaitForSeconds(0.24f);

                if (moves == 10) yield return Capture("02_playing");
                if (moves == 20) yield return Capture("03_busy_board");
                if (moves == SkilledMoves) yield return Capture("04_mid_run");
            }

            Debug.Log($"[Snapline] SmokeShots endless finished after {moves} moves, score {run.Score.Score}");
            Debug.Log(_dragMismatches == 0
                ? $"[Snapline] drag path OK: {_dragsPerformed} drags all landed on the intended cell."
                : $"[Snapline] drag path BROKEN: {_dragMismatches} of {_dragsPerformed} drags landed on the wrong cell.");

            yield return new WaitForSeconds(1.6f);
            yield return Capture("05_no_more_moves");

            if (_controller.NoMovesPopup.IsVisible)
            {
                int filledBefore = run.Board.FilledCells;
                _controller.RequestRevive();
                yield return new WaitForSeconds(3.2f);

                if (!run.IsGameOver && run.RevivesUsed == 1)
                {
                    Debug.Log($"[Snapline] revive OK: board went from {filledBefore} to {run.Board.FilledCells} " +
                              $"filled cells, run resumed, score kept at {run.Score.Score}");
                    yield return Capture("05c_after_continue");
                    // Careless on purpose: the point is to reach the results card, and a strong player
                    // with a rescued board survives hundreds of moves.
                    yield return PlayOut(run, 4242UL, 0.05f);
                }
                else
                {
                    Debug.Log("[Snapline] revive: not taken (no simulated ad?), ending the run");
                    _controller.NoMovesPopup.ChooseEnd();
                }
            }

            yield return new WaitForSeconds(4.6f);
            yield return Capture("05d_great_run");
        }

        private IEnumerator PlayOut(GameRun run, ulong seed, float pause)
        {
            var rng = new Rng(seed);
            var player = new AutoPlayer(PlayerSkill.Random);
            int guard = 0;
            while (!run.IsGameOver && guard++ < MaxMoves)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                _drag.SimulateDragTo(slot, col, row);
                yield return new WaitForSeconds(pause);
            }

            // A second dead board goes straight to the results; end it if the card still asks.
            yield return new WaitForSeconds(1.2f);
            if (_controller.NoMovesPopup.IsVisible) _controller.NoMovesPopup.ChooseEnd();
        }

        private IEnumerator PausePhase()
        {
            _app.StartNewGame();
            yield return new WaitForSeconds(1.0f);

            GameRun run = _controller.Run;
            var rng = new Rng(777UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            for (int i = 0; i < 6 && !run.IsGameOver; i++)
            {
                while (_controller.IsBusy) yield return null;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                _drag.SimulateDragTo(slot, col, row);
                yield return new WaitForSeconds(0.3f);
            }

            yield return new WaitForSeconds(0.8f);
            _controller.OpenPause();
            yield return new WaitForSeconds(1.1f);
            yield return Capture("12_pause");
        }

        // --- levels ----------------------------------------------------------------------------

        private IEnumerator LevelPhase()
        {
            _app.ShowLevelSelect();
            yield return new WaitForSeconds(1.2f);
            yield return Capture("06_level_select");

            int level = Mathf.Min(SaveSystem.HighestUnlockedLevel(), 3);
            _app.StartLevel(level);
            yield return new WaitForSeconds(1.2f);

            GameRun run = _controller.Run;
            var rng = new Rng(31337UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            int moves = 0;
            bool captured = false;

            while (!run.IsGameOver && moves < MaxMoves)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                _drag.SimulateDragTo(slot, col, row);
                moves++;
                yield return new WaitForSeconds(0.24f);

                if (!captured && moves >= 6)
                {
                    captured = true;
                    yield return Capture("07_level_playing");
                }
            }

            yield return new WaitForSeconds(4.0f);
            yield return Capture("08_level_result");

            Debug.Log($"[Snapline] level {level}: complete={run.LevelComplete} failed={run.LevelFailed} " +
                      $"lines={run.Score.TotalLinesCleared}/{run.Objective?.LineTarget} moves={run.MovesUsed}");

            _app.ShowLevelSelect();
            yield return new WaitForSeconds(1.2f);
            yield return Capture("09_level_select_progress");

            _app.ShowScores();
            yield return new WaitForSeconds(1.2f);
            yield return Capture("10_scores");
        }

        /// <summary>
        /// A puzzle level with every kind of special block: the NEW BLOCK! cards, the board, a clear
        /// that cracks a stone or sets a bomb off, and the big-clear celebrations.
        /// </summary>
        private IEnumerator SpecialPhase()
        {
            NewBlockPopup.ResetSeen();
            _app.StartLevel(Puzzles.BombsFrom + 4);
            yield return new WaitForSeconds(1.8f);
            yield return Capture("17_new_block");

            for (int i = 0; i < 3; i++)
            {
                _controller.DismissIntroForHarness();
                yield return new WaitForSeconds(1.0f);
            }
            yield return Capture("18_special_level");

            GameRun run = _controller.Run;
            var rng = new Rng(2024UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            bool captured = false;
            int guard = 0;

            while (!run.IsGameOver && guard++ < 40)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                ulong specialsBefore = run.Board.SpecialMask;
                ulong stonesBefore = run.Board.StickyMask;
                _drag.SimulateDragTo(slot, col, row);

                if (!captured && (run.Board.SpecialMask != specialsBefore || run.Board.StickyMask != stonesBefore))
                {
                    captured = true;
                    yield return new WaitForSeconds(0.2f);
                    yield return Capture("18b_special_clear");
                    Debug.Log($"[Snapline] special clear seen after {guard} moves:\n{run.Board}");
                }

                yield return new WaitForSeconds(0.26f);
            }

            Debug.Log($"[Snapline] special level {run.LevelNumber}: complete={run.LevelComplete} lines={run.Score.TotalLinesCleared} " +
                      $"moves left={run.MovesRemaining} bonus={run.BonusMoves} special clear captured={captured}");

            yield return new WaitForSeconds(3.5f);
            _app.StartNewGame();
            yield return new WaitForSeconds(1.0f);

            _controller.DebugCelebrate(3);
            yield return new WaitForSeconds(0.5f);
            yield return Capture("19_boom_huge_play");
            yield return new WaitForSeconds(2.2f);

            _controller.DebugCelebrate(5);
            yield return new WaitForSeconds(0.55f);
            yield return Capture("20_unnatural");
            yield return new WaitForSeconds(2.4f);
        }

        private IEnumerator StorePhase()
        {
            _app.ShowToolbox();
            yield return new WaitForSeconds(1.3f);
            yield return Capture("13_toolbox");
        }

        private IEnumerator DailyPhase()
        {
            _app.ShowDaily();
            yield return new WaitForSeconds(1.5f);
            yield return Capture("14_daily");

            _app.StartDaily();
            yield return new WaitForSeconds(1.6f);
            yield return Capture("15_daily_playing");

            GameRun run = _controller.Run;
            var rng = new Rng(99UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            int guard = 0;
            while (!run.IsGameOver && guard++ < 80)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                _drag.SimulateDragTo(slot, col, row);
                yield return new WaitForSeconds(0.24f);
            }

            yield return new WaitForSeconds(4.0f);
            yield return Capture("16_daily_result");
            Debug.Log($"[Snapline] daily: complete={run.LevelComplete} lines={run.Score.TotalLinesCleared} " +
                      $"moves={run.MovesUsed} streak={DailyProgress.Streak()}");
        }

        private IEnumerator Capture(string name)
        {
            string path = Path.Combine(_outputDir, $"{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 6; i++) yield return new WaitForEndOfFrame();
            Debug.Log($"[Snapline] captured {path}");
        }
    }
}
