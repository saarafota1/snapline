using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;

namespace Snapline.View
{
    /// <summary>
    /// The three offered pieces along the bottom of the screen.
    ///
    /// Tray pieces are drawn smaller than board cells so that even a 3x3 block fits comfortably in
    /// a third of the screen width; they scale up to full board size the moment they are lifted,
    /// which is what makes the drag read as picking something up.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform[] _slots;
        private PieceVisual[] _pieces;
        private Image[] _slotPanels;

        private float _trayCellSize;
        private float _trayGap;

        public int SlotCount => _slots?.Length ?? 0;
        public float TrayCellSize => _trayCellSize;
        public float TrayGap => _trayGap;

        public void Init(RectTransform root, int slots, float slotWidth, float trayCellSize, float trayGap)
        {
            _root = root;
            _trayCellSize = trayCellSize;
            _trayGap = trayGap;

            WarnIfShapesDoNotFit(root, slotWidth, trayCellSize, trayGap);

            _slots = new RectTransform[slots];
            _pieces = new PieceVisual[slots];
            _slotPanels = new Image[slots];

            float totalWidth = slotWidth * slots;
            float startX = -totalWidth * 0.5f + slotWidth * 0.5f;

            for (int i = 0; i < slots; i++)
            {
                RectTransform slot = UIKit.Rect($"Slot{i}", _root);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = new Vector2(startX + i * slotWidth, 0f);
                slot.sizeDelta = new Vector2(slotWidth - 12f, _root.sizeDelta.y);
                _slots[i] = slot;

                Image panel = UIKit.Image($"SlotPanel{i}", slot, ArtKit.SoftPanel(), Color.white, Image.Type.Sliced);
                RectTransform prt = panel.rectTransform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                _slotPanels[i] = panel;

                _pieces[i] = new PieceVisual(slot, $"Piece{i}");
            }
        }

        /// <summary>
        /// Complain loudly if any shape in the catalogue is too big for a tray slot.
        ///
        /// A piece that does not fit still draws — it just hangs off the bottom of the screen, which
        /// is easy to miss and was in fact shipped once before a screenshot caught it. Adding a
        /// taller shape to the catalogue should fail noisily, not quietly.
        /// </summary>
        private static void WarnIfShapesDoNotFit(RectTransform root, float slotWidth,
                                                 float trayCellSize, float trayGap)
        {
            float step = trayCellSize + trayGap;
            float availableHeight = root.sizeDelta.y;
            float availableWidth = slotWidth - 12f;

            foreach (ShapeDef shape in Shapes.All)
            {
                float h = shape.Height * step - trayGap;
                float w = shape.Width * step - trayGap;

                if (h > availableHeight)
                    Debug.LogError($"[Snapline] Shape '{shape.Name}' is {h:F0} tall but the tray is " +
                                   $"{availableHeight:F0}. Reduce TrayCellSize or raise TrayHeight.");

                if (w > availableWidth)
                    Debug.LogError($"[Snapline] Shape '{shape.Name}' is {w:F0} wide but a slot is " +
                                   $"{availableWidth:F0}. Reduce TrayCellSize or widen the slots.");
            }
        }

        public PieceVisual Piece(int slot) => _pieces[slot];
        public RectTransform Slot(int slot) => _slots[slot];

        /// <summary>Redraw all three slots from the engine's tray.</summary>
        public void Refresh(TrayPiece[] tray, bool animate)
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (i >= tray.Length || tray[i].IsEmpty)
                {
                    _pieces[i].Clear();
                    continue;
                }

                _pieces[i].Set(tray[i].Shape, tray[i].Colour, _trayCellSize, _trayGap);
                CentreInSlot(i);

                if (animate) StartCoroutine(DealIn(_pieces[i].Root, i * 0.07f));
            }
        }

        /// <summary>Position a piece so its bounding box sits in the middle of its slot.</summary>
        public void CentreInSlot(int slot)
        {
            PieceVisual piece = _pieces[slot];
            if (!piece.HasShape) return;

            piece.Root.SetParent(_slots[slot], false);
            piece.Root.anchorMin = piece.Root.anchorMax = new Vector2(0.5f, 0.5f);
            piece.Root.pivot = new Vector2(0f, 1f);

            Vector2 size = piece.BoundingSize;
            piece.Root.anchoredPosition = new Vector2(-size.x * 0.5f, size.y * 0.5f);
            piece.Root.localScale = Vector3.one;
            piece.SetAlpha(1f);
        }

        /// <summary>Grey a slot out while its piece has nowhere to go.</summary>
        public void SetSlotPlayable(int slot, bool playable)
        {
            if (_pieces[slot] == null || !_pieces[slot].HasShape) return;
            _pieces[slot].SetAlpha(playable ? 1f : 0.32f);
        }

        private IEnumerator DealIn(RectTransform rt, float delay)
        {
            if (rt == null) yield break;

            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(0f, -260f);
            rt.localScale = Vector3.one * 0.6f;

            if (delay > 0f) yield return new WaitForSeconds(delay);

            const float duration = 0.26f;
            float t = 0f;
            Vector2 from = rt.anchoredPosition;

            while (t < duration)
            {
                t += Time.deltaTime;
                if (rt == null) yield break;

                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                rt.anchoredPosition = Vector2.Lerp(from, home, eased);
                rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, eased);
                yield return null;
            }

            if (rt == null) yield break;
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one;
        }

        /// <summary>Bounce a piece back to its slot after an illegal drop.</summary>
        public IEnumerator ReturnToSlot(int slot, float duration = 0.18f)
        {
            PieceVisual piece = _pieces[slot];
            if (!piece.HasShape) yield break;

            RectTransform rt = piece.Root;
            Vector3 fromWorld = rt.position;
            Vector3 fromScale = rt.localScale;

            CentreInSlot(slot);
            Vector3 toWorld = rt.position;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                rt.position = Vector3.Lerp(fromWorld, toWorld, eased);
                rt.localScale = Vector3.Lerp(fromScale, Vector3.one, eased);
                yield return null;
            }

            CentreInSlot(slot);
        }
    }
}
