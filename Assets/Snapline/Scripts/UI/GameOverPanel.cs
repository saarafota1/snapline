using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// End-of-run panel: final score, whether it beat the best, a few run stats, and the one button
    /// that matters. Slides in rather than appearing, so the run feels concluded instead of cut off.
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _card;
        private Image _scrim;
        private Text _title;
        private Text _scoreLabel;
        private Text _bestLabel;
        private Text _statsLabel;
        private Text _newBest;

        public event Action PlayAgainRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("GameOver", parent);

            _scrim = UIKit.Image("Scrim", _root, ArtKit.Solid(), new Color(0f, 0f, 0f, 0.72f), Image.Type.Sliced);
            RectTransform sr = _scrim.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
            _scrim.raycastTarget = true; // swallow taps on the board underneath

            _card = UIKit.Rect("Card", _root);
            UIKit.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(860f, 900f));

            Image cardBg = UIKit.Image("CardBg", _card, ArtKit.RoundedRect("gocard",
                                        Palette.BoardPanel, Palette.BoardPanelRim, 5f),
                                        Color.white, Image.Type.Sliced);
            RectTransform cb = cardBg.rectTransform;
            cb.anchorMin = Vector2.zero;
            cb.anchorMax = Vector2.one;
            cb.offsetMin = Vector2.zero;
            cb.offsetMax = Vector2.zero;

            _title = UIKit.Label("Title", _card, "NO ROOM LEFT", 64, Palette.TextBright);
            UIKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -70f), new Vector2(800f, 80f));

            _newBest = UIKit.Label("NewBest", _card, "NEW BEST!", 52, Palette.Accent);
            UIKit.Place(_newBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -170f), new Vector2(800f, 70f));

            Text scoreCaption = UIKit.Label("ScoreCaption", _card, "SCORE", 34, Palette.TextDim);
            UIKit.Place(scoreCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -258f), new Vector2(800f, 44f));

            _scoreLabel = UIKit.Label("Score", _card, "0", 128, Palette.TextBright);
            UIKit.Place(_scoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -310f), new Vector2(800f, 150f));

            _bestLabel = UIKit.Label("Best", _card, "BEST 0", 42, Palette.Accent);
            UIKit.Place(_bestLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -470f), new Vector2(800f, 56f));

            _statsLabel = UIKit.Label("Stats", _card, "", 34, Palette.TextDim);
            UIKit.Place(_statsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -556f), new Vector2(800f, 120f));

            Button again = UIKit.Button("PlayAgain", _card, "PLAY AGAIN", new Color(0.24f, 0.78f, 0.45f, 1f),
                                        Color.white, 52);
            UIKit.Place(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(620f, 140f));
            again.onClick.AddListener(() => PlayAgainRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        public void Show(long score, long best, bool isNewBest, int lines, int bestCombo, int pieces)
        {
            _scoreLabel.text = Hud.Format(score);
            _bestLabel.text = $"BEST  {Hud.Format(best)}";
            _statsLabel.text = $"{lines} lines cleared\n{pieces} pieces placed    best combo x{Mathf.Max(1, bestCombo)}";
            _newBest.gameObject.SetActive(isNewBest);
            _title.text = isNewBest ? "WHAT A RUN" : "NO ROOM LEFT";

            _root.gameObject.SetActive(true);
            StartCoroutine(SlideIn());
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private IEnumerator SlideIn()
        {
            const float duration = 0.34f;
            float t = 0f;

            Color scrimColour = _scrim.color;
            float targetAlpha = scrimColour.a;

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
            scrimColour.a = targetAlpha;
            _scrim.color = scrimColour;
        }
    }
}
