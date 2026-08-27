using System;

namespace Snapline.Core
{
    /// <summary>Everything one move produced. The view animates from this and nothing else.</summary>
    public struct MoveResult
    {
        public bool Accepted;
        public int TraySlot;
        public int Col;
        public int Row;
        public byte Colour;

        /// <summary>The shape that was placed. Carried here because the tray slot is cleared immediately.</summary>
        public int ShapeId;
        public PlaceResult Placement;
        public ScoreDelta Score;

        /// <summary>True when this move emptied the tray and a fresh one was dealt.</summary>
        public bool TrayRefilled;

        /// <summary>True when nothing in the remaining tray fits. The run is over.</summary>
        public bool GameOver;

        /// <summary>True when the board was completely emptied by this move.</summary>
        public bool PerfectClear;
    }

    /// <summary>
    /// A complete, restorable run. Plain data so a save is a value copy, not a graph walk.
    /// </summary>
    public sealed class RunSnapshot
    {
        public int Version = 1;
        public ulong Occupied;
        public byte[] Colours;
        public int[] TrayShapeIds;
        public byte[] TrayColours;
        public bool[] TrayConsumed;
        public ulong RngState;
        public long Score;
        public int ComboCount;
        public int BestCombo;
        public int TotalLinesCleared;
        public int TotalPiecesPlaced;
        public int BestSimultaneousLines;
        public bool GameOver;
    }

    /// <summary>
    /// The rules engine: board plus tray plus score plus dealer, and the game-over condition that
    /// ties them together.
    ///
    /// Contains no UnityEngine reference of any kind, which is enforced at the assembly level by
    /// Snapline.Core.asmdef (noEngineReferences). That is what lets the whole game be simulated
    /// millions of times in a console harness and unit-tested without opening the editor.
    /// </summary>
    public sealed class GameRun
    {
        public Board Board { get; } = new Board();
        public ScoreState Score { get; } = new ScoreState();
        public ScoreRules Rules { get; }
        public Dealer Dealer { get; }
        public TrayPiece[] Tray { get; }

        private Rng _rng;

        public bool IsGameOver { get; private set; }
        public ulong RngState => _rng.State;

        /// <summary>Raised after a move resolves. The presentation layer subscribes; the engine never calls back into it.</summary>
        public event Action<MoveResult> MoveResolved;

        /// <summary>Raised when a fresh tray has been dealt.</summary>
        public event Action TrayDealt;

        /// <summary>Raised once, when the run ends.</summary>
        public event Action GameOverRaised;

        public GameRun(DealerConfig dealerConfig = null, ScoreRules rules = null)
        {
            Rules = rules ?? new ScoreRules();
            Dealer = new Dealer(dealerConfig ?? DealerConfig.Default());
            Tray = new TrayPiece[Math.Max(1, Dealer.Config.TraySize)];
        }

        public void StartNew(ulong seed)
        {
            Board.Clear();
            Score.Reset();
            _rng = new Rng(seed);
            IsGameOver = false;
            DealTray();
            RefreshGameOver();
        }

        // --- moves ---------------------------------------------------------------------

        /// <summary>True if that tray piece can legally go at that anchor right now.</summary>
        public bool CanPlace(int traySlot, int col, int row)
        {
            if (IsGameOver) return false;
            if (traySlot < 0 || traySlot >= Tray.Length) return false;
            TrayPiece piece = Tray[traySlot];
            if (piece.IsEmpty) return false;
            return Board.CanPlaceAt(piece.Shape, col, row);
        }

        /// <summary>
        /// Place a tray piece. Returns a result whose Accepted flag is false, with no state changed,
        /// if the move was illegal, so the view can just shake the piece and drop it back.
        /// </summary>
        public MoveResult Place(int traySlot, int col, int row)
        {
            var move = new MoveResult { TraySlot = traySlot, Col = col, Row = row };
            if (!CanPlace(traySlot, col, row)) return move;

            TrayPiece piece = Tray[traySlot];
            move.Colour = piece.Colour;
            move.ShapeId = piece.ShapeId;
            move.Placement = Board.Place(piece.Shape, col, row, piece.Colour);
            if (!move.Placement.Placed) return move;

            move.Accepted = true;

            bool emptyAfter = Board.IsEmpty;
            move.PerfectClear = emptyAfter && move.Placement.LinesCleared > 0;
            move.Score = Scoring.Apply(Score, Rules, move.Placement, emptyAfter);

            Tray[traySlot].Consumed = true;
            Tray[traySlot].ShapeId = -1;

            if (TrayIsEmpty())
            {
                DealTray();
                move.TrayRefilled = true;
            }

            RefreshGameOver();
            move.GameOver = IsGameOver;

            MoveResolved?.Invoke(move);
            if (IsGameOver) GameOverRaised?.Invoke();

            return move;
        }

