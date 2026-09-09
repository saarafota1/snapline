using System;
using System.Collections.Generic;

namespace Snapline.Core
{
    /// <summary>How hard the dealer works to keep a tray playable.</summary>
    public enum TrayGuarantee
    {
        /// <summary>Pure weighted random. Here to be the control group in measurements, not to ship.</summary>
        None = 0,

        /// <summary>At least one of the three pieces fits the board as dealt.</summary>
        AtLeastOneFits = 1,

        /// <summary>
        /// Some order and placement of all three pieces exists. The player cannot be killed by this
        /// tray, only by their own choices, which is the difference between "hard" and "unfair".
        /// </summary>
        WholeTraySurvivable = 2,
    }

    /// <summary>One offered piece.</summary>
    public struct TrayPiece
    {
        public int ShapeId;
        public byte Colour;
        public bool Consumed;

        public ShapeDef Shape => Shapes.Get(ShapeId);
        public static TrayPiece Empty => new TrayPiece { ShapeId = -1, Consumed = true };
        public bool IsEmpty => ShapeId < 0 || Consumed;
    }

    /// <summary>
    /// Every knob on the dealer.
    ///
    /// These values are measured, not guessed. `dotnet run -- sweep` in Tools/Bench produced them;
    /// the headline results, at 400 runs per cell against the heuristic autoplayer, were:
    ///
    ///   policy                     median pieces   runs under 20 pieces
    ///   pure random                      44               4.50 %
    ///   congestion weighting only       137               1.75 %
    ///   at-least-one-fits               137               1.75 %   (identical: buys nothing)
    ///   whole-tray survivable           173               1.50 %
    ///
    /// Two findings drove the defaults below, and both contradicted the obvious guess:
    ///
    ///   1. PlacementAwareness dominates CongestionBias. With awareness at 0 the median run is 83
    ///      pieces no matter how hard congestion is pushed (bias 0 through 5). Weighting a shape by
    ///      how much room it currently has matters far more than weighting it by size.
    ///   2. AtLeastOneFits is a no-op once weighting is on — its numbers are identical to no
    ///      guarantee at all. The whole-tray search is what actually buys the improvement, and it
    ///      costs 3.7 nodes and 26 us per deal, which is nothing.
    ///
    /// Caveat worth keeping in view: these are autoplayer numbers. A model of a good player is not
    /// a player. The Greedy autoplayer sits at a median of 29 pieces under every config tested,
    /// which says the dealer cannot rescue careless play — only avoid punishing careful play.
    /// Final tuning wants real session-length data.
    /// </summary>
    public sealed class DealerConfig
    {
        public TrayGuarantee Guarantee = TrayGuarantee.WholeTraySurvivable;

        /// <summary>Per-shape base weights before congestion adjustment. Null means uniform.</summary>
        public double[] BaseWeight;

        /// <summary>
        /// How hard to favour small shapes as the board fills. 0 disables congestion weighting
        /// entirely; higher values push harder toward dots and 2-cell pieces on a packed board.
        /// </summary>
        public double CongestionBias = 2.2;

        /// <summary>Fill ratio below which congestion weighting does nothing at all.</summary>
        public double CongestionOnset = 0.30;

        /// <summary>
        /// Exponent on (fitting anchors / total anchors). 0 ignores fit; 1 weights a shape in
        /// direct proportion to how much room it currently has.
        ///
        /// 1.2 chosen from the sweep: it gives a median run of 293 pieces and drops runs ending
        /// under 20 pieces to 0.75%. Raising it to the 5.0/1.2 corner reached a median of 488 and
        /// 0% short runs, which was rejected as too generous — an endless game still needs to end.
        /// </summary>
        public double PlacementAwareness = 1.2;

        /// <summary>Relative weight of a shape that fits nowhere. Deliberately small, not zero.</summary>
        public double NonFittingWeight = 0.05;

        /// <summary>Resamples allowed before falling back to repairing the tray in place.</summary>
        public int MaxTrayAttempts = 24;

        /// <summary>Distinct colour indices the dealer may assign. The renderer maps these to gradients.</summary>
        /// <summary>
        /// How many distinct block colours exist. Six, matching the authored candy artwork — the
        /// seventh square on the art sheet is the empty cell, not a playable colour.
        ///
        /// Colour is decorative throughout: the board stores one per cell and the save round-trips
        /// it, but no rule ever reads it, so this number is free to follow the art.
        /// </summary>
        public int PaletteSize = 6;

        public int TraySize = 3;

        public DealerConfig Clone() => (DealerConfig)MemberwiseClone();

        public static DealerConfig Default() => new DealerConfig();

        /// <summary>Pure random, no fairness at all. The baseline every measurement compares against.</summary>
        public static DealerConfig PureRandom() => new DealerConfig
        {
            Guarantee = TrayGuarantee.None,
            CongestionBias = 0.0,
            PlacementAwareness = 0.0,
            NonFittingWeight = 1.0,
        };
    }

