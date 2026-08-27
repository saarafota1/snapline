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

        private const float HudHeight = 300f;
        private const float TrayHeight = 300f;
        private const float TrayBottomInset = 60f;

        // Sized so the tallest shape in the catalogue still fits inside the tray. The 5-cell bar
        // needs 5*(cell+gap) - gap <= TrayHeight minus a little padding; at cell 60 that came to
        // 324 against a 300-tall tray and the piece hung off the bottom of the screen.
        private const float TrayCellSize = 50f;
        private const float TrayGap = 5f;

        private void Awake()
        {
            ConfigureScreen();

            Canvas canvas = BuildCanvas();
            EnsureEventSystem();

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            BuildBackground(canvasRect);

            // Everything that should shake lives under here. The game-over panel deliberately does
            // not, so a big final clear cannot leave the results card wobbling.
            RectTransform shakeRoot = UIKit.Stretch("ShakeRoot", canvasRect);

            float boardCell = ComputeBoardCellSize();
            float gridExtent = Board.Width * boardCell + (Board.Width - 1) * BoardGap;

            RectTransform grid = BuildBoardPanel(shakeRoot, gridExtent);
            RectTransform ghostLayer = BuildGhostLayer(grid);
            RectTransform trayRoot = BuildTrayRoot(shakeRoot);
            RectTransform dragLayer = UIKit.Stretch("DragLayer", shakeRoot);

            // Effects sit above the board and the tray but below the results card.
            RectTransform effectLayer = UIKit.Stretch("EffectLayer", shakeRoot);
            var juice = effectLayer.gameObject.AddComponent<Juice>();
            RectTransform particleLayer = UIKit.Stretch("Particles", effectLayer);
            RectTransform popupLayer = UIKit.Stretch("Popups", effectLayer);
            juice.Init(particleLayer, popupLayer, shakeRoot);

            var boardView = grid.gameObject.AddComponent<BoardView>();
            boardView.Init(grid, ghostLayer, juice, boardCell, BoardGap);

            var trayView = trayRoot.gameObject.AddComponent<TrayView>();
            trayView.Init(trayRoot, 3, RefWidth / 3f, TrayCellSize, TrayGap);

            var hud = new GameObject("HudController").AddComponent<Hud>();
            hud.transform.SetParent(shakeRoot, false);
            hud.Init(shakeRoot, SaveSystem.HighScore);

            var gameOver = new GameObject("GameOverController").AddComponent<GameOverPanel>();
            gameOver.transform.SetParent(canvasRect, false);
            gameOver.Init(canvasRect);

            var drag = gameObject.AddComponent<DragController>();
            drag.Init(boardView, trayView, dragLayer, canvas, boardCell, BoardGap);

            var sfx = gameObject.AddComponent<Sfx>();
            sfx.Init();

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(boardView, trayView, drag, hud, gameOver, juice, sfx);
            controller.Begin();

            if (SmokeShots.RequestedOnCommandLine())
            {
                drag.InputEnabled = false;
                gameObject.AddComponent<SmokeShots>().Begin(controller, drag);
            }
        }

        private static void ConfigureScreen()
        {
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

            // Match on height so the board keeps its size on tall phones and the extra width
            // becomes margin, rather than the grid growing until it collides with the tray.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

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

        /// <summary>Largest cell size that fits eight cells and seven gaps inside the margins.</summary>
        private static float ComputeBoardCellSize()
        {
            float usable = RefWidth - BoardMargin * 2f - BoardPadding * 2f;
            return Mathf.Floor((usable - (Board.Width - 1) * BoardGap) / Board.Width);
        }

        private static RectTransform BuildBoardPanel(RectTransform parent, float gridExtent)
        {
            // Vertical centre of the space left between the HUD and the tray.
            float available = RefHeight - HudHeight - (TrayHeight + TrayBottomInset);
            float centreFromBottom = TrayHeight + TrayBottomInset + available * 0.5f;
            float offsetFromCentre = centreFromBottom - RefHeight * 0.5f;

            float panelSize = gridExtent + BoardPadding * 2f;

            RectTransform panel = UIKit.Rect("BoardPanel", parent);
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, offsetFromCentre), new Vector2(panelSize, panelSize));

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

        private static RectTransform BuildTrayRoot(RectTransform parent)
        {
            RectTransform tray = UIKit.Rect("Tray", parent);
            UIKit.Place(tray, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, TrayBottomInset), new Vector2(RefWidth, TrayHeight));
            return tray;
        }
    }
}
