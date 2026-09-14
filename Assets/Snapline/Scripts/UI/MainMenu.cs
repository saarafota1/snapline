using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The front screen.
    ///
    /// Laid out against `home.png`, with the owner's measured positions: a status bar, the wordmark,
    /// the board picture, the way back into a run, the two modes, the daily challenge, and a row of
    /// round buttons. Everything on it moves a little — the wordmark floats, the play button
    /// breathes and shines, sprinkles drift behind — because a front screen that sits dead reads as
    /// a loading screen.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>
        /// Where everything on this screen sits.
        ///
        /// Split deliberately. The status bar, wordmark and board hang from the TOP, because that is
        /// what they are visually attached to. Everything from the play button down is measured from
        /// the BOTTOM, because the canvas is 1080 wide and however tall the phone's aspect makes it.
        /// </summary>
        private static class Layout
        {
            // --- measured DOWN from the top of the screen ---
            public const float StatusY = 78f;
            public const float LogoY = 213f;
            public const float LogoWidth = 860f;
            public const float LogoHeight = 200f;
            public const float BoardCentreY = 630f;
            public const float BoardWidth = 720f;
            public const float ToolsX = 240f;
            public const float ToolsY = 75f;
            public const float BoardGapBelow = 28f;

            /// <summary>The canvas height the bottom-measured values were authored against.</summary>
            public const float DesignHeight = 2340f;

            // --- measured UP from the bottom of the screen, before scaling ---
            public const float PlayY = 1045f;
            public const float PlayWidth = 700f;
            public const float PlayHeight = 200f;
            public const float ModesY = 830f;
            public const float ModeWidth = 448f;
            public const float ModeHeight = 200f;
            public const float ModeSplit = 232f;
            public const float DailyY = 540f;
            public const float DailyWidth = 950f;
            public const float DailyHeight = 300f;
            public const float BottomRowY = 240f;
            public const float BottomRowSplit = 286f;
        }

        private RectTransform _root;

        private Text _continueCaption;
        private Text _continueDetail;
        private Text _levelsDetail;
        private Text _endlessDetail;
        private Text _dailyDetail;
        private Text _dailyReward;
        private Image _soundIcon;

        private Button _continue;
        private Button _levels;
        private Button _endless;
        private Button _daily;
        private Button _privacy;
        private RectTransform _logo;
        private RectTransform _board;
        private readonly Button[] _bottom = new Button[3];

        private readonly Image[] _dayDots = new Image[7];
        private readonly Text[] _dayNames = new Text[7];

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
        public event Action DailyRequested;
        public event Action SettingsRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void SetPrivacyAvailable(bool available)
        {
            if (_privacy != null) _privacy.gameObject.SetActive(available);
        }

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
            // back into the consent form, but a player with no form must not see a button that opens nothing.
            _privacy = CandyUI.SpriteButton("Privacy", _root, null, "PRIVACY SETTINGS", 26, CandyUI.CaptionDim);
            _privacy.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            CandyUI.Place(_privacy, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 50f));
            _privacy.onClick.AddListener(() => PrivacyRequested?.Invoke());
            _privacy.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        // --- status bar ------------------------------------------------------------------------

        private void BuildStatusBar()
        {
            var top = new Vector2(0.5f, 1f);

            Button gear = CandyUI.SpriteButton("Settings", _root, ArtKit.Ui("btn_gear"));
            gear.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(gear, top, new Vector2(-438f, -Layout.StatusY), new Vector2(Design.StatusButtonSize, Design.StatusButtonSize));
            gear.onClick.AddListener(() => SettingsRequested?.Invoke());
            gear.GetComponent<CandyPress>().Wiggle = 4f;

            CoinPill.Create(_root, top, new Vector2(-78f, -Layout.StatusY), 300f, Design.PillHeight);
            ToolboxButton.Create(_root, top, new Vector2(Layout.ToolsX, -Layout.ToolsY), 120f);

            Button sound = CandyUI.SpriteButton("Sound", _root, ArtKit.Ui("btn_sound"));
            sound.GetComponent<Image>().preserveAspect = true;
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
                CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -Layout.LogoY),
                              new Vector2(Layout.LogoWidth, Layout.LogoHeight));
            }
            else
            {
                Text title = W.Title("Title", _root, "SNAPLINE", new Vector2(0.5f, 1f), new Vector2(0f, -260f), 132);
                _logo = title.rectTransform;
            }

            CandyPress motion = _logo.gameObject.AddComponent<CandyPress>();
            motion.Bob = 9f;
            motion.BobSpeed = 1.3f;
            motion.Click = false;
            Glint glint = _logo.gameObject.AddComponent<Glint>();
            glint.Every = 0.7f;
            glint.Size = 90f;
        }

        private void BuildBoardPreview()
        {
            Sprite board = ArtKit.Ui("board_preview");
            if (board == null) return;

            float aspect = board.rect.width / Mathf.Max(1f, board.rect.height);
            float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float floor = canvasHeight - Layout.PlayY * BottomScale - Layout.PlayHeight * 0.5f - Layout.BoardGapBelow;
            float maxHeight = Mathf.Max(0f, floor - Layout.BoardCentreY) * 2f;
            float width = Mathf.Min(Layout.BoardWidth, maxHeight * aspect);
            float height = width / aspect;

            Image img = CandyUI.Icon("BoardPreview", _root, board);
            CandyUI.Place(img, new Vector2(0.5f, 1f), new Vector2(0f, -Layout.BoardCentreY), new Vector2(width, height));
            _board = img.rectTransform;
            Glint glint = img.gameObject.AddComponent<Glint>();
            glint.Every = 0.45f;
            glint.Size = 70f;
        }

        // --- the way into a run ----------------------------------------------------------------

        private void BuildPrimary()
        {
            var bottom = new Vector2(0.5f, 0f);

            _continue = CandyUI.SpriteButton("Continue", _root, ArtKit.Ui("tile_pink"), Image.Type.Sliced);
            CandyUI.Place(_continue, bottom, new Vector2(0f, Layout.PlayY * BottomScale),
                          new Vector2(Layout.PlayWidth, Layout.PlayHeight));
            _continue.onClick.AddListener(OnPrimary);

            _continueCaption = W.Text("Caption", _continue.transform, "PLAY", W.Centre, new Vector2(0f, 30f),
                                      new Vector2(840f, 96f), 80, CandyStyle.OnPink, Color.white);
            _continueCaption.font = Design.Display;
            _continueCaption.gameObject.AddComponent<CandyShine>();

            _continueDetail = W.Text("Detail", _continue.transform, "", W.Centre, new Vector2(0f, -52f),
                                     new Vector2(840f, 52f), Design.BodySize, CandyStyle.OnPink, Color.white);

            W.Sprinkles((RectTransform)_continue.transform, new Vector2(Layout.PlayWidth, Layout.PlayHeight));
            ShineSweep.Add(_continue.GetComponent<Image>(), 3.0f);
            _continue.GetComponent<CandyPress>().Pulse = 0.022f;
        }

        private void BuildModes()
        {
            var bottom = new Vector2(0.5f, 0f);
            var size = new Vector2(Layout.ModeWidth, Layout.ModeHeight);

            _levels = CandyUI.SpriteButton("Levels", _root, ArtKit.Ui("tile_purple"), Image.Type.Sliced);
            CandyUI.Place(_levels, bottom, new Vector2(-Layout.ModeSplit, Layout.ModesY * BottomScale), size);
            _levels.onClick.AddListener(() => LevelsRequested?.Invoke());
            BuildModeFace(_levels, ArtKit.Ui("icon_levels"), "60 LEVELS", CandyStyle.OnPurple, out _levelsDetail);

            _endless = CandyUI.SpriteButton("Endless", _root, ArtKit.Ui("tile_cyan"), Image.Type.Sliced);
            CandyUI.Place(_endless, bottom, new Vector2(Layout.ModeSplit, Layout.ModesY * BottomScale), size);
            _endless.onClick.AddListener(() => NewGameRequested?.Invoke());
            BuildModeFace(_endless, ArtKit.Ui("icon_infinity"), "ENDLESS", CandyStyle.OnBlue, out _endlessDetail);
        }

        private static void BuildModeFace(Button button, Sprite icon, string title, CandyStyle style, out Text detail)
        {
            Image img = CandyUI.Icon("Icon", button.transform, icon);
            CandyUI.Place(img, new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(100f, 100f));
            CandyPress wobble = img.gameObject.AddComponent<CandyPress>();
            wobble.Wiggle = 6f;
            wobble.WiggleEvery = 4.2f;
            wobble.Click = false;

            const float textCentre = 278f;
            const float textWidth = 250f;

            Text label = W.Text("Title", button.transform, title, new Vector2(0f, 0.5f), new Vector2(textCentre, 26f),
                                new Vector2(textWidth, 58f), 44, style, Color.white);
            label.font = Design.Display;

            detail = W.Text("Detail", button.transform, "", new Vector2(0f, 0.5f), new Vector2(textCentre, -30f),
                            new Vector2(textWidth, 44f), 32, style, Color.white);

            CandyUI.Place(CandyUI.Icon("Chevron", button.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(36f, 58f));
        }

        /// <summary>The daily challenge strip: today's objective, its reward, and the week so far.</summary>
        private void BuildDaily()
        {
            var bottom = new Vector2(0.5f, 0f);
            const float panelW = Layout.DailyWidth;
            const float panelH = Layout.DailyHeight;

            _daily = CandyUI.SpriteButton("Daily", _root, ArtKit.Ui("tile_yellow"), Image.Type.Sliced);
            CandyUI.Place(_daily, bottom, new Vector2(0f, Layout.DailyY * BottomScale), new Vector2(panelW, panelH));
            _daily.onClick.AddListener(() => DailyRequested?.Invoke());

            Image calendar = CandyUI.Icon("Calendar", _daily.transform, ArtKit.Ui("icon_calendar"));
            CandyUI.Place(calendar, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(132f, 132f));
            CandyPress wobble = calendar.gameObject.AddComponent<CandyPress>();
            wobble.Wiggle = 7f;
            wobble.Click = false;

            Image rule = CandyUI.Icon("Rule", _daily.transform, ArtKit.Ui("tile_navy"));
            rule.type = Image.Type.Sliced;
            rule.preserveAspect = false;
            rule.color = new Color(0.55f, 0.36f, 0.10f, 0.28f);
            CandyUI.Place(rule, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(4f, 210f));

            const float contentLeft = 200f;

            Text title = W.Text("Title", _daily.transform, "DAILY CHALLENGE", new Vector2(0f, 1f),
                                new Vector2(contentLeft + 230f, -52f), new Vector2(480f, 56f), 46, CandyStyle.OnGold,
                                Color.white, TextAnchor.MiddleLeft);
            title.font = Design.Display;

            _dailyDetail = W.Text("Detail", _daily.transform, "Clear 8 lines", new Vector2(0f, 1f),
                                  new Vector2(contentLeft + 168f, -104f), new Vector2(340f, 44f), 34, CandyStyle.OnGold,
                                  Color.white, TextAnchor.MiddleLeft);

            RectTransform reward = UIKit.Rect("Reward", _daily.transform);
            CandyUI.Place(reward, new Vector2(1f, 1f), new Vector2(-118f, -58f), new Vector2(190f, 72f));
            Image coin = CandyUI.Icon("RewardCoin", reward, ArtKit.Ui("coin"));
            CandyUI.Place(coin, new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(60f, 60f));
            coin.gameObject.AddComponent<Glint>().Every = 1.4f;
            _dailyReward = W.Text("RewardValue", reward, "+100", new Vector2(0.5f, 0.5f), new Vector2(-4f, 0f),
                                  new Vector2(130f, 50f), 42, CandyStyle.OnGold, Color.white);
            _dailyReward.font = Design.Display;

            CandyUI.Place(CandyUI.Icon("Chevron", _daily.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(36f, 58f));

            string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            const float stripLeft = contentLeft + 20f;
            const float stripRight = panelW - 116f;
            float step = (stripRight - stripLeft) / (days.Length - 1);

            for (int i = 0; i < days.Length; i++)
            {
                float x = stripLeft + step * i;
                _dayDots[i] = CandyUI.Icon($"Day{i}", _daily.transform, ArtKit.Ui("dot_pink"));
                CandyUI.Place(_dayDots[i], new Vector2(0f, 0f), new Vector2(x, 122f), new Vector2(64f, 64f));

                _dayNames[i] = W.Text($"DayName{i}", _daily.transform, days[i], new Vector2(0f, 0f), new Vector2(x, 68f),
                                      new Vector2(110f, 36f), 28, CandyStyle.OnGold, Color.white);
            }
        }

        private void BuildBottomRow()
        {
            var bottom = new Vector2(0.5f, 0f);
            float y = Layout.BottomRowY * BottomScale;
            const float disc = Design.DiscButtonSize;

            _bottom[0] = MakeDiscButton("Best", "circle_gold", "icon_trophy", "BEST", disc);
            CandyUI.Place(_bottom[0], bottom, new Vector2(-Layout.BottomRowSplit, y), new Vector2(disc, disc));
            _bottom[0].onClick.AddListener(() => ScoresRequested?.Invoke());

            _bottom[1] = MakeDiscButton("Share", "circle_pink", "icon_share", "SHARE", disc);
            CandyUI.Place(_bottom[1], bottom, new Vector2(0f, y), new Vector2(disc, disc));
            _bottom[1].onClick.AddListener(() => ShareRequested?.Invoke());

            _bottom[2] = MakeDiscButton("ToolsBig", "circle_blue", "icon_toolbox", "TOOLS", disc);
            CandyUI.Place(_bottom[2], bottom, new Vector2(Layout.BottomRowSplit, y), new Vector2(disc, disc));
            _bottom[2].onClick.AddListener(Nav.OpenToolbox);
        }

        private Button MakeDiscButton(string name, string disc, string icon, string caption, float size)
        {
            Button button = CandyUI.SpriteButton(name, _root, ArtKit.Ui(disc));
            button.GetComponent<Image>().preserveAspect = true;

            CandyUI.Place(CandyUI.Icon("Icon", button.transform, ArtKit.Ui(icon)),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.66f, size * 0.66f));

            Text label = W.Text("Label", button.transform, caption, new Vector2(0.5f, 0f), new Vector2(0f, -38f),
                                new Vector2(240f, 50f), 36, CandyStyle.White, Color.white);
            label.font = Design.Display;

            button.GetComponent<CandyPress>().Bob = 5f;
            return button;
        }

        // --- decoration ------------------------------------------------------------------------

        /// <summary>Candy sprinkles drifting slowly over the background, like the reference has scattered.</summary>
        private void BuildDecor()
        {
            const int count = 16;
            _decor = new RectTransform[count];
            _decorPhase = new float[count];
            _decorHome = new Vector2[count];

            var rng = new System.Random(20260914);

            for (int i = 0; i < count; i++)
            {
                Image img = UIKit.Image($"Sprinkle{i}", _root, Fx.CapsuleSprite(), Fx.CandyColours[i % Fx.CandyColours.Length]);
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float len = 44f + (float)rng.NextDouble() * 26f;
                rt.sizeDelta = new Vector2(len, len / 2.4f);

                // Kept to the edges, where the reference scatters them, so they never sit on a button.
                float side = i % 2 == 0 ? -1f : 1f;
                var home = new Vector2(side * (420f + (float)rng.NextDouble() * 110f), ((float)rng.NextDouble() - 0.5f) * 2100f);
                rt.anchoredPosition = home;
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);

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
                ? $"Endless  •  Score {Hud.Format(SaveSystem.SavedRunScore())}"
                : "Endless  •  no timer, no levels";

            int completed = SaveSystem.LevelsCompleted();
            _levelsDetail.text = $"{completed} / {Levels.Count}";
            _endlessDetail.text = best > 0 ? $"BEST {Hud.Format(best)}" : "no best yet";

            RefreshDaily();
            RefreshSoundLabel();

            _root.gameObject.SetActive(true);
            Enter();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void RefreshDaily()
        {
            int today = DailyProgress.Today;
            int weekStart = Daily.WeekStart(today);
            int todayIndex = today - weekStart;

            LevelDef puzzle = Daily.ForDay(today);
            bool done = DailyProgress.TodayDone;
            _dailyDetail.text = done ? "Done today! Back tomorrow" : $"Clear {puzzle.LineTarget} lines";
            _dailyReward.text = done ? "✓" : $"+{Daily.CompletionCoins}";

            for (int i = 0; i < 7; i++)
            {
                bool dayDone = DailyProgress.IsDone(weekStart + i);
                bool isToday = i == todayIndex;
                _dayDots[i].sprite = ArtKit.Ui(dayDone ? "badge_check" : "dot_pink");
                _dayDots[i].color = isToday && !dayDone ? new Color(1f, 0.86f, 0.30f, 1f) : Color.white;
                _dayDots[i].rectTransform.sizeDelta = isToday ? new Vector2(76f, 76f) : new Vector2(64f, 64f);

                CandyPress pulse = W.Ensure<CandyPress>(_dayDots[i]);
                pulse.Click = false;
                pulse.Pulse = isToday && !dayDone ? 0.1f : 0f;
            }
        }

        public void RefreshSoundLabel()
        {
            if (_soundIcon == null) return;
            _soundIcon.color = Settings.SoundEnabled ? Color.white : new Color(0.55f, 0.55f, 0.60f, 0.75f);
        }

        private void OnPrimary()
        {
            Fx.Instance?.Sparkles(_continue.transform.position, 14, 320f, 80f);
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
                var drift = new Vector2(Mathf.Sin(t * 0.25f + phase) * 18f, Mathf.Cos(t * 0.19f + phase) * 30f);
                _decor[i].anchoredPosition = _decorHome[i] + drift;
                _decor[i].localRotation = Quaternion.Euler(0f, 0f, phase * Mathf.Rad2Deg + t * 12f * (i % 2 == 0 ? 1f : -1f));
            }
        }

        /// <summary>Everything arrives in a quick cascade from the top down.</summary>
        private void Enter()
        {
            if (_logo != null) Tween.PopIn(_logo, 0f, 0.55f, 0.4f);
            if (_board != null) Tween.PopIn(_board, 0.06f, 0.5f, 0.6f);
            Tween.PopIn(_continue.transform, 0.12f, 0.5f, 0.5f);
            Tween.PopIn(_levels.transform, 0.18f, 0.45f, 0.5f);
            Tween.PopIn(_endless.transform, 0.22f, 0.45f, 0.5f);
            Tween.PopIn(_daily.transform, 0.28f, 0.45f, 0.6f);
            for (int i = 0; i < _bottom.Length; i++) Tween.PopIn(_bottom[i].transform, 0.34f + i * 0.05f, 0.45f, 0.2f);
        }
    }
}
