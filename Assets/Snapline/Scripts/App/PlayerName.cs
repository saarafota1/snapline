using System.Threading.Tasks;
using Snapline.Core;
using UnityEngine;
#if SNAPLINE_UGS_AUTH
using Unity.Services.Authentication;
#endif

namespace Snapline.App
{
    /// <summary>
    /// The name a player appears under on the world board.
    ///
    /// Unity gives every anonymous player a generated name like HARSHSHAKING, which is fine until a
    /// player wants to find themselves on a board. This lets them pick one. Player accounts come
    /// later; the name is not a login and nothing is recovered with it.
    ///
    /// Unity keeps the authority: it stores the name against the anonymous player id and appends a
    /// four-digit tag of its own, so two people can both be SAARA without a fight over the name.
    /// </summary>
    public static class PlayerName
    {
        public const int MinLength = 3;

        /// <summary>Unity allows 50; a leaderboard row does not, and a row that fits is worth more.</summary>
        public const int MaxLength = 14;

        private const string Key = "snapline.playername";

        /// <summary>What the player chose, as far as this device knows. Empty when they never chose.</summary>
        public static string Current => PlayerPrefs.GetString(Key, string.Empty);

        public static bool Chosen => !string.IsNullOrEmpty(Current);

        /// <summary>Why this name cannot be used, or null when it can.</summary>
        public static string Problem(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Pick a name first.";

            string trimmed = name.Trim();
            if (trimmed.Length < MinLength) return $"At least {MinLength} letters, please.";
            if (trimmed.Length > MaxLength) return $"Up to {MaxLength} letters, please.";

            foreach (char c in trimmed)
            {
                // Unity refuses whitespace outright; the rest is ours, so a name stays readable in a row.
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') continue;
                return "Letters and numbers only.";
            }

            // A name strangers see is moderated content. Refused without repeating the word back,
            // which only teaches what the filter is looking for.
            if (NameFilter.IsBlocked(trimmed)) return "Pick a different name, please.";

            return null;
        }

        /// <summary>
        /// Sends the name to Unity and remembers it here. False if it was refused - not signed in,
        /// no network, or a name the service would not take - and the old name stays.
        /// </summary>
        public static async Task<bool> SetAsync(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (Problem(trimmed) != null) return false;

#if SNAPLINE_UGS_AUTH
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.LogWarning("[Snapline] player name: not signed in yet, keeping the old name.");
                    return false;
                }

                await AuthenticationService.Instance.UpdatePlayerNameAsync(trimmed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Snapline] player name refused: " + e.Message);
                return false;
            }
#else
            await Task.CompletedTask;
#endif

            PlayerPrefs.SetString(Key, trimmed);
            PlayerPrefs.Save();
            Debug.Log("[Snapline] player name set to " + trimmed);
            return true;
        }
    }
}
