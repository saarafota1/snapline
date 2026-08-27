using System;
using UnityEngine;
using Snapline.Core;

namespace Snapline.View
{
    /// <summary>
    /// Turns a finger drag into a board placement.
    ///
    /// Input is polled rather than routed through EventSystem drag callbacks. A tray piece is a
    /// loose bag of block images with gaps between them, so pointer-based hit testing would miss
    /// the holes in an L or an S; testing against the slot rectangle instead means the player can
    /// grab a piece anywhere near it, which is what they expect on a phone.
    ///
    /// The project uses the legacy Input Manager (activeInputHandler 0), so UnityEngine.Input is
    /// the right API here. It also covers touch, since Unity synthesises mouse events from touch 0.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        private BoardView _board;
        private TrayView _tray;
        private RectTransform _dragLayer;
        private Canvas _canvas;
        private Camera _camera;

        private int _activeSlot = -1;
        private bool _dragging;
        private Vector2 _grabOffset;

        private float _boardCellSize;
        private float _boardGap;

        /// <summary>
        /// How far above the finger the piece floats while dragging, in cells.
        ///
        /// Without this the thumb covers the piece and the cells it is about to fill, which is the
        /// difference between a game that feels precise and one that feels like guessing.
        /// </summary>
        public float LiftCells = 1.35f;

        /// <summary>Raised when the player releases over a legal cell.</summary>
        public event Action<int, int, int> PlacementRequested;

        /// <summary>Raised when the player releases somewhere illegal, so the piece can bounce back.</summary>
        public event Action<int> PlacementRejected;

        /// <summary>Asked whether a slot/cell combination is legal right now.</summary>
        public Func<int, int, int, bool> CanPlace;

        /// <summary>Asked for the shape in a slot, for the ghost preview. Null means empty.</summary>
        public Func<int, ShapeDef> ShapeInSlot;

        public bool IsDragging => _dragging;
        public bool InputEnabled { get; set; } = true;

        public void Init(BoardView board, TrayView tray, RectTransform dragLayer, Canvas canvas,
                         float boardCellSize, float boardGap)
        {
            _board = board;
            _tray = tray;
            _dragLayer = dragLayer;
            _canvas = canvas;
            _camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            _boardCellSize = boardCellSize;
            _boardGap = boardGap;
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                if (_dragging) CancelDrag();
                return;
            }

            if (!TryGetPointer(out Vector2 screenPoint, out bool pressed, out bool released)) return;

