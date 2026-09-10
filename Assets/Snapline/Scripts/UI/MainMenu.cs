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

        /// <summary>
        /// Where everything on this screen sits.
        ///
        /// Split deliberately. The status bar, wordmark and board hang from the TOP, because that is
        /// what they are visually attached to. Everything from the play button down is measured from
        /// the BOTTOM, because the canvas is 1080 wide and however tall the phone's aspect makes it —
        /// a value measured from the top would put the bottom row off-screen on a short phone and
        /// leave it floating halfway up a tall one.
        ///
        /// The slack between the two halves opens and closes in the middle, under the board, which is
        /// the one place on this screen where a gap costs nothing.
        /// </summary>
        private static class Layout
        {
            // --- measured DOWN from the top of the screen ---
            public const float StatusY = 78f;      // gear, coins, toolbox, sound
            public const float LogoY = 213f;       // centre of the wordmark
            public const float LogoWidth = 860f;
            public const float LogoHeight = 200f;  // the art is letterboxed inside this, never squashed
            /// <summary>Centre of the board preview.</summary>
            public const float BoardCentreY = 630f;

            /// <summary>Widest the board is drawn. It shrinks below this on a short screen.</summary>
            public const float BoardWidth = 720f;
            public const float ToolsX = 240f;
            public const float ToolsY = 75f;

            /// <summary>Breathing room between the board and the play button under it.</summary>
            public const float BoardGapBelow = 28f;

            /// <summary>
            /// The canvas height these bottom-measured values were authored against — a 1080x2340
            /// phone, which is what most modern Android hardware is.
            ///
            /// On a shorter screen every offset below is scaled by the ratio, so the block compresses
            /// evenly instead of one element absorbing the whole difference. Without it a 16:9 screen
            /// has 420 fewer units and the board, being the only flexible thing, shrinks to a stamp.
            /// </summary>
            public const float DesignHeight = 2340f;

            // --- measured UP from the bottom of the screen, before scaling ---
            public const float PlayY = 1045f;
            public const float PlayWidth = 700f;
            public const float PlayHeight = 200f;
            public const float ModesY = 830f;
            public const float ModeWidth = 448f;
            public const float ModeHeight = 200f;
            public const float ModeSplit = 232f;   // how far each mode button sits from the centre
            public const float DailyY = 540f;
            public const float DailyWidth = 950f;
            public const float DailyHeight = 300f;
            public const float BottomRowY = 240f;
            public const float BottomRowSplit = 286f;
        }

        private RectTransform _root;

        private Text _coinLabel;
        private Text _continueCaption;
        private Text _continueDetail;
        private Text _levelsDetail;
        private Text _endlessDetail;
        private Text _dailyDetail;
        private Image _soundIcon;
        private Image _toolsBadge;
        private Text _toolsBadgeLabel;

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
        public event Action ToolsRequested;

        /// <summary>Reopens the consent form. Only ever raised where a form actually exists.</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void SetPrivacyAvailable(bool available)
        {
            if (_privacy != null) _privacy.gameObject.SetActive(available);
        }

        /// <summary>
        /// How much to squeeze the bottom-measured layout on this screen.
        ///
        /// Clamped at both ends: below about three quarters the buttons start touching, and letting
        /// it grow without limit on a very tall screen would strand the bottom row miles from the
        /// daily panel.
        /// </summary>
        private static float BottomScale
        {
            get
            {
                float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
                return Mathf.Clamp(canvasHeight / Layout.DesignHeight, 0.76f, 1.12f);
            }
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
            CandyUI.Place(gear, top, new Vector2(-438f, -Layout.StatusY), new Vector2(Design.StatusButtonSize, Design.StatusButtonSize));
            // PLACEHOLDER: there is no settings screen. Sound lives on its own button for now, so
            // this opens nothing until there is something to open.
            gear.onClick.AddListener(() => Debug.Log("[Snapline] settings: not built yet"));

            // --- coin balance. PLACEHOLDER: no currency exists; the number is not persisted. ---
            Image coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            coinPill.type = Image.Type.Sliced;
            coinPill.preserveAspect = false;
            CandyUI.Place(coinPill, top, new Vector2(-78f, -Layout.StatusY), new Vector2(300f, Design.PillHeight));

            CandyUI.Place(CandyUI.Icon("CoinIcon", coinPill.transform, ArtKit.Ui("coin_s")),
                          new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(68f, 68f));

            _coinLabel = CandyUI.Label("Coins", coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f), new Vector2(200f, 60f));

            Button plus = CandyUI.SpriteButton("CoinPlus", coinPill.transform, ArtKit.Ui("btn_plus"));
            CandyUI.Place(plus, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(78f, 78f));
            plus.onClick.AddListener(OpenTools);

            // --- tools store. PLACEHOLDER: no store, no inventory, no items. ---
            Button tools = CandyUI.SpriteButton("Tools", _root, ArtKit.Ui("icon_toolbox"));
            CandyUI.Place(tools, top, new Vector2(Layout.ToolsX, -Layout.ToolsY), new Vector2(120f, 120f));
            tools.onClick.AddListener(OpenTools);

            _toolsBadge = CandyUI.Icon("ToolsBadge", tools.transform, ArtKit.Ui("dot_red"));
            CandyUI.Place(_toolsBadge, new Vector2(1f, 1f), new Vector2(-2f, -2f), new Vector2(56f, 56f));
            _toolsBadgeLabel = CandyUI.Label("BadgeCount", _toolsBadge.transform, "0", 32, CandyUI.Caption,
                                             outline: false);
            CandyUI.Place(_toolsBadgeLabel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 42f));
            _toolsBadge.gameObject.SetActive(false);

            Button sound = CandyUI.SpriteButton("Sound", _root, ArtKit.Ui("btn_sound"));
            CandyUI.Place(sound, top, new Vector2(438f, -Layout.StatusY), new Vector2(Design.StatusButtonSize, Design.StatusButtonSize));
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
                // Sized by height rather than derived from the width: the wordmark has transparent
                // padding, so matching its aspect exactly made it taller on screen than it looks.
                // preserveAspect keeps it centred inside this box rather than stretching it.
                CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -Layout.LogoY),
                              new Vector2(Layout.LogoWidth, Layout.LogoHeight));
                return;
            }

            Text title = CandyUI.Label("Title", _root, "SNAPLINE", 132, CandyUI.Caption);
            _logo = title.rectTransform;
            CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 160f));
        }

        /// <summary>
        /// The framed board behind the buttons.
        ///
        /// One authored image rather than a frame plus a ground plus sixty-four cells plus a glow.
        /// It is decorative — not the real board, and it never updates — so building it out of live
        /// pieces bought nothing and cost about sixty-seven draw calls on the screen the player sees
        /// first.
        ///
        /// It is also the one element on this screen measured from neither edge: everything above it
        /// hangs from the top and everything below it from the bottom, so it takes whatever is left
        /// and shrinks on a short phone. That is deliberate — it is the only thing here that can lose
        /// size without losing meaning.
        /// </summary>
        private void BuildBoardPreview()
        {
            Sprite board = ArtKit.Ui("board_preview");
            if (board == null) return;

            float aspect = board.rect.width / Mathf.Max(1f, board.rect.height);

            float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float floor = canvasHeight - Layout.PlayY * BottomScale
                        - Layout.PlayHeight * 0.5f - Layout.BoardGapBelow;

            // The centre stays put and the board shrinks around it, so on a short phone it pulls in
            // from both edges rather than sliding up the screen. Half the gap to the play button is
            // what limits it.
            float maxHeight = Mathf.Max(0f, floor - Layout.BoardCentreY) * 2f;

            // Width first, then height from the artwork's own aspect, so a re-exported board at a
            // different shape is never squashed to fit a hard-coded box.
            float width = Mathf.Min(Layout.BoardWidth, maxHeight * aspect);
            float height = width / aspect;

            Image img = CandyUI.Icon("BoardPreview", _root, board);
            CandyUI.Place(img, new Vector2(0.5f, 1f), new Vector2(0f, -Layout.BoardCentreY),
                          new Vector2(width, height));
        }

        // --- the way into a run ----------------------------------------------------------------

        private void BuildPrimary()
        {
            var bottom = new Vector2(0.5f, 0f);

            _continue = CandyUI.SpriteButton("Continue", _root, ArtKit.Ui("tile_pink"), Image.Type.Sliced);
            CandyUI.Place(_continue, bottom, new Vector2(0f, Layout.PlayY * BottomScale),
                          new Vector2(Layout.PlayWidth, Layout.PlayHeight));
            _continue.onClick.AddListener(OnPrimary);

            _continueCaption = CandyUI.Label("Caption", _continue.transform, "PLAY", Design.ButtonSize, CandyUI.Caption);
            CandyUI.Place(_continueCaption, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(840f, 92f));

            _continueDetail = CandyUI.Label("Detail", _continue.transform, "", Design.BodySize, CandyUI.CaptionDim);
            CandyUI.Place(_continueDetail, new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(840f, 52f));
        }

        private void BuildModes()
        {
            var bottom = new Vector2(0.5f, 0f);
            var size = new Vector2(Layout.ModeWidth, Layout.ModeHeight);

            Button levels = CandyUI.SpriteButton("Levels", _root, ArtKit.Ui("tile_purple"), Image.Type.Sliced);
            CandyUI.Place(levels, bottom, new Vector2(-Layout.ModeSplit, Layout.ModesY * BottomScale), size);
            levels.onClick.AddListener(() => LevelsRequested?.Invoke());
            BuildModeFace(levels, ArtKit.Ui("icon_levels"), "60 LEVELS", out _levelsDetail);

            Button endless = CandyUI.SpriteButton("Endless", _root, ArtKit.Ui("tile_cyan"), Image.Type.Sliced);
            CandyUI.Place(endless, bottom, new Vector2(Layout.ModeSplit, Layout.ModesY * BottomScale), size);
            // Starts a fresh endless run. When one is already saved, CONTINUE above resumes it and
            // this replaces it — the same pair the old NEW GAME button provided.
            endless.onClick.AddListener(() => NewGameRequested?.Invoke());
            BuildModeFace(endless, ArtKit.Ui("icon_infinity"), "ENDLESS", out _endlessDetail);
        }

        /// <summary>Icon, title and a small detail line — the shared face of both mode buttons.</summary>
        private static void BuildModeFace(Button button, Sprite icon, string title, out Text detail)
        {
            CandyUI.Place(CandyUI.Icon("Icon", button.transform, icon),
                          new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(100f, 100f));

            // Title over detail, both centred in the space the icon and chevron leave. Centring them
            // on the button itself would put them under the icon, which is what left-aligning was
            // avoiding before.
            const float textCentre = 278f;
            const float textWidth = 250f;

            Text label = CandyUI.Label("Title", button.transform, title, 40, CandyUI.Caption);
            CandyUI.Place(label, new Vector2(0f, 0.5f), new Vector2(textCentre, 26f),
                          new Vector2(textWidth, 54f));

            detail = CandyUI.Label("Detail", button.transform, "", 32, CandyUI.CaptionDim);
            CandyUI.Place(detail, new Vector2(0f, 0.5f), new Vector2(textCentre, -30f),
                          new Vector2(textWidth, 44f));

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
            var bottom = new Vector2(0.5f, 0f);
            const float panelW = Layout.DailyWidth;
            const float panelH = Layout.DailyHeight;

            Button daily = CandyUI.SpriteButton("Daily", _root, ArtKit.Ui("tile_yellow"), Image.Type.Sliced);
            CandyUI.Place(daily, bottom, new Vector2(0f, Layout.DailyY * BottomScale), new Vector2(panelW, panelH));
            daily.onClick.AddListener(() => Debug.Log("[Snapline] daily challenge: not built yet"));

            // The calendar sits in its own column on the left, separated by a rule, so the panel
            // reads as "this thing, about these days" rather than as four unrelated items in a row.
            CandyUI.Place(CandyUI.Icon("Calendar", daily.transform, ArtKit.Ui("icon_calendar")),
                          new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(132f, 132f));

            Image rule = CandyUI.Icon("Rule", daily.transform, ArtKit.Ui("tile_navy"));
            rule.type = Image.Type.Sliced;
            rule.preserveAspect = false;
            rule.color = new Color(0.55f, 0.36f, 0.10f, 0.28f);
            CandyUI.Place(rule, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(4f, 210f));

            const float contentLeft = 200f;

            // White on the yellow panel, per the brief. White on saturated yellow is a weak pairing
            // on its own, so these keep the outline the dark text did not need.
            Text title = CandyUI.Label("Title", daily.transform, "DAILY CHALLENGE", 44,
                                       CandyUI.Caption, TextAnchor.MiddleLeft);
            CandyUI.Place(title, new Vector2(0f, 1f), new Vector2(contentLeft + 230f, -52f),
                          new Vector2(480f, 50f));

            _dailyDetail = CandyUI.Label("Detail", daily.transform, "Clear 8 lines", 34,
                                         CandyUI.Caption, TextAnchor.MiddleLeft);
            CandyUI.Place(_dailyDetail, new Vector2(0f, 1f), new Vector2(contentLeft + 168f, -104f),
                          new Vector2(340f, 42f));

            // Reward badge, top right.
            // The gold pill behind the reward is switched off rather than removed: the coin and the
            // value are its children and position against it, so it still earns its place as the
            // rect that holds them together. Disabling the Image and not the GameObject is the
            // difference between hiding the pill and hiding the reward.
            Image reward = CandyUI.Icon("Reward", daily.transform, ArtKit.Ui("pill_gold"));
            reward.type = Image.Type.Sliced;
            reward.preserveAspect = false;
            reward.enabled = false;
            CandyUI.Place(reward, new Vector2(1f, 1f), new Vector2(-118f, -58f), new Vector2(190f, 72f));
            CandyUI.Place(CandyUI.Icon("RewardCoin", reward.transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(56f, 56f));
            CandyUI.Place(CandyUI.Label("RewardValue", reward.transform, "+50", 38, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(-13f, 0f), new Vector2(118f, 44f));

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
                CandyUI.Place(dot, new Vector2(0f, 0f), new Vector2(x, 122f), new Vector2(64f, 64f));
                if (today) dot.color = new Color(1f, 0.86f, 0.30f, 1f);

                CandyUI.Place(CandyUI.Label($"DayName{i}", daily.transform, days[i], 28,
                                            CandyUI.Caption, TextAnchor.MiddleCenter),
                              new Vector2(0f, 0f), new Vector2(x, 68f), new Vector2(110f, 36f));
            }
        }
        private void BuildBottomRow()
        {
            var bottom = new Vector2(0.5f, 0f);
            float y = Layout.BottomRowY * BottomScale;
            const float disc = Design.DiscButtonSize;

            // A disc behind each icon, rather than a bare icon. The reference reads as three buttons
            // because of the discs; without them the icons float and the row stops looking pressable.
            Button best = MakeDiscButton("Best", "circle_gold", "icon_trophy", "BEST", disc);
            CandyUI.Place(best, bottom, new Vector2(-Layout.BottomRowSplit, y), new Vector2(disc, disc));
            best.onClick.AddListener(() => ScoresRequested?.Invoke());

            Button share = MakeDiscButton("Share", "circle_pink", "icon_share", "SHARE", disc);
            CandyUI.Place(share, bottom, new Vector2(0f, y), new Vector2(disc, disc));
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button tools = MakeDiscButton("ToolsBig", "circle_blue", "icon_toolbox", "TOOLS", disc);
            CandyUI.Place(tools, bottom, new Vector2(Layout.BottomRowSplit, y), new Vector2(disc, disc));
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

        /// <summary>Opens the store. Bootstrap owns the screen; the menu only asks.</summary>
        private void OpenTools() => ToolsRequested?.Invoke();

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

        /// <summary>Coin balance and the tools badge, both straight off the wallet.</summary>
        private void RefreshCoins()
        {
            if (_coinLabel != null) _coinLabel.text = Hud.Format(App.Wallet.Coins);

            if (_toolsBadge == null) return;
            int tools = App.Wallet.TotalTools;
            _toolsBadge.gameObject.SetActive(tools > 0);
            if (_toolsBadgeLabel != null) _toolsBadgeLabel.text = tools.ToString();
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
