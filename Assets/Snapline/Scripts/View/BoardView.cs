using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using Snapline.UI;
using GameKit.Art;

namespace Snapline.View
{
    /// <summary>
    /// Draws the 8x8 grid and animates everything that happens on it.
    ///
    /// Holds no game state. It is told what the board looks like and what just happened; it never
    /// decides anything. It does keep a reference to the engine's board, read-only, so that while a
    /// piece is being dragged it can show which lines that drop would clear.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private RectTransform _grid;
        private RectTransform _ghostLayer;
        private BlockPool _pool;
        private Board _model;

        private readonly Image[] _blocks = new Image[Board.CellCount];
        private readonly int[] _blockColour = new int[Board.CellCount];
        private readonly Special[] _blockSpecial = new Special[Board.CellCount];
        private readonly Image[] _cells = new Image[Board.CellCount];
        private Image[] _ghost;

        private float _cellSize;
        private float _gap;

        private ulong _previewMask;

        private Image _tapCatcher;
        private Action<int, int> _onTap;
        private bool _hammerArmed;
        private Image _hammer;

        public float CellSize => _cellSize;
        public RectTransform Grid => _grid;

        public void Init(RectTransform grid, RectTransform ghostLayer, float cellSize, float gap)
        {
            _grid = grid;
            _ghostLayer = ghostLayer;
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
                    Image img = UIKit.Image($"cell{col}_{row}", _grid, cellSprite, Palette.EmptyCellTint, Image.Type.Simple);
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
            _ghost = new Image[Shapes.MaxCellCount];
            for (int i = 0; i < _ghost.Length; i++)
            {
                Image img = UIKit.Image($"ghost{i}", _ghostLayer, ArtKit.Block(0), Color.white);
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_cellSize, _cellSize);
                img.gameObject.SetActive(false);
                _ghost[i] = img;
            }
        }

        // --- coordinates ------------------------------------------------------------------

        /// <summary>Centre of a cell in grid-local space. Rows run downward, matching the engine.</summary>
        public Vector2 CellToLocal(int col, int row)
        {
            float step = _cellSize + _gap;
            return new Vector2(col * step + _cellSize * 0.5f, -(row * step + _cellSize * 0.5f));
        }

        public bool LocalToCell(Vector2 local, out int col, out int row)
        {
            float step = _cellSize + _gap;
            col = Mathf.RoundToInt((local.x - _cellSize * 0.5f) / step);
            row = Mathf.RoundToInt((-local.y - _cellSize * 0.5f) / step);
            return col >= 0 && col < Board.Width && row >= 0 && row < Board.Height;
        }

        public Vector3 CellToWorld(int col, int row) => _grid.TransformPoint(CellToLocal(col, row));

        private Vector3 IndexToWorld(int idx) => CellToWorld(Bits.ColOf(idx), Bits.RowOf(idx));

        /// <summary>The middle of the board, in world space.</summary>
        public Vector3 CentreWorld => _grid.TransformPoint(new Vector2(GridExtent * 0.5f, -GridExtent * 0.5f));

        private float GridExtent => Board.Width * _cellSize + (Board.Width - 1) * _gap;

        // --- state ------------------------------------------------------------------------

        /// <summary>Redraw every block from scratch. With <paramref name="cascade"/>, they pop in row by row.</summary>
        public void SyncFromBoard(Board board, bool cascade = false)
        {
            _model = board;
            ClearPreview();

            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] == null) continue;
                _pool.Return(_blocks[i]);
                _blocks[i] = null;
                _blockSpecial[i] = Special.None;
            }

            for (int row = 0; row < Board.Height; row++)
            {
                for (int col = 0; col < Board.Width; col++)
                {
                    if (!board.IsOccupied(col, row)) continue;
                    SetBlock(col, row, board.ColourAt(col, row), board.SpecialAt(col, row));
                    if (cascade)
                        Tween.PopIn(_blocks[Bits.Index(col, row)].rectTransform, 0.05f + row * 0.04f + col * 0.015f, 0.35f, 0f);
                }
            }
        }

        public void SetBlock(int col, int row, int colourIndex, Special special = Special.None)
        {
            int idx = Bits.Index(col, row);
            if (_blocks[idx] != null) _pool.Return(_blocks[idx]);
            _blocks[idx] = _pool.Take(colourIndex, _cellSize, CellToLocal(col, row));
            _blockColour[idx] = colourIndex;
            _blockSpecial[idx] = special;
            Dress(idx);
        }

        private Sprite BaseSprite(int idx) => _blockSpecial[idx] switch
        {
            Special.Stone => SpecialArt.Stone(),
            Special.CrackedStone => SpecialArt.CrackedStone(),
            _ => ArtKit.Block(_blockColour[idx]),
        };

        /// <summary>Gives a block the look of whatever special it holds: a stone face, or a bomb or gift icon on top.</summary>
        private void Dress(int idx)
        {
            Image block = _blocks[idx];
            if (block == null) return;

            block.sprite = BaseSprite(idx);
            Special s = _blockSpecial[idx];
            bool icon = s == Special.Bomb || s == Special.Gift;

            Image overlay = BlockPool.Overlay(block);
            overlay.gameObject.SetActive(icon);
            if (!icon) return;
            overlay.sprite = s == Special.Bomb ? SpecialArt.Bomb() : SpecialArt.Gift();
            overlay.rectTransform.localScale = Vector3.one;
        }

        // --- placement and clears -----------------------------------------------------------

        /// <summary>A piece lands: each cell drops in with a squash, and a puff of sparkle marks where.</summary>
        public void AnimatePlacement(ulong pieceMask, int colourIndex, ulong clearedMask)
        {
            ClearPreview();
            int order = 0;
            Vector3 centre = Vector3.zero;
            int cells = 0;

            ulong m = pieceMask;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;

                int col = Bits.ColOf(idx);
                int row = Bits.RowOf(idx);
                SetBlock(col, row, colourIndex);
                centre += CellToWorld(col, row);
                cells++;

                if ((clearedMask & (1UL << idx)) != 0UL) continue;

                RectTransform rt = _blocks[idx].rectTransform;
                float delay = order * 0.018f;
                rt.localScale = Vector3.one * 1.3f;
                Tween.Run(rt, 0.3f, k => rt.localScale = Vector3.one * Mathf.LerpUnclamped(1.3f, 1f, Ease.OutBack(k, 2.4f)), delay);
                order++;
            }

            if (cells > 0 && Fx.Instance != null)
            {
                centre /= cells;
                Fx.Instance.Sparkles(centre, 4, _cellSize * 1.2f, _cellSize * 0.6f);
                Fx.Instance.Glow(centre, new Color(1f, 1f, 1f, 0.35f), _cellSize * 3f, 0.3f);
            }
        }

        /// <summary>
        /// Blow up every cleared cell, crack every stone that was hit, and set the bombs off.
        ///
        /// Each line gets a golden sweep, each removed block charges gold and bursts into shards of
        /// its own candy — staggered outward from where the piece landed, and a beat later again for
        /// blocks caught only in a blast, so the explosion visibly travels out from the bomb.
        /// </summary>
        public void AnimateClear(in PlaceResult result, int originCol, int originRow)
        {
            if (result.ClearedMask == 0UL && result.CrackedMask == 0UL) return;
            ClearPreview();

            Fx fx = Fx.Instance;
            float half = _cellSize * 0.5f;

            if (fx != null)
            {
                for (int r = 0; r < Board.Height; r++)
                {
                    if ((result.ClearedRowFlags & (1 << r)) == 0) continue;
                    fx.LineSweep(_grid.TransformPoint(CellToLocal(0, r) - new Vector2(half, 0f)),
                                 _grid.TransformPoint(CellToLocal(Board.Width - 1, r) + new Vector2(half, 0f)), _cellSize);
                }

                for (int c = 0; c < Board.Width; c++)
                {
                    if ((result.ClearedColFlags & (1 << c)) == 0) continue;
                    fx.LineSweep(_grid.TransformPoint(CellToLocal(c, 0) + new Vector2(0f, half)),
                                 _grid.TransformPoint(CellToLocal(c, Board.Height - 1) - new Vector2(0f, half)), _cellSize);
                }

                ulong bombs = result.BombMask;
                while (bombs != 0UL)
                {
                    int idx = Bits.TrailingZeroCount(bombs);
                    bombs &= bombs - 1;
                    Vector3 world = IndexToWorld(idx);
                    fx.Ring(world, new Color(1f, 0.6f, 0.2f, 0.95f), _cellSize * 6.5f, 0.55f);
                    fx.Glow(world, new Color(1f, 0.75f, 0.3f, 0.95f), _cellSize * 5f, 0.45f);
                    fx.Sprinkles(world, 12, _cellSize * 15f, _cellSize * 0.42f);
                    fx.Sparkles(world, 10, _cellSize * 1.6f, _cellSize * 0.9f);
                    fx.Text(world, "KABOOM!", CandyStyle.Gold, 96f, 0.9f, 180f);
                }
            }

            // One CRACK! per move, however many stones it hits: two stones in a cleared row put two
            // labels on top of each other and on top of the clear's own shout.
            bool labelled = false;
            ulong k = result.CrackedMask;
            while (k != 0UL)
            {
                int idx = Bits.TrailingZeroCount(k);
                k &= k - 1;
                if (_blocks[idx] == null) continue;
                StartCoroutine(Crack(idx, !labelled));
                labelled = true;
            }

            int n = 0;
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

                bool gift = _blockSpecial[idx] == Special.Gift;
                _blockSpecial[idx] = Special.None;

                float dist = Vector2.Distance(new Vector2(col, row), new Vector2(originCol, originRow));
                float delay = dist * 0.028f + ((result.BlastMask & (1UL << idx)) != 0UL ? 0.14f : 0f);
                StartCoroutine(Burst(block, _blockColour[idx], CellToWorld(col, row), delay, n++ % 3 == 0, gift));
            }
        }

        /// <summary>A stone takes its first hit: it shudders, cracks, and chips fly.</summary>
        private IEnumerator Crack(int idx, bool label)
        {
            Image block = _blocks[idx];
            _blockSpecial[idx] = Special.CrackedStone;
            Vector3 world = IndexToWorld(idx);

            yield return new WaitForSeconds(0.06f);
            if (block == null || _blocks[idx] != block) yield break;

            block.sprite = SpecialArt.CrackedStone();
            Fx.Instance?.Shards(world, SpecialArt.Stone(), 6, _cellSize * 9f, _cellSize * 0.9f);
            Fx.Instance?.Glow(world, new Color(0.85f, 0.88f, 1f, 0.7f), _cellSize * 2.2f, 0.3f);
            if (label)
                Fx.Instance?.Text(world + (_grid.TransformVector(new Vector3(0f, _cellSize * 1.1f, 0f))),
                                  "CRACK!", CandyStyle.White, 60f, 0.7f, 120f);

            RectTransform rt = block.rectTransform;
            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null || _blocks[idx] != block) yield break;
                float s = 1f - t / duration;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 70f) * 10f * s);
                rt.localScale = Vector3.one * (1f + 0.1f * s);
                yield return null;
            }

            if (block != null && _blocks[idx] == block)
            {
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
        }

        private IEnumerator Burst(Image block, int colour, Vector3 world, float delay, bool sprinkles, bool gift = false)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null) yield break;

            // The cleared line turns to glowing gold and swells for a beat before it bursts, as the
            // reference's clear does — the moment of "that line is done" is what the eye catches.
            Sprite original = block.sprite;
            BlockPool.Overlay(block).gameObject.SetActive(false);
            block.sprite = ArtKit.Block(2);
            RectTransform rt = block.rectTransform;
            rt.SetAsLastSibling();
            const float charge = 0.08f;
            float c = 0f;
            while (c < charge)
            {
                c += Time.deltaTime;
                if (block == null) yield break;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, Ease.OutCubic(c / charge));
                yield return null;
            }
            if (block == null) yield break;

            Fx fx = Fx.Instance;
            if (fx != null)
            {
                BlockColour bc = Palette.Block(colour);
                fx.Shards(world, original, 3, _cellSize * 10f, _cellSize);
                fx.Glow(world, new Color(bc.Glow.r, bc.Glow.g, bc.Glow.b, 0.8f), _cellSize * 1.9f, 0.32f);
                if (sprinkles) fx.Sprinkles(world, 2, _cellSize * 11f, _cellSize * 0.34f);

                if (gift)
                {
                    fx.Text(world, $"+{GameRun.GiftMoves} MOVES", CandyStyle.Gold, 78f, 1.3f, 240f);
                    fx.Sparkles(world, 14, _cellSize * 1.5f, _cellSize);
                    fx.Ring(world, new Color(1f, 0.5f, 0.85f, 0.9f), _cellSize * 4f, 0.5f);
                }
            }

            const float duration = 0.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                rt.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.5f, Ease.OutCubic(k));
                rt.localRotation = Quaternion.Euler(0f, 0f, 18f * k);
                block.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }

            _pool.Return(block);
        }

        // --- the drag preview ---------------------------------------------------------------

        /// <summary>
        /// Show where a dragged piece would land — in its own colour, half transparent — and turn
        /// every block in a line that drop would complete into the piece's colour, so the player
        /// sees the clear before they commit to it. Stones that would only crack keep their face.
        /// </summary>
        public void ShowGhost(ShapeDef shape, int col, int row, bool valid, int colourIndex = 0)
        {
            ClearPreview();
            if (shape == null) { HideGhost(); return; }

            Sprite sprite = ArtKit.Block(colourIndex);
            Color tint = valid ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0.4f, 0.45f, 0.35f);
            ulong mask = 0UL;
            bool inside = true;

            for (int i = 0; i < _ghost.Length; i++)
            {
                if (i >= shape.CellCount) { _ghost[i].gameObject.SetActive(false); continue; }

                (int Col, int Row) cell = shape.Cells[i];
                int c = col + cell.Col;
                int r = row + cell.Row;

                if (c < 0 || c >= Board.Width || r < 0 || r >= Board.Height)
                {
                    _ghost[i].gameObject.SetActive(false);
                    inside = false;
                    continue;
                }

                mask |= 1UL << Bits.Index(c, r);
                _ghost[i].sprite = sprite;
                _ghost[i].rectTransform.anchoredPosition = CellToLocal(c, r);
                _ghost[i].color = tint;
                _ghost[i].gameObject.SetActive(true);
            }

            if (!valid || !inside || _model == null) return;

            ulong occupied = _model.Occupied;
            ulong cleared = (occupied | mask) & ~Board.Simulate(occupied, mask, _model.StickyMask);
            _previewMask = cleared & occupied;

            ulong m = _previewMask;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;
                if (_blocks[idx] != null) _blocks[idx].sprite = sprite;
            }

            if (cleared != 0UL)
                for (int i = 0; i < shape.CellCount && i < _ghost.Length; i++)
                    _ghost[i].color = new Color(1f, 1f, 1f, 0.85f);
        }

        public void HideGhost()
        {
            ClearPreview();
            if (_ghost == null) return;
            for (int i = 0; i < _ghost.Length; i++) _ghost[i].gameObject.SetActive(false);
        }

        private void ClearPreview()
        {
            ulong m = _previewMask;
            _previewMask = 0UL;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;
                if (_blocks[idx] == null) continue;
                _blocks[idx].sprite = BaseSprite(idx);
                _blocks[idx].rectTransform.localScale = Vector3.one;
            }
        }

        // --- tools --------------------------------------------------------------------------

        /// <summary>
        /// Arms or disarms the hammer. While armed the blocks tremble, and a tap on the board reports
        /// the cell it landed on instead of doing nothing.
        /// </summary>
        public void SetHammerMode(bool armed, Action<int, int> onTap)
        {
            _hammerArmed = armed;
            _onTap = onTap;

            if (_tapCatcher == null)
            {
                _tapCatcher = UIKit.Image("HammerTaps", _grid, ProcArt.Solid(), new Color(1f, 1f, 1f, 0f));
                RectTransform rt = _tapCatcher.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(GridExtent, GridExtent);
                _tapCatcher.gameObject.AddComponent<BoardTap>().View = this;
            }

            _tapCatcher.raycastTarget = armed;
            _tapCatcher.gameObject.SetActive(armed);
            _tapCatcher.rectTransform.SetAsLastSibling();

            if (armed) return;
            for (int i = 0; i < _blocks.Length; i++)
                if (_blocks[i] != null) _blocks[i].rectTransform.localRotation = Quaternion.identity;
        }

        internal void Tapped(Vector2 screen, Camera cam)
        {
            if (!_hammerArmed) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, screen, cam, out Vector2 local)) return;
            if (LocalToCell(local, out int col, out int row)) _onTap?.Invoke(col, row);
        }

        /// <summary>
        /// The hammer swings down onto a cell and every block in the smashed area shatters. The engine
        /// has already removed them; this only takes the picture away with a bang. The mask says which
        /// cells it took, so the view can never break a block the engine kept.
        /// </summary>
        public IEnumerator Smash(ulong mask, int col, int row)
        {
            int idx = Bits.Index(col, row);
            Vector2 target = CellToLocal(col, row);
            if (mask == 0UL) mask = 1UL << idx;

            if (_hammer == null)
            {
                _hammer = UIKit.Image("Hammer", _grid, ArtKit.Ui("icon_hammer"), Color.white);
                _hammer.raycastTarget = false;
                _hammer.preserveAspect = true;
                RectTransform hr = _hammer.rectTransform;
                hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
                hr.pivot = new Vector2(0.25f, 0.2f);
            }

            RectTransform h = _hammer.rectTransform;
            h.SetAsLastSibling();
            h.sizeDelta = new Vector2(_cellSize * 2.2f, _cellSize * 2.2f);
            h.anchoredPosition = target + new Vector2(-_cellSize * 0.2f, -_cellSize * 0.1f);
            _hammer.color = Color.white;
            _hammer.gameObject.SetActive(true);

            float t = 0f;
            const float swing = 0.2f;
            while (t < swing)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / swing);
                h.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -18f, Ease.InCubic(k)));
                h.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.05f, k);
                yield return null;
            }

            Vector3 world = CellToWorld(col, row);
            Fx fx = Fx.Instance;
            if (fx != null)
            {
                Sprite sprite = _blocks[idx] != null ? _blocks[idx].sprite : ArtKit.Block(0);
                fx.Shards(world, sprite, 14, _cellSize * 15f, _cellSize * 1.2f);
                fx.Sprinkles(world, 14, _cellSize * 14f, _cellSize * 0.45f);
                fx.Ring(world, new Color(1f, 0.95f, 0.7f, 0.9f), _cellSize * 6.5f, 0.5f);
                fx.Glow(world, new Color(1f, 1f, 1f, 0.9f), _cellSize * 4.5f, 0.32f);
                fx.Shake(0.75f);
            }

            Sound.Smash();
            Haptics.Heavy();

            // Outwards from the cell that was hit, so the area reads as one blow spreading rather
            // than nine blocks vanishing at once.
            ulong rest = mask;
            while (rest != 0UL)
            {
                int cell = Bits.TrailingZeroCount(rest);
                rest &= rest - 1;
                if (_blocks[cell] == null) continue;

                Image block = _blocks[cell];
                Vector3 at = CellToWorld(Bits.ColOf(cell), Bits.RowOf(cell));
                _blocks[cell] = null;
                _blockSpecial[cell] = Special.None;
                StartCoroutine(Burst(block, _blockColour[cell], at, cell == idx ? 0f : 0.05f, false));
                if (cell != idx) fx?.Sprinkles(at, 4, _cellSize * 9f, _cellSize * 0.32f);
            }

            t = 0f;
            const float lift = 0.25f;
            while (t < lift)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / lift);
                h.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-18f, 30f, k));
                _hammer.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }

            _hammer.gameObject.SetActive(false);
        }

        /// <summary>A shimmer across the board, for an undo putting things back.</summary>
        public void Shimmer()
        {
            if (Fx.Instance == null) return;
            Fx.Instance.Sparkles(CentreWorld, 16, GridExtent * 0.45f, _cellSize * 0.9f);
            Tween.Punch(_grid.parent, 0.03f, 0.3f);
        }

        private void Update()
        {
            float time = Time.unscaledTime;

            if (_hammerArmed)
            {
                for (int i = 0; i < _blocks.Length; i++)
                    if (_blocks[i] != null)
                        _blocks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 22f + i * 1.7f) * 4f);
            }

            // Bombs tick: the icon throbs, so the eye finds them on a busy board.
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] == null || _blockSpecial[i] != Special.Bomb) continue;
                Image overlay = BlockPool.Overlay(_blocks[i]);
                if (!overlay.gameObject.activeSelf) continue;
                overlay.rectTransform.localScale = Vector3.one * (1f + 0.09f * Mathf.Max(0f, Mathf.Sin(time * 7f + i)));
            }

            if (_previewMask != 0UL)
            {
                float s = 1f + 0.06f * (Mathf.Sin(time * 14f) + 1f) * 0.5f;
                ulong m = _previewMask;
                while (m != 0UL)
                {
                    int idx = Bits.TrailingZeroCount(m);
                    m &= m - 1;
                    if (_blocks[idx] != null) _blocks[idx].rectTransform.localScale = Vector3.one * s;
                }
            }
        }

        /// <summary>The board drains of colour, row by row, when a run ends.</summary>
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

        private static IEnumerator Desaturate(Image block)
        {
            const float duration = 0.35f;
            float t = 0f;
            Color start = block.color;
            var target = new Color(0.5f, 0.52f, 0.64f, 1f);

            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null) yield break;
                block.color = Color.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }
    }

    /// <summary>Forwards taps on the board to the view while the hammer is armed.</summary>
    public sealed class BoardTap : MonoBehaviour, IPointerClickHandler
    {
        public BoardView View;

        public void OnPointerClick(PointerEventData eventData) =>
            View?.Tapped(eventData.position, eventData.pressEventCamera);
    }
}