    /// <summary>
    /// Counters the dealer keeps about its own behaviour. This is the instrumentation the playbook
    /// insists on building before tuning; without it, dealing changes are guesswork.
    /// </summary>
    public sealed class DealerStats
    {
        public int TraysDealt;
        public int TotalSampleAttempts;
        public int TraysRepaired;
        public int TraysGuaranteeUnmet;
        public int SurvivabilitySearches;
        public long SurvivabilityNodes;
        public int DeadTrays;

        public double MeanAttemptsPerTray => TraysDealt == 0 ? 0 : TotalSampleAttempts / (double)TraysDealt;
        public double RepairRate => TraysDealt == 0 ? 0 : TraysRepaired / (double)TraysDealt;
        public double MeanSurvivabilityNodes => SurvivabilitySearches == 0 ? 0 : SurvivabilityNodes / (double)SurvivabilitySearches;

        public void Reset()
        {
            TraysDealt = 0;
            TotalSampleAttempts = 0;
            TraysRepaired = 0;
            TraysGuaranteeUnmet = 0;
            SurvivabilitySearches = 0;
            SurvivabilityNodes = 0;
            DeadTrays = 0;
        }
    }

    /// <summary>
    /// Fairness-aware piece dealer.
    ///
    /// Two mechanisms, deliberately separable so each can be measured on its own:
    ///   1. Congestion weighting biases the sampling distribution toward small shapes as the board
    ///      fills. A soft, always-on nudge the player never consciously notices.
    ///   2. The tray guarantee is a hard filter applied after sampling: resample until the tray is
    ///      playable, and repair it in place if resampling runs out.
    ///
    /// Turning both off (DealerConfig.PureRandom) gives the control group.
    /// </summary>
    public sealed class Dealer
    {
        private readonly DealerConfig _config;
        private readonly double[] _weights;
        private readonly int[] _scratchIds;
        private readonly HashSet<ulong> _seen = new HashSet<ulong>();
        private readonly List<int> _fitting = new List<int>();

        public DealerStats Stats { get; } = new DealerStats();
        public DealerConfig Config => _config;

        public Dealer(DealerConfig config)
        {
            _config = config ?? DealerConfig.Default();
            _weights = new double[Shapes.Count];
            _scratchIds = new int[Math.Max(1, _config.TraySize)];
        }

        /// <summary>
        /// Fill <paramref name="tray"/> with a fresh set of pieces for the given board.
        /// Returns false only when the board admits no shape at all, which is a genuine dead end.
        /// </summary>
        public bool Deal(ulong occupied, TrayPiece[] tray, ref Rng rng)
        {
            int size = Math.Min(_config.TraySize, tray.Length);
            BuildWeights(occupied);

            bool satisfied = false;
            int attempts = 0;
            int maxAttempts = Math.Max(1, _config.MaxTrayAttempts);

            for (; attempts < maxAttempts;)
            {
                for (int i = 0; i < size; i++) _scratchIds[i] = SampleShapeId(ref rng);
                attempts++;
                if (SatisfiesGuarantee(occupied, _scratchIds, size))
                {
                    satisfied = true;
                    break;
                }
            }

            Stats.TraysDealt++;
            Stats.TotalSampleAttempts += attempts;

            if (!satisfied)
            {
                Stats.TraysGuaranteeUnmet++;
                if (!RepairTray(occupied, _scratchIds, size, ref rng))
                {
                    Stats.DeadTrays++;
                    // Deal it anyway. The run is over either way, and the player should see the
                    // pieces that could not be placed rather than an empty tray.
                    WriteTray(tray, size, ref rng);
                    return false;
                }
                Stats.TraysRepaired++;
            }

            WriteTray(tray, size, ref rng);
            return true;
        }

        private void WriteTray(TrayPiece[] tray, int size, ref Rng rng)
        {
            int palette = Math.Max(1, _config.PaletteSize);
            for (int i = 0; i < size; i++)
            {
                tray[i] = new TrayPiece
                {
                    ShapeId = _scratchIds[i],
                    Colour = (byte)rng.NextInt(palette),
                    Consumed = false,
                };
            }
            for (int i = size; i < tray.Length; i++) tray[i] = TrayPiece.Empty;
        }

        // --- weighting -----------------------------------------------------------------

        /// <summary>
        /// Recompute sampling weights for the current board. Called once per tray rather than once
        /// per piece: the board does not change while a tray is being sampled, and PlacementCount
        /// across the whole catalogue is the single most expensive thing the dealer does.
        /// </summary>
        private void BuildWeights(ulong occupied)
        {
            double fill = Bits.PopCount(occupied) / (double)Board.CellCount;
            double onset = _config.CongestionOnset;
            double congestion = onset >= 1.0 ? 0.0 : (fill - onset) / (1.0 - onset);
            if (congestion < 0.0) congestion = 0.0;
            if (congestion > 1.0) congestion = 1.0;

            double sizeSpan = Math.Max(1, Shapes.MaxCellCount - 1);

            for (int i = 0; i < Shapes.Count; i++)
            {
                ShapeDef shape = Shapes.All[i];
                double w = _config.BaseWeight != null && i < _config.BaseWeight.Length
                    ? _config.BaseWeight[i]
                    : 1.0;

                if (w <= 0.0) { _weights[i] = 0.0; continue; }

                if (_config.CongestionBias > 0.0 && congestion > 0.0)
                {
                    double sizeNorm = (shape.CellCount - 1) / sizeSpan;
                    w *= Math.Exp(-_config.CongestionBias * congestion * sizeNorm);
                }

                int fits = Board.PlacementCount(occupied, shape);
                if (fits == 0)
                {
                    w *= _config.NonFittingWeight;
                }
                else if (_config.PlacementAwareness > 0.0)
                {
                    double room = fits / (double)shape.PlacementMasks.Length;
                    w *= Math.Pow(room, _config.PlacementAwareness);
                }

                _weights[i] = w;
            }
        }

