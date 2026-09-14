using System.Collections.Generic;
using UnityEngine;

namespace Snapline.Art
{
    /// <summary>
    /// Every sound effect in the game, played from recorded cues.
    ///
    /// The cues are Sparkwick's recorded set, shipped under <c>Resources/Snapline/Sound</c>. The kit's
    /// synthesised bank this replaces was written for placeholder beeps, and a candy game whose
    /// every clear goes "bip" undoes all the work the artwork does.
    ///
    /// One recording becomes many sounds by playback rate: a single pop climbs a pentatonic scale as
    /// a combo grows, so a streak audibly rises instead of repeating, and it cannot turn sour however
    /// long it runs. A fixed pool of voices plays them, because a big clear fires a dozen overlapping
    /// cues in a second and creating a source per cue would allocate constantly.
    /// </summary>
    public static class Sound
    {
        private const int VoiceCount = 14;

        /// <summary>Notes a combo climbs through. Pentatonic, so a long streak cannot sour.</summary>
        private static readonly float[] Scale = { 1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f, 2.25f, 2.5f, 3f };

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, int> Played = new Dictionary<string, int>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        private static AudioSource[] _voices;
        private static int _next;
        private static float _lastTap;
        private static string _lastPrize;
        private static readonly System.Random Rng = new System.Random(20260914);

        /// <summary>Silences everything without unbuilding it. Driven by the sound setting.</summary>
        public static bool Muted;

        public static bool Ready => _voices != null;

        /// <summary>How many recorded cues were found, so a check can prove they shipped.</summary>
        public static int ClipCount => Clips.Count;

        public static void Init(Transform parent)
        {
            if (_voices != null) return;

            var go = new GameObject("Sound");
            go.transform.SetParent(parent, false);

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }

            AudioClip[] clips = Resources.LoadAll<AudioClip>("Snapline/Sound");
            foreach (AudioClip clip in clips)
                if (clip != null) Clips[clip.name] = clip;

            Debug.Log($"[Snapline] sound: {Clips.Count} recorded cues loaded");
        }

        // --- playback ------------------------------------------------------------------------

        private static void Play(string cue, float volume, float pitch = 1f, float delay = 0f)
        {
            if (Muted || _voices == null) return;

            if (!Clips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                // Said once, not on every clear: a missing file is a packaging fault worth one line.
                if (Missing.Add(cue)) Debug.LogWarning($"[Snapline] sound: no cue named '{cue}'");
                return;
            }

            Played.TryGetValue(cue, out int n);
            Played[cue] = n + 1;

            AudioSource src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
            if (delay > 0f) src.PlayDelayed(delay);
            else src.Play();
        }

        private static float Vary(float spread) => 1f + ((float)Rng.NextDouble() * 2f - 1f) * spread;

        /// <summary>
        /// How many times a cue has played this session. For the smoke run, which cannot hear: a
        /// sound wired to a moment that never calls it is silent in a way no screenshot shows.
        /// </summary>
        public static int PlayCount(string cue) => Played.TryGetValue(cue, out int n) ? n : 0;

        // --- interface -----------------------------------------------------------------------

        /// <summary>A button press. Throttled, so a double tap is one click rather than a flam.</summary>
        public static void Tap()
        {
            if (Time.unscaledTime - _lastTap < 0.04f) return;
            _lastTap = Time.unscaledTime;
            Play("tap", 0.55f, Vary(0.05f));
        }

        public static void Toggle(bool on) => Play("tap", 0.55f, on ? 1.25f : 0.9f);

        /// <summary>A popup opens: a soft upward bounce.</summary>
        public static void Open() => Play("bounce", 0.45f, 1.15f);

        public static void Close() => Play("bounce", 0.32f, 0.85f);

        /// <summary>A screen slides in.</summary>
        public static void Swoosh() => Play("shoot", 0.28f, 1.3f);

        public static void Sparkle() => Play("mascot_sparkle", 0.35f, Vary(0.04f));

