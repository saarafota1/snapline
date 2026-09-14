using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The level grid, from `levels.png`: twenty-five levels a page, swiped between, with page dots.
    ///
    /// A cleared level is a coloured candy tile with its stars; the level you are up to is the pink
    /// PLAY tile, breathing; the rest are navy with a padlock, and every tenth carries a chest.
    /// Built once and refreshed on each show, so returning from a level does not churn sixty tiles.
    /// </summary>
    public sealed class LevelSelect : MonoBehaviour
    {
        private static class Layout
        {
            public const int Columns = 5;
            public const int PerPage = 25;
            public const float Tile = 180f;
            public const float StepX = 194f;
            public const float StepY = 198f;
            public const float FirstRowY = 530f;
            public const float TitleY = 214f;
            public const float StatsY = 366f;
            public const float DotsY = 1478f;
            public const float BackFromBottom = 250f;
            public const float PageWidth = 1080f;
        }

        public event Action<int> LevelChosen;
        public event Action BackRequested;

        private RectTransform _root;
        private RectTransform _pages;
        private RectTransform _grid;
        private Text _title;
        private Text _complete;
        private Text _stars;
        private Image[] _dots;

        private Image[] _tiles;
        private Text[] _numbers;
        private Image[][] _starIcons;
        private Image[] _locks;
        private Image[] _play;
        private Text[] _playText;
        private Image[] _chests;

        private int _page;

        private static int PageCount => (Levels.Count + Layout.PerPage - 1) / Layout.PerPage;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("LevelSelect", parent);
            Vector2 top = W.Top;

            Button back = W.Round("Back", _root, "circle_pink", "sym_back", top, new Vector2(-420f, -106f), 124f, 0.5f);
            back.onClick.AddListener(() => BackRequested?.Invoke());

            CoinPill.Create(_root, top, new Vector2(214f, -92f), 290f, 88f);
            ToolboxButton.Create(_root, top, new Vector2(436f, -100f), 114f);

            _title = W.Title("Title", _root, "LEVELS", top, new Vector2(0f, -Layout.TitleY), 150);

            Image stats = W.Sliced("Stats", _root, "pill_blue", top, new Vector2(0f, -Layout.StatsY), new Vector2(790f, 96f));
            _complete = W.Text("Complete", stats.transform, "", W.Centre, new Vector2(-175f, 2f), new Vector2(360f, 70f),
                               38, CandyStyle.OnBlue, Color.white);
            W.Rounded("Divider", stats.transform, new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.55f), W.Centre,
                      new Vector2(22f, 0f), new Vector2(5f, 52f), 2f);
            W.Img("Star", stats.transform, "reward_star_gold", W.Centre, new Vector2(78f, 2f), new Vector2(62f, 62f));
            _stars = W.Text("Stars", stats.transform, "", W.Centre, new Vector2(240f, 2f), new Vector2(290f, 70f),
                            38, CandyStyle.OnBlue, Color.white);

            BuildPages();
            BuildDots();

            Button backBottom = W.Pill("BackBottom", _root, "pill_red", "BACK", W.Bottom,
                                       new Vector2(0f, Layout.BackFromBottom), new Vector2(700f, 164f), 96,
                                       CandyStyle.OnPink, sprinkles: true, shine: true);
            backBottom.onClick.AddListener(() => BackRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        private void BuildPages()
        {
            _grid = UIKit.Rect("Grid", _root);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.offsetMin = new Vector2(0f, -Layout.DotsY + 60f);
            _grid.offsetMax = new Vector2(0f, -Layout.StatsY - 60f);

            // The swipe surface. Transparent, but it has to be a raycast target, or a drag that starts
            // between two tiles would go nowhere.
            Image surface = UIKit.Image("Swipe", _grid, ProcArt.Solid(), new Color(1f, 1f, 1f, 0f));
            RectTransform sr = surface.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
            surface.gameObject.AddComponent<RectMask2D>();
            PageSwipe swipe = surface.gameObject.AddComponent<PageSwipe>();
            swipe.Owner = this;

            _pages = UIKit.Rect("Pages", sr);
            _pages.anchorMin = _pages.anchorMax = new Vector2(0.5f, 1f);
            _pages.pivot = new Vector2(0.5f, 1f);
            _pages.sizeDelta = new Vector2(Layout.PageWidth, 10f);

            int n = Levels.Count;
            _tiles = new Image[n];
            _numbers = new Text[n];
            _starIcons = new Image[n][];
            _locks = new Image[n];
            _play = new Image[n];
            _playText = new Text[n];
            _chests = new Image[n];

            float gridTop = Layout.StatsY + 60f;

            for (int i = 0; i < n; i++)
            {
                int page = i / Layout.PerPage;
                int slot = i % Layout.PerPage;
                int col = slot % Layout.Columns;
                int row = slot / Layout.Columns;
                int number = i + 1;

                float x = page * Layout.PageWidth + (col - 2) * Layout.StepX;
                float y = -(Layout.FirstRowY - gridTop) - row * Layout.StepY;

                Button b = CandyUI.SpriteButton($"Level{number}", _pages, ArtKit.Ui("tile_navy"));
                CandyUI.Place(b, W.Top, new Vector2(x, y), new Vector2(Layout.Tile, Layout.Tile));
                Image img = b.GetComponent<Image>();
                img.preserveAspect = true;
                _tiles[i] = img;
                int captured = number;
                b.onClick.AddListener(() => Choose(captured));

                _numbers[i] = W.Text("N", b.transform, number.ToString(), W.Centre, new Vector2(0f, 30f),
                                     new Vector2(Layout.Tile, 90f), 70, CandyStyle.OnBlue, Color.white);
                _numbers[i].font = Design.Display;

                _starIcons[i] = new Image[3];
                for (int s = 0; s < 3; s++)
                    _starIcons[i][s] = W.Img("S" + s, b.transform, "reward_star_gold", W.Centre,
                                             new Vector2((s - 1) * 46f, -44f - (s == 1 ? 4f : 0f)), new Vector2(54f, 54f));

                _locks[i] = W.Img("Lock", b.transform, "icon_lock", W.Centre, new Vector2(0f, -38f), new Vector2(58f, 62f));

                _play[i] = W.Img("Play", b.transform, "icon_play", W.Centre, new Vector2(4f, 22f), new Vector2(76f, 82f));
                _playText[i] = W.Text("PlayText", b.transform, "PLAY", W.Centre, new Vector2(0f, -50f),
                                      new Vector2(Layout.Tile, 50f), 40, CandyStyle.OnPink, Color.white);
                _playText[i].font = Design.Display;

                _chests[i] = W.Img("Chest", b.transform, "icon_chest", W.Top, new Vector2(0f, -2f), new Vector2(84f, 76f));
            }
        }

        private void BuildDots()
        {
            _dots = new Image[PageCount];
            for (int p = 0; p < PageCount; p++)
            {
                _dots[p] = W.Img("Dot" + p, _root, "dot_pink", W.Top,
                                 new Vector2((p - (PageCount - 1) * 0.5f) * 58f, -Layout.DotsY), new Vector2(40f, 40f));
                int captured = p;
                Button b = _dots[p].gameObject.AddComponent<Button>();
                _dots[p].raycastTarget = true;
                b.onClick.AddListener(() => GoToPage(captured, true));
            }
        }

        private void Choose(int number)
        {
            if (!SaveSystem.IsLevelUnlocked(number))
            {
                Sound.Deny();
                Tween.Shake((RectTransform)_tiles[number - 1].transform, 14f, 0.35f);
                return;
            }

            Fx.Instance?.Sparkles(_tiles[number - 1].transform.position, 10, 90f, 70f);
            LevelChosen?.Invoke(number);
        }

        /// <summary>Tile colours for cleared levels, cycled by position as the reference does.</summary>
        private static readonly string[] ClearedTiles = { "tile_green", "tile_cyan", "tile_purple" };
        private static readonly CandyStyle[] ClearedStyles = { CandyStyle.OnGreen, CandyStyle.OnBlue, CandyStyle.OnPurple };

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
            GoToPage((SaveSystem.HighestUnlockedLevel() - 1) / Layout.PerPage, false);

            Tween.PopIn(_title.transform, 0f, 0.5f, 0.3f);
            Cascade();
            Sound.Swoosh();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

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

                string tile = cleared ? ClearedTiles[i % ClearedTiles.Length] : current ? "tile_pink" : "tile_navy";
                _tiles[i].sprite = ArtKit.Ui(tile);

                _numbers[i].gameObject.SetActive(!current);
                CandyText candy = _numbers[i].GetComponent<CandyText>();
                candy.Set(cleared ? ClearedStyles[i % ClearedStyles.Length] : CandyStyle.OnBlue);
                _numbers[i].rectTransform.anchoredPosition = new Vector2(0f, cleared ? 30f : 32f);

                for (int s = 0; s < 3; s++)
                {
                    _starIcons[i][s].gameObject.SetActive(cleared);
                    _starIcons[i][s].sprite = ArtKit.Ui(s < stars ? "reward_star_gold" : "reward_star_silver");
                }

                _locks[i].gameObject.SetActive(!open);
                _play[i].gameObject.SetActive(current);
                _playText[i].gameObject.SetActive(current);
                _chests[i].gameObject.SetActive(!cleared && number % 10 == 0);

                CandyPress press = _tiles[i].GetComponent<CandyPress>();
                if (press != null) press.Pulse = current ? 0.045f : 0f;

                Glint glint = _tiles[i].GetComponent<Glint>();
                if (current && glint == null) glint = _tiles[i].gameObject.AddComponent<Glint>();
                if (glint != null) glint.enabled = current;
            }

            _complete.text = $"{SaveSystem.LevelsCompleted()} / {Levels.Count} COMPLETE";
            _stars.text = $"{SaveSystem.TotalStars()} / {Levels.Count * 3} STARS";
        }

        private void Cascade()
        {
            int first = _page * Layout.PerPage;
            for (int i = first; i < Mathf.Min(first + Layout.PerPage, Levels.Count); i++)
            {
                int slot = i - first;
                Tween.PopIn(_tiles[i].transform, 0.05f + (slot / Layout.Columns) * 0.05f + (slot % Layout.Columns) * 0.025f,
                            0.4f, 0.2f);
            }
        }

        // --- paging --------------------------------------------------------------------------------

        private float _dragStart;

        internal void BeginSwipe() => _dragStart = _pages.anchoredPosition.x;

        internal void Swipe(float delta)
        {
            float x = _dragStart + delta;
            float min = -(PageCount - 1) * Layout.PageWidth;
            // Resist past either end, rather than stopping dead.
            if (x > 0f) x *= 0.35f;
            if (x < min) x = min + (x - min) * 0.35f;
            _pages.anchoredPosition = new Vector2(x, 0f);
        }

        internal void EndSwipe(float delta)
        {
            int target = _page;
            if (delta < -110f) target++;
            if (delta > 110f) target--;
            GoToPage(Mathf.Clamp(target, 0, PageCount - 1), true);
        }

        private void GoToPage(int page, bool animate)
        {
            bool changed = page != _page;
            _page = page;

            float to = -page * Layout.PageWidth;
            if (animate)
            {
                float from = _pages.anchoredPosition.x;
                Tween.Run(_pages, 0.38f, k => _pages.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, Ease.OutBack(k, 1.1f)), 0f));
                if (changed)
                {
                    Sound.Swoosh();
                    Cascade();
                }
            }
            else
            {
                _pages.anchoredPosition = new Vector2(to, 0f);
            }

            for (int p = 0; p < _dots.Length; p++)
            {
                bool on = p == page;
                _dots[p].rectTransform.sizeDelta = on ? new Vector2(50f, 50f) : new Vector2(36f, 36f);
                _dots[p].color = on ? Color.white : new Color(0.78f, 0.8f, 0.95f, 1f);
            }
        }
    }

    /// <summary>Turns a horizontal drag over the level grid into paging.</summary>
    public sealed class PageSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public LevelSelect Owner;
        private float _startX;
        private float _scale = 1f;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _startX = eventData.position.x;
            Canvas canvas = GetComponentInParent<Canvas>();
            _scale = canvas != null ? canvas.scaleFactor : 1f;
            Owner?.BeginSwipe();
        }

        public void OnDrag(PointerEventData eventData) =>
            Owner?.Swipe((eventData.position.x - _startX) / Mathf.Max(0.01f, _scale));

        public void OnEndDrag(PointerEventData eventData) =>
            Owner?.EndSwipe((eventData.position.x - _startX) / Mathf.Max(0.01f, _scale));
    }
}
