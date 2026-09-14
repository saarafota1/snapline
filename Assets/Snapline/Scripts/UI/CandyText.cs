using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Snapline.UI
{
    /// <summary>The colour recipes for 3D candy lettering.</summary>
    public enum CandyStyle
    {
        /// <summary>White-to-blush face on a pink extrusion: LEVELS, TOOLBOX, PAUSED.</summary>
        White,
        /// <summary>Icy face on a blue extrusion: CHALLENGE, SCORES.</summary>
        Cyan,
        /// <summary>Butter-to-orange face on a caramel extrusion: combos, rewards, NEW BEST.</summary>
        Gold,
        /// <summary>Hot pink lettering with no outline, for text on cream: UNDO.</summary>
        Pink,
        /// <summary>Violet lettering for text on cream: SHUFFLE.</summary>
        Purple,
        /// <summary>Blue lettering for text on cream: HAMMER, LEVEL 9.</summary>
        Blue,
        /// <summary>White caption on a pink button.</summary>
        OnPink,
        /// <summary>White caption on a blue or cyan button.</summary>
        OnBlue,
        /// <summary>White caption on a purple button.</summary>
        OnPurple,
        /// <summary>White caption on a green button.</summary>
        OnGreen,
        /// <summary>White caption on a gold or orange button.</summary>
        OnGold,
        /// <summary>Dark chocolate lettering on cream, a little raised: stats, prices.</summary>
        Cocoa,
        /// <summary>Navy lettering on cream or pale blue, a little raised: scores in a table.</summary>
        Navy,
    }

    /// <summary>
    /// 3D candy lettering on an ordinary uGUI Text: a gradient face with a gloss band, a solid
    /// extrusion under it, a thick outline around both, and a soft drop shadow.
    ///
    /// Built as a mesh effect rather than as artwork, so every title, button caption and combo
    /// shout in the game is live text — translatable, and changeable without asking for new images.
    /// The reference art's lettering is exactly this construction; it just happens to be painted.
    ///
    /// How the layers are drawn, back to front:
    ///   shadow    one copy, dropped below everything, at low alpha
    ///   outline   copies in a ring around both the face and the bottom of the extrusion
    ///   extrusion copies stepped downward, darkening as they go
    ///   face      the glyphs themselves, cut into horizontal bands so a gradient and a gloss line
    ///             can run through each letter rather than only from its top edge to its bottom
    ///
    /// Two optional touches: <see cref="Arc"/> bends the line into a smile for text on a ribbon,
    /// and <see cref="Shine"/> sweeps a highlight across the face, animated by
    /// <see cref="CandyShine"/>.
    ///
    /// Alpha comes from the Text's own colour, so fading a label still fades all of it.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class CandyText : BaseMeshEffect
    {
        public Color FaceTop = Color.white;
        public Color FaceBottom = Color.white;
        public Color Depth = new Color(0.85f, 0.25f, 0.6f);
        public Color OutlineColour = new Color(0.35f, 0.08f, 0.35f);
        public Color ShadowColour = new Color(0.12f, 0.03f, 0.2f, 0.32f);

        /// <summary>Extrusion depth as a fraction of the font size.</summary>
        public float DepthFraction = 0.085f;

        /// <summary>Outline thickness as a fraction of the font size. Zero for none.</summary>
        public float OutlineFraction = 0.06f;

        /// <summary>How much the upper part of each letter is lifted toward white.</summary>
        public float Gloss = 0.45f;

        /// <summary>How far, in canvas units, the ends of the line drop below its middle.</summary>
        public float Arc;

        /// <summary>Position of the highlight sweep across the text, 0..1. Outside that, no sweep.</summary>
        public float Shine = -5f;

        private const int OutlineSteps = 12;
        private const int DepthSteps = 5;
        private static readonly float[] BandStops = { 0f, 0.3f, 0.5f, 0.72f, 1f };

        private static readonly List<UIVertex> Src = new List<UIVertex>(512);
        private static readonly List<UIVertex> Out = new List<UIVertex>(8192);

        private float _minX, _maxX, _minY, _maxY;

        /// <summary>Re-render after a colour or shine change made from code.</summary>
        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            Src.Clear();
            vh.GetUIVertexStream(Src);
            if (Src.Count < 6) return;

            float size = graphic is Text text ? text.fontSize : 60f;

            _minX = _minY = float.MaxValue;
            _maxX = _maxY = float.MinValue;
            for (int i = 0; i < Src.Count; i++)
            {
                Vector3 p = Src[i].position;
                if (p.x < _minX) _minX = p.x;
                if (p.x > _maxX) _maxX = p.x;
                if (p.y < _minY) _minY = p.y;
                if (p.y > _maxY) _maxY = p.y;
            }

            if (Mathf.Abs(Arc) > 0.01f)
            {
                float cx = (_minX + _maxX) * 0.5f;
                float half = Mathf.Max(1f, (_maxX - _minX) * 0.5f);
                for (int i = 0; i < Src.Count; i++)
                {
                    UIVertex v = Src[i];
                    float k = (v.position.x - cx) / half;
                    v.position.y -= Arc * k * k;
                    Src[i] = v;
                }
                _minY -= Arc;
            }

            float depth = size * DepthFraction;
            float outline = size * OutlineFraction;

            Out.Clear();

            // Shadow.
            if (ShadowColour.a > 0f)
                AddCopy(new Vector2(0f, -depth - outline - size * 0.05f), ShadowColour);

            // Outline, around both the face and the foot of the extrusion.
            if (outline > 0.01f)
            {
                int levels = depth > outline ? 3 : 2;
                for (int lv = 0; lv < levels; lv++)
                {
                    float dy = -depth * lv / (levels - 1);
                    for (int s = 0; s < OutlineSteps; s++)
                    {
                        float a = s * Mathf.PI * 2f / OutlineSteps;
                        AddCopy(new Vector2(Mathf.Cos(a) * outline, Mathf.Sin(a) * outline + dy), OutlineColour);
                    }
                }
            }

            // Extrusion, darkest at the bottom.
            if (depth > 0.01f)
            {
                Color deep = Color.Lerp(Depth, Color.black, 0.28f);
                deep.a = Depth.a;
                for (int d = DepthSteps; d >= 1; d--)
                {
                    float k = d / (float)DepthSteps;
                    AddCopy(new Vector2(0f, -depth * k), Color.Lerp(Depth, deep, k));
                }
            }

            AddFace();

            vh.Clear();
            vh.AddUIVertexTriangleStream(Out);
        }

        private void AddCopy(Vector2 offset, Color colour)
        {
            for (int i = 0; i < Src.Count; i++)
            {
                UIVertex v = Src[i];
                v.position.x += offset.x;
                v.position.y += offset.y;
                Color32 c = colour;
                c.a = (byte)(colour.a * v.color.a);
                v.color = c;
                Out.Add(v);
            }
        }

        private void AddFace()
        {
            for (int q = 0; q + 5 < Src.Count; q += 6)
            {
                UIVertex tl = Src[q], tr = Src[q + 1], br = Src[q + 2], bl = Src[q + 4];

                for (int b = 0; b < BandStops.Length - 1; b++)
                {
                    UIVertex l0 = Mix(tl, bl, BandStops[b]);
                    UIVertex r0 = Mix(tr, br, BandStops[b]);
                    UIVertex l1 = Mix(tl, bl, BandStops[b + 1]);
                    UIVertex r1 = Mix(tr, br, BandStops[b + 1]);

                    Out.Add(Paint(l0)); Out.Add(Paint(r0)); Out.Add(Paint(r1));
                    Out.Add(Paint(r1)); Out.Add(Paint(l1)); Out.Add(Paint(l0));
                }
            }
        }

        private UIVertex Paint(UIVertex v)
        {
            float h = Mathf.Max(1f, _maxY - _minY);
            float t = Mathf.Clamp01((v.position.y - _minY) / h);

            Color c = Color.Lerp(FaceBottom, FaceTop, t);

            // The gloss: the top of each letter catches the light, with a fairly quick transition
            // so it reads as a glossy highlight rather than a gentle fade.
            if (t > 0.5f)
            {
                float g = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.32f);
                c = Color.Lerp(c, Color.white, Gloss * g);
            }

            if (Shine > -1f && Shine < 2f)
            {
                float w = Mathf.Max(1f, _maxX - _minX);
                float sx = (v.position.x - _minX) / w + (t - 0.5f) * 0.18f;
                float d = Mathf.Abs(sx - Shine);
                if (d < 0.1f) c = Color.Lerp(c, Color.white, (1f - d / 0.1f) * 0.9f);
            }

            byte alpha = v.color.a;
            Color32 c32 = c;
            c32.a = alpha;
            v.color = c32;
            return v;
        }

        private static UIVertex Mix(UIVertex a, UIVertex b, float t)
        {
            var v = new UIVertex
            {
                position = Vector3.LerpUnclamped(a.position, b.position, t),
                normal = a.normal,
                tangent = a.tangent,
                color = Color32.Lerp(a.color, b.color, t),
                uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t),
                uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t),
                uv2 = a.uv2,
                uv3 = a.uv3,
            };
            return v;
        }

        // --- styles ----------------------------------------------------------------------------

        /// <summary>
        /// Puts 3D lettering on a label, replacing any flat Outline or Shadow it was built with.
        /// The label's own colour is set to white, because the effect paints the colour and only
        /// the alpha of the label is used.
        /// </summary>
        public static CandyText Apply(Text label, CandyStyle style)
        {
            if (label == null) return null;

            foreach (Shadow s in label.GetComponents<Shadow>())
            {
                s.enabled = false;
                Object.Destroy(s);
            }

            CandyText fx = label.GetComponent<CandyText>();
            if (fx == null) fx = label.gameObject.AddComponent<CandyText>();

            Color keepAlpha = Color.white;
            keepAlpha.a = label.color.a;
            label.color = keepAlpha;

            fx.Set(style);
            return fx;
        }

        public void Set(CandyStyle style)
        {
            Arc = 0f;
            ShadowColour = new Color(0.12f, 0.03f, 0.2f, 0.30f);
            Gloss = 0.45f;

            switch (style)
            {
                case CandyStyle.White:
                    Face(Color.white, Hex(0xFFE3F1), Hex(0xF0479C), Hex(0x5A1650), 0.10f, 0.065f);
                    break;
                case CandyStyle.Cyan:
                    Face(Hex(0xE8FFFF), Hex(0x3FD3FF), Hex(0x1576E0), Hex(0x0B2F7A), 0.10f, 0.065f);
                    break;
                case CandyStyle.Gold:
                    Face(Hex(0xFFF6B0), Hex(0xFFB21E), Hex(0xE0660C), Hex(0x5C2508), 0.09f, 0.06f);
                    break;
                case CandyStyle.Pink:
                    Face(Hex(0xFF6FA8), Hex(0xE2195E), Hex(0xA80E45), Hex(0xFFFFFF), 0.05f, 0f);
                    ShadowColour = new Color(0.5f, 0.05f, 0.2f, 0.18f);
                    Gloss = 0.25f;
                    break;
                case CandyStyle.Purple:
                    Face(Hex(0xB07BFF), Hex(0x6A2BE0), Hex(0x3F1795), Hex(0xFFFFFF), 0.05f, 0f);
                    ShadowColour = new Color(0.25f, 0.05f, 0.5f, 0.18f);
                    Gloss = 0.25f;
                    break;
                case CandyStyle.Blue:
                    Face(Hex(0x4FB2FF), Hex(0x1A55D6), Hex(0x0E2F86), Hex(0xFFFFFF), 0.06f, 0.045f);
                    ShadowColour = new Color(0.05f, 0.1f, 0.4f, 0.2f);
                    Gloss = 0.3f;
                    break;
                case CandyStyle.OnPink:
                    Face(Color.white, Hex(0xFFF0F6), Hex(0xB5124E), Hex(0x8C0B3C), 0.07f, 0.035f);
                    break;
                case CandyStyle.OnBlue:
                    Face(Color.white, Hex(0xEEF6FF), Hex(0x1348B8), Hex(0x0B2C7A), 0.07f, 0.035f);
                    break;
                case CandyStyle.OnPurple:
                    Face(Color.white, Hex(0xF3EEFF), Hex(0x4B1BA8), Hex(0x2E0E70), 0.07f, 0.035f);
                    break;
                case CandyStyle.OnGreen:
                    Face(Color.white, Hex(0xEFFFF3), Hex(0x0E8C47), Hex(0x075A2C), 0.07f, 0.035f);
                    break;
                case CandyStyle.OnGold:
                    Face(Color.white, Hex(0xFFF8E6), Hex(0xC2620A), Hex(0x7A3A05), 0.07f, 0.035f);
                    break;
                case CandyStyle.Cocoa:
                    Face(Hex(0x7A3A18), Hex(0x4A1E08), Hex(0xE8C9A8), Hex(0x000000), 0.035f, 0f);
                    ShadowColour = Color.clear;
                    Gloss = 0.08f;
                    break;
                case CandyStyle.Navy:
                    Face(Hex(0x2A4BB8), Hex(0x14277A), Hex(0xB9C6EE), Hex(0x000000), 0.035f, 0f);
                    ShadowColour = Color.clear;
                    Gloss = 0.1f;
                    break;
            }

            Refresh();
        }

        private void Face(Color top, Color bottom, Color depth, Color outline, float depthFraction,
                          float outlineFraction)
        {
            FaceTop = top;
            FaceBottom = bottom;
            Depth = depth;
            OutlineColour = outline;
            DepthFraction = depthFraction;
            OutlineFraction = outlineFraction;
        }

        public static Color Hex(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    /// <summary>
    /// Sweeps a highlight across a <see cref="CandyText"/> every few seconds. Titles and the primary
    /// button only — each sweep rebuilds the text mesh for the length of the pass.
    /// </summary>
    public sealed class CandyShine : MonoBehaviour
    {
        public float Period = 3.2f;
        public float Duration = 0.7f;

        private CandyText _text;
        private float _clock;

        private void Awake()
        {
            _text = GetComponent<CandyText>();
            _clock = Random.Range(0f, Period);
        }

        private void Update()
        {
            if (_text == null) return;

            _clock += Time.unscaledDeltaTime;
            if (_clock > Period) _clock -= Period;

            float k = _clock / Duration;
            float shine = k <= 1f ? Mathf.Lerp(-0.2f, 1.2f, k) : -5f;

            if (Mathf.Approximately(shine, _text.Shine)) return;
            _text.Shine = shine;
            _text.Refresh();
        }
    }
}
