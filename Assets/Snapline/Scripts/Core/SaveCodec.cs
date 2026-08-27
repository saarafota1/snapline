using System;
using System.Globalization;
using System.Text;

namespace Snapline.Core
{
    /// <summary>
    /// Serialises a run to a single string and back.
    ///
    /// Hand-rolled rather than JsonUtility because that lives in UnityEngine, and this assembly
    /// deliberately cannot see it. The upside is that save/load is covered by the same unit tests
    /// as the rest of the engine, instead of only being exercised on device.
    ///
    /// The payload carries a checksum. A truncated save — the app killed mid-write is the usual
    /// cause — then fails loudly at load and starts a fresh run, rather than restoring half a board
    /// and looking like a gameplay bug.
    /// </summary>
    public static class SaveCodec
    {
        private const string Magic = "SNAP";
        private const int CurrentVersion = 1;
        private const char Sep = '|';

        public static string Encode(RunSnapshot snap)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));

            var sb = new StringBuilder(256);
            sb.Append(Magic).Append(CurrentVersion).Append(Sep);
            sb.Append(snap.Occupied.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(EncodeBytes(snap.Colours)).Append(Sep);
            sb.Append(EncodeInts(snap.TrayShapeIds)).Append(Sep);
            sb.Append(EncodeBytes(snap.TrayColours)).Append(Sep);
            sb.Append(EncodeBools(snap.TrayConsumed)).Append(Sep);
            sb.Append(snap.RngState.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.Score.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.ComboCount.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.BestCombo.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.TotalLinesCleared.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.TotalPiecesPlaced.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.BestSimultaneousLines.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(snap.GameOver ? '1' : '0');

            string payload = sb.ToString();
            return payload + Sep + Checksum(payload).ToString("X8", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Decode a save. Returns null for anything malformed, unversioned, or checksum-failing.
        /// Callers treat null as "no run in progress".
        /// </summary>
        public static RunSnapshot Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            int lastSep = text.LastIndexOf(Sep);
            if (lastSep <= 0 || lastSep == text.Length - 1) return null;

            string payload = text.Substring(0, lastSep);
            string checksumText = text.Substring(lastSep + 1);

            if (!uint.TryParse(checksumText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint stored))
                return null;
            if (stored != Checksum(payload)) return null;

            string[] f = payload.Split(Sep);
            if (f.Length < 14) return null;
            if (!f[0].StartsWith(Magic, StringComparison.Ordinal)) return null;

            if (!int.TryParse(f[0].Substring(Magic.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                return null;
            if (version > CurrentVersion) return null; // written by a newer build; do not guess

            try
            {
                return new RunSnapshot
                {
                    Version = version,
                    Occupied = ulong.Parse(f[1], CultureInfo.InvariantCulture),
                    Colours = DecodeBytes(f[2]),
                    TrayShapeIds = DecodeInts(f[3]),
                    TrayColours = DecodeBytes(f[4]),
                    TrayConsumed = DecodeBools(f[5]),
                    RngState = ulong.Parse(f[6], CultureInfo.InvariantCulture),
                    Score = long.Parse(f[7], CultureInfo.InvariantCulture),
                    ComboCount = int.Parse(f[8], CultureInfo.InvariantCulture),
                    BestCombo = int.Parse(f[9], CultureInfo.InvariantCulture),
                    TotalLinesCleared = int.Parse(f[10], CultureInfo.InvariantCulture),
                    TotalPiecesPlaced = int.Parse(f[11], CultureInfo.InvariantCulture),
                    BestSimultaneousLines = int.Parse(f[12], CultureInfo.InvariantCulture),
                    GameOver = f[13] == "1",
                };
            }
            catch (FormatException) { return null; }
            catch (OverflowException) { return null; }
            catch (ArgumentException) { return null; }
        }

        // --- field codecs ---------------------------------------------------------------

        private static string EncodeBytes(byte[] v)
        {
            if (v == null || v.Length == 0) return string.Empty;
            var sb = new StringBuilder(v.Length * 2);
            for (int i = 0; i < v.Length; i++) sb.Append(v[i].ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static byte[] DecodeBytes(string s)
        {
            if (string.IsNullOrEmpty(s)) return new byte[0];
            if ((s.Length & 1) != 0) throw new FormatException("Odd-length byte field.");
            var v = new byte[s.Length / 2];
            for (int i = 0; i < v.Length; i++)
                v[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return v;
        }

        private static string EncodeInts(int[] v)
        {
            if (v == null || v.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(v[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static int[] DecodeInts(string s)
        {
            if (string.IsNullOrEmpty(s)) return new int[0];
            string[] parts = s.Split(',');
            var v = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) v[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            return v;
        }

        private static string EncodeBools(bool[] v)
        {
            if (v == null || v.Length == 0) return string.Empty;
            var sb = new StringBuilder(v.Length);
            for (int i = 0; i < v.Length; i++) sb.Append(v[i] ? '1' : '0');
            return sb.ToString();
        }

        private static bool[] DecodeBools(string s)
        {
            if (string.IsNullOrEmpty(s)) return new bool[0];
            var v = new bool[s.Length];
            for (int i = 0; i < s.Length; i++) v[i] = s[i] == '1';
            return v;
        }

        /// <summary>FNV-1a. Not cryptographic — this catches truncation, not tampering.</summary>
        private static uint Checksum(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
