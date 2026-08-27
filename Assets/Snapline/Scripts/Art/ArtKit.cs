using UnityEngine;
using GameKit.Art;

namespace Snapline.Art
{
    /// <summary>
    /// Snapline's sprite vocabulary.
    ///
    /// The pixel work lives in the kit (GameKit.Art.ProcArt) because it is not specific to this
    /// game. What stays here is the mapping from *this* game's palette to those generators, which
    /// is the only part another game would want to write differently.
    ///
    /// The gameplay code never names a colour — it passes a colour index — so replacing these
    /// generated sprites with imported artwork later means editing this file and nothing else.
    /// </summary>
    public static class ArtKit
    {
        /// <summary>A filled play block in the given palette colour.</summary>
        public static Sprite Block(int colourIndex, int size = 128)
        {
            BlockColour c = Palette.Block(colourIndex);
            return ProcArt.Block($"snapline{colourIndex}", c.Top, c.Bottom, c.Glow, c.Rim, size);
        }

        public static Sprite RoundedRect(string key, Color fill, Color rim, float rimPixels = 3f,
                                         int size = 64, float radiusFraction = 0.28f) =>
            ProcArt.RoundedRect(key, fill, rim, rimPixels, size, radiusFraction);

        public static Sprite SoftCircle(int size = 64) => ProcArt.SoftCircle(size);

        public static Sprite EmptyCell() =>
            ProcArt.RoundedRect("emptycell", Palette.EmptyCell, Palette.EmptyCellRim, 3f);

        public static Sprite Panel() =>
            ProcArt.RoundedRect("panel", Palette.BoardPanel, Palette.BoardPanelRim, 4f);

        public static Sprite SoftPanel() =>
            ProcArt.RoundedRect("softpanel", new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.10f), 2f);

        public static Sprite Solid() => ProcArt.Solid();

        public static Sprite Background() =>
            ProcArt.VerticalGradient("bg", Palette.BackgroundBottom, Palette.BackgroundTop);

        public static void ClearCache() => ProcArt.ClearCache();
    }
}
