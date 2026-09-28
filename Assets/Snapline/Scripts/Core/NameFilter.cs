using System.Text;

namespace Snapline.Core
{
    /// <summary>
    /// What a player may not call themselves on the world board.
    ///
    /// A display name strangers can see is user-generated content, and Google Play expects a game
    /// carrying it to moderate it and to offer a way to report what gets through. This is the
    /// moderation half; the report route is on the WORLD table of the scores screen.
    ///
    /// It is a blocklist, so it is beatable by anyone determined - the point is to stop the casual
    /// case, not to win an arms race. Names are normalised first (case folded, separators dropped,
    /// the obvious digit-for-letter swaps undone) so that the usual dodges do not walk straight
    /// through. If a board ever needs more than this, the honest fix is to stop taking free text and
    /// hand out names from a list instead.
    /// </summary>
    public static class NameFilter
    {
        /// <summary>
        /// Stems, matched anywhere in the normalised name. Deliberately short: slurs and the
        /// strongest profanity, not mild words, because a filter that rejects "damn" mostly teaches
        /// players that the name box is broken.
        /// </summary>
        private static readonly string[] Blocked =
        {
            "fuck", "shit", "cunt", "bitch", "bastard", "wanker", "bollock", "arsehole", "asshole",
            "dick", "cock", "penis", "vagina", "pussy", "boob", "tits", "porn", "rape", "slut",
            "whore", "nigg", "fagg", "kike", "spic", "chink", "paki", "coon", "tranny", "retard",
            "hitler", "nazi", "isis", "jihad", "kkk",
            "admin", "moderator", "snapline", "scibox", "support",
        };

        /// <summary>True when the name carries something the board will not print.</summary>
        public static bool IsBlocked(string name)
        {
            string flat = Normalise(name);
            if (flat.Length == 0) return false;

            foreach (string word in Blocked)
                if (flat.Contains(word)) return true;

            return false;
        }

        /// <summary>
        /// Case folded, separators removed, and the swaps people reach for first undone, so that
        /// "N-i-c-e" and "n1ce" and "N.I.C.E" all read the same to the check below.
        /// </summary>
        private static string Normalise(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var sb = new StringBuilder(name.Length);
            foreach (char raw in name.ToLowerInvariant())
            {
                char c = raw switch
                {
                    '0' => 'o',
                    '1' => 'i',
                    '3' => 'e',
                    '4' => 'a',
                    '5' => 's',
                    '7' => 't',
                    '8' => 'b',
                    '@' => 'a',
                    '$' => 's',
                    '!' => 'i',
                    _ => raw,
                };

                if (char.IsLetter(c)) sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
