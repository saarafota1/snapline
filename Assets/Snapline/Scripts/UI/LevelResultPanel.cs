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
    /// Shown when a level ends, won or lost.
    ///
    /// Separate from the endless game-over card because the two say completely different things:
    /// one is "here is your score", the other is "here is how well you did against a goal, and
    /// here is the next one".
    /// </summary>
    public sealed class LevelResultPanel : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _card;
        private Image _scrim;
        private Text _title;
        private Text _subtitle;
        private Text _stats;
        private Image[] _stars;
        private Button _next;

        public event Action NextRequested;
        public event Action RetryRequested;
        public event Action LevelsRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("LevelResult", parent);

            _scrim = UIKit.Image("Scrim", _root, ArtKit.Solid(), new Color(0f, 0f, 0f, 0.72f), Image.Type.Sliced);
            RectTransform sr = _scrim.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
            _scrim.raycastTarget = true;

            _card = UIKit.Rect("Card", _root);
            UIKit.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(860f, 900f));

            Image cardBg = UIKit.Image("CardBg", _card,
                                       ArtKit.RoundedRect("lvlcard", Palette.BoardPanel, Palette.BoardPanelRim, 5f),
                                       Color.white, Image.Type.Sliced);
            RectTransform cb = cardBg.rectTransform;
            cb.anchorMin = Vector2.zero;
            cb.anchorMax = Vector2.one;
            cb.offsetMin = Vector2.zero;
            cb.offsetMax = Vector2.zero;

            _title = UIKit.Label("Title", _card, "LEVEL COMPLETE", 60, Palette.TextBright);
            UIKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -60f), new Vector2(800f, 80f));

            _subtitle = UIKit.Label("Subtitle", _card, "", 40, Palette.TextDim);
            UIKit.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -142f), new Vector2(800f, 56f));

            BuildStars();

            _stats = UIKit.Label("Stats", _card, "", 34, Palette.TextDim);
            UIKit.Place(_stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -420f), new Vector2(800f, 100f));

            _next = UIKit.Button("Next", _card, "NEXT LEVEL", new Color(0.24f, 0.78f, 0.45f, 1f), Color.white, 50);
            UIKit.Place(_next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(620f, 130f));
            _next.onClick.AddListener(() => NextRequested?.Invoke());

            Button retry = UIKit.Button("Retry", _card, "RETRY", new Color(0.34f, 0.55f, 0.85f, 1f), Color.white, 40);
            UIKit.Place(retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(-160f, 55f), new Vector2(290f, 105f));
            retry.onClick.AddListener(() => RetryRequested?.Invoke());

            Button levels = UIKit.Button("Levels", _card, "LEVELS", new Color(0.30f, 0.36f, 0.62f, 1f),
                                         Color.white, 40);
            UIKit.Place(levels.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(160f, 55f), new Vector2(290f, 105f));
            levels.onClick.AddListener(() => LevelsRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        private void BuildStars()
        {
            _stars = new Image[3];
            const float size = 150f;
            const float spacing = 40f;

            for (int i = 0; i < 3; i++)
            {
                Image star = UIKit.Image($"Star{i}", _card, ProcArt.Star(), Color.white);
                RectTransform rt = star.rectTransform;
                UIKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2((i - 1) * (size + spacing), -300f), new Vector2(size, size));
                _stars[i] = star;
            }
        }

        public void Show(int levelNumber, bool complete, int stars, int linesCleared, int lineTarget,
                         int movesUsed, long score, bool hasNextLevel)
        {
            _title.text = complete ? "LEVEL COMPLETE" : "OUT OF MOVES";
            _title.color = complete ? Palette.TextBright : new Color(1f, 0.65f, 0.6f);

            _subtitle.text = complete
                ? $"Level {levelNumber}"
                : $"Level {levelNumber} — {linesCleared} of {lineTarget} lines";

            _stats.text = complete
                ? $"{linesCleared} lines in {movesUsed} moves\nscore {Hud.Format(score)}"
                : $"{movesUsed} moves used\nscore {Hud.Format(score)}";

            _next.gameObject.SetActive(complete && hasNextLevel);

            _root.gameObject.SetActive(true);
            StartCoroutine(Play(complete ? stars : 0));
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private IEnumerator Play(int stars)
        {
            const float duration = 0.32f;
            float t = 0f;

            Color scrimColour = _scrim.color;
            float targetAlpha = scrimColour.a;

            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = new Color(1f, 1f, 1f, 0.12f);
                _stars[i].rectTransform.localScale = Vector3.one * 0.8f;
            }

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                _card.anchoredPosition = new Vector2(0f, Mathf.Lerp(-1400f, 0f, eased));
                _card.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, eased);
                scrimColour.a = targetAlpha * eased;
                _scrim.color = scrimColour;

                yield return null;
            }

            _card.anchoredPosition = Vector2.zero;
            _card.localScale = Vector3.one;

            // Stars land one at a time. Three at once reads as a single event; staggered, each one
            // is its own small reward and the third landing actually feels like something.
            for (int i = 0; i < stars; i++)
            {
                yield return new WaitForSecondsRealtime(0.18f);
                yield return PopStar(_stars[i]);
            }
        }

        private IEnumerator PopStar(Image star)
        {
            const float duration = 0.26f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                float scale = k < 0.55f
                    ? Mathf.Lerp(0.8f, 1.35f, k / 0.55f)
                    : Mathf.Lerp(1.35f, 1f, (k - 0.55f) / 0.45f);

                star.rectTransform.localScale = Vector3.one * scale;
                star.color = Color.Lerp(new Color(1f, 1f, 1f, 0.12f), Palette.Accent, k);
                yield return null;
            }

            star.rectTransform.localScale = Vector3.one;
            star.color = Palette.Accent;
        }
    }
}
