using System;

namespace Snapline.Core
{
    /// <summary>What a cell holds besides a colour. Numbering is stable: level data refers to it.</summary>
    public enum Special : byte
    {
        None = 0,

        /// <summary>Takes two clears. The first cracks it; it stays on the board and keeps its line full.</summary>
        Stone = 1,

        /// <summary>A stone that has been hit once. The next clear removes it.</summary>
        CrackedStone = 2,

        /// <summary>When it is cleared it explodes, hitting the 3x3 around it. Bombs in the blast go off too.</summary>
        Bomb = 3,

        /// <summary>Clearing it gives extra moves in a level.</summary>
        Gift = 4,
    }

    /// <summary>What a single placement did. Everything the presentation layer needs to animate.</summary>
    public struct PlaceResult
    {
        public bool Placed;
        public int CellsPlaced;

        /// <summary>Cells the piece itself occupies.</summary>
        public ulong PieceMask;

        /// <summary>Every cell actually removed — by line clears and by bombs — as it stood before removal.</summary>
        public ulong ClearedMask;

        public int RowsCleared;
        public int ColsCleared;

        /// <summary>Bit r set = row r cleared. Lets the view stagger explosions line by line.</summary>
        public int ClearedRowFlags;
        public int ClearedColFlags;

        /// <summary>Stones that were hit and cracked, and so stayed on the board.</summary>
        public ulong CrackedMask;

        /// <summary>Bombs that went off.</summary>
        public ulong BombMask;

        /// <summary>Cells removed by a blast that were not in a completed line.</summary>
        public ulong BlastMask;

        /// <summary>Gift blocks removed.</summary>
        public int GiftsCollected;

        public int LinesCleared => RowsCleared + ColsCleared;
    }

    /// <summary>
    /// The 8x8 playfield.
    ///
    /// Occupancy is a single ulong — 64 cells, one bit each — so a placement test is one AND and
    /// line detection is sixteen compares. Colour and special blocks live alongside as per-cell
    /// bytes; the board never knows what a colour looks like, which keeps the renderer swappable.
    ///
    /// Special blocks change what a clear REMOVES, never what counts as a clear. A line full of
    /// blocks is a cleared line whether a stone survives in it or a bomb blows half the board away,
    /// so scoring, combos and level targets all stay exactly as they were.
    /// </summary>
    public sealed class Board
    {
        public const int Width = 8;
        public const int Height = 8;
        public const int CellCount = Width * Height;

        private ulong _occupied;
        private readonly byte[] _colour = new byte[CellCount];
        private readonly byte[] _special = new byte[CellCount];

        /// <summary>For each cell, the 3x3 block of cells centred on it, clipped to the board.</summary>
        private static readonly ulong[] Blast = BuildBlastMasks();

        public ulong Occupied => _occupied;
        public int FilledCells => Bits.PopCount(_occupied);
        public int EmptyCells => CellCount - FilledCells;
        public double FillRatio => FilledCells / (double)CellCount;
        public bool IsEmpty => _occupied == 0UL;

        public bool IsOccupied(int col, int row) => (_occupied & (1UL << Bits.Index(col, row))) != 0UL;

        /// <summary>Colour index of a cell. Meaningless where the cell is empty.</summary>
        public byte ColourAt(int col, int row) => _colour[Bits.Index(col, row)];

        public Special SpecialAt(int col, int row) => (Special)_special[Bits.Index(col, row)];

        /// <summary>Cells holding any special block.</summary>
        public ulong SpecialMask => MaskOf(s => s != 0);

        /// <summary>
        /// Cells that survive being cleared: uncracked stones. Look-ahead code passes this to
        /// <see cref="Simulate(ulong, ulong, ulong)"/> so it does not count on room a stone still holds.
        /// </summary>
        public ulong StickyMask => MaskOf(s => s == (byte)Special.Stone);

        private ulong MaskOf(Func<byte, bool> test)
        {
            ulong mask = 0UL;
            ulong m = _occupied;
            while (m != 0UL)
            {
                int idx = Bits.TrailingZeroCount(m);
                m &= m - 1;
                if (test(_special[idx])) mask |= 1UL << idx;
            }
            return mask;
        }

        public void Clear()
        {
            _occupied = 0UL;
            Array.Clear(_colour, 0, _colour.Length);
            Array.Clear(_special, 0, _special.Length);
        }

        /// <summary>
        /// Empties one cell, for the hammer. A hammer breaks anything outright, stone included, and
        /// sets nothing off — a hammered bomb is defused.
        ///
        /// Deliberately does not check for completed lines. A hammer that could trigger a clear
        /// would let a player buy a combo, and score is meant to measure play rather than spending.
        /// Returns false if the cell was already empty, so the caller can decline to charge for it.
        /// </summary>
        public bool ClearCell(int col, int row)
        {
            if (col < 0 || row < 0 || col >= Width || row >= Height) return false;

            int index = Bits.Index(col, row);
            ulong bit = 1UL << index;
            if ((_occupied & bit) == 0UL) return false;

            _occupied &= ~bit;
            _colour[index] = 0;
            _special[index] = 0;
            return true;
        }

        public Board Clone()
        {
            var b = new Board { _occupied = _occupied };
            Array.Copy(_colour, b._colour, CellCount);
            Array.Copy(_special, b._special, CellCount);
            return b;
        }

        /// <summary>Restore from a saved run or a level's opening. Arrays are copied, not aliased.</summary>
        public void Restore(ulong occupied, byte[] colours, byte[] specials = null)
        {
            _occupied = occupied;
            Array.Clear(_colour, 0, _colour.Length);
            Array.Clear(_special, 0, _special.Length);
            if (colours != null)
                Array.Copy(colours, _colour, Math.Min(colours.Length, CellCount));
            if (specials != null)
                Array.Copy(specials, _special, Math.Min(specials.Length, CellCount));
        }

