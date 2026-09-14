using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// Navigation asked for from places that do not own screens — the coin pill's plus and the
    /// toolbox icon appear on nearly every screen, and every one of them opens the same store.
    /// </summary>
    public static class Nav
    {
        public static event Action ToolboxRequested;

        public static void OpenToolbox() => ToolboxRequested?.Invoke();
    }

    /// <summary>
    /// Builders for the candy interface's recurring pieces, so each screen reads as layout rather
    /// than as a wall of Image and RectTransform setup.
    /// </summary>
    public static class W
    {
        public static readonly Vector2 Top = new Vector2(0.5f, 1f);
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        public static readonly Vector2 Left = new Vector2(0f, 0.5f);
        public static readonly Vector2 Right = new Vector2(1f, 0.5f);

        /// <summary>
        /// The component on an object, added if it is not there yet.
        ///
        /// Never write <c>GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()</c>. In the editor a missing
        /// component comes back as a fake-null object that Unity's == calls null but C#'s ?? does not,
        /// so ?? keeps the fake, the next line throws MissingComponentException, and whatever was
        /// opening stops half way. That froze the NO MORE MOVES card in the editor while every player
        /// build — where the null is real — worked.
        /// </summary>
        public static T Ensure<T>(Component on) where T : Component =>
            on.TryGetComponent(out T existing) ? existing : on.gameObject.AddComponent<T>();

        /// <summary>A plain sprite, letterboxed inside its box.</summary>
        public static Image Img(string name, Transform parent, string ui, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image img = CandyUI.Icon(name, parent, ArtKit.Ui(ui));
            CandyUI.Place(img, anchor, pos, size);
            return img;
        }

        /// <summary>
        /// A nine-sliced pill or tile, with its corners scaled to the height it is drawn at.
        ///
        /// Without the scale a sliced sprite keeps its caps at their pixel size whatever height it is
        /// drawn — a 220-pixel pill drawn 150 tall ends up with caps wider than it is high, which is
        /// exactly the squashed look these buttons had before.
        /// </summary>
        public static Image Sliced(string name, Transform parent, string ui, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image img = CandyUI.Icon(name, parent, ArtKit.Ui(ui));
            img.preserveAspect = false;
            img.type = Image.Type.Sliced;
            CandyUI.Place(img, anchor, pos, size);
            FitSlices(img, size);
            return img;
        }

        public static void FitSlices(Image img, Vector2 size)
        {
            if (img == null || img.sprite == null) return;
            Rect r = img.sprite.rect;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, Mathf.Min(r.width, r.height) / Mathf.Max(1f, Mathf.Min(size.x, size.y)));
        }

        /// <summary>
        /// A cream card with the candy-stripe border, at any size.
        ///
        /// Generated at the size it is drawn rather than stretched from the sheet. Both ways of
        /// stretching the delivered card failed on screen: sliced, the diagonal stripes along a long
        /// edge smear into streaks; tiled, the cream middle repeats its own edge shading and the card
        /// shows a grid of seams. A generated frame has straight stripes the full length of every side.
        /// </summary>
        public static Image Card(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float stripe = 50f)
        {
            Image img = UIKit.Image(name, parent, CardSprite(size, stripe), Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;
            CandyUI.Place(img, anchor, pos, size);
            return img;
        }

        /// <summary>Replaces a card's frame after a resize, so the stripe stays its drawn thickness.</summary>
        public static void ResizeCard(Image card, Vector2 size, float stripe = 50f)
        {
            if (card == null) return;
            card.rectTransform.sizeDelta = size;
            card.sprite = CardSprite(size, stripe);
        }

        private static readonly Dictionary<string, Sprite> Cards = new Dictionary<string, Sprite>();

        /// <summary>
        /// The candy card: a glossy pink-and-white striped tube around a cream panel with a soft inner
        /// shadow. Rendered at half the drawn resolution — the art is soft gradients and bilinear
        /// filtering hides the difference — and cached per size.
        /// </summary>
        public static Sprite CardSprite(Vector2 size, float stripe)
        {
            const float scale = 0.5f;
            int w = Mathf.Max(8, Mathf.CeilToInt(size.x * scale));
            int h = Mathf.Max(8, Mathf.CeilToInt(size.y * scale));
            string key = $"card:{w}x{h}:{stripe:F0}";
            if (Cards.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            float s = stripe * scale;
            float radius = Mathf.Min(s * 1.5f, Mathf.Min(w, h) * 0.5f - 1f);
            float period = s * 1.3f;

            Color pink = CandyText.Hex(0xFF52A8);
            Color white = CandyText.Hex(0xFFF6FB);
            Color rimOuter = CandyText.Hex(0xD62C86);
            Color rimInner = CandyText.Hex(0xF06AAE);
            Color cream = CandyText.Hex(0xFFF6E6);
            Color creamShade = CandyText.Hex(0xF6E0CC);

            var px = new Color32[w * h];
            float halfW = w * 0.5f - 1f;
            float halfH = h * 0.5f - 1f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f;
                    float cy = y + 0.5f - h * 0.5f;
                    float d = ProcArt.RoundedBoxDistance(cx, cy, halfW, halfH, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) continue;

                    float depth = -d;
                    Color c;

                    if (depth < s)
                    {
                        float t = Mathf.Clamp01(depth / s);
                        float f = (x + y) / period;
                        f -= Mathf.Floor(f);
                        float stripeMix = Mathf.Clamp01(0.5f + Mathf.Sin(f * Mathf.PI * 2f) * period * 0.18f);
                        c = Color.Lerp(white, pink, stripeMix);

                        // A tube: brightest along its middle, darker at both edges.
                        float shade = 0.84f + 0.2f * Mathf.Sin(t * Mathf.PI);
                        c *= shade;
                        float gloss = Mathf.Exp(-Mathf.Pow((t - 0.34f) / 0.13f, 2f)) * (cy > 0f ? 0.35f : 0.18f);
                        c = Color.Lerp(c, Color.white, gloss);

                        if (t < 0.1f) c = Color.Lerp(rimOuter, c, t / 0.1f);
                        if (t > 0.9f) c = Color.Lerp(c, rimInner, (t - 0.9f) / 0.1f);
                    }
                    else
                    {
                        float k = Mathf.Clamp01(1f - (depth - s) / (s * 0.45f));
                        c = Color.Lerp(cream, creamShade, k * k * 0.8f);
                    }

                    c.a = alpha;
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f * scale, 0,
                                          SpriteMeshType.FullRect);
            sprite.name = key;
            Cards[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The bright candy blue of the reference's secondary buttons. There is no blue pill in the
        /// delivered sheets — `pill_blue` is the deep navy of the score readouts, `pill_teal` reads as
        /// green — so the white pill is tinted: its highlights stay lighter than its body, which is
        /// what keeps a tinted gloss looking glossy.
        /// </summary>
        public static readonly Color CandyBlue = new Color(0.36f, 0.66f, 1f, 1f);

        /// <summary>A label, optionally with 3D candy lettering.</summary>
        public static Text Text(string name, Transform parent, string content, Vector2 anchor, Vector2 pos, Vector2 box,
                                int size, CandyStyle? style, Color colour, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Text label = CandyUI.Label(name, parent, content, size, colour, align, outline: false);
            CandyUI.Place(label, anchor, pos, box);
            if (style.HasValue) CandyText.Apply(label, style.Value);
            return label;
        }

        /// <summary>A screen title in big 3D lettering, with a highlight sweeping across it.</summary>
        public static Text Title(string name, Transform parent, string content, Vector2 anchor, Vector2 pos, int size,
                                 CandyStyle style = CandyStyle.White, bool shine = true)
        {
            Text label = Text(name, parent, content, anchor, pos, new Vector2(1060f, size * 1.4f), size, style, Color.white);
            label.font = Design.Display;
            if (shine) label.gameObject.AddComponent<CandyShine>();
            return label;
        }

        /// <summary>A round candy button with a symbol on it.</summary>
        public static Button Round(string name, Transform parent, string disc, string symbol, Vector2 anchor, Vector2 pos,
                                   float size, float symbolScale = 0.56f)
        {
            Button b = CandyUI.SpriteButton(name, parent, ArtKit.Ui(disc));
            b.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(b, anchor, pos, new Vector2(size, size));
            if (!string.IsNullOrEmpty(symbol))
                CandyUI.Place(CandyUI.Icon("Sym", b.transform, ArtKit.Ui(symbol)), Centre, Vector2.zero,
                              new Vector2(size * symbolScale, size * symbolScale));
            return b;
        }

        /// <summary>
        /// A glossy pill or tile button with a 3D caption, an optional icon before it, optional
        /// sprinkles at its ends, a shine that sweeps across it, and a breathing pulse.
        /// </summary>
        public static Button Pill(string name, Transform parent, string sprite, string caption, Vector2 anchor, Vector2 pos,
                                  Vector2 size, int fontSize, CandyStyle style, string icon = null, float iconSize = 0f,
                                  bool sprinkles = false, bool shine = false, float pulse = 0f, Color? tint = null)
        {
            Button b = CandyUI.SpriteButton(name, parent, ArtKit.Ui(sprite), Image.Type.Sliced);
            Image img = b.GetComponent<Image>();
            img.preserveAspect = false;
            if (tint.HasValue) img.color = tint.Value;
            CandyUI.Place(b, anchor, pos, size);
            FitSlices(img, size);

            float captionShift = 0f;
            if (!string.IsNullOrEmpty(icon))
            {
                if (iconSize <= 0f) iconSize = size.y * 0.62f;
                captionShift = iconSize * 0.42f;
            }

            Text label = Text("Caption", b.transform, caption, Centre, new Vector2(captionShift, size.y * 0.03f),
                              new Vector2(size.x, size.y), fontSize, style, Color.white);
            label.font = Design.Display;

            if (!string.IsNullOrEmpty(icon))
            {
                float textHalf = Mathf.Min(label.preferredWidth, size.x * 0.7f) * 0.5f;
                float x = captionShift - textHalf - iconSize * 0.62f;
                x = Mathf.Max(x, -size.x * 0.5f + size.y * 0.5f);
                CandyUI.Place(CandyUI.Icon("Icon", b.transform, ArtKit.Ui(icon)), Centre, new Vector2(x, 0f),
                              new Vector2(iconSize, iconSize));
            }

            if (sprinkles) Sprinkles((RectTransform)b.transform, size);
            if (shine) ShineSweep.Add(img);

            CandyPress press = b.GetComponent<CandyPress>();
            if (press != null) press.Pulse = pulse;

            return b;
        }

        public static Text Caption(Button b) => b != null ? b.transform.Find("Caption")?.GetComponent<Text>() : null;

        /// <summary>Candy sprinkles scattered over the rounded ends of a button, as the references have.</summary>
        public static void Sprinkles(RectTransform on, Vector2 size)
        {
            var rng = new System.Random(on.name.GetHashCode());
            float capX = size.x * 0.5f - size.y * 0.42f;
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                float x = side * (capX + (float)(rng.NextDouble() - 0.5) * size.y * 0.5f);
                float y = (float)(rng.NextDouble() - 0.5) * size.y * 0.6f;
                Image s = UIKit.Image("Sprinkle", on, Fx.CapsuleSprite(), Fx.CandyColours[rng.Next(Fx.CandyColours.Length)]);
                s.raycastTarget = false;
                RectTransform rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = Centre;
                float len = size.y * 0.2f;
                rt.sizeDelta = new Vector2(len, len / 2.4f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 180f);
            }
        }

        /// <summary>A red dot with a number in it.</summary>
        public static Image Badge(Transform parent, string content, Vector2 anchor, Vector2 pos, float size = 58f)
        {
            Image dot = CandyUI.Icon("Badge", parent, ArtKit.Ui("dot_red"));
            CandyUI.Place(dot, anchor, pos, new Vector2(size, size));
            Text t = Text("Count", dot.transform, content, Centre, new Vector2(0f, 1f), new Vector2(size * 1.4f, size),
                          Mathf.RoundToInt(size * 0.56f), CandyStyle.OnPink, Color.white);
            t.font = Design.Display;
            return dot;
        }

        /// <summary>A slowly turning starburst, for behind a reward.</summary>
        public static Image Starburst(Transform parent, Vector2 anchor, Vector2 pos, float size, float speed = 24f)
        {
            Image img = CandyUI.Icon("Starburst", parent, ArtKit.Ui("reward_starburst"));
            CandyUI.Place(img, anchor, pos, new Vector2(size, size));
            img.gameObject.AddComponent<Spin>().Speed = speed;
            Breathe b = img.gameObject.AddComponent<Breathe>();
            b.AlphaMin = 0.55f;
            b.ScaleAmount = 0.08f;
            return img;
        }

        /// <summary>A generated rounded rectangle, for tracks and quiet panels the art has no piece for.</summary>
        public static Image Rounded(string name, Transform parent, Color fill, Color rim, Vector2 anchor, Vector2 pos,
                                    Vector2 size, float radiusUnits = 0f)
        {
            Image img = UIKit.Image(name, parent, ProcArt.RoundedRect("w_" + ColorUtility.ToHtmlStringRGBA(fill) +
                                                                     ColorUtility.ToHtmlStringRGBA(rim), fill, rim, 4f, 96, 0.46f),
                                    Color.white, Image.Type.Sliced);
            img.raycastTarget = false;
            CandyUI.Place(img, anchor, pos, size);
            float radius = radiusUnits > 0f ? radiusUnits : Mathf.Min(size.x, size.y) * 0.5f;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, (96f * 0.46f + 6f) / radius);
            return img;
        }
    }

    /// <summary>
    /// The coin balance: a blue pill with a coin, the number, and a green plus into the store.
    ///
    /// Every pill on every screen follows the wallet, and rolls to a new balance rather than
    /// snapping — held back for a moment when coins are flying at it, so the number climbs as they
    /// land instead of before they set off.
    /// </summary>
    public sealed class CoinPill : MonoBehaviour
    {
        private static readonly List<CoinPill> Live = new List<CoinPill>();
        private static float _holdUntil;

        private Text _label;
        private float _shown;
        private int _target;

        public RectTransform Coin { get; private set; }

        public static CoinPill Create(Transform parent, Vector2 anchor, Vector2 pos, float width = 300f, float height = 88f)
        {
            Image pill = W.Sliced("CoinPill", parent, "pill_blue", anchor, pos, new Vector2(width, height));
            CoinPill cp = pill.gameObject.AddComponent<CoinPill>();

            Image coin = W.Img("Coin", pill.transform, "coin", W.Left, new Vector2(height * 0.5f, 0f),
                               new Vector2(height * 0.8f, height * 0.8f));
            cp.Coin = coin.rectTransform;

            cp._label = W.Text("Coins", pill.transform, "0", W.Centre, new Vector2(4f, 2f),
                               new Vector2(width - height * 1.6f, height), Mathf.RoundToInt(height * 0.5f),
                               CandyStyle.OnBlue, Color.white);

            Button plus = CandyUI.SpriteButton("Plus", pill.transform, ArtKit.Ui("btn_plus"));
            plus.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(plus, W.Right, new Vector2(-height * 0.48f, 0f), new Vector2(height * 0.86f, height * 0.86f));
            plus.onClick.AddListener(Nav.OpenToolbox);

            cp._shown = cp._target = Wallet.Coins;
            cp._label.text = Hud.Format(cp._target);
            return cp;
        }

        /// <summary>Holds every pill's number where it is while coins fly to it.</summary>
        public static void HoldRoll(float seconds) => _holdUntil = Time.unscaledTime + seconds;

        /// <summary>The pill on screen right now, for coins to fly at. Null if none is showing.</summary>
        public static CoinPill Visible()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null && Live[i].isActiveAndEnabled) return Live[i];
            return null;
        }

        /// <summary>Shows a balance without rolling to it — the "before" of a payout.</summary>
        public void ShowImmediate(int value)
        {
            // OnEnable runs inside AddComponent, before Create has built the label.
            if (_label == null) return;
            _shown = value;
            _label.text = Hud.Format(value);
        }

        private void OnEnable()
        {
            Live.Add(this);
            Wallet.Changed += OnWallet;
            _target = Wallet.Coins;
            if (Time.unscaledTime >= _holdUntil) ShowImmediate(_target);
        }

        private void OnDisable()
        {
            Live.Remove(this);
            Wallet.Changed -= OnWallet;
        }

        private void OnWallet() => _target = Wallet.Coins;

        private void Update()
        {
            if (Mathf.Approximately(_shown, _target) || Time.unscaledTime < _holdUntil) return;

            float step = Mathf.Max(30f, Mathf.Abs(_target - _shown) * 5f) * Time.unscaledDeltaTime;
            _shown = Mathf.MoveTowards(_shown, _target, step);
            _label.text = Hud.Format(Mathf.RoundToInt(_shown));

            if (Mathf.Approximately(_shown, _target)) Tween.Punch(transform, 0.12f, 0.25f);
        }
    }

    /// <summary>The toolbox icon, with a red badge counting the power-ups owned. Wiggles when there are some.</summary>
    public sealed class ToolboxButton : MonoBehaviour
    {
        private Image _badge;
        private Text _count;
        private CandyPress _press;

        public static ToolboxButton Create(Transform parent, Vector2 anchor, Vector2 pos, float size = 120f)
        {
            Button b = CandyUI.SpriteButton("Toolbox", parent, ArtKit.Ui("icon_toolbox"));
            b.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(b, anchor, pos, new Vector2(size, size));
            b.onClick.AddListener(Nav.OpenToolbox);

            ToolboxButton tb = b.gameObject.AddComponent<ToolboxButton>();
            tb._badge = W.Badge(b.transform, "0", new Vector2(1f, 1f), new Vector2(-size * 0.08f, -size * 0.12f), size * 0.46f);
            tb._count = tb._badge.GetComponentInChildren<Text>();
            tb._press = b.GetComponent<CandyPress>();
            return tb;
        }

        private void OnEnable()
        {
            Wallet.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Wallet.Changed -= Refresh;

        private void Refresh()
        {
            int tools = Wallet.TotalTools;
            if (_badge != null) _badge.gameObject.SetActive(tools > 0);
            if (_count != null) _count.text = tools > 99 ? "99" : tools.ToString();
            if (_press != null) _press.Wiggle = tools > 0 ? 7f : 0f;
        }
    }

    /// <summary>The green ON / grey OFF switch from the reference art.</summary>
    public sealed class CandyToggle : MonoBehaviour
    {
        private Image _image;
        private Text _label;
        private Action<bool> _changed;

        public bool On { get; private set; }

        public static CandyToggle Create(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, bool on, Action<bool> changed)
        {
            Button b = CandyUI.SpriteButton("Toggle", parent, ArtKit.Ui(on ? "toggle_on" : "toggle_off"));
            b.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(b, anchor, pos, size);

            CandyToggle t = b.gameObject.AddComponent<CandyToggle>();
            t._image = b.GetComponent<Image>();
            t._label = W.Text("State", b.transform, "ON", W.Centre, Vector2.zero, new Vector2(size.x * 0.6f, size.y),
                              Mathf.RoundToInt(size.y * 0.4f), CandyStyle.OnGreen, Color.white);
            t._label.font = Design.Display;
            t._changed = changed;
            t.Set(on, false);
            b.onClick.AddListener(() => t.Set(!t.On, true));
            return t;
        }

        public void Set(bool on, bool notify)
        {
            On = on;
            _image.sprite = ArtKit.Ui(on ? "toggle_on" : "toggle_off");
            _label.text = on ? "ON" : "OFF";
            float w = ((RectTransform)transform).sizeDelta.x;
            _label.rectTransform.anchoredPosition = new Vector2(on ? -w * 0.17f : w * 0.17f, 2f);
            CandyText candy = _label.GetComponent<CandyText>();
            if (candy != null) candy.Set(on ? CandyStyle.OnGreen : CandyStyle.OnBlue);

            if (!notify) return;
            Sound.Toggle(on);
            Tween.Punch(transform, 0.1f, 0.22f);
            _changed?.Invoke(on);
        }
    }

    /// <summary>A rounded track with a glossy yellow fill that eases to its value.</summary>
    public sealed class CandyBar : MonoBehaviour
    {
        private RectTransform _fill;
        private Image _fillImage;
        private float _value = -1f;
        private float _shown;
        private float _width;
        private float _height;

        public float Value => _value;

        public static CandyBar Create(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string fillSprite = "pill_yellow")
        {
            Image track = W.Rounded("Bar", parent, CandyText.Hex(0x1B2D78), CandyText.Hex(0x4D6BE0), anchor, pos, size);
            CandyBar bar = track.gameObject.AddComponent<CandyBar>();
            bar._width = size.x;
            bar._height = size.y;

            float inset = Mathf.Max(3f, size.y * 0.12f);
            bar._fillImage = CandyUI.Icon("Fill", track.transform, ArtKit.Ui(fillSprite));
            bar._fillImage.preserveAspect = false;
            bar._fillImage.type = Image.Type.Sliced;
            bar._fill = bar._fillImage.rectTransform;
            bar._fill.anchorMin = bar._fill.anchorMax = new Vector2(0f, 0.5f);
            bar._fill.pivot = new Vector2(0f, 0.5f);
            bar._fill.anchoredPosition = new Vector2(inset, 0f);
            bar._height = size.y - inset * 2f;
            bar._width = size.x - inset * 2f;
            W.FitSlices(bar._fillImage, new Vector2(bar._width, bar._height));
            bar.Set(0f, false);
            return bar;
        }

        public void Set(float value, bool animate = true)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, _value)) return;
            _value = value;
            if (!animate) Apply(value);
        }

        private void Update()
        {
            if (Mathf.Approximately(_shown, _value)) return;
            Apply(Mathf.MoveTowards(_shown, _value, Time.unscaledDeltaTime * 1.4f));
        }

        private void Apply(float shown)
        {
            _shown = shown;
            _fillImage.enabled = shown > 0.001f;
            _fill.sizeDelta = new Vector2(Mathf.Max(_height, _width * shown), _height);
        }
    }
}
