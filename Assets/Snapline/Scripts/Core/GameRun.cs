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

        /// <summary>Level mode only: this move met the line target.</summary>
        public bool LevelComplete;

        /// <summary>Level mode only: the run ended without meeting the target.</summary>
        public bool LevelFailed;

        /// <summary>Level mode only: moves left in the budget after this one.</summary>
        public int MovesRemaining;
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

        /// <summary>Added in save version 2. Absent from a version 1 save, which defaults it to 0.</summary>
        public int DryMovesSinceClear;

        /// <summary>
        /// Level moves spent at the moment of the snapshot. Kept for undo only — it is NOT written
        /// by SaveCodec, because only endless runs are saved and they have no move count.
        /// </summary>
        public int MovesUsed;

        /// <summary>Level gift moves at the moment of the snapshot. Undo only; not saved.</summary>
        public int BonusMoves;

        /// <summary>
        /// Special blocks at the moment of the snapshot. Undo only; not saved, because only endless
        /// runs are saved and endless has no special blocks. Null restores a board without any.
        /// </summary>
        public byte[] Specials;
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

        /// <summary>Which mode this run is being played in. Endless has no objective.</summary>
        public GameMode Mode { get; private set; } = GameMode.Endless;

        /// <summary>Null in endless. In level mode, what has to be achieved and by when.</summary>
        public LevelObjective Objective { get; private set; }

        /// <summary>Level mode only: which level this is, for saving and for the results screen.</summary>
        public int LevelNumber { get; private set; }

        public int MovesUsed { get; private set; }

        public int MovesRemaining =>
            Objective == null ? int.MaxValue : Math.Max(0, Objective.MoveBudget + BonusMoves - MovesUsed);

        /// <summary>Extra moves earned in this level, from gift blocks.</summary>
        public int BonusMoves { get; private set; }

        /// <summary>Moves a single gift block is worth.</summary>
        public const int GiftMoves = 3;

        public bool LevelComplete { get; private set; }
        public bool LevelFailed { get; private set; }

        /// <summary>Lines still needed. Level mode only.</summary>
        public int LinesRemaining =>
            Objective == null ? 0 : Math.Max(0, Objective.LineTarget - Score.TotalLinesCleared);

        public void StartNew(ulong seed)
        {
            Mode = GameMode.Endless;
            Objective = null;
            LevelNumber = 0;
            ResetRun(seed);
        }

        /// <summary>Begin a level. The seed comes from the level, so everyone gets the same challenge.</summary>
        public void StartLevel(LevelDef level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            Mode = GameMode.Level;
            Objective = level.ToObjective();
            LevelNumber = level.Number;
            ResetRun(level.Seed, level.StartOccupied, level.StartColours, level.StartSpecials);
        }

        private void ResetRun(ulong seed, ulong startOccupied = 0UL, byte[] startColours = null,
                              byte[] startSpecials = null)
        {
            Board.Clear();
            if (startOccupied != 0UL) Board.Restore(startOccupied, startColours, startSpecials);
            _undoPoint = null;
            BonusMoves = 0;
            Score.Reset();
            _rng = new Rng(seed);
            IsGameOver = false;
            MovesUsed = 0;
            LevelComplete = false;
            LevelFailed = false;
            DealTray();
            RefreshGameOver();
            ResolveLevelOutcome();
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

            // Taken before anything changes, and only once the move is known to be legal, so a
            // rejected drop cannot quietly consume the player's undo.
            _undoPoint = Snapshot();

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

            if (Objective != null) MovesUsed++;
            if (Objective != null && move.Placement.GiftsCollected > 0)
                BonusMoves += GiftMoves * move.Placement.GiftsCollected;

            if (TrayIsEmpty())
            {
                DealTray();
                move.TrayRefilled = true;
            }

            RefreshGameOver();
            ResolveLevelOutcome();

            move.GameOver = IsGameOver;
            move.LevelComplete = LevelComplete;
            move.LevelFailed = LevelFailed;
            move.MovesRemaining = MovesRemaining;

            MoveResolved?.Invoke(move);
            if (IsGameOver) GameOverRaised?.Invoke();

            return move;
        }

        /// <summary>
        /// Decide whether a level has been won or lost.
        ///
        /// Meeting the line target wins immediately, even on the move that also exhausts the budget
        /// or fills the board — reaching the goal should never be stolen by a technicality. Anything
        /// else that ends the run in level mode is a loss.
        /// </summary>
        private void ResolveLevelOutcome()
        {
            if (Objective == null || LevelComplete || LevelFailed) return;

            if (Score.TotalLinesCleared >= Objective.LineTarget)
            {
                LevelComplete = true;
                IsGameOver = true;
                return;
            }

            if (MovesRemaining <= 0 || IsGameOver)
            {
                LevelFailed = true;
                IsGameOver = true;
            }
        }

        /// <summary>How many times this run has been rescued. The app decides the cap.</summary>
        public int RevivesUsed { get; private set; }

        /// <summary>
        /// Bring a finished endless run back to life by clearing space and dealing a fresh tray.
        ///
        /// Only meaningful in endless: a level has a move budget, so reviving it would just be a
        /// worse version of RETRY. The score carries over — that is the whole point of paying for it
        /// — but the combo streak does not, so a revive cannot be used to bank a multiplier.
        ///
        /// Returns the cells cleared, so the view can blow them up with the ordinary clear effect.
        /// Zero means the revive was refused or achieved nothing.
        /// </summary>
        public ulong Revive(int rowsToClear = 3)
        {
            if (!IsGameOver || Mode != GameMode.Endless) return 0UL;

            ulong cleared = Board.ClearFullestRows(rowsToClear);

            RevivesUsed++;
            Score.ComboCount = 0;
            Score.DryMovesSinceClear = 0;
            IsGameOver = false;

            // Throw away whatever could not be placed and deal again. The dealer's survivability
            // guarantee then applies to the freshly cleared board, so the new tray is playable.
            for (int i = 0; i < Tray.Length; i++) Tray[i] = TrayPiece.Empty;
            DealTray();

            RefreshGameOver();

            // If even a cleared board cannot take the new tray, the rescue failed; do not pretend.
            return IsGameOver ? 0UL : cleared;
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

        /// <summary>
        /// The state to return to if the player undoes. One move deep.
        ///
        /// Deliberately not saved. A resumed run cannot undo the move that came before the app was
        /// closed, which costs a player almost nothing and keeps the save format — and the version-1
        /// and version-2 compatibility already proven by tests — exactly as it is.
        /// </summary>
        private RunSnapshot _undoPoint;

        public bool CanUndo => _undoPoint != null && !IsGameOver;

        /// <summary>
        /// Takes back the last placement, restoring the board, the tray, the score and the combo.
        ///
        /// Returns false when there is nothing to undo, so the caller can decline to spend the tool
        /// rather than charging for nothing. The undo point is cleared either way: undo is one move
        /// deep, and letting it be used twice would silently rewind two.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;

            RunSnapshot point = _undoPoint;
            _undoPoint = null;
            Restore(point);

            // A level's move count is not part of the save format, so Restore leaves it alone. An
            // undo that took the piece back but kept the move spent would read as a bug.
            if (Objective != null)
            {
                MovesUsed = point.MovesUsed;
                BonusMoves = point.BonusMoves;
            }
            return true;
        }

        /// <summary>
        /// Replaces the three tray pieces with a fresh deal — what the SHUFFLE tool does.
        ///
        /// Redeals the tray rather than rearranging the board, because rearranging cannot help: a
        /// board with no room still has no room whatever order its blocks are in. Only new pieces can.
        ///
        /// The deal goes through the ordinary dealer, so the tray guarantee applies and a shuffle
        /// cannot hand back another dead tray.
        ///
        /// This is the mid-run tool, and it refuses to act on a finished run — rescuing a dead board
        /// is <see cref="Revive"/>, which clears space as well and is capped at once per run. On the
        /// continue card a shuffle is one of three ways to *pay* for that rescue, not a weaker
        /// version of it.
        /// </summary>
        public bool ShuffleTray()
        {
            if (IsGameOver) return false;

            for (int i = 0; i < Tray.Length; i++) Tray[i] = TrayPiece.Empty;
            DealTray();

            // An undo across a shuffle would restore the pieces the player just paid to be rid of.
            _undoPoint = null;

            RefreshGameOver();
            return true;
        }

        /// <summary>
        /// Removes a single placed block — what the HAMMER tool does.
        ///
        /// It scores nothing and clears no line even if it empties one. A hammer is a way out of a
        /// mistake, not a way to earn: paying coins for points would make score a function of
        /// spending, and the combo would break on a move the player did not really make.
        ///
        /// Returns false if the cell was already empty, so a misplaced tap does not consume the tool.
        /// </summary>
        public bool Hammer(int col, int row)
        {
            if (!Board.IsOccupied(col, row)) return false;

            _undoPoint = Snapshot();
            if (!Board.ClearCell(col, row)) return false;

            // Clearing a cell can only ever open the board up, so a run that was over may not be.
            IsGameOver = false;
            RefreshGameOver();
            return true;
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
            Dealer.StickyMask = Board.StickyMask;
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
                DryMovesSinceClear = Score.DryMovesSinceClear,
                MovesUsed = MovesUsed,
                BonusMoves = BonusMoves,
                Specials = Board.CopySpecials(),
            };
        }

        /// <summary>
        /// Restore a saved run exactly, including the RNG state, so the pieces that follow are the
        /// ones the player would have got had they never closed the app.
        /// </summary>
        public void Restore(RunSnapshot snap)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));

            Board.Restore(snap.Occupied, snap.Colours, snap.Specials);

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

            Score.DryMovesSinceClear = snap.DryMovesSinceClear;

            IsGameOver = snap.GameOver;

            // A save taken mid-tray can legitimately have an empty tray if the app was killed in
            // the instant between consuming the last piece and dealing. Deal rather than deadlock.
            if (!IsGameOver && TrayIsEmpty()) DealTray();

            RefreshGameOver();
        }
    }
}
