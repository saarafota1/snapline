using System.Collections.Generic;
using UnityEngine;

namespace Snapline.Art
{
    /// <summary>
    /// Generates every sprite in the game at runtime. No art files to manage, no import settings to
    /// get wrong, and the whole look is a few numbers away from being different.
    ///
    /// Everything here is CPU pixel work on purpose. The studio playbook records that Graphics.Blit
    /// silently yields a blank texture under -nographics rather than failing, so anything built with
    /// the GPU would come out empty in batch mode — which is exactly how store screenshots and
    /// smoke tests get produced.
    ///
    /// Sprites are cached by key, so a colour is rasterised once per session and then reused.
    /// </summary>
    public static class ArtKit
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static void ClearCache()
        {
            foreach (var kv in Cache)
            {
                if (kv.Value == null) continue;
                if (kv.Value.texture != null) Object.Destroy(kv.Value.texture);
                Object.Destroy(kv.Value);
            }
            Cache.Clear();
        }

        // --- public sprite factories -----------------------------------------------------

        /// <summary>A filled play block: vertical gradient, rounded corners, top bevel, inner glow.</summary>
        public static Sprite Block(int colourIndex, int size = 128)
        {
            string key = $"block:{colourIndex}:{size}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            BlockColour c = Palette.Block(colourIndex);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = size * 0.5f;
            float radius = size * 0.22f;
            float glowWidth = size * 0.20f;
            float rimWidth = Mathf.Max(1.5f, size * 0.045f);

            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float pxc = x + 0.5f - half;
                    float pyc = y + 0.5f - half;
                    float d = RoundedBoxDistance(pxc, pyc, half, half, radius);

                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // Vertical gradient. Texture row 0 is the bottom, so t rises toward the top.
                    float t = y / (float)(size - 1);
                    Color col = Color.Lerp(c.Bottom, c.Top, t * t * (3f - 2f * t));

                    float inset = -d; // distance inside the edge, in pixels

                    // Inner glow, strongest right at the border and biased toward the top so the
                    // block reads as lit from above rather than uniformly outlined.
                    float glow = Mathf.Exp(-inset / Mathf.Max(1f, glowWidth));
                    float upBias = 0.45f + 0.55f * Mathf.Clamp01(0.5f + 0.5f * (pyc / half));
                    col = Color.Lerp(col, c.Glow, Mathf.Clamp01(glow * 0.55f * upBias));

                    // Bevel: a bright band just inside the top edge, a dark one inside the bottom.
                    float bevel = Mathf.Clamp01(1f - inset / rimWidth);
                    if (bevel > 0f)
                    {
                        float dir = pyc / half;
                        if (dir > 0f) col = Color.Lerp(col, Color.white, bevel * 0.45f * dir);
                        else col = Color.Lerp(col, c.Rim, bevel * 0.55f * -dir);
                    }

                    col.a = alpha;
                    px[y * size + x] = col;
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A rounded rectangle for panels, cells and buttons. Nine-sliced, so one 64px texture
        /// stretches to any size without the corners smearing.
        /// </summary>
        public static Sprite RoundedRect(string key, Color fill, Color rim, float rimPixels = 3f,
                                         int size = 64, float radiusFraction = 0.28f)
        {
            string cacheKey = $"rect:{key}:{size}";
            if (Cache.TryGetValue(cacheKey, out Sprite cached)) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = size * 0.5f;
            float radius = size * radiusFraction;
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    Color col = fill;
                    if (rimPixels > 0f && rim.a > 0f)
                    {
                        float rimAmount = Mathf.Clamp01(1f - (-d) / rimPixels);
                        col = Color.Lerp(col, rim, rimAmount * rim.a);
                    }

                    col.a = fill.a * alpha;
                    px[y * size + x] = col;
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            int border = Mathf.RoundToInt(radius + rimPixels + 2f);
            border = Mathf.Clamp(border, 2, size / 2 - 1);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                          0, SpriteMeshType.FullRect,
                                          new Vector4(border, border, border, border));
            Cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>Soft round dot. Every particle in the game is one of these, so they all batch.</summary>
        public static Sprite SoftCircle(int size = 64)
        {
            const string key = "softcircle";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = size * 0.5f;
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Bright solid core fading to nothing, rather than a hard-edged disc — reads as
                    // light rather than as a small circle when hundreds are on screen.
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    float core = Mathf.Clamp01(1f - r / 0.45f);
                    a = Mathf.Clamp01(a + core * 0.6f);

                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Full-screen vertical gradient for the background.</summary>
        public static Sprite VerticalGradient(string key, Color bottom, Color top, int height = 256)
        {
            string cacheKey = $"grad:{key}:{height}";
            if (Cache.TryGetValue(cacheKey, out Sprite cached)) return cached;

            var tex = new Texture2D(4, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color col = Color.Lerp(bottom, top, t);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = col;
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f), 100f);
            Cache[cacheKey] = sprite;
            return sprite;
        }

        // --- shared sprite handles used across the UI -------------------------------------

        public static Sprite EmptyCell() =>
            RoundedRect("emptycell", Palette.EmptyCell, Palette.EmptyCellRim, 3f);

        public static Sprite Panel() =>
            RoundedRect("panel", Palette.BoardPanel, Palette.BoardPanelRim, 4f);

        public static Sprite SoftPanel() =>
            RoundedRect("softpanel", new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.10f), 2f);

        public static Sprite Solid() =>
            RoundedRect("solid", Color.white, new Color(0, 0, 0, 0), 0f, 32, 0.30f);

        public static Sprite Background() =>
            VerticalGradient("bg", Palette.BackgroundBottom, Palette.BackgroundTop);

        // --- maths ------------------------------------------------------------------------

        /// <summary>
        /// Signed distance from a point to a rounded box, in pixels. Negative inside. Used for
        /// antialiasing: the coverage of a pixel is very nearly clamp(0.5 - d, 0, 1).
        /// </summary>
        private static float RoundedBoxDistance(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius);
            float qy = Mathf.Abs(py) - (halfH - radius);
            float outsideX = Mathf.Max(qx, 0f);
            float outsideY = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }
    }
}
