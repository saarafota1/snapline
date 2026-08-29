using System.IO;
using UnityEditor;
using UnityEngine;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.EditorTools
{
    /// <summary>
    /// Generates the store artwork that has no text in it: the 512x512 listing icon and the
    /// 1024x1024 launcher source.
    ///
    /// Drawn with the same rounded-block maths the game uses, so the icon is literally made of the
    /// thing the player sees rather than an interpretation of it. All CPU work — the playbook is
    /// explicit that Graphics.Blit returns a blank texture under -nographics, which is exactly how
    /// this runs.
    ///
    /// The feature graphic needs the game's font, so it is captured from the running player instead;
    /// see StoreShots.
    /// </summary>
    public static class StoreAssets
    {
        private const string OutputDir = "StoreAssets";

        [MenuItem("Snapline/Store/Generate Icons")]
        public static void GenerateIcons()
        {
            Directory.CreateDirectory(OutputDir);

            WritePng(Path.Combine(OutputDir, "icon-512.png"), BuildIcon(512));
            WritePng(Path.Combine(OutputDir, "icon-1024.png"), BuildIcon(1024));

            Debug.Log($"[Snapline] Icons written to {Path.GetFullPath(OutputDir)}");
        }

        /// <summary>Batch entry point.</summary>
        public static void GenerateIconsCLI()
        {
            GenerateIcons();
            Debug.Log("[Snapline] StoreAssets.GenerateIconsCLI complete.");
        }

        /// <summary>
        /// Four blocks in a 2x2, on the game's background gradient.
        ///
        /// Deliberately not the 8x8 board: at 48 px in a launcher tray an eight-column grid turns to
        /// mush, while four fat blocks still read as a block puzzle. The colours are the palette's
        /// most separated hues so it stays legible for colour-blind players too.
        /// </summary>
        private static Color32[] BuildIcon(int size)
        {
            var px = new Color32[size * size];

            // Background gradient, bottom to top.
            for (int y = 0; y < size; y++)
            {
                Color row = Color.Lerp(Palette.BackgroundBottom, Palette.BackgroundTop, y / (float)(size - 1));
                for (int x = 0; x < size; x++) px[y * size + x] = row;
            }

            float margin = size * 0.13f;
            float gap = size * 0.045f;
            float cell = (size - margin * 2f - gap) * 0.5f;

            // Coral, amber, mint, indigo: the four most distinct hues in the palette.
            int[] colours = { 0, 1, 3, 5 };

            for (int i = 0; i < 4; i++)
            {
                int cx = i % 2;
                int cy = i / 2;

                float x0 = margin + cx * (cell + gap);

                // Row 0 of the icon layout is the top, so flip into texture space.
                float y0 = size - margin - cell - cy * (cell + gap);

                DrawBlock(px, size, x0, y0, cell, Palette.Block(colours[i]));
            }

            return px;
        }

        /// <summary>Same gradient, inner glow and bevel as an in-game block, composited in place.</summary>
        private static void DrawBlock(Color32[] px, int size, float x0, float y0, float extent, BlockColour c)
        {
            float half = extent * 0.5f;
            float centreX = x0 + half;
            float centreY = y0 + half;

            float radius = extent * 0.22f;
            float glowWidth = extent * 0.20f;
            float rimWidth = Mathf.Max(1.5f, extent * 0.045f);

            int minX = Mathf.Max(0, Mathf.FloorToInt(x0) - 2);
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(x0 + extent) + 2);
            int minY = Mathf.Max(0, Mathf.FloorToInt(y0) - 2);
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(y0 + extent) + 2);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float pxc = x + 0.5f - centreX;
                    float pyc = y + 0.5f - centreY;

                    float d = ProcArt.RoundedBoxDistance(pxc, pyc, half, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) continue;

                    float t = Mathf.InverseLerp(y0, y0 + extent, y + 0.5f);
                    Color col = Color.Lerp(c.Bottom, c.Top, t * t * (3f - 2f * t));

                    float inset = -d;
                    float glow = Mathf.Exp(-inset / Mathf.Max(1f, glowWidth));
                    float upBias = 0.45f + 0.55f * Mathf.Clamp01(0.5f + 0.5f * (pyc / half));
                    col = Color.Lerp(col, c.Glow, Mathf.Clamp01(glow * 0.55f * upBias));

                    float bevel = Mathf.Clamp01(1f - inset / rimWidth);
                    if (bevel > 0f)
                    {
                        float dir = pyc / half;
                        if (dir > 0f) col = Color.Lerp(col, Color.white, bevel * 0.45f * dir);
                        else col = Color.Lerp(col, c.Rim, bevel * 0.55f * -dir);
                    }

                    int idx = y * size + x;
                    Color under = px[idx];
                    px[idx] = Color.Lerp(under, col, alpha);
                }
            }
        }

        private static void WritePng(string path, Color32[] pixels)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log($"[Snapline] wrote {path} ({size}x{size})");
        }
    }
}