        private int SampleShapeId(ref Rng rng)
        {
            double total = 0.0;
            for (int i = 0; i < _weights.Length; i++) total += _weights[i];

            // Every shape zeroed out (reachable with a hostile BaseWeight array). Fall back to
            // uniform rather than returning shape 0 forever.
            if (total <= 0.0) return rng.NextInt(Shapes.Count);

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < _weights.Length; i++)
            {
                acc += _weights[i];
                if (roll < acc) return i;
            }
            return _weights.Length - 1;
        }

        // --- guarantees ----------------------------------------------------------------

        private bool SatisfiesGuarantee(ulong occupied, int[] ids, int size)
        {
            switch (_config.Guarantee)
            {
                case TrayGuarantee.None:
                    return true;

                case TrayGuarantee.AtLeastOneFits:
                    for (int i = 0; i < size; i++)
                        if (Board.CanPlaceAnywhere(occupied, Shapes.Get(ids[i]))) return true;
                    return false;

                case TrayGuarantee.WholeTraySurvivable:
                    return IsTraySurvivable(occupied, ids, size);

                default:
                    return true;
            }
        }

        /// <summary>
        /// Is there an order and a set of placements that gets all <paramref name="size"/> pieces
        /// down? Depth-first, with dedupe on the resulting occupancy.
        ///
        /// The cost profile is the opposite of what it looks like. On an open board the first
        /// branch succeeds and it returns almost immediately; on a near-dead board there are barely
        /// any legal placements to branch over. The expensive middle is rare, and
        /// DealerStats.MeanSurvivabilityNodes exists to prove that rather than assume it.
        /// </summary>
        public bool IsTraySurvivable(ulong occupied, int[] ids, int size)
        {
            Stats.SurvivabilitySearches++;
            _seen.Clear();
            return Search(occupied, ids, size, 0);
        }

        private bool Search(ulong occupied, int[] ids, int size, int usedMask)
        {
            if (usedMask == (1 << size) - 1) return true;

            for (int i = 0; i < size; i++)
            {
                int bit = 1 << i;
                if ((usedMask & bit) != 0) continue;

                ShapeDef shape = Shapes.Get(ids[i]);
                ulong[] masks = shape.PlacementMasks;

                for (int m = 0; m < masks.Length; m++)
                {
                    ulong pm = masks[m];
                    if ((occupied & pm) != 0UL) continue;

                    Stats.SurvivabilityNodes++;
                    ulong next = Board.Simulate(occupied, pm);

                    // Two different placements can leave the board in the same state, especially
                    // after a clear. Key on the state together with which pieces remain, or a
                    // state reached with different pieces left would be wrongly pruned.
                    ulong key = next ^ ((ulong)(usedMask | bit) * 0x9E3779B97F4A7C15UL);
                    if (!_seen.Add(key)) continue;

                    if (Search(next, ids, size, usedMask | bit)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Last resort when resampling could not produce a legal tray: overwrite slots, starting
        /// from the last, with shapes that do fit. Returns false only if nothing in the catalogue
        /// fits, which means the board is genuinely dead.
        /// </summary>
        private bool RepairTray(ulong occupied, int[] ids, int size, ref Rng rng)
        {
            _fitting.Clear();
            for (int i = 0; i < Shapes.Count; i++)
                if (Board.CanPlaceAnywhere(occupied, Shapes.All[i])) _fitting.Add(i);

            if (_fitting.Count == 0) return false;

            for (int slot = size - 1; slot >= 0; slot--)
            {
                ids[slot] = PickWeighted(_fitting, ref rng);
                if (SatisfiesGuarantee(occupied, ids, size)) return true;
            }

            // Every slot is now a shape that fits and the guarantee still fails. That can only be
            // WholeTraySurvivable on a board too tight for three pieces. Downgrade to "the player
            // can place something" rather than hand them a tray that is dead on arrival.
            for (int i = 0; i < size; i++)
                if (Board.CanPlaceAnywhere(occupied, Shapes.Get(ids[i]))) return true;

            return false;
        }

        private int PickWeighted(List<int> candidates, ref Rng rng)
        {
            double total = 0.0;
            for (int i = 0; i < candidates.Count; i++) total += _weights[candidates[i]];
            if (total <= 0.0) return candidates[rng.NextInt(candidates.Count)];

            double roll = rng.NextDouble() * total;
            double acc = 0.0;
            for (int i = 0; i < candidates.Count; i++)
            {
                acc += _weights[candidates[i]];
                if (roll < acc) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }
}
