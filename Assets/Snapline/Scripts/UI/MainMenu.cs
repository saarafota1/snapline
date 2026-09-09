using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;

namespace Snapline.UI
{
    /// <summary>
    /// The front screen.
    ///
    /// Laid out against the candy reference art: a status bar, the wordmark, a decorative board, the
    /// way back into a run, the two modes, the daily challenge, and a row of round buttons.
    ///
    /// Three of those — coins, the tools store and the daily challenge — are **presentation only**.
    /// Nothing behind them exists yet: there is no currency, no inventory and no date tracking. They
    /// are built now because the layout has to be judged as a whole, and each is wired to a single
    /// obvious place to plug the real system in later. Every one of them is marked PLACEHOLDER.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        // Design space. The reference art is 941x1672, which is this shape.
        private const float RefWidth = 1080f;

        private RectTransform _root;

        private Text _coinLabel;
        private Text _continueCaption;
        private Text _continueDetail;
        private Text _levelsDetail;
        private Text _endlessDetail;
        private Text _dailyDetail;
        private Image _soundIcon;

        private Button _continue;
        private Button _privacy;
        private RectTransform _logo;

        private RectTransform[] _decor;
        private float[] _decorPhase;
        private Vector2[] _decorHome;

        public event Action ContinueRequested;
        public event Action NewGameRequested;
        public event Action LevelsRequested;
        public event Action ScoresRequested;
        public event Action ShareRequested;
        public event Action SoundToggled;
        public event Action PrivacyRequested;

        /// <summary>Reopens the consent form. Only ever raised where a form actually exists.</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void SetPrivacyAvailable(bool available)
        {
            if (_privacy != null) _privacy.gameObject.SetActive(available);
        }

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("MainMenu", parent);

            BuildDecor();
            BuildStatusBar();
            BuildLogo();
            BuildBoardPreview();
            BuildPrimary();
            BuildModes();
            BuildDaily();
            BuildBottomRow();

            // Quiet, and hidden outside the regions that require it. Google asks for a persistent way
            // back into the consent form, but a player in a region with no form must not be shown a
            // button that opens nothing.
            _privacy = CandyUI.SpriteButton("Privacy", _root, null, "PRIVACY", 26, CandyUI.CaptionDim);
            CandyUI.Place(_privacy, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(300f, 50f));
            _privacy.onClick.AddListener(() => PrivacyRequested?.Invoke());
            _privacy.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        // --- status bar ------------------------------------------------------------------------

        /// <summary>
        /// Settings, the coin balance, the tools store and the sound toggle, along the top.
        /// </summary>
        private void BuildStatusBar()
        {
            var top = new Vector2(0.5f, 1f);

            Button gear = CandyUI.SpriteButton("Settings", _root, ArtKit.Ui("btn_gear"));
            CandyUI.Place(gear, top, new Vector2(-438f, -78f), new Vector2(116f, 116f));
            // PLACEHOLDER: there is no settings screen. Sound lives on its own button for now, so
            // this opens nothing until there is something to open.
            gear.onClick.AddListener(() => Debug.Log("[Snapline] settings: not built yet"));

            // --- coin balance. PLACEHOLDER: no currency exists; the number is not persisted. ---
            Image coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            coinPill.type = Image.Type.Sliced;
            coinPill.preserveAspect = false;
            CandyUI.Place(coinPill, top, new Vector2(-78f, -78f), new Vector2(300f, 88f));

            CandyUI.Place(CandyUI.Icon("CoinIcon", coinPill.transform, ArtKit.Ui("coin_s")),
                          new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(68f, 68f));

            _coinLabel = CandyUI.Label("Coins", coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f), new Vector2(200f, 60f));

            Button plus = CandyUI.SpriteButton("CoinPlus", coinPill.transform, ArtKit.Ui("btn_plus"));
            CandyUI.Place(plus, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(78f, 78f));
            plus.onClick.AddListener(OpenTools);

            // --- tools store. PLACEHOLDER: no store, no inventory, no items. ---
            Button tools = CandyUI.SpriteButton("Tools", _root, ArtKit.Ui("icon_toolbox"));
            CandyUI.Place(tools, top, new Vector2(170f, -76f), new Vector2(120f, 120f));
            tools.onClick.AddListener(OpenTools);

