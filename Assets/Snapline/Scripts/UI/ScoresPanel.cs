using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;
using Snapline.App;

namespace Snapline.UI
{
    /// <summary>
    /// The player's ten best runs.
    ///
    /// Local only for now, and honest about it — there is no global board until a Unity Gaming
    /// Services project exists, and a screen that promises worldwide ranks and shows one name is
    /// worse than one that says what it is. The layout leaves the top row free for a global tab.
    /// </summary>
    public sealed class ScoresPanel : MonoBehaviour
    {
        private const int Rows = 10;

        private RectTransform _root;
        private Text[] _rank;
        private Text[] _score;
        private Text[] _date;
        private Image[] _rowPanels;
        private Image[] _medals;
        private Text _empty;
        private Text _coinLabel;
        private readonly Text[] _statValue = new Text[3];
        private readonly Text[] _statName = new Text[3];

        public event Action BackRequested;
        public event Action ShareRequested;
        public event Action PlayRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("Scores", parent);

            Image bg = UIKit.Image("Bg", _root, ArtKit.Background(), Color.white);
            RectTransform bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var topAnchor = new Vector2(0.5f, 1f);

            Button back = CandyUI.SpriteButton("Back", _root, ArtKit.Ui("circle_pink"));
            CandyUI.Place(back, topAnchor, new Vector2(-438f, -84f), new Vector2(116f, 116f));
            CandyUI.Place(CandyUI.Icon("Sym", back.transform, ArtKit.Ui("sym_back")),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            Image coinPill = CandyUI.Icon("CoinPill", _root, ArtKit.Ui("pill_blue"));
            coinPill.type = Image.Type.Sliced;
            coinPill.preserveAspect = false;
            CandyUI.Place(coinPill, topAnchor, new Vector2(70f, -84f), new Vector2(300f, 88f));
            CandyUI.Place(CandyUI.Icon("Coin", coinPill.transform, ArtKit.Ui("coin")),
                          new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(66f, 66f));
            _coinLabel = CandyUI.Label("Coins", coinPill.transform, "0", 44, CandyUI.Caption);
            CandyUI.Place(_coinLabel, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(190f, 58f));

            CandyUI.Place(CandyUI.Label("Title", _root, "BEST SCORES", 88, CandyUI.Caption),
                          topAnchor, new Vector2(0f, -212f), new Vector2(960f, 110f));

            // Three stat tiles, as the reference has them: games, lifetime lines, best combo.
            BuildStat(0, -324f, -302f, "tile_purple", "icon_levels");
            BuildStat(1, 0f, -324f, "tile_yellow", "icon_levels");
            BuildStat(2, 324f, -302f, "tile_pink", "icon_star");

            BuildRows();

            _empty = CandyUI.Label("Empty", _root, "No runs yet.\nPlay a game to get on the board.",
                                   42, CandyUI.Caption);
            CandyUI.Place(_empty, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 170f));

