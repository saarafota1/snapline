using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using Snapline.Core;

namespace Snapline.View
{
    /// <summary>
    /// Draws the 8x8 grid and animates everything that happens on it.
    ///
    /// Holds no game state. It is told what the board looks like and what just happened; it never
    /// decides anything. The engine could be swapped for a different mechanic and this would still
    /// draw squares.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private RectTransform _grid;
        private RectTransform _ghostLayer;
        private BlockPool _pool;
        private Juice _juice;

        private readonly Image[] _blocks = new Image[Board.CellCount];
        private readonly int[] _blockColour = new int[Board.CellCount];
        private readonly Image[] _cells = new Image[Board.CellCount];
        private Image[] _ghost;

        private float _cellSize;
        private float _gap;

        public float CellSize => _cellSize;
        public RectTransform Grid => _grid;

        public void Init(RectTransform grid, RectTransform ghostLayer, Juice juice, float cellSize, float gap)
        {
            _grid = grid;
            _ghostLayer = ghostLayer;
            _juice = juice;
            _cellSize = cellSize;
            _gap = gap;

            BuildEmptyCells();
            _pool = new BlockPool(_grid);
            BuildGhost();
        }

        private void BuildEmptyCells()
        {
            Sprite cellSprite = ArtKit.EmptyCell();

            for (int row = 0; row < Board.Height; row++)
            {
                for (int col = 0; col < Board.Width; col++)
                {
                    Image img = UIKit.Image($"cell{col}_{row}", _grid, cellSprite, Color.white,
                                            Image.Type.Sliced);
                    RectTransform rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(_cellSize, _cellSize);
                    rt.anchoredPosition = CellToLocal(col, row);
                    _cells[Bits.Index(col, row)] = img;
                }
            }
        }

