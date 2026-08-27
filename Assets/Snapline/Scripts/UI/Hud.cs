using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The score bar across the top: current score, best score, and the combo streak.
    ///
    /// The score counter deliberately rolls rather than snapping. A number that climbs is the
    /// cheapest reward signal there is, and it keeps the eye on the score after a big clear instead
    /// of the value having already changed by the time the explosion finishes.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private Text _scoreLabel;
        private Text _bestLabel;
        private Text _comboLabel;
        private RectTransform _scoreRect;
        private RectTransform _comboRect;
        private Image _comboPanel;

        private long _displayedScore;
        private long _targetScore;
        private float _rollSpeed;
        private Coroutine _punch;

        private long _bestScore;
        private bool _beatenThisRun;

        public RectTransform Root { get; private set; }

        public void Init(RectTransform parent, long bestScore)
        {
            Root = UIKit.Rect("Hud", parent);
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(1f, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.offsetMin = new Vector2(0f, -300f);
            Root.offsetMax = new Vector2(0f, 0f);

            _bestScore = bestScore;

            Text bestCaption = UIKit.Label("BestCaption", Root, "BEST", 34, Palette.TextDim);
            UIKit.Place(bestCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(500f, 40f));

            _bestLabel = UIKit.Label("Best", Root, Format(bestScore), 44, Palette.Accent);
            UIKit.Place(_bestLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(500f, 56f));

            _scoreLabel = UIKit.Label("Score", Root, "0", 118, Palette.TextBright);
            _scoreRect = _scoreLabel.rectTransform;
            UIKit.Place(_scoreRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 140f));

            // Combo badge, hidden until a streak is actually running.
            _comboRect = UIKit.Rect("Combo", Root);
            UIKit.Place(_comboRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(300f, 62f));

            _comboPanel = UIKit.Image("ComboPanel", _comboRect,
                                      ArtKit.RoundedRect("combo", new Color(1f, 0.72f, 0.18f, 0.92f),
                                                         new Color(1f, 1f, 1f, 0.5f), 3f),
                                      Color.white, Image.Type.Sliced);
            RectTransform cp = _comboPanel.rectTransform;
            cp.anchorMin = Vector2.zero;
            cp.anchorMax = Vector2.one;
            cp.offsetMin = Vector2.zero;
            cp.offsetMax = Vector2.zero;

            _comboLabel = UIKit.Label("ComboText", _comboRect, "COMBO x2", 38, new Color(0.15f, 0.08f, 0f));
            RectTransform cl = _comboLabel.rectTransform;
            cl.anchorMin = Vector2.zero;
            cl.anchorMax = Vector2.one;
            cl.offsetMin = Vector2.zero;
            cl.offsetMax = Vector2.zero;

            _comboRect.gameObject.SetActive(false);
        }

        public void ResetForNewRun(long bestScore)
        {
            _bestScore = bestScore;
            _beatenThisRun = false;
            _displayedScore = 0;
            _targetScore = 0;
            _scoreLabel.text = "0";
            _scoreLabel.color = Palette.TextBright;
            _bestLabel.text = Format(bestScore);
            SetCombo(0);
        }

        /// <summary>Jump straight to a value with no roll. Used when a saved run is restored.</summary>
        public void SetScoreImmediate(long score)
        {
            _displayedScore = score;
            _targetScore = score;
            _scoreLabel.text = Format(score);
        }

        public void SetScore(long score)
        {
            _targetScore = score;

            // Roll fast enough that a big clear resolves in well under a second, but always at
            // least a fixed floor so tiny gains still visibly tick.
            long delta = _targetScore - _displayedScore;
            _rollSpeed = Mathf.Max(240f, Mathf.Abs(delta) / 0.55f);

            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(Punch(_scoreRect, 1.14f));
        }

        public void SetCombo(int combo)
        {
            bool show = combo >= 2;
            if (_comboRect.gameObject.activeSelf != show) _comboRect.gameObject.SetActive(show);
            if (!show) return;

            _comboLabel.text = $"COMBO x{combo}";

            // Ramp the badge from amber toward hot pink as the streak grows, so a long combo is
            // visible at a glance without reading the number.
            float t = Mathf.Clamp01((combo - 2) / 8f);
            _comboPanel.color = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.55f, 0.85f, 1f), t);
            StartCoroutine(Punch(_comboRect, 1.2f));
        }

        private void Update()
        {
            if (_displayedScore == _targetScore) return;

            float step = _rollSpeed * Time.deltaTime;
            long delta = _targetScore - _displayedScore;
            long move = (long)Mathf.Max(1f, step);

            if (System.Math.Abs(delta) <= move) _displayedScore = _targetScore;
            else _displayedScore += delta > 0 ? move : -move;

            _scoreLabel.text = Format(_displayedScore);

            // Best tracks the *rolling* score, not the target. Driving it from the target instead
            // made BEST display a number the score had not visibly reached yet, which reads as a
            // bug the first time a player beats their record.
            if (_displayedScore <= _bestScore) return;

            _bestScore = _displayedScore;
            _bestLabel.text = Format(_displayedScore);

            if (_beatenThisRun) return;

            _beatenThisRun = true;
            _bestLabel.color = Palette.Accent;
            StartCoroutine(Punch(_bestLabel.rectTransform, 1.35f));
        }

        private IEnumerator Punch(RectTransform rt, float scale)
        {
            const float duration = 0.18f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = k < 0.4f
                    ? Mathf.Lerp(1f, scale, k / 0.4f)
                    : Mathf.Lerp(scale, 1f, (k - 0.4f) / 0.6f);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>Thousands separators, invariant so a European locale cannot turn 1,000 into 1.000.</summary>
        public static string Format(long value) =>
            value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
