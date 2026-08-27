using System;
using System.Collections.Generic;
using System.Text;

namespace Snapline.Core.Sim
{
    /// <summary>Result of one simulated run.</summary>
    public struct RunOutcome
    {
        public int PiecesPlaced;
        public long Score;
        public int LinesCleared;
        public int BestCombo;
        public int BestSimultaneousLines;
        public double PeakFillRatio;
        public double EndFillRatio;
    }

    /// <summary>Aggregate over many runs, with the percentiles that actually matter for feel.</summary>
    public sealed class SimReport
    {
        public string Label;
        public int Runs;

        public double MeanPieces;
        public int P05Pieces;
        public int P25Pieces;
        public int MedianPieces;
        public int P75Pieces;
        public int P95Pieces;

        public double MeanScore;
        public long MedianScore;

        public double MeanLines;
        public double MeanBestCombo;
        public double MeanPeakFill;

        /// <summary>Share of runs that ended before this many pieces. The "felt unfair" band.</summary>
        public double ShareUnder20Pieces;
        public double ShareUnder40Pieces;

        public DealerStats Dealer;
        public double MeanDealMicroseconds;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {Label} ({Runs} runs) ---");
            sb.AppendLine($"  pieces placed   mean {MeanPieces,8:F1}   p05 {P05Pieces,4}  p25 {P25Pieces,4}  med {MedianPieces,4}  p75 {P75Pieces,4}  p95 {P95Pieces,4}");
            sb.AppendLine($"  score           mean {MeanScore,8:F0}   median {MedianScore}");
            sb.AppendLine($"  lines cleared   mean {MeanLines,8:F1}   best combo mean {MeanBestCombo:F2}");
            sb.AppendLine($"  peak fill       mean {MeanPeakFill,8:P1}");
            sb.AppendLine($"  short runs      under 20 pieces {ShareUnder20Pieces,6:P2}   under 40 {ShareUnder40Pieces,6:P2}");
            if (Dealer != null)
            {
                sb.AppendLine($"  dealer          trays {Dealer.TraysDealt}  mean attempts {Dealer.MeanAttemptsPerTray:F2}  repaired {Dealer.RepairRate:P2}  dead {Dealer.DeadTrays}");
                sb.AppendLine($"  survivability   searches {Dealer.SurvivabilitySearches}  mean nodes {Dealer.MeanSurvivabilityNodes:F1}");
            }
            sb.AppendLine($"  cost            mean deal {MeanDealMicroseconds:F1} us");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Runs the game headlessly, many times, and reports what the dealing actually produces.
    ///
    /// This exists because the brief calls piece dealing the single biggest factor in whether the
    /// game feels good, and the playbook is explicit that game-feel parameters must be measured
    /// before they are tuned. Every number in the shipped DealerConfig should be traceable to a
    /// sweep run through here, not to an opinion.
    /// </summary>
    public static class Simulator
    {
        /// <summary>Hard stop so a degenerate config that never ends cannot hang the harness.</summary>
        public const int MaxPiecesPerRun = 100000;

        public static RunOutcome PlayOne(DealerConfig dealerConfig, ScoreRules rules, PlayerSkill skill,
                                         ulong seed, HeuristicWeights weights = null)
        {
            var run = new GameRun(dealerConfig, rules);
            var player = new AutoPlayer(skill, weights);
            var rng = new Rng(seed ^ 0xA5A5A5A5A5A5A5A5UL);

            run.StartNew(seed);

            var outcome = new RunOutcome();
            double peakFill = 0.0;

            while (!run.IsGameOver && outcome.PiecesPlaced < MaxPiecesPerRun)
            {
                if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                MoveResult move = run.Place(slot, col, row);
                if (!move.Accepted) break; // defensive: a chooser bug must not spin forever

                outcome.PiecesPlaced++;

                double fill = run.Board.FillRatio;
                if (fill > peakFill) peakFill = fill;
            }

            outcome.Score = run.Score.Score;
            outcome.LinesCleared = run.Score.TotalLinesCleared;
            outcome.BestCombo = run.Score.BestCombo;
            outcome.BestSimultaneousLines = run.Score.BestSimultaneousLines;
            outcome.PeakFillRatio = peakFill;
            outcome.EndFillRatio = run.Board.FillRatio;
            return outcome;
        }

