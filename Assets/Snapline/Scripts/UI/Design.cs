using UnityEngine;

namespace Snapline.UI
{
    /// <summary>
    /// Every number and colour worth adjusting by hand, in one file.
    ///
    /// This exists so the look can be tuned without reading layout code. Change a value here, press
    /// play, and it applies everywhere that thing is used — rather than hunting the same 44 through
    /// six screens and missing two of them.
    ///
    /// What is NOT here, because it cannot be:
    ///
    /// • **Corner roundness.** Every button and panel is a drawn sprite, and its corners are painted
    ///   pixels. There is no radius to change. A rounder button needs new artwork.
    /// • **Button colours.** Same reason — a pink button is `tile_pink`, not a pink tint. Multiplying
    ///   a colour through glossy artwork muddies its highlight instead of recolouring it, so a
    ///   different colour means a different sprite. The available ones are listed in Swatch below.
    /// • **Per-screen positions.** Where a particular button sits is layout, not style, and lives at
    ///   the top of that screen's own file under a "Layout" heading.
    /// </summary>
    public static class Design
    {
        // --- font ---------------------------------------------------------------------------

        private static Font _body;
        private static Font _display;

        /// <summary>
        /// Heebo Bold — the game's text.
        ///
        /// Loaded from Resources rather than taken from the operating system: an OS font lookup
        /// finds nothing on a phone, and the label would silently fall back to a default that looks
        /// nothing like this. The file ships inside the build.
        ///
        /// Falls back to whatever the kit was using if the file is missing, so a bad copy costs the
        /// look and not the text.
        /// </summary>
        public static Font Body =>
            _body != null ? _body : _body = Resources.Load<Font>("Snapline/Fonts/Heebo-Bold")
                                            ?? GameKit.Art.UIKit.Font;

        /// <summary>
        /// Heebo Black, for display text — titles, the big score, a primary button's caption.
        ///
        /// A separate file rather than <c>FontStyle.Bold</c> on the regular weight: that synthesises
        /// a fake bold by smearing the glyphs, which on large text looks exactly like what it is.
        /// </summary>
        public static Font Display =>
            _display != null ? _display : _display = Resources.Load<Font>("Snapline/Fonts/Heebo-Black")
                                                     ?? Body;

        /// <summary>
        /// At and above this size a label uses <see cref="Display"/> instead of <see cref="Body"/>.
        ///
        /// A size threshold rather than a flag at every call site, because the rule it encodes is
        /// really about scale: heavy weights read well large and turn into a blur small, and there
        /// are well over a hundred labels in the game to keep consistent by hand.
        /// </summary>
        public const int DisplayThreshold = 44;

        public static Font For(int size) => size >= DisplayThreshold ? Display : Body;

        // --- type ---------------------------------------------------------------------------
        //
        // One scale, used everywhere. Raising Body raises every secondary line in the game at once,
        // which is usually what you want when text feels small on a phone.

        /// <summary>Screen titles: LEVELS, BEST SCORES, TOOLBOX.</summary>
        public const int TitleSize = 96;

        /// <summary>The big word on a primary button: PLAY, CONTINUE.</summary>
        public const int ButtonSize = 68;

        /// <summary>Secondary buttons and list rows.</summary>
        public const int SubtitleSize = 54;

        /// <summary>Labels on mode buttons and card headings.</summary>
        public const int HeadingSize = 44;

        /// <summary>Ordinary text: the line under a button, a stat value.</summary>
        public const int BodySize = 36;

        /// <summary>Small print: day names, "YOU HAVE 3", dates.</summary>
        public const int SmallSize = 30;

        /// <summary>Smallest used anywhere. Below about 24 it stops being readable on a phone.</summary>
        public const int TinySize = 24;

        // --- text colour --------------------------------------------------------------------

        /// <summary>White, for text on a saturated button or over the background.</summary>
        public static readonly Color Text = Color.white;

        /// <summary>White at reduced strength, for a secondary line under a caption.</summary>
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.86f);

        /// <summary>
        /// Dark text for pale panels — the yellow daily strip, the cream cards.
        ///
        /// A warm near-black rather than a maroon: maroon on saturated yellow is a low-contrast
        /// pairing that turns muddy below about 30px, which the day names proved.
        /// </summary>
        public static readonly Color TextOnPale = new Color(0.28f, 0.13f, 0.02f, 1f);

        /// <summary>
        /// The dark outline behind white text.
        ///
        /// The candy background is busy and pale in places, so unoutlined white loses its edges over
        /// the clouds. Raise the alpha for more separation; drop OutlineOffset to zero to remove it.
        /// </summary>
        public static readonly Color OutlineColour = new Color(0.20f, 0.07f, 0.24f, 0.55f);

        public const float OutlineOffset = 2.5f;

        // --- controls -----------------------------------------------------------------------

        /// <summary>Height of a full-width primary button.</summary>
        public const float PrimaryButtonHeight = 162f;

        /// <summary>Height of a half-width or secondary button.</summary>
        public const float SecondaryButtonHeight = 148f;

        /// <summary>Diameter of a round icon button in a status bar.</summary>
        public const float StatusButtonSize = 116f;

        /// <summary>Diameter of the large round buttons in the menu's bottom row.</summary>
        public const float DiscButtonSize = 142f;

        /// <summary>Height of the coin pill and similar status readouts.</summary>
        public const float PillHeight = 88f;

        /// <summary>
        /// How far a button shrinks while held. 1 is no feedback; below about 0.9 it feels rubbery.
        ///
        /// Scale rather than the usual colour tint, because darkening glossy artwork reads as the
        /// button going muddy rather than going down.
        /// </summary>
        public const float PressScale = 0.94f;

        // --- layout -------------------------------------------------------------------------

        /// <summary>
        /// The design canvas. Every position in the game is expressed in these units and scaled to
        /// the real screen, so a number here means the same thing on every phone.
        /// </summary>
        public const float CanvasWidth = 1080f;
        public const float CanvasHeight = 1920f;

        /// <summary>Gap from the screen edge to anything that should not touch it.</summary>
        public const float ScreenMargin = 60f;

        /// <summary>Vertical gap between stacked elements.</summary>
        public const float Gap = 24f;

        /// <summary>
        /// Which sprite to ask for when you want a given colour.
        ///
        /// Button colour is a choice of artwork, not a tint, so changing one means swapping the name
        /// passed to ArtKit.Ui. These are the wide rounded-rectangle buttons; there are matching
        /// `circle_*` discs and `pill_*` capsules.
        /// </summary>
        public static class Swatch
        {
            public const string Pink = "tile_pink";
            public const string Purple = "tile_purple";
            public const string Cyan = "tile_cyan";
            public const string Green = "tile_green";
            public const string Yellow = "tile_yellow";
            public const string Navy = "tile_navy";
        }
    }
}