            Button share = CandyUI.SpriteButton("Share", _root, ArtKit.Ui("tile_cyan"), Image.Type.Sliced);
            CandyUI.Place(share, new Vector2(0.5f, 0f), new Vector2(0f, 288f), new Vector2(760f, 146f));
            CandyUI.Place(CandyUI.Icon("Sym", share.transform, ArtKit.Ui("icon_share")),
                          new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(66f, 66f));
            CandyUI.Place(CandyUI.Label("Caption", share.transform, "SHARE BEST", 56, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(38f, 0f), new Vector2(560f, 72f));
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button play = CandyUI.SpriteButton("PlayEndless", _root, ArtKit.Ui("tile_pink"), Image.Type.Sliced);
            CandyUI.Place(play, new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(760f, 146f));
            CandyUI.Place(CandyUI.Icon("Sym", play.transform, ArtKit.Ui("icon_play")),
                          new Vector2(0f, 0.5f), new Vector2(126f, 0f), new Vector2(60f, 60f));
            CandyUI.Place(CandyUI.Label("Caption", play.transform, "PLAY ENDLESS", 56, CandyUI.Caption),
                          new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(560f, 72f));
            play.onClick.AddListener(() => PlayRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        /// <summary>One of the three stat tiles across the top.</summary>
        private void BuildStat(int index, float x, float y, string tile, string icon)
        {
            Image panel = CandyUI.Icon("Stat" + index, _root, ArtKit.Ui(tile));
            panel.type = Image.Type.Sliced;
            panel.preserveAspect = false;
            CandyUI.Place(panel, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(324f, 122f));

            CandyUI.Place(CandyUI.Icon("Icon", panel.transform, ArtKit.Ui(icon)),
                          new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(58f, 58f));

            _statValue[index] = CandyUI.Label("Value", panel.transform, "0", 46, CandyUI.Caption,
                                              TextAnchor.MiddleLeft);
            CandyUI.Place(_statValue[index], new Vector2(0f, 0.5f), new Vector2(206f, 16f),
                          new Vector2(176f, 50f));

            _statName[index] = CandyUI.Label("Name", panel.transform, "", 23, CandyUI.CaptionDim,
                                             TextAnchor.MiddleLeft);
            CandyUI.Place(_statName[index], new Vector2(0f, 0.5f), new Vector2(206f, -22f),
                          new Vector2(176f, 32f));
        }

        private void BuildRows()
        {
            _rank = new Text[Rows];
            _score = new Text[Rows];
            _date = new Text[Rows];
            _rowPanels = new Image[Rows];
            _medals = new Image[Rows];

            const float rowHeight = 96f;
            const float first = -452f;

            // The whole table sits on one candy card, as the reference has it, rather than each row
            // floating on the background.
            Image card = CandyUI.Icon("Card", _root, ArtKit.Ui("card_cream"));
            card.type = Image.Type.Sliced;
            card.preserveAspect = false;
            // Sized from the rows rather than by eye, so the last row cannot fall off the bottom of
            // the card when the row height or the row count changes.
            float span = rowHeight * (Rows - 1);
            CandyUI.Place(card, new Vector2(0.5f, 1f), new Vector2(0f, first - span * 0.5f),
                          new Vector2(920f, span + rowHeight + 76f));

            for (int i = 0; i < Rows; i++)
            {
                RectTransform row = UIKit.Rect("Row" + i, _root);
                CandyUI.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, first - i * rowHeight),
                              new Vector2(844f, rowHeight - 8f));

                // First place gets the gold treatment; the rest alternate two pale tints so ten rows
                // of numbers stay readable as rows rather than as a block of text.
                Image panel = CandyUI.Icon("Panel", row, ArtKit.Ui(i == 0 ? "pill_gold" : "pill_white"));
                panel.type = Image.Type.Sliced;
                panel.preserveAspect = false;
                RectTransform pr = panel.rectTransform;
                pr.anchorMin = Vector2.zero;
                pr.anchorMax = Vector2.one;
                pr.offsetMin = Vector2.zero;
                pr.offsetMax = Vector2.zero;
                if (i > 0) panel.color = i % 2 == 1 ? new Color(0.88f, 0.94f, 1f, 1f) : Color.white;
                _rowPanels[i] = panel;

                // Crown, silver and bronze for the top three; nothing for the rest.
                _medals[i] = CandyUI.Icon("Medal", row,
                    i == 0 ? ArtKit.Ui("reward_crown") :
                    i == 1 ? ArtKit.Ui("reward_medal_silver") :
                    i == 2 ? ArtKit.Ui("reward_medal_bronze") : null);
                CandyUI.Place(_medals[i], new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(70f, 70f));
                _medals[i].gameObject.SetActive(i < 3);

                _rank[i] = CandyUI.Label("Rank", row, (i + 1).ToString(), 44, CandyUI.CaptionOnYellow,
                                         TextAnchor.MiddleLeft, outline: false);
                CandyUI.Place(_rank[i], new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(90f, 56f));

                _score[i] = CandyUI.Label("Score", row, "", 50, CandyUI.CaptionOnYellow,
                                          TextAnchor.MiddleLeft, outline: false);
                CandyUI.Place(_score[i], new Vector2(0f, 0.5f), new Vector2(400f, 0f), new Vector2(340f, 60f));

                _date[i] = CandyUI.Label("Date", row, "", 30, CandyUI.CaptionOnYellow,
                                         TextAnchor.MiddleRight, outline: false);
                CandyUI.Place(_date[i], new Vector2(1f, 0.5f), new Vector2(-160f, 0f), new Vector2(280f, 52f));
            }
        }

        public void Show()
        {
            List<SaveSystem.ScoreEntry> scores = SaveSystem.BestScores();

            for (int i = 0; i < Rows; i++)
            {
                bool has = i < scores.Count;
                _rowPanels[i].gameObject.SetActive(true);
                _rank[i].gameObject.SetActive(true);

                if (has)
                {
                    _score[i].text = Hud.Format(scores[i].Score);
                    _date[i].text = scores[i].Date.ToString("d MMM yyyy",
                                                            System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    _score[i].text = "—";
                    _date[i].text = string.Empty;
                }

                Color dim = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                _score[i].color = (i == 0 && has ? Palette.Accent : Palette.TextBright) * dim;
            }

            _empty.gameObject.SetActive(scores.Count == 0);
            for (int i = 0; i < Rows; i++) _rowPanels[i].transform.parent.gameObject.SetActive(scores.Count > 0);

            _statValue[0].text = SaveSystem.GamesPlayed.ToString();
            _statName[0].text = "GAMES";
            _statValue[1].text = Hud.Format(SaveSystem.LifetimeLines);
            _statName[1].text = "LINES";
            _statValue[2].text = "x" + Mathf.Max(1, SaveSystem.BestCombo);
            _statName[2].text = "BEST COMBO";

            if (_coinLabel != null) _coinLabel.text = Hud.Format(App.Wallet.Coins);

            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
