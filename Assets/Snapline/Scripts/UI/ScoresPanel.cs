using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using GameKit;
using Snapline.App;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// The player's ten best runs, from `best_scores.png`: three stat tiles, the table on a candy
    /// card with a crown and medals on the top three, SHARE BEST and PLAY ENDLESS.
    ///
    /// Local only, and honest about it — there is no global board, and a screen that promised
    /// worldwide ranks and showed one name would be worse than one that says what it is.
    /// </summary>
    public sealed class ScoresPanel : MonoBehaviour
    {
        private static class Layout
        {
            public const float TitleY = 192f;
            public const float StatsY = 346f;
            public const float CardY = 990f;
            public static readonly Vector2 Card = new Vector2(900f, 1110f);
            // Below the tabs, which the first row used to sit on top of.
            public const float FirstRowY = 576f;
            public const float RowStep = 101f;
            public static readonly Vector2 Row = new Vector2(790f, 90f);
            public const float TabsY = 486f;
            public const float ShareFromBottom = 318f;
            public const float PlayFromBottom = 150f;
        }

        private const int Rows = 10;

        /// <summary>Which table the card is showing.</summary>
        private enum Table { Mine, World }

        public event Action BackRequested;
        public event Action ShareRequested;
        public event Action PlayRequested;

        private RectTransform _root;
        private RectTransform _titleGroup;
        private RectTransform _card;
        private readonly RectTransform[] _statTiles = new RectTransform[3];
        private readonly Text[] _statValues = new Text[3];
        private readonly RectTransform[] _rows = new RectTransform[Rows];
        private readonly Text[] _rank = new Text[Rows];
        private readonly Text[] _score = new Text[Rows];
        private readonly Text[] _date = new Text[Rows];
        private Button _report;
        private readonly List<string> _shown = new List<string>();
        private Button _mineTab;
        private Button _worldTab;
        private Table _table = Table.Mine;

        /// <summary>Rises on every switch, so a slow fetch cannot paint over a newer one.</summary>
        private int _fetch;
        private Text _empty;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Stretch("Scores", parent);
            Vector2 top = W.Top;

            Button back = W.Round("Back", _root, "circle_pink", "sym_back", top, new Vector2(-420f, -100f), 124f, 0.5f);
            back.onClick.AddListener(() => BackRequested?.Invoke());
            CoinPill.Create(_root, top, new Vector2(232f, -62f), 270f, 80f);
            ToolboxButton.Create(_root, top, new Vector2(436f, -86f), 108f);

            _titleGroup = UIKit.Rect("TitleGroup", _root);
            CandyUI.Place(_titleGroup, top, new Vector2(0f, -Layout.TitleY), new Vector2(1000f, 180f));
            Text best = W.Title("Best", _titleGroup, "BEST", W.Centre, Vector2.zero, 118, CandyStyle.White);
            Text scores = W.Title("Scores", _titleGroup, "SCORES", W.Centre, Vector2.zero, 118, CandyStyle.Cyan);

            // Laid out from the words' real widths, so the pair stays centred and never overlaps
            // whatever the font renders them at.
            const float gap = 26f;
            float wb = best.preferredWidth;
            float ws = scores.preferredWidth;
            float start = -(wb + gap + ws) * 0.5f;
            best.rectTransform.anchoredPosition = new Vector2(start + wb * 0.5f, 0f);
            scores.rectTransform.anchoredPosition = new Vector2(start + wb + gap + ws * 0.5f, 0f);

            Stat(0, "tile_purple", "sym_controller", "GAMES", CandyStyle.OnPurple);
            Stat(1, "tile_yellow", "icon_levels", "LINES", CandyStyle.OnGold);
            Stat(2, "tile_pink", "sym_bolt", "BEST COMBO", CandyStyle.OnPink);

            Image card = W.Card("Card", _root, top, new Vector2(0f, -Layout.CardY), Layout.Card, 48f);
            _card = card.rectTransform;

            _mineTab = W.Pill("MineTab", _root, "pill_white", "MY BEST", top, new Vector2(-200f, -Layout.TabsY),
                              new Vector2(290f, 84f), 42, CandyStyle.OnBlue, tint: W.CandyBlue);
            _mineTab.onClick.AddListener(() => SetTable(Table.Mine));

            _worldTab = W.Pill("WorldTab", _root, "pill_white", "WORLD", top, new Vector2(100f, -Layout.TabsY),
                               new Vector2(290f, 84f), 42, CandyStyle.OnBlue, tint: W.CandyBlue);
            _worldTab.onClick.AddListener(() => SetTable(Table.World));

            BuildRows();

            // A name other players can see is content we are answerable for, so there has to be a way
            // to tell us about one. Shown only on the WORLD table, where the names are.
            // Beside the WORLD tab: at the foot of the screen it was drawn underneath SHARE BEST,
            // which is created after it.
            _report = W.Pill("Report", _root, "pill_white", "REPORT", top, new Vector2(352f, -Layout.TabsY),
                             new Vector2(170f, 84f), 32, CandyStyle.Pink);
            _report.onClick.AddListener(ReportNames);
            _report.gameObject.SetActive(false);


            _empty = W.Text("Empty", _root, "No runs yet.\nPlay a game to get on the board!", top,
                            new Vector2(0f, -Layout.CardY), new Vector2(800f, 200f), 50, CandyStyle.Cocoa, Color.white);
            _empty.font = Design.Display;

            Button share = W.Pill("Share", _root, "pill_white", "SHARE BEST", W.Bottom, new Vector2(0f, Layout.ShareFromBottom),
                                  new Vector2(720f, 152f), 76, CandyStyle.OnBlue, "icon_share", 90f, shine: true,
                                  tint: W.CandyBlue);
            share.onClick.AddListener(() => ShareRequested?.Invoke());

            Button play = W.Pill("PlayEndless", _root, "pill_red", "PLAY ENDLESS", W.Bottom, new Vector2(0f, Layout.PlayFromBottom),
                                 new Vector2(720f, 152f), 76, CandyStyle.OnPink, "icon_play", 84f, sprinkles: true, pulse: 0.02f);
            play.onClick.AddListener(() => PlayRequested?.Invoke());

            _root.gameObject.SetActive(false);
        }

        private void Stat(int index, string tile, string icon, string caption, CandyStyle style)
        {
            float x = (index - 1) * 300f;
            Image t = W.Sliced("Stat" + index, _root, tile, W.Top, new Vector2(x, -Layout.StatsY), new Vector2(284f, 148f));
            _statTiles[index] = t.rectTransform;
            W.Img("Icon", t.transform, icon, W.Left, new Vector2(66f, 0f), new Vector2(92f, 92f));
            _statValues[index] = W.Text("Value", t.transform, "0", W.Centre, new Vector2(46f, 20f), new Vector2(180f, 80f),
                                        index == 2 ? 62 : 66, style, Color.white);
            _statValues[index].font = Design.Display;
            W.Text("Caption", t.transform, caption, W.Centre, new Vector2(46f, -40f), new Vector2(200f, 40f),
                   index == 2 ? 24 : 30, style, Color.white);
        }

        private void BuildRows()
        {
            for (int i = 0; i < Rows; i++)
            {
                float y = -(Layout.FirstRowY + i * Layout.RowStep);
                Image row = W.Sliced("Row" + i, _root, i == 0 ? "pill_yellow" : "pill_white", W.Top, new Vector2(0f, y), Layout.Row);
                if (i > 0) row.color = i % 2 == 1 ? new Color(0.84f, 0.92f, 1f, 1f) : new Color(1f, 0.95f, 0.88f, 1f);
                _rows[i] = row.rectTransform;

                if (i < 3)
                {
                    string medal = i == 0 ? "reward_crown" : i == 1 ? "reward_medal_silver" : "reward_medal_bronze";
                    Image m = W.Img("Medal", row.transform, medal, W.Left, new Vector2(88f, i == 0 ? 6f : 0f),
                                    i == 0 ? new Vector2(116f, 96f) : new Vector2(76f, 92f));
                    if (i == 0) m.gameObject.AddComponent<Glint>().Every = 1.3f;
                }

                _rank[i] = W.Text("Rank", row.transform, (i + 1).ToString(), W.Left, new Vector2(214f, 2f),
                                  new Vector2(90f, 80f), 54, i == 0 ? CandyStyle.Cocoa : CandyStyle.Navy, Color.white);
                _rank[i].font = Design.Display;
                _score[i] = W.Text("Score", row.transform, "", W.Centre, new Vector2(-20f, 2f), new Vector2(320f, 80f),
                                   i == 0 ? 66 : 58, i == 0 ? CandyStyle.Cocoa : CandyStyle.Navy, Color.white);
                _score[i].font = Design.Display;
                _date[i] = W.Text("Date", row.transform, "", W.Right, new Vector2(-104f, 2f), new Vector2(180f, 60f),
                                  32, i == 0 ? CandyStyle.Cocoa : CandyStyle.Navy, Color.white);
            }
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            SetTable(Table.Mine);

            Tween.PopIn(_titleGroup, 0f, 0.5f, 0.3f);
            for (int i = 0; i < 3; i++) Tween.PopIn(_statTiles[i], 0.1f + i * 0.07f, 0.45f, 0.2f);
            Sound.Swoosh();
        }

        /// <summary>Switches table and repaints. The world table paints again when its fetch lands.</summary>
        private void SetTable(Table table)
        {
            _table = table;
            _fetch++;

            Highlight(_mineTab, table == Table.Mine);
            Highlight(_worldTab, table == Table.World);

            _report.gameObject.SetActive(table == Table.World);

            if (table == Table.Mine) ShowMine();
            else ShowWorld(_fetch);
        }

        private static void Highlight(Button tab, bool on)
        {
            tab.GetComponent<Image>().color = on ? Color.white : new Color(0.74f, 0.78f, 0.86f, 1f);
            tab.transform.localScale = Vector3.one * (on ? 1f : 0.94f);
        }

        private void ShowMine()
        {
            List<SaveSystem.ScoreEntry> scores = SaveSystem.BestScores();
            DateTime today = DateTime.UtcNow.Date;

            for (int i = 0; i < Rows; i++)
            {
                bool has = i < scores.Count;
                _rows[i].gameObject.SetActive(has);
                if (!has) continue;

                _score[i].text = Hud.Format(scores[i].Score);
                DateTime date = scores[i].Date;
                _date[i].text = date == today ? "TODAY"
                    : date.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
            }

            _empty.text = "No runs yet.\nPlay a game to get on the board!";
            _empty.gameObject.SetActive(scores.Count == 0);

            _statValues[0].text = SaveSystem.GamesPlayed.ToString();
            _statValues[1].text = Hud.Format(SaveSystem.LifetimeLines);
            _statValues[2].text = "x" + Mathf.Max(1, SaveSystem.BestCombo);

            for (int i = 0; i < Rows; i++)
                if (_rows[i].gameObject.activeSelf) Tween.SlideIn(_rows[i], new Vector2(0f, -120f), 0.06f + i * 0.04f, 0.4f);
            if (scores.Count > 0) Tween.Delay(0.25f, () => Fx.Instance?.Sparkles(_rows[0].position, 12, 300f, 80f));
        }

        /// <summary>
        /// The global top ten, and where the player sits in it.
        ///
        /// Written so every way this can go wrong ends in a sentence on the card: no board id, no
        /// linked project, no network, an empty board, or a player who has not ranked yet.
        /// </summary>
        private async void ShowWorld(int fetch)
        {
            for (int i = 0; i < Rows; i++) _rows[i].gameObject.SetActive(false);

            _empty.text = "Loading the world board...";
            _empty.gameObject.SetActive(true);

            string id = Boards.BestScoreId;
            if (string.IsNullOrEmpty(id) || !Boards.Available)
            {
                _empty.text = "The world board is offline.\nYour own scores are safe on this device.";
                return;
            }

            List<LeaderboardEntry> top = await GameKitRuntime.Leaderboards.TopAsync(id, Rows);
            LeaderboardEntry? mine = await GameKitRuntime.Leaderboards.PlayerEntryAsync(id);

            // The player switched tables, or left the screen, while the network was thinking.
            if (fetch != _fetch || _table != Table.World) return;

            _shown.Clear();
            _empty.gameObject.SetActive(top.Count == 0);
            if (top.Count == 0)
            {
                _empty.text = "Nobody is on the board yet.\nFinish an endless run to be the first!";
                return;
            }

            for (int i = 0; i < Rows; i++)
            {
                bool has = i < top.Count;
                _rows[i].gameObject.SetActive(has);
                if (!has) continue;

                _rank[i].text = top[i].rank.ToString();
                _score[i].text = Hud.Format((long)top[i].score);
                _date[i].text = Name(top[i].playerName);
                _shown.Add($"#{top[i].rank} {top[i].playerName}");
            }

            // Where the player is in all this. In the ten, their row says YOU; outside it, the last
            // row becomes their row, rank and all, so the card always answers "where am I".
            if (mine.HasValue)
            {
                int rank = mine.Value.rank;
                int slot = rank <= Rows ? rank - 1 : Rows - 1;

                if (rank > Rows)
                {
                    _rows[slot].gameObject.SetActive(true);
                    _rank[slot].text = rank.ToString();
                    _score[slot].text = Hud.Format((long)mine.Value.score);
                }

                _date[slot].text = "YOU";
                _rows[slot].GetComponent<Image>().color = new Color(1f, 0.86f, 0.42f, 1f);
            }

            for (int i = 0; i < Rows; i++)
                if (_rows[i].gameObject.activeSelf) Tween.SlideIn(_rows[i], new Vector2(0f, -120f), 0.04f + i * 0.04f, 0.4f);
        }

        /// <summary>
        /// Opens a mail to us listing the names on the board, so a player can say which one is a
        /// problem. A mail client is a poor form, but it is a route that exists on every phone and
        /// needs no account, and a route that exists beats a better one that does not.
        /// </summary>
        private void ReportNames()
        {
            Sound.Tap();

            var body = new System.Text.StringBuilder();
            body.Append("Which name is a problem, and why?").Append("\n\n");
            body.Append("On the board when reported:").Append("\n");
            foreach (string row in _shown) body.Append(row).Append("\n");

            string url = "mailto:saar@scibox-studios.com" +
                         "?subject=" + UnityWebRequest.EscapeURL("Snapline: report a leaderboard name") +
                         "&body=" + UnityWebRequest.EscapeURL(body.ToString());
            Application.OpenURL(url);
        }

        /// <summary>
        /// Unity hands back an empty name for a player who never set one, and appends a discriminator
        /// to the ones that did: Bob#1234 is Bob to everyone reading the board.
        /// </summary>
        private static string Name(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return "PLAYER";

            int hash = playerName.IndexOf((char)35);
            string clean = hash > 0 ? playerName.Substring(0, hash) : playerName;
            return clean.Length <= 12 ? clean.ToUpperInvariant() : clean.Substring(0, 12).ToUpperInvariant();
        }

        /// <summary>Switches to the world table. The smoke harness uses it.</summary>
        public void ShowWorldForHarness() => SetTable(Table.World);

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
