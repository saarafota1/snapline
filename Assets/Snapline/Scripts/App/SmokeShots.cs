using System.Collections;
using System.IO;
using UnityEngine;
using Snapline.Core;
using Snapline.Core.Sim;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Drives the real game automatically and captures screenshots.
    ///
    /// This is the only way to check that the presentation layer actually works. Unit tests prove
    /// the rules are right and the console harness proves the dealing is fair, but neither can tell
    /// you the board rendered, the gradients look like gradients, or the explosion fired. It plays
    /// through the ordinary placement path, so what it captures is what a player would see.
    ///
    /// Enabled with -snapline-shots on the player command line. Never active in a normal launch.
    /// </summary>
    public sealed class SmokeShots : MonoBehaviour
    {
        public const string EnableFlag = "-snapline-shots";
        private const string OutputFlag = "-snapline-shots-dir";

        /// <summary>Moves played with a competent player before switching to careless play.</summary>
        private const int SkilledMoves = 26;

        /// <summary>Hard cap so a badly behaved build cannot spin forever.</summary>
        private const int MaxMoves = 400;

        private GameController _controller;
        private DragController _drag;
        private string _outputDir;
        private int _shotIndex;

        private int _dragMismatches;
        private int _dragsPerformed;
        private int _lastRequestedCol = -1;
        private int _lastRequestedRow = -1;

        public static bool RequestedOnCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == EnableFlag) return true;
            return false;
        }

        private static string ResolveOutputDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == OutputFlag) return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "shots");
        }

        private Bootstrap _app;

        public void Begin(GameController controller, DragController drag, Bootstrap app)
        {
            _controller = controller;
            _drag = drag;
            _app = app;
            _outputDir = ResolveOutputDir();
            Directory.CreateDirectory(_outputDir);

            // Record where the drag path actually asked to place each piece, so it can be compared
            // against where the harness intended to drop it.
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
            // The front screen, with its drifting blocks settled.
            yield return new WaitForSeconds(1.1f);
            yield return Capture("00_menu");

            _app.StartNewGame();

            // Let the first tray animate in.
            yield return new WaitForSeconds(1.2f);
            yield return Capture("01_start");

            var rng = new Rng(20260827UL);
            GameRun run = _controller.Run;

            int moves = 0;
            var skilled = new AutoPlayer(PlayerSkill.Heuristic);
            var careless = new AutoPlayer(PlayerSkill.Random);

            while (!run.IsGameOver && moves < MaxMoves)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;

                AutoPlayer player = moves < SkilledMoves ? skilled : careless;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                // Go through the real drag path — pick up, move, release — rather than calling the
                // controller directly, so the input maths is actually exercised.
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

                // Pause on the frames just after a clear, where the explosion is at full strength.
                yield return new WaitForSeconds(0.22f);

                if (moves == 10) yield return Capture("02_playing");
                if (moves == 20) yield return Capture("03_busy_board");
                if (moves == SkilledMoves) yield return Capture("04_mid_run");
            }

            // The game-over card slides in and the board sweeps; give both time to land.
            yield return new WaitForSeconds(2.4f);
            yield return Capture("05_game_over");

            Debug.Log($"[Snapline] SmokeShots finished after {moves} moves, " +
                      $"score {run.Score.Score}, gameOver={run.IsGameOver}");

            if (_dragMismatches == 0)
                Debug.Log($"[Snapline] drag path OK: {_dragsPerformed} drags all landed on the intended cell.");
            else
                Debug.LogError($"[Snapline] drag path BROKEN: {_dragMismatches} of {_dragsPerformed} " +
                               "drags landed on the wrong cell.");

            yield return LevelPhase();

            yield return new WaitForSeconds(0.4f);
            Application.Quit(0);
        }

        /// <summary>
        /// Walk the level side of the game: the grid, a level being played, and the result card.
        /// Endless being fine says nothing about whether level mode renders at all.
        /// </summary>
        private IEnumerator LevelPhase()
        {
            _app.ShowLevelSelect();
            yield return new WaitForSeconds(0.7f);
            yield return Capture("06_level_select");

            _app.StartLevel(1);
            yield return new WaitForSeconds(1.1f);

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
                yield return new WaitForSeconds(0.2f);

                if (!captured && moves >= 6)
                {
                    captured = true;
                    yield return Capture("07_level_playing");
                }
            }

            // The result card slides in and the stars land one by one.
            yield return new WaitForSeconds(2.6f);
            yield return Capture("08_level_result");

            Debug.Log($"[Snapline] level 1: complete={run.LevelComplete} failed={run.LevelFailed} " +
                      $"lines={run.Score.TotalLinesCleared}/{run.Objective?.LineTarget} moves={run.MovesUsed}");

            // The level grid again, now with level 1 cleared and level 2 unlocked.
            _app.ShowLevelSelect();
            yield return new WaitForSeconds(0.6f);
            yield return Capture("09_level_select_progress");

            _app.ShowScores();
            yield return new WaitForSeconds(0.6f);
            yield return Capture("10_scores");

            _app.ShowMenu();
            yield return new WaitForSeconds(0.8f);
            yield return Capture("11_menu_returning");
        }

        private IEnumerator Capture(string name)
        {
            _shotIndex++;
            string path = Path.Combine(_outputDir, $"{name}.png");

            // CaptureScreenshot writes at the end of the current frame, so the file does not exist
            // yet when the call returns. Waiting a few frames is what makes it reliable.
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 6; i++) yield return new WaitForEndOfFrame();

            Debug.Log($"[Snapline] captured {path}");
        }
    }
}
