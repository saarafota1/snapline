namespace Snapline.Core
{
    /// <summary>
    /// Deterministic xorshift64* generator.
    ///
    /// Deliberately not System.Random: the whole point of the dealing instrumentation is that a run
    /// can be replayed exactly from a seed, and that a saved run resumes with the identical piece
    /// sequence it would have had. System.Random's algorithm is not contractually stable across
    /// runtimes, so a saved game could deal differently after an engine upgrade.
    /// </summary>
    public struct Rng
    {
        private ulong _state;

        public Rng(ulong seed)
        {
            // 0 is a fixed point of xorshift, so remap it.
            _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>Raw state, for save/restore. Round-trips exactly.</summary>
        public ulong State
        {
            get => _state;
            set => _state = value == 0UL ? 0x9E3779B97F4A7C15UL : value;
        }

        public ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform in [0, bound). Rejection-sampled, so no modulo bias.</summary>
        public int NextInt(int bound)
        {
            if (bound <= 1) return 0;
            ulong ubound = (ulong)bound;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % ubound);
            ulong r;
            do { r = NextULong(); } while (r >= limit);
            return (int)(r % ubound);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
    }
}
