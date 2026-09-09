using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using Snapline.Core;
using Snapline.App;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The store: three power-ups, what you own, what they cost, and a way to earn more coins.
    ///
    /// Laid out from `Design/references/toolbox.png`. The one thing in that reference deliberately
    /// left out is RESTORE PURCHASES, which only means anything if there are real-money purchases to
    /// restore — and nothing else in the design describes any. A button that restores nothing is
    /// worse than no button, and adding IAP changes both the store listing and the Data safety form.
    /// </summary>
    public sealed class ToolboxPanel : MonoBehaviour
    {
        private RectTransform _root;
        private Text _coinLabel;
        private readonly Text[] _owned = new Text[3];
        private readonly Button[] _buy = new Button[3];

        public event Action BackRequested;

        /// <summary>Raised when the player asks to watch an ad for coins.</summary>
        public event Action WatchAdRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        private static readonly Tool[] Order = { Tool.Undo, Tool.Shuffle, Tool.Hammer };

        private static readonly string[] Icons = { "icon_undo", "icon_shuffle", "icon_hammer" };
        private static readonly string[] Names = { "UNDO", "SHUFFLE", "HAMMER" };

        private static readonly string[] Blurbs =
        {
            "Take back your last move",
            "Replace all three pieces",
            "Remove one block",
        };

        /// <summary>Each tool's name colour, matching the reference.</summary>
        private static readonly Color[] Tints =
        {
            new Color(0.93f, 0.16f, 0.42f, 1f),
            new Color(0.45f, 0.20f, 0.85f, 1f),
            new Color(0.13f, 0.55f, 0.92f, 1f),
        };

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("Toolbox", parent);

            var top = new Vector2(0.5f, 1f);

            Button back = CandyUI.SpriteButton("Back", _root, ArtKit.Ui("circle_pink"));
            CandyUI.Place(back, top, new Vector2(-438f, -84f), new Vector2(116f, 116f));
            CandyUI.Place(CandyUI.Icon("Sym", back.transform, ArtKit.Ui("sym_back")),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            Image coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            coinPill.type = Image.Type.Sliced;
            coinPill.preserveAspect = false;
            CandyUI.Place(coinPill, top, new Vector2(230f, -84f), new Vector2(300f, 88f));
            CandyUI.Place(CandyUI.Icon("Coin", coinPill.transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(66f, 66f));
            _coinLabel = CandyUI.Label("Coins", coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(190f, 58f));

            CandyUI.Place(CandyUI.Label("Title", _root, "TOOLBOX", 92, CandyUI.Caption),
                          top, new Vector2(0f, -216f), new Vector2(900f, 110f));

            CandyUI.Place(CandyUI.Icon("Hero", _root, ArtKit.Ui("reward_toolbox_open")),
                          top, new Vector2(0f, -400f), new Vector2(300f, 260f));

            Image sub = CandyUI.Icon("SubPill", _root, ArtKit.Ui("pill_purple"));
            sub.type = Image.Type.Sliced;
            sub.preserveAspect = false;
            CandyUI.Place(sub, top, new Vector2(0f, -556f), new Vector2(520f, 86f));
            CandyUI.Place(CandyUI.Label("SubText", sub.transform, "YOUR POWER-UPS", 40, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 56f));

            for (int i = 0; i < Order.Length; i++) BuildRow(i, -700f - i * 236f);

            BuildEarnRow(-1420f);

            _root.gameObject.SetActive(false);
        }

        /// <summary>One store row: icon, name, blurb, how many you own, and the price.</summary>
        private void BuildRow(int index, float y)
        {
            var top = new Vector2(0.5f, 1f);

            Image row = CandyUI.Icon($"Row{index}", _root, ArtKit.Ui("row_cream"));
            row.type = Image.Type.Sliced;
            row.preserveAspect = false;
            CandyUI.Place(row, top, new Vector2(0f, y), new Vector2(940f, 210f));

            CandyUI.Place(CandyUI.Icon("Icon", row.transform, ArtKit.Ui(Icons[index])),
                          new Vector2(0f, 0.5f), new Vector2(122f, 0f), new Vector2(130f, 130f));

            Text name = CandyUI.Label("Name", row.transform, Names[index], 54, Tints[index],
                                      TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(name, new Vector2(0f, 1f), new Vector2(440f, -62f), new Vector2(340f, 60f));

            Text blurb = CandyUI.Label("Blurb", row.transform, Blurbs[index], 30, CandyUI.CaptionOnYellow,
                                       TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(blurb, new Vector2(0f, 1f), new Vector2(470f, -110f), new Vector2(400f, 40f));

            _owned[index] = CandyUI.Label("Owned", row.transform, "YOU HAVE 0", 32, CandyUI.CaptionOnYellow,
                                          TextAnchor.MiddleLeft, outline: false);
            CandyUI.Place(_owned[index], new Vector2(0f, 0f), new Vector2(450f, 52f), new Vector2(360f, 44f));

            // Price, which is also the buy button — the reference draws the green plus and the price
            // tag as separate shapes but they do the same thing, and two hit targets for one action
            // is a way to make a player think they missed.
            _buy[index] = CandyUI.SpriteButton($"Buy{index}", row.transform, ArtKit.Ui("pill_gold"),
                                               Image.Type.Sliced);
            CandyUI.Place(_buy[index], new Vector2(1f, 0f), new Vector2(-140f, 62f), new Vector2(210f, 80f));
            CandyUI.Place(CandyUI.Icon("Coin", _buy[index].transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(58f, 58f));
            CandyUI.Place(CandyUI.Label("Price", _buy[index].transform, Economy.Price(Order[index]).ToString(),
                                        40, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(140f, 50f));

            int captured = index;
            _buy[index].onClick.AddListener(() => Buy(captured));

            Button plus = CandyUI.SpriteButton($"Plus{index}", row.transform, ArtKit.Ui("btn_plus"));
            CandyUI.Place(plus, new Vector2(1f, 1f), new Vector2(-160f, -66f), new Vector2(86f, 86f));
            plus.onClick.AddListener(() => Buy(captured));
        }

        private void BuildEarnRow(float y)
        {
            var top = new Vector2(0.5f, 1f);

            Image panel = CandyUI.Icon("Earn", _root, ArtKit.Ui("panel_blue"));
            panel.type = Image.Type.Sliced;
            panel.preserveAspect = false;
            CandyUI.Place(panel, top, new Vector2(0f, y), new Vector2(940f, 200f));

            CandyUI.Place(CandyUI.Icon("Coins", panel.transform, ArtKit.Ui("reward_coins")),
                          new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(200f, 160f));

            CandyUI.Place(CandyUI.Label("Ask", panel.transform, "NEED MORE COINS?", 42, CandyUI.Caption),
                          new Vector2(0.5f, 1f), new Vector2(80f, -52f), new Vector2(560f, 56f));

            Button watch = CandyUI.SpriteButton("Watch", panel.transform, ArtKit.Ui("pill_green"),
                                                Image.Type.Sliced);
            CandyUI.Place(watch, new Vector2(0.5f, 0f), new Vector2(80f, 58f), new Vector2(520f, 92f));
            CandyUI.Place(CandyUI.Icon("Film", watch.transform, ArtKit.Ui("sym_ad")),
                          new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(74f, 74f));
            CandyUI.Place(CandyUI.Label("Text", watch.transform, $"WATCH  +{Economy.AdReward}", 42,
                                        CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(380f, 54f));
            watch.onClick.AddListener(() => WatchAdRequested?.Invoke());
        }

        private void Buy(int index)
        {
            Tool tool = Order[index];

            // Refusing loudly beats a button that sometimes silently does nothing. There is nowhere
            // else to send them yet — the only way to earn is the ad below, already on this screen.
            if (!Wallet.TryBuy(tool))
            {
                Debug.Log($"[Snapline] cannot afford {tool} ({Economy.Price(tool)}), balance {Wallet.Coins}");
                return;
            }

            Refresh();
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Pulls every number from the wallet. Called on open and after any purchase.</summary>
        public void Refresh()
        {
            if (_coinLabel == null) return;

            _coinLabel.text = Hud.Format(Wallet.Coins);

            for (int i = 0; i < Order.Length; i++)
            {
                _owned[i].text = $"YOU HAVE {Wallet.Count(Order[i])}";

                // Unaffordable prices are dimmed rather than disabled: the row still explains what
                // the tool does and what it costs, which is the point of a store.
                var img = _buy[i].GetComponent<Image>();
                img.color = Wallet.CanAfford(Economy.Price(Order[i]))
                    ? Color.white
                    : new Color(0.72f, 0.72f, 0.74f, 1f);
            }
        }
    }
}
