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
        private Text _empty;
        private Text _summary;

        public event Action BackRequested;
        public event Action ShareRequested;

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

            Text title = UIKit.Label("Title", _root, "BEST SCORES", 72, Palette.TextBright);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -70f), new Vector2(900f, 90f));

            _summary = UIKit.Label("Summary", _root, "", 34, Palette.TextDim);
            UIKit.Place(_summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -158f), new Vector2(900f, 44f));

            BuildRows();

            _empty = UIKit.Label("Empty", _root, "No runs yet.\nPlay a game to get on the board.",
                                 40, Palette.TextDim);
            UIKit.Place(_empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 0f), new Vector2(900f, 160f));

            Button share = UIKit.Button("Share", _root, "SHARE", new Color(0.34f, 0.55f, 0.85f, 1f),
                                        Color.white, 40);
            UIKit.Place(share.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(520f, 110f));
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button back = UIKit.Button("Back", _root, "BACK", new Color(0.30f, 0.36f, 0.62f, 1f), Color.white, 40);
            UIKit.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(520f, 110f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        private void BuildRows()
        {
            _rank = new Text[Rows];
            _score = new Text[Rows];
            _date = new Text[Rows];
            _rowPanels = new Image[Rows];

            const float rowHeight = 106f;
            const float top = -240f;

            Sprite rowSprite = ArtKit.RoundedRect("scorerow", new Color(1f, 1f, 1f, 0.05f),
                                                  new Color(1f, 1f, 1f, 0.09f), 2f);
            Sprite topSprite = ArtKit.RoundedRect("scorerow1", new Color(1f, 0.78f, 0.25f, 0.16f),
                                                  new Color(1f, 0.85f, 0.4f, 0.5f), 3f);

            for (int i = 0; i < Rows; i++)
            {
                RectTransform row = UIKit.Rect($"Row{i}", _root);
                UIKit.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, top - i * rowHeight), new Vector2(900f, rowHeight - 12f));

                Image panel = UIKit.Image("Panel", row, i == 0 ? topSprite : rowSprite, Color.white,
                                          Image.Type.Sliced);
                RectTransform pr = panel.rectTransform;
                pr.anchorMin = Vector2.zero;
                pr.anchorMax = Vector2.one;
                pr.offsetMin = Vector2.zero;
                pr.offsetMax = Vector2.zero;
                _rowPanels[i] = panel;

                _rank[i] = UIKit.Label("Rank", row, $"{i + 1}", 42,
                                       i == 0 ? Palette.Accent : Palette.TextDim, TextAnchor.MiddleLeft);
                UIKit.Place(_rank[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(120f, 60f));

                _score[i] = UIKit.Label("Score", row, "", 50,
                                        i == 0 ? Palette.Accent : Palette.TextBright, TextAnchor.MiddleLeft);
                UIKit.Place(_score[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(450f, 60f));

                _date[i] = UIKit.Label("Date", row, "", 32, Palette.TextDim, TextAnchor.MiddleRight);
                UIKit.Place(_date[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(300f, 60f));
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

            _summary.text = SaveSystem.GamesPlayed == 0
                ? string.Empty
                : $"{SaveSystem.GamesPlayed} games    {Hud.Format(SaveSystem.LifetimeLines)} lines    " +
                  $"best combo x{Mathf.Max(1, SaveSystem.BestCombo)}";

            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
