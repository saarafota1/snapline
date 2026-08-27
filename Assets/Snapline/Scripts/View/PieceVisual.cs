using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;

namespace Snapline.View
{
    /// <summary>
    /// The blocks that make up one offered piece, laid out exactly like the board lays out cells.
    ///
    /// The root is pivoted at its top-left and uses the same step as the grid, so aligning a piece
    /// with the board is a single subtraction rather than a special case per shape.
    /// </summary>
    public sealed class PieceVisual
    {
        public RectTransform Root { get; private set; }
        public ShapeDef Shape { get; private set; }
        public int ColourIndex { get; private set; }

        private readonly List<Image> _blocks = new List<Image>();
        private float _cellSize;
        private float _gap;

        public bool HasShape => Shape != null;

        /// <summary>Size of the piece's bounding box at the current cell size.</summary>
        public Vector2 BoundingSize
        {
            get
            {
                if (Shape == null) return Vector2.zero;
                float step = _cellSize + _gap;
                return new Vector2(Shape.Width * step - _gap, Shape.Height * step - _gap);
            }
        }

        public PieceVisual(RectTransform parent, string name)
        {
            Root = UIKit.Rect(name, parent);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = Vector2.zero;
        }

        public void Set(ShapeDef shape, int colourIndex, float cellSize, float gap)
        {
            Shape = shape;
            ColourIndex = colourIndex;
            _cellSize = cellSize;
            _gap = gap;

            if (shape == null)
            {
                foreach (Image b in _blocks) b.gameObject.SetActive(false);
                Root.gameObject.SetActive(false);
                return;
            }

            Root.gameObject.SetActive(true);
            Sprite sprite = ArtKit.Block(colourIndex);
            float step = cellSize + gap;

            for (int i = 0; i < shape.CellCount; i++)
            {
                Image img = i < _blocks.Count ? _blocks[i] : CreateBlock();
                (int Col, int Row) cell = shape.Cells[i];

                RectTransform rt = img.rectTransform;
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(cell.Col * step + cellSize * 0.5f,
                                                  -(cell.Row * step + cellSize * 0.5f));
                img.sprite = sprite;
                img.color = Color.white;
                img.gameObject.SetActive(true);
            }

            for (int i = shape.CellCount; i < _blocks.Count; i++) _blocks[i].gameObject.SetActive(false);
        }

        /// <summary>Rescale in place, keeping the same shape. Used when a piece is lifted off the tray.</summary>
        public void SetCellSize(float cellSize, float gap)
        {
            if (Shape == null) return;
            Set(Shape, ColourIndex, cellSize, gap);
        }

        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (!_blocks[i].gameObject.activeSelf) continue;
                Color c = _blocks[i].color;
                c.a = alpha;
                _blocks[i].color = c;
            }
        }

        public void Clear() => Set(null, 0, _cellSize, _gap);

        private Image CreateBlock()
        {
            Image img = UIKit.Image($"b{_blocks.Count}", Root, null, Color.white);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            _blocks.Add(img);
            return img;
        }
    }
}
