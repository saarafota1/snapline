using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The vocabulary of the candy interface: sprite-backed buttons, icons and labels.
    ///
    /// <see cref="UIKit"/> builds controls out of generated rounded rectangles, which was right when
    /// every pixel was generated. These screens are built from authored artwork instead, and the
    /// difference is not just the sprite — a drawn button carries its own shadow and highlight, so
    /// it wants no tint, a nine-sliced fill, and a press that scales rather than darkens.
    ///
    /// Kept here rather than in the kit because it is this game's look, not a general one.
    /// </summary>
    public static class CandyUI
    {
        /// <summary>
        /// Text colour on the candy palette. The artwork is bright and saturated, so the near-white
        /// used on the old dark navy has too little contrast; captions carry a dark outline instead.
        /// </summary>
        public static Color Caption => Design.Text;
        public static Color CaptionDim => Design.TextDim;
        public static Color CaptionDark => Design.TextOnPale;

        /// <summary>
        /// Small text on the yellow panels.
        ///
        /// <see cref="CaptionDark"/> is a maroon, and maroon on saturated yellow is a low-contrast
        /// pairing that goes muddy below about 30px — the day names were the proof. A warm near-black
        /// keeps the same warmth and reads at any size.
        /// </summary>
        public static Color CaptionOnYellow => Design.TextOnPale;

        /// <summary>
        /// A button whose entire appearance is one sprite.
        ///
        /// The tint stays white: these sprites are drawn with their own lighting, and multiplying a
        /// colour through them muddies the gloss that makes them read as candy. Feedback comes from
        /// <see cref="PressScale"/> instead of the usual colour swap.
        /// </summary>
        public static Button SpriteButton(string name, Transform parent, Sprite sprite,
                                          Image.Type type = Image.Type.Simple)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = Color.white;
            img.raycastTarget = true;

            // No alphaHitTestMinimumThreshold here, tempting as it is. It would clip the hit area to
            // the drawn shape rather than the sprite's bounding box, but Unity throws unless the
            // texture is CPU-readable or crunch-compressed — one doubles the memory, the other is
            // lossy on exactly the smooth gradients this art is made of. The layout keeps controls
            // far enough apart that a rectangular hit area costs nothing.

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            go.AddComponent<PressScale>();

            return button;
        }

        /// <summary>A sprite button with a centred caption laid over it.</summary>
        public static Button SpriteButton(string name, Transform parent, Sprite sprite, string caption,
                                          int fontSize, Color captionColour, Image.Type type = Image.Type.Sliced)
        {
            Button button = SpriteButton(name, parent, sprite, type);
            Text label = Label("Caption", button.transform, caption, fontSize, captionColour);
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>
        /// A label with an outline. Every one of these screens sits on a busy pastel background, so
        /// plain white text loses its edges over the pale parts of it.
        /// </summary>
        public static Text Label(string name, Transform parent, string content, int size, Color colour,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = true)
        {
            Text label = UIKit.Label(name, parent, content, size, colour);
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            if (outline)
            {
                var o = label.gameObject.AddComponent<Outline>();
                o.effectColor = Design.OutlineColour;
                o.effectDistance = new Vector2(Design.OutlineOffset, -Design.OutlineOffset);
            }

            return label;
        }

        /// <summary>
        /// A dark wash across a whole screen, behind its contents.
        ///
        /// INTERIM. The screens that have not been restyled yet were drawn for the old near-black
        /// background — dim panels, low-contrast locked states, white text with no outline. Against
        /// the candy background those choices stop working; the level grid in particular becomes
        /// unreadable, because its locked tiles were a slightly lighter dark and are now a slightly
        /// lighter *photograph*.
        ///
        /// This restores the contrast they were designed against until each one is properly
        /// restyled, at which point its scrim should go. It is not a design; it is scaffolding, and
        /// leaving it in place permanently would waste the background on every screen but one.
        /// </summary>
        public static Image Scrim(Transform parent, float opacity = 0.72f)
        {
            Image img = UIKit.Image("Scrim", parent, ProcArt.Solid(), new Color(0.04f, 0.05f, 0.14f, opacity));
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Deliberately left at the end of the child list, so it covers whatever has been built
            // so far and is covered by whatever comes next. These screens paint their own
            // full-screen background, so a scrim forced to the back would sit behind it and do
            // nothing at all — which is exactly what happened the first time.
            return img;
        }

        /// <summary>A non-interactive sprite, sized and placed by the caller.</summary>
        public static Image Icon(string name, Transform parent, Sprite sprite)
        {
            Image img = UIKit.Image(name, parent, sprite, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Shorthand for the anchored-centre placement nearly everything here uses.</summary>
        public static void Place(Component c, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var rt = c is RectTransform r ? r : (RectTransform)c.transform;
            UIKit.Place(rt, anchor, anchor, new Vector2(0.5f, 0.5f), offset, size);
        }
    }

    /// <summary>
    /// Squashes a button slightly while it is held.
    ///
    /// The usual uGUI colour tint is wrong for artwork that carries its own shading — darkening a
    /// glossy sprite reads as the button going muddy rather than going down. Scale reads as a press
    /// on any sprite, whatever its colour.
    /// </summary>
    public sealed class PressScale : MonoBehaviour,
        UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler
    {
        private static float Pressed => Design.PressScale;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData _) =>
            transform.localScale = Vector3.one * Pressed;

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData _) =>
            transform.localScale = Vector3.one;

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
