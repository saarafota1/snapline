using System;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using Snapline.Core;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// Everything above the board: pause, the score, coins and the toolbox, and — per mode — the
    /// combo pill in endless, or the lines and moves pills and the star meter in a level.
    ///
    /// Laid out from `endless_game.png` and `level_game.png`, measured off the references and
    /// converted to canvas units (the references are 941 wide, the canvas 1080). Everything hangs
    /// from the top of the screen.
    ///
    /// The score rolls rather than snapping. A number that climbs is the cheapest reward signal
    /// there is, and it keeps the eye on the score after a big clear.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        /// <summary>Where everything sits, in canvas units measured down from the top.</summary>
        public static class Layout
        {
            public const float PauseX = -425f;
            public const float PauseY = 82f;
            public const float PauseSize = 124f;

            public const float CoinX = 352f;
            public const float CoinY = 78f;
            public const float CoinWidth = 290f;
            public const float CoinHeight = 88f;

            public const float ToolboxX = 426f;
            public const float ToolboxY = 200f;
            public const float ToolboxSize = 116f;

            // endless
            public const float ScoreY = 92f;
            public const float ScoreWidth = 380f;
            public const float ScoreHeight = 116f;
            public const float BestY = 178f;
            public const float ComboY = 246f;
            public const float ComboWidth = 470f;
            public const float ComboHeight = 80f;

            /// <summary>Bottom of the endless HUD. The board is placed below this.</summary>
            public const float EndlessBottom = 292f;

            // level
            public const float LevelPillY = 90f;
            public const float LevelPillWidth = 300f;
            public const float LevelPillHeight = 132f;
            public const float GoalY = 272f;
            public const float GoalWidth = 330f;
            public const float GoalHeight = 150f;
            public const float GoalSplit = 176f;
            public const float StarBarY = 400f;
            public const float StarBarWidth = 590f;
            public const float StarBarHeight = 46f;

            /// <summary>Bottom of the level HUD.</summary>
            public const float LevelBottom = 436f;
        }

        public event Action PauseRequested;

        /// <summary>Raised once per run, the moment the rolling score passes the previous best.</summary>
        public event Action NewBestReached;

        public RectTransform Root { get; private set; }

        private RectTransform _endless;
        private RectTransform _level;

        private Text _score;
        private Text _best;
        private RectTransform _scorePill;
        private RectTransform _combo;
        private Text _comboLeft;
        private Text _comboRight;
        private int _comboShown;

        private Text _levelName;
        private Text _levelScore;
        private RectTransform _levelPill;
        private RectTransform _linesPill;
        private Text _linesValue;
        private CandyBar _linesBar;
        private Text _movesValue;
        private CandyText _movesCandy;
        private CandyBar _starBar;
        private readonly Image[] _stars = new Image[3];
        private readonly bool[] _starLit = new bool[3];
        private readonly bool[] _starPossible = { true, true, true };
        private int _lastLines = -1;
        private int _lastMoves = -1;

        private GameMode _mode;
        private long _displayed;
        private long _target;
        private float _rollSpeed;
        private long _bestScore;
        private bool _beaten;

        public static float Bottom(GameMode mode) => mode == GameMode.Level ? Layout.LevelBottom : Layout.EndlessBottom;

        /// <summary>The pill the score is shown in, for points to fly at.</summary>
        public RectTransform ScoreTarget => _mode == GameMode.Level ? _levelPill : _scorePill;

        public void Init(RectTransform parent, long bestScore)
        {
            Root = UIKit.Rect("Hud", parent);
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(1f, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.offsetMin = new Vector2(0f, -Layout.LevelBottom);
            Root.offsetMax = Vector2.zero;

            _bestScore = bestScore;
            Vector2 top = W.Top;

            Button pause = CandyUI.SpriteButton("Pause", Root, ArtKit.Ui("circle_pink"));
            pause.GetComponent<Image>().preserveAspect = true;
            CandyUI.Place(pause, top, new Vector2(Layout.PauseX, -Layout.PauseY), new Vector2(Layout.PauseSize, Layout.PauseSize));
            Color bar = Color.white;
            Color rim = CandyText.Hex(0xFFC3DD);
            W.Rounded("BarL", pause.transform, bar, rim, W.Centre, new Vector2(-15f, 0f), new Vector2(24f, 58f), 12f);
            W.Rounded("BarR", pause.transform, bar, rim, W.Centre, new Vector2(15f, 0f), new Vector2(24f, 58f), 12f);
            pause.onClick.AddListener(() => PauseRequested?.Invoke());

            CoinPill.Create(Root, top, new Vector2(Layout.CoinX, -Layout.CoinY), Layout.CoinWidth, Layout.CoinHeight);
            ToolboxButton.Create(Root, top, new Vector2(Layout.ToolboxX, -Layout.ToolboxY), Layout.ToolboxSize);

            BuildEndless(top);
            BuildLevel(top);

            SetMode(GameMode.Endless);
        }

        private void BuildEndless(Vector2 top)
        {
            _endless = UIKit.Stretch("Endless", Root);

            Image pill = W.Sliced("ScorePill", _endless, "pill_blue", top, new Vector2(0f, -Layout.ScoreY),
                                  new Vector2(Layout.ScoreWidth, Layout.ScoreHeight));
            _scorePill = pill.rectTransform;
            _score = W.Text("Score", pill.transform, "0", W.Centre, new Vector2(0f, 4f),
                            new Vector2(Layout.ScoreWidth, Layout.ScoreHeight), 76, CandyStyle.OnBlue, Color.white);
            _score.font = Design.Display;

            _best = W.Text("Best", _endless, "BEST 0", top, new Vector2(0f, -Layout.BestY), new Vector2(520f, 50f),
                           36, CandyStyle.OnBlue, Color.white);

            Image combo = W.Sliced("Combo", _endless, "pill_red", top, new Vector2(0f, -Layout.ComboY),
                                   new Vector2(Layout.ComboWidth, Layout.ComboHeight));
            _combo = combo.rectTransform;
            _comboLeft = W.Text("Left", combo.transform, "COMBO x2  •", W.Centre, Vector2.zero, new Vector2(400f, 70f),
                                42, CandyStyle.OnPink, Color.white, TextAnchor.MiddleRight);
            _comboLeft.font = Design.Display;
            _comboRight = W.Text("Right", combo.transform, "1.5×", W.Centre, Vector2.zero, new Vector2(200f, 70f),
                                 42, CandyStyle.Gold, Color.white, TextAnchor.MiddleLeft);
            _comboRight.font = Design.Display;
            _combo.gameObject.SetActive(false);
        }

        private void BuildLevel(Vector2 top)
        {
            _level = UIKit.Stretch("Level", Root);

            Image pill = W.Sliced("LevelPill", _level, "pill_blue", top, new Vector2(0f, -Layout.LevelPillY),
                                  new Vector2(Layout.LevelPillWidth, Layout.LevelPillHeight));
            _levelPill = pill.rectTransform;
            _levelName = W.Text("Name", pill.transform, "LEVEL 1", W.Centre, new Vector2(0f, 34f), new Vector2(300f, 46f),
                                32, CandyStyle.OnBlue, Color.white);
            _levelScore = W.Text("Score", pill.transform, "0", W.Centre, new Vector2(0f, -14f), new Vector2(300f, 80f),
                                 64, CandyStyle.OnBlue, Color.white);
            _levelScore.font = Design.Display;

            Image lines = W.Sliced("Lines", _level, "tile_pink", top, new Vector2(-Layout.GoalSplit, -Layout.GoalY),
                                   new Vector2(Layout.GoalWidth, Layout.GoalHeight));
            _linesPill = lines.rectTransform;
            W.Img("Icon", lines.transform, "icon_levels", W.Left, new Vector2(64f, 14f), new Vector2(76f, 76f));
            W.Text("Caption", lines.transform, "LINES", W.Centre, new Vector2(44f, 40f), new Vector2(220f, 40f),
                   30, CandyStyle.OnPink, Color.white);
            _linesValue = W.Text("Value", lines.transform, "0 / 4", W.Centre, new Vector2(44f, -4f), new Vector2(230f, 64f),
                                 52, CandyStyle.OnPink, Color.white);
            _linesValue.font = Design.Display;
            _linesBar = CandyBar.Create(lines.transform, W.Bottom, new Vector2(0f, 28f),
                                        new Vector2(Layout.GoalWidth - 64f, 28f));

            Image moves = W.Sliced("Moves", _level, "tile_purple", top, new Vector2(Layout.GoalSplit, -Layout.GoalY),
                                   new Vector2(Layout.GoalWidth, Layout.GoalHeight));
            W.Img("Icon", moves.transform, "sym_restart", W.Left, new Vector2(70f, 0f), new Vector2(78f, 78f));
            W.Text("Caption", moves.transform, "MOVES", W.Centre, new Vector2(46f, 36f), new Vector2(220f, 40f),
                   30, CandyStyle.OnPurple, Color.white);
            _movesValue = W.Text("Value", moves.transform, "22", W.Centre, new Vector2(46f, -14f), new Vector2(220f, 80f),
                                 72, CandyStyle.OnPurple, Color.white);
            _movesValue.font = Design.Display;
            _movesCandy = _movesValue.GetComponent<CandyText>();

            _starBar = CandyBar.Create(_level, top, new Vector2(0f, -Layout.StarBarY),
                                       new Vector2(Layout.StarBarWidth, Layout.StarBarHeight));
            for (int i = 0; i < 3; i++)
            {
                float x = -Layout.StarBarWidth * 0.5f + Layout.StarBarWidth * (i + 1) / 3f - (i == 2 ? 34f : 0f);
                _stars[i] = W.Img("Star" + i, _level, "reward_star_gold", top, new Vector2(x, -Layout.StarBarY),
                                  new Vector2(84f, 84f));
            }
        }

        // --- mode ------------------------------------------------------------------------------

        public void SetMode(GameMode mode)
        {
            _mode = mode;
            _endless.gameObject.SetActive(mode == GameMode.Endless);
            _level.gameObject.SetActive(mode == GameMode.Level);
        }

        /// <summary>
        /// The level readout: lines against target, moves left, and the star meter.
        ///
        /// The meter is split into equal thirds, one per star, rather than drawn linearly in moves.
        /// Three stars survive until only a fifth of the budget is left, so a linear bar would sit
        /// nearly full for most of a level and then collapse through two stars in a few moves; in
        /// thirds, every star visibly drains before it is lost.
        /// </summary>
        public void SetObjective(string levelName, int lines, int target, int movesLeft, LevelDef level)
        {
            _levelName.text = levelName;

            int shown = Mathf.Min(lines, target);
            _linesValue.text = $"{shown} / {target}";
            _linesBar.Set(target <= 0 ? 0f : shown / (float)target);
            if (_lastLines >= 0 && shown > _lastLines)
            {
                Tween.Punch(_linesPill, 0.14f, 0.3f);
                if (Fx.Instance != null) Fx.Instance.Sparkles(_linesPill.position, 5, 120f, 60f);
            }
            _lastLines = shown;

            _movesValue.text = movesLeft.ToString();
            if (_lastMoves >= 0 && movesLeft != _lastMoves) Tween.Punch(_movesValue.transform, 0.22f, 0.25f);
            _movesCandy.Set(movesLeft <= 3 ? CandyStyle.Gold : CandyStyle.OnPurple);
            _lastMoves = movesLeft;

            if (level == null) return;

            // The bar fills with progress toward the goal, and each star lights as the bar reaches
            // it. The first version drained a bar of "stars still possible", which started every
            // level full — it read as three stars handed out before a single move.
            //
            // A star lights only while it can still be earned, so a star the move budget has already
            // ruled out stays silver even when the bar passes it. At the finish the bar is full and
            // the gold stars are exactly the ones awarded.
            float progress = target <= 0 ? 0f : shown / (float)target;
            _starBar.Set(progress, animate: true);

            int possible = level.StarsFor(movesLeft);

            for (int i = 0; i < 3; i++)
            {
                bool reached = progress >= (i + 1) / 3f - 0.001f;
                bool canEarn = possible >= i + 1;
                bool lit = reached && canEarn;

                _stars[i].sprite = ArtKit.Ui(lit ? "reward_star_gold" : "reward_star_silver");
                _stars[i].color = lit || !canEarn ? Color.white : new Color(1f, 1f, 1f, 0.55f);

                if (lit && !_starLit[i])
                {
                    Tween.PopIn(_stars[i].transform, 0f, 0.45f, 1.8f);
                    Sound.Star(i + 1);
                    if (Fx.Instance != null) Fx.Instance.Sparkles(_stars[i].rectTransform.position, 8, 60f, 56f);
                }

                if (!canEarn && _starPossible[i])
                {
                    Tween.Shake(_stars[i].rectTransform, 14f, 0.4f);
                    Sound.StarLost();
                    if (Fx.Instance != null) Fx.Instance.Sparkles(_stars[i].rectTransform.position, 6, 50f, 44f, new Color(0.85f, 0.88f, 1f));
                }

                _starLit[i] = lit;
                _starPossible[i] = canEarn;
            }
        }

        // --- score -----------------------------------------------------------------------------

        public void ResetForNewRun(long bestScore)
        {
            _bestScore = bestScore;
            _beaten = false;
            _displayed = 0;
            _target = 0;
            _score.text = "0";
            _levelScore.text = "0";
            _best.text = $"BEST {Format(bestScore)}";
            CandyText bestCandy = _best.GetComponent<CandyText>();
            if (bestCandy != null) bestCandy.Set(CandyStyle.OnBlue);
            for (int i = 0; i < 3; i++)
            {
                _starLit[i] = false;
                _starPossible[i] = true;
            }
            _lastLines = -1;
            _lastMoves = -1;
            _comboShown = 0;
            _combo.gameObject.SetActive(false);
            _starBar.Set(0f, animate: false);
        }

        public void SetScoreImmediate(long score)
        {
            _displayed = score;
            _target = score;
            _score.text = Format(score);
            _levelScore.text = Format(score);
            if (score > _bestScore) _beaten = true;
        }

        public void SetScore(long score)
        {
            _target = score;
            long delta = _target - _displayed;
            _rollSpeed = Mathf.Max(240f, Mathf.Abs(delta) / 0.55f);
            Tween.Punch(_mode == GameMode.Level ? _levelScore.transform : _score.transform, 0.16f, 0.22f);
        }

        /// <summary>The streak and what it is worth right now, e.g. COMBO x4 • 3.2×.</summary>
        public void SetCombo(int combo, double multiplier = 1.0)
        {
            bool show = combo >= 2 && _mode == GameMode.Endless;
            if (!show)
            {
                _combo.gameObject.SetActive(false);
                _comboShown = 0;
                return;
            }

            _comboLeft.text = $"COMBO x{combo}  •";
            _comboRight.text = $"{multiplier:0.0}×";

            float lw = _comboLeft.preferredWidth;
            float rw = _comboRight.preferredWidth;
            const float gap = 14f;
            float start = -(lw + gap + rw) * 0.5f;
            _comboLeft.rectTransform.anchoredPosition = new Vector2(start + lw - 200f, 2f);
            _comboRight.rectTransform.anchoredPosition = new Vector2(start + lw + gap + 100f, 2f);

            if (!_combo.gameObject.activeSelf)
            {
                _combo.gameObject.SetActive(true);
                Tween.PopIn(_combo, 0f, 0.4f, 0.4f);
            }
            else if (combo > _comboShown)
            {
                Tween.Punch(_combo, 0.18f, 0.3f);
            }

            if (combo > _comboShown && Fx.Instance != null)
                Fx.Instance.Sparkles(_combo.position, 4 + Mathf.Min(combo, 6), 180f, 60f);

            _comboShown = combo;
        }

        private void Update()
        {
            if (_displayed == _target) return;

            long step = (long)Mathf.Max(1f, _rollSpeed * Time.unscaledDeltaTime);
            long delta = _target - _displayed;
            if (Math.Abs(delta) <= step) _displayed = _target;
            else _displayed += delta > 0 ? step : -step;

            string text = Format(_displayed);
            _score.text = text;
            _levelScore.text = text;

            if (_mode != GameMode.Endless || _displayed <= _bestScore) return;

            _best.text = $"BEST {text}";
            if (_beaten) return;

            _beaten = true;
            CandyText bestCandy = _best.GetComponent<CandyText>();
            if (bestCandy != null) bestCandy.Set(CandyStyle.Gold);
            Tween.Punch(_best.transform, 0.35f, 0.4f);
            if (_bestScore > 0) NewBestReached?.Invoke();
        }

        /// <summary>Thousands separators, invariant so a European locale cannot turn 1,000 into 1.000.</summary>
        public static string Format(long value) =>
            value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
