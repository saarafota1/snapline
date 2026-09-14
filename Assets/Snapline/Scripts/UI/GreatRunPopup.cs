using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The endless result, from `popup_great_run.png`: the score counting up in its pill, NEW BEST
    /// with the old record beneath, three stat tiles, the coins paid and the balance moving, PLAY
    /// AGAIN / HOME / SHARE, and where the run ranks among the player's best.
    ///
    /// Played as a sequence rather than shown all at once — the score climbs, the record lands with
    /// confetti, the tiles drop in, the coins fly to the balance — because a results card is the
    /// payoff for the whole run, and a still one wastes it.
    /// </summary>
    public sealed class GreatRunPopup : CandyPopup
    {
        private static class Layout
        {
            public static readonly Vector2 Card = new Vector2(920f, 1360f);
            public const float CardY = -10f;
            public const float ScoreY = 250f;
            public const float ScoreCaptionY = 342f;
            public const float NewBestY = 418f;
            public const float PreviousY = 494f;
            public const float TilesY = 650f;
            public const float TileSplit = 262f;
            public static readonly Vector2 Tile = new Vector2(240f, 222f);
            public const float BalanceY = 826f;
            public const float AgainY = 960f;
            public const float SecondaryY = 1106f;
            public const float RankY = 1250f;
        }

        public event Action PlayAgainRequested;
        public event Action HomeRequested;
        public event Action ShareRequested;
        public event Action ScoresRequested;

        private Text _score;
        private RectTransform _scorePill;
        private Image _newBest;
        private Text _previous;
        private Image _previousPill;
        private readonly RectTransform[] _tiles = new RectTransform[3];
        private Text _lines;
        private Text _combo;
        private Text _coins;
        private Text _from;
        private Text _to;
        private RectTransform _balance;
        private Text _rank;
        private Image _rankRow;
        private Button _again;
        private Image[] _ribbonStars;

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "GreatRun", "GREAT RUN!", Layout.Card, Layout.CardY, true, 740f, 92);

            _ribbonStars = new[]
            {
                W.Img("StarL", Ribbon, "reward_star_gold", W.Left, new Vector2(10f, 20f), new Vector2(150f, 150f)),
                W.Img("StarR", Ribbon, "reward_star_gold", W.Right, new Vector2(-10f, 20f), new Vector2(150f, 150f)),
            };
            _ribbonStars[0].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _ribbonStars[1].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            Image pill = W.Sliced("ScorePill", Card, "pill_blue", W.Top, new Vector2(0f, -Layout.ScoreY), new Vector2(540f, 146f));
            _scorePill = pill.rectTransform;
            _score = W.Text("Score", pill.transform, "0", W.Centre, new Vector2(0f, 6f), new Vector2(540f, 140f),
                            104, CandyStyle.OnBlue, Color.white);
            _score.font = Design.Display;
            _score.gameObject.AddComponent<CandyShine>();

            W.Text("ScoreCaption", Card, "SCORE", W.Top, new Vector2(0f, -Layout.ScoreCaptionY), new Vector2(300f, 50f),
                   40, CandyStyle.Blue, Color.white).font = Design.Display;

            _newBest = W.Sliced("NewBest", Card, "pill_gold", W.Top, new Vector2(0f, -Layout.NewBestY), new Vector2(460f, 92f));
            W.Img("Crown", _newBest.transform, "reward_crown", W.Left, new Vector2(78f, 4f), new Vector2(88f, 72f));
            W.Text("Text", _newBest.transform, "NEW BEST!", W.Centre, new Vector2(40f, 2f), new Vector2(360f, 80f),
                   54, CandyStyle.Cocoa, Color.white).font = Design.Display;
            _newBest.gameObject.AddComponent<Glint>().Every = 0.9f;

            _previousPill = W.Rounded("PreviousPill", Card, CandyText.Hex(0xFAE6D4), CandyText.Hex(0xF0D0B6), W.Top,
                                      new Vector2(0f, -Layout.PreviousY), new Vector2(430f, 58f));
            _previous = W.Text("Previous", _previousPill.transform, "", W.Centre, new Vector2(0f, 1f), new Vector2(430f, 52f),
                               30, CandyStyle.Cocoa, Color.white);

            _lines = Tile(0, "tile_green", "icon_levels", "LINES", CandyStyle.OnGreen);
            _combo = Tile(1, "tile_purple", "sym_bolt", "BEST COMBO", CandyStyle.OnPurple);
            _coins = Tile(2, "tile_yellow", "coin", "COINS", CandyStyle.OnGold);

            Image balance = W.Rounded("Balance", Card, CandyText.Hex(0xFAE6D4), CandyText.Hex(0xF0D0B6), W.Top,
                                      new Vector2(0f, -Layout.BalanceY), new Vector2(520f, 86f));
            _balance = balance.rectTransform;
            W.Img("Coin", balance.transform, "coin", W.Left, new Vector2(56f, 0f), new Vector2(66f, 66f));
            _from = W.Text("From", balance.transform, "0", W.Centre, new Vector2(-50f, 2f), new Vector2(160f, 70f),
                           44, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleRight);
            W.Text("Arrow", balance.transform, "→", W.Centre, new Vector2(62f, 2f), new Vector2(50f, 70f),
                   40, CandyStyle.Cocoa, Color.white);
            _to = W.Text("To", balance.transform, "0", W.Centre, new Vector2(176f, 2f), new Vector2(160f, 70f),
                         48, CandyStyle.OnGreen, Color.white, TextAnchor.MiddleLeft);
            _to.font = Design.Display;

            _again = W.Pill("PlayAgain", Card, "pill_red", "PLAY AGAIN", W.Top, new Vector2(0f, -Layout.AgainY),
                            new Vector2(740f, 150f), 80, CandyStyle.OnPink, "icon_play", 86f, sprinkles: true,
                            shine: true, pulse: 0.02f);
            _again.onClick.AddListener(() => PlayAgainRequested?.Invoke());

            Button home = W.Pill("Home", Card, "pill_white", "HOME", W.Top, new Vector2(-190f, -Layout.SecondaryY),
                                 new Vector2(360f, 112f), 54, CandyStyle.OnBlue, "icon_home", 70f, tint: W.CandyBlue);
            home.onClick.AddListener(() => HomeRequested?.Invoke());

            Button share = W.Pill("Share", Card, "pill_white", "SHARE", W.Top, new Vector2(190f, -Layout.SecondaryY),
                                  new Vector2(360f, 112f), 54, CandyStyle.OnBlue, "icon_share", 70f, tint: W.CandyBlue);
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            _rankRow = W.Rounded("Rank", Card, CandyText.Hex(0xFAE6D4), CandyText.Hex(0xF0D0B6), W.Top,
                                 new Vector2(0f, -Layout.RankY), new Vector2(740f, 106f), 36f);
            W.Img("Trophy", _rankRow.transform, "reward_trophy", W.Left, new Vector2(64f, 0f), new Vector2(72f, 72f));
            _rank = W.Text("Text", _rankRow.transform, "#1  0", W.Left, new Vector2(250f, 2f), new Vector2(280f, 70f),
                           46, CandyStyle.Cocoa, Color.white, TextAnchor.MiddleLeft);
            _rank.font = Design.Display;
            Button scores = W.Pill("Scores", _rankRow.transform, "pill_blue", "BEST SCORES", W.Right,
                                   new Vector2(-160f, 0f), new Vector2(290f, 82f), 34, CandyStyle.OnBlue);
            scores.onClick.AddListener(() => ScoresRequested?.Invoke());
        }

        private Text Tile(int index, string tile, string icon, string caption, CandyStyle style)
        {
            Image img = W.Sliced("Tile" + index, Card, tile, W.Top,
                                 new Vector2((index - 1) * Layout.TileSplit, -Layout.TilesY), Layout.Tile);
            _tiles[index] = img.rectTransform;
            W.Img("Icon", img.transform, icon, W.Top, new Vector2(0f, -58f), new Vector2(80f, 80f));
            Text value = W.Text("Value", img.transform, "0", W.Centre, new Vector2(0f, -22f), new Vector2(230f, 80f),
                                66, style, Color.white);
            value.font = Design.Display;
            W.Text("Caption", img.transform, caption, W.Bottom, new Vector2(0f, 30f), new Vector2(230f, 40f),
                   index == 1 ? 26 : 32, style, Color.white);
            return value;
        }

        public void Show(long score, long previousBest, bool newBest, int lines, int bestCombo, int coins,
                         int coinsBefore, int rank)
        {
            SetTitle(newBest ? "NEW RECORD!" : "GREAT RUN!");
            _score.text = "0";
            _newBest.gameObject.SetActive(newBest);
            _previousPill.gameObject.SetActive(previousBest > 0);
            _previous.text = newBest ? $"PREVIOUS BEST: {Hud.Format(previousBest)}" : $"BEST: {Hud.Format(previousBest)}";
            _lines.text = lines.ToString();
            _combo.text = "x" + Mathf.Max(1, bestCombo);
            _coins.text = "+" + coins;
            _from.text = Hud.Format(coinsBefore);
            _to.text = Hud.Format(coinsBefore);
            _rankRow.gameObject.SetActive(rank > 0);
            _rank.text = rank > 0 ? $"#{rank}   {Hud.Format(score)}" : "";

            Present();
            StartCoroutine(Sequence(score, newBest, coins, coinsBefore));
        }

        private IEnumerator Sequence(long score, bool newBest, int coins, int coinsBefore)
        {
            foreach (Image star in _ribbonStars) Tween.PopIn(star.transform, 0.35f, 0.5f, 0f);
            foreach (RectTransform tile in _tiles) tile.localScale = Vector3.zero;
            _newBest.transform.localScale = Vector3.zero;

            yield return new WaitForSecondsRealtime(0.35f);

            // The score counts up, ticking as it goes.
            const float count = 1.0f;
            float t = 0f;
            float nextTick = 0f;
            while (t < count)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutCubic(Mathf.Clamp01(t / count));
                _score.text = Hud.Format((long)(score * k));
                if (t > nextTick)
                {
                    Sound.Coin();
                    nextTick = t + 0.09f;
                }
                yield return null;
            }
            _score.text = Hud.Format(score);
            Tween.Punch(_scorePill, 0.15f, 0.35f);
            Fx.Instance?.Sparkles(_scorePill.position, 10, 260f, 80f);

            if (newBest)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                Tween.PopIn(_newBest.transform, 0f, 0.5f, 0f);
                Sound.NewBest();
                Fx.Instance?.Confetti(110);
                Haptics.Heavy();
            }

            for (int i = 0; i < _tiles.Length; i++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                Tween.PopIn(_tiles[i], 0f, 0.45f, 0f);
                Sound.Place();
            }

            yield return new WaitForSecondsRealtime(0.3f);

            if (coins > 0)
            {
                int landed = 0;
                int flying = Mathf.Clamp(coins / 15, 4, 14);
                Fx.Instance?.CoinFly(_tiles[2].position, _balance, flying, 72f, () =>
                {
                    landed++;
                    int shown = coinsBefore + Mathf.RoundToInt(coins * landed / (float)flying);
                    _to.text = Hud.Format(shown);
                });
                Sound.Coins(coins);
            }

            _again.transform.localScale = Vector3.one;
        }
    }
}
