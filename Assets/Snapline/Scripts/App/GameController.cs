using System;
using System.Collections;
using UnityEngine;
using Snapline.Art;
using Snapline.Core;
using Snapline.UI;
using Snapline.View;

namespace Snapline.App
{
    /// <summary>
    /// Joins the rules engine to everything the player can see and hear.
    ///
    /// All the decisions live in Snapline.Core; this reacts to them — how loud a clear is, which card
    /// comes up when a run ends, what a tool costs when you hold none. It owns no rules of its own.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private GameRun _run;
        private BoardView _board;
        private TrayView _tray;
        private DragController _drag;
        private Hud _hud;
        private ToolsBar _tools;
        private AdController _ads;

        private PausePopup _pause;
        private NoMovesPopup _noMoves;
        private GreatRunPopup _greatRun;
        private LevelEndPopup _levelEnd;
        private NewBlockPopup _newBlock;

        /// <summary>Special blocks in this level the player has not met yet, explained one at a time before play.</summary>
        private readonly System.Collections.Generic.Queue<Special> _intros = new System.Collections.Generic.Queue<Special>();

        private bool _busy;
        private bool _hammerArmed;

        /// <summary>
        /// True while a result card is closing and a possible interstitial is running, so a second tap
        /// on the card during its close cannot start a second level or a second ad.
        /// </summary>
        private bool _leaving;
        private LevelDef _level;
        private bool _daily;

        public GameRun Run => _run;
        public bool IsBusy => _busy;
        public bool IsDaily => _daily;

        public PausePopup PausePopup => _pause;
        public NoMovesPopup NoMovesPopup => _noMoves;
        public GreatRunPopup GreatRunPopup => _greatRun;
        public LevelEndPopup LevelEndPopup => _levelEnd;

        public event Action MenuRequested;
        public event Action LevelsRequested;
        public event Action DailyRequested;
        public event Action ScoresRequested;

        /// <summary>Raised when a run starts in a mode, so the screen can lay the board out for it.</summary>
        public event Action<GameMode> LayoutRequested;

        public static bool HasSavedRun => SaveSystem.HasSavedRun();

        public void Init(BoardView board, TrayView tray, DragController drag, Hud hud, ToolsBar tools,
                         AdController ads, PausePopup pause, NoMovesPopup noMoves, GreatRunPopup greatRun,
                         LevelEndPopup levelEnd, NewBlockPopup newBlock)
        {
            _newBlock = newBlock;
            _newBlock.Done += NextIntro;
            _board = board;
            _tray = tray;
            _drag = drag;
            _hud = hud;
            _tools = tools;
            _ads = ads;
            _pause = pause;
            _noMoves = noMoves;
            _greatRun = greatRun;
            _levelEnd = levelEnd;

            _run = new GameRun(DealerConfig.Default(), new ScoreRules());

            _drag.CanPlace = (slot, col, row) => _run.CanPlace(slot, col, row);
            _drag.ShapeInSlot = slot => _run.Tray[slot].IsEmpty ? null : _run.Tray[slot].Shape;
            _drag.PlacementRequested += OnPlacementRequested;
            _drag.PlacementRejected += OnPlacementRejected;

            _hud.PauseRequested += OpenPause;
            _hud.NewBestReached += OnNewBest;
            _tools.ToolPressed += OnTool;

            _pause.ResumeRequested += Resume;
            _pause.RestartRequested += Restart;
            _pause.LevelsRequested += () => Leave(() => (_daily ? DailyRequested : LevelsRequested)?.Invoke());
            _pause.HomeRequested += () => Leave(() => MenuRequested?.Invoke());

            _noMoves.ShuffleChosen += () => StartCoroutine(Rescue(Route.Shuffle));
            _noMoves.WatchChosen += () => StartCoroutine(Rescue(Route.Watch));
            _noMoves.CoinsChosen += () => StartCoroutine(Rescue(Route.Coins));
            _noMoves.EndChosen += () =>
            {
                if (_busy) return;
                _noMoves.Close(() => StartCoroutine(FinishEndless()));
            };

            // The interstitial runs as the player leaves the results card, never on top of it.
            _greatRun.PlayAgainRequested += () => StartCoroutine(LeaveCard(_greatRun, StartNewRun));
            _greatRun.HomeRequested += () => StartCoroutine(LeaveCard(_greatRun, () => MenuRequested?.Invoke()));
            _greatRun.ScoresRequested += () => StartCoroutine(LeaveCard(_greatRun, () => ScoresRequested?.Invoke()));
            _greatRun.ShareRequested += ShareScore;

            // Between levels, the same way: the interstitial runs as the player leaves the result
            // card, on the shared pacing, never over the stars.
            _levelEnd.NextRequested += () => StartCoroutine(LeaveCard(_levelEnd, () => StartLevel(_run.LevelNumber + 1)));
            _levelEnd.RetryRequested += () => StartCoroutine(LeaveCard(_levelEnd, Restart));
            _levelEnd.LevelsRequested += () => StartCoroutine(LeaveCard(_levelEnd, () => (_daily ? DailyRequested : LevelsRequested)?.Invoke()));
            _levelEnd.HomeRequested += () => StartCoroutine(LeaveCard(_levelEnd, () => MenuRequested?.Invoke()));
        }

