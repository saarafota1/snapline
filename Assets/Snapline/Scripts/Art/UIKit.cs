using UnityEngine;
using UnityEngine.UI;

namespace Snapline.Art
{
    /// <summary>
    /// Small helpers for assembling the UI in code. The whole interface is built at runtime, so
    /// there is no scene to hand-edit, nothing to lose in a merge, and layout is reviewable as
    /// source rather than as a binary asset.
    /// </summary>
    public static class UIKit
    {
        private static Font _font;

        /// <summary>
        /// The UI font.
        ///
        /// Deliberately a built-in font rather than TextMeshPro: TMP needs its "Essential Resources"
        /// imported once through an editor menu, and without them text renders as nothing at all
        /// while the build still succeeds. That is precisely the silent failure the playbook warns
        /// about, and not worth it for a UI that is mostly numbers. Upgrading later is a contained
        /// change, since every label in the game is created through this file.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                _font = TryBuiltin("LegacyRuntime.ttf") ?? TryBuiltin("Arial.ttf");

                if (_font == null)
                {
                    // Last resort on a machine with neither builtin present.
                    _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                }

                if (_font == null)
                    Debug.LogError("[Snapline] No usable UI font was found; labels will not render.");

                return _font;
            }
        }

        private static Font TryBuiltin(string name)
        {
            try { return Resources.GetBuiltinResource<Font>(name); }
            catch { return null; }
        }

        // --- construction ----------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>A rect that fills its parent, optionally inset by a uniform margin.</summary>
        public static RectTransform Stretch(string name, Transform parent, float margin = 0f)
        {
            RectTransform rt = Rect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color colour,
                                  Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = colour;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string content, int size,
                                 Color colour, TextAnchor anchor = TextAnchor.MiddleCenter,
                                 bool bold = true, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = colour;
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.55f);
                o.effectDistance = new Vector2(2.5f, -2.5f);
            }

            return text;
        }

        /// <summary>A rounded button with a label. Returns the Button; the label is its only child.</summary>
        public static Button Button(string name, Transform parent, string caption, Color fill,
                                    Color captionColour, int fontSize = 46)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = ArtKit.RoundedRect($"btn_{name}", fill, new Color(1f, 1f, 1f, 0.28f), 3f);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;

            var colours = button.colors;
            colours.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colours.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            Text label = Label("Caption", rt, caption, fontSize, captionColour);
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>Set anchors, pivot, position and size in one call, since layout here is all code.</summary>
        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                 Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }
    }
}
