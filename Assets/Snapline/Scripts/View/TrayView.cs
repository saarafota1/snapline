using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using Snapline.Core;
using Snapline.UI;
using GameKit.Art;

namespace Snapline.View
{
    /// <summary>
    /// The three offered pieces, each in its own navy slot.
    ///
    /// Each piece is sized to fit its own slot rather than all sharing one cell size: a single
    /// 2x2 block drawn at the size that lets a 5-long bar fit looks lost in its panel, which is
    /// how the tray looked before. Pieces grow to full board size the moment they are lifted.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform[] _slots;
        private PieceVisual[] _pieces;
        private Image[] _panels;

        private Vector2 _slotSize = new Vector2(320f, 290f);
        private float _gap = 5f;
        private float _maxCell = 66f;

        public int SlotCount => _slots?.Length ?? 0;

        public void Init(RectTransform root, int slots, float gap)
        {
            _root = root;
            _gap = gap;

            _slots = new RectTransform[slots];
            _pieces = new PieceVisual[slots];
            _panels = new Image[slots];

            for (int i = 0; i < slots; i++)
            {
                RectTransform slot = UIKit.Rect($"Slot{i}", _root);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                _slots[i] = slot;

                Image panel = W.Rounded($"SlotPanel{i}", slot, CandyText.Hex(0x1E2F7E), CandyText.Hex(0x4C74EA),
                                        W.Centre, Vector2.zero, _slotSize, 34f);
                RectTransform prt = panel.rectTransform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                _panels[i] = panel;

                _pieces[i] = new PieceVisual(slot, $"Piece{i}");
            }
        }

        /// <summary>Sizes and spaces the slots. The two game modes use different tray proportions.</summary>
        public void Layout(Vector2 slotSize, float spacing, float maxCell)
        {
            _slotSize = slotSize;
            _maxCell = maxCell;

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].sizeDelta = slotSize;
                _slots[i].anchoredPosition = new Vector2((i - (_slots.Length - 1) * 0.5f) * spacing, 0f);
                W.FitSlices(_panels[i], slotSize);
                _panels[i].pixelsPerUnitMultiplier = Mathf.Max(0.05f, 50f / 34f);
                if (_pieces[i].HasShape) CentreInSlot(i);
            }
        }

        public PieceVisual Piece(int slot) => _pieces[slot];
        public RectTransform Slot(int slot) => _slots[slot];

        /// <summary>The largest cell that lets this shape sit inside a slot with room around it.</summary>
        public float CellFor(ShapeDef shape)
        {
            if (shape == null) return _maxCell;
            float byWidth = (_slotSize.x - 56f - (shape.Width - 1) * _gap) / shape.Width;
            float byHeight = (_slotSize.y - 56f - (shape.Height - 1) * _gap) / shape.Height;
            return Mathf.Floor(Mathf.Clamp(Mathf.Min(byWidth, byHeight), 20f, _maxCell));
        }

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

                _pieces[i].Set(tray[i].Shape, tray[i].Colour, CellFor(tray[i].Shape), _gap);
                CentreInSlot(i);

                if (animate) StartCoroutine(DealIn(i, i * 0.08f));
            }
        }

        /// <summary>Put a piece back at tray size, centred in its slot.</summary>
        public void CentreInSlot(int slot)
        {
            PieceVisual piece = _pieces[slot];
            if (!piece.HasShape) return;

            piece.SetCellSize(CellFor(piece.Shape), _gap);
            piece.Root.SetParent(_slots[slot], false);
            piece.Root.anchorMin = piece.Root.anchorMax = new Vector2(0.5f, 0.5f);
            piece.Root.pivot = new Vector2(0f, 1f);

            Vector2 size = piece.BoundingSize;
            piece.Root.anchoredPosition = new Vector2(-size.x * 0.5f, size.y * 0.5f);
            piece.Root.localScale = Vector3.one;
            piece.Root.localRotation = Quaternion.identity;
            piece.SetAlpha(1f);
        }

        /// <summary>Grey a slot out while its piece has nowhere to go.</summary>
        public void SetSlotPlayable(int slot, bool playable)
        {
            if (_pieces[slot] == null || !_pieces[slot].HasShape) return;
            _pieces[slot].SetAlpha(playable ? 1f : 0.35f);
            _panels[slot].color = playable ? Color.white : new Color(0.75f, 0.7f, 0.8f, 1f);
        }

        private IEnumerator DealIn(int slot, float delay)
        {
            RectTransform rt = _pieces[slot].Root;
            if (rt == null) yield break;

            Vector2 home = rt.anchoredPosition;
            Vector2 from = home + new Vector2(0f, -200f);
            rt.anchoredPosition = from;
            rt.localScale = Vector3.zero;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            const float duration = 0.42f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (rt == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, home, Ease.OutBack(k, 1.3f));
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, Ease.OutBack(k, 2.2f));
                yield return null;
            }

            if (rt == null) yield break;
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one;
            if (Fx.Instance != null) Fx.Instance.Sparkles(_slots[slot].position, 3, 90f, 46f);
        }

        /// <summary>
        /// The shuffle: the three pieces spin away into their slots, then <paramref name="dealt"/>
        /// runs so the new ones can be dealt in.
        /// </summary>
        public IEnumerator SpinOut(Action dealt)
        {
            const float duration = 0.28f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                for (int i = 0; i < _pieces.Length; i++)
                {
                    if (!_pieces[i].HasShape) continue;
                    _pieces[i].Root.localScale = Vector3.one * (1f - Ease.InCubic(k));
                    _pieces[i].Root.localRotation = Quaternion.Euler(0f, 0f, 540f * k);
                }
                yield return null;
            }

            for (int i = 0; i < _slots.Length; i++)
                if (Fx.Instance != null) Fx.Instance.Sparkles(_slots[i].position, 5, 110f, 60f);

            dealt?.Invoke();
        }

        /// <summary>Bounce a piece back to its slot after an illegal drop, with a little shudder.</summary>
        public IEnumerator ReturnToSlot(int slot, float duration = 0.22f)
        {
            PieceVisual piece = _pieces[slot];
            if (!piece.HasShape) yield break;

            RectTransform rt = piece.Root;
            Vector3 fromWorld = rt.position;

            CentreInSlot(slot);
            Vector3 toWorld = rt.position;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.position = Vector3.LerpUnclamped(fromWorld, toWorld, Ease.OutBack(k, 1.2f));
                yield return null;
            }

            CentreInSlot(slot);
            Tween.Shake(_slots[slot], 12f, 0.3f);
        }
    }
}