        public bool TrayIsEmpty()
        {
            for (int i = 0; i < Tray.Length; i++)
                if (!Tray[i].IsEmpty) return false;
            return true;
        }

        /// <summary>True when no piece has been taken from the current tray yet.</summary>
        public bool TrayFullyDealt()
        {
            for (int i = 0; i < Tray.Length; i++)
                if (Tray[i].IsEmpty) return false;
            return true;
        }

        /// <summary>Does any piece still in the tray fit anywhere on the board?</summary>
        public bool AnyRemainingPieceFits()
        {
            ulong occupied = Board.Occupied;
            for (int i = 0; i < Tray.Length; i++)
            {
                if (Tray[i].IsEmpty) continue;
                if (Board.CanPlaceAnywhere(occupied, Tray[i].Shape)) return true;
            }
            return false;
        }

        /// <summary>Anchors the given tray piece legally fits at. Used for the drag-time hint overlay.</summary>
        public int CountPlacements(int traySlot)
        {
            if (traySlot < 0 || traySlot >= Tray.Length) return 0;
            TrayPiece piece = Tray[traySlot];
            if (piece.IsEmpty) return 0;
            return Board.PlacementCount(Board.Occupied, piece.Shape);
        }

        private void DealTray()
        {
            Dealer.Deal(Board.Occupied, Tray, ref _rng);
            TrayDealt?.Invoke();
        }

        private void RefreshGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = !AnyRemainingPieceFits();
        }

        // --- persistence ---------------------------------------------------------------

        public RunSnapshot Snapshot()
        {
            var trayIds = new int[Tray.Length];
            var trayColours = new byte[Tray.Length];
            var trayConsumed = new bool[Tray.Length];
            for (int i = 0; i < Tray.Length; i++)
            {
                trayIds[i] = Tray[i].ShapeId;
                trayColours[i] = Tray[i].Colour;
                trayConsumed[i] = Tray[i].Consumed;
            }

            return new RunSnapshot
            {
                Version = 1,
                Occupied = Board.Occupied,
                Colours = Board.CopyColours(),
                TrayShapeIds = trayIds,
                TrayColours = trayColours,
                TrayConsumed = trayConsumed,
                RngState = _rng.State,
                Score = Score.Score,
                ComboCount = Score.ComboCount,
                BestCombo = Score.BestCombo,
                TotalLinesCleared = Score.TotalLinesCleared,
                TotalPiecesPlaced = Score.TotalPiecesPlaced,
                BestSimultaneousLines = Score.BestSimultaneousLines,
                GameOver = IsGameOver,
            };
        }

        /// <summary>
        /// Restore a saved run exactly, including the RNG state, so the pieces that follow are the
        /// ones the player would have got had they never closed the app.
        /// </summary>
        public void Restore(RunSnapshot snap)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));

            Board.Restore(snap.Occupied, snap.Colours);

            for (int i = 0; i < Tray.Length; i++)
            {
                int id = snap.TrayShapeIds != null && i < snap.TrayShapeIds.Length ? snap.TrayShapeIds[i] : -1;
                bool consumed = snap.TrayConsumed != null && i < snap.TrayConsumed.Length ? snap.TrayConsumed[i] : true;
                byte colour = snap.TrayColours != null && i < snap.TrayColours.Length ? snap.TrayColours[i] : (byte)0;

                // Guard against a save written by a build with a longer shape catalogue.
                if (id >= Shapes.Count) { id = -1; consumed = true; }

                Tray[i] = new TrayPiece { ShapeId = id, Colour = colour, Consumed = consumed || id < 0 };
            }

            _rng = new Rng(0);
            _rng.State = snap.RngState;

            Score.Score = snap.Score;
            Score.ComboCount = snap.ComboCount;
            Score.BestCombo = snap.BestCombo;
            Score.TotalLinesCleared = snap.TotalLinesCleared;
            Score.TotalPiecesPlaced = snap.TotalPiecesPlaced;
            Score.BestSimultaneousLines = snap.BestSimultaneousLines;

            IsGameOver = snap.GameOver;

            // A save taken mid-tray can legitimately have an empty tray if the app was killed in
            // the instant between consuming the last piece and dealing. Deal rather than deadlock.
            if (!IsGameOver && TrayIsEmpty()) DealTray();

            RefreshGameOver();
        }
    }
}
