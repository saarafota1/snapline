namespace Snapline.Core
{
    /// <summary>
    /// Bit helpers for the 8x8 board. Kept hand-rolled rather than using
    /// System.Numerics.BitOperations, which is not on the netstandard2.1 surface Unity compiles
    /// against — it is .NET Core 3.0+ only, so it would build fine in the benchmark harness and
    /// then fail to compile for the player.
    /// </summary>
    public static class Bits
    {
        /// <summary>Full row of the board, indexed by row.</summary>
        public static readonly ulong[] RowMask = BuildRowMasks();

        /// <summary>Full column of the board, indexed by column.</summary>
        public static readonly ulong[] ColMask = BuildColMasks();

        private static ulong[] BuildRowMasks()
        {
            var m = new ulong[Board.Height];
            for (int r = 0; r < Board.Height; r++) m[r] = 0xFFUL << (r * Board.Width);
            return m;
        }

        private static ulong[] BuildColMasks()
        {
            var m = new ulong[Board.Width];
            for (int c = 0; c < Board.Width; c++) m[c] = 0x0101010101010101UL << c;
            return m;
        }

        public static int PopCount(ulong v)
        {
            v -= (v >> 1) & 0x5555555555555555UL;
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }

        /// <summary>Index of the lowest set bit. Undefined for 0 — callers must guard.</summary>
        public static int TrailingZeroCount(ulong v)
        {
            int n = 0;
            if ((v & 0xFFFFFFFFUL) == 0) { n += 32; v >>= 32; }
            if ((v & 0xFFFFUL) == 0) { n += 16; v >>= 16; }
            if ((v & 0xFFUL) == 0) { n += 8; v >>= 8; }
            if ((v & 0xFUL) == 0) { n += 4; v >>= 4; }
            if ((v & 0x3UL) == 0) { n += 2; v >>= 2; }
            if ((v & 0x1UL) == 0) { n += 1; }
            return n;
        }

        public static int Index(int col, int row) => row * Board.Width + col;
        public static int ColOf(int index) => index % Board.Width;
        public static int RowOf(int index) => index / Board.Width;
    }
}