            Image badge = CandyUI.Icon("ToolsBadge", tools.transform, ArtKit.Ui("dot_red"));
            CandyUI.Place(badge, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(52f, 52f));
            CandyUI.Place(CandyUI.Label("BadgeCount", badge.transform, "0", 30, CandyUI.Caption, outline: false),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 40f));
            badge.gameObject.SetActive(false);

            Button sound = CandyUI.SpriteButton("Sound", _root, ArtKit.Ui("btn_sound"));
            CandyUI.Place(sound, top, new Vector2(438f, -78f), new Vector2(116f, 116f));
            sound.onClick.AddListener(() => SoundToggled?.Invoke());
            _soundIcon = sound.GetComponent<Image>();
        }

        private void BuildLogo()
        {
            Sprite logo = ArtKit.Logo();

            if (logo != null)
            {
                Image img = CandyUI.Icon("Logo", _root, logo);
                _logo = img.rectTransform;
                // Height follows the art's own aspect, so a re-exported wordmark at a different
                // shape is not stretched to fit a hard-coded box.
                float height = 860f * (logo.rect.height / Mathf.Max(1f, logo.rect.width));
                CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(860f, height));
                return;
            }

            Text title = CandyUI.Label("Title", _root, "SNAPLINE", 132, CandyUI.Caption);
            _logo = title.rectTransform;
            CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 160f));
        }

        /// <summary>
        /// A still board behind the buttons, framed in candy stripes.
        ///
        /// Decorative — it is not the real board and never updates. It exists because the reference
        /// art leads with it, and because a front screen for a block puzzle that shows no blocks
        /// gives no sense of the game.
        /// </summary>
        private void BuildBoardPreview()
        {
            const float size = 664f;
            const int grid = 8;

            RectTransform frame = UIKit.Rect("BoardPreview", _root);
            CandyUI.Place(frame, new Vector2(0.5f, 1f), new Vector2(0f, -684f), new Vector2(size, size));

            // The frame's centre is transparent, and the empty-cell sprites do not quite meet at
            // their corners, so without a solid ground behind them the candy background shows
            // through the grid as pale speckle.
            Image ground = CandyUI.Icon("Ground", frame, ArtKit.Ui("tile_navy"));
            ground.type = Image.Type.Sliced;
            ground.preserveAspect = false;
            ground.color = new Color(0.40f, 0.46f, 0.76f, 1f);
            CandyUI.Place(ground, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 112f, size - 112f));

            Image border = CandyUI.Icon("Frame", frame, ArtKit.BoardFrame());
            // Tiled, not Sliced. Both keep the border at its drawn thickness, but Sliced *stretches*
            // the middle of each edge — and this border is diagonal candy stripes, which smear into
            // flat white when stretched across two-thirds of a side. Tiling repeats them instead, so
            // the stripe density stays the same however wide the board gets.
            border.type = Image.Type.Tiled;
            border.preserveAspect = false;
            RectTransform brt = border.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            // Clear of the candy border, which nine-slicing keeps at its drawn thickness however
            // large the frame gets. An inset narrower than the border puts the outer blocks
            // underneath it.
            const float inset = 60f;
            float cell = (size - inset * 2f) / grid;

            // A fixed arrangement, not a random one: the front screen should look the same every
            // time it is opened rather than reshuffling behind the player. Row 6 is deliberately a
            // complete line — the reference art leads with a clear in progress, and a board that is
            // one move from scoring says more about the game than an arbitrary scatter.
            const string pattern =
                "302....." +
                "112.4..." +
                "...1.2.." +
                "0010...." +
                "22......" +
                "22222222" +
                "05341122" +
                "........";

            const int clearingRow = 5;

            for (int row = 0; row < grid; row++)
            for (int col = 0; col < grid; col++)
            {
                char c = pattern[row * grid + col];
                bool filled = c != '.';
                bool clearing = row == clearingRow;

                // The clearing row is drawn in the sunflower colour rather than its own, which is
                // what a line about to pop looks like in play.
                Sprite sprite = filled ? ArtKit.Block(clearing ? 2 : c - '0') : ArtKit.EmptyCell();

                Image cellImg = CandyUI.Icon($"C{row}_{col}", frame, sprite);
                cellImg.preserveAspect = false;

                // The empty-cell sprite is a fully lit blue block, so at full brightness a mostly
                // empty board reads as a completely full one — every hole looks like a piece. Darkened
                // until the holes sit behind the blocks the way they do in the reference art.
                if (!filled) cellImg.color = Palette.EmptyCellTint;

                CandyUI.Place(cellImg, new Vector2(0f, 1f),
                              new Vector2(inset + cell * (col + 0.5f), -(inset + cell * (row + 0.5f))),
                              new Vector2(cell - 5f, cell - 5f));
            }

            // The delivered line-clear effect. Earlier versions had the transparency checkerboard
            // blended into the faint halo, where the colour drifted from gold to grey-tan as the
            // alpha fell; this one holds a constant saturated #FFAE16 down to alpha 8, which is what
            // an uncontaminated glow looks like.
            Image flare = CandyUI.Icon("ClearFlare", frame, ArtLoader.Sprite("FX/fx_line_clear"));
            flare.preserveAspect = false;
            CandyUI.Place(flare, new Vector2(0f, 1f),
                          new Vector2(size * 0.5f, -(inset + cell * (clearingRow + 0.5f))),
                          new Vector2(size - inset * 0.5f, cell * 2.4f));
        }

        // --- the way into a run ----------------------------------------------------------------

        private void BuildPrimary()
        {
            var top = new Vector2(0.5f, 1f);

            _continue = CandyUI.SpriteButton("Continue", _root, ArtKit.Ui("tile_pink"), Image.Type.Sliced);
            CandyUI.Place(_continue, top, new Vector2(0f, -1108f), new Vector2(880f, 162f));
            _continue.onClick.AddListener(OnPrimary);

            _continueCaption = CandyUI.Label("Caption", _continue.transform, "PLAY", 68, CandyUI.Caption);
            CandyUI.Place(_continueCaption, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(840f, 78f));

            _continueDetail = CandyUI.Label("Detail", _continue.transform, "", 36, CandyUI.CaptionDim);
            CandyUI.Place(_continueDetail, new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(840f, 44f));
        }

        private void BuildModes()
        {
            var top = new Vector2(0.5f, 1f);
            var size = new Vector2(448f, 148f);

            Button levels = CandyUI.SpriteButton("Levels", _root, ArtKit.Ui("tile_purple"), Image.Type.Sliced);
            CandyUI.Place(levels, top, new Vector2(-232f, -1288f), size);
            levels.onClick.AddListener(() => LevelsRequested?.Invoke());
            BuildModeFace(levels, ArtKit.Ui("icon_levels"), "60 LEVELS", out _levelsDetail);

            Button endless = CandyUI.SpriteButton("Endless", _root, ArtKit.Ui("tile_cyan"), Image.Type.Sliced);
            CandyUI.Place(endless, top, new Vector2(232f, -1288f), size);
            // Starts a fresh endless run. When one is already saved, CONTINUE above resumes it and
            // this replaces it — the same pair the old NEW GAME button provided.
            endless.onClick.AddListener(() => NewGameRequested?.Invoke());
            BuildModeFace(endless, ArtKit.Ui("icon_infinity"), "ENDLESS", out _endlessDetail);
        }

        /// <summary>Icon, title and a small detail line — the shared face of both mode buttons.</summary>
        private static void BuildModeFace(Button button, Sprite icon, string title, out Text detail)
        {
            CandyUI.Place(CandyUI.Icon("Icon", button.transform, icon),
                          new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(78f, 78f));

            Text label = CandyUI.Label("Title", button.transform, title, 40, CandyUI.Caption, TextAnchor.MiddleLeft);
            CandyUI.Place(label, new Vector2(0f, 0.5f), new Vector2(272f, 20f), new Vector2(280f, 52f));

            detail = CandyUI.Label("Detail", button.transform, "", 32, CandyUI.CaptionDim, TextAnchor.MiddleLeft);
            CandyUI.Place(detail, new Vector2(0f, 0.5f), new Vector2(272f, -26f), new Vector2(280f, 42f));

            CandyUI.Place(CandyUI.Icon("Chevron", button.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(36f, 58f));
        }

        /// <summary>
        /// The daily challenge strip.
        ///
        /// PLACEHOLDER throughout. There is no date tracking, no daily seed and no streak, so the
        /// objective is fixed text and every day dot is drawn unclaimed. `RefreshDaily` is the one
        /// place that has to change when the real thing exists.
        /// </summary>
        private void BuildDaily()
        {
            var top = new Vector2(0.5f, 1f);
            const float panelW = 950f;
            const float panelH = 258f;

            Button daily = CandyUI.SpriteButton("Daily", _root, ArtKit.Ui("tile_yellow"), Image.Type.Sliced);
            CandyUI.Place(daily, top, new Vector2(0f, -1494f), new Vector2(panelW, panelH));
            daily.onClick.AddListener(() => Debug.Log("[Snapline] daily challenge: not built yet"));

            // The calendar sits in its own column on the left, separated by a rule, so the panel
            // reads as "this thing, about these days" rather than as four unrelated items in a row.
            CandyUI.Place(CandyUI.Icon("Calendar", daily.transform, ArtKit.Ui("icon_calendar")),
                          new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(118f, 118f));

            Image rule = CandyUI.Icon("Rule", daily.transform, ArtKit.Ui("tile_navy"));
            rule.type = Image.Type.Sliced;
            rule.preserveAspect = false;
            rule.color = new Color(0.55f, 0.36f, 0.10f, 0.28f);
            CandyUI.Place(rule, new Vector2(0f, 0.5f), new Vector2(172f, 0f), new Vector2(4f, 176f));

            const float contentLeft = 200f;

            Text title = CandyUI.Label("Title", daily.transform, "DAILY CHALLENGE", 44,
                                       CandyUI.CaptionOnYellow, TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(title, new Vector2(0f, 1f), new Vector2(contentLeft + 230f, -40f),
                          new Vector2(480f, 46f));

            _dailyDetail = CandyUI.Label("Detail", daily.transform, "Clear 8 lines", 34,
                                         CandyUI.CaptionOnYellow, TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(_dailyDetail, new Vector2(0f, 1f), new Vector2(contentLeft + 168f, -80f),
                          new Vector2(340f, 38f));

            // Reward badge, top right.
            Image reward = CandyUI.Icon("Reward", daily.transform, ArtKit.Ui("pill_gold"));
            reward.type = Image.Type.Sliced;
            reward.preserveAspect = false;
            CandyUI.Place(reward, new Vector2(1f, 1f), new Vector2(-118f, -46f), new Vector2(180f, 66f));
            CandyUI.Place(CandyUI.Icon("RewardCoin", reward.transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(56f, 56f));
            CandyUI.Place(CandyUI.Label("RewardValue", reward.transform, "+50", 38, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(118f, 44f));

            CandyUI.Place(CandyUI.Icon("Chevron", daily.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(36f, 58f));

            // The week strip sits under the text, inside the content column, not across the whole
            // panel — the calendar column and the chevron are not days.
            string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            const float stripLeft = contentLeft + 20f;
            const float stripRight = panelW - 116f;
            float step = (stripRight - stripLeft) / (days.Length - 1);

            for (int i = 0; i < days.Length; i++)
            {
                float x = stripLeft + step * i;

                // PLACEHOLDER: no date tracking, so day one is drawn as today and none are claimed.
                bool today = i == 0;

                Image dot = CandyUI.Icon($"Day{i}", daily.transform, ArtKit.Ui("dot_pink"));
                CandyUI.Place(dot, new Vector2(0f, 0f), new Vector2(x, 112f), new Vector2(60f, 60f));
                if (today) dot.color = new Color(1f, 0.86f, 0.30f, 1f);

                CandyUI.Place(CandyUI.Label($"DayName{i}", daily.transform, days[i], 28,
                                            CandyUI.CaptionOnYellow, TextAnchor.MiddleCenter, outline: false),
                              new Vector2(0f, 0f), new Vector2(x, 64f), new Vector2(110f, 34f));
            }
        }
        private void BuildBottomRow()
        {
            var top = new Vector2(0.5f, 1f);
            const float y = -1742f;
            const float disc = 142f;

            // A disc behind each icon, rather than a bare icon. The reference reads as three buttons
            // because of the discs; without them the icons float and the row stops looking pressable.
            Button best = MakeDiscButton("Best", "circle_gold", "icon_trophy", "BEST", disc);
            CandyUI.Place(best, top, new Vector2(-286f, y), new Vector2(disc, disc));
            best.onClick.AddListener(() => ScoresRequested?.Invoke());

            Button share = MakeDiscButton("Share", "circle_pink", "icon_share", "SHARE", disc);
            CandyUI.Place(share, top, new Vector2(0f, y), new Vector2(disc, disc));
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button tools = MakeDiscButton("ToolsBig", "circle_blue", "icon_toolbox", "TOOLS", disc);
            CandyUI.Place(tools, top, new Vector2(286f, y), new Vector2(disc, disc));
            tools.onClick.AddListener(OpenTools);
        }

        /// <summary>A coloured disc with an icon on it and a caption beneath.</summary>
        private Button MakeDiscButton(string name, string disc, string icon, string caption, float size)
        {
            Button button = CandyUI.SpriteButton(name, _root, ArtKit.Ui(disc));

            CandyUI.Place(CandyUI.Icon("Icon", button.transform, ArtKit.Ui(icon)),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.66f, size * 0.66f));

            CandyUI.Place(CandyUI.Label("Label", button.transform, caption, 34, CandyUI.Caption),
                          new Vector2(0.5f, 0f), new Vector2(0f, -38f), new Vector2(240f, 46f));

            return button;
        }

        /// <summary>PLACEHOLDER: the store does not exist. One place to replace when it does.</summary>
        private void OpenTools() => Debug.Log("[Snapline] tools store: not built yet");

        // --- decoration ------------------------------------------------------------------------

        /// <summary>
        /// A scatter of slowly drifting blocks. Kept from the old menu, but far fainter — the candy
        /// background already carries the screen, and at the old opacity these read as smudges over
        /// it rather than as depth behind it.
        /// </summary>
        private void BuildDecor()
        {
            const int count = 7;
            _decor = new RectTransform[count];
            _decorPhase = new float[count];
            _decorHome = new Vector2[count];

            var rng = new System.Random(20260828);

            for (int i = 0; i < count; i++)
            {
                Image img = UIKit.Image($"Decor{i}", _root, ArtKit.Block(i % Palette.Count), Color.white);
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float size = 70f + (float)rng.NextDouble() * 70f;
                rt.sizeDelta = new Vector2(size, size);

                var home = new Vector2((float)(rng.NextDouble() - 0.5) * RefWidth,
                                       (float)(rng.NextDouble() - 0.5) * 1680f);
                rt.anchoredPosition = home;
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);

                Color c = img.color;
                c.a = 0.14f;
                img.color = c;

                _decor[i] = rt;
                _decorHome[i] = home;
                _decorPhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            }
        }

        // --- state -----------------------------------------------------------------------------

        public void Show(long best, bool hasSavedRun)
        {
            _continueCaption.text = hasSavedRun ? "CONTINUE" : "PLAY";
            _continueDetail.text = hasSavedRun
                ? $"Endless  •  Score {Hud.Format(App.SaveSystem.SavedRunScore())}"
                : "Endless  •  no timer, no levels";

            int completed = App.SaveSystem.LevelsCompleted();
            _levelsDetail.text = $"{completed} / {Core.Levels.Count}";
            _endlessDetail.text = best > 0 ? $"BEST {Hud.Format(best)}" : "no best yet";

            RefreshCoins();
            RefreshSoundLabel();

            _root.gameObject.SetActive(true);
            StartCoroutine(SlideIn());
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>PLACEHOLDER: no wallet exists, so the balance is always zero.</summary>
        private void RefreshCoins()
        {
            if (_coinLabel != null) _coinLabel.text = "0";
        }

        /// <summary>
        /// The sound button is a single icon with no caption, so muted is shown by dimming it.
        /// </summary>
        public void RefreshSoundLabel()
        {
            if (_soundIcon == null) return;
            _soundIcon.color = App.Settings.SoundEnabled
                ? Color.white
                : new Color(0.55f, 0.55f, 0.60f, 0.75f);
        }

        private void OnPrimary()
        {
            if (_continueCaption.text == "CONTINUE") ContinueRequested?.Invoke();
            else NewGameRequested?.Invoke();
        }

        private void Update()
        {
            if (_decor == null || !IsVisible) return;

            float t = Time.unscaledTime;
            for (int i = 0; i < _decor.Length; i++)
            {
                float phase = _decorPhase[i];
                var drift = new Vector2(Mathf.Sin(t * 0.18f + phase) * 26f,
                                        Mathf.Cos(t * 0.13f + phase) * 34f);
                _decor[i].anchoredPosition = _decorHome[i] + drift;
                _decor[i].localRotation = Quaternion.Euler(0f, 0f, phase * Mathf.Rad2Deg + t * 4f);
            }
        }

        private IEnumerator SlideIn()
        {
            const float duration = 0.32f;
            float t = 0f;

            RectTransform primary = _continue.GetComponent<RectTransform>();
            Vector2 primaryHome = primary.anchoredPosition;
            Vector2 logoHome = _logo != null ? _logo.anchoredPosition : Vector2.zero;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                primary.anchoredPosition = primaryHome + new Vector2(0f, Mathf.Lerp(-160f, 0f, eased));
                primary.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, eased);
                if (_logo != null) _logo.anchoredPosition = logoHome + new Vector2(0f, Mathf.Lerp(70f, 0f, eased));

                yield return null;
            }

            primary.anchoredPosition = primaryHome;
            primary.localScale = Vector3.one;
            if (_logo != null) _logo.anchoredPosition = logoHome;
        }
    }
}
