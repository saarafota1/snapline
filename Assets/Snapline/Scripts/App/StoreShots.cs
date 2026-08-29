using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;
using Snapline.Core.Sim;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Produces the Play listing artwork that has to come from the running game.
    ///
    /// Screenshots are captured from genuine play: the harness wipes progress, then actually beats
    /// levels and plays an endless run, and captures what happens. Play's listing policy requires
    /// screenshots to be real gameplay from a non-development build, and mocked-up images are a
    /// rejection reason — so nothing here is staged beyond choosing *when* to press the shutter.
    ///
    ///   Game.exe -snapline-store -screen-width 1080 -screen-height 1920 -screen-fullscreen 0
    ///   Game.exe -snapline-feature -screen-width 1024 -screen-height 500 -screen-fullscreen 0
    /// </summary>
    public sealed class StoreShots : MonoBehaviour
    {
        public const string StoreFlag = "-snapline-store";
        public const string FeatureFlag = "-snapline-feature";
        private const string OutputFlag = "-snapline-store-dir";

        private Bootstrap _app;
        private GameController _controller;
        private string _outputDir;

        public static bool StoreShotsRequested() => HasFlag(StoreFlag);
        public static bool FeatureGraphicRequested() => HasFlag(FeatureFlag);

        private static bool HasFlag(string flag)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == flag) return true;
            return false;
        }

        public static string ResolveOutputDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == OutputFlag) return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "store");
        }

        // --- screenshots -----------------------------------------------------------------

        public void Begin(GameController controller, Bootstrap app)
        {
            _controller = controller;
            _app = app;
            _outputDir = ResolveOutputDir();
            Directory.CreateDirectory(_outputDir);

            Debug.Log($"[Snapline] StoreShots writing to {_outputDir} at {Screen.width}x{Screen.height}");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Start from nothing so everything the listing shows was earned during this capture run.
            SaveSystem.WipeAll();

            yield return new WaitForSeconds(0.6f);

            // --- earn some level progress for real ---------------------------------------
            for (int level = 1; level <= 8; level++)
            {
                Debug.Log($"[Snapline] phase: level {level} start");
                _app.StartLevel(level);
                yield return new WaitForSeconds(0.5f);
                yield return PlayOut(fast: true, captureClears: 0);

                GameRun r = _controller.Run;
                Debug.Log($"[Snapline] phase: level {level} end complete={r.LevelComplete} " +
                          $"failed={r.LevelFailed} moves={r.MovesUsed} stars={SaveSystem.StarsForLevel(level)}");

                yield return new WaitForSeconds(1.6f);
            }

            yield return Capture("05_level_complete");

            Debug.Log("[Snapline] phase: level select");
            _app.ShowLevelSelect();
            yield return new WaitForSeconds(0.9f);
            yield return Capture("04_levels");

            // --- an endless run, capturing the good moments -------------------------------
            Debug.Log("[Snapline] phase: endless run");
            _app.StartNewGame();
            yield return new WaitForSeconds(1.0f);
            yield return PlayOut(fast: false, captureClears: 3);

            Debug.Log("[Snapline] phase: endless finished");
            yield return new WaitForSeconds(1.0f);

            Debug.Log("[Snapline] phase: scores");
            _app.ShowScores();
            yield return new WaitForSeconds(0.8f);
            yield return Capture("06_scores");

            Debug.Log("[Snapline] phase: menu");
            _app.ShowMenu();
            yield return new WaitForSeconds(1.0f);
            yield return Capture("01_menu");

            Debug.Log($"[Snapline] StoreShots done. best={SaveSystem.HighScore} " +
                      $"levels={SaveSystem.LevelsCompleted()} stars={SaveSystem.TotalStars()}");

            yield return new WaitForSeconds(0.4f);
            Application.Quit(0);
        }

        /// <summary>
        /// Play the current run to its end. When capturing, it waits on the frames right after a
        /// multi-line clear, which is when the board is busiest and the effects are at full strength.
        /// </summary>
        private IEnumerator PlayOut(bool fast, int captureClears)
        {
            GameRun run = _controller.Run;
            var rng = new Rng(20260828UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            int captured = 0;
            int moves = 0;

            while (!run.IsGameOver && moves < 600)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;

                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                int before = run.Score.TotalLinesCleared;
                _controller.PlaceProgrammatically(slot, col, row);
                moves++;

                yield return new WaitForSeconds(fast ? 0.06f : 0.18f);

                int cleared = run.Score.TotalLinesCleared - before;

                if (captured < captureClears && cleared >= 2 && run.Board.FilledCells > 14)
                {
                    // A beat after the clear starts: debris in the air, popups up, score rolling.
                    yield return new WaitForSeconds(0.16f);
                    yield return Capture($"0{2 + captured}_gameplay");
                    captured++;
                }
            }
        }

        /// <summary>
        /// Multiplies the capture resolution, so the window can stay small enough to actually fit on
        /// the desktop while the PNG comes out at listing size. Running the window itself at
        /// 1080x1920 is taller than most monitors and the player never draws a frame.
        /// </summary>
        private const int SuperSize = 2;

        private IEnumerator Capture(string name)
        {
            string path = Path.Combine(_outputDir, $"{name}.png");
            ScreenCapture.CaptureScreenshot(path, SuperSize);
            for (int i = 0; i < 8; i++) yield return new WaitForEndOfFrame();
            Debug.Log($"[Snapline] captured {path} at {Screen.width * SuperSize}x{Screen.height * SuperSize}");
        }

        // --- feature graphic --------------------------------------------------------------

        /// <summary>
        /// Build the 1024x500 feature graphic and capture it.
        ///
        /// Rendered by the game rather than drawn offline so the title uses the same font and the
        /// blocks the same generator as everything the player sees — the banner and the game cannot
        /// drift apart.
        /// </summary>
        public static IEnumerator BuildAndCaptureFeatureGraphic(RectTransform canvasRect)
        {
            RectTransform root = UIKit.Stretch("Feature", canvasRect);

            Image bg = UIKit.Image("Bg", root, ArtKit.Background(), Color.white);
            RectTransform bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Blocks live in the outer bands only. The first pass scattered them across the middle
            // and the title lost all its contrast — on a feature graphic the words have to win.
            const float textSafeHalfWidth = 330f;
            var rng = new System.Random(90210);

            for (int i = 0; i < 14; i++)
            {
                Image block = UIKit.Image($"B{i}", root, ArtKit.Block(i % Palette.Count), Color.white);
                RectTransform rt = block.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float size = 58f + (float)rng.NextDouble() * 84f;
                rt.sizeDelta = new Vector2(size, size);

                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * Mathf.Lerp(textSafeHalfWidth + size * 0.3f, 500f, (float)rng.NextDouble());
                float y = ((float)rng.NextDouble() - 0.5f) * 430f;

                rt.anchoredPosition = new Vector2(x, y);
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 40f - 20f);

                Color c = block.color;
                c.a = 0.55f + (float)rng.NextDouble() * 0.4f;
                block.color = c;
            }

            Text title = UIKit.Label("Title", root, "SNAPLINE", 132, Palette.TextBright);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(1000f, 150f));

            Text sub = UIKit.Label("Sub", root, "BLOCK PUZZLE", 52, Palette.Accent);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(1000f, 64f));

            Text tag = UIKit.Label("Tag", root, "ENDLESS  ·  60 LEVELS", 34, Palette.TextDim);
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1000f, 46f));

            string dir = ResolveOutputDir();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "feature-graphic-1024x500.png");

            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 8; i++) yield return new WaitForEndOfFrame();

            Debug.Log($"[Snapline] captured {path} at {Screen.width}x{Screen.height}");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(0);
        }
    }
}
