using System;
using UnityEngine;
using UnityEngine.UI;
using GameKit;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// REMOVE ADS: the one thing the game sells.
    ///
    /// It says exactly what the money buys and what it does not. The ads between levels and after a
    /// run go; the videos a player chooses to watch for coins or a continue stay, because those are
    /// the player own benefit and taking them away for paying would be a punishment.
    ///
    /// The price is whatever Google says it is in the player currency. A hard-coded one is wrong in
    /// most countries, and wrong in a way that ends in refund requests.
    /// </summary>
    public sealed class RemoveAdsPopup : CandyPopup
    {
        public event Action Changed;

        private Text _blurb;
        private Text _status;
        private Button _buy;
        private Text _buyLabel;
        private Button _restore;

        public void Init(RectTransform parent)
        {
            // Tall enough for the status line to clear the BUY button: at 1040 the button sat over
            // the one sentence that explains why the button says UNAVAILABLE.
            BuildShell(parent, "RemoveAds", "REMOVE ADS", new Vector2(880f, 1140f), -20f, true, 700f, 76);

            W.Starburst(Card, W.Top, new Vector2(0f, -300f), 480f, 16f);
            Image hero = W.Img("Hero", Card, "icon_noads", W.Top, new Vector2(0f, -300f), new Vector2(230f, 230f));
            hero.gameObject.AddComponent<Glint>().Every = 0.9f;

            _blurb = W.Text("Blurb", Card, "No more ads between levels or after a run.", W.Top,
                            new Vector2(0f, -470f), new Vector2(740f, 130f), 44, CandyStyle.Cocoa, Color.white);
            _blurb.horizontalOverflow = HorizontalWrapMode.Wrap;

            W.Text("Keep", Card, "Videos you choose to watch for coins or a second chance stay.", W.Top,
                   new Vector2(0f, -610f), new Vector2(720f, 120f), 32, CandyStyle.Cocoa, Color.white)
             .horizontalOverflow = HorizontalWrapMode.Wrap;

            _status = W.Text("Status", Card, "", W.Top, new Vector2(0f, -700f), new Vector2(720f, 70f),
                             34, CandyStyle.Pink, Color.white);

            _buy = W.Pill("Buy", Card, "pill_green", "BUY", W.Bottom, new Vector2(0f, 240f),
                          new Vector2(600f, 156f), 68, CandyStyle.OnGreen, sprinkles: true, shine: true, pulse: 0.025f);
            _buyLabel = W.Caption(_buy);
            _buy.onClick.AddListener(Buy);

            _restore = W.Pill("Restore", Card, "pill_white", "RESTORE PURCHASE", W.Bottom, new Vector2(0f, 92f),
                              new Vector2(520f, 104f), 40, CandyStyle.Blue, tint: W.CandyBlue);
            _restore.onClick.AddListener(Restore);
        }

        public void Show()
        {
            Refresh();
            Present();
        }

        /// <summary>
        /// The button says what the store actually offers. Until Google answers there is no price to
        /// print, and a button that says BUY with no price is how a player ends up surprised.
        /// </summary>
        private void Refresh()
        {
            bool owned = GameKitRuntime.AdsRemoved;
            IStoreService store = GameKitRuntime.Store;
            string price = store != null ? store.RemoveAdsPrice : null;

            _restore.gameObject.SetActive(!owned);
            _buy.gameObject.SetActive(!owned);

            if (owned)
            {
                _status.text = "Thank you! Ads between levels are off.";
                return;
            }

            bool sellable = store != null && !string.IsNullOrEmpty(price);
            _buy.interactable = sellable;
            _buy.GetComponent<Image>().color = sellable ? Color.white : new Color(0.78f, 0.82f, 0.78f, 1f);
            if (_buyLabel != null) _buyLabel.text = sellable ? price : "UNAVAILABLE";
            _status.text = sellable ? string.Empty : "The store is not reachable right now.";
        }

        private async void Buy()
        {
            _buy.interactable = false;
            _status.text = "Talking to the store...";

            PurchaseResult result = await GameKitRuntime.Store.BuyRemoveAdsAsync();
            switch (result)
            {
                case PurchaseResult.Purchased:
                case PurchaseResult.AlreadyOwned:
                    Sound.Prize();
                    Fx.Instance?.Confetti(60);
                    Changed?.Invoke();
                    Refresh();
                    break;

                case PurchaseResult.Cancelled:
                    // Backing out is not an error and gets no scolding.
                    Refresh();
                    break;

                default:
                    Sound.Deny();
                    _status.text = "That did not go through. Nothing was charged.";
                    Refresh();
                    break;
            }
        }

        private async void Restore()
        {
            _restore.interactable = false;
            _status.text = "Checking your purchases...";

            bool owned = await GameKitRuntime.Store.RestoreAsync();
            _restore.interactable = true;

            if (owned)
            {
                Sound.Prize();
                Changed?.Invoke();
            }
            else
            {
                _status.text = "Nothing to restore on this account.";
            }

            Refresh();
        }
    }
}
