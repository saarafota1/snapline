using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;
using Snapline.UI;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Builds the entire game at runtime from one empty GameObject.
    ///
    /// The scene asset holds nothing but this component. Everything else — canvas, layout, board,
    /// tray, screens, popups, effects, sound — is constructed here, which means the whole interface
    /// is reviewable as source, diffs cleanly, and cannot be broken by someone nudging a
    /// RectTransform in the inspector.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Bootstrap : MonoBehaviour
    {
        // Design resolution. Everything below is expressed in these units and scaled to the device.
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;

        /// <summary>The board's proportions, as fractions of the framed board's size.</summary>
        private static class BoardLayout
        {
            /// <summary>Largest the framed board is drawn, on a tall phone.</summary>
            public const float MaxSize = 880f;
            public const float SideMargin = 44f;

            /// <summary>From the frame's outer edge to the first cell.</summary>
            public const float Inset = 0.078f;
            public const float Gap = 0.0095f;

            /// <summary>Thickness of the candy stripe.</summary>
            public const float Stripe = 0.078f;
        }

        /// <summary>
        /// Where the tray and the tools sit in each mode, measured UP from the bottom of the screen,
        /// read off `endless_game.png` and `level_game.png`. The two references disagree about whether
        /// the tools go above or below the tray; each mode follows its own reference.
        /// </summary>
        private static class PlayLayout
        {
            public static readonly Vector2 EndlessSlot = new Vector2(328f, 296f);
            public const float EndlessSlotSpacing = 336f;
            public const float EndlessTrayY = 326f;
            public const float EndlessToolsY = 622f;
            public const float EndlessToolsScale = 1.04f;
            public const float EndlessBottomReserve = 772f;

            public static readonly Vector2 LevelSlot = new Vector2(310f, 232f);
            public const float LevelSlotSpacing = 322f;
            public const float LevelTrayY = 488f;
            public const float LevelToolsY = 238f;
            public const float LevelToolsScale = 0.98f;
            public const float LevelBottomReserve = 618f;

            public const float ToolSpacing = 292f;
            public const float BoardGap = 14f;
        }

        /// <summary>Services config. Assigned by SceneBuilder, so the reference is visible in the scene.</summary>
        [SerializeField] private GameKit.GameKitConfig _gameKitConfig;

        private RectTransform _gameRoot;
        private RectTransform _boardPanel;
        private RectTransform _trayRoot;
        private float _safeHeight;

        private GameController _controller;
        private DragController _drag;
        private TrayView _tray;
        private ToolsBar _tools;

        private MainMenu _menu;
        private LevelSelect _levelSelect;
        private DailyScreen _daily;
        private ScoresPanel _scores;
        private ToolboxPanel _toolbox;
        private PausePopup _pause;

        private bool _toolboxOverGame;
        private bool _dragWasEnabled;

        private void Awake()
        {
            ConfigureScreen();

            Canvas canvas = BuildCanvas();
            EnsureEventSystem();
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            // The feature graphic is a landscape banner, not the game. Build it and quit before any
            // of the portrait layout below runs.
            if (StoreShots.FeatureGraphicRequested())
            {
                StartCoroutine(StoreShots.BuildAndCaptureFeatureGraphic(canvasRect));
                return;
            }

            // The background is the only thing outside the safe area, so it bleeds behind a notch.
            BuildBackground(canvasRect);

            RectTransform safeRoot = UIKit.Rect("SafeArea", canvasRect);
            ApplySafeArea(safeRoot);

            float canvasHeight = CanvasHeightUnits();
            Rect safe = Screen.safeArea;
            float safeWidth = safe.width * UnitsPerPixel();
            _safeHeight = canvasHeight * (safe.height / Mathf.Max(1f, Screen.height));

            // Everything that should shake lives under here. Screens and cards deliberately do not.
            RectTransform shakeRoot = UIKit.Stretch("ShakeRoot", safeRoot);
            _gameRoot = UIKit.Stretch("GameRoot", shakeRoot);

            float boardSize = ComputeBoardSize(safeWidth, _safeHeight);
            float gap = Mathf.Max(4f, Mathf.Round(boardSize * BoardLayout.Gap));
            float cell = Mathf.Floor((boardSize * (1f - 2f * BoardLayout.Inset) - (Board.Width - 1) * gap) / Board.Width);
            float gridExtent = Board.Width * cell + (Board.Width - 1) * gap;

            Debug.Log($"[Snapline] layout: screen {Screen.width}x{Screen.height}, canvas {RefWidth:F0}x{canvasHeight:F0} " +
                      $"units, safe {safeWidth:F0}x{_safeHeight:F0}, board {boardSize:F0}, cell {cell:F0}, gap {gap:F0}");

            RectTransform grid = BuildBoardPanel(_gameRoot, boardSize, gridExtent);
            RectTransform ghostLayer = BuildGhostLayer(grid);

            _trayRoot = UIKit.Rect("Tray", _gameRoot);
            _trayRoot.anchorMin = _trayRoot.anchorMax = new Vector2(0.5f, 0f);
            _trayRoot.pivot = new Vector2(0.5f, 0.5f);
            _trayRoot.sizeDelta = new Vector2(RefWidth, 300f);

            _tools = Make<ToolsBar>("ToolsController", _gameRoot);
            _tools.Init(_gameRoot);

            var hud = Make<Hud>("HudController", _gameRoot);
            hud.Init(_gameRoot, SaveSystem.HighScore);

            // Last in the game screen, so a dragged piece passes over the tray, the tools and the HUD.
            RectTransform dragLayer = UIKit.Stretch("DragLayer", _gameRoot);

            var boardView = grid.gameObject.AddComponent<BoardView>();
            boardView.Init(grid, ghostLayer, cell, gap);

            _tray = _trayRoot.gameObject.AddComponent<TrayView>();
            _tray.Init(_trayRoot, 3, Mathf.Max(4f, Mathf.Round(gap * 0.6f)));

            _drag = gameObject.AddComponent<DragController>();
            _drag.Init(boardView, _tray, dragLayer, canvas, cell, gap);

            Sound.Init(transform);
            Sound.Muted = !Settings.SoundEnabled;
            Music.Create(transform, Settings.MusicEnabled);

            // Screens, then the cards over them, then the store over everything — it can be opened
            // from a card, and has to cover it.
            _menu = Make<MainMenu>("MainMenu", safeRoot);
            _menu.Init(safeRoot);
            _levelSelect = Make<LevelSelect>("LevelSelect", safeRoot);
            _levelSelect.Init(safeRoot);
            _daily = Make<DailyScreen>("DailyScreen", safeRoot);
            _daily.Init(safeRoot);
            _scores = Make<ScoresPanel>("ScoresPanel", safeRoot);
            _scores.Init(safeRoot);

            _pause = Make<PausePopup>("PausePopup", safeRoot);
            _pause.Init(safeRoot);
            var noMoves = Make<NoMovesPopup>("NoMovesPopup", safeRoot);
            noMoves.Init(safeRoot);
            var greatRun = Make<GreatRunPopup>("GreatRunPopup", safeRoot);
            greatRun.Init(safeRoot);
            var levelEnd = Make<LevelEndPopup>("LevelEndPopup", safeRoot);
            levelEnd.Init(safeRoot);
            var newBlock = Make<NewBlockPopup>("NewBlockPopup", safeRoot);
            newBlock.Init(safeRoot);

            _toolbox = Make<ToolboxPanel>("Toolbox", safeRoot);
            _toolbox.Init(safeRoot);

            // Services come up in the background. Nothing waits on them: with no SDK installed the
            // kit hands back offline implementations and the game plays exactly the same.
            //
            // Through AnalyticsConsent rather than straight into the kit, so a player who declines
            // consent - at launch or later from PRIVACY SETTINGS - actually stops being measured.
            if (_gameKitConfig != null) _ = AnalyticsConsent.InitializeAsync(_gameKitConfig);

            Telemetry.ResumeSession();

            var ads = gameObject.AddComponent<AdController>();
            ads.Init(_gameKitConfig);

            _controller = gameObject.AddComponent<GameController>();
            _controller.Init(boardView, _tray, _drag, hud, _tools, ads, _pause, noMoves, greatRun, levelEnd, newBlock);
            _controller.LayoutRequested += ApplyLayout;
            _controller.MenuRequested += ShowMenu;
            _controller.LevelsRequested += ShowLevelSelect;
            _controller.DailyRequested += ShowDaily;
            _controller.ScoresRequested += ShowScores;
            ApplyLayout(GameMode.Endless);

            _menu.NewGameRequested += StartNewGame;
            _menu.ContinueRequested += ContinueGame;
            _menu.LevelsRequested += ShowLevelSelect;
            _menu.ScoresRequested += ShowScores;
            _menu.ShareRequested += Share;
            _menu.SoundToggled += ToggleSound;
            _menu.PrivacyRequested += ShowPrivacyOptions;
            _menu.DailyRequested += ShowDaily;
            _menu.SettingsRequested += OpenSettings;

            _levelSelect.LevelChosen += StartLevel;
            _levelSelect.BackRequested += ShowMenu;

            _daily.PlayRequested += StartDaily;
            _daily.BackRequested += ShowMenu;

            _scores.BackRequested += ShowMenu;
            _scores.ShareRequested += Share;
            _scores.PlayRequested += StartNewGame;

            _toolbox.BackRequested += CloseToolbox;
            _toolbox.WatchAdRequested += WatchAdForCoins;
            Nav.ToolboxRequested += ShowToolbox;

            _pause.PrivacyRequested += ShowPrivacyOptions;

            // Over every screen and card, so an effect fired from a result card is never hidden by it.
            Fx.Create(canvasRect, shakeRoot);

            // Every screen is built by this point, so one pass reaches every label — including the
            // ones the kit creates internally. Must stay ABOVE the screenshot branches below.
            CandyUI.ApplyFont(canvasRect.gameObject);

            if (StoreShots.StoreShotsRequested())
            {
                _drag.InputEnabled = false;
                ShowMenu();
                gameObject.AddComponent<StoreShots>().Begin(_controller, this);
                return;
            }

            if (SmokeShots.RequestedOnCommandLine())
            {
                _drag.InputEnabled = false;
                ShowMenu();
                gameObject.AddComponent<SmokeShots>().Begin(_controller, _drag, this);
                return;
            }

            ShowMenu();

            // The splash goes over a menu that is already built and shown, so a player who taps to
            // skip on the first frame lands on a finished screen instead of watching one assemble.
            SplashScreen.TryPlay(canvasRect, null);
        }

        private void OnDestroy() => Nav.ToolboxRequested -= ShowToolbox;

        private static T Make<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        // --- screens ---------------------------------------------------------------------------

        private void HideScreens()
        {
            _controller.HideOverlays();
            _pause.HideNow();
            _menu.Hide();
            _levelSelect.Hide();
            _daily.Hide();
            _scores.Hide();
            _toolbox.Hide();
            _toolboxOverGame = false;
        }

        internal void ShowMenu()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(false);

            // Re-asked every time: consent is gathered asynchronously, so at first launch the answer
            // often is not known yet when the menu is first built.
            _menu.SetPrivacyAvailable(GameKit.GameKitRuntime.Consent.IsPrivacyOptionsRequired);
            _menu.Show(SaveSystem.HighScore, GameController.HasSavedRun);
        }

        internal void ShowScores()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(false);
            _scores.Show();
        }

        internal void ShowLevelSelect()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(false);
            _levelSelect.Show();
        }

        internal void ShowDaily()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(false);
            _daily.Show();
        }

        /// <summary>
        /// The store opens over whatever is showing and closes back to it — including a run in
        /// progress, whose input is held off while the store is up, because the drag controller
        /// polls the pointer directly and would otherwise pick pieces up through it.
        /// </summary>
        internal void ShowToolbox()
        {
            if (_toolbox.IsVisible) return;

            _toolboxOverGame = _gameRoot.gameObject.activeSelf;
            if (_toolboxOverGame)
            {
                _dragWasEnabled = _drag.InputEnabled;
                _drag.InputEnabled = false;
            }

            _toolbox.Show();
        }

        private void CloseToolbox()
        {
            _toolbox.Hide();
            if (_toolboxOverGame) _drag.InputEnabled = _dragWasEnabled;
            _toolboxOverGame = false;
        }

        internal void OpenSettings() =>
            _pause.ShowSettings(GameKit.GameKitRuntime.Consent.IsPrivacyOptionsRequired);

        internal void StartLevel(int number)
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(true);
            _controller.StartLevel(number);
        }

        internal void StartNewGame()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(true);
            _controller.StartNewRun();
        }

        internal void StartDaily()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(true);
            _controller.StartDaily();
        }

        private void ContinueGame()
        {
            HideScreens();
            _gameRoot.gameObject.SetActive(true);

            // The save can vanish between the menu being drawn and the tap. Fall back to a fresh board.
            if (!_controller.ResumeSavedRun()) _controller.StartNewRun();
        }

        private static void Share() => GameKit.Share.TextWithLink(GameController.ShareMessage(SaveSystem.HighScore));

        private void ToggleSound()
        {
            Settings.SoundEnabled = !Settings.SoundEnabled;
            Sound.Muted = !Settings.SoundEnabled;
            _menu.RefreshSoundLabel();
            if (Settings.SoundEnabled) Sound.Tap();
        }

        /// <summary>
        /// Pays out for a rewarded ad, and pays nothing if the ad did not actually play. Silently
        /// granting coins for an ad that never ran is how an ad account gets flagged.
        /// </summary>
        private async void WatchAdForCoins()
        {
            var ads = GetComponent<AdController>();
            if (ads == null) return;

            if (Wallet.AdRewardsLeftToday <= 0)
            {
                Sound.Deny();
                if (_toolbox.WatchButton != null)
                {
                    Tween.Shake((RectTransform)_toolbox.WatchButton, 16f, 0.4f);
                    Fx.Instance?.Text(_toolbox.WatchButton.position, "MORE VIDEOS TOMORROW", CandyStyle.White, 50f, 1.3f, 180f);
                }
                return;
            }

            bool watched = await ads.ShowRewardedAsync();
            if (!watched)
            {
                Sound.Deny();
                return;
            }

            CoinPill.HoldRoll(1.2f);
            Wallet.RecordAdReward();
            Wallet.Grant(Economy.AdReward);
            _toolbox.Refresh();

            CoinPill pill = CoinPill.Visible();
            if (pill != null && _toolbox.WatchButton != null)
                Fx.Instance?.CoinFly(_toolbox.WatchButton.position, pill.Coin, 10, 72f);
            Sound.Prize();
        }

        /// <summary>
        /// Reopens the consent form. Through GameKitRuntime rather than GameKitRuntime.Consent: only the
        /// runtime's version raises ConsentChanged afterwards, which is how LevelPlay's GDPR flag and
        /// analytics collection hear a changed answer.
        /// </summary>
        private async void ShowPrivacyOptions()
        {
            await GameKit.GameKitRuntime.ShowPrivacyOptionsAsync();

            bool required = GameKit.GameKitRuntime.Consent.IsPrivacyOptionsRequired;
            if (_menu != null) _menu.SetPrivacyAvailable(required);
            if (_pause != null) _pause.SetPrivacyAvailable(required);
        }

        // --- the play screen's layout ----------------------------------------------------------

        /// <summary>
        /// The largest framed board that fits across the screen and, in BOTH modes, between the HUD
        /// and the tray and tools. One size for both, so switching modes never rescales the pieces.
        /// </summary>
        private static float ComputeBoardSize(float safeWidth, float safeHeight)
        {
            float byWidth = Mathf.Min(BoardLayout.MaxSize, safeWidth - BoardLayout.SideMargin * 2f);
            float endless = safeHeight - Hud.Layout.EndlessBottom - PlayLayout.BoardGap * 2f - PlayLayout.EndlessBottomReserve;
            float level = safeHeight - Hud.Layout.LevelBottom - PlayLayout.BoardGap * 2f - PlayLayout.LevelBottomReserve;
            return Mathf.Floor(Mathf.Max(360f, Mathf.Min(byWidth, Mathf.Min(endless, level))));
        }

        /// <summary>Places the board, the tray and the tools for a mode. The board centres in the room left.</summary>
        private void ApplyLayout(GameMode mode)
        {
            bool level = mode == GameMode.Level;

            float top = Hud.Bottom(mode) + PlayLayout.BoardGap;
            float reserve = level ? PlayLayout.LevelBottomReserve : PlayLayout.EndlessBottomReserve;
            float available = _safeHeight - top - reserve - PlayLayout.BoardGap;
            _boardPanel.anchoredPosition = new Vector2(0f, -(top + available * 0.5f));

            _trayRoot.anchoredPosition = new Vector2(0f, level ? PlayLayout.LevelTrayY : PlayLayout.EndlessTrayY);
            _tray.Layout(level ? PlayLayout.LevelSlot : PlayLayout.EndlessSlot,
                         level ? PlayLayout.LevelSlotSpacing : PlayLayout.EndlessSlotSpacing,
                         level ? 58f : 70f);

            _tools.Place(level ? PlayLayout.LevelToolsY : PlayLayout.EndlessToolsY, PlayLayout.ToolSpacing,
                         level ? PlayLayout.LevelToolsScale : PlayLayout.EndlessToolsScale);
        }

        private RectTransform BuildBoardPanel(RectTransform parent, float size, float gridExtent)
        {
            RectTransform panel = UIKit.Rect("BoardPanel", parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(size, size);
            _boardPanel = panel;

            float ground = size * (1f - BoardLayout.Stripe * 1.2f);
            W.Rounded("Ground", panel, CandyText.Hex(0x172668), CandyText.Hex(0x2C44A0), W.Centre, Vector2.zero,
                      new Vector2(ground, ground), size * 0.05f);

            // The candy stripe, tiled rather than stretched so its stripes and sprinkles keep their
            // drawn proportions along the full length of each side.
            Sprite frameSprite = ArtKit.BoardFrame();
            if (frameSprite != null)
            {
                Image frame = CandyUI.Icon("Frame", panel, frameSprite);
                frame.preserveAspect = false;
                frame.type = Image.Type.Tiled;
                RectTransform fr = frame.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = Vector2.zero;
                fr.offsetMax = Vector2.zero;
                frame.pixelsPerUnitMultiplier = Mathf.Max(0.05f, frameSprite.border.x / (size * BoardLayout.Stripe));
            }

            // The grid is pivoted top-left so cell (0,0) is the top-left cell, matching the engine.
            RectTransform grid = UIKit.Rect("Grid", panel);
            grid.anchorMin = grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            float inset = (size - gridExtent) * 0.5f;
            grid.anchoredPosition = new Vector2(inset, -inset);
            grid.sizeDelta = new Vector2(gridExtent, gridExtent);

            return grid;
        }

        /// <summary>The ghost lives inside the grid, so it is positioned with the same maths as the blocks.</summary>
        private static RectTransform BuildGhostLayer(RectTransform grid)
        {
            RectTransform ghost = UIKit.Rect("GhostLayer", grid);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0f, 1f);
            ghost.pivot = new Vector2(0f, 1f);
            ghost.anchoredPosition = Vector2.zero;
            ghost.sizeDelta = Vector2.zero;
            return ghost;
        }

        // --- canvas ----------------------------------------------------------------------------

        private static void ConfigureScreen()
        {
            // A standalone player pauses entirely when it loses focus, which hangs any capture run
            // launched from a script. Only enabled for the harnesses.
            if (StoreShots.StoreShotsRequested() || StoreShots.FeatureGraphicRequested() ||
                SmokeShots.RequestedOnCommandLine())
            {
                Application.runInBackground = true;
            }

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private static Canvas BuildCanvas()
        {
            var camGo = new GameObject("MainCamera", typeof(Camera));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.BackgroundBottom;
            cam.orthographic = true;

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);

            // Match on WIDTH. Matching on height shipped a board that ran off both sides of almost
            // every modern phone; matching on width pins 1080 units to the physical screen width, and
            // extra height on tall phones becomes vertical room.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }

        private static void BuildBackground(RectTransform parent)
        {
            Image bg = UIKit.Image("Background", parent, ArtKit.Background(), Color.white);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Canvas height in reference units: 1920 on 16:9, 2400 on 20:9, 1440 on 4:3.</summary>
        private static float CanvasHeightUnits() => RefWidth * Screen.height / Mathf.Max(1f, Screen.width);

        private static float UnitsPerPixel() => RefWidth / Mathf.Max(1f, Screen.width);

        /// <summary>Inset a rect to the display's safe area, so nothing lands under a notch or the gesture bar.</summary>
        private static void ApplySafeArea(RectTransform rt)
        {
            Rect safe = Screen.safeArea;
            rt.anchorMin = new Vector2(safe.xMin / Mathf.Max(1f, Screen.width), safe.yMin / Mathf.Max(1f, Screen.height));
            rt.anchorMax = new Vector2(safe.xMax / Mathf.Max(1f, Screen.width), safe.yMax / Mathf.Max(1f, Screen.height));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
