using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;
using Snapline.Core.Sim;
using Snapline.UI;

namespace Snapline.App
{
    /// <summary>
    /// Produces the Play listing artwork that has to come from the running game.
    ///
    /// Screenshots are captured from genuine play: the harness wipes progress, then actually beats
    /// levels, plays an endless run and a special-block level, and captures what happens. Nothing is
    /// staged beyond choosing when to press the shutter — the one exception is the big-clear
    /// celebration, which is fired by hand only if the run never produced a real 3-line clear, and
    /// the log says so.
    ///
    ///   Game.exe -snapline-store -snapline-store-dir StoreAssets/shots -screen-width 540 -screen-height 960 -screen-fullscreen 0
    ///   Game.exe -snapline-feature -snapline-store-dir StoreAssets/shots -screen-width 1024 -screen-height 500 -screen-fullscreen 0
    /// </summary>
    public sealed class StoreShots : MonoBehaviour
    {
        public const string StoreFlag = "-snapline-store";
        public const string FeatureFlag = "-snapline-feature";
        private const string OutputFlag = "-snapline-store-dir";

        private Bootstrap _app;
        private GameController _controller;
        private string _outputDir;
        private bool _bigClearCaptured;

        public static bool StoreShotsRequested() => HasFlag(StoreFlag);
        public static bool FeatureGraphicRequested() => HasFlag(FeatureFlag);

        private static bool HasFlag(string flag)
        {
            foreach (string arg in System.Environment.GetCommandLineArgs())
                if (arg == flag) return true;
            return false;
        }

        public static string ResolveOutputDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            // Made absolute against the directory the player was launched from. A relative path goes
            // to ScreenCapture as-is, and a Windows player resolves it under its own _Data folder,
            // where the directory does not exist — every capture failed while the log said captured.
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == OutputFlag) return Path.GetFullPath(args[i + 1]);
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
            // Start from nothing, so every number on screen was earned during this capture.
            SaveSystem.WipeAll();
            Wallet.Reset();
            DailyProgress.Reset();
            NewBlockPopup.ResetSeen();

            yield return new WaitForSeconds(0.8f);

            // --- levels 1 to 8, for real, capturing the last win card ----------------------------
            for (int level = 1; level <= 8; level++)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    _app.StartLevel(level);
                    yield return new WaitForSeconds(0.6f);
                    yield return PlayOut(0.05f, 600);

                    float waited = 0f;
                    while (!_controller.LevelEndPopup.IsVisible && waited < 6f)
                    {
                        waited += Time.deltaTime;
                        yield return null;
                    }

                    GameRun r = _controller.Run;
                    Debug.Log($"[Snapline] store: level {level} attempt {attempt + 1} complete={r.LevelComplete} " +
                              $"stars={SaveSystem.StarsForLevel(level)}");

                    if (r.LevelComplete)
                    {
                        if (level == 8)
                        {
                            // Late enough that the three-star confetti has fallen past the ribbon and the
                            // coins have landed; earlier, confetti covered LEVEL COMPLETE! and drifted
                            // on into the next screen's capture.
                            yield return new WaitForSeconds(5.8f);
                            yield return Capture("06_level_complete");
                        }
                        break;
                    }

