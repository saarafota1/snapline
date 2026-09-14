using System;
using System.Collections.Generic;
using UnityEngine;
using Snapline.Core;

namespace Snapline.App
{
    /// <summary>
    /// Which daily challenges the player has finished, and the streak that makes.
    ///
    /// Days are the phone's LOCAL calendar days. A player in Tokyo and one in London get the same
    /// puzzle for "Thursday", each on their own Thursday — which is what a player means by today.
    /// </summary>
    public static class DailyProgress
    {
        private const string DoneKey = "snapline.daily.done";

        /// <summary>Days remembered. Enough for a streak display and this week's strip.</summary>
        private const int Remember = 60;

        /// <summary>Overridable by the screenshot harness, so a capture can show a mid-week state.</summary>
        public static int? TodayOverride;

        public static int Today => TodayOverride ?? Daily.DayIndex(DateTime.Now);

        private static HashSet<int> Load()
        {
            var set = new HashSet<int>();
            string raw = PlayerPrefs.GetString(DoneKey, string.Empty);
            foreach (string part in raw.Split(','))
                if (int.TryParse(part, out int day)) set.Add(day);
            return set;
        }

        public static bool IsDone(int day) => Load().Contains(day);

        public static bool TodayDone => IsDone(Today);

        /// <summary>Records a finished day. Returns false if it was already recorded, so a replay pays nothing.</summary>
        public static bool MarkDone(int day)
        {
            HashSet<int> set = Load();
            if (!set.Add(day)) return false;

            var keep = new List<int>();
            foreach (int d in set)
                if (d > Today - Remember) keep.Add(d);
            keep.Sort();

            PlayerPrefs.SetString(DoneKey, string.Join(",", keep));
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Consecutive finished days ending today — or ending yesterday, when today is still open.
        /// A streak is not broken by a day that has not finished yet.
        /// </summary>
        public static int Streak()
        {
            HashSet<int> set = Load();
            int day = set.Contains(Today) ? Today : Today - 1;
            int streak = 0;
            while (set.Contains(day))
            {
                streak++;
                day--;
            }
            return streak;
        }

        /// <summary>Finished days in the current Monday-to-Sunday week.</summary>
        public static int DoneThisWeek()
        {
            HashSet<int> set = Load();
            int start = Daily.WeekStart(Today);
            int n = 0;
            for (int i = 0; i < 7; i++)
                if (set.Contains(start + i)) n++;
            return n;
        }

        /// <summary>Wipes the record. The screenshot harness only.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(DoneKey);
            PlayerPrefs.Save();
        }
    }
}
