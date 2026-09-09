using System;
using UnityEngine;
using Snapline.Core;

namespace Snapline.App
{
    /// <summary>
    /// The player's coins and power-ups, persisted.
    ///
    /// The rules — what things cost, what a run pays — live in <see cref="Economy"/>, in the engine
    /// assembly with no Unity in it, so they can be balanced against simulated play. This is only
    /// where the numbers are kept between sessions.
    ///
    /// Every mutation writes through immediately rather than at a checkpoint. Android kills
    /// backgrounded apps without warning, and a player who spends 120 coins on a hammer and finds it
    /// gone after a crash has been robbed by the save system.
    /// </summary>
    public static class Wallet
    {
        private const string CoinsKey = "snapline.coins";
        private const string ToolKeyPrefix = "snapline.tool.";

        /// <summary>
        /// What a new player starts with.
        ///
        /// Enough for one undo and change, so the store is not a wall of things they cannot afford
        /// before their first run has paid out — but not enough to buy the hammer, which is the
        /// reward for playing rather than for installing.
        /// </summary>
        private const int StartingCoins = 60;

        private const string SeededKey = "snapline.wallet.seeded";

        /// <summary>Raised whenever the balance or any tool count changes, so screens can refresh.</summary>
        public static event Action Changed;

        public static int Coins
        {
            get
            {
                EnsureSeeded();
                return PlayerPrefs.GetInt(CoinsKey, 0);
            }
        }

        /// <summary>
        /// Grants coins. Negative amounts are ignored rather than quietly deducting — spending goes
        /// through <see cref="TrySpend"/>, which is the only path that can fail and so the only path
        /// that gets to think about affordability.
        /// </summary>
        public static void Grant(int amount)
        {
            if (amount <= 0) return;
            EnsureSeeded();
            Set(CoinsKey, Coins + amount);
        }

        /// <summary>
        /// Spends coins if there are enough, and says whether it happened.
        ///
        /// Returns false and changes nothing when the player cannot afford it, so a caller can offer
        /// the store instead. The balance is never allowed below zero.
        /// </summary>
        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            EnsureSeeded();

            int coins = Coins;
            if (coins < amount) return false;

            Set(CoinsKey, coins - amount);
            return true;
        }

        public static bool CanAfford(int amount) => Coins >= amount;

        // --- tools ---------------------------------------------------------------------------

        public static int Count(Tool tool) => PlayerPrefs.GetInt(Key(tool), 0);

        /// <summary>Total power-ups owned. The number on the toolbox badge.</summary>
        public static int TotalTools =>
            Count(Tool.Undo) + Count(Tool.Shuffle) + Count(Tool.Hammer);

        public static void GrantTool(Tool tool, int count = 1)
        {
            if (count <= 0) return;
            Set(Key(tool), Count(tool) + count);
        }

        /// <summary>
        /// Consumes one of a tool. False when the player has none, so the caller can offer to buy
        /// rather than firing an effect that did not happen.
        /// </summary>
        public static bool TryUseTool(Tool tool)
        {
            int held = Count(tool);
            if (held <= 0) return false;

            Set(Key(tool), held - 1);
            return true;
        }

        /// <summary>
        /// Buys one of a tool with coins.
        ///
        /// The two writes are not atomic — PlayerPrefs has no transaction — so the coins go first.
        /// If the process dies between them the player loses the coins rather than getting the tool
        /// for free, which is the failure that does not need refunding.
        /// </summary>
        public static bool TryBuy(Tool tool)
        {
            if (!TrySpend(Economy.Price(tool))) return false;

            GrantTool(tool);
            return true;
        }

        // --- housekeeping --------------------------------------------------------------------

        private static string Key(Tool tool) => ToolKeyPrefix + (int)tool;

        private static void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, Mathf.Max(0, value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Gives a brand-new player their opening balance, exactly once.
        ///
        /// Guarded by its own flag rather than by "are the coins zero", because a player who has
        /// spent everything is at zero too and must not be topped up again.
        /// </summary>
        private static void EnsureSeeded()
        {
            if (PlayerPrefs.GetInt(SeededKey, 0) == 1) return;

            PlayerPrefs.SetInt(SeededKey, 1);
            PlayerPrefs.SetInt(CoinsKey, StartingCoins);
            PlayerPrefs.Save();
        }

        /// <summary>Wipes the wallet. Used by the screenshot harness, which must start clean.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(SeededKey);
            foreach (Tool tool in Enum.GetValues(typeof(Tool))) PlayerPrefs.DeleteKey(Key(tool));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
