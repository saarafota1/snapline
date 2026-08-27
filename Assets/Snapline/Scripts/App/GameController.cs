using System.Collections;
using UnityEngine;
using Snapline.Art;
using GameKit.Art;
using Snapline.Core;
using Snapline.UI;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Joins the rules engine to everything the player can see.
    ///
    /// All the decisions live in Snapline.Core; this reacts to them. It owns no rules of its own
    /// beyond how loud a given event should be.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private GameRun _run;
        private BoardView _board;
        private TrayView _tray;
        private DragController _drag;
        private Hud _hud;
        private GameOverPanel _gameOver;
        private Juice _juice;
        private Sfx _sfx;

        private bool _busy;

        public GameRun Run => _run;

        public void Init(BoardView board, TrayView tray, DragController drag, Hud hud,
                         GameOverPanel gameOver, Juice juice, Sfx sfx)
        {
            _board = board;
            _tray = tray;
            _drag = drag;
            _hud = hud;
            _gameOver = gameOver;
            _juice = juice;
            _sfx = sfx;

            _run = new GameRun(DealerConfig.Default(), new ScoreRules());

            _drag.CanPlace = (slot, col, row) => _run.CanPlace(slot, col, row);
            _drag.ShapeInSlot = slot => _run.Tray[slot].IsEmpty ? null : _run.Tray[slot].Shape;
            _drag.PlacementRequested += OnPlacementRequested;
            _drag.PlacementRejected += OnPlacementRejected;

            _gameOver.PlayAgainRequested += StartNewRun;
            _gameOver.MenuRequested += () => MenuRequested?.Invoke();
            _gameOver.ShareRequested += ShareScore;
            _hud.HomeRequested += () => { SaveNow(); MenuRequested?.Invoke(); };
        }

        /// <summary>Raised when the player asks to go back to the front screen.</summary>
        public event System.Action MenuRequested;

        public static bool HasSavedRun => SaveSystem.HasSavedRun();

        /// <summary>Pick the saved run back up exactly where it was left.</summary>
        public bool ResumeSavedRun()
        {
            RunSnapshot saved = SaveSystem.LoadRun();
            if (saved == null) return false;

            _gameOver.Hide();
            _run.Restore(saved);

            _board.SyncFromBoard(_run.Board);
            _tray.Refresh(_run.Tray, animate: false);
            _hud.ResetForNewRun(SaveSystem.HighScore);
            _hud.SetScoreImmediate(_run.Score.Score);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));
            RefreshSlotPlayability();

            _drag.InputEnabled = !_run.IsGameOver;
            _busy = false;

            if (_run.IsGameOver) ShowGameOver(isNewBest: false);
            return true;
        }

        /// <summary>What a streak of this length is currently worth, for display.</summary>
        private double ComboMultiplier(int combo) =>
            System.Math.Min(1.0 + _run.Rules.ComboStep * System.Math.Max(0, combo - 1),
                            _run.Rules.MaxComboMultiplier);

        /// <summary>Offer the current score to the system share sheet.</summary>
        public void ShareScore()
        {
            long score = _run != null && _run.Score.Score > 0 ? _run.Score.Score : SaveSystem.HighScore;
            GameKit.Share.Text(ShareMessage(score));
        }

        public static string ShareMessage(long score) =>
            score > 0
                ? $"I scored {Hud.Format(score)} in Snapline! Think you can beat that?"
                : "I'm playing Snapline — see if you can beat my score!";

        public void StartNewRun()
        {
            _gameOver.Hide();
            SaveSystem.ClearRun();

            _run.StartNew(NewSeed());

            _board.SyncFromBoard(_run.Board);
            _tray.Refresh(_run.Tray, animate: true);
            _hud.ResetForNewRun(SaveSystem.HighScore);
            RefreshSlotPlayability();

            _drag.InputEnabled = true;
            _busy = false;

            SaveNow();
        }

        /// <summary>
        /// A seed that differs every run but is still a plain number, so a player-reported run can
        /// be reproduced exactly in the console harness from the value stored in their save.
        /// </summary>
        private static ulong NewSeed()
        {
            ulong a = (ulong)System.DateTime.UtcNow.Ticks;
            ulong b = (ulong)Random.Range(int.MinValue, int.MaxValue) & 0xFFFFFFFFUL;
            return a ^ (b << 21) ^ 0x9E3779B97F4A7C15UL;
        }

        // --- moves --------------------------------------------------------------------------

        /// <summary>
        /// Play a move without going through the drag input. Used by the screenshot and smoke
        /// harnesses so they exercise the real animation and scoring path rather than a shortcut.
        /// </summary>
        public void PlaceProgrammatically(int slot, int col, int row) => OnPlacementRequested(slot, col, row);

        /// <summary>True while a move is still animating and input should be ignored.</summary>
        public bool IsBusy => _busy;

        private void OnPlacementRequested(int slot, int col, int row)
        {
            if (_busy || _run.IsGameOver) return;

            MoveResult move = _run.Place(slot, col, row);
            if (!move.Accepted)
            {
                OnPlacementRejected(slot);
                return;
            }

            StartCoroutine(ResolveMove(move));
        }

        private IEnumerator ResolveMove(MoveResult move)
        {
            _busy = true;

            _board.AnimatePlacement(move.Placement.PieceMask, move.Colour, move.Placement.ClearedMask);
            _tray.Piece(move.TraySlot).Clear();

            int lines = move.Placement.LinesCleared;

            if (lines > 0)
            {
                _board.AnimateClear(move.Placement);
                _juice.Shake(ShakeFor(lines, move.Score.ComboCount));
                ShowClearPopups(move);

                _sfx.PlayClear(lines);
                _sfx.PlayCombo(move.Score.ComboCount);
                if (move.PerfectClear) _sfx.PlayPerfect();
            }
            else
            {
                _juice.Shake(0.06f);
                _sfx.PlayPlace();
            }

            _hud.SetScore(_run.Score.Score);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));

            if (move.TrayRefilled)
            {
                // Let the explosion breathe before three new pieces slide in underneath it.
                yield return new WaitForSeconds(lines > 0 ? 0.20f : 0.06f);
                _tray.Refresh(_run.Tray, animate: true);
            }

            RefreshSlotPlayability();
            SaveNow();

            _busy = false;

            if (move.GameOver)
            {
                _drag.InputEnabled = false;
                _sfx.PlayGameOver();
                yield return new WaitForSeconds(0.45f);
                yield return _board.PlayGameOverSweep();
                yield return new WaitForSeconds(0.25f);

                bool isNewBest = SaveSystem.SubmitScore(_run.Score.Score);
                SaveSystem.RecordFinishedRun(_run.Score.Score, _run.Score.TotalLinesCleared, _run.Score.BestCombo);
                SaveSystem.ClearRun();
                ShowGameOver(isNewBest);
            }
        }

        private void OnPlacementRejected(int slot)
        {
            if (slot < 0 || slot >= _tray.SlotCount) return;
            _sfx.PlayInvalid();
            StartCoroutine(_tray.ReturnToSlot(slot));
        }

        // --- feedback -----------------------------------------------------------------------

        /// <summary>
        /// Shake grows with the size of the clear and again with the combo, but is clamped: past a
        /// point more shake stops reading as impact and starts reading as a bug.
        /// </summary>
        private static float ShakeFor(int lines, int combo)
        {
            float baseAmount = 0.20f + 0.14f * (lines - 1);
            float comboAmount = 0.04f * Mathf.Max(0, combo - 1);
            return Mathf.Min(0.85f, baseAmount + comboAmount);
        }

        /// <summary>
        /// Everything the player is told about a clear, stacked vertically over the piece they just
        /// dropped. Deliberately layered rather than one message: what happened (DOUBLE!), how well
        /// they are doing (GREAT!), what the streak is worth (x2.5 POINTS) and what they earned
        /// (+324) are four different pieces of information, and merging them loses all four.
        ///
        /// Each line is gated, so an ordinary single-line clear stays quiet and a big one is loud.
        /// </summary>
        private void ShowClearPopups(in MoveResult move)
        {
            int lines = move.Placement.LinesCleared;
            int combo = move.Score.ComboCount;
            double totalMultiplier = move.Score.SimultaneousMultiplier * move.Score.ComboMultiplier;

            Vector2 centre = PopupAnchor(move);

            string headline = lines switch
            {
                1 => "CLEAR!",
                2 => "DOUBLE!",
                3 => "TRIPLE!",
                4 => "QUAD!",
                5 => "MASSIVE!",
                _ => "UNREAL!",
            };

            Color headlineColour = lines >= 3 ? new Color(1f, 0.78f, 0.25f) : Color.white;
            _juice.Popup(centre, headline, headlineColour, 62f + 10f * Mathf.Min(lines, 5));

            _juice.Popup(centre + new Vector2(0f, -84f), $"+{Hud.Format(move.Score.Total)}",
                         new Color(0.75f, 0.95f, 1f), 52f, 0.85f);

            // Double points and beyond gets said out loud — otherwise the multiplier only ever
            // shows up as a number that got bigger for no visible reason.
            if (totalMultiplier >= 1.95)
            {
                _juice.Popup(centre + new Vector2(0f, -158f), $"x{totalMultiplier:0.#} POINTS",
                             new Color(1f, 0.85f, 0.35f), 48f, 0.9f);
            }

            if (combo >= 2)
            {
                _juice.Popup(centre + new Vector2(0f, 92f), $"COMBO x{combo}",
                             new Color(1f, 0.55f, 0.85f), 54f, 1.0f);
            }

            string praise = PraiseFor(lines, combo);
            if (praise != null)
            {
                _juice.Popup(centre + new Vector2(0f, 176f), praise, new Color(0.6f, 1f, 0.75f),
                             58f + 6f * Mathf.Min(combo, 6), 1.15f);
            }

            if (move.PerfectClear)
            {
                _juice.Popup(centre + new Vector2(0f, 258f), "PERFECT CLEAR", Palette.Accent, 66f, 1.4f);
                _juice.Shake(0.9f);
            }
        }

        /// <summary>
        /// A word for how well that went, or null when the move does not deserve one.
        ///
        /// Driven by the streak first and the size of the clear second, so praise escalates as the
        /// player keeps something going rather than firing on every lucky single line.
        /// </summary>
        private static string PraiseFor(int lines, int combo)
        {
            if (combo >= 10) return "LEGENDARY!";
            if (combo >= 8) return "UNSTOPPABLE!";
            if (combo >= 6) return "ON FIRE!";
            if (combo >= 4) return "AMAZING!";
            if (combo >= 3) return "GREAT!";
            if (combo >= 2) return "NICE!";

            // No streak, but a big single move still deserves acknowledgement.
            return lines >= 3 ? "SUPERB!" : null;
        }

        /// <summary>
        /// Where the popup stack is centred: over the piece the player just dropped, rather than a
        /// fixed spot, so their eye is already looking at it.
        ///
        /// Clamped vertically because the stack reaches well above and below this point. A clear on
        /// the top row would otherwise throw "GREAT!" and the combo badge straight over the score,
        /// and one on the bottom row would push the points readout behind the tray.
        /// </summary>
        private Vector2 PopupAnchor(in MoveResult move)
        {
            ShapeDef shape = Shapes.Get(move.ShapeId);

            int cx = Mathf.Clamp(move.Col + Mathf.RoundToInt(shape.Width * 0.5f - 0.5f), 0, Board.Width - 1);
            int cy = Mathf.Clamp(move.Row + Mathf.RoundToInt(shape.Height * 0.5f - 0.5f), 0, Board.Height - 1);

            Vector3 world = _board.CellToWorld(cx, cy);
            Vector2 local = _juice.transform.InverseTransformPoint(world);

            local.y = Mathf.Clamp(local.y, PopupAnchorMinY, PopupAnchorMaxY);
            return local;
        }

        /// <summary>
        /// Vertical band the popup stack may be centred in, in canvas units from the screen centre.
        /// Derived from the HUD height at the top and the tray height at the bottom, with room for
        /// the tallest entry in the stack (PERFECT CLEAR, +258) and the ~80 units popups rise.
        /// </summary>
        private const float PopupAnchorMaxY = 282f;
        private const float PopupAnchorMinY = -402f;

        private void RefreshSlotPlayability()
        {
            for (int i = 0; i < _tray.SlotCount && i < _run.Tray.Length; i++)
            {
                if (_run.Tray[i].IsEmpty) continue;
                bool fits = Board.CanPlaceAnywhere(_run.Board.Occupied, _run.Tray[i].Shape);
                _tray.SetSlotPlayable(i, fits);
            }
        }

        private void ShowGameOver(bool isNewBest)
        {
            _drag.InputEnabled = false;
            _gameOver.Show(_run.Score.Score, SaveSystem.HighScore, isNewBest,
                           _run.Score.TotalLinesCleared, _run.Score.BestCombo, _run.Score.TotalPiecesPlaced);
        }

        // --- persistence --------------------------------------------------------------------

        private void SaveNow()
        {
            if (_run == null || _run.IsGameOver) return;
            SaveSystem.SaveRun(_run.Snapshot());
        }

        /// <summary>
        /// Android does not reliably call OnApplicationQuit when it kills a backgrounded app, so
        /// pause is the only dependable moment to persist. Saving here is what makes "a run in
        /// progress survives the app being killed" actually true rather than true-in-the-editor.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }
    }
}
