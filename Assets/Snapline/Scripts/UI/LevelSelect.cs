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

        private Button[] _buttons;
        private Text[] _numbers;
        private Image[][] _stars;
        private Image[] _panels;

        public event Action<int> LevelChosen;
        public event Action BackRequested;

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

            // INTERIM: see CandyUI.Scrim. Must come after the background above, not before it.
            // Remove when this screen is restyled.
            CandyUI.Scrim(_root);

            Text title = UIKit.Label("Title", _root, "LEVELS", 76, Palette.TextBright);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -70f), new Vector2(700f, 90f));

            _summary = UIKit.Label("Summary", _root, "", 34, Palette.TextDim);
            UIKit.Place(_summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -158f), new Vector2(800f, 44f));

            Button back = UIKit.Button("Back", _root, "BACK", new Color(0.30f, 0.36f, 0.62f, 1f), Color.white, 40);
            UIKit.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(520f, 110f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

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
            scrollRect.offsetMin = new Vector2(30f, 190f);
            scrollRect.offsetMax = new Vector2(-30f, -200f);

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

                var button = go.GetComponent<Button>();
                button.targetGraphic = img;
                int captured = number;
                button.onClick.AddListener(() => LevelChosen?.Invoke(captured));
                _buttons[i] = button;

                Text numberLabel = UIKit.Label("N", rt, number.ToString(), 52, Color.white);
                UIKit.Place(numberLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(CellSize, 60f));
                _numbers[i] = numberLabel;

                // Three small stars under the number. Same sprite as the results card, so they
                // batch together and mean the same thing in both places.
                _stars[i] = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    Image star = UIKit.Image($"Star{s}", rt, ProcArt.Star(), Color.white);
                    UIKit.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                new Vector2(0.5f, 0f), new Vector2((s - 1) * 36f, 28f), new Vector2(30f, 30f));
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

        public void Refresh()
        {
            Sprite unlocked = ArtKit.RoundedRect("lvl_open", new Color(0.30f, 0.38f, 0.68f, 1f),
                                                 new Color(1f, 1f, 1f, 0.3f), 3f);
            Sprite cleared = ArtKit.RoundedRect("lvl_done", new Color(0.22f, 0.62f, 0.42f, 1f),
                                                new Color(1f, 1f, 1f, 0.35f), 3f);
            Sprite locked = ArtKit.RoundedRect("lvl_lock", new Color(0.17f, 0.20f, 0.34f, 1f),
                                               new Color(1f, 1f, 1f, 0.08f), 2f);

            for (int i = 0; i < Levels.Count; i++)
            {
                int number = i + 1;
                int stars = SaveSystem.StarsForLevel(number);
                bool open = SaveSystem.IsLevelUnlocked(number);

                _buttons[i].interactable = open;
                _panels[i].sprite = !open ? locked : stars > 0 ? cleared : unlocked;

                _numbers[i].text = number.ToString();
                _numbers[i].color = open ? Color.white : new Color(1f, 1f, 1f, 0.30f);

                // Earned stars are gold; the rest sit as faint outlines so the player can always
                // see there were three to get, not just how many they got.
                for (int s = 0; s < 3; s++)
                {
                    _stars[i][s].gameObject.SetActive(open);
                    _stars[i][s].color = s < stars ? Palette.Accent : new Color(1f, 1f, 1f, 0.13f);
                }
            }

            _summary.text = $"{SaveSystem.LevelsCompleted()} of {Levels.Count} complete    " +
                            $"{SaveSystem.TotalStars()} / {Levels.Count * 3} stars";
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
