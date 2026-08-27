using System;

namespace Snapline.Core
{
    /// <summary>
    /// Every scoring number in one mutable place. Nothing here is a magic constant buried in the
    /// engine, because these are exactly the values the simulator sweeps when tuning game feel.
    /// </summary>
    public sealed class ScoreRules
    {
        /// <summary>Awarded per cell just for placing a piece, so a dry move still ticks up.</summary>
        public int PointsPerCell = 1;

        /// <summary>Base value of one cleared line before any multiplier.</summary>
        public int PointsPerLine = 100;

        /// <summary>
        /// Multiplier for clearing N lines with a single piece, indexed by N. Index 0 is unused.
        /// Superlinear on purpose: a quadruple should feel like an event, not like four singles.
        /// </summary>
        public double[] SimultaneousMultiplier = { 0.0, 1.0, 1.6, 2.4, 3.4, 4.6, 6.0, 7.6, 9.4, 11.4,
                                                   13.6, 16.0, 18.6, 21.4, 24.4, 27.6, 31.0 };

        /// <summary>Added to the combo multiplier for each consecutive clearing move beyond the first.</summary>
        public double ComboStep = 0.5;

        /// <summary>
        /// How many moves that clear nothing a streak survives before it breaks.
        ///
        /// Measured with `dotnet run -- combo`, 400 runs each against the heuristic player:
        ///
        ///   grace   mean best combo   runs reaching x5   moves inside a combo   mean score
        ///     0          3.82              24.2 %              11.4 %             21,418
        ///     1          5.94              82.3 %              35.0 %             26,189
        ///     2         13.07              95.3 %              67.2 %             43,510
        ///     3         20.96              97.5 %              80.9 %             61,928
        ///
        /// 1 is the pick. It triples how often a combo is live and turns a x5 streak from rare into
        /// something most runs see, for only ~22% score inflation. At 2 and above a combo is running
        /// on the majority of moves, which stops it being a reward and just becomes the base rate.
        ///
        /// Median run length was 320 pieces at every setting, so this changes how often the game
        /// feels generous without changing how hard it is.
        /// </summary>
        public int ComboGraceMoves = 1;

        /// <summary>Ceiling on the combo multiplier, so a long streak cannot run away with the score.</summary>
        public double MaxComboMultiplier = 8.0;

        /// <summary>Flat bonus for emptying the board completely. Rare, and should feel like it.</summary>
        public int PerfectClearBonus = 3000;

        public double MultiplierForLines(int lines)
        {
            if (lines <= 0) return 0.0;
            int i = Math.Min(lines, SimultaneousMultiplier.Length - 1);
            return SimultaneousMultiplier[i];
        }
    }

    /// <summary>Running score and combo state for one run. Plain data, trivially serialisable.</summary>
    public sealed class ScoreState
    {
        public long Score;

        /// <summary>Consecutive moves that cleared at least one line. Resets on a dry move.</summary>
        public int ComboCount;

        public int BestCombo;
        public int TotalLinesCleared;
        public int TotalPiecesPlaced;
        public int BestSimultaneousLines;

        /// <summary>Dry moves since the last clear. Compared against ScoreRules.ComboGraceMoves.</summary>
        public int DryMovesSinceClear;

        public void Reset()
        {
            Score = 0;
            ComboCount = 0;
            BestCombo = 0;
            TotalLinesCleared = 0;
            TotalPiecesPlaced = 0;
            BestSimultaneousLines = 0;
            DryMovesSinceClear = 0;
        }
    }

    /// <summary>Breakdown of one move's award, so the UI can pop the pieces separately.</summary>
    public struct ScoreDelta
    {
        public int PlacementPoints;
        public int LinePoints;
        public int PerfectClearPoints;
        public double SimultaneousMultiplier;
        public double ComboMultiplier;
        public int ComboCount;
        public int LinesCleared;

        public int Total => PlacementPoints + LinePoints + PerfectClearPoints;
    }

    public static class Scoring
    {
        /// <summary>
        /// Apply one placement to the score state and return the breakdown.
        ///
        /// The combo multiplier is read from the streak *before* this move is counted, so the first
        /// clearing move of a streak scores at 1.0x and the second at 1.0 + ComboStep. Counting it
        /// after would silently hand every player a free multiplier on their opening clear.
        /// </summary>
        public static ScoreDelta Apply(ScoreState state, ScoreRules rules, in PlaceResult result, bool boardEmptyAfter)
        {
            var delta = new ScoreDelta();
            if (!result.Placed) return delta;

            state.TotalPiecesPlaced++;
            delta.PlacementPoints = result.CellsPlaced * rules.PointsPerCell;

            int lines = result.LinesCleared;
            delta.LinesCleared = lines;

            if (lines > 0)
            {
                double simMult = rules.MultiplierForLines(lines);
                double comboMult = Math.Min(1.0 + rules.ComboStep * state.ComboCount, rules.MaxComboMultiplier);

                delta.SimultaneousMultiplier = simMult;
                delta.ComboMultiplier = comboMult;
                delta.LinePoints = (int)Math.Round(lines * rules.PointsPerLine * simMult * comboMult);

                state.ComboCount++;
                state.DryMovesSinceClear = 0;
                state.TotalLinesCleared += lines;
                if (state.ComboCount > state.BestCombo) state.BestCombo = state.ComboCount;
                if (lines > state.BestSimultaneousLines) state.BestSimultaneousLines = lines;

                if (boardEmptyAfter) delta.PerfectClearPoints = rules.PerfectClearBonus;
            }
            else
            {
                // A streak survives ComboGraceMoves dry moves. Without any grace a combo needs a
                // clear on literally every move, which almost never happens outside expert play.
                state.DryMovesSinceClear++;
                if (state.DryMovesSinceClear > rules.ComboGraceMoves)
                {
                    state.ComboCount = 0;
                    state.DryMovesSinceClear = 0;
                }

                delta.ComboMultiplier = 1.0;
            }

            delta.ComboCount = state.ComboCount;
            state.Score += delta.Total;
            return delta;
        }
    }
}
