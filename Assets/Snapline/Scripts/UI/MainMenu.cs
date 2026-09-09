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
            CandyUI.Place(gear, top, new Vector2(-430f, -86f), new Vector2(112f, 112f));
            // PLACEHOLDER: there is no settings screen. Sound lives on its own button for now, so
            // this opens nothing until there is something to open.
            gear.onClick.AddListener(() => Debug.Log("[Snapline] settings: not built yet"));

            // --- coin balance. PLACEHOLDER: no currency exists; the number is not persisted. ---
            Image coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            coinPill.type = Image.Type.Sliced;
            coinPill.preserveAspect = false;
            CandyUI.Place(coinPill, top, new Vector2(-105f, -82f), new Vector2(330f, 96f));

            CandyUI.Place(CandyUI.Icon("CoinIcon", coinPill.transform, ArtKit.Ui("coin_s")),
                          new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(74f, 74f));

            _coinLabel = CandyUI.Label("Coins", coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f), new Vector2(200f, 60f));

            Button plus = CandyUI.SpriteButton("CoinPlus", coinPill.transform, ArtKit.Ui("btn_plus"));
            CandyUI.Place(plus, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(84f, 84f));
            plus.onClick.AddListener(OpenTools);

            // --- tools store. PLACEHOLDER: no store, no inventory, no items. ---
            Button tools = CandyUI.SpriteButton("Tools", _root, ArtKit.Ui("icon_toolbox"));
            CandyUI.Place(tools, top, new Vector2(160f, -84f), new Vector2(118f, 118f));
            tools.onClick.AddListener(OpenTools);

            Image badge = CandyUI.Icon("ToolsBadge", tools.transform, ArtKit.Ui("dot_red"));
            CandyUI.Place(badge, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(52f, 52f));
            CandyUI.Place(CandyUI.Label("BadgeCount", badge.transform, "0", 30, CandyUI.Caption, outline: false),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 40f));
            badge.gameObject.SetActive(false);

            Button sound = CandyUI.SpriteButton("Sound", _root, ArtKit.Ui("btn_sound"));
            CandyUI.Place(sound, top, new Vector2(430f, -86f), new Vector2(112f, 112f));
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
                float height = 900f * (logo.rect.height / Mathf.Max(1f, logo.rect.width));
                CandyUI.Place(_logo, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, height));
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
            const float size = 660f;
            const int grid = 8;

            RectTransform frame = UIKit.Rect("BoardPreview", _root);
            CandyUI.Place(frame, new Vector2(0.5f, 1f), new Vector2(0f, -700f), new Vector2(size, size));

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
            const float inset = 72f;
            float cell = (size - inset * 2f) / grid;

            // A fixed arrangement, not a random one: the front screen should look the same every
            // time it is opened rather than reshuffling behind the player.
            const string pattern =
                "..333..." +
                "..3.1..." +
                ".22.1..." +
                ".22.1..." +
                "....1..." +
                "...054.." +
                "..0554.." +
                "5555554.";

            for (int row = 0; row < grid; row++)
            for (int col = 0; col < grid; col++)
            {
                char c = pattern[row * grid + col];
                bool filled = c != '.';
                Sprite sprite = filled ? ArtKit.Block(c - '0') : ArtKit.EmptyCell();

                Image cellImg = CandyUI.Icon($"C{row}_{col}", frame, sprite);
                cellImg.preserveAspect = false;

                // The empty-cell sprite is a fully lit blue block, so at full brightness a mostly
                // empty board reads as a completely full one — every hole looks like a piece. Darkened
                // until the holes sit behind the blocks the way they do in the reference art.
                if (!filled) cellImg.color = new Color(0.42f, 0.50f, 0.78f, 1f);

                CandyUI.Place(cellImg, new Vector2(0f, 1f),
                              new Vector2(inset + cell * (col + 0.5f), -(inset + cell * (row + 0.5f))),
                              new Vector2(cell - 4f, cell - 4f));
            }
        }

        // --- the way into a run ----------------------------------------------------------------

        private void BuildPrimary()
        {
            var bottom = new Vector2(0.5f, 0f);

            _continue = CandyUI.SpriteButton("Continue", _root, ArtKit.Ui("pill_red"));
            CandyUI.Place(_continue, bottom, new Vector2(0f, 838f), new Vector2(760f, 150f));
            _continue.onClick.AddListener(OnPrimary);

            _continueCaption = CandyUI.Label("Caption", _continue.transform, "PLAY", 66, CandyUI.Caption);
            CandyUI.Place(_continueCaption, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(700f, 76f));

            _continueDetail = CandyUI.Label("Detail", _continue.transform, "", 34, CandyUI.CaptionDim);
            CandyUI.Place(_continueDetail, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(700f, 46f));
        }

        private void BuildModes()
        {
            var bottom = new Vector2(0.5f, 0f);
            var size = new Vector2(372f, 132f);

            Button levels = CandyUI.SpriteButton("Levels", _root, ArtKit.Ui("pill_purple"));
            CandyUI.Place(levels, bottom, new Vector2(-196f, 648f), size);
            levels.onClick.AddListener(() => LevelsRequested?.Invoke());
            BuildModeFace(levels, ArtKit.Ui("icon_levels"), "60 LEVELS", out _levelsDetail);

            Button endless = CandyUI.SpriteButton("Endless", _root, ArtKit.Ui("pill_teal"));
            CandyUI.Place(endless, bottom, new Vector2(196f, 648f), size);
            // Starts a fresh endless run. When one is already saved, CONTINUE above resumes it and
            // this replaces it — the same pair the old NEW GAME button provided.
            endless.onClick.AddListener(() => NewGameRequested?.Invoke());
            BuildModeFace(endless, ArtKit.Ui("icon_infinity"), "ENDLESS", out _endlessDetail);
        }

        /// <summary>Icon, title and a small detail line — the shared face of both mode buttons.</summary>
        private static void BuildModeFace(Button button, Sprite icon, string title, out Text detail)
        {
            CandyUI.Place(CandyUI.Icon("Icon", button.transform, icon),
                          new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(66f, 66f));

            Text label = CandyUI.Label("Title", button.transform, title, 38, CandyUI.Caption, TextAnchor.MiddleLeft);
            CandyUI.Place(label, new Vector2(0f, 0.5f), new Vector2(240f, 18f), new Vector2(280f, 46f));

            detail = CandyUI.Label("Detail", button.transform, "", 28, CandyUI.CaptionDim, TextAnchor.MiddleLeft);
            CandyUI.Place(detail, new Vector2(0f, 0.5f), new Vector2(240f, -22f), new Vector2(280f, 40f));

            CandyUI.Place(CandyUI.Icon("Chevron", button.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(34f, 54f));
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

            Button daily = CandyUI.SpriteButton("Daily", _root, ArtKit.Ui("pill_yellow"));
            CandyUI.Place(daily, bottom, new Vector2(0f, 418f), new Vector2(940f, 248f));
            daily.onClick.AddListener(() => Debug.Log("[Snapline] daily challenge: not built yet"));

            CandyUI.Place(CandyUI.Icon("Calendar", daily.transform, ArtKit.Ui("icon_calendar")),
                          new Vector2(0f, 1f), new Vector2(84f, -68f), new Vector2(96f, 96f));

            Text title = CandyUI.Label("Title", daily.transform, "DAILY CHALLENGE", 40, CandyUI.CaptionDark,
                                       TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(title, new Vector2(0f, 1f), new Vector2(390f, -52f), new Vector2(460f, 50f));

            _dailyDetail = CandyUI.Label("Detail", daily.transform, "Clear 8 lines", 32, CandyUI.CaptionDark,
                                         TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(_dailyDetail, new Vector2(0f, 1f), new Vector2(360f, -100f), new Vector2(400f, 44f));

            CandyUI.Place(CandyUI.Icon("Chevron", daily.transform, ArtKit.Ui("icon_chevron")),
                          new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(34f, 54f));

            // Reward badge.
            Image reward = CandyUI.Icon("Reward", daily.transform, ArtKit.Ui("pill_blue"));
            reward.type = Image.Type.Sliced;
            reward.preserveAspect = false;
            CandyUI.Place(reward, new Vector2(1f, 1f), new Vector2(-108f, -56f), new Vector2(190f, 74f));
            CandyUI.Place(CandyUI.Icon("RewardCoin", reward.transform, ArtKit.Ui("coin_s")),
                          new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(58f, 58f));
            CandyUI.Place(CandyUI.Label("RewardValue", reward.transform, "+50", 34, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(120f, 46f));

            // Seven day dots.
            string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            const float step = 118f;
            float first = -(days.Length - 1) * step * 0.5f;

            for (int i = 0; i < days.Length; i++)
            {
                float x = first + step * i;

                Image dot = CandyUI.Icon($"Day{i}", daily.transform, ArtKit.Ui("dot_pink"));
                CandyUI.Place(dot, new Vector2(0.5f, 0f), new Vector2(x, 84f), new Vector2(56f, 56f));

                CandyUI.Place(CandyUI.Label($"DayName{i}", daily.transform, days[i], 26, CandyUI.CaptionDark,
                              TextAnchor.MiddleCenter, outline: false),
                          new Vector2(0.5f, 0f), new Vector2(x, 38f), new Vector2(110f, 34f));
            }
        }

        private void BuildBottomRow()
        {
            var bottom = new Vector2(0.5f, 0f);
            const float y = 180f;
            const float iconSize = 132f;

            Button best = CandyUI.SpriteButton("Best", _root, ArtKit.Ui("icon_trophy"));
            CandyUI.Place(best, bottom, new Vector2(-280f, y), new Vector2(iconSize, iconSize));
            best.onClick.AddListener(() => ScoresRequested?.Invoke());
            BottomLabel(best, "BEST");

            Button share = CandyUI.SpriteButton("Share", _root, ArtKit.Ui("btn_share"));
            CandyUI.Place(share, bottom, new Vector2(0f, y), new Vector2(iconSize, iconSize));
            share.onClick.AddListener(() => ShareRequested?.Invoke());
            BottomLabel(share, "SHARE");

            Button tools = CandyUI.SpriteButton("ToolsBig", _root, ArtKit.Ui("icon_toolbox"));
            CandyUI.Place(tools, bottom, new Vector2(280f, y), new Vector2(iconSize, iconSize));
            tools.onClick.AddListener(OpenTools);
            BottomLabel(tools, "TOOLS");
        }

        private static void BottomLabel(Button button, string caption) =>
            CandyUI.Place(CandyUI.Label("Label", button.transform, caption, 32, CandyUI.Caption),
                          new Vector2(0.5f, 0f), new Vector2(0f, -34f), new Vector2(240f, 44f));

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