        public byte[] CopyColours()
        {
            var c = new byte[CellCount];
            Array.Copy(_colour, c, CellCount);
            return c;
        }

        public byte[] CopySpecials()
        {
            var s = new byte[CellCount];
            Array.Copy(_special, s, CellCount);
            return s;
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
        ///
        /// Special blocks in those lines are then resolved: a stone takes at most one hit per move
        /// (a first hit cracks it, a hit on a cracked stone removes it), a removed bomb hits its 3x3
        /// and any bomb removed by that blast goes off in turn, and removed gifts are counted.
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
                _special[idx] = 0;
            }

            result.Placed = true;
            result.CellsPlaced = shape.CellCount;
            result.PieceMask = pieceMask;

            ulong lines = 0UL;
            for (int r = 0; r < Height; r++)
            {
                ulong rm = Bits.RowMask[r];
                if ((_occupied & rm) == rm)
                {
                    lines |= rm;
                    result.RowsCleared++;
                    result.ClearedRowFlags |= 1 << r;
                }
            }
            for (int c = 0; c < Width; c++)
            {
                ulong cm = Bits.ColMask[c];
                if ((_occupied & cm) == cm)
                {
                    lines |= cm;
                    result.ColsCleared++;
                    result.ClearedColFlags |= 1 << c;
                }
            }

            if (lines == 0UL) return result;

            ulong stones = StickyMask;
            ulong bombs = MaskOf(s => s == (byte)Special.Bomb);

            // Everything hit this move, growing as bombs go off.
            ulong hit = lines;
            ulong exploded = 0UL;
            while (true)
            {
                ulong removedSoFar = hit & ~stones;
                ulong fresh = removedSoFar & bombs & ~exploded;
                if (fresh == 0UL) break;

                ulong f = fresh;
                while (f != 0UL)
                {
                    int idx = Bits.TrailingZeroCount(f);
                    f &= f - 1;
                    hit |= Blast[idx] & _occupied;
                }
                exploded |= fresh;
            }

            ulong cracked = hit & stones;
            ulong removed = hit & ~cracked;

            ulong q = removed;
            while (q != 0UL)
            {
                int idx = Bits.TrailingZeroCount(q);
                q &= q - 1;
                if (_special[idx] == (byte)Special.Gift) result.GiftsCollected++;
                _colour[idx] = 0;
                _special[idx] = 0;
            }

            ulong k = cracked;
            while (k != 0UL)
            {
                int idx = Bits.TrailingZeroCount(k);
                k &= k - 1;
                _special[idx] = (byte)Special.CrackedStone;
            }

            _occupied &= ~removed;
            result.ClearedMask = removed;
            result.CrackedMask = cracked;
            result.BombMask = exploded;
            result.BlastMask = removed & ~lines;
            return result;
        }

        /// <summary>
        /// Clear the rows holding the most blocks, and report what was removed.
        ///
        /// Used by the revive. Picking the fullest rows rather than, say, the bottom ones means the
        /// rescue is always worth something.
        /// </summary>
        public ulong ClearFullestRows(int count)
        {
            if (count <= 0 || _occupied == 0UL) return 0UL;

            var order = new int[Height];
            for (int r = 0; r < Height; r++) order[r] = r;

            for (int i = 1; i < Height; i++)
            {
                int key = order[i];
                int keyCount = Bits.PopCount(_occupied & Bits.RowMask[key]);
                int j = i - 1;

                while (j >= 0 && Bits.PopCount(_occupied & Bits.RowMask[order[j]]) < keyCount)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = key;
            }

            ulong cleared = 0UL;
            int taken = Math.Min(count, Height);
            for (int i = 0; i < taken; i++) cleared |= _occupied & Bits.RowMask[order[i]];

            if (cleared == 0UL) return 0UL;

            _occupied &= ~cleared;

            ulong q = cleared;
            while (q != 0UL)
            {
                int idx = Bits.TrailingZeroCount(q);
                q &= q - 1;
                _colour[idx] = 0;
                _special[idx] = 0;
            }

            return cleared;
        }

        /// <summary>
        /// Board state after placing a shape and resolving clears, without mutating anything and
        /// without allocating. The dealer lookahead and the offline simulator run on this.
        /// </summary>
        public static ulong Simulate(ulong occupied, ulong pieceMask) => Simulate(occupied, pieceMask, 0UL);

        /// <summary>
        /// As above, with <paramref name="sticky"/> cells — uncracked stones — surviving the clear.
        /// Bombs are ignored, which only ever underestimates the room a move frees, so a look-ahead
        /// built on this stays on the safe side.
        /// </summary>
        public static ulong Simulate(ulong occupied, ulong pieceMask, ulong sticky)
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
            return next & ~(cleared & ~sticky);
        }

        private static ulong[] BuildBlastMasks()
        {
            var masks = new ulong[CellCount];
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
            {
                ulong m = 0UL;
                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = row + dr, c = col + dc;
                    if (r < 0 || c < 0 || r >= Height || c >= Width) continue;
                    m |= 1UL << Bits.Index(c, r);
                }
                masks[Bits.Index(col, row)] = m;
            }
            return masks;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(CellCount + Height);
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    int i = Bits.Index(c, r);
                    char ch = '.';
                    if ((_occupied & (1UL << i)) != 0UL)
                    {
                        ch = (Special)_special[i] switch
                        {
                            Special.Stone => 'S',
                            Special.CrackedStone => 's',
                            Special.Bomb => 'B',
                            Special.Gift => 'G',
                            _ => '#',
                        };
                    }
                    sb.Append(ch);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
