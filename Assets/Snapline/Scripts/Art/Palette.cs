using UnityEngine;

namespace Snapline.Art
{
    /// <summary>One block colour, described richly enough to bake a gradient and a glow from it.</summary>
    public struct BlockColour
    {
        public Color Top;
        public Color Bottom;
        public Color Glow;
        public Color Rim;

        public Color Mid => Color.Lerp(Top, Bottom, 0.5f);
    }

    /// <summary>
    /// The game's colours, in one place.
    ///
    /// The core engine deals in colour *indices* and knows nothing about any of this, so the whole
    /// look can be replaced — including swapping generated blocks for real sprites — without the
    /// rules engine changing at all.
    /// </summary>
    public static class Palette
    {
        public static readonly Color BackgroundTop = Hex("1B2350");
        public static readonly Color BackgroundBottom = Hex("0A0E24");

        public static readonly Color BoardPanel = Hex("161C3D");
        public static readonly Color BoardPanelRim = Hex("2C3670");
        public static readonly Color EmptyCell = Hex("212A57");
        public static readonly Color EmptyCellRim = Hex("2E3970");

        public static readonly Color TextBright = Hex("FFFFFF");
        public static readonly Color TextDim = Hex("9AA6E0");
        public static readonly Color Accent = Hex("FFD54A");

        /// <summary>Ghost preview shown under a dragged piece where it will land.</summary>
        public static readonly Color GhostValid = new Color(1f, 1f, 1f, 0.28f);
        public static readonly Color GhostInvalid = new Color(1f, 0.35f, 0.35f, 0.22f);

        /// <summary>
        /// Block colours. Hues are spread wide and kept at high saturation so adjacent blocks read
        /// as distinct at thumb size, which is the only size that matters on a phone.
        /// </summary>
        private static readonly BlockColour[] Blocks =
        {
            Make("FF7A6B", "F0425A", "FF9C8F"), // coral
            Make("FFC24D", "FF8A2B", "FFD98A"), // amber
            Make("FFE96B", "FFC02E", "FFF3A8"), // sunflower
            Make("6BE59B", "20B978", "A2F2C4"), // mint
            Make("5CD2F0", "1E93D8", "9BE6FA"), // sky
            Make("C97BFF", "8E3EE0", "E0B0FF"), // violet
        };

        public static int Count => Blocks.Length;

        public static BlockColour Block(int index)
        {
            if (Blocks.Length == 0) return default;
            int i = index % Blocks.Length;
            if (i < 0) i += Blocks.Length;
            return Blocks[i];
        }

        private static BlockColour Make(string top, string bottom, string glow) => new BlockColour
        {
            Top = Hex(top),
            Bottom = Hex(bottom),
            Glow = Hex(glow),
            Rim = Color.Lerp(Hex(bottom), Color.black, 0.35f),
        };

        public static Color Hex(string hex)
        {
            if (hex.Length == 6) hex += "FF";
            uint v = uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return new Color(
                ((v >> 24) & 0xFF) / 255f,
                ((v >> 16) & 0xFF) / 255f,
                ((v >> 8) & 0xFF) / 255f,
                (v & 0xFF) / 255f);
        }
    }
}
