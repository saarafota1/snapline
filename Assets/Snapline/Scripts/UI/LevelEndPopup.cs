using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The end of a level or a daily, from `popup_level_complete.png`: the level, three stars that
    /// land one at a time on a turning starburst, the result, the coins and the balance moving, and
    /// NEXT LEVEL / REPLAY / LEVELS.
    ///
    /// The same card says OUT OF MOVES when the level is lost — grey stars, how close it came, and a
    /// big RETRY — and DAILY COMPLETE with the day's reward and the streak.
    /// </summary>
    public sealed class LevelEndPopup : CandyPopup
    {
        private static class Layout
        {
            public static readonly Vector2 Card = new Vector2(880f, 1160f);
            public const float CardY = -30f;
            public const float NameY = 206f;
            public const float StarsY = 380f;
            public const float StarSplit = 250f;
            public const float ResultY = 528f;
            public const float BadgeY = 604f;
            public const float CoinsY = 698f;
            public const float NextY = 860f;
            public const float SecondaryY = 1020f;
        }

        public event Action NextRequested;
        public event Action RetryRequested;
        public event Action LevelsRequested;
        public event Action HomeRequested;

        private Text _name;
        private readonly Image[] _stars = new Image[3];
        private Image _burst;
        private Text _result;
        private Image _badge;
        private Text _badgeText;
        private Image _coinsRow;
        private Text _gain;
        private Image _gainIcon;
        private Text _from;
        private Text _to;
        private Button _next;
        private Text _nextCaption;
        private Button _replay;
        private Button _levels;
        private Text _levelsCaption;

        private enum Mode { Complete, Failed, Daily }
        private Mode _mode;

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "LevelEnd", "LEVEL COMPLETE!", Layout.Card, Layout.CardY, true, 820f, 80);

            _name = W.Text("Name", Card, "LEVEL 1", W.Top, new Vector2(0f, -Layout.NameY), new Vector2(760f, 100f),
                           76, CandyStyle.Blue, Color.white);
            _name.font = Design.Display;

            _burst = W.Starburst(Card, W.Top, new Vector2(0f, -Layout.StarsY), 520f);
            for (int i = 0; i < 3; i++)
            {
                bool middle = i == 1;
                float size = middle ? 236f : 196f;
                float y = -Layout.StarsY + (middle ? 12f : -26f);
                _stars[i] = W.Img("Star" + i, Card, "reward_star_gold", W.Top,
                                  new Vector2((i - 1) * Layout.StarSplit, y), new Vector2(size, size));
                _stars[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, (1 - i) * 10f);
            }

            _result = W.Text("Result", Card, "", W.Top, new Vector2(0f, -Layout.ResultY), new Vector2(800f, 70f),
                             50, CandyStyle.Blue, Color.white);
            _result.font = Design.Display;

            _badge = W.Sliced("Badge", Card, "pill_gold", W.Top, new Vector2(0f, -Layout.BadgeY), new Vector2(300f, 60f));
            _badgeText = W.Text("Text", _badge.transform, "NEW BEST!", W.Centre, new Vector2(0f, 1f), new Vector2(300f, 56f),
                                34, CandyStyle.Cocoa, Color.white);
            _badgeText.font = Design.Display;
            _badge.gameObject.AddComponent<Glint>().Every = 1.1f;

            _coinsRow = W.Rounded("Coins", Card, CandyText.Hex(0xFAE6D4), CandyText.Hex(0xF0D0B6), W.Top,
                                  new Vector2(0f, -Layout.CoinsY), new Vector2(700f, 110f), 40f);
            _gainIcon = W.Img("Coin", _coinsRow.transform, "coin", W.Left, new Vector2(80f, 0f), new Vector2(96f, 96f));
            _gain = W.Text("Gain", _coinsRow.transform, "+50", W.Left, new Vector2(230f, 2f), new Vector2(200f, 90f),
                           64, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleLeft);
            _gain.font = Design.Display;
            W.Rounded("Rule", _coinsRow.transform, CandyText.Hex(0xE3C3AE), CandyText.Hex(0xE3C3AE), W.Centre,
                      new Vector2(-10f, 0f), new Vector2(4f, 70f), 2f);
            _from = W.Text("From", _coinsRow.transform, "0", W.Right, new Vector2(-320f, 2f), new Vector2(160f, 70f),
                           42, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleRight);
            W.Text("Arrow", _coinsRow.transform, "→", W.Right, new Vector2(-208f, 2f), new Vector2(40f, 70f),
                   36, CandyStyle.Cocoa, Color.white);
            _to = W.Text("To", _coinsRow.transform, "0", W.Right, new Vector2(-84f, 2f), new Vector2(160f, 70f),
                         48, CandyStyle.OnGreen, Color.white, TextAnchor.MiddleLeft);
            _to.font = Design.Display;

            _next = W.Pill("Next", Card, "pill_red", "NEXT LEVEL  ›", W.Top, new Vector2(0f, -Layout.NextY),
                           new Vector2(760f, 160f), 80, CandyStyle.OnPink, sprinkles: true, shine: true, pulse: 0.022f);
            _nextCaption = W.Caption(_next);
            _next.onClick.AddListener(OnNext);

            _replay = W.Pill("Replay", Card, "pill_white", "REPLAY", W.Top, new Vector2(-194f, -Layout.SecondaryY),
                             new Vector2(370f, 124f), 54, CandyStyle.OnBlue, "sym_restart", 76f, tint: W.CandyBlue);
            _replay.onClick.AddListener(() => RetryRequested?.Invoke());

            _levels = W.Pill("Levels", Card, "pill_purple", "LEVELS", W.Top, new Vector2(194f, -Layout.SecondaryY),
                             new Vector2(370f, 124f), 54, CandyStyle.OnPurple, "icon_levels", 74f);
            _levelsCaption = W.Caption(_levels);
            _levels.onClick.AddListener(() => LevelsRequested?.Invoke());
        }

        private void OnNext()
        {
            switch (_mode)
            {
                case Mode.Complete: NextRequested?.Invoke(); break;
                case Mode.Failed: RetryRequested?.Invoke(); break;
                default: LevelsRequested?.Invoke(); break;
            }
        }

        public void ShowComplete(int level, int stars, int lines, int moves, bool newBest, int coins, int before, bool hasNext)
        {
            _mode = Mode.Complete;
            SetTitle("LEVEL COMPLETE!");
            _name.text = $"LEVEL {level}";
            _result.text = $"{lines} LINES IN {moves} MOVES";
            _badgeText.text = "NEW BEST!";
            _badge.gameObject.SetActive(newBest);
            SetCoins(coins, before, "coin");
            _nextCaption.text = hasNext ? "NEXT LEVEL  ›" : "ALL LEVELS DONE!";
            _next.interactable = hasNext;
            _replay.gameObject.SetActive(true);
            _levelsCaption.text = "LEVELS";
            Layout2(true);

            Present();
            StartCoroutine(Stars(stars, coins, before));
        }

        public void ShowFailed(string name, int lines, int target, bool daily)
        {
            _mode = Mode.Failed;
            SetTitle("OUT OF MOVES");
            _name.text = name;
            _result.text = $"{Mathf.Min(lines, target)} / {target} LINES";
            _badgeText.text = target - lines == 1 ? "SO CLOSE!" : "TRY AGAIN!";
            _badge.gameObject.SetActive(true);
            _coinsRow.gameObject.SetActive(false);
            _nextCaption.text = "RETRY";
            _next.interactable = true;
            _replay.gameObject.SetActive(false);
            _levelsCaption.text = daily ? "DAILY" : "LEVELS";
            Layout2(false);

            for (int i = 0; i < 3; i++)
            {
                _stars[i].sprite = ArtKit.Ui("reward_star_silver");
                _stars[i].color = new Color(1f, 1f, 1f, 0.8f);
                _stars[i].transform.localScale = Vector3.one;
            }
            _burst.gameObject.SetActive(false);

            Present();
            Sound.GameOver();
        }

        public void ShowDaily(int stars, int lines, int moves, bool firstToday, int coins, int before,
                              Daily.Reward reward, int streak)
        {
            _mode = Mode.Daily;
            SetTitle("DAILY COMPLETE!");
            _name.text = streak > 1 ? $"{streak} DAY STREAK!" : "TODAY'S PUZZLE";
            _result.text = $"{lines} LINES IN {moves} MOVES";
            _badgeText.text = firstToday ? RewardText(reward) : "ALREADY CLAIMED";
            _badge.gameObject.SetActive(true);
            _nextCaption.text = "CONTINUE  ›";
            _next.interactable = true;
            _replay.gameObject.SetActive(false);
            _levelsCaption.text = "DAILY";
            Layout2(false);

            if (firstToday) SetCoins(coins, before, "coin");
            else _coinsRow.gameObject.SetActive(false);

            Present();
            StartCoroutine(Stars(stars, firstToday ? coins : 0, before));
        }

        private static string RewardText(Daily.Reward reward) => reward.Kind switch
        {
            Daily.RewardKind.Tool => $"+1 {reward.Tool.ToString().ToUpperInvariant()}",
            Daily.RewardKind.Chest => "CHEST OPENED!",
            _ => "BONUS COINS!",
        };

        /// <summary>With REPLAY the lower row has two buttons; without it, LEVELS sits centred.</summary>
        private void Layout2(bool twoButtons)
        {
            ((RectTransform)_levels.transform).anchoredPosition =
                new Vector2(twoButtons ? 194f : 0f, -Layout.SecondaryY);
        }

        private void SetCoins(int coins, int before, string icon)
        {
            _coinsRow.gameObject.SetActive(coins > 0);
            _gain.text = "+" + coins;
            _gainIcon.sprite = ArtKit.Ui(icon);
            _from.text = Hud.Format(before);
            _to.text = Hud.Format(before);
        }

        private IEnumerator Stars(int stars, int coins, int before)
        {
            _burst.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                _stars[i].sprite = ArtKit.Ui("reward_star_silver");
                _stars[i].color = new Color(1f, 1f, 1f, 0.45f);
                _stars[i].transform.localScale = Vector3.one * 0.8f;
            }

            yield return new WaitForSecondsRealtime(0.45f);

            // Left, right, then the big one in the middle — the order that saves the best for last.
            int[] order = { 0, 2, 1 };
            for (int n = 0; n < stars; n++)
            {
                Image star = _stars[order[n]];
                star.sprite = ArtKit.Ui("reward_star_gold");
                star.color = Color.white;
                Tween.PopIn(star.transform, 0f, 0.5f, 2.2f);
                Sound.Star(n + 1);
                Haptics.Medium();
                yield return new WaitForSecondsRealtime(0.16f);
                Fx.Instance?.Sparkles(star.transform.position, 10, 110f, 76f);
                Fx.Instance?.Ring(star.transform.position, new Color(1f, 0.9f, 0.4f, 0.9f), 360f, 0.45f);
                if (order[n] == 1)
                {
                    _burst.gameObject.SetActive(true);
                    Tween.PopIn(_burst.transform, 0f, 0.6f, 0f);
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }

            if (stars == 3)
            {
                Fx.Instance?.Confetti(90);
                Sound.Prize();
            }

            if (coins <= 0 || !_coinsRow.gameObject.activeSelf) yield break;

            yield return new WaitForSecondsRealtime(0.2f);
            int landed = 0;
            int flying = Mathf.Clamp(coins / 10, 4, 12);
            Fx.Instance?.CoinFly(_gainIcon.transform.position, _to.rectTransform, flying, 64f, () =>
            {
                landed++;
                _to.text = Hud.Format(before + Mathf.RoundToInt(coins * landed / (float)flying));
            });
            Sound.Coins(coins);
        }

        protected override void OnCloseButton() => LevelsRequested?.Invoke();
    }
}
