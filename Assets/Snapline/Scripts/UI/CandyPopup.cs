using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The shell every popup in the game shares: a dimmed screen, a cream card with the candy
    /// stripe, a pink ribbon across its top carrying the title, and a close button.
    ///
    /// It arrives the way the references imply it should — the card bounces in, the ribbon drops
    /// onto it a beat later — and leaves quickly, because nobody wants to watch a card leave.
    /// </summary>
    public abstract class CandyPopup : MonoBehaviour
    {
        protected RectTransform Root { get; private set; }
        protected RectTransform Card { get; private set; }
        protected Text TitleLabel { get; private set; }
        protected RectTransform Ribbon { get; private set; }
        protected Button CloseButton { get; private set; }

        private Image _scrim;
        private CanvasGroup _group;
        private const float ScrimAlpha = 0.58f;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        public event Action Opened;

        protected void BuildShell(RectTransform parent, string name, string title, Vector2 cardSize, float cardY = 0f,
                                  bool closeButton = true, float ribbonWidth = 700f, int titleSize = 80)
        {
            Root = UIKit.Stretch(name, parent);
            _group = Root.gameObject.AddComponent<CanvasGroup>();

            _scrim = UIKit.Image("Scrim", Root, ProcArt.Solid(), new Color(0.06f, 0.08f, 0.25f, ScrimAlpha));
            RectTransform sr = _scrim.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(-50f, -50f);
            sr.offsetMax = new Vector2(50f, 50f);
            _scrim.raycastTarget = true;

            Card = UIKit.Rect("Card", Root);
            CandyUI.Place(Card, W.Centre, new Vector2(0f, cardY), cardSize);
            Image bg = W.Card("CardBg", Card, W.Centre, Vector2.zero, cardSize);
            bg.raycastTarget = true;

            Image ribbon = W.Img("Ribbon", Card, "banner_pink", W.Top, new Vector2(0f, 6f),
                                 new Vector2(ribbonWidth, ribbonWidth * 181f / 480f));
            Ribbon = ribbon.rectTransform;

            TitleLabel = W.Text("Title", Ribbon, title, W.Centre, new Vector2(0f, ribbonWidth * 0.045f),
                                new Vector2(ribbonWidth * 1.2f, titleSize * 1.4f), titleSize, CandyStyle.White, Color.white);
            TitleLabel.font = Design.Display;
            CandyText candy = TitleLabel.GetComponent<CandyText>();
            candy.Arc = titleSize * 0.18f;
            candy.Refresh();
            TitleLabel.gameObject.AddComponent<CandyShine>().Period = 2.6f;

            if (closeButton)
            {
                CloseButton = W.Round("Close", Card, "circle_pink", "sym_x", new Vector2(1f, 1f), new Vector2(-10f, -14f), 116f, 0.5f);
                CloseButton.onClick.AddListener(OnCloseButton);
            }

            Root.gameObject.SetActive(false);
        }

        protected void SetTitle(string title)
        {
            if (TitleLabel != null) TitleLabel.text = title;
        }

        /// <summary>What the close button does. Most popups just close; some mean "end the run".</summary>
        protected virtual void OnCloseButton() => Close();

        protected void Present()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();

            _group.alpha = 1f;
            _group.interactable = true;
            Color c = _scrim.color;
            c.a = 0f;
            _scrim.color = c;
            Tween.Run(_scrim, 0.25f, k =>
            {
                Color s = _scrim.color;
                s.a = ScrimAlpha * k;
                _scrim.color = s;
            });

            Card.localScale = Vector3.one * 0.55f;
            Tween.Run(Card, 0.46f, k => Card.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, Ease.OutBack(k, 1.6f)));

            Vector2 ribbonHome = Ribbon.anchoredPosition;
            Ribbon.anchoredPosition = ribbonHome + new Vector2(0f, 180f);
            Ribbon.localScale = Vector3.one * 0.7f;
            Tween.Run(Ribbon, 0.5f, k =>
            {
                Ribbon.anchoredPosition = Vector2.LerpUnclamped(ribbonHome + new Vector2(0f, 180f), ribbonHome, Ease.OutBack(k, 1.4f));
                Ribbon.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, Ease.OutElastic(k));
            }, 0.12f);

            if (CloseButton != null) Tween.PopIn(CloseButton.transform, 0.3f);

            Sound.Open();
            Opened?.Invoke();
        }

        /// <summary>Closes, and runs <paramref name="then"/> once it has gone.</summary>
        public void Close(Action then = null)
        {
            if (!IsVisible)
            {
                then?.Invoke();
                return;
            }

            _group.interactable = false;
            Tween.Run(Card, 0.16f, k =>
            {
                Card.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, k);
                _group.alpha = 1f - k;
            }, 0f, () =>
            {
                Root.gameObject.SetActive(false);
                Card.localScale = Vector3.one;
                _group.alpha = 1f;
                then?.Invoke();
            });
        }

        public void HideNow()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        /// <summary>Pops a child in after the card has landed, for a cascade.</summary>
        protected static void Reveal(Component c, float delay)
        {
            if (c == null) return;
            Tween.PopIn(c.transform, delay, 0.4f, 0.3f);
        }
    }
}
