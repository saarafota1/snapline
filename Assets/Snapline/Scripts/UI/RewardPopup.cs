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
    /// "YOU WON!" — what a finished rewarded video actually paid, and a COLLECT button to take it.
    ///
    /// The video used to pay silently: the coin pill ticked up somewhere above while the player was
    /// still looking at the store, and nothing said what had been earned. A prize is worth showing.
    ///
    /// COLLECT sends the reward flying to wherever it lives on screen — coins into the coin pill, a
    /// tool onto its own button — and the wallet is credited as it lands, so the number the player
    /// watches roll up is the one they were promised.
    /// </summary>
    public sealed class RewardPopup : CandyPopup
    {
        private Image _hero;
        private Image _heroIcon;
        private Text _amount;
        private Text _caption;
        private Button _collect;

        private Sprite _flyIcon;
        private RectTransform _target;
        private int _flyCount;
        private Action _onCollected;
        private bool _taken;

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "Reward", "YOU WON!", new Vector2(860f, 940f), -20f, true, 660f, 84);

            W.Starburst(Card, W.Top, new Vector2(0f, -330f), 560f, 22f);
            _hero = W.Img("Hero", Card, "tile_navy", W.Top, new Vector2(0f, -330f), new Vector2(240f, 240f));
            _heroIcon = W.Img("Icon", _hero.transform, "coin", W.Centre, Vector2.zero, new Vector2(196f, 196f));
            CandyPress bob = _hero.gameObject.AddComponent<CandyPress>();
            bob.Bob = 12f;
            bob.Click = false;
            _hero.gameObject.AddComponent<Glint>().Every = 0.55f;

            _amount = W.Text("Amount", Card, "", W.Top, new Vector2(0f, -530f), new Vector2(800f, 130f),
                             104, CandyStyle.Gold, Color.white);
            _amount.font = Design.Display;

            _caption = W.Text("Caption", Card, "", W.Top, new Vector2(0f, -650f), new Vector2(740f, 80f),
                              44, CandyStyle.Cocoa, Color.white);

            _collect = W.Pill("Collect", Card, "pill_green", "COLLECT", W.Bottom, new Vector2(0f, 140f),
                              new Vector2(580f, 158f), 78, CandyStyle.OnGreen, sprinkles: true, shine: true, pulse: 0.03f);
            _collect.onClick.AddListener(Collect);
        }

        /// <summary>Coins won, flying into the coin pill.</summary>
        public void ShowCoins(int amount, RectTransform target, Action onCollected)
        {
            Show(ArtLoader.Sprite("UI/coin"), $"+{amount}", amount == 1 ? "COIN" : "COINS",
                 target, Mathf.Clamp(amount / 4, 6, 14), onCollected);
        }

        /// <summary>A tool won, flying onto the button it will be used from.</summary>
        public void ShowTool(Tool tool, int count, RectTransform target, Action onCollected)
        {
            string icon = tool switch
            {
                Tool.Undo => "icon_undo",
                Tool.Shuffle => "icon_shuffle",
                _ => "icon_hammer",
            };
            Show(ArtKit.Ui(icon), $"+{count}", tool.ToString().ToUpperInvariant(), target, Mathf.Clamp(count, 1, 3), onCollected);
        }

        private void Show(Sprite icon, string amount, string caption, RectTransform target, int flyCount, Action onCollected)
        {
            _flyIcon = icon;
            _target = target;
            _flyCount = flyCount;
            _onCollected = onCollected;
            _taken = false;

            _heroIcon.sprite = icon;
            _amount.text = amount;
            _caption.text = caption;

            Present();
            Sound.Prize();
            Fx.Instance?.Sparkles(_hero.rectTransform.position, 16, 150f, 80f);
        }

        /// <summary>
        /// Closing the card is not declining the prize. It has been earned by then — a rewarded video
        /// was watched to the end — so the X collects it too rather than throwing it away.
        /// </summary>
        protected override void OnCloseButton() => Collect();

        /// <summary>Presses COLLECT. The smoke harness only.</summary>
        public void CollectForHarness() => Collect();

        private void Collect()
        {
            if (_taken) return;
            _taken = true;

            Sound.Tap();
            Vector3 from = _hero.rectTransform.position;

            Close(() =>
            {
                Action done = _onCollected;
                _onCollected = null;

                Fx fx = Fx.Instance;
                if (fx == null || _target == null)
                {
                    done?.Invoke();
                    return;
                }

                // Credited once, when the first piece lands, so the pill rolls up under the arrivals.
                bool credited = false;
                fx.Fly(from, _target, _flyIcon, _flyCount, 84f, () =>
                {
                    if (credited) return;
                    credited = true;
                    done?.Invoke();
                });
            });
        }
    }
}
