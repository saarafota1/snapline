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
    /// "BUY THIS?" — the card that stands between a tap and spent coins.
    ///
    /// Every purchase in the game used to happen on the tap that asked for it: tapping a tool you did
    /// not hold bought one, and the store's plus button bought one, both without a word. A hammer is
    /// 250 coins and several runs' earnings, and the tool buttons sit right beside the tray, so the
    /// first thing a player knew about a purchase was the coins missing afterwards.
    ///
    /// The card names the tool, what it does, what it costs and what is left after, so buying is a
    /// decision rather than an accident.
    /// </summary>
    public sealed class ConfirmPopup : CandyPopup
    {
        private static readonly Tool[] Order = { Tool.Undo, Tool.Shuffle, Tool.Hammer };
        private static readonly string[] Icons = { "icon_undo", "icon_shuffle", "icon_hammer" };
        private static readonly string[] Names = { "UNDO", "SHUFFLE", "HAMMER" };
        private static readonly CandyStyle[] NameStyles = { CandyStyle.Pink, CandyStyle.Purple, CandyStyle.Blue };

        private Image _hero;
        private Image _heroIcon;
        private Text _name;
        private Text _blurb;
        private Text _price;
        private Text _after;
        private Button _buy;
        private Button _cancel;

        private Action _confirmed;
        private Action _closed;
        private Tool _tool;

        public void Init(RectTransform parent)
        {
            // Tall enough that BUY clears the cost panel: at 1020 it sat over the "left after" line,
            // hiding the one number the decision needs.
            BuildShell(parent, "Confirm", "BUY THIS?", new Vector2(880f, 1140f), -20f, true, 660f, 80);

            W.Starburst(Card, W.Top, new Vector2(0f, -300f), 480f, 18f);
            _hero = W.Img("Hero", Card, "tile_navy", W.Top, new Vector2(0f, -300f), new Vector2(210f, 210f));
            _heroIcon = W.Img("Icon", _hero.transform, "icon_hammer", W.Centre, Vector2.zero, new Vector2(170f, 170f));
            CandyPress bob = _hero.gameObject.AddComponent<CandyPress>();
            bob.Bob = 9f;
            bob.Click = false;
            _hero.gameObject.AddComponent<Glint>().Every = 0.8f;

            _name = W.Text("Name", Card, "", W.Top, new Vector2(0f, -470f), new Vector2(800f, 96f),
                           72, CandyStyle.Blue, Color.white);
            _name.font = Design.Display;

            _blurb = W.Text("Blurb", Card, "", W.Top, new Vector2(0f, -572f), new Vector2(720f, 70f),
                            38, CandyStyle.Cocoa, Color.white);
            _blurb.horizontalOverflow = HorizontalWrapMode.Wrap;

            // The price, and what the purchase leaves behind: the two numbers the decision is made on.
            Image panel = W.Rounded("Cost", Card, CandyText.Hex(0xFBE5D3), CandyText.Hex(0xF1CDB5), W.Top,
                                    new Vector2(0f, -700f), new Vector2(720f, 160f), 36f);
            W.Img("Coin", panel.transform, "coin", W.Left, new Vector2(110f, 26f), new Vector2(86f, 86f));
            _price = W.Text("Price", panel.transform, "", W.Left, new Vector2(300f, 26f), new Vector2(320f, 86f),
                            66, CandyStyle.Gold, Color.white, TextAnchor.MiddleLeft);
            _price.font = Design.Display;
            _after = W.Text("After", panel.transform, "", W.Bottom, new Vector2(0f, 18f), new Vector2(660f, 48f),
                            32, CandyStyle.Cocoa, Color.white);

            _buy = W.Pill("Buy", Card, "pill_green", "BUY", W.Bottom, new Vector2(0f, 250f), new Vector2(560f, 150f),
                          74, CandyStyle.OnGreen, "coin", 82f, sprinkles: true, shine: true, pulse: 0.025f);
            _buy.onClick.AddListener(Confirm);

            _cancel = W.Pill("Cancel", Card, "pill_white", "NO, THANKS", W.Bottom, new Vector2(0f, 100f),
                             new Vector2(460f, 104f), 46, CandyStyle.Blue, tint: W.CandyBlue);
            _cancel.onClick.AddListener(() => Close(Finish));
        }

        /// <summary>
        /// Asks before spending. <paramref name="onConfirm"/> runs only on BUY; <paramref name="onClosed"/>
        /// runs however the card leaves, so a caller that suspended input can restore it exactly once.
        /// </summary>
        public void Ask(Tool tool, Action onConfirm, Action onClosed = null)
        {
            _tool = tool;
            _confirmed = onConfirm;
            _closed = onClosed;

            int index = Array.IndexOf(Order, tool);
            int price = Economy.Price(tool);

            _heroIcon.sprite = ArtKit.Ui(Icons[index]);
            _name.text = Names[index];
            CandyText.Apply(_name, NameStyles[index]);
            _blurb.text = Blurb(tool);
            _price.text = price.ToString();
            _after.text = $"YOU HAVE {Wallet.Coins}  •  {Mathf.Max(0, Wallet.Coins - price)} LEFT AFTER";

            Present();
        }

        /// <summary>What the tool does, in the words the store uses for it.</summary>
        private static string Blurb(Tool tool) => tool switch
        {
            Tool.Undo => "Takes back your last move.",
            Tool.Shuffle => "Replaces all three pieces.",
            _ => "Smashes a 3 x 3 area of blocks.",
        };

        protected override void OnCloseButton() => Close(Finish);

        /// <summary>Presses BUY. The smoke harness only.</summary>
        public void ConfirmForHarness() => Confirm();

        private void Confirm()
        {
            Action run = _confirmed;
            _confirmed = null;
            Sound.Tap();
            Close(() =>
            {
                run?.Invoke();
                Finish();
            });
        }

        /// <summary>Runs the caller's "the card has gone" hook once, whichever way it went.</summary>
        private void Finish()
        {
            Action closed = _closed;
            _closed = null;
            _confirmed = null;
            closed?.Invoke();
        }
    }
}
