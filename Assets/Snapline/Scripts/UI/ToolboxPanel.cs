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
    /// The store, from `toolbox.png`: the open toolbox with the tools bursting out of it, then one
    /// candy-striped row per power-up — what it does, how many you hold, a green plus and its price —
    /// and a way to earn coins by watching a video.
    ///
    /// RESTORE PURCHASES is deliberately left out. It only means something if there are real-money
    /// purchases to restore, nothing else in the design describes any, and a button that restores
    /// nothing is worse than no button.
    /// </summary>
    public sealed class ToolboxPanel : MonoBehaviour
    {
        private static class Layout
        {
            /// <summary>Low enough to clear the coin pill above it, which the first version ran into.</summary>
            public const float TitleY = 204f;
            public const int TitleSize = 132;
            public const float CoinY = 66f;
            public const float HeroY = 398f;
            public const float SubY = 548f;
            public const float FirstRowY = 752f;
            public const float RowStep = 316f;
            public static readonly Vector2 Row = new Vector2(950f, 292f);
            public const float NoAdsY = 1700f;
            public static readonly Vector2 NoAds = new Vector2(950f, 190f);
            public const float EarnFromBottom = 206f;

            /// <summary>The height this screen is laid out for: a 1080x2340 phone.</summary>
            public const float DesignHeight = 2340f;
        }

        public event Action BackRequested;
        public event Action WatchAdRequested;

        /// <summary>The REMOVE ADS row was tapped.</summary>
        public event Action RemoveAdsRequested;

        private static readonly Tool[] Order = { Tool.Undo, Tool.Shuffle, Tool.Hammer };
        private static readonly string[] Icons = { "icon_undo", "icon_shuffle", "icon_hammer" };
        private static readonly string[] Names = { "UNDO", "SHUFFLE", "HAMMER" };
        private static readonly CandyStyle[] NameStyles = { CandyStyle.Pink, CandyStyle.Purple, CandyStyle.Blue };
        private static readonly string[] Blurbs =
        {
            "Take back your last move",
            "Replace all three pieces",
            "Smash a 3 x 3 area",
        };

        private RectTransform _root;
        private RectTransform _hero;
        private Text _title;
        private readonly RectTransform[] _rows = new RectTransform[3];
        private readonly RectTransform[] _icons = new RectTransform[3];
        private readonly Text[] _owned = new Text[3];
        private readonly Button[] _price = new Button[3];
        private Button _watch;
        private Button _noAds;
        private Text _noAdsPrice;
        private ConfirmPopup _confirm;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>The watch button, so coins paid for a video can fly out of it.</summary>
        public Transform WatchButton => _watch != null ? _watch.transform : null;

        public void Init(RectTransform parent, ConfirmPopup confirm)
        {
            _confirm = confirm;
            _root = FitToDesign(UIKit.Rect("Toolbox", parent));
            Vector2 top = W.Top;

            // Opaque, because the store can open over a run in progress and the board must not show
            // through it.
            Image bg = UIKit.Image("Bg", _root, ArtKit.Background(), Color.white);
            RectTransform bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-200f, -200f);
            bgRect.offsetMax = new Vector2(200f, 200f);
            bg.raycastTarget = true;

            Button back = W.Round("Back", _root, "circle_pink", "sym_back", top, new Vector2(-420f, -100f), 124f, 0.5f);
            back.onClick.AddListener(() => BackRequested?.Invoke());
            CoinPill.Create(_root, top, new Vector2(316f, -Layout.CoinY), 330f, 92f);

            _title = W.Title("Title", _root, "TOOLBOX", top, new Vector2(0f, -Layout.TitleY), Layout.TitleSize);

            W.Starburst(_root, top, new Vector2(0f, -Layout.HeroY), 480f, 18f);
            Image hero = W.Img("Hero", _root, "reward_toolbox_open", top, new Vector2(0f, -Layout.HeroY), new Vector2(360f, 330f));
            // The title stays in front of the burst of tools, as the reference layers it.
            _title.transform.SetAsLastSibling();
            _hero = hero.rectTransform;
            CandyPress heroMotion = hero.gameObject.AddComponent<CandyPress>();
            heroMotion.Bob = 12f;
            heroMotion.Click = false;
            Glint glint = hero.gameObject.AddComponent<Glint>();
            glint.Every = 0.5f;
            glint.Size = 80f;

            Button sub = W.Pill("Sub", _root, "pill_purple", "YOUR POWER-UPS", top, new Vector2(0f, -Layout.SubY),
                                new Vector2(640f, 100f), 48, CandyStyle.OnPurple, sprinkles: true);
            sub.interactable = false;

            // Laid out against the design height rather than the real screen: the rows used to stretch
            // into whatever height was going, which left nowhere to put anything else. The whole screen
            // is scaled to fit instead, so the REMOVE ADS row below has room on every phone.
            float earnTop = _designHeight - Layout.EarnFromBottom - 125f;
            float noAdsCentre = earnTop - 30f - Layout.NoAds.y * 0.5f;
            float lastCentre = noAdsCentre - Layout.NoAds.y * 0.5f - 30f - Layout.Row.y * 0.5f;
            float step = Mathf.Clamp((lastCentre - Layout.FirstRowY) / 2f, 260f, 350f);
            for (int i = 0; i < Order.Length; i++) BuildRow(i, -(Layout.FirstRowY + i * step));

            BuildNoAds(-noAdsCentre);
            BuildEarn();

            _root.gameObject.SetActive(false);
        }

        /// <summary>The height this screen is given: the design height, or more on a taller phone.</summary>
        private float _designHeight = Layout.DesignHeight;

        /// <summary>
        /// Lays the store out on a design-sized rect, shrunk as a whole to fit a shorter screen - the
        /// same treatment the daily screen needs, and for the same reason: too many fixed-height pieces
        /// stacked to fit a 16:9 phone by moving them apart.
        /// </summary>
        private RectTransform FitToDesign(RectTransform rt)
        {
            float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float safeHeight = canvasHeight * Screen.safeArea.height / Mathf.Max(1f, Screen.height);

            _designHeight = Mathf.Max(Layout.DesignHeight, safeHeight);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Design.CanvasWidth, _designHeight);
            rt.localScale = Vector3.one * Mathf.Min(1f, safeHeight / Layout.DesignHeight);
            return rt;
        }

        /// <summary>
        /// The one thing in here that costs real money, and the only row that can disappear: once it
        /// is bought there is nothing left to sell, so the row says so and stops being a button.
        /// </summary>
        private void BuildNoAds(float y)
        {
            Image card = W.Card("NoAds", _root, W.Top, new Vector2(0f, y), Layout.NoAds, 34f);

            W.Img("Icon", card.transform, "icon_noads", W.Left, new Vector2(120f, 0f), new Vector2(140f, 140f));
            W.Text("Name", card.transform, "REMOVE ADS", W.Left, new Vector2(450f, 26f), new Vector2(420f, 70f),
                   56, CandyStyle.Pink, Color.white, TextAnchor.MiddleLeft).font = Design.Display;
            // Short enough to stop before the price pill, which the longer line ran underneath.
            W.Text("Blurb", card.transform, "No ads between levels", W.Left,
                   new Vector2(455f, -32f), new Vector2(430f, 44f), 30, CandyStyle.Cocoa, Color.white,
                   TextAnchor.MiddleLeft);

            _noAds = W.Pill("Price", card.transform, "pill_green", "", W.Right, new Vector2(-140f, 0f),
                            new Vector2(240f, 96f), 44, CandyStyle.OnGreen, shine: true);
            _noAdsPrice = W.Caption(_noAds);
            _noAds.onClick.AddListener(() => RemoveAdsRequested?.Invoke());

            Button whole = CandyUI.SpriteButton("NoAdsTap", card.transform, null);
            whole.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            CandyUI.Place(whole, W.Centre, Vector2.zero, Layout.NoAds);
            whole.transform.SetAsFirstSibling();
            whole.onClick.AddListener(() => RemoveAdsRequested?.Invoke());
        }

        private void BuildRow(int index, float y)
        {
            Image card = W.Card("Row" + index, _root, W.Top, new Vector2(0f, y), Layout.Row, 40f);
            RectTransform row = card.rectTransform;
            _rows[index] = row;

            Image box = W.Rounded("IconBox", row, CandyText.Hex(0xFFF1E0), CandyText.Hex(0xF1D5BD), W.Left,
                                  new Vector2(150f, 0f), new Vector2(200f, 200f), 36f);
            Image icon = W.Img("Icon", box.transform, Icons[index], W.Centre, Vector2.zero, new Vector2(170f, 170f));
            _icons[index] = icon.rectTransform;

            Text name = W.Text("Name", row, Names[index], W.Left, new Vector2(460f, 70f), new Vector2(360f, 84f),
                               68, NameStyles[index], Color.white, TextAnchor.MiddleLeft);
            name.font = Design.Display;

            W.Text("Blurb", row, Blurbs[index], W.Left, new Vector2(500f, 14f), new Vector2(460f, 46f),
                   34, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleLeft);

            Image have = W.Rounded("Have", row, CandyText.Hex(0xFBE3D2), CandyText.Hex(0xF2CDB6), W.Left,
                                   new Vector2(440f, -76f), new Vector2(300f, 76f));
            W.Text("Caption", have.transform, "YOU HAVE", W.Centre, new Vector2(-34f, 2f), new Vector2(200f, 60f),
                   32, CandyStyle.Cocoa, Color.white);
            _owned[index] = W.Text("Count", have.transform, "0", W.Centre, new Vector2(94f, 2f), new Vector2(80f, 70f),
                                   58, CandyStyle.Pink, Color.white);
            _owned[index].font = Design.Display;

            int captured = index;

            Button plus = W.Round("Plus", row, "btn_plus", null, W.Right, new Vector2(-150f, 70f), 118f);
            plus.onClick.AddListener(() => Buy(captured));

            _price[index] = W.Pill("Price", row, "pill_gold", Economy.Price(Order[index]).ToString(), W.Right,
                                   new Vector2(-150f, -72f), new Vector2(270f, 96f), 54, CandyStyle.OnGold, "coin", 78f);
            _price[index].onClick.AddListener(() => Buy(captured));
        }

        private void BuildEarn()
        {
            Image panel = W.Sliced("Earn", _root, "panel_blue", W.Bottom, new Vector2(0f, Layout.EarnFromBottom),
                                   new Vector2(950f, 250f));

            Image coins = W.Img("Coins", panel.transform, "reward_coins", W.Left, new Vector2(150f, 0f), new Vector2(250f, 216f));
            coins.gameObject.AddComponent<Glint>().Every = 0.9f;

            W.Text("Ask", panel.transform, "NEED MORE COINS?", W.Centre, new Vector2(110f, 64f), new Vector2(620f, 70f),
                   54, CandyStyle.White, Color.white).font = Design.Display;

            _watch = W.Pill("Watch", panel.transform, "pill_green", $"WATCH +{Economy.AdReward}", W.Centre,
                            new Vector2(110f, -46f), new Vector2(580f, 118f), 58, CandyStyle.OnGreen, "sym_ad", 86f,
                            shine: true, pulse: 0.025f);
            _watch.onClick.AddListener(() => WatchAdRequested?.Invoke());

            // How many paying videos are left today, as a badge on the button.
            _adsLeft = W.Badge(_watch.transform, "5", new Vector2(1f, 1f), new Vector2(-18f, -14f), 62f);
            _adsLeftText = _adsLeft.GetComponentInChildren<Text>();
        }

        private Image _adsLeft;
        private Text _adsLeftText;

        /// <summary>
        /// The plus and the price ask before they spend. Tapping either used to buy on the spot, so a
        /// stray tap in a store built around big, inviting buttons cost up to 250 coins in silence.
        /// </summary>
        /// <summary>
        /// The price is whatever Google says it is, in the player currency. Until the store answers
        /// there is no price to print, and once the product is owned there is nothing to sell.
        /// </summary>
        private void RefreshNoAds()
        {
            if (_noAds == null) return;

            bool owned = GameKit.GameKitRuntime.AdsRemoved;
            string price = GameKit.GameKitRuntime.Store != null ? GameKit.GameKitRuntime.Store.RemoveAdsPrice : null;

            _noAds.interactable = !owned;
            if (_noAdsPrice != null) _noAdsPrice.text = owned ? "ON" : string.IsNullOrEmpty(price) ? "BUY" : price;
            _noAds.GetComponent<Image>().color = owned ? new Color(0.78f, 0.86f, 0.78f, 1f) : Color.white;
        }

        private void Buy(int index)
        {
            Tool tool = Order[index];

            if (!Wallet.CanAfford(Economy.Price(tool)))
            {
                Refuse(index);
                return;
            }

            if (_confirm == null) Complete(index);
            else _confirm.Ask(tool, () => Complete(index));
        }

        private void Refuse(int index)
        {
            Sound.Deny();
            Tween.Shake((RectTransform)_price[index].transform, 16f, 0.4f);
            Fx.Instance?.Text(_price[index].transform.position, "NOT ENOUGH COINS", CandyStyle.White, 48f, 1.1f, 180f);
        }

        /// <summary>The purchase itself, once it has been agreed to.</summary>
        private void Complete(int index)
        {
            Tool tool = Order[index];
            int price = Economy.Price(tool);

            if (!Wallet.TryBuy(tool))
            {
                Refuse(index);
                return;
            }

            Sound.Purchase();
            Haptics.Medium();
            Fx.Instance?.Text(_price[index].transform.position, $"-{price}", CandyStyle.Gold, 64f, 0.9f, 200f);
            Fx.Instance?.Sparkles(_icons[index].position, 14, 120f, 80f);
            Fx.Instance?.Sprinkles(_icons[index].position, 10, 900f, 26f);
            Tween.PopIn(_icons[index], 0f, 0.45f, 1.5f);
            Tween.Punch(_owned[index].transform, 0.5f, 0.35f);
            Refresh();
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            Tween.PopIn(_title.transform, 0f, 0.5f, 0.3f);
            Tween.PopIn(_hero, 0.08f, 0.6f, 0f);
            for (int i = 0; i < _rows.Length; i++)
                Tween.SlideIn(_rows[i], new Vector2(i % 2 == 0 ? -1100f : 1100f, 0f), 0.12f + i * 0.08f, 0.5f);
            Sound.Swoosh();
            Tween.Delay(0.35f, Sound.Chest);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            RefreshNoAds();

            int left = Wallet.AdRewardsLeftToday;
            if (_adsLeftText != null) _adsLeftText.text = left.ToString();
            if (_adsLeft != null) _adsLeft.gameObject.SetActive(left > 0);
            Text watch = W.Caption(_watch);
            if (watch != null) watch.text = left > 0 ? $"WATCH +{Economy.AdReward}" : "BACK TOMORROW";
            _watch.GetComponent<Image>().color = left > 0 ? Color.white : new Color(0.72f, 0.74f, 0.8f, 1f);

            for (int i = 0; i < Order.Length; i++)
            {
                _owned[i].text = Wallet.Count(Order[i]).ToString();
                _price[i].GetComponent<Image>().color = Wallet.CanAfford(Economy.Price(Order[i]))
                    ? Color.white
                    : new Color(0.8f, 0.78f, 0.8f, 1f);
            }
        }
    }
}