        private void BuildGhost()
        {
            // The ghost never needs more cells than the largest shape.
            _ghost = new Image[Shapes.MaxCellCount];
            Sprite sprite = ArtKit.Solid();

            for (int i = 0; i < _ghost.Length; i++)
            {
                Image img = UIKit.Image($"ghost{i}", _ghostLayer, sprite, Palette.GhostValid, Image.Type.Sliced);
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_cellSize, _cellSize);
                img.gameObject.SetActive(false);
                _ghost[i] = img;
            }
        }

        // --- coordinates ------------------------------------------------------------------

        /// <summary>
        /// Centre of a cell in grid-local space. The grid rect is pivoted top-left, so rows run
        /// downward in negative Y — matching the engine, where row 0 is the top.
        /// </summary>
        public Vector2 CellToLocal(int col, int row)
        {
            float step = _cellSize + _gap;
            return new Vector2(col * step + _cellSize * 0.5f,
                               -(row * step + _cellSize * 0.5f));
        }

        /// <summary>Nearest cell to a grid-local point. Returns false if it is off the board.</summary>
        public bool LocalToCell(Vector2 local, out int col, out int row)
        {
            float step = _cellSize + _gap;
            col = Mathf.RoundToInt((local.x - _cellSize * 0.5f) / step);
            row = Mathf.RoundToInt((-local.y - _cellSize * 0.5f) / step);
            return col >= 0 && col < Board.Width && row >= 0 && row < Board.Height;
        }

        /// <summary>Cell centre in world space, for spawning effects on the layer above.</summary>
        public Vector3 CellToWorld(int col, int row) => _grid.TransformPoint(CellToLocal(col, row));

        // --- state ------------------------------------------------------------------------

        /// <summary>Redraw every block from scratch. Used on restore and on a new run.</summary>
        public void SyncFromBoard(Board board)
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] == null) continue;
                _pool.Return(_blocks[i]);
                _blocks[i] = null;
            }

            for (int row = 0; row < Board.Height; row++)
            {
                for (int col = 0; col < Board.Width; col++)
                {
                    if (!board.IsOccupied(col, row)) continue;
                    SetBlock(col, row, board.ColourAt(col, row));
                }
            }
        }

        public void SetBlock(int col, int row, int colourIndex)
        {
            int idx = Bits.Index(col, row);
            if (_blocks[idx] != null) _pool.Return(_blocks[idx]);
            _blocks[idx] = _pool.Take(colourIndex, _cellSize, CellToLocal(col, row));
            _blockColour[idx] = colourIndex;
        }

        // --- animation --------------------------------------------------------------------

        /// <summary>
        /// Pop each cell of a freshly placed piece, staggered so the shape reads as landing.
        ///
        /// Cells that this same move cleared still get their block created — the clear animation
        /// needs something to blow up — but they skip the pop, or the two animations would fight
        /// over the same transform and the explosion would visibly stutter.
        /// </summary>
        public void AnimatePlacement(ulong pieceMask, int colourIndex, ulong clearedMask)
        {
            int order = 0;
            ulong m = pieceMask;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;

                int col = Bits.ColOf(idx);
                int row = Bits.RowOf(idx);
                SetBlock(col, row, colourIndex);

                if ((clearedMask & (1UL << idx)) != 0UL) continue;

                StartCoroutine(PopIn(_blocks[idx].rectTransform, order * 0.022f));
                order++;
            }
        }

        private IEnumerator PopIn(RectTransform rt, float delay)
        {
            if (rt == null) yield break;

            rt.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (rt == null) yield break;

            const float duration = 0.16f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (rt == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                // Overshoot slightly past full size, then settle. Reads as weight.
                float scale = k < 0.7f
                    ? Mathf.Lerp(0f, 1.14f, k / 0.7f)
                    : Mathf.Lerp(1.14f, 1f, (k - 0.7f) / 0.3f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            if (rt != null) rt.localScale = Vector3.one;
        }

        /// <summary>
        /// Blow up every cleared cell. Each line gets a flash along its length and each block
        /// explodes into debris, staggered outward from the line centre so a clear sweeps rather
        /// than popping all at once.
        /// </summary>
        public void AnimateClear(in PlaceResult result)
        {
            if (result.ClearedMask == 0UL) return;

            for (int r = 0; r < Board.Height; r++)
            {
                if ((result.ClearedRowFlags & (1 << r)) == 0) continue;
                Vector2 centre = CellToLocal(Board.Width / 2, r);
                _juice.Flash(GridToEffectSpace(centre), new Color(1f, 1f, 1f, 0.9f), _cellSize * 3.4f);
            }

            for (int c = 0; c < Board.Width; c++)
            {
                if ((result.ClearedColFlags & (1 << c)) == 0) continue;
                Vector2 centre = CellToLocal(c, Board.Height / 2);
                _juice.Flash(GridToEffectSpace(centre), new Color(1f, 1f, 1f, 0.9f), _cellSize * 3.4f);
            }

            ulong m = result.ClearedMask;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;

                int col = Bits.ColOf(idx);
                int row = Bits.RowOf(idx);

                Image block = _blocks[idx];
                _blocks[idx] = null;
                if (block == null) continue;

                BlockColour bc = Palette.Block(_blockColour[idx]);

                // Distance from the middle of the board drives the stagger, so the explosion
                // travels outward from where the piece landed rather than firing uniformly.
                float dist = Vector2.Distance(new Vector2(col, row), new Vector2(3.5f, 3.5f));
                float delay = dist * 0.018f;

                StartCoroutine(ExplodeBlock(block, GridToEffectSpace(CellToLocal(col, row)), bc, delay));
            }
        }

        private IEnumerator ExplodeBlock(Image block, Vector2 effectPosition, BlockColour colour, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null) yield break;

            _juice.Burst(effectPosition, colour.Glow, 11, _cellSize * 8.5f, _cellSize * 0.46f);
            _juice.Burst(effectPosition, colour.Top, 5, _cellSize * 4.5f, _cellSize * 0.30f);
            _juice.Flash(effectPosition, colour.Top, _cellSize * 1.5f, 0.24f);

            RectTransform rt = block.rectTransform;
            const float duration = 0.17f;
            float t = 0f;
            Color start = block.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null) yield break;

                float k = Mathf.Clamp01(t / duration);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, k);
                Color c = Color.Lerp(start, Color.white, k * 0.8f);
                c.a = 1f - k;
                block.color = c;
                yield return null;
            }

            _pool.Return(block);
        }

        /// <summary>
        /// Grid-local coordinates converted into the effect layer's space. The two layers are
        /// siblings under different parents, so going through world space is what keeps effects
        /// aligned with the grid when the board shakes.
        /// </summary>
        private Vector2 GridToEffectSpace(Vector2 gridLocal)
        {
            Vector3 world = _grid.TransformPoint(gridLocal);
            return _juice.transform.InverseTransformPoint(world);
        }

        // --- ghost preview ------------------------------------------------------------------

        /// <summary>Show where a dragged piece would land. Pass a null shape to hide it.</summary>
        public void ShowGhost(ShapeDef shape, int col, int row, bool valid)
        {
            if (shape == null) { HideGhost(); return; }

            Color tint = valid ? Palette.GhostValid : Palette.GhostInvalid;

            for (int i = 0; i < _ghost.Length; i++)
            {
                if (i >= shape.CellCount) { _ghost[i].gameObject.SetActive(false); continue; }

                (int Col, int Row) cell = shape.Cells[i];
                int c = col + cell.Col;
                int r = row + cell.Row;

                if (c < 0 || c >= Board.Width || r < 0 || r >= Board.Height)
                {
                    _ghost[i].gameObject.SetActive(false);
                    continue;
                }

                _ghost[i].rectTransform.anchoredPosition = CellToLocal(c, r);
                _ghost[i].color = tint;
                _ghost[i].gameObject.SetActive(true);
            }
        }

        public void HideGhost()
        {
            if (_ghost == null) return;
            for (int i = 0; i < _ghost.Length; i++) _ghost[i].gameObject.SetActive(false);
        }

        /// <summary>Flash the whole grid red-ish when the run ends.</summary>
        public IEnumerator PlayGameOverSweep()
        {
            for (int row = 0; row < Board.Height; row++)
            {
                for (int col = 0; col < Board.Width; col++)
                {
                    int idx = Bits.Index(col, row);
                    if (_blocks[idx] == null) continue;
                    StartCoroutine(Desaturate(_blocks[idx]));
                }
                yield return new WaitForSeconds(0.045f);
            }
        }

        private IEnumerator Desaturate(Image block)
        {
            const float duration = 0.35f;
            float t = 0f;
            Color start = block.color;
            var target = new Color(0.45f, 0.47f, 0.6f, 1f);

            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null) yield break;
                block.color = Color.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }
    }
}