        public static void Unlock() => Play("star", 0.5f, 1.15f);

        public static void Deny() => Play("deny", 0.5f);

        // --- play ----------------------------------------------------------------------------

        public static void Pickup() => Play("bounce", 0.38f, 1.35f * Vary(0.03f));

        public static void Place() => Play("stick", 0.62f, Vary(0.06f));

        /// <summary>
        /// Lines cleared. The pop climbs the scale with the combo, and every line past the first
        /// adds one more note a beat later, so a triple is audibly bigger than a single without
        /// simply being louder.
        /// </summary>
        public static void Clear(int lines, int combo)
        {
            int step = Mathf.Clamp(combo - 1, 0, Scale.Length - 1);
            Play("pop", 0.7f, Scale[step] * Vary(0.015f));

            for (int i = 1; i < Mathf.Min(lines, 4); i++)
                Play("pop", 0.55f, Scale[Mathf.Min(step + i * 2, Scale.Length - 1)], i * 0.07f);

            if (lines >= 2) Play("star", 0.45f, 1f + 0.08f * Mathf.Min(lines, 5), 0.05f);
            if (lines >= 3) Play("takeover_good_" + (1 + Rng.Next(3)), 0.6f, 1f, 0.02f);
        }

        /// <summary>A combo that deserves its own sting on top of the clear.</summary>
        public static void Combo(int combo)
        {
            if (combo < 3) return;
            Play("star", 0.4f, 1f + 0.07f * Mathf.Min(combo, 10), 0.12f);
        }

        /// <summary>The whole board cleared at once.</summary>
        public static void Perfect() => Play("takeover_huge", 0.85f);

        /// <summary>A personal best beaten mid-run, or a record on the results card.</summary>
        public static void NewBest() => Play("hype", 0.6f);

        public static void Invalid() => Play("deny", 0.4f, 1.1f);

        /// <summary>The shuffle tool deals a fresh tray.</summary>
        public static void Whoosh() => Play("shoot", 0.62f, 0.85f);

        /// <summary>The hammer lands.</summary>
        public static void Smash() => Play("bomb", 0.6f, 1.2f);

        /// <summary>The undo tool rewinds a move.</summary>
        public static void Rewind()
        {
            Play("shoot", 0.5f, 0.62f);
            Play("bounce", 0.4f, 0.8f, 0.12f);
        }

        /// <summary>A star is lost off the level's star bar.</summary>
        public static void StarLost() => Play("drop", 0.4f, 1.35f);

        public static void GameOver() => Play("lose", 0.62f);

        // --- rewards -------------------------------------------------------------------------

        public static void Coin() => Play("coin", 0.42f, Vary(0.08f));

        /// <summary>
        /// A cascade of coins for a payout that flies across the screen. One clink under sixteen
        /// visibly moving coins reads as one coin; these climb slightly, so a big payout sounds big.
        /// </summary>
        public static void Coins(int count)
        {
            int voices = Mathf.Clamp(count / 3, 3, 6);
            for (int i = 0; i < voices; i++) Play("coin", 0.4f, 1f + i * 0.05f, i * 0.075f);
        }

        public static void Purchase() => Play("purchase", 0.7f);

        /// <summary>A star lands on a result card: 1, 2 or 3, each higher than the last.</summary>
        public static void Star(int index) =>
            Play("star_earned", 0.75f, Mathf.Pow(1.26f, Mathf.Clamp(index, 1, 3) - 1));

        public static void Win() => Play("win", 0.75f);

        public static void Chest() => Play("chest", 0.7f);

        /// <summary>
        /// A prize handed over. Three takes, never the same one twice running, so the daily reward
        /// does not turn into wallpaper within the first week.
        /// </summary>
        public static void Prize()
        {
            string pick;
            do pick = "prize_" + (1 + Rng.Next(3));
            while (pick == _lastPrize);
            _lastPrize = pick;
            Play(pick, 0.72f);
        }
    }
}
