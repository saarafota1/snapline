using System.Collections.Generic;
using UnityEngine;
using Snapline.Core;

namespace Snapline.App
{
    /// <summary>
    /// Persistence for the run in progress, the high score and lifetime stats.
    ///
    /// PlayerPrefs rather than a file: on Android it is backed by SharedPreferences, which the OS
    /// flushes and which survives the process being killed, and it avoids a whole class of
    /// half-written-file bugs. The payload is checksummed by SaveCodec, so a torn write is detected
    /// and discarded rather than restoring a corrupt board.
    /// </summary>
    public static class SaveSystem
    {
        private const string RunKey = "snapline.run";
        private const string BestKey = "snapline.best";
        private const string GamesKey = "snapline.games";
        private const string TotalLinesKey = "snapline.lines";
        private const string BestComboKey = "snapline.bestcombo";

        public static long HighScore
        {
            get
            {
                // PlayerPrefs has no long accessor and scores can exceed int range on a long run.
                string raw = PlayerPrefs.GetString(BestKey, "0");
                return long.TryParse(raw, out long v) ? v : 0L;
            }
            private set
            {
                PlayerPrefs.SetString(BestKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public static int GamesPlayed => PlayerPrefs.GetInt(GamesKey, 0);
        public static int LifetimeLines => PlayerPrefs.GetInt(TotalLinesKey, 0);
        public static int BestCombo => PlayerPrefs.GetInt(BestComboKey, 0);

        /// <summary>Returns true if this score is a new personal best, and records it either way.</summary>
        public static bool SubmitScore(long score)
        {
            if (score <= HighScore) return false;
            HighScore = score;
            PlayerPrefs.Save();
            return true;
        }

        public static void RecordFinishedRun(long score, int lines, int bestCombo)
        {
            RecordScore(score);
            PlayerPrefs.SetInt(GamesKey, GamesPlayed + 1);
            PlayerPrefs.SetInt(TotalLinesKey, LifetimeLines + lines);
            if (bestCombo > BestCombo) PlayerPrefs.SetInt(BestComboKey, bestCombo);
            SubmitScore(score);
            PlayerPrefs.Save();
        }

        // --- run in progress ---------------------------------------------------------------

        public static void SaveRun(RunSnapshot snapshot)
        {
            if (snapshot == null) return;
            PlayerPrefs.SetString(RunKey, SaveCodec.Encode(snapshot));
            PlayerPrefs.Save();
        }

        /// <summary>The saved run, or null if there is none or it failed its checksum.</summary>
        public static RunSnapshot LoadRun()
        {
            string raw = PlayerPrefs.GetString(RunKey, null);
            if (string.IsNullOrEmpty(raw)) return null;

            RunSnapshot snapshot = SaveCodec.Decode(raw);
            if (snapshot == null)
            {
                Debug.LogWarning("[Snapline] Saved run failed to decode and was discarded.");
                ClearRun();
                return null;
            }

            // A finished run is not worth resuming; the player wants a fresh board.
            if (snapshot.GameOver)
            {
                ClearRun();
                return null;
            }

            return snapshot;
        }

        public static bool HasSavedRun()
        {
            string raw = PlayerPrefs.GetString(RunKey, null);
            return !string.IsNullOrEmpty(raw);
        }

        /// <summary>
        /// The score of the run waiting to be resumed, or 0 if there is none.
        ///
        /// The menu shows this on its CONTINUE button, so it is worth knowing that a torn or
        /// unreadable save answers 0 rather than throwing: the button still works, it just says
        /// less, which is the right failure for a decoration on top of a working action.
        /// </summary>
        public static long SavedRunScore()
        {
            RunSnapshot snapshot = LoadRun();
            return snapshot?.Score ?? 0L;
        }

        public static void ClearRun()
        {
            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.Save();
        }

        // --- personal best scores -------------------------------------------------------------

        private const string TableKey = "snapline.scores";
        private const int TableSize = 10;

        /// <summary>One finished run worth remembering.</summary>
        public readonly struct ScoreEntry
        {
            public readonly long Score;

            /// <summary>Days since 2020-01-01, so the whole table stays a short ASCII string.</summary>
            public readonly int Day;

            public ScoreEntry(long score, int day)
            {
                Score = score;
                Day = day;
            }

            public System.DateTime Date => new System.DateTime(2020, 1, 1).AddDays(Day);
        }

        private static int Today =>
            (int)(System.DateTime.UtcNow.Date - new System.DateTime(2020, 1, 1)).TotalDays;

        /// <summary>The player's best runs, highest first.</summary>
        public static List<ScoreEntry> BestScores()
        {
            var list = new List<ScoreEntry>();
            string raw = PlayerPrefs.GetString(TableKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (string part in raw.Split('|'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                string[] bits = part.Split(':');
                if (bits.Length != 2) continue;
                if (!long.TryParse(bits[0], out long score)) continue;
                if (!int.TryParse(bits[1], out int day)) continue;
                list.Add(new ScoreEntry(score, day));
            }

            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        /// <summary>Add a finished run to the table, keeping only the best few.</summary>
        public static void RecordScore(long score)
        {
            if (score <= 0) return;

            List<ScoreEntry> list = BestScores();
            list.Add(new ScoreEntry(score, Today));
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (list.Count > TableSize) list.RemoveRange(TableSize, list.Count - TableSize);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(list[i].Score).Append(':').Append(list[i].Day);
            }

            PlayerPrefs.SetString(TableKey, sb.ToString());
            PlayerPrefs.Save();
        }

        // --- level progress -----------------------------------------------------------------

        private const string StarsKey = "snapline.stars";

        /// <summary>
        /// Stars earned per level, one digit each, indexed from level 1. Stored as a plain string so
        /// adding levels later just makes it longer — a short or missing string reads as zero stars
        /// rather than as corrupt data.
        /// </summary>
        private static string StarString => PlayerPrefs.GetString(StarsKey, string.Empty);

        public static int StarsForLevel(int level)
        {
            if (level < 1) return 0;
            string s = StarString;
            if (level > s.Length) return 0;
            char c = s[level - 1];
            return c >= '0' && c <= '3' ? c - '0' : 0;
        }

        /// <summary>Record a result, keeping the player's best star count for that level.</summary>
        public static void RecordLevelResult(int level, int stars)
        {
            if (level < 1 || stars < 1) return;
            if (stars <= StarsForLevel(level)) return;

            string s = StarString;
            if (s.Length < level) s = s.PadRight(level, '0');

            char[] chars = s.ToCharArray();
            chars[level - 1] = (char)('0' + Mathf.Clamp(stars, 0, 3));

            PlayerPrefs.SetString(StarsKey, new string(chars));
            PlayerPrefs.Save();
        }

        public static bool IsLevelUnlocked(int level) => level <= 1 || StarsForLevel(level - 1) > 0;

        /// <summary>The furthest level the player may play. Always at least 1.</summary>
        public static int HighestUnlockedLevel()
        {
            int highest = 1;
            for (int i = 1; i <= Core.Levels.Count; i++)
            {
                if (StarsForLevel(i) > 0) highest = Mathf.Min(i + 1, Core.Levels.Count);
                else break;
            }
            return highest;
        }

        public static int TotalStars()
        {
            int total = 0;
            for (int i = 1; i <= Core.Levels.Count; i++) total += StarsForLevel(i);
            return total;
        }

        public static int LevelsCompleted()
        {
            int done = 0;
            for (int i = 1; i <= Core.Levels.Count; i++) if (StarsForLevel(i) > 0) done++;
            return done;
        }

        /// <summary>Wipe everything. Only reachable from the editor menu.</summary>
        public static void WipeAll()
        {
            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.DeleteKey(BestKey);
            PlayerPrefs.DeleteKey(GamesKey);
            PlayerPrefs.DeleteKey(TotalLinesKey);
            PlayerPrefs.DeleteKey(BestComboKey);
            PlayerPrefs.Save();
        }
    }
}
