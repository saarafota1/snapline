using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// PAUSED, from `popup_paused.png`: where you are, RESUME / RESTART / LEVELS / HOME, the music,
    /// sound and vibration switches, and HOW TO PLAY.
    ///
    /// The same card, without the run buttons, is the settings card behind the gear on the front
    /// screen — one set of switches, so they can never disagree.
    /// </summary>
    public sealed class PausePopup : CandyPopup
    {
        private static class Layout
        {
            public static readonly Vector2 Card = new Vector2(800f, 1300f);
            public const float CardY = -40f;
            public const float NameY = 176f;
            public const float InfoY = 250f;
            public const float ResumeY = 386f;
            public const float RestartY = 540f;
            public const float LevelsY = 674f;
            public const float HomeY = 800f;
            public const float RuleY = 876f;
            public const float MusicY = 950f;
            public const float RowStep = 98f;
            public const float HowToY = 1242f;
        }

        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action LevelsRequested;
        public event Action HomeRequested;
        public event Action PrivacyRequested;

        private Text _name;
        private Text _info;
        private Image _infoPill;
        private Button _resume;
        private Button _restart;
        private Button _levels;
        private Button _home;
        private Button _privacy;
        private RectTransform _settings;
        private CandyToggle _music;
        private CandyToggle _sound;
        private CandyToggle _vibration;
        private Text _howTo;
        private RectTransform _help;
        private bool _inGame;

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "Pause", "PAUSED", Layout.Card, Layout.CardY, true, 560f, 86);

            _name = W.Text("Name", Card, "LEVEL 1", W.Top, new Vector2(0f, -Layout.NameY), new Vector2(700f, 100f),
                           76, CandyStyle.Blue, Color.white);
            _name.font = Design.Display;

            _infoPill = W.Rounded("InfoPill", Card, CandyText.Hex(0xFBE3CC), CandyText.Hex(0xF2CFB0), W.Top,
                                  new Vector2(0f, -Layout.InfoY), new Vector2(600f, 66f));
            _info = W.Text("Info", _infoPill.transform, "", W.Centre, new Vector2(0f, 1f), new Vector2(600f, 60f),
                           32, CandyStyle.Cocoa, Color.white);

            _resume = W.Pill("Resume", Card, "pill_red", "RESUME", W.Top, new Vector2(0f, -Layout.ResumeY),
                             new Vector2(660f, 150f), 78, CandyStyle.OnPink, "icon_play", 80f, sprinkles: true,
                             shine: true, pulse: 0.02f);
            _resume.onClick.AddListener(() => ResumeRequested?.Invoke());

            _restart = W.Pill("Restart", Card, "pill_white", "RESTART", W.Top, new Vector2(0f, -Layout.RestartY),
                              new Vector2(570f, 122f), 60, CandyStyle.OnBlue, "sym_restart", 76f, tint: W.CandyBlue);
            _restart.onClick.AddListener(() => RestartRequested?.Invoke());

            _levels = W.Pill("Levels", Card, "pill_white", "LEVELS", W.Top, new Vector2(0f, -Layout.LevelsY),
                             new Vector2(570f, 122f), 60, CandyStyle.OnBlue, "icon_levels", 74f, tint: W.CandyBlue);
            _levels.onClick.AddListener(() => LevelsRequested?.Invoke());

            _home = W.Pill("Home", Card, "pill_white", "HOME", W.Top, new Vector2(0f, -Layout.HomeY),
                           new Vector2(500f, 100f), 50, CandyStyle.Blue, "icon_home", 64f);
            _home.onClick.AddListener(() => HomeRequested?.Invoke());

            _settings = UIKit.Rect("Settings", Card);
            CandyUI.Place(_settings, W.Top, Vector2.zero, new Vector2(Layout.Card.x, 10f));

            W.Rounded("Rule", _settings, CandyText.Hex(0xEBC9B5), CandyText.Hex(0xEBC9B5), W.Top,
                      new Vector2(0f, -Layout.RuleY), new Vector2(640f, 5f), 2f);

            _music = Row("Music", "sym_music", "MUSIC", 0, Settings.MusicEnabled, on =>
            {
                Settings.MusicEnabled = on;
                if (Music.Instance != null) Music.Instance.SetEnabled(on);
            });
            _sound = Row("Sound", "btn_sound", "SOUND", 1, Settings.SoundEnabled, on =>
            {
                Settings.SoundEnabled = on;
                Sound.Muted = !on;
            });
            _vibration = Row("Vibration", "sym_vibration", "VIBRATION", 2, Settings.VibrationEnabled, on =>
            {
                Settings.VibrationEnabled = on;
                if (on) Haptics.Medium();
            });

            Button howTo = CandyUI.SpriteButton("HowTo", _settings, null);
            howTo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            CandyUI.Place(howTo, W.Top, new Vector2(0f, -Layout.HowToY), new Vector2(360f, 64f));
            _howTo = W.Text("Text", howTo.transform, "HOW TO PLAY", W.Centre, Vector2.zero, new Vector2(360f, 60f),
                            38, CandyStyle.Blue, Color.white);
            W.Rounded("Underline", howTo.transform, CandyText.Hex(0x2A57D8), CandyText.Hex(0x2A57D8), W.Centre,
                      new Vector2(0f, -24f), new Vector2(250f, 4f), 2f);
            howTo.onClick.AddListener(ToggleHelp);

            _privacy = CandyUI.SpriteButton("Privacy", _settings, null);
            _privacy.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            CandyUI.Place(_privacy, W.Top, new Vector2(0f, -Layout.HowToY - 70f), new Vector2(420f, 56f));
            W.Text("Text", _privacy.transform, "PRIVACY SETTINGS", W.Centre, Vector2.zero, new Vector2(420f, 56f),
                   30, CandyStyle.Cocoa, Color.white);
            _privacy.onClick.AddListener(() => PrivacyRequested?.Invoke());
            _privacy.gameObject.SetActive(false);

            BuildHelp();
        }

        private CandyToggle Row(string name, string icon, string caption, int index, bool on, Action<bool> changed)
        {
            float y = -(Layout.MusicY + index * Layout.RowStep);
            Image row = W.Rounded(name, _settings, CandyText.Hex(0xFCEBDD), CandyText.Hex(0xF4D7C2), W.Top,
                                  new Vector2(0f, y), new Vector2(640f, 86f), 40f);
            W.Img("Icon", row.transform, icon, W.Left, new Vector2(70f, 0f), new Vector2(70f, 70f));
            W.Text("Caption", row.transform, caption, W.Left, new Vector2(270f, 0f), new Vector2(280f, 60f),
                   40, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleLeft);
            return CandyToggle.Create(row.transform, W.Right, new Vector2(-104f, 0f), new Vector2(160f, 80f), on, changed);
        }

        /// <summary>Four lines of how to play, shown over the buttons when HOW TO PLAY is tapped.</summary>
        private void BuildHelp()
        {
            Image panel = W.Rounded("Help", Card, CandyText.Hex(0xFFF7EC), CandyText.Hex(0xF3C9DD), W.Top,
                                    new Vector2(0f, -560f), new Vector2(690f, 720f), 44f);
            panel.raycastTarget = true;
            _help = panel.rectTransform;

            string[] steps =
            {
                "Drag a piece from the tray onto the board.",
                "Fill a whole row or column to clear it.",
                "Clear lines back to back for a COMBO.",
                "UNDO, SHUFFLE and HAMMER get you out of trouble.",
                "The run ends when no piece fits.",
            };

            for (int i = 0; i < steps.Length; i++)
            {
                Image dot = W.Img("Dot" + i, _help, i % 2 == 0 ? "circle_pink" : "circle_purple", W.Top,
                                  new Vector2(-280f, -80f - i * 124f), new Vector2(64f, 64f));
                W.Text("N", dot.transform, (i + 1).ToString(), W.Centre, new Vector2(0f, 2f), new Vector2(60f, 60f),
                       34, CandyStyle.OnPink, Color.white);
                Text t = W.Text("Step" + i, _help, steps[i], W.Top, new Vector2(40f, -80f - i * 124f),
                                new Vector2(540f, 110f), 34, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleLeft);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            Button ok = W.Pill("Ok", _help, "pill_green", "GOT IT", W.Bottom, new Vector2(0f, 70f),
                               new Vector2(360f, 100f), 50, CandyStyle.OnGreen);
            ok.onClick.AddListener(ToggleHelp);
            _help.gameObject.SetActive(false);
        }

        private void ToggleHelp()
        {
            bool show = !_help.gameObject.activeSelf;
            _help.gameObject.SetActive(show);
            _help.SetAsLastSibling();
            if (show) Tween.PopIn(_help, 0f, 0.35f, 0.6f);
        }

        public void SetPrivacyAvailable(bool available)
        {
            if (_privacy != null) _privacy.gameObject.SetActive(available && !_inGame);
        }

        /// <summary>The card as a pause during a run.</summary>
        public void ShowInGame(string title, string info, string levelsCaption)
        {
            _inGame = true;
            SetTitle("PAUSED");
            _name.text = title;
            _info.text = info;
            Text levels = W.Caption(_levels);
            if (levels != null) levels.text = levelsCaption;

            SetRunButtons(true);
            _settings.anchoredPosition = Vector2.zero;
            Card.sizeDelta = Layout.Card;
            Card.anchoredPosition = new Vector2(0f, Layout.CardY);
            ResizeCardBackground(Layout.Card);
            _privacy.gameObject.SetActive(false);
            Open();
        }

        /// <summary>The card as settings, from the front screen: just the switches.</summary>
        public void ShowSettings(bool privacyAvailable)
        {
            _inGame = false;
            SetTitle("SETTINGS");
            SetRunButtons(false);

            const float lift = 640f;
            _settings.anchoredPosition = new Vector2(0f, lift + 120f);
            var size = new Vector2(Layout.Card.x, Layout.Card.y - lift + (privacyAvailable ? 80f : 0f));
            Card.sizeDelta = size;
            Card.anchoredPosition = Vector2.zero;
            ResizeCardBackground(size);
            _privacy.gameObject.SetActive(privacyAvailable);
            Open();
        }

        private void SetRunButtons(bool on)
        {
            _name.gameObject.SetActive(on);
            _infoPill.gameObject.SetActive(on);
            _resume.gameObject.SetActive(on);
            _restart.gameObject.SetActive(on);
            _levels.gameObject.SetActive(on);
            _home.gameObject.SetActive(on);
        }

        private void ResizeCardBackground(Vector2 size)
        {
            Transform bg = Card.Find("CardBg");
            if (bg != null) W.ResizeCard(bg.GetComponent<Image>(), size);
        }

        private void Open()
        {
            _help.gameObject.SetActive(false);
            _music.Set(Settings.MusicEnabled, false);
            _sound.Set(Settings.SoundEnabled, false);
            _vibration.Set(Settings.VibrationEnabled, false);

            Present();

            if (_inGame)
            {
                Reveal(_name, 0.12f);
                Reveal(_resume, 0.18f);
                Reveal(_restart, 0.24f);
                Reveal(_levels, 0.3f);
                Reveal(_home, 0.36f);
            }
        }

        /// <summary>The close button resumes, the same as RESUME — it is still a pause.</summary>
        protected override void OnCloseButton()
        {
            if (_inGame) ResumeRequested?.Invoke();
            else Close();
        }
    }
}
