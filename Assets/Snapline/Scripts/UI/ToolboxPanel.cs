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
            public const float TitleY = 180f;
            public const float HeroY = 398f;
            public const float SubY = 548f;
            public const float FirstRowY = 752f;
            public const float RowStep = 316f;
            public static readonly Vector2 Row = new Vector2(950f, 292f);
            public const float EarnFromBottom = 206f;
        }

        public event Action BackRequested;
        public event Action WatchAdRequested;

        private static readonly Tool[] Order = { Tool.Undo, Tool.Shuffle, Tool.Hammer };
        private static readonly string[] Icons = { "icon_undo", "icon_shuffle", "icon_hammer" };
        private static readonly string[] Names = { "UNDO", "SHUFFLE", "HAMMER" };
        private static readonly CandyStyle[] NameStyles = { CandyStyle.Pink, CandyStyle.Purple, CandyStyle.Blue };
        private static readonly string[] Blurbs =
        {
            "Take back your last move",
            "Replace all three pieces",
            "Remove one block",
        };

        private RectTransform _root;
        private RectTransform _hero;
        private Text _title;
        private readonly RectTransform[] _rows = new RectTransform[3];
        private readonly RectTransform[] _icons = new RectTransform[3];
        private readonly Text[] _owned = new Text[3];
        private readonly Button[] _price = new Button[3];
        private Button _watch;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>The watch button, so coins paid for a video can fly out of it.</summary>
        public Transform WatchButton => _watch != null ? _watch.transform : null;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("Toolbox", parent);
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
            CoinPill.Create(_root, top, new Vector2(316f, -84f), 330f, 92f);

            _title = W.Title("Title", _root, "TOOLBOX", top, new Vector2(0f, -Layout.TitleY), 150);

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

            // Rows spread into a tall screen's extra height, up to a point, instead of leaving it all
            // as one empty band above the coins panel.
            float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float lastCentre = canvasHeight - Layout.EarnFromBottom - 125f - 40f - Layout.Row.y * 0.5f;
            float step = Mathf.Clamp((lastCentre - Layout.FirstRowY) / 2f, Layout.RowStep, 350f);
            for (int i = 0; i < Order.Length; i++) BuildRow(i, -(Layout.FirstRowY + i * step));

            BuildEarn();

            _root.gameObject.SetActive(false);
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
        }

        private void Buy(int index)
        {
            Tool tool = Order[index];
            int price = Economy.Price(tool);

            if (!Wallet.TryBuy(tool))
            {
                Sound.Deny();
                Tween.Shake((RectTransform)_price[index].transform, 16f, 0.4f);
                Fx.Instance?.Text(_price[index].transform.position, "NOT ENOUGH COINS", CandyStyle.White, 48f, 1.1f, 180f);
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
