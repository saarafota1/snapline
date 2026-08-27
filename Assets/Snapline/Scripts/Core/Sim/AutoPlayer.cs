using System;

namespace Snapline.Core.Sim
{
    /// <summary>
    /// How well the simulated player plays. Dealing that feels fair to a careful player can still
    /// be brutal to a careless one, so every measurement is run at more than one skill level.
    /// </summary>
    public enum PlayerSkill
    {
        /// <summary>Uniformly random legal move. The floor.</summary>
        Random = 0,

        /// <summary>Takes line clears when offered, otherwise random. A distracted player.</summary>
        Greedy = 1,

        /// <summary>Full heuristic evaluation of every legal move. A player who is paying attention.</summary>
        Heuristic = 2,
    }

    /// <summary>
    /// Weights for the survival heuristic. Exposed because "what does a good player optimise for"
    /// is itself a hypothesis, and the whole point of the harness is that hypotheses get tested.
    /// </summary>
    public sealed class HeuristicWeights
    {
        public double LinesCleared = 100.0;
        public double FilledCells = -2.0;

        /// <summary>Empty cells walled in on all four sides. These are what actually kill a run.</summary>
        public double EnclosedCells = -8.0;

        /// <summary>Room left for a 2x2, the piece that most often has nowhere to go.</summary>
        public double Square2Room = 1.5;

        /// <summary>Room left for 3-long bars in each orientation.</summary>
        public double Bar3Room = 0.75;

        public static HeuristicWeights Default() => new HeuristicWeights();
    }

    /// <summary>
    /// Chooses moves for an unattended run. Used by the simulator, and also drives the attract-mode
    /// demo on the title screen.
    /// </summary>
    public sealed class AutoPlayer
    {
        private readonly PlayerSkill _skill;
        private readonly HeuristicWeights _w;

        private static readonly ShapeDef Square2 = Shapes.Get(9);
        private static readonly ShapeDef Bar3H = Shapes.Get(3);
        private static readonly ShapeDef Bar3V = Shapes.Get(4);

        public AutoPlayer(PlayerSkill skill = PlayerSkill.Heuristic, HeuristicWeights weights = null)
        {
            _skill = skill;
            _w = weights ?? HeuristicWeights.Default();
        }

        /// <summary>
        /// Best move available, or Accepted=false if nothing in the tray fits.
        /// Does not mutate the run; the caller applies the move.
        /// </summary>
        public bool ChooseMove(GameRun run, ref Rng rng, out int traySlot, out int col, out int row)
        {
            traySlot = -1;
            col = 0;
            row = 0;

            ulong occupied = run.Board.Occupied;
            double bestScore = double.NegativeInfinity;
            int candidateCount = 0;

            for (int slot = 0; slot < run.Tray.Length; slot++)
            {
                TrayPiece piece = run.Tray[slot];
                if (piece.IsEmpty) continue;

                ShapeDef shape = piece.Shape;
                ulong[] masks = shape.PlacementMasks;
                int[] anchors = shape.PlacementAnchors;

                for (int m = 0; m < masks.Length; m++)
                {
                    ulong pm = masks[m];
                    if ((occupied & pm) != 0UL) continue;

                    candidateCount++;
                    double score = Evaluate(occupied, pm, ref rng);

                    // Reservoir-style tie-breaking, so a run of equal-scoring moves does not always
                    // resolve to the lowest board index and bias the measurement.
                    bool take = score > bestScore;
                    if (!take && score == bestScore && rng.NextInt(candidateCount) == 0) take = true;

                    if (take)
                    {
                        bestScore = score;
                        traySlot = slot;
                        int anchor = anchors[m];
                        col = Bits.ColOf(anchor);
                        row = Bits.RowOf(anchor);
                    }
                }
            }

            return traySlot >= 0;
        }

        private double Evaluate(ulong occupied, ulong pieceMask, ref Rng rng)
        {
            if (_skill == PlayerSkill.Random) return rng.NextDouble();

            ulong merged = occupied | pieceMask;
            int lines = CountCompletedLines(merged);

            if (_skill == PlayerSkill.Greedy)
                return lines * 100.0 + rng.NextDouble();

            ulong after = Board.Simulate(occupied, pieceMask);

            double score = 0.0;
            score += _w.LinesCleared * lines;
            score += _w.FilledCells * Bits.PopCount(after);
            score += _w.EnclosedCells * CountEnclosedEmptyCells(after);
            score += _w.Square2Room * Board.PlacementCount(after, Square2);
            score += _w.Bar3Room * (Board.PlacementCount(after, Bar3H) + Board.PlacementCount(after, Bar3V));
            return score;
        }

        private static int CountCompletedLines(ulong merged)
        {
            int lines = 0;
            for (int r = 0; r < Board.Height; r++)
                if ((merged & Bits.RowMask[r]) == Bits.RowMask[r]) lines++;
            for (int c = 0; c < Board.Width; c++)
                if ((merged & Bits.ColMask[c]) == Bits.ColMask[c]) lines++;
            return lines;
        }

        /// <summary>
        /// Empty cells with occupied cells (or a board edge) on all four sides. Branchless, using
        /// the fact that "the neighbour above cell i" is simply bit i-8 — with the outer rows and
        /// columns OR-ed in as walls so edge cells are not miscounted as open.
        /// </summary>
        public static int CountEnclosedEmptyCells(ulong occupied)
        {
            ulong colA = Bits.ColMask[0];
            ulong colH = Bits.ColMask[Board.Width - 1];
            ulong rowTop = Bits.RowMask[0];
            ulong rowBottom = Bits.RowMask[Board.Height - 1];

            ulong leftBlocked = ((occupied << 1) & ~colA) | colA;
            ulong rightBlocked = ((occupied >> 1) & ~colH) | colH;
            ulong upBlocked = (occupied << Board.Width) | rowTop;
            ulong downBlocked = (occupied >> Board.Width) | rowBottom;

            ulong enclosed = ~occupied & leftBlocked & rightBlocked & upBlocked & downBlocked;
            return Bits.PopCount(enclosed);
        }
    }
}
