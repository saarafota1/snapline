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
        /// <summary>
        /// Authored block artwork, in palette order.
        ///
        /// Six, not the palette's seven. The candy sheet ships eight squares, but the deep blue one
        /// is the *empty cell*, not a playable colour — its centre samples at #153CA2 against the
        /// #284BA0–#355FAF of the unfilled cells in the reference art, while every real block sits
        /// somewhere vivid like #51F4AB or #FDBD49. Treating it as a seventh colour would have put
        /// blocks on the board that are almost indistinguishable from the holes between them.
        ///
        /// A colour index is only ever a look-up, never anything the engine reasons about, so
        /// serving seven indices from six sprites changes which colour a given piece is drawn in
        /// and nothing else.
        /// </summary>
        private static readonly string[] BlockArt =
        {
            "Blocks/block_pink",    // coral
            "Blocks/block_orange",  // amber
            "Blocks/block_yellow",  // sunflower
            "Blocks/block_green",   // mint
            "Blocks/block_cyan",    // sky
            "Blocks/block_purple",  // violet
        };

        /// <summary>
        /// A filled play block in the given palette colour.
        ///
        /// Authored art first, the generator second. Both have to work for as long as the art is
        /// still arriving screen by screen, and the fallback is what keeps the game playable in
        /// between rather than only once every last PNG has landed.
        /// </summary>
        public static Sprite Block(int colourIndex, int size = 128)
        {
            if (BlockArt.Length > 0)
            {
                int i = colourIndex % BlockArt.Length;
                if (i < 0) i += BlockArt.Length;
                Sprite authored = ArtLoader.Sprite(BlockArt[i]);
                if (authored != null) return authored;
            }

            BlockColour c = Palette.Block(colourIndex);
            return ProcArt.Block($"snapline{colourIndex}", c.Top, c.Bottom, c.Glow, c.Rim, size);
        }

        public static Sprite RoundedRect(string key, Color fill, Color rim, float rimPixels = 3f,
                                         int size = 64, float radiusFraction = 0.28f) =>
            ProcArt.RoundedRect(key, fill, rim, rimPixels, size, radiusFraction);

        public static Sprite SoftCircle(int size = 64) => ProcArt.SoftCircle(size);

        /// <summary>
        /// An unfilled board cell — the deep blue square from the candy sheet, which is what the
        /// reference art uses for the holes rather than for any playable piece.
        /// </summary>
        public static Sprite EmptyCell() =>
            ArtLoader.Sprite("Blocks/cell_empty") ??
            ProcArt.RoundedRect("emptycell", Palette.EmptyCell, Palette.EmptyCellRim, 3f);

        public static Sprite Panel() =>
            ProcArt.RoundedRect("panel", Palette.BoardPanel, Palette.BoardPanelRim, 4f);

        public static Sprite SoftPanel() =>
            ProcArt.RoundedRect("softpanel", new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.10f), 2f);

        public static Sprite Solid() => ProcArt.Solid();

        public static Sprite Background() =>
            ArtLoader.Sprite("UI/candy_background") ??
            ProcArt.VerticalGradient("bg", Palette.BackgroundBottom, Palette.BackgroundTop);

        /// <summary>The wordmark. Null if it has not been delivered, so callers can fall back to text.</summary>
        public static Sprite Logo() => ArtLoader.Sprite("UI/logo");

        /// <summary>The candy border drawn around the board. Nine-sliced, so it holds its thickness.</summary>
        public static Sprite BoardFrame() => ArtLoader.Sprite("Blocks/board_frame");

        /// <summary>
        /// Any authored UI sprite by name, relative to <c>Resources/Snapline/UI/</c>.
        ///
        /// Deliberately untyped: the home screen alone uses a dozen one-off icons, and a named
        /// accessor each would be a wall of near-identical one-liners that says nothing.
        /// </summary>
        public static Sprite Ui(string name) => ArtLoader.Sprite("UI/" + name);

        public static void ClearCache() => ProcArt.ClearCache();
    }
}