            if (!_dragging && pressed) TryBeginDrag(screenPoint);
            else if (_dragging && released) EndDrag(screenPoint);
            else if (_dragging) UpdateDrag(screenPoint);
        }

        /// <summary>
        /// Current pointer position and edge states. Touch is read directly where present so that a
        /// second finger cannot hijack a drag already in progress.
        /// </summary>
        private bool TryGetPointer(out Vector2 screenPoint, out bool pressed, out bool released)
        {
            screenPoint = default;
            pressed = false;
            released = false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                screenPoint = touch.position;
                pressed = touch.phase == TouchPhase.Began;
                released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                return true;
            }

            screenPoint = Input.mousePosition;
            pressed = Input.GetMouseButtonDown(0);
            released = Input.GetMouseButtonUp(0);
            return Input.GetMouseButton(0) || pressed || released;
        }

        private void TryBeginDrag(Vector2 screenPoint)
        {
            for (int slot = 0; slot < _tray.SlotCount; slot++)
            {
                PieceVisual piece = _tray.Piece(slot);
                if (!piece.HasShape) continue;

                RectTransform slotRect = _tray.Slot(slot);
                if (!RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPoint, _camera)) continue;

                BeginDrag(slot, screenPoint);
                return;
            }
        }

        private void BeginDrag(int slot, Vector2 screenPoint)
        {
            _activeSlot = slot;
            _dragging = true;

            PieceVisual piece = _tray.Piece(slot);

            // Grow to full board scale on pick-up, and reparent above everything so the piece is
            // never clipped by the tray panel while it travels.
            piece.SetCellSize(_boardCellSize, _boardGap);
            piece.Root.SetParent(_dragLayer, true);
            piece.Root.SetAsLastSibling();
            piece.Root.localScale = Vector3.one;

            _grabOffset = Vector2.zero;
            UpdateDrag(screenPoint);
        }

        private void UpdateDrag(Vector2 screenPoint)
        {
            PieceVisual piece = _tray.Piece(_activeSlot);
            if (!piece.HasShape) { CancelDrag(); return; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer, screenPoint, _camera, out Vector2 local)) return;

            Vector2 size = piece.BoundingSize;
            float lift = (_boardCellSize + _boardGap) * LiftCells;

            // The root is pivoted top-left, so centring the piece horizontally on the finger and
            // floating it above means offsetting by half its width and its full height.
            piece.Root.anchoredPosition = local + _grabOffset + new Vector2(-size.x * 0.5f, size.y + lift);

            if (TryResolveCell(piece, out int col, out int row))
            {
                bool valid = CanPlace != null && CanPlace(_activeSlot, col, row);
                _board.ShowGhost(piece.Shape, col, row, valid);
            }
            else
            {
                _board.HideGhost();
            }
        }

        /// <summary>
        /// Which board cell the dragged piece is aligned with.
        ///
        /// The piece root and the grid use the same pivot convention and the same step, so the
        /// piece's top-left corner in grid space divides straight down to a cell index. No
        /// per-shape special casing, and it stays correct while the board is shaking.
        /// </summary>
        private bool TryResolveCell(PieceVisual piece, out int col, out int row)
        {
            Vector3 world = piece.Root.position;
            Vector3 gridLocal = _board.Grid.InverseTransformPoint(world);

            float step = _boardCellSize + _boardGap;
            col = Mathf.RoundToInt(gridLocal.x / step);
            row = Mathf.RoundToInt(-gridLocal.y / step);

            // Allow the anchor itself to sit slightly outside; the legality check is what decides.
            return col > -2 && col < Board.Width + 1 && row > -2 && row < Board.Height + 1;
        }

        private void EndDrag(Vector2 screenPoint)
        {
            PieceVisual piece = _tray.Piece(_activeSlot);
            int slot = _activeSlot;

            _board.HideGhost();
            _dragging = false;
            _activeSlot = -1;

            if (!piece.HasShape) return;

            if (TryResolveCell(piece, out int col, out int row) &&
                CanPlace != null && CanPlace(slot, col, row))
            {
                PlacementRequested?.Invoke(slot, col, row);
            }
            else
            {
                PlacementRejected?.Invoke(slot);
            }
        }

        private void CancelDrag()
        {
            if (_activeSlot >= 0) PlacementRejected?.Invoke(_activeSlot);
            _board.HideGhost();
            _dragging = false;
            _activeSlot = -1;
        }

        // --- harness support ---------------------------------------------------------------

        /// <summary>
        /// Perform a complete drag of a tray piece onto a board cell, through the real pick-up,
        /// move and release path — screen-point conversions, reparenting and all.
        ///
        /// This exists because the screenshot harness previously placed pieces by calling the
        /// controller directly, which meant the entire input path shipped unexercised. Everything
        /// downstream of the pointer is covered here; only the reading of the pointer itself is not.
        /// </summary>
        public void SimulateDragTo(int slot, int col, int row)
        {
            PieceVisual piece = _tray.Piece(slot);
            if (piece == null || !piece.HasShape) return;

            BeginDrag(slot, Vector2.zero);

            // Recomputed after BeginDrag, which rescales the piece to board size and so changes
            // its bounding box — the very thing the screen point is derived from.
            Vector2 screenPoint = ScreenPointForCell(piece, col, row);

            UpdateDrag(screenPoint);
            EndDrag(screenPoint);
        }

        /// <summary>
        /// The pointer position that would leave a piece aligned with a given cell. Inverts exactly
        /// the offsets UpdateDrag applies, so a mismatch here is a genuine bug in that maths.
        /// </summary>
        private Vector2 ScreenPointForCell(PieceVisual piece, int col, int row)
        {
            float step = _boardCellSize + _boardGap;

            Vector3 targetWorld = _board.Grid.TransformPoint(new Vector3(col * step, -row * step, 0f));
            Vector3 rootLocal = _dragLayer.InverseTransformPoint(targetWorld);

            Vector2 size = piece.BoundingSize;
            float lift = step * LiftCells;

            // UpdateDrag applies (-size.x/2, +size.y + lift) to the pointer to get the root
            // position, so both terms invert. Getting the y sign wrong here put every simulated
            // drop two piece-heights above the board and every placement was rejected.
            var local = new Vector2(rootLocal.x + size.x * 0.5f, rootLocal.y - size.y - lift);
            Vector3 world = _dragLayer.TransformPoint(local);

            return RectTransformUtility.WorldToScreenPoint(_camera, world);
        }
    }
}
