using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Art;

namespace Snapline.Art
{
    /// <summary>
    /// The special blocks: stone, cracked stone, bomb and gift.
    ///
    /// Authored art wins wherever it exists — drop `Blocks/special_stone`, `special_stone_cracked`,
    /// `special_bomb` or `special_gift` into Resources/Snapline and it is used instead. Until then
    /// they are drawn here, in the candy style: glossy, rounded, lit from the top left like every
    /// block on the sheet. Stones replace the block outright; the bomb and the gift are drawn as
    /// icons over a block of the cell's own colour, so they still belong to the board.
    /// </summary>
    public static class SpecialArt
    {
        public static Sprite Stone() => ArtLoader.Sprite("Blocks/special_stone") ?? Make("sp_stone", (u, v) => StonePixel(u, v, false));

        public static Sprite CrackedStone() =>
            ArtLoader.Sprite("Blocks/special_stone_cracked") ?? Make("sp_stone_cracked", (u, v) => StonePixel(u, v, true));

        public static Sprite Bomb() => ArtLoader.Sprite("Blocks/special_bomb") ?? Make("sp_bomb", BombPixel);

        public static Sprite Gift() => ArtLoader.Sprite("Blocks/special_gift") ?? Make("sp_gift", GiftPixel);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private const int Size = 160;

        private static Sprite Make(string key, Func<float, float, Color> pixel)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = pixel((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);

            tex.SetPixels(px);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Distance from a point to a segment, in the same units.</summary>
        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / Mathf.Max(1e-5f, dx * dx + dy * dy));
            float cx = ax + dx * t - px, cy = ay + dy * t - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        private static Color StonePixel(float u, float v, bool cracked)
        {
            float half = Size * 0.5f;
            float d = ProcArt.RoundedBoxDistance(u * half, v * half, half * 0.9f, half * 0.9f, half * 0.26f);
            float alpha = Mathf.Clamp01(0.5f - d);
            if (alpha <= 0f) return Color.clear;

            float depth = Mathf.Clamp01(-d / (half * 0.16f));
            Color top = new Color(0.80f, 0.83f, 0.90f);
            Color bottom = new Color(0.44f, 0.47f, 0.56f);
            Color c = Color.Lerp(bottom, top, (v + 1f) * 0.5f);

            // Bevel: the rim catches light on the top left and falls into shadow on the bottom right.
            float rim = 1f - depth;
            float lit = Mathf.Clamp01((v - u) * 0.5f + 0.5f);
            c = Color.Lerp(c, Color.Lerp(new Color(0.28f, 0.30f, 0.37f), Color.white, lit), rim * 0.55f);

            // Speckles and pits, so it reads as rock rather than as a grey candy.
            float n = Hash(Mathf.Floor(u * 22f), Mathf.Floor(v * 22f));
            if (n > 0.86f) c *= 0.82f;
            else if (n < 0.08f) c = Color.Lerp(c, Color.white, 0.18f);

            // Gloss on the top face.
            float gloss = Mathf.Exp(-Mathf.Pow((v - 0.55f) / 0.16f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(u + 0.2f) * 1.4f);
            c = Color.Lerp(c, Color.white, gloss * 0.35f);

            if (cracked)
            {
                float crack = Mathf.Min(
                    Mathf.Min(Segment(u, v, -0.05f, 0.85f, 0.08f, 0.3f), Segment(u, v, 0.08f, 0.3f, -0.12f, -0.1f)),
                    Mathf.Min(Mathf.Min(Segment(u, v, -0.12f, -0.1f, 0.18f, -0.55f), Segment(u, v, 0.08f, 0.3f, 0.6f, 0.12f)),
                              Segment(u, v, -0.12f, -0.1f, -0.62f, -0.28f)));
                float line = Mathf.Clamp01(1f - (crack - 0.022f) / 0.02f);
                c = Color.Lerp(c, new Color(0.16f, 0.17f, 0.22f), line);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(crack - 0.05f) / 0.02f) * 0.35f;
                c = Color.Lerp(c, Color.white, edge * (1f - line));
            }

            c.a = alpha;
            return c;
        }