        /// <summary>Dismisses every card and puts the tools away, for a screen change.</summary>
        public void HideOverlays()
        {
            _pause.HideNow();
            _noMoves.HideNow();
            _greatRun.HideNow();
            _levelEnd.HideNow();
            _newBlock.HideNow();
            _intros.Clear();
            Disarm();
        }

        // --- starting runs ---------------------------------------------------------------------

        public void StartNewRun()
        {
            HideOverlays();
            SaveSystem.ClearRun();
            _level = null;
            _daily = false;

            _run.StartNew(NewSeed());
            LayoutRequested?.Invoke(GameMode.Endless);

            _board.SyncFromBoard(_run.Board);
            _tray.Refresh(_run.Tray, animate: true);
            _hud.SetMode(GameMode.Endless);
            _hud.ResetForNewRun(SaveSystem.HighScore);
            AfterReset();
            SaveNow();
        }

        /// <summary>Begin a level. Clamped to the ladder, so "next level" past the end is harmless.</summary>
        public void StartLevel(int number)
        {
            _daily = false;
            Begin(Levels.Get(Mathf.Clamp(number, 1, Levels.Count)));
        }

        /// <summary>Today's daily challenge.</summary>
        public void StartDaily()
        {
            _daily = true;
            Begin(Daily.ForDay(DailyProgress.Today));
        }

        private void Begin(LevelDef level)
        {
            HideOverlays();
            _level = level;

            _run.StartLevel(level);
            LayoutRequested?.Invoke(GameMode.Level);

            _board.SyncFromBoard(_run.Board, cascade: level.StartOccupied != 0UL);
            _tray.Refresh(_run.Tray, animate: true);
            _hud.SetMode(GameMode.Level);
            _hud.ResetForNewRun(SaveSystem.HighScore);
            PushObjective();
            AfterReset();

            foreach (Special kind in new[] { Special.Stone, Special.Gift, Special.Bomb })
                if (level.Has(kind) && !NewBlockPopup.HasSeen(kind)) _intros.Enqueue(kind);

            if (_intros.Count > 0)
            {
                // Held until the board has cascaded in, so the card explains blocks the player can see.
                _drag.InputEnabled = false;
                Tween.Delay(0.7f, NextIntro);
            }
        }

        /// <summary>Shows the next unexplained block, or hands the board back once there are none left.</summary>
        private void NextIntro()
        {
            if (_hud == null || !_hud.Root.gameObject.activeInHierarchy) return;

            if (_intros.Count > 0)
            {
                _newBlock.Show(_intros.Dequeue());
                return;
            }

            if (!_run.IsGameOver && !_pause.IsVisible && !_hammerArmed && !_busy) _drag.InputEnabled = true;
        }

        /// <summary>Closes the block explainer as if GOT IT were tapped. The smoke harness uses it.</summary>
        public void DismissIntroForHarness()
        {
            if (_newBlock.IsVisible) _newBlock.Dismiss();
        }

