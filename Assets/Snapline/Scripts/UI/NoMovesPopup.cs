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
    /// NO MORE MOVES, from `popup_no_more_moves.png`: the board is full, and three ways to keep the
    /// run going — spend a shuffle you hold, watch a video, or pay coins — or END RUN.
    ///
    /// All three buy the same rescue, and there is one per run. A route that is not open right now is
    /// dimmed rather than hidden, so the card keeps its shape and the player can see what exists.
    /// </summary>
    public sealed class NoMovesPopup : CandyPopup
    {
        public enum Choice { Shuffle, Watch, Coins }

        private static class Layout
        {
            public static readonly Vector2 Card = new Vector2(930f, 1200f);
            public const float CardY = -40f;
            public const float CoinsY = 150f;
            public const float PictureY = 360f;
            public const float LineOneY = 548f;
            public const float LineTwoY = 604f;
            public const float ShuffleY = 712f;
            public const float WatchY = 874f;
            public const float PayY = 1030f;
            public const float EndY = 1136f;
        }

        public event Action ShuffleChosen;
        public event Action WatchChosen;
        public event Action CoinsChosen;
        public event Action EndChosen;

        private Button _shuffle;
        private Text _shuffleLeft;
        private Button _watch;
        private Text _watchCaption;
        private Button _pay;
        private CoinPill _coins;

        public void Init(RectTransform parent)
        {
            BuildShell(parent, "NoMoves", "NO MORE MOVES", Layout.Card, Layout.CardY, true, 760f, 74);

            _coins = CoinPill.Create(Card, W.Top, new Vector2(0f, -Layout.CoinsY), 320f, 88f);

            BuildPicture();

            W.Text("Full", Card, "Your board is full.", W.Top, new Vector2(0f, -Layout.LineOneY), new Vector2(860f, 70f),
                   52, CandyStyle.Cocoa, Color.white).font = Design.Display;
            W.Text("Keep", Card, "Keep this run going?", W.Top, new Vector2(0f, -Layout.LineTwoY), new Vector2(860f, 50f),
                   38, CandyStyle.Cocoa, Color.white);

            _shuffle = W.Pill("Shuffle", Card, "pill_purple", "SHUFFLE BOARD", W.Top, new Vector2(0f, -Layout.ShuffleY),
                              new Vector2(800f, 144f), 52, CandyStyle.OnPurple);
            W.Caption(_shuffle).rectTransform.anchoredPosition = new Vector2(-6f, 4f);
            W.Img("Icon", _shuffle.transform, "icon_shuffle", W.Left, new Vector2(84f, 0f), new Vector2(96f, 96f));
            Image left = W.Sliced("Left", _shuffle.transform, "pill_red", W.Right, new Vector2(-104f, 0f), new Vector2(160f, 74f));
            _shuffleLeft = W.Text("Count", left.transform, "2 LEFT", W.Centre, new Vector2(0f, 2f), new Vector2(170f, 70f),
                                  38, CandyStyle.OnPink, Color.white);
            _shuffleLeft.font = Design.Display;
            _shuffle.onClick.AddListener(() => ShuffleChosen?.Invoke());

            _watch = W.Pill("Watch", Card, "pill_green", "WATCH TO CONTINUE", W.Top, new Vector2(0f, -Layout.WatchY),
                            new Vector2(800f, 150f), 54, CandyStyle.OnGreen, "sym_ad", 100f, shine: true, pulse: 0.025f);
            _watchCaption = W.Caption(_watch);
            _watchCaption.rectTransform.anchoredPosition += new Vector2(20f, 16f);
            W.Text("Sub", _watch.transform, "Free rescue", W.Centre, new Vector2(70f, -38f), new Vector2(500f, 40f),
                   32, CandyStyle.OnGreen, Color.white);
            _watch.onClick.AddListener(() => WatchChosen?.Invoke());

            _pay = W.Pill("Pay", Card, "pill_white", $"USE {Economy.ContinuePrice} COINS", W.Top,
                          new Vector2(0f, -Layout.PayY), new Vector2(600f, 116f), 50, CandyStyle.OnBlue, "coin", 92f,
                          tint: W.CandyBlue);
            _pay.onClick.AddListener(() => CoinsChosen?.Invoke());

            Button end = CandyUI.SpriteButton("End", Card, null);
            end.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            CandyUI.Place(end, W.Top, new Vector2(0f, -Layout.EndY), new Vector2(340f, 70f));
            W.Text("Text", end.transform, "END RUN", W.Centre, Vector2.zero, new Vector2(300f, 60f), 38,
                   CandyStyle.Cocoa, Color.white);
            W.Rounded("RuleL", Card, CandyText.Hex(0xE3C3AE), CandyText.Hex(0xE3C3AE), W.Top,
                      new Vector2(-230f, -Layout.EndY), new Vector2(110f, 4f), 2f);
            W.Rounded("RuleR", Card, CandyText.Hex(0xE3C3AE), CandyText.Hex(0xE3C3AE), W.Top,
                      new Vector2(230f, -Layout.EndY), new Vector2(110f, 4f), 2f);
            end.onClick.AddListener(() => EndChosen?.Invoke());
        }

        /// <summary>
        /// The picture from the reference: three loose pieces with nowhere to go, and a crammed board.
        /// Built from the block art rather than sliced from the mock-up, so it matches the real pieces.
        /// </summary>
        private void BuildPicture()
        {
            RectTransform pic = UIKit.Rect("Picture", Card);
            CandyUI.Place(pic, W.Top, new Vector2(0f, -Layout.PictureY), new Vector2(820f, 320f));
            pic.gameObject.AddComponent<CandyPress>().Bob = 8f;

            void Piece(string name, int colour, (int c, int r)[] cells, Vector2 at, float cell, float angle)
            {
                RectTransform root = UIKit.Rect(name, pic);
                CandyUI.Place(root, W.Centre, at, new Vector2(cell * 3f, cell * 3f));
                root.localRotation = Quaternion.Euler(0f, 0f, angle);
                foreach ((int c, int r) in cells)
                {
                    Image b = UIKit.Image("b", root, ArtKit.Block(colour), Color.white);
                    b.raycastTarget = false;
                    CandyUI.Place(b, W.Centre, new Vector2((c - 0.5f) * cell, (0.5f - r) * cell), new Vector2(cell - 4f, cell - 4f));
                }
            }

            Piece("Green", 3, new[] { (0, 0), (0, 1), (1, 1) }, new Vector2(-300f, 40f), 78f, 18f);
            Piece("Orange", 1, new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, new Vector2(-120f, 70f), 72f, -8f);
            Piece("Blue", 4, new[] { (-1, 0), (0, 0), (1, 0) }, new Vector2(-190f, -100f), 80f, 0f);

            for (int i = 0; i < 3; i++)
                W.Rounded("Dash" + i, pic, CandyText.Hex(0xFF6B8A), CandyText.Hex(0xFF6B8A), W.Centre,
                          new Vector2(30f + i * 36f, 0f), new Vector2(22f, 10f), 5f);

            Image frame = CandyUI.Icon("Frame", pic, ArtKit.BoardFrame());
            CandyUI.Place(frame, W.Centre, new Vector2(240f, 0f), new Vector2(300f, 300f));
            var rng = new System.Random(5);
            const float cellSize = 46f;
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 5; c++)
                {
                    Image b = UIKit.Image("m", pic, ArtKit.Block(rng.Next(6)), Color.white);
                    b.raycastTarget = false;
                    CandyUI.Place(b, W.Centre, new Vector2(240f + (c - 2) * cellSize, (2 - r) * cellSize),
                                  new Vector2(cellSize - 3f, cellSize - 3f));
                }
            frame.rectTransform.SetAsLastSibling();
        }

        public void Show(int shufflesHeld, bool videoReady)
        {
            _shuffleLeft.text = $"{shufflesHeld} LEFT";
            Dim(_shuffle, shufflesHeld <= 0);
            Dim(_pay, Wallet.Coins < Economy.ContinuePrice);
            SetWatchAvailable(videoReady);
            SetWatchBusy(false);
            _coins.ShowImmediate(Wallet.Coins);

            Present();
            Reveal(_shuffle, 0.16f);
            Reveal(_watch, 0.22f);
            Reveal(_pay, 0.28f);
        }

        private static void Dim(Button b, bool dim)
        {
            b.GetComponent<Image>().color = dim ? new Color(0.72f, 0.72f, 0.78f, 1f) : Color.white;
            CanvasGroup g = b.GetComponent<CanvasGroup>() ?? b.gameObject.AddComponent<CanvasGroup>();
            g.alpha = dim ? 0.7f : 1f;
        }

        /// <summary>A route the player tapped but cannot take: shake it and say why.</summary>
        public void Refuse(Choice choice)
        {
            Button b = choice == Choice.Shuffle ? _shuffle : choice == Choice.Coins ? _pay : _watch;
            Sound.Deny();
            Tween.Shake((RectTransform)b.transform, 18f, 0.4f);
            string why = choice == Choice.Shuffle ? "NO SHUFFLES LEFT" : "NOT ENOUGH COINS";
            Fx.Instance?.Text(b.transform.position, why, CandyStyle.White, 52f, 1.1f, 160f);
        }

        public void SetWatchBusy(bool busy)
        {
            _watch.interactable = !busy;
            _watchCaption.text = busy ? "LOADING…" : "WATCH TO CONTINUE";
        }

        public void SetWatchAvailable(bool available)
        {
            _watch.interactable = available;
            Dim(_watch, !available);
            _watchCaption.text = available ? "WATCH TO CONTINUE" : "NO VIDEO RIGHT NOW";
        }

        protected override void OnCloseButton() => EndChosen?.Invoke();

        /// <summary>END RUN, as if tapped. The smoke harness uses it when no rescue is on offer.</summary>
        public void ChooseEnd() => EndChosen?.Invoke();
    }
}
