using System;

namespace Snapline.Core
{
    /// <summary>What a single placement did. Everything the presentation layer needs to animate.</summary>
    public struct PlaceResult
    {
        public bool Placed;
        public int CellsPlaced;

        /// <summary>Cells the piece itself occupies.</summary>
        public ulong PieceMask;

        /// <summary>Every cell removed by line clears, as it stood before removal.</summary>
        public ulong ClearedMask;

        public int RowsCleared;
        public int ColsCleared;

        /// <summary>Bit r set = row r cleared. Lets the view stagger explosions line by line.</summary>
        public int ClearedRowFlags;
        public int ClearedColFlags;

        public int LinesCleared => RowsCleared + ColsCleared;
    }

    /// <summary>
    /// The 8x8 playfield.
    ///
    /// Occupancy is a single ulong — 64 cells, one bit each — so a placement test is one AND and
    /// line detection is sixteen compares. Colour lives alongside as a per-cell index; the board
    /// never knows what a colour looks like, which is what keeps the renderer swappable for real
    /// sprites later without touching gameplay.
    /// </summary>
    public sealed class Board
    {
        public const int Width = 8;
        public const int Height = 8;
        public const int CellCount = Width * Height;

        private ulong _occupied;
        private readonly byte[] _colour = new byte[CellCount];

        public ulong Occupied => _occupied;
        public int FilledCells => Bits.PopCount(_occupied);
        public int EmptyCells => CellCount - FilledCells;
        public double FillRatio => FilledCells / (double)CellCount;
        public bool IsEmpty => _occupied == 0UL;

        public bool IsOccupied(int col, int row) => (_occupied & (1UL << Bits.Index(col, row))) != 0UL;

        /// <summary>Colour index of a cell. Meaningless where the cell is empty.</summary>
        public byte ColourAt(int col, int row) => _colour[Bits.Index(col, row)];

        public void Clear()
        {
            _occupied = 0UL;
            Array.Clear(_colour, 0, _colour.Length);
        }

        public Board Clone()
        {
            var b = new Board { _occupied = _occupied };
            Array.Copy(_colour, b._colour, CellCount);
            return b;
        }

        /// <summary>Restore from a saved run. The colour array is copied, not aliased.</summary>
        public void Restore(ulong occupied, byte[] colours)
        {
            _occupied = occupied;
            Array.Clear(_colour, 0, _colour.Length);
            if (colours != null)
                Array.Copy(colours, _colour, Math.Min(colours.Length, CellCount));
        }

        public byte[] CopyColours()
        {
            var c = new byte[CellCount];
            Array.Copy(_colour, c, CellCount);
            return c;
        }

        // --- placement -----------------------------------------------------------------

        /// <summary>True if the shape fits with its bounding box anchored at this board cell.</summary>
        public bool CanPlaceAt(ShapeDef shape, int col, int row)
        {
            if (col < 0 || row < 0) return false;
            if (col + shape.Width > Width || row + shape.Height > Height) return false;
            ulong mask = shape.BaseMask << Bits.Index(col, row);
            return (_occupied & mask) == 0UL;
        }

        /// <summary>True if the shape fits somewhere, anywhere, on the board as it stands.</summary>
        public bool CanPlaceAnywhere(ShapeDef shape) => CanPlaceAnywhere(_occupied, shape);

        public static bool CanPlaceAnywhere(ulong occupied, ShapeDef shape)
        {
            ulong[] masks = shape.PlacementMasks;
            for (int i = 0; i < masks.Length; i++)
                if ((occupied & masks[i]) == 0UL) return true;
            return false;
        }

        /// <summary>How many distinct anchors the shape fits at. Feeds the congestion metrics.</summary>
        public static int PlacementCount(ulong occupied, ShapeDef shape)
        {
            ulong[] masks = shape.PlacementMasks;
            int n = 0;
            for (int i = 0; i < masks.Length; i++)
                if ((occupied & masks[i]) == 0UL) n++;
            return n;
        }

        /// <summary>
        /// Place a shape and resolve completed lines. Rows and columns are detected together
        /// against the post-placement board and removed as one step, so a piece that completes a
        /// row and a column at once clears both and the shared cell is not counted twice.
        /// </summary>
        public PlaceResult Place(ShapeDef shape, int col, int row, byte colour)
        {
            var result = new PlaceResult();
            if (!CanPlaceAt(shape, col, row)) return result;

            ulong pieceMask = shape.BaseMask << Bits.Index(col, row);
            _occupied |= pieceMask;

            ulong m = pieceMask;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;
                _colour[idx] = colour;
            }

            result.Placed = true;
            result.CellsPlaced = shape.CellCount;
            result.PieceMask = pieceMask;

            ulong cleared = 0UL;
            for (int r = 0; r < Height; r++)
            {
                ulong rm = Bits.RowMask[r];
                if ((_occupied & rm) == rm)
                {
                    cleared |= rm;
                    result.RowsCleared++;
                    result.ClearedRowFlags |= 1 << r;
                }
            }
            for (int c = 0; c < Width; c++)
            {
                ulong cm = Bits.ColMask[c];
                if ((_occupied & cm) == cm)
                {
                    cleared |= cm;
                    result.ColsCleared++;
                    result.ClearedColFlags |= 1 << c;
                }
            }

            if (cleared != 0UL)
            {
                _occupied &= ~cleared;
                ulong q = cleared;
                while (q != 0UL)
                {
                    int idx = Bits.TrailingZeroCount(q);
                    q &= q - 1;
                    _colour[idx] = 0;
                }
                result.ClearedMask = cleared;
            }

            return result;
        }

        /// <summary>
        /// Board state after placing a shape and resolving clears, without mutating anything and
        /// without allocating. The dealer lookahead and the offline simulator run on this.
        /// </summary>
        public static ulong Simulate(ulong occupied, ulong pieceMask)
        {
            ulong next = occupied | pieceMask;
            ulong cleared = 0UL;
            for (int r = 0; r < Height; r++)
            {
                ulong rm = Bits.RowMask[r];
                if ((next & rm) == rm) cleared |= rm;
            }
            for (int c = 0; c < Width; c++)
            {
                ulong cm = Bits.ColMask[c];
                if ((next & cm) == cm) cleared |= cm;
            }
            return next & ~cleared;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(CellCount + Height);
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++) sb.Append(IsOccupied(c, r) ? '#' : '.');
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
