using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The daily challenge, from `daily_challenge.png`: the streak, the week strip with each day's
    /// reward, today's puzzle with its board and objective, PLAY TODAY, and the week's progress to
    /// Sunday's chest.
    /// </summary>
    public sealed class DailyScreen : MonoBehaviour
    {
        private static class Layout
        {
            public const float TitleY = 228f;
            public const float SubtitleY = 334f;
            public const float StreakY = 440f;
            public const float WeekY = 692f;
            public static readonly Vector2 Week = new Vector2(990f, 380f);
            public const float PuzzleY = 1262f;
            public const float ProgressY = 1790f;
            public static readonly Vector2 Puzzle = new Vector2(930f, 600f);
            public const float ProgressFromBottom = 196f;
            public const float DayStep = 136f;
        }

        public event Action PlayRequested;
        public event Action BackRequested;

        private RectTransform _root;
        private Text _title;
        private Text _subtitle;
        private Text _streak;
        private RectTransform _streakPill;

        private readonly Text[] _dayNames = new Text[7];
        private readonly Image[] _dayDiscs = new Image[7];
        private readonly Image[] _daySymbols = new Image[7];
        private readonly Text[] _dayToday = new Text[7];
        private readonly Image[] _links = new Image[6];
        private readonly Image[] _rewardIcons = new Image[7];
        private readonly Text[] _rewardAmounts = new Text[7];

        private RectTransform _week;
        private RectTransform _puzzle;
        private RectTransform _miniGrid;
        private Image[] _miniCells;
        private Text _objective;
        private Text _moves;
        private Image _dayReward;
        private Text _dayRewardText;
        private Button _play;
        private Text _playCaption;
        private Image _done;

        private Text _progressText;
        private readonly Image[] _segments = new Image[7];
        private RectTransform _chest;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("Daily", parent);
            Vector2 top = W.Top;

            Button back = W.Round("Back", _root, "circle_pink", "sym_back", top, new Vector2(-420f, -100f), 124f, 0.5f);
            back.onClick.AddListener(() => BackRequested?.Invoke());
            CoinPill.Create(_root, top, new Vector2(0f, -76f), 300f, 88f);
            ToolboxButton.Create(_root, top, new Vector2(436f, -96f), 114f);

            _title = W.Title("Title", _root, "DAILY", top, new Vector2(0f, -Layout.TitleY), 150, CandyStyle.White);
            _subtitle = W.Title("Subtitle", _root, "CHALLENGE", top, new Vector2(0f, -Layout.SubtitleY), 132, CandyStyle.Cyan);

            Image streak = W.Sliced("Streak", _root, "pill_blue", top, new Vector2(0f, -Layout.StreakY), new Vector2(440f, 86f));
            _streakPill = streak.rectTransform;
            W.Img("Flame", streak.transform, "sym_flame", W.Left, new Vector2(62f, 4f), new Vector2(62f, 70f));
            _streak = W.Text("Text", streak.transform, "", W.Centre, new Vector2(30f, 2f), new Vector2(340f, 70f),
                             42, CandyStyle.OnBlue, Color.white);
            _streak.font = Design.Display;

            BuildWeek(top);
            BuildPuzzle(top);
            BuildProgress();

            _root.gameObject.SetActive(false);
        }

        private void BuildWeek(Vector2 top)
        {
            Image card = W.Card("Week", _root, top, new Vector2(0f, -Layout.WeekY), Layout.Week, 44f);
            _week = card.rectTransform;
            string[] names = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };

            for (int i = 0; i < 6; i++)
                _links[i] = W.Rounded("Link" + i, _week, CandyText.Hex(0x35C96B), CandyText.Hex(0x2BA85A), W.Centre,
                                      new Vector2((i - 2.5f) * Layout.DayStep, 40f), new Vector2(Layout.DayStep, 16f), 8f);

            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * Layout.DayStep;
                _dayNames[i] = W.Text("Name" + i, _week, names[i], W.Centre, new Vector2(x, 118f), new Vector2(130f, 44f),
                                      32, CandyStyle.Blue, Color.white);
                _dayNames[i].font = Design.Display;

                _dayDiscs[i] = W.Img("Disc" + i, _week, "circle_green", W.Centre, new Vector2(x, 40f), new Vector2(112f, 112f));
                _daySymbols[i] = W.Img("Sym", _dayDiscs[i].transform, "sym_check", W.Centre, Vector2.zero, new Vector2(64f, 64f));
                _dayToday[i] = W.Text("Today", _dayDiscs[i].transform, "TODAY", W.Centre, new Vector2(0f, 2f),
                                      new Vector2(120f, 40f), 26, CandyStyle.OnPink, Color.white);
                _dayToday[i].font = Design.Display;

                Image box = W.Rounded("Box" + i, _week, CandyText.Hex(0xFFF2E2), CandyText.Hex(0xF2D9C0), W.Centre,
                                      new Vector2(x, -94f), new Vector2(118f, 126f), 24f);
                bool sunday = i == 6;
                _rewardIcons[i] = W.Img("Icon", box.transform, "coin", W.Centre, new Vector2(0f, sunday ? 6f : 18f),
                                        sunday ? new Vector2(130f, 130f) : new Vector2(70f, 70f));
                _rewardAmounts[i] = W.Text("Amount", box.transform, "", W.Centre, new Vector2(0f, -38f), new Vector2(118f, 44f),
                                           36, CandyStyle.Cocoa, Color.white);
                _rewardAmounts[i].font = Design.Display;
            }
        }

        private void BuildPuzzle(Vector2 top)
        {
            Image card = W.Card("Puzzle", _root, top, new Vector2(0f, -Layout.PuzzleY), Layout.Puzzle, 46f);
            _puzzle = card.rectTransform;

            Image header = W.Sliced("Header", _puzzle, "pill_red", W.Top, new Vector2(0f, 6f), new Vector2(780f, 124f));
            W.Text("Text", header.transform, "TODAY'S PUZZLE", W.Centre, new Vector2(0f, 4f), new Vector2(760f, 110f),
                   68, CandyStyle.OnPink, Color.white).font = Design.Display;
            W.Sprinkles(header.rectTransform, new Vector2(780f, 124f));

            Image board = W.Rounded("Board", _puzzle, CandyText.Hex(0x1C2C74), CandyText.Hex(0x3A5AD0), W.Centre,
                                    new Vector2(-218f, -10f), new Vector2(370f, 370f), 26f);
            _miniGrid = board.rectTransform;
            _miniCells = new Image[Board.CellCount];
            const float cell = 40f;
            for (int r = 0; r < Board.Height; r++)
                for (int c = 0; c < Board.Width; c++)
                {
                    Image img = UIKit.Image("c", _miniGrid, ArtKit.EmptyCell(), Palette.EmptyCellTint);
                    img.raycastTarget = false;
                    CandyUI.Place(img, W.Centre, new Vector2((c - 3.5f) * (cell + 3f), (3.5f - r) * (cell + 3f)),
                                  new Vector2(cell, cell));
                    _miniCells[Bits.Index(c, r)] = img;
                }

            _objective = W.Text("Objective", _puzzle, "", W.Centre, new Vector2(200f, 150f), new Vector2(440f, 70f),
                                54, CandyStyle.Cocoa, Color.white);
            _objective.font = Design.Display;

            Image movesPill = W.Rounded("MovesPill", _puzzle, CandyText.Hex(0xFBE2CE), CandyText.Hex(0xF1CBB0), W.Centre,
                                        new Vector2(200f, 72f), new Vector2(330f, 70f));
            _moves = W.Text("Moves", movesPill.transform, "", W.Centre, new Vector2(0f, 2f), new Vector2(330f, 64f),
                            44, CandyStyle.Cocoa, Color.white);
            _moves.font = Design.Display;

            Image rewards = W.Rounded("Rewards", _puzzle, CandyText.Hex(0xFFF4E6), CandyText.Hex(0xF2D9C0), W.Centre,
                                      new Vector2(200f, -76f), new Vector2(360f, 180f), 30f);
            W.Text("Caption", rewards.transform, "REWARDS", W.Top, new Vector2(0f, -26f), new Vector2(300f, 36f),
                   28, CandyStyle.Cocoa, Color.white);
            W.Img("Coin", rewards.transform, "coin", W.Centre, new Vector2(-126f, -20f), new Vector2(72f, 72f));
            W.Text("CoinAmount", rewards.transform, $"+{Daily.CompletionCoins}", W.Centre, new Vector2(-40f, -20f),
                   new Vector2(110f, 60f), 42, CandyStyle.Cocoa, Color.white).font = Design.Display;
            _dayReward = W.Img("Day", rewards.transform, "icon_hammer", W.Centre, new Vector2(66f, -20f), new Vector2(72f, 72f));
            _dayRewardText = W.Text("DayAmount", rewards.transform, "+1", W.Centre, new Vector2(140f, -20f),
                                    new Vector2(90f, 60f), 42, CandyStyle.Cocoa, Color.white);
            _dayRewardText.font = Design.Display;

            _play = W.Pill("Play", _puzzle, "pill_red", "PLAY TODAY", W.Bottom, new Vector2(0f, -20f),
                           new Vector2(760f, 164f), 88, CandyStyle.OnPink, sprinkles: true, shine: true, pulse: 0.025f);
            _playCaption = W.Caption(_play);
            _countdown = W.Text("Countdown", _play.transform, "", W.Centre, new Vector2(0f, -46f),
                                new Vector2(720f, 46f), 36, CandyStyle.OnBlue, Color.white);
            _play.onClick.AddListener(OnPlay);

            _done = W.Img("Done", _puzzle, "badge_check", W.Centre, new Vector2(-70f, 150f), new Vector2(110f, 110f));
        }

        private void BuildProgress()
        {
            // Right under the puzzle, as the reference has it — but never lower than the bottom margin
            // on a short screen.
            float canvasHeight = Design.CanvasWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float fromTop = Mathf.Min(Layout.ProgressY, canvasHeight - Layout.ProgressFromBottom);
            Image panel = W.Sliced("Progress", _root, "tile_purple", W.Top,
                                   new Vector2(0f, -fromTop), new Vector2(940f, 176f));
            _progressText = W.Text("Text", panel.transform, "", W.Left, new Vector2(250f, 38f), new Vector2(420f, 60f),
                                   46, CandyStyle.OnPurple, Color.white, TextAnchor.MiddleLeft);
            _progressText.font = Design.Display;

            const float seg = 96f;
            for (int i = 0; i < 7; i++)
                _segments[i] = W.Rounded("Seg" + i, panel.transform, CandyText.Hex(0x3AD86E), CandyText.Hex(0x23A24F),
                                         W.Left, new Vector2(84f + i * (seg + 6f), -34f), new Vector2(seg, 52f), 22f);

            Image chest = W.Img("Chest", panel.transform, "reward_chest", W.Right, new Vector2(-92f, 6f), new Vector2(160f, 170f));
            _chest = chest.rectTransform;
            chest.gameObject.AddComponent<CandyPress>().Wiggle = 5f;
            chest.gameObject.AddComponent<Glint>().Every = 1.2f;
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);

            Tween.PopIn(_title.transform, 0f, 0.5f, 0.3f);
            Tween.PopIn(_subtitle.transform, 0.08f, 0.5f, 0.3f);
            Tween.PopIn(_streakPill, 0.16f, 0.45f, 0.2f);
            Tween.SlideIn(_week, new Vector2(-1100f, 0f), 0.1f, 0.55f);
            Tween.SlideIn(_puzzle, new Vector2(1100f, 0f), 0.18f, 0.55f);
            for (int i = 0; i < 7; i++) Tween.PopIn(_dayDiscs[i].transform, 0.4f + i * 0.05f, 0.4f, 0f);
            Sound.Swoosh();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            int today = DailyProgress.Today;
            int weekStart = Daily.WeekStart(today);
            int todayIndex = today - weekStart;
            int streak = DailyProgress.Streak();

            _streak.text = streak > 0 ? $"{streak} DAY STREAK" : "START A STREAK";

            for (int i = 0; i < 7; i++)
            {
                int day = weekStart + i;
                bool done = DailyProgress.IsDone(day);
                bool isToday = i == todayIndex;
                bool future = i > todayIndex;

                CandyText nameCandy = _dayNames[i].GetComponent<CandyText>();
                nameCandy.Set(isToday ? CandyStyle.Pink : CandyStyle.Blue);

                string disc = done ? "circle_green" : isToday ? "circle_pink" : future ? (i == 6 ? "circle_gold" : "circle_purple") : "circle_blue";
                _dayDiscs[i].sprite = ArtKit.Ui(disc);
                _dayDiscs[i].color = !done && !isToday && !future ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                _dayDiscs[i].rectTransform.sizeDelta = isToday ? new Vector2(136f, 136f) : new Vector2(108f, 108f);

                _daySymbols[i].gameObject.SetActive(done || future);
                _daySymbols[i].sprite = ArtKit.Ui(done ? "sym_check" : i == 6 ? "sym_lock_gold" : "sym_lock_blue");
                _dayToday[i].gameObject.SetActive(isToday && !done);

                CandyPress press = W.Ensure<CandyPress>(_dayDiscs[i]);
                press.Pulse = isToday && !done ? 0.06f : 0f;
                press.Click = false;

                if (i < 6)
                {
                    bool linked = done && DailyProgress.IsDone(day + 1);
                    _links[i].color = linked ? Color.white : new Color(0.72f, 0.7f, 0.95f, 1f);
                }

                Daily.Reward reward = Daily.RewardFor(i);
                switch (reward.Kind)
                {
                    case Daily.RewardKind.Coins:
                        _rewardIcons[i].sprite = ArtKit.Ui("coin");
                        _rewardAmounts[i].text = reward.Amount.ToString();
                        break;
                    case Daily.RewardKind.Tool:
                        _rewardIcons[i].sprite = ArtKit.Ui(ToolIcon(reward.Tool));
                        _rewardAmounts[i].text = reward.Amount.ToString();
                        break;
                    default:
                        _rewardIcons[i].sprite = ArtKit.Ui("reward_chest");
                        _rewardAmounts[i].text = "";
                        break;
                }
            }

            LevelDef puzzle = Daily.ForDay(today);
            for (int idx = 0; idx < Board.CellCount; idx++)
            {
                bool filled = (puzzle.StartOccupied & (1UL << idx)) != 0UL;
                _miniCells[idx].sprite = filled ? ArtKit.Block(puzzle.StartColours[idx]) : ArtKit.EmptyCell();
                _miniCells[idx].color = filled ? Color.white : Palette.EmptyCellTint;
            }

            _objective.text = $"CLEAR {puzzle.LineTarget} LINES";
            _moves.text = $"{puzzle.MoveBudget} MOVES";

            Daily.Reward todays = Daily.RewardFor(todayIndex);
            _dayReward.sprite = ArtKit.Ui(todays.Kind == Daily.RewardKind.Tool ? ToolIcon(todays.Tool)
                                          : todays.Kind == Daily.RewardKind.Chest ? "reward_chest" : "reward_coins");
            _dayRewardText.text = todays.Kind == Daily.RewardKind.Tool ? "+1" : "+" + todays.Amount;

            bool todayDone = DailyProgress.TodayDone;
            SetLocked(todayDone);
            _done.gameObject.SetActive(todayDone);
            _objective.rectTransform.anchoredPosition = new Vector2(todayDone ? 240f : 200f, 150f);

            int complete = DailyProgress.DoneThisWeek();
            _progressText.text = $"{complete} / 7 COMPLETE";
            for (int i = 0; i < 7; i++)
                _segments[i].color = DailyProgress.IsDone(weekStart + i) ? Color.white : new Color(0.62f, 0.62f, 0.78f, 1f);
        }

        // --- locking once done ------------------------------------------------------------------

        private Text _countdown;
        private bool _locked;
        private int _lockedDay;
        private float _nextTick;

        /// <summary>
        /// One puzzle a day, and once it is done it is done: the button greys out, says COMPLETED, and
        /// counts down to the next puzzle. It unlocks by itself at midnight, even with the screen open.
        /// </summary>
        private void SetLocked(bool locked)
        {
            _locked = locked;
            _lockedDay = DailyProgress.Today;

            _playCaption.text = locked ? "COMPLETED!" : "PLAY TODAY";
            _playCaption.fontSize = locked ? 72 : 88;
            _playCaption.rectTransform.anchoredPosition = new Vector2(0f, locked ? 22f : 5f);
            _countdown.gameObject.SetActive(locked);

            _play.GetComponent<Image>().color = locked ? new Color(0.7f, 0.7f, 0.78f, 1f) : Color.white;
            if (_play.TryGetComponent(out CandyPress press)) press.Pulse = locked ? 0f : 0.025f;
            if (_play.TryGetComponent(out ShineSweep shine)) shine.enabled = !locked;

            _nextTick = 0f;
            UpdateCountdown();
        }

        private void OnPlay()
        {
            if (_locked)
            {
                Sound.Deny();
                Tween.Shake((RectTransform)_play.transform, 16f, 0.4f);
                Fx.Instance?.Text(_play.transform.position, "COME BACK TOMORROW!", CandyStyle.White, 54f, 1.3f, 200f);
                return;
            }

            Fx.Instance?.Sparkles(_play.transform.position, 12, 260f, 80f);
            PlayRequested?.Invoke();
        }

        private void Update()
        {
            if (!_locked || !IsVisible || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.5f;

            // Midnight passed while the screen was open: a new puzzle is out.
            if (DailyProgress.Today != _lockedDay)
            {
                Refresh();
                if (!_locked) Tween.PopIn(_play.transform, 0f, 0.5f, 0.6f);
                return;
            }

            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            if (!_locked) return;
            TimeSpan left = DateTime.Today.AddDays(1) - DateTime.Now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            _countdown.text = left.TotalHours >= 1
                ? $"NEXT PUZZLE IN {(int)left.TotalHours}h {left.Minutes:00}m"
                : $"NEXT PUZZLE IN {left.Minutes}m {left.Seconds:00}s";
        }

        public static string ToolIcon(Tool tool) => tool switch
        {
            Tool.Undo => "icon_undo",
            Tool.Shuffle => "icon_shuffle",
            _ => "icon_hammer",
        };
    }
}
