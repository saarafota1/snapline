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
    /// tray, effects — is constructed here, which means the whole interface is reviewable as source,
    /// diffs cleanly, and cannot be broken by someone nudging a RectTransform in the inspector.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Bootstrap : MonoBehaviour
    {
        // Design resolution. Everything below is expressed in these units and scaled to the device.
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;

        private const float BoardMargin = 48f;
        private const float BoardGap = 10f;
        private const float BoardPadding = 18f;

        /// <summary>Breathing room above and below the board within its area.</summary>
        private const float BoardVerticalMargin = 24f;

        private const float HudHeight = 300f;
        private const float TrayHeight = 300f;
        private const float TrayBottomInset = 60f;

        // Sized so the tallest shape in the catalogue still fits inside the tray. The 5-cell bar
        // needs 5*(cell+gap) - gap <= TrayHeight minus a little padding; at cell 60 that came to
        // 324 against a 300-tall tray and the piece hung off the bottom of the screen.
        private const float TrayCellSize = 50f;
        private const float TrayGap = 5f;


        /// <summary>
        /// Services config. Assigned by SceneBuilder rather than loaded from Resources, so the asset
        /// stays where the kit puts it and the reference is visible in the scene.
        /// </summary>
        [SerializeField] private GameKit.GameKitConfig _gameKitConfig;


        /// <summary>Heights resolved at startup from the real screen shape.</summary>

        private float _hudHeight = HudHeight;

        private float _trayHeight = TrayHeight;

        private float _trayInset = TrayBottomInset;

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

            // The background is the only thing outside the safe area, so it bleeds behind a notch
            // rather than leaving a bar of nothing there.
            BuildBackground(canvasRect);

            RectTransform safeRoot = UIKit.Rect("SafeArea", canvasRect);
            ApplySafeArea(safeRoot);

            float canvasHeight = CanvasHeightUnits();
            Rect safe = Screen.safeArea;
            float unitsPerPixel = UnitsPerPixel();
            float safeWidth = safe.width * unitsPerPixel;
            float safeHeight = canvasHeight * (safe.height / Mathf.Max(1f, Screen.height));

            // Everything that should shake lives under here. The menu and the game-over panel
            // deliberately do not, so a big final clear cannot leave a card wobbling.
            RectTransform shakeRoot = UIKit.Stretch("ShakeRoot", safeRoot);

            // The whole game screen hangs off one rect that gets switched off for the menu.
            _gameRoot = UIKit.Stretch("GameRoot", shakeRoot);

            // Tall phones have height to spare once the board is capped by the screen width. Handing
            // it to the HUD and the tray beats leaving a big symmetric void around a small board,
            // and a taller tray means bigger, easier-to-grab pieces.
            float extra = Mathf.Max(0f, safeHeight - RefHeight);
            _hudHeight = HudHeight + extra * 0.30f;
            _trayHeight = TrayHeight + extra * 0.35f;
            _trayInset = TrayBottomInset + extra * 0.10f;

            float boardCell = ComputeBoardCellSize(safeWidth, safeHeight, _hudHeight, _trayHeight + _trayInset);
            float gridExtent = Board.Width * boardCell + (Board.Width - 1) * BoardGap;

            float slotWidth = safeWidth / 3f;
            float trayCell = ComputeTrayCellSize(_trayHeight, slotWidth);

            Debug.Log($"[Snapline] layout: screen {Screen.width}x{Screen.height}, " +
                      $"canvas {RefWidth:F0}x{canvasHeight:F0} units, safe {safeWidth:F0}x{safeHeight:F0}, " +
                      $"cell {boardCell:F0}, board {gridExtent + BoardPadding * 2f:F0}");

            RectTransform grid = BuildBoardPanel(_gameRoot, gridExtent);
            RectTransform ghostLayer = BuildGhostLayer(grid);
            RectTransform trayRoot = BuildTrayRoot(_gameRoot);
            RectTransform dragLayer = UIKit.Stretch("DragLayer", _gameRoot);

            // Effects sit above the board and the tray but below the results card.
            RectTransform effectLayer = UIKit.Stretch("EffectLayer", _gameRoot);
            var juice = effectLayer.gameObject.AddComponent<Juice>();
            RectTransform particleLayer = UIKit.Stretch("Particles", effectLayer);
            RectTransform popupLayer = UIKit.Stretch("Popups", effectLayer);
            juice.Init(particleLayer, popupLayer, shakeRoot);

            var boardView = grid.gameObject.AddComponent<BoardView>();
            boardView.Init(grid, ghostLayer, juice, boardCell, BoardGap);

            var trayView = trayRoot.gameObject.AddComponent<TrayView>();
            trayView.Init(trayRoot, 3, slotWidth, trayCell, TrayGap);

            var hud = new GameObject("HudController").AddComponent<Hud>();
            hud.transform.SetParent(_gameRoot, false);
            hud.Init(_gameRoot, SaveSystem.HighScore, _hudHeight);

            var gameOver = new GameObject("GameOverController").AddComponent<GameOverPanel>();
            gameOver.transform.SetParent(safeRoot, false);
            gameOver.Init(safeRoot);

            var drag = gameObject.AddComponent<DragController>();
            drag.Init(boardView, trayView, dragLayer, canvas, boardCell, BoardGap);

            _sfx = gameObject.AddComponent<Sfx>();
            _sfx.Init();
            _sfx.Muted = !Settings.SoundEnabled;

            _controller = gameObject.AddComponent<GameController>();
            var levelResult = new GameObject("LevelResult").AddComponent<LevelResultPanel>();
            levelResult.transform.SetParent(safeRoot, false);
            levelResult.Init(safeRoot);

            // Services come up in the background. Nothing waits on them: with no SDK installed the
            // kit hands back offline implementations and the game plays exactly the same.
            if (_gameKitConfig != null) _ = GameKit.GameKitRuntime.InitializeAsync(_gameKitConfig);

            var ads = gameObject.AddComponent<AdController>();
            ads.Init(_gameKitConfig);

            _controller.Init(boardView, trayView, drag, hud, gameOver, juice, _sfx, levelResult, ads);
            _controller.SetPopupBounds(safeHeight, _hudHeight, _trayHeight + _trayInset);

            _controller.MenuRequested += ShowMenu;
            _controller.LevelsRequested += ShowLevelSelect;

            _menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            _menu.transform.SetParent(safeRoot, false);
            _menu.Init(safeRoot);
            _menu.NewGameRequested += StartNewGame;
            _menu.ContinueRequested += ContinueGame;
            _menu.LevelsRequested += ShowLevelSelect;

            _menu.ScoresRequested += ShowScores;
            _menu.ShareRequested += () => GameKit.Share.Text(GameController.ShareMessage(SaveSystem.HighScore));
            _menu.SoundToggled += ToggleSound;

            _levelSelect = new GameObject("LevelSelect").AddComponent<LevelSelect>();
            _levelSelect.transform.SetParent(safeRoot, false);
            _levelSelect.Init(safeRoot);
            _levelSelect.LevelChosen += StartLevel;
            _levelSelect.BackRequested += ShowMenu;

            _scores = new GameObject("ScoresPanel").AddComponent<ScoresPanel>();
            _scores.transform.SetParent(safeRoot, false);
            _scores.Init(safeRoot);
            _scores.BackRequested += ShowMenu;
            _scores.ShareRequested += () => GameKit.Share.Text(GameController.ShareMessage(SaveSystem.HighScore));

            if (StoreShots.StoreShotsRequested())
            {
                drag.InputEnabled = false;
                ShowMenu();
                gameObject.AddComponent<StoreShots>().Begin(_controller, this);
                return;
            }

            if (SmokeShots.RequestedOnCommandLine())
            {
                // The harness captures the menu, then starts a game itself.
                drag.InputEnabled = false;
                ShowMenu();
                gameObject.AddComponent<SmokeShots>().Begin(_controller, drag, this);
                return;
            }

            ShowMenu();
        }

        private RectTransform _gameRoot;
        private GameController _controller;
        private MainMenu _menu;
        private LevelSelect _levelSelect;

        private ScoresPanel _scores;
        private Sfx _sfx;

        internal void ShowMenu()
        {
            _gameRoot.gameObject.SetActive(false);
            _controller.HideOverlays();
            _levelSelect.Hide();
            _scores.Hide();
            _menu.Show(SaveSystem.HighScore, GameController.HasSavedRun);
        }

        internal void ShowScores()
        {
            _gameRoot.gameObject.SetActive(false);
            _controller.HideOverlays();
            _menu.Hide();
            _levelSelect.Hide();
            _scores.Show();
        }



        internal void ShowLevelSelect()
        {
            _gameRoot.gameObject.SetActive(false);
            _controller.HideOverlays();
            _menu.Hide();
            _scores.Hide();
            _levelSelect.Show();
            _levelSelect.ScrollTo(SaveSystem.HighestUnlockedLevel());
        }

        internal void StartLevel(int number)
        {
            _menu.Hide();
            _levelSelect.Hide();
            _scores.Hide();
            _gameRoot.gameObject.SetActive(true);
            _controller.StartLevel(number);
        }

        internal void StartNewGame()
        {
            _menu.Hide();
            _levelSelect.Hide();
            _scores.Hide();
            _gameRoot.gameObject.SetActive(true);
            _controller.StartNewRun();
        }

        private void ContinueGame()
        {
            _menu.Hide();
            _levelSelect.Hide();
            _scores.Hide();
            _gameRoot.gameObject.SetActive(true);

            // The save can vanish between the menu being drawn and the tap — a crash, or the run
            // having already ended. Fall back to a fresh board rather than an empty screen.
            if (!_controller.ResumeSavedRun()) _controller.StartNewRun();
        }

        private void ToggleSound()
        {
            Settings.SoundEnabled = !Settings.SoundEnabled;
            _sfx.Muted = !Settings.SoundEnabled;
            _menu.RefreshSoundLabel();
        }

        private static void ConfigureScreen()
        {
            // A standalone player pauses entirely when it loses focus, which hangs any capture run
            // launched from a script. Only enabled for the harnesses; the shipped game keeps the
            // default so it never burns battery in the background.
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
            // A camera is created even though the canvas is in overlay mode: without one the scene
            // renders nothing behind the UI and the editor reports a missing main camera.
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

            // Match on WIDTH.
            //
            // Matching on height was wrong and shipped a board that ran off both sides of almost
            // every modern phone. Matching on height sets the scale from screenHeight/1920, so the
            // canvas is only screenWidth/scale reference units wide — on a 20:9 display that is
            // 1080 * 1920/2400 = 864 units, against a board panel 1018 wide. Taller screens make the
            // usable width *smaller*, not larger.
            //
            // Matching on width pins 1080 units to the physical screen width, so anything drawn
            // inside 1080 always fits horizontally. Extra height on tall phones becomes vertical
            // room, which the layout below distributes rather than assuming.
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

        /// <summary>
        /// Canvas height in reference units.
        ///
        /// With the scaler matching on width, 1080 units always map to the physical screen width, so
        /// the height in units follows the aspect ratio: 1920 on 16:9, 2400 on 20:9, 1440 on 4:3.
        /// The layout has to work from this rather than from RefHeight, which is only the value on
        /// one particular phone shape.
        /// </summary>
        private static float CanvasHeightUnits() =>
            RefWidth * Screen.height / Mathf.Max(1f, Screen.width);

        /// <summary>Reference units per screen pixel, for converting the safe area.</summary>
        private static float UnitsPerPixel() => RefWidth / Mathf.Max(1f, Screen.width);

        /// <summary>
        /// Inset a rect to the display's safe area, so nothing lands under a notch, a punch-hole or
        /// the gesture bar. Insets are zero on hardware without cutouts, so this costs nothing there.
        /// </summary>
        private static void ApplySafeArea(RectTransform rt)
        {
            Rect safe = Screen.safeArea;

            float left = safe.xMin / Mathf.Max(1f, Screen.width);
            float right = safe.xMax / Mathf.Max(1f, Screen.width);
            float bottom = safe.yMin / Mathf.Max(1f, Screen.height);
            float top = safe.yMax / Mathf.Max(1f, Screen.height);

            rt.anchorMin = new Vector2(left, bottom);
            rt.anchorMax = new Vector2(right, top);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Largest cell size that fits the board both across the screen and into the gap between the
        /// HUD and the tray.
        ///
        /// Taking the smaller of the two constraints is what stops the board running off the sides
        /// on a narrow-in-units display, and off the top and bottom on a short one like a tablet.
        /// </summary>
        private static float ComputeBoardCellSize(float safeWidth, float safeHeight, float hudHeight, float trayReserve)
        {
            float usableWidth = safeWidth - BoardMargin * 2f - BoardPadding * 2f;

            float usableHeight = safeHeight - hudHeight - trayReserve
                                 - BoardPadding * 2f - BoardVerticalMargin * 2f;

            float extent = Mathf.Min(usableWidth, usableHeight);
            float cell = Mathf.Floor((extent - (Board.Width - 1) * BoardGap) / Board.Width);

            // A floor, so a freakishly short window degrades to a small board rather than a
            // zero-sized or negative one.
            return Mathf.Max(24f, cell);
        }

        /// <summary>
        /// Tray cell size, bounded by both the tray's height and one slot's width.
        ///
        /// The tallest shape is five cells and so is the widest, so both constraints bite. Checking
        /// only the height is what previously let a 5-long bar hang off the bottom of the screen;
        /// checking only the width would let one run into its neighbouring slot.
        /// </summary>
        private static float ComputeTrayCellSize(float trayHeight, float slotWidth)
        {
            const int longest = 5;

            float byHeight = (trayHeight - TrayVerticalPadding - (longest - 1) * TrayGap) / longest;
            float byWidth = (slotWidth - TraySlotInset - (longest - 1) * TrayGap) / longest;

            return Mathf.Clamp(Mathf.Floor(Mathf.Min(byHeight, byWidth)), 24f, 74f);
        }

        /// <summary>Space kept clear above and below a tray piece.</summary>
        private const float TrayVerticalPadding = 44f;

        /// <summary>Gap between one tray slot's contents and the next.</summary>
        private const float TraySlotInset = 24f;

        private RectTransform BuildBoardPanel(RectTransform parent, float gridExtent)
        {
            // The board sits centred in whatever is left between the HUD and the tray. Anchoring the
            // container to both edges instead of positioning it from a hardcoded 1920-unit centre is
            // what lets the same layout hold on a 16:9 phone, a 20:9 phone and a 4:3 tablet.
            RectTransform area = UIKit.Rect("BoardArea", parent);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(0f, _trayHeight + _trayInset);
            area.offsetMax = new Vector2(0f, -_hudHeight);

            float panelSize = gridExtent + BoardPadding * 2f;

            RectTransform panel = UIKit.Rect("BoardPanel", area);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(panelSize, panelSize));

            Image bg = UIKit.Image("PanelBg", panel, ArtKit.Panel(), Color.white, Image.Type.Sliced);
            RectTransform bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // The grid is pivoted top-left so cell (0,0) is the top-left cell, matching the engine.
            RectTransform grid = UIKit.Rect("Grid", panel);
            grid.anchorMin = grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = new Vector2(BoardPadding, -BoardPadding);
            grid.sizeDelta = new Vector2(gridExtent, gridExtent);

            return grid;
        }

        /// <summary>
        /// The ghost lives inside the grid with a zero offset, so ghost cells can be positioned with
        /// the very same CellToLocal maths the blocks use.
        /// </summary>
        private static RectTransform BuildGhostLayer(RectTransform grid)
        {
            RectTransform ghost = UIKit.Rect("GhostLayer", grid);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0f, 1f);
            ghost.pivot = new Vector2(0f, 1f);
            ghost.anchoredPosition = Vector2.zero;
            ghost.sizeDelta = Vector2.zero;
            return ghost;
        }

        private RectTransform BuildTrayRoot(RectTransform parent)
        {
            RectTransform tray = UIKit.Rect("Tray", parent);
            tray.anchorMin = new Vector2(0f, 0f);
            tray.anchorMax = new Vector2(1f, 0f);
            tray.pivot = new Vector2(0.5f, 0f);
            tray.offsetMin = new Vector2(0f, _trayInset);
            tray.offsetMax = new Vector2(0f, _trayInset + _trayHeight);
            return tray;
        }
    }
}
