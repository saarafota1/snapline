using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.View
{
    /// <summary>
    /// Recycles the Image objects used to draw blocks.
    ///
    /// Needed because a cleared block has to keep animating after the engine already considers its
    /// cell empty, and the player may drop a new piece into that same cell on the very next frame.
    /// Owning the visuals separately from the grid is what lets both happen at once.
    /// </summary>
    public sealed class BlockPool
    {
        private readonly Stack<Image> _free = new Stack<Image>();
        private readonly RectTransform _parent;

        public BlockPool(RectTransform parent, int prewarm = 96)
        {
            _parent = parent;
            for (int i = 0; i < prewarm; i++) _free.Push(Create());
        }

        private Image Create()
        {
            var go = new GameObject("block", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            go.SetActive(false);
            return img;
        }

        public Image Take(int colourIndex, float size, Vector2 anchoredPosition)
        {
            Image img = _free.Count > 0 ? _free.Pop() : Create();

            img.sprite = ArtKit.Block(colourIndex);
            img.color = Color.white;

            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();

            img.gameObject.SetActive(true);
            return img;
        }

        public void Return(Image img)
        {
            if (img == null) return;
            img.gameObject.SetActive(false);
            img.rectTransform.localScale = Vector3.one;
            img.rectTransform.localRotation = Quaternion.identity;
            img.color = Color.white;
            _free.Push(img);
        }
    }
}
