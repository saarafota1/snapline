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

        public static void ClearRun()
        {
            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.Save();
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