                    yield return new WaitForSeconds(0.4f);
                }
            }

            _app.ShowLevelSelect();
            yield return new WaitForSeconds(1.6f);
            yield return Capture("04_levels");

            // --- an endless run: a clear, and a big clear if one comes --------------------------
            _app.StartNewGame();
            yield return new WaitForSeconds(1.2f);
            yield return PlayEndless();

            if (!_bigClearCaptured)
            {
                Debug.Log("[Snapline] store: no 3-line clear happened in the run; firing the celebration by hand");
                _app.StartNewGame();
                yield return new WaitForSeconds(1.0f);
                yield return PlayOut(0.2f, 22);

                // Let the last real clear's shouts fade first; fired straight away, its GREAT! and COMBO
                // showed through the celebration as ghosted text.
                while (_controller.IsBusy) yield return null;
                yield return new WaitForSeconds(1.6f);
                _controller.DebugCelebrate(3);
                yield return new WaitForSeconds(0.55f);
                yield return Capture("03_big_clear");
            }

            // --- the special blocks --------------------------------------------------------------
            _app.StartLevel(Specials.FirstLevelHolding(Puzzles.First, Special.Stone, Special.Bomb));
            yield return new WaitForSeconds(1.8f);
            for (int i = 0; i < 3; i++)
            {
                _controller.DismissIntroForHarness();
                yield return new WaitForSeconds(1.0f);
            }
            yield return PlaySpecial();

            // --- the other screens ---------------------------------------------------------------
            _app.ShowDaily();
            yield return new WaitForSeconds(1.8f);
            yield return Capture("07_daily_challenge");

            _app.ShowToolbox();
            yield return new WaitForSeconds(1.6f);
            yield return Capture("08_toolbox");

            _app.ShowMenu();
            yield return new WaitForSeconds(2.2f);
            yield return Capture("01_home");

            Debug.Log($"[Snapline] StoreShots done. best={SaveSystem.HighScore} levels={SaveSystem.LevelsCompleted()} " +
                      $"stars={SaveSystem.TotalStars()} coins={Wallet.Coins}");

            yield return new WaitForSeconds(0.4f);
            Application.Quit(0);
        }

        private IEnumerator PlayOut(float pause, int maxMoves)
        {
            GameRun run = _controller.Run;
            var rng = new Rng(20260828UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            for (int moves = 0; moves < maxMoves && !run.IsGameOver; moves++)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                _controller.PlaceProgrammatically(slot, col, row);
                yield return new WaitForSeconds(pause);
            }
        }

        /// <summary>
        /// Plays an endless run, capturing an ordinary clear on a busy board and — if the run produces
        /// one — a real 3-line clear at the moment its celebration lands.
        /// </summary>
        private IEnumerator PlayEndless()
        {
            GameRun run = _controller.Run;
            var rng = new Rng(777001UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);
            bool clearCaptured = false;
            int lastClear = -10;

            for (int moves = 0; moves < 500 && !run.IsGameOver; moves++)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                int before = run.Score.TotalLinesCleared;
                _controller.PlaceProgrammatically(slot, col, row);
                yield return new WaitForSeconds(0.14f);
                int cleared = run.Score.TotalLinesCleared - before;

                if (cleared >= 3 && !_bigClearCaptured)
                {
                    _bigClearCaptured = true;
                    yield return new WaitForSeconds(0.36f);
                    yield return Capture("03_big_clear");
                    Debug.Log($"[Snapline] store: real {cleared}-line clear captured at move {moves}");
                }
                // Only a clear with nothing cleared on the moves just before it: the previous clear's
                // shouts live for about a second, and two NICE!s stacked on top of each other look broken.
                else if (!clearCaptured && cleared > 0 && moves - lastClear >= 4 && moves > 12 && run.Board.FilledCells > 16)
                {
                    clearCaptured = true;
                    yield return new WaitForSeconds(0.04f);
                    yield return Capture("02_gameplay");
                }

                if (cleared > 0) lastClear = moves;

                if (clearCaptured && _bigClearCaptured) break;
            }

            if (!clearCaptured) yield return Capture("02_gameplay");
            yield return new WaitForSeconds(0.8f);
        }

        /// <summary>Plays the special-block level until a stone cracks or a bomb goes off, and captures it.</summary>
        private IEnumerator PlaySpecial()
        {
            GameRun run = _controller.Run;
            var rng = new Rng(2024UL);
            var player = new AutoPlayer(PlayerSkill.Heuristic);

            for (int moves = 0; moves < 40 && !run.IsGameOver; moves++)
            {
                while (_controller.IsBusy) yield return null;
                if (run.IsGameOver) break;
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                ulong specials = run.Board.SpecialMask;
                ulong stones = run.Board.StickyMask;
                _controller.PlaceProgrammatically(slot, col, row);

                if (run.Board.SpecialMask != specials || run.Board.StickyMask != stones)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return Capture("05_special_blocks");
                    yield break;
                }

                yield return new WaitForSeconds(0.28f);
            }

            yield return Capture("05_special_blocks");
        }

        /// <summary>
        /// Multiplies the capture resolution, so the window fits on a desktop while the PNG comes out
        /// at listing size: a 540x960 window captures at 1080x1920.
        /// </summary>
        private const int SuperSize = 2;

        private IEnumerator Capture(string name)
        {
            string path = Path.Combine(_outputDir, $"{name}.png");
            ScreenCapture.CaptureScreenshot(path, SuperSize);
            for (int i = 0; i < 8; i++) yield return new WaitForEndOfFrame();

            // Checked rather than assumed: CaptureScreenshot reports failure only in its own log line.
            if (File.Exists(path))
                Debug.Log($"[Snapline] captured {path} at {Screen.width * SuperSize}x{Screen.height * SuperSize}");
            else
                Debug.LogError($"[Snapline] capture FAILED, no file at {path}");
        }

        // --- feature graphic --------------------------------------------------------------

        /// <summary>
        /// Builds the 1024x500 feature graphic and captures it: the wordmark and a tagline on the left,
        /// the candy board on the right, over the game's own background.
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
            bg.preserveAspect = false;

            var rng = new System.Random(90210);
            for (int i = 0; i < 12; i++)
            {
                Image s = UIKit.Image("Sprinkle", root, Fx.CapsuleSprite(), Fx.CandyColours[i % Fx.CandyColours.Length]);
                RectTransform rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(46f, 19f);
                rt.anchoredPosition = new Vector2((float)(rng.NextDouble() - 0.5) * 1040f, (float)(rng.NextDouble() - 0.5) * 500f);
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 180f);
            }

            Sprite board = ArtKit.Ui("board_preview");
            if (board != null)
            {
                Image b = CandyUI.Icon("Board", root, board);
                CandyUI.Place(b, new Vector2(0.5f, 0.5f), new Vector2(285f, -6f), new Vector2(500f, 450f));
            }

            Sprite logo = ArtKit.Logo();
            if (logo != null)
            {
                Image l = CandyUI.Icon("Logo", root, logo);
                CandyUI.Place(l, new Vector2(0.5f, 0.5f), new Vector2(-250f, 58f), new Vector2(560f, 220f));
            }
            else
            {
                W.Title("Title", root, "SNAPLINE", new Vector2(0.5f, 0.5f), new Vector2(-250f, 58f), 120, CandyStyle.White, false);
            }

            Text tag = W.Text("Tag", root, $"{Levels.Count} LEVELS  •  DAILY PUZZLES", new Vector2(0.5f, 0.5f),
                              new Vector2(-250f, -118f), new Vector2(600f, 70f), 46, CandyStyle.Cyan, Color.white);
            tag.font = Design.Display;

            string dir = ResolveOutputDir();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "feature-graphic-1024x500.png");

            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 8; i++) yield return new WaitForEndOfFrame();

            Debug.Log($"[Snapline] captured {path} at {Screen.width}x{Screen.height}");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(0);
        }
    }
}