        public static SimReport Measure(string label, DealerConfig dealerConfig, ScoreRules rules,
                                        PlayerSkill skill, int runs, ulong baseSeed = 12345UL,
                                        HeuristicWeights weights = null)
        {
            var pieces = new int[runs];
            var scores = new long[runs];
            double sumPieces = 0, sumScore = 0, sumLines = 0, sumCombo = 0, sumPeak = 0;

            // One shared dealer-stats accumulator would need one shared Dealer, which would share
            // its RNG across runs. Instead each run gets its own and the totals are summed.
            var totals = new DealerStats();
            var sw = new System.Diagnostics.Stopwatch();
            long totalTrays = 0;

            for (int i = 0; i < runs; i++)
            {
                var run = new GameRun(dealerConfig, rules);
                var player = new AutoPlayer(skill, weights);
                var rng = new Rng((baseSeed + (ulong)i) ^ 0xA5A5A5A5A5A5A5A5UL);

                sw.Start();
                run.StartNew(baseSeed + (ulong)i);
                sw.Stop();

                int placed = 0;
                double peak = 0.0;

                while (!run.IsGameOver && placed < MaxPiecesPerRun)
                {
                    if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;

                    sw.Start();
                    MoveResult move = run.Place(slot, col, row);
                    sw.Stop();

                    if (!move.Accepted) break;
                    placed++;
                    double fill = run.Board.FillRatio;
                    if (fill > peak) peak = fill;
                }

                pieces[i] = placed;
                scores[i] = run.Score.Score;
                sumPieces += placed;
                sumScore += run.Score.Score;
                sumLines += run.Score.TotalLinesCleared;
                sumCombo += run.Score.BestCombo;
                sumPeak += peak;

                DealerStats d = run.Dealer.Stats;
                totals.TraysDealt += d.TraysDealt;
                totals.TotalSampleAttempts += d.TotalSampleAttempts;
                totals.TraysRepaired += d.TraysRepaired;
                totals.TraysGuaranteeUnmet += d.TraysGuaranteeUnmet;
                totals.SurvivabilitySearches += d.SurvivabilitySearches;
                totals.SurvivabilityNodes += d.SurvivabilityNodes;
                totals.DeadTrays += d.DeadTrays;
                totalTrays += d.TraysDealt;
            }

            Array.Sort(pieces);
            long[] sortedScores = (long[])scores.Clone();
            Array.Sort(sortedScores);

            int under20 = 0, under40 = 0;
            for (int i = 0; i < runs; i++)
            {
                if (pieces[i] < 20) under20++;
                if (pieces[i] < 40) under40++;
            }

            return new SimReport
            {
                Label = label,
                Runs = runs,
                MeanPieces = sumPieces / runs,
                P05Pieces = Percentile(pieces, 0.05),
                P25Pieces = Percentile(pieces, 0.25),
                MedianPieces = Percentile(pieces, 0.50),
                P75Pieces = Percentile(pieces, 0.75),
                P95Pieces = Percentile(pieces, 0.95),
                MeanScore = sumScore / runs,
                MedianScore = sortedScores[Math.Min(runs - 1, runs / 2)],
                MeanLines = sumLines / runs,
                MeanBestCombo = sumCombo / runs,
                MeanPeakFill = sumPeak / runs,
                ShareUnder20Pieces = under20 / (double)runs,
                ShareUnder40Pieces = under40 / (double)runs,
                Dealer = totals,
                MeanDealMicroseconds = totalTrays == 0
                    ? 0.0
                    : sw.Elapsed.TotalMilliseconds * 1000.0 / totalTrays,
            };
        }

        private static int Percentile(int[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            int idx = (int)Math.Round(p * (sorted.Length - 1));
            if (idx < 0) idx = 0;
            if (idx >= sorted.Length) idx = sorted.Length - 1;
            return sorted[idx];
        }

        /// <summary>
        /// Distribution of dealt shape sizes bucketed by how full the board was at deal time.
        /// This is the direct read on "are small shapes actually being favoured when congested",
        /// which is the claim the congestion weighting makes and therefore the one to verify.
        /// </summary>
        public static string MeasureSizeByCongestion(DealerConfig config, PlayerSkill skill, int runs, ulong baseSeed = 999UL)
        {
            const int buckets = 5;
            var cellSum = new double[buckets];
            var count = new double[buckets];

            for (int i = 0; i < runs; i++)
            {
                var run = new GameRun(config, new ScoreRules());
                var player = new AutoPlayer(skill);
                var rng = new Rng((baseSeed + (ulong)i) ^ 0x5A5A5A5A5A5A5A5AUL);

                double fillAtDeal = 0.0;
                run.TrayDealt += () => { };
                run.StartNew(baseSeed + (ulong)i);

                int placed = 0;
                while (!run.IsGameOver && placed < MaxPiecesPerRun)
                {
                    fillAtDeal = run.Board.FillRatio;
                    int b = Math.Min(buckets - 1, (int)(fillAtDeal * buckets));

                    for (int t = 0; t < run.Tray.Length; t++)
                    {
                        if (run.Tray[t].IsEmpty) continue;
                        cellSum[b] += run.Tray[t].Shape.CellCount;
                        count[b] += 1;
                    }

                    if (!player.ChooseMove(run, ref rng, out int slot, out int col, out int row)) break;
                    if (!run.Place(slot, col, row).Accepted) break;
                    placed++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("  mean cells per offered piece, by board fill at deal time:");
            for (int b = 0; b < buckets; b++)
            {
                double lo = b / (double)buckets, hi = (b + 1) / (double)buckets;
                double mean = count[b] == 0 ? 0 : cellSum[b] / count[b];
                sb.AppendLine($"    fill {lo,4:P0}-{hi,4:P0}   {mean,5:F2} cells   (n={count[b]:F0})");
            }
            return sb.ToString();
        }
    }
}
