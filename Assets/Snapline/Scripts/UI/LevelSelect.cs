using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.App;
using Snapline.Core;

namespace Snapline.UI
{
    /// <summary>
    /// The level grid: every level, its star rating, and whether it is unlocked yet.
    ///
    /// Built once and refreshed on each show, rather than rebuilt, so returning from a level does
    /// not churn sixty buttons and their sprites.
    /// </summary>
    public sealed class LevelSelect : MonoBehaviour
    {
        private const int Columns = 5;
        private const float CellSize = 168f;
        private const float CellSpacing = 26f;

        private RectTransform _root;
        private RectTransform _content;
        private Text _summary;
        private Image _coinPill;
        private Text _coinLabel;

        private Button[] _buttons;
        private Text[] _numbers;
        private Image[][] _stars;
        private Image[] _panels;
        private Image[] _locks;
        private Image[] _play;

        public event Action<int> LevelChosen;
        public event Action BackRequested;
        public event Action ToolsRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("LevelSelect", parent);

            Image bg = UIKit.Image("Bg", _root, ArtKit.Background(), Color.white);
            RectTransform bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var top = new Vector2(0.5f, 1f);

            Button back = CandyUI.SpriteButton("Back", _root, ArtKit.Ui("circle_pink"));
            CandyUI.Place(back, top, new Vector2(-438f, -84f), new Vector2(116f, 116f));
            CandyUI.Place(CandyUI.Icon("Sym", back.transform, ArtKit.Ui("sym_back")),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            _coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            _coinPill.type = Image.Type.Sliced;
            _coinPill.preserveAspect = false;
            CandyUI.Place(_coinPill, top, new Vector2(70f, -84f), new Vector2(300f, 88f));
            CandyUI.Place(CandyUI.Icon("Coin", _coinPill.transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(66f, 66f));
            _coinLabel = CandyUI.Label("Coins", _coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(190f, 58f));

            Button toolbox = CandyUI.SpriteButton("Toolbox", _root, ArtKit.Ui("icon_toolbox"));
            CandyUI.Place(toolbox, top, new Vector2(400f, -84f), new Vector2(120f, 120f));
            toolbox.onClick.AddListener(() => ToolsRequested?.Invoke());

            CandyUI.Place(CandyUI.Label("Title", _root, "LEVELS", 108, CandyUI.Caption),
                          top, new Vector2(0f, -216f), new Vector2(900f, 124f));

            Image summaryPill = CandyUI.Icon("SummaryPill", _root, ArtKit.Ui("pill_blue"));
            summaryPill.type = Image.Type.Sliced;
            summaryPill.preserveAspect = false;
            CandyUI.Place(summaryPill, top, new Vector2(0f, -332f), new Vector2(760f, 86f));
            _summary = CandyUI.Label("Summary", summaryPill.transform, "", 36, CandyUI.Caption);
            CandyUI.Place(_summary, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(720f, 56f));

            Button backBottom = CandyUI.SpriteButton("BackBottom", _root, ArtKit.Ui("tile_pink"),
                                                     Image.Type.Sliced);
            CandyUI.Place(backBottom, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(700f, 152f));
            CandyUI.Place(CandyUI.Label("Caption", backBottom.transform, "BACK", 64, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 80f));
            backBottom.onClick.AddListener(() => BackRequested?.Invoke());

            BuildScrollingGrid();

            _root.gameObject.SetActive(false);
        }

        private void BuildScrollingGrid()
        {
            // Viewport: the window the grid scrolls behind. RectMask2D rather than Mask, because it
            // needs no material and therefore no extra draw call.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(_root, false);
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(30f, 226f);
            scrollRect.offsetMax = new Vector2(-30f, -392f);

            RectTransform viewport = UIKit.Stretch("Viewport", scrollRect);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UIKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = new Vector2(0f, 0f);
            _content.offsetMax = new Vector2(0f, 0f);

            int rows = Mathf.CeilToInt(Levels.Count / (float)Columns);
            float contentHeight = rows * (CellSize + CellSpacing) + CellSpacing;
            _content.sizeDelta = new Vector2(0f, contentHeight);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;

            BuildButtons();
        }

        private void BuildButtons()
        {
            _buttons = new Button[Levels.Count];
            _numbers = new Text[Levels.Count];
            _stars = new Image[Levels.Count][];
            _panels = new Image[Levels.Count];
            _locks = new Image[Levels.Count];
            _play = new Image[Levels.Count];

            float rowWidth = Columns * CellSize + (Columns - 1) * CellSpacing;
            float startX = -rowWidth * 0.5f + CellSize * 0.5f;

            for (int i = 0; i < Levels.Count; i++)
            {
                int number = i + 1;
                int col = i % Columns;
                int row = i / Columns;

                var go = new GameObject($"Level{number}", typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_content, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(CellSize, CellSize);
                rt.anchoredPosition = new Vector2(startX + col * (CellSize + CellSpacing),
                                                  -(CellSpacing + CellSize * 0.5f + row * (CellSize + CellSpacing)));

                var img = go.GetComponent<Image>();
                img.type = Image.Type.Sliced;
                _panels[i] = img;
                go.AddComponent<PressScale>();

                var button = go.GetComponent<Button>();
                button.targetGraphic = img;
                // The tiles carry their own lighting, so the usual colour tint muddies them; the
                // press is a squash instead, as everywhere else in this interface.
                button.transition = Selectable.Transition.None;
                int captured = number;
                button.onClick.AddListener(() => LevelChosen?.Invoke(captured));
                _buttons[i] = button;

                Text numberLabel = CandyUI.Label("N", rt, number.ToString(), 58, CandyUI.Caption);
                CandyUI.Place(numberLabel, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f),
                              new Vector2(CellSize, 64f));
                _numbers[i] = numberLabel;

                // Shown instead of the number on the level the player is up to, and instead of
                // nothing on the ones they cannot reach yet.
                _play[i] = CandyUI.Icon("Play", rt, ArtKit.Ui("icon_play"));
                CandyUI.Place(_play[i], new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(58f, 58f));
                _play[i].gameObject.SetActive(false);

                _locks[i] = CandyUI.Icon("Lock", rt, ArtKit.Ui("icon_lock"));
                CandyUI.Place(_locks[i], new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(48f, 48f));
                _locks[i].gameObject.SetActive(false);

                // Three small stars under the number. Same sprite as the results card, so they
                // batch together and mean the same thing in both places.
                _stars[i] = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    Image star = CandyUI.Icon("Star" + s, rt, ArtKit.Ui("icon_star"));
                    CandyUI.Place(star, new Vector2(0.5f, 0f), new Vector2((s - 1) * 40f, 34f),
                                  new Vector2(42f, 42f));
                    _stars[i][s] = star;
                }
            }
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Tile colours for cleared levels, cycled by level number.
        ///
        /// The reference cycles them rather than colouring by star count, so the grid reads as a
        /// sweet shop rather than as a report card. How well a level went is already on its tile,
        /// in stars.
        /// </summary>
        private static readonly string[] ClearedTiles = { "tile_green", "tile_cyan", "tile_purple" };

        public void Refresh()
        {
            int next = SaveSystem.HighestUnlockedLevel();

            for (int i = 0; i < Levels.Count; i++)
            {
                int number = i + 1;
                int stars = SaveSystem.StarsForLevel(number);
                bool open = SaveSystem.IsLevelUnlocked(number);
                bool cleared = stars > 0;
                bool current = open && !cleared && number == next;

                _buttons[i].interactable = open;

                _panels[i].sprite = ArtKit.Ui(
                    cleared ? ClearedTiles[i % ClearedTiles.Length] :
                    current ? "tile_pink" : "tile_navy");
                _panels[i].color = Color.white;

                // The level you are up to shows a play arrow instead of its number, as the reference
                // does — it is the one tile the player is meant to reach for.
                _play[i].gameObject.SetActive(current);
                _numbers[i].gameObject.SetActive(!current);
                _numbers[i].text = number.ToString();
                _numbers[i].color = CandyUI.Caption;

                _locks[i].gameObject.SetActive(!open);

                // Earned stars are shown and the rest hidden, rather than drawn as empty outlines.
                // A locked tile showing three blank stars reads as a level failed rather than as one
                // not yet reached.
                for (int s = 0; s < 3; s++)
                    _stars[i][s].gameObject.SetActive(cleared && s < stars);
            }

            _summary.text = SaveSystem.LevelsCompleted() + " / " + Levels.Count + " COMPLETE   \u2022   "
                          + SaveSystem.TotalStars() + " / " + (Levels.Count * 3) + " STARS";

            if (_coinLabel != null) _coinLabel.text = Hud.Format(App.Wallet.Coins);
        }

        /// <summary>Scroll so a given level is in view. Used after finishing one.</summary>
        public void ScrollTo(int level)
        {
            if (_content == null) return;

            int row = (Mathf.Clamp(level, 1, Levels.Count) - 1) / Columns;
            float target = row * (CellSize + CellSpacing) - 400f;

            // Measured from the real viewport rather than a hardcoded height, which was only right
            // on one aspect ratio and would have over-scrolled on anything taller.
            float viewportHeight = _content.parent is RectTransform vp ? vp.rect.height : 0f;
            float maxScroll = Mathf.Max(0f, _content.sizeDelta.y - viewportHeight);
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(target, 0f, maxScroll));
        }
    }
}
