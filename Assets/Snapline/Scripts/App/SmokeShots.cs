using System.Collections;
using System.IO;
using UnityEngine;
using Snapline.Core;
using Snapline.Core.Sim;

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
        private string _outputDir;
        private int _shotIndex;

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

        public void Begin(GameController controller)
        {
            _controller = controller;
            _outputDir = ResolveOutputDir();
            Directory.CreateDirectory(_outputDir);
            Debug.Log($"[Snapline] SmokeShots writing to {_outputDir}");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Let Bootstrap finish and the first tray animate in.
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

                _controller.PlaceProgrammatically(slot, col, row);
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

            yield return new WaitForSeconds(0.4f);
            Application.Quit(0);
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