        /// <summary>Fires the big-clear celebration for a number of lines. The smoke harness uses it.</summary>
        public void DebugCelebrate(int lines)
        {
            Fx.Instance?.Celebrate(_board.CentreWorld, lines);
            Sound.BigPlay(lines >= 5 ? 3 : lines == 4 ? 2 : 1);
        }

        private void AfterReset()
        {
            _tools.Refresh();
            RefreshSlotPlayability();
            _drag.InputEnabled = true;
            _busy = false;
        }

        /// <summary>Pick the saved endless run back up exactly where it was left.</summary>
        public bool ResumeSavedRun()
        {
            RunSnapshot saved = SaveSystem.LoadRun();
            if (saved == null) return false;

            HideOverlays();
            _level = null;
            _daily = false;
            _run.StartNew(NewSeed());
            _run.Restore(saved);
            LayoutRequested?.Invoke(GameMode.Endless);

            _board.SyncFromBoard(_run.Board, cascade: true);
            _tray.Refresh(_run.Tray, animate: true);
            _hud.SetMode(GameMode.Endless);
            _hud.ResetForNewRun(SaveSystem.HighScore);
            _hud.SetScoreImmediate(_run.Score.Score);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));
            AfterReset();

            if (_run.IsGameOver)
            {
                _drag.InputEnabled = false;
                StartCoroutine(FinishEndless());
            }

            return true;
        }

        private string LevelName => _daily ? "DAILY" : $"LEVEL {_run.LevelNumber}";

        private void PushObjective()
        {
            if (_run.Objective == null) return;
            _hud.SetObjective(LevelName, _run.Score.TotalLinesCleared, _run.Objective.LineTarget,
                              _run.MovesRemaining, _level);
        }

        private double ComboMultiplier(int combo) =>
            Math.Min(1.0 + _run.Rules.ComboStep * Math.Max(0, combo - 1), _run.Rules.MaxComboMultiplier);

        private static ulong NewSeed()
        {
            ulong a = (ulong)DateTime.UtcNow.Ticks;
            ulong b = (ulong)UnityEngine.Random.Range(int.MinValue, int.MaxValue) & 0xFFFFFFFFUL;
            return a ^ (b << 21) ^ 0x9E3779B97F4A7C15UL;
        }

        public void ShareScore()
        {
            long score = _run != null && _run.Score.Score > 0 ? _run.Score.Score : SaveSystem.HighScore;
            GameKit.Share.TextWithLink(ShareMessage(score));
        }

        public static string ShareMessage(long score) =>
            score > 0
                ? $"I scored {Hud.Format(score)} in Snapline! Think you can beat that?"
                : "I'm playing Snapline — see if you can beat my score!";

        // --- moves -----------------------------------------------------------------------------

        /// <summary>Play a move without the drag input. The harnesses use it to exercise the real path.</summary>
        public void PlaceProgrammatically(int slot, int col, int row) => OnPlacementRequested(slot, col, row);

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

            int lines = move.Placement.LinesCleared;
            int combo = move.Score.ComboCount;

            _board.AnimatePlacement(move.Placement.PieceMask, move.Colour, move.Placement.ClearedMask);
            _tray.Piece(move.TraySlot).Clear();
            Sound.Place();

            Fx fx = Fx.Instance;

            if (lines > 0)
            {
                ShapeDef shape = Shapes.Get(move.ShapeId);
                int originCol = Mathf.Clamp(move.Col + shape.Width / 2, 0, Board.Width - 1);
                int originRow = Mathf.Clamp(move.Row + shape.Height / 2, 0, Board.Height - 1);

                _board.AnimateClear(move.Placement, originCol, originRow);
                fx?.Shake(ShakeFor(lines, combo));
                Sound.Clear(lines, combo);
                Sound.Combo(combo);

                if (move.Placement.BombMask != 0UL)
                {
                    Sound.Blast();
                    Haptics.Heavy();
                    fx?.Shake(0.75f);
                }
                if (move.Placement.CrackedMask != 0UL) Sound.Crack();
                if (move.Placement.GiftsCollected > 0) Sound.Gift();
                if (lines >= 2 || combo >= 3) Haptics.Heavy();
                else Haptics.Medium();

                ShowClearShouts(move, originCol, originRow);

                if (move.PerfectClear)
                {
                    Sound.Perfect();
                    fx?.ScreenFlash(0.5f);
                    fx?.Confetti(90);
                }
            }
            else
            {
                Haptics.Light();
                fx?.Shake(0.05f);
            }

            _hud.SetScore(_run.Score.Score);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));

            if (move.TrayRefilled)
            {
                yield return new WaitForSeconds(lines > 0 ? 0.22f : 0.08f);
                _tray.Refresh(_run.Tray, animate: true);
            }

            PushObjective();
            RefreshSlotPlayability();
            SaveNow();

            _busy = false;

            if (!move.GameOver) yield break;

            _drag.InputEnabled = false;

            if (_run.Mode == GameMode.Level)
            {
                yield return ResolveLevelEnd(move);
                yield break;
            }

            // Out of room. A shudder first, so the card arriving reads as a consequence.
            Sound.Invalid();
            fx?.Shake(0.35f);
            yield return new WaitForSeconds(0.6f);

            bool offered = false;
            if (_run.RevivesUsed < AdController.MaxRevivesPerRun)
            {
                // Guarded: if the card fails to open, the run must still end rather than leave the
                // player on a dead board with input off and nothing to tap.
                try
                {
                    _noMoves.Show(Wallet.Count(Tool.Shuffle), _ads != null && _ads.CanOfferRevive(_run));
                    offered = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _noMoves.HideNow();
                }
            }

            if (offered) yield break;
            yield return FinishEndless();
        }

        private static float ShakeFor(int lines, int combo)
        {
            float amount = 0.22f + 0.15f * (lines - 1) + 0.05f * Mathf.Max(0, combo - 1);
            return Mathf.Min(0.85f, amount);
        }

        /// <summary>
        /// What the player is told about a clear, over where it happened: the size of the clear, the
        /// points, and the streak. Three shouts at most — more than that is noise nobody reads.
        /// </summary>
        private void ShowClearShouts(in MoveResult move, int originCol, int originRow)
        {
            Fx fx = Fx.Instance;
            if (fx == null) return;

            int lines = move.Placement.LinesCleared;
            int combo = move.Score.ComboCount;
            Vector2 local = _board.CellToLocal(originCol, originRow);
            Vector3 At(float dy) => _board.Grid.TransformPoint(local + new Vector2(0f, dy));

            string headline = lines switch
            {
                1 => combo >= 3 ? PraiseFor(combo) : "NICE!",
                2 => "DOUBLE!",
                3 => "TRIPLE!",
                4 => "QUAD!",
                _ => "INCREDIBLE!",
            };

            // Three lines or more is a moment, not a shout: BOOM! HUGE PLAY!, MEGA!, UNNATURAL!!!
            bool celebrating = lines >= 3;
            if (celebrating)
            {
                fx.Celebrate(_board.CentreWorld, lines);
                Sound.BigPlay(lines >= 5 ? 3 : lines == 4 ? 2 : 1);
            }
            else
            {
                CandyStyle style = lines == 2 ? CandyStyle.Cyan : CandyStyle.White;
                fx.Text(At(40f), headline, style, 96f + 12f * Mathf.Min(lines, 4), 1.05f, 200f);
            }

            fx.Text(At(-70f), $"+{Hud.Format(move.Score.Total)}", CandyStyle.White, 64f, 0.95f, 160f, 0.08f);

            if (combo >= 2 && !celebrating)
                fx.Text(At(150f), $"COMBO x{combo}", CandyStyle.Gold, 70f + 4f * Mathf.Min(combo, 8), 1.1f, 180f, 0.14f);

            if (move.PerfectClear)
                fx.Text(_board.CentreWorld, "PERFECT!", CandyStyle.Gold, 150f, 1.6f, 120f, 0.25f);
        }

        private static string PraiseFor(int combo)
        {
            if (combo >= 10) return "LEGENDARY!";
            if (combo >= 8) return "UNSTOPPABLE!";
            if (combo >= 6) return "ON FIRE!";
            if (combo >= 4) return "AMAZING!";
            return "GREAT!";
        }

        private void OnPlacementRejected(int slot)
        {
            if (slot < 0 || slot >= _tray.SlotCount) return;
            Sound.Invalid();
            StartCoroutine(_tray.ReturnToSlot(slot));
        }

        private void RefreshSlotPlayability()
        {
            for (int i = 0; i < _tray.SlotCount && i < _run.Tray.Length; i++)
            {
                if (_run.Tray[i].IsEmpty) continue;
                _tray.SetSlotPlayable(i, Board.CanPlaceAnywhere(_run.Board.Occupied, _run.Tray[i].Shape));
            }
        }

        private void OnNewBest()
        {
            Fx fx = Fx.Instance;
            if (fx == null) return;
            fx.Text(_hud.ScoreTarget.position + new Vector3(0f, -1f, 0f) * 0f, "NEW BEST!", CandyStyle.Gold, 100f, 1.6f, -140f);
            fx.Confetti(60);
            Sound.NewBest();
            Haptics.Heavy();
        }

        // --- level end -------------------------------------------------------------------------

        private IEnumerator ResolveLevelEnd(MoveResult move)
        {
            int lines = _run.Score.TotalLinesCleared;

            // A finished level or daily, won or lost, counts toward the interstitial pacing exactly as
            // a finished endless run does, so the first-ad grace and the gap between ads span both.
            _ads?.RecordGameFinished();

            if (move.LevelComplete)
            {
                Sound.Win();
                Fx.Instance?.Confetti(70);
                Fx.Instance?.Text(_board.CentreWorld, "COMPLETE!", CandyStyle.Gold, 130f, 1.2f, 100f, 0.1f);
                Haptics.Heavy();

                // The winning clear should be seen before the card covers it.
                yield return new WaitForSeconds(1.0f);

                int stars = _level.StarsFor(_run.MovesRemaining);

                if (_daily)
                {
                    CompleteDaily(stars, lines);
                    yield break;
                }

                int number = _run.LevelNumber;
                int previous = SaveSystem.StarsForLevel(number);
                SaveSystem.RecordLevelResult(number, stars);
                SaveSystem.SubmitScore(_run.Score.Score);
                Telemetry.LevelCompleted();

                int coins = Economy.LevelReward(previous, stars);
                int before = Wallet.Coins;
                CoinPill.HoldRoll(3f);
                Wallet.Grant(coins);

                _levelEnd.ShowComplete(number, stars, lines, _run.MovesUsed, previous > 0 && stars > previous,
                                       coins, before, number < Levels.Count);
                yield break;
            }

            Sound.GameOver();
            yield return new WaitForSeconds(0.4f);
            yield return _board.PlayGameOverSweep();
            yield return new WaitForSeconds(0.25f);

            _levelEnd.ShowFailed(LevelName, lines, _run.Objective.LineTarget, _daily);
        }

        /// <summary>
        /// Pays out a finished daily: the completion coins plus that weekday's reward — but only the
        /// first time today. A replay is still a win, and says so, but pays nothing.
        /// </summary>
        private void CompleteDaily(int stars, int lines)
        {
            int today = DailyProgress.Today;
            bool first = DailyProgress.MarkDone(today);
            Daily.Reward reward = Daily.RewardFor(Daily.WeekdayOf(today));

            int before = Wallet.Coins;
            int coins = 0;

            if (first)
            {
                coins = Daily.CompletionCoins;
                switch (reward.Kind)
                {
                    case Daily.RewardKind.Coins:
                        coins += reward.Amount;
                        break;
                    case Daily.RewardKind.Tool:
                        Wallet.GrantTool(reward.Tool, reward.Amount);
                        break;
                    case Daily.RewardKind.Chest:
                        coins += reward.Amount;
                        Wallet.GrantTool(Tool.Undo);
                        Wallet.GrantTool(Tool.Shuffle);
                        Wallet.GrantTool(Tool.Hammer);
                        break;
                }

                CoinPill.HoldRoll(3f);
                Wallet.Grant(coins);
            }

            _levelEnd.ShowDaily(stars, lines, _run.MovesUsed, first, coins, before, reward, DailyProgress.Streak());
        }

        // --- the endless finish ----------------------------------------------------------------

        private enum Route { Shuffle, Watch, Coins }

        /// <summary>Trigger the video rescue as if its button were tapped. Used by the smoke harness.</summary>
        public void RequestRevive() => StartCoroutine(Rescue(Route.Watch));

        /// <summary>
        /// Rescue a dead board by one of the three routes on the NO MORE MOVES card. Whatever the
        /// route, the rescue is the same, and there is one per run: space is cleared and a fresh tray
        /// dealt. A route that fails — no shuffle held, not enough coins, an ad that did not finish —
        /// costs nothing and leaves the card up.
        /// </summary>
        private IEnumerator Rescue(Route route)
        {
            if (_busy || !_run.IsGameOver || _run.Mode != GameMode.Endless) yield break;
            _busy = true;

            switch (route)
            {
                case Route.Shuffle:
                    if (!Wallet.TryUseTool(Tool.Shuffle))
                    {
                        _noMoves.Refuse(NoMovesPopup.Choice.Shuffle);
                        _busy = false;
                        yield break;
                    }
                    break;

                case Route.Coins:
                    if (!Wallet.TrySpend(Economy.ContinuePrice))
                    {
                        _noMoves.Refuse(NoMovesPopup.Choice.Coins);
                        _busy = false;
                        yield break;
                    }
                    Sound.Purchase();
                    break;

                case Route.Watch:
                    if (_ads == null)
                    {
                        _busy = false;
                        yield break;
                    }

                    _noMoves.SetWatchBusy(true);
                    System.Threading.Tasks.Task<bool> watching = _ads.ShowRewardedAsync();
                    while (!watching.IsCompleted) yield return null;
                    _noMoves.SetWatchBusy(false);

                    if (!watching.Result)
                    {
                        // Do not offer it again this run; a second failure reads as a broken button.
                        _noMoves.SetWatchAvailable(false);
                        _busy = false;
                        yield break;
                    }

                    Telemetry.RewardedAdWatched();
                    break;
            }

            bool closed = false;
            _noMoves.Close(() => closed = true);
            while (!closed) yield return null;

            ulong cleared = _run.Revive(AdController.ReviveRowsCleared);
            if (cleared == 0UL)
            {
                _busy = false;
                yield return FinishEndless();
                yield break;
            }

            var rescue = new PlaceResult { ClearedMask = cleared };
            for (int r = 0; r < Board.Height; r++)
                if ((cleared & Bits.RowMask[r]) != 0UL) rescue.ClearedRowFlags |= 1 << r;

            _board.AnimateClear(rescue, Board.Width / 2, Board.Height / 2);
            Fx.Instance?.Shake(0.6f);
            Fx.Instance?.Text(_board.CentreWorld, "SAVED!", CandyStyle.Gold, 140f, 1.3f, 120f);
            Fx.Instance?.Confetti(50);
            Sound.Clear(3, 1);
            Sound.Prize();
            Haptics.Heavy();

            yield return new WaitForSeconds(0.5f);

            _board.SyncFromBoard(_run.Board);
            _tray.Refresh(_run.Tray, animate: true);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));
            RefreshSlotPlayability();
            _tools.Refresh();

            _drag.InputEnabled = true;
            _busy = false;
            SaveNow();
        }

        private IEnumerator FinishEndless()
        {
            _busy = true;
            Sound.GameOver();
            yield return _board.PlayGameOverSweep();
            yield return new WaitForSeconds(0.3f);

            long score = _run.Score.Score;
            long previousBest = SaveSystem.HighScore;
            bool newBest = score > previousBest;
            if (newBest) Telemetry.NewHighScore(score);

            int lines = _run.Score.TotalLinesCleared;
            int bestCombo = _run.Score.BestCombo;
            SaveSystem.RecordFinishedRun(score, lines, bestCombo);
            SaveSystem.ClearRun();

            int coins = Economy.EndlessReward(lines, bestCombo);
            int before = Wallet.Coins;
            CoinPill.HoldRoll(3.5f);
            Wallet.Grant(coins);

            // Counted before the card appears, so the pacing sees it. The interstitial itself waits
            // until the player leaves the card.
            _ads?.RecordGameFinished();

            _busy = false;
            try
            {
                _greatRun.Show(score, previousBest, newBest, lines, bestCombo, coins, before, RankOf(score));
            }
            catch (Exception e)
            {
                // Everything is already saved and paid; a card that will not open must not strand the player.
                Debug.LogException(e);
                _greatRun.HideNow();
                MenuRequested?.Invoke();
            }
        }

        private static int RankOf(long score)
        {
            var table = SaveSystem.BestScores();
            for (int i = 0; i < table.Count; i++)
                if (table[i].Score == score) return i + 1;
            return 0;
        }

        /// <summary>
        /// Close the results card, run the paced interstitial, then do what the player asked. The
        /// action always happens; gating navigation on an ad strands players when a network hangs.
        /// </summary>
        private IEnumerator LeaveCard(CandyPopup card, Action then)
        {
            if (_leaving) yield break;
            _leaving = true;

            bool closed = false;
            card.Close(() => closed = true);
            while (!closed) yield return null;

            if (_ads != null)
            {
                System.Threading.Tasks.Task showing = _ads.MaybeShowInterstitialAsync();
                while (!showing.IsCompleted) yield return null;
            }

            _leaving = false;
            then?.Invoke();
        }

        // --- pause -----------------------------------------------------------------------------

        public void OpenPause()
        {
            if (_busy || _run.IsGameOver || _pause.IsVisible || _newBlock.IsVisible) return;

            Disarm();
            _drag.InputEnabled = false;

            string info = _run.Mode == GameMode.Level
                ? $"{Mathf.Min(_run.Score.TotalLinesCleared, _run.Objective.LineTarget)} / {_run.Objective.LineTarget} LINES   •   {_run.MovesRemaining} MOVES LEFT"
                : $"SCORE {Hud.Format(_run.Score.Score)}   •   BEST {Hud.Format(Math.Max(SaveSystem.HighScore, _run.Score.Score))}";

            string title = _run.Mode == GameMode.Level ? LevelName : "ENDLESS";
            _pause.ShowInGame(title, info, _daily ? "DAILY" : "LEVELS");
        }

        private void Resume()
        {
            _pause.Close(() => _drag.InputEnabled = !_run.IsGameOver && !_hammerArmed);
        }

        private void Restart()
        {
            _pause.HideNow();
            _levelEnd.HideNow();

            if (_run.Mode == GameMode.Endless) StartNewRun();
            else if (_daily) StartDaily();
            else StartLevel(_run.LevelNumber);
        }

        private void Leave(Action then)
        {
            SaveNow();
            _pause.Close(then);
        }

        // --- tools -----------------------------------------------------------------------------

        private void OnTool(Tool tool)
        {
            if (_busy || _run.IsGameOver || _pause.IsVisible || _newBlock.IsVisible) return;

            if (_hammerArmed)
            {
                Disarm();
                if (tool == Tool.Hammer)
                {
                    Sound.Close();
                    return;
                }
            }

            RectTransform button = _tools.ButtonRect(tool);

            if (tool == Tool.Undo && !_run.CanUndo)
            {
                Refuse(button, "NOTHING TO UNDO");
                return;
            }

            if (Wallet.Count(tool) <= 0 && !Buy(tool, button)) return;

            switch (tool)
            {
                case Tool.Undo:
                    UseUndo();
                    break;
                case Tool.Shuffle:
                    StartCoroutine(UseShuffle());
                    break;
                case Tool.Hammer:
                    Arm();
                    break;
            }
        }

        /// <summary>Buys a tool on the spot when none is held, rather than leaving the run for a store.</summary>
        private bool Buy(Tool tool, RectTransform button)
        {
            int price = Economy.Price(tool);
            if (!Wallet.TrySpend(price))
            {
                Refuse(button, $"NEED {price} COINS");
                CoinPill pill = CoinPill.Visible();
                if (pill != null) Tween.Shake((RectTransform)pill.transform, 14f, 0.4f);
                return false;
            }

            Wallet.GrantTool(tool);
            Sound.Purchase();
            Fx.Instance?.Text(button.position, $"-{price}", CandyStyle.Gold, 60f, 0.9f, 200f);
            return true;
        }

        private static void Refuse(RectTransform button, string why)
        {
            Sound.Deny();
            Tween.Shake(button, 16f, 0.4f);
            Fx.Instance?.Text(button.position, why, CandyStyle.White, 46f, 1.2f, 260f);
        }

        private void UseUndo()
        {
            if (!Wallet.TryUseTool(Tool.Undo) || !_run.Undo()) return;

            _board.SyncFromBoard(_run.Board);
            _board.Shimmer();
            _tray.Refresh(_run.Tray, animate: true);
            _hud.SetScoreImmediate(_run.Score.Score);
            _hud.SetCombo(_run.Score.ComboCount, ComboMultiplier(_run.Score.ComboCount));
            PushObjective();
            RefreshSlotPlayability();

            Sound.Rewind();
            Haptics.Medium();
            Fx.Instance?.Text(_board.CentreWorld, "UNDO!", CandyStyle.Cyan, 110f, 0.9f, 120f);
            SaveNow();
        }

        private IEnumerator UseShuffle()
        {
            if (!Wallet.TryUseTool(Tool.Shuffle)) yield break;

            _busy = true;
            Sound.Whoosh();
            Haptics.Medium();

            yield return _tray.SpinOut(() =>
            {
                _run.ShuffleTray();
                _tray.Refresh(_run.Tray, animate: true);
            });

            Fx.Instance?.Text(_tools.ButtonRect(Tool.Shuffle).position, "SHUFFLE!", CandyStyle.Cyan, 80f, 0.9f, 260f);
            RefreshSlotPlayability();
            SaveNow();
            _busy = false;
        }

        private void Arm()
        {
            _hammerArmed = true;
            _drag.InputEnabled = false;
            _board.SetHammerMode(true, OnHammerCell);
            _tools.SetArmed(Tool.Hammer);
            Fx.Instance?.Text(_board.CentreWorld, "TAP A BLOCK", CandyStyle.White, 80f, 1.5f, 40f);
        }

        private void Disarm()
        {
            if (!_hammerArmed) return;
            _hammerArmed = false;
            _board.SetHammerMode(false, null);
            _tools.SetArmed(null);
            if (!_run.IsGameOver && !_pause.IsVisible) _drag.InputEnabled = true;
        }

        private void OnHammerCell(int col, int row)
        {
            if (_busy) return;
            if (!_run.Board.IsOccupied(col, row))
            {
                Sound.Deny();
                return;
            }

            StartCoroutine(UseHammer(col, row));
        }

        private IEnumerator UseHammer(int col, int row)
        {
            if (!Wallet.TryUseTool(Tool.Hammer)) yield break;

            _busy = true;
            _run.Hammer(col, row);
            _hammerArmed = false;
            _board.SetHammerMode(false, null);
            _tools.SetArmed(null);

            yield return _board.Smash(col, row);

            RefreshSlotPlayability();
            PushObjective();
            SaveNow();
            _busy = false;
            _drag.InputEnabled = !_run.IsGameOver;
        }

        // --- persistence -----------------------------------------------------------------------

        private void SaveNow()
        {
            if (_run == null || _run.IsGameOver) return;

            // Only the endless run is resumable. Saving a level would overwrite the endless board the
            // menu's CONTINUE offers, losing a long run because someone dipped into level 3.
            if (_run.Mode != GameMode.Endless) return;

            SaveSystem.SaveRun(_run.Snapshot());
        }

        /// <summary>
        /// Android does not reliably call OnApplicationQuit when it kills a backgrounded app, so pause
        /// is the only dependable moment to persist. A run left mid-move comes back paused, too.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveNow();
                if (_hud != null && _hud.Root.gameObject.activeInHierarchy && !_run.IsGameOver && !_busy) OpenPause();
            }
            else
            {
                Telemetry.ResumeSession();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();
    }
}
