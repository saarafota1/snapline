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
    /// The front screen: title, best score, and the way into a game.
    ///
    /// Built in code like the rest of the interface. Laid out so a mode picker and a leaderboard
    /// button drop into the button column later without the rest moving.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _card;
        private Text _bestLabel;
        private Text _statsLabel;
        private Text _soundLabel;

        private Button _primary;
        private Text _primaryCaption;
        private Button _secondary;
        private Button _levels;
        private Text _levelsCaption;
        private Button _share;

        private RectTransform[] _decor;
        private float[] _decorPhase;
        private Vector2[] _decorHome;

        public event Action ContinueRequested;
        public event Action NewGameRequested;
        public event Action LevelsRequested;
        public event Action ScoresRequested;
        public event Action ShareRequested;
        public event Action SoundToggled;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("MainMenu", parent);

            BuildDecor();

            // --- title -------------------------------------------------------------------
            Text title = UIKit.Label("Title", _root, "SNAPLINE", 132, Palette.TextBright);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -300f), new Vector2(1000f, 160f));

            Text subtitle = UIKit.Label("Subtitle", _root, "BLOCK PUZZLE", 46, Palette.TextDim);
            UIKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -430f), new Vector2(1000f, 60f));

            // --- best score card ---------------------------------------------------------
            _card = UIKit.Rect("BestCard", _root);
            UIKit.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 190f), new Vector2(760f, 260f));

            Image cardBg = UIKit.Image("CardBg", _card,
                                       ArtKit.RoundedRect("menucard", Palette.BoardPanel, Palette.BoardPanelRim, 4f),
                                       Color.white, Image.Type.Sliced);
            RectTransform cb = cardBg.rectTransform;
            cb.anchorMin = Vector2.zero;
            cb.anchorMax = Vector2.one;
            cb.offsetMin = Vector2.zero;
            cb.offsetMax = Vector2.zero;

            Text bestCaption = UIKit.Label("BestCaption", _card, "BEST SCORE", 36, Palette.TextDim);
            UIKit.Place(bestCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -34f), new Vector2(700f, 44f));

            _bestLabel = UIKit.Label("Best", _card, "0", 104, Palette.Accent);
            UIKit.Place(_bestLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -78f), new Vector2(700f, 120f));

            _statsLabel = UIKit.Label("Stats", _card, "", 30, Palette.TextDim);
            UIKit.Place(_statsLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 26f), new Vector2(700f, 40f));

            // --- buttons ------------------------------------------------------------------
            _primary = UIKit.Button("Primary", _root, "PLAY", new Color(0.24f, 0.78f, 0.45f, 1f), Color.white, 58);
            UIKit.Place(_primary.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(660f, 140f));
            _primaryCaption = _primary.GetComponentInChildren<Text>();
            _primary.onClick.AddListener(OnPrimary);

            _levels = UIKit.Button("Levels", _root, "LEVELS", new Color(0.55f, 0.38f, 0.82f, 1f), Color.white, 52);
            UIKit.Place(_levels.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(660f, 140f));
            _levelsCaption = _levels.GetComponentInChildren<Text>();
            _levels.onClick.AddListener(() => LevelsRequested?.Invoke());

            Button scores = UIKit.Button("Scores", _root, "BEST SCORES", new Color(0.32f, 0.45f, 0.72f, 1f),
                                         Color.white, 44);
            UIKit.Place(scores.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -345f), new Vector2(660f, 120f));
            scores.onClick.AddListener(() => ScoresRequested?.Invoke());

            _secondary = UIKit.Button("Secondary", _root, "NEW GAME", new Color(0.30f, 0.36f, 0.62f, 1f),
                                      Color.white, 40);
            UIKit.Place(_secondary.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(-170f, -475f), new Vector2(320f, 108f));
            _secondary.onClick.AddListener(() => NewGameRequested?.Invoke());

            _share = UIKit.Button("Share", _root, "SHARE", new Color(0.34f, 0.55f, 0.85f, 1f), Color.white, 40);
            _share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button sound = UIKit.Button("Sound", _root, "SOUND ON", new Color(0.28f, 0.31f, 0.52f, 1f),
                                        Palette.TextDim, 36);
            UIKit.Place(sound.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(400f, 90f));
            _soundLabel = sound.GetComponentInChildren<Text>();
            sound.onClick.AddListener(() => SoundToggled?.Invoke());

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// A scatter of slowly drifting blocks behind the menu. Cheap, and it stops the front screen
        /// looking like a settings dialog — this is a colourful game and the menu should say so.
        /// </summary>
        private void BuildDecor()
        {
            const int count = 9;
            _decor = new RectTransform[count];
            _decorPhase = new float[count];
            _decorHome = new Vector2[count];

            var rng = new System.Random(20260828);

            for (int i = 0; i < count; i++)
            {
                Image img = UIKit.Image($"Decor{i}", _root, ArtKit.Block(i % Palette.Count), Color.white);
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float size = 90f + (float)rng.NextDouble() * 90f;
                rt.sizeDelta = new Vector2(size, size);

                var home = new Vector2((float)(rng.NextDouble() - 0.5) * 940f,
                                       (float)(rng.NextDouble() - 0.5) * 1680f);
                rt.anchoredPosition = home;
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);

                Color c = img.color;
                c.a = 0.22f;
                img.color = c;

                _decor[i] = rt;
                _decorHome[i] = home;
                _decorPhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            }
        }

        public void Show(long best, bool hasSavedRun)
        {
            _bestLabel.text = Hud.Format(best);

            int games = App.SaveSystem.GamesPlayed;
            int lines = App.SaveSystem.LifetimeLines;
            _statsLabel.text = games == 0
                ? "no games played yet"
                : $"{games} games    {Hud.Format(lines)} lines cleared    best combo x{Mathf.Max(1, App.SaveSystem.BestCombo)}";

            // A run in progress turns PLAY into CONTINUE and reveals the discard option, so the
            // saved board is never thrown away by someone just tapping the big green button.
            _primaryCaption.text = hasSavedRun ? "CONTINUE" : "PLAY";
            _secondary.gameObject.SetActive(hasSavedRun);

            // With no saved run there is no NEW GAME beside it, so SHARE takes the whole row rather
            // than sitting lopsided in half of one.
            RectTransform shareRect = _share.GetComponent<RectTransform>();
            UIKit.Place(shareRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(hasSavedRun ? 170f : 0f, -475f),
                        new Vector2(hasSavedRun ? 320f : 660f, 108f));

            int completed = App.SaveSystem.LevelsCompleted();
            _levelsCaption.text = completed == 0
                ? "LEVELS"
                : $"LEVELS   {completed}/{Core.Levels.Count}";

            RefreshSoundLabel();

            _root.gameObject.SetActive(true);
            StartCoroutine(SlideIn());
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void RefreshSoundLabel()
        {
            _soundLabel.text = App.Settings.SoundEnabled ? "SOUND ON" : "SOUND OFF";
            _soundLabel.color = App.Settings.SoundEnabled ? Palette.TextBright : Palette.TextDim;
        }

        private void OnPrimary()
        {
            if (_primaryCaption.text == "CONTINUE") ContinueRequested?.Invoke();
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

            RectTransform primary = _primary.GetComponent<RectTransform>();
            Vector2 cardHome = _card.anchoredPosition;
            Vector2 primaryHome = primary.anchoredPosition;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                _card.anchoredPosition = cardHome + new Vector2(0f, Mathf.Lerp(120f, 0f, eased));
                primary.anchoredPosition = primaryHome + new Vector2(0f, Mathf.Lerp(-160f, 0f, eased));
                primary.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, eased);

                yield return null;
            }

            _card.anchoredPosition = cardHome;
            primary.anchoredPosition = primaryHome;
            primary.localScale = Vector3.one;
        }
    }
}