        private static Color BombPixel(float u, float v)
        {
            Color result = Color.clear;

            // The spark at the tip of the fuse.
            float sx = u - 0.58f, sy = v - 0.72f;
            float sr = Mathf.Sqrt(sx * sx + sy * sy);
            float spark = Mathf.Exp(-sr * sr * 60f) + (Mathf.Exp(-Mathf.Abs(sx) * 40f) + Mathf.Exp(-Mathf.Abs(sy) * 40f)) * Mathf.Clamp01(1f - sr * 3.2f) * 0.8f;
            if (spark > 0.02f) result = Over(result, new Color(1f, Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(spark)), 0.2f, Mathf.Clamp01(spark)));

            // The fuse.
            float fuse = Mathf.Min(Segment(u, v, 0.22f, 0.36f, 0.4f, 0.55f), Segment(u, v, 0.4f, 0.55f, 0.56f, 0.7f));
            float fa = Mathf.Clamp01(1f - (fuse - 0.035f) / 0.02f);
            if (fa > 0f) result = Over(result, new Color(0.62f, 0.44f, 0.26f, fa));

            // The cap.
            float capD = ProcArt.RoundedBoxDistance((u - 0.18f) * 80f, (v - 0.3f) * 80f, 13f, 10f, 4f);
            float capA = Mathf.Clamp01(0.5f - capD);
            if (capA > 0f) result = Over(result, new Color(0.55f, 0.58f, 0.66f, capA));

            // The sphere, lit from the top left, with a sharp specular.
            float cx = u + 0.06f, cy = v + 0.1f;
            float r = Mathf.Sqrt(cx * cx + cy * cy);
            const float radius = 0.56f;
            float sa = Mathf.Clamp01((radius - r) * 80f);
            if (sa > 0f)
            {
                float nx = cx / radius, ny = cy / radius;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float light = Mathf.Clamp01(nx * -0.5f + ny * 0.6f + nz * 0.62f);
                Color body = Color.Lerp(new Color(0.08f, 0.08f, 0.14f), new Color(0.38f, 0.38f, 0.5f), light);
                float spec = Mathf.Pow(Mathf.Clamp01(nx * -0.42f + ny * 0.55f + nz * 0.72f), 40f);
                body = Color.Lerp(body, Color.white, spec);
                body.a = sa;
                result = Over(body, result);
            }

            return result;
        }

        private static Color GiftPixel(float u, float v)
        {
            Color result = Color.clear;
            Color box = new Color(1f, 0.97f, 0.9f);
            Color ribbon = new Color(1f, 0.28f, 0.6f);

            // Bow loops.
            for (int side = -1; side <= 1; side += 2)
            {
                float bx = (u - side * 0.2f) / 0.2f, by = (v - 0.5f) / 0.13f;
                float bd = bx * bx + by * by;
                float ba = Mathf.Clamp01((1f - bd) * 6f);
                float hole = Mathf.Clamp01((0.3f - bd) * 10f);
                if (ba > 0f)
                {
                    Color loop = Color.Lerp(ribbon, Color.white, Mathf.Clamp01(by * 0.3f + 0.1f));
                    loop.a = ba * (1f - hole * 0.85f);
                    result = Over(loop, result);
                }
            }

            // Body and lid.
            float bodyD = ProcArt.RoundedBoxDistance(u * 80f, (v + 0.2f) * 80f, 44f, 34f, 6f);
            float lidD = ProcArt.RoundedBoxDistance(u * 80f, (v - 0.22f) * 80f, 52f, 12f, 5f);
            float bodyA = Mathf.Clamp01(0.5f - bodyD);
            float lidA = Mathf.Clamp01(0.5f - lidD);

            if (bodyA > 0f)
            {
                Color c = Color.Lerp(box * 0.86f, box, (v + 0.6f) * 1.2f);
                if (Mathf.Abs(u) < 0.1f) c = Color.Lerp(ribbon * 0.85f, ribbon, (v + 0.6f));
                c.a = bodyA;
                result = Over(c, result);
            }

            if (lidA > 0f)
            {
                Color c = Color.Lerp(box * 0.92f, Color.white, (v - 0.1f) * 4f);
                if (Mathf.Abs(u) < 0.1f) c = ribbon;
                c.a = lidA;
                result = Over(c, result);
            }

            return result;
        }

        /// <summary>Alpha "a over b".</summary>
        private static Color Over(Color a, Color b)
        {
            float outA = a.a + b.a * (1f - a.a);
            if (outA <= 0f) return Color.clear;
            Color c = (a * a.a + b * b.a * (1f - a.a)) / outA;
            c.a = outA;
            return c;
        }
    }
}
