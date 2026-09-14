using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The three power-ups under or over the tray: UNDO, SHUFFLE and HAMMER, each a glossy bubble
    /// with the tool on it, its name on a pill beneath, and how many you hold in a red badge.
    ///
    /// A tool you hold none of shows a green plus instead of a count — tapping it buys one on the
    /// spot if you can afford it, rather than sending you out of the run to a store.
    /// </summary>
    public sealed class ToolsBar : MonoBehaviour
    {
        public static readonly Tool[] Order = { Tool.Undo, Tool.Shuffle, Tool.Hammer };

        private static readonly string[] Discs = { "circle_pink", "circle_purple", "circle_blue" };
        private static readonly string[] Icons = { "icon_undo", "icon_shuffle", "icon_hammer" };
        private static readonly string[] Pills = { "pill_red", "pill_purple", "pill_blue" };
        private static readonly string[] Names = { "UNDO", "SHUFFLE", "HAMMER" };
        private static readonly CandyStyle[] Styles = { CandyStyle.OnPink, CandyStyle.OnPurple, CandyStyle.OnBlue };
        private static readonly Color[] Glows =
        {
            new Color(1f, 0.45f, 0.75f, 0.9f), new Color(0.7f, 0.5f, 1f, 0.9f), new Color(0.4f, 0.85f, 1f, 0.9f),
        };

        public const float Size = 176f;

        public event Action<Tool> ToolPressed;

        public RectTransform Root { get; private set; }

        private readonly RectTransform[] _slots = new RectTransform[3];
        private readonly Button[] _buttons = new Button[3];
        private readonly Image[] _badges = new Image[3];
        private readonly Text[] _counts = new Text[3];
        private readonly Image[] _plus = new Image[3];
        private readonly Image[] _glows = new Image[3];

        public void Init(RectTransform parent)
        {
            Root = UIKit.Rect("Tools", parent);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.sizeDelta = new Vector2(1000f, 260f);

            for (int i = 0; i < 3; i++)
            {
                RectTransform slot = UIKit.Rect(Names[i], Root);
                CandyUI.Place(slot, W.Centre, Vector2.zero, new Vector2(220f, 250f));
                _slots[i] = slot;

                Image glow = UIKit.Image("Glow", slot, Fx.GlowSprite(), Glows[i]);
                glow.raycastTarget = false;
                CandyUI.Place(glow, W.Centre, new Vector2(0f, 24f), new Vector2(Size * 1.9f, Size * 1.9f));
                glow.gameObject.AddComponent<Breathe>().Speed = 5f;
                glow.gameObject.SetActive(false);
                _glows[i] = glow;

                Button b = W.Round("Button", slot, Discs[i], Icons[i], W.Centre, new Vector2(0f, 24f), Size, 0.64f);
                int captured = i;
                b.onClick.AddListener(() => ToolPressed?.Invoke(Order[captured]));
                _buttons[i] = b;

                Image pill = W.Sliced("Label", slot, Pills[i], W.Centre, new Vector2(0f, -84f), new Vector2(Size * 1.02f, 58f));
                pill.raycastTarget = false;
                Text name = W.Text("Name", pill.transform, Names[i], W.Centre, new Vector2(0f, 2f), new Vector2(240f, 56f),
                                   32, Styles[i], Color.white);
                name.font = Design.Display;

                _badges[i] = W.Badge(b.transform, "x0", new Vector2(1f, 1f), new Vector2(-16f, -18f), 66f);
                _counts[i] = _badges[i].GetComponentInChildren<Text>();
                _plus[i] = W.Img("Plus", b.transform, "btn_plus", new Vector2(1f, 1f), new Vector2(-16f, -18f), new Vector2(66f, 66f));
            }
        }

        /// <summary>Centres the row at a height above the bottom of the screen, with a gap between tools.</summary>
        public void Place(float centreFromBottom, float spacing, float scale)
        {
            Root.anchoredPosition = new Vector2(0f, centreFromBottom);
            Root.localScale = Vector3.one * scale;
            for (int i = 0; i < 3; i++) _slots[i].anchoredPosition = new Vector2((i - 1) * spacing, 0f);
        }

        /// <summary>Counts straight off the wallet.</summary>
        public void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                int n = Wallet.Count(Order[i]);
                _badges[i].gameObject.SetActive(n > 0);
                _plus[i].gameObject.SetActive(n <= 0);
                _counts[i].text = "x" + (n > 99 ? 99 : n);
            }
        }

        /// <summary>Lights one tool up while it is waiting for a target — the hammer, choosing a block.</summary>
        public void SetArmed(Tool? tool)
        {
            for (int i = 0; i < 3; i++)
            {
                bool armed = tool.HasValue && Order[i] == tool.Value;
                _glows[i].gameObject.SetActive(armed);
                CandyPress press = _buttons[i].GetComponent<CandyPress>();
                if (press != null) press.Pulse = armed ? 0.06f : 0f;
            }
        }

        public RectTransform ButtonRect(Tool tool) => (RectTransform)_buttons[Array.IndexOf(Order, tool)].transform;

        private void OnEnable()
        {
            Wallet.Changed += Refresh;
            if (_badges[0] != null) Refresh();
        }

        private void OnDisable() => Wallet.Changed -= Refresh;
    }
}
