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
        public static readonly Color Caption = Color.white;
        public static readonly Color CaptionDim = new Color(1f, 1f, 1f, 0.86f);
        public static readonly Color CaptionDark = new Color(0.35f, 0.14f, 0.30f, 1f);

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
                o.effectColor = new Color(0.20f, 0.07f, 0.24f, 0.55f);
                o.effectDistance = new Vector2(2.5f, -2.5f);
            }

            return label;
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
        private const float Pressed = 0.94f;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData _) =>
            transform.localScale = Vector3.one * Pressed;

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData _) =>
            transform.localScale = Vector3.one;

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
