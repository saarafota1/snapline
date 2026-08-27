using UnityEngine;

namespace Snapline.Art
{
    /// <summary>
    /// Synthesises every sound effect at runtime.
    ///
    /// Same reasoning as the sprites: no audio files to import, license, or keep in the repo, and
    /// the pitch of a clear can scale with how many lines went at once because the clip is
    /// generated rather than played back. Everything is a short envelope over a few oscillators —
    /// a handful of kilobytes of float data, built once at startup.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;

        private AudioClip _place;
        private AudioClip _invalid;
        private AudioClip[] _clear;      // indexed by lines cleared, 1..6
        private AudioClip[] _combo;      // indexed by combo step
        private AudioClip _gameOver;
        private AudioClip _perfect;

        public bool Muted { get; set; }

        public void Init()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.65f;

            _place = BuildPlace();
            _invalid = BuildInvalid();
            _gameOver = BuildGameOver();
            _perfect = BuildPerfect();

            _clear = new AudioClip[7];
            for (int lines = 1; lines <= 6; lines++) _clear[lines] = BuildClear(lines);

            _combo = new AudioClip[9];
            for (int step = 1; step <= 8; step++) _combo[step] = BuildCombo(step);
        }

        // --- playback ---------------------------------------------------------------------

        private void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (Muted || clip == null || _source == null) return;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
        }

        public void PlayPlace() => Play(_place, 0.55f, Random.Range(0.94f, 1.07f));
        public void PlayInvalid() => Play(_invalid, 0.4f);
        public void PlayGameOver() => Play(_gameOver, 0.7f);
        public void PlayPerfect() => Play(_perfect, 0.9f);

        public void PlayClear(int lines)
        {
            int i = Mathf.Clamp(lines, 1, 6);
            Play(_clear[i], 0.7f);
        }

        public void PlayCombo(int combo)
        {
            if (combo < 2) return;
            int i = Mathf.Clamp(combo - 1, 1, 8);
            Play(_combo[i], 0.5f);
        }

        // --- synthesis --------------------------------------------------------------------

        private static AudioClip Make(string name, float seconds, System.Func<float, float, float> generator)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)count;
                data[i] = Mathf.Clamp(generator(t, progress), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Exponential decay. The workhorse envelope for anything percussive.</summary>
        private static float Decay(float progress, float sharpness) => Mathf.Exp(-progress * sharpness);

        /// <summary>Short attack so a sound never starts with a click.</summary>
        private static float Attack(float progress, float length = 0.02f) =>
            progress < length ? progress / length : 1f;

        private static AudioClip BuildPlace()
        {
            // A low thump plus a brief click: reads as a solid object being set down.
            return Make("sfx_place", 0.10f, (t, p) =>
            {
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 150f, p) * t);
                float click = (Random.value * 2f - 1f) * Decay(p, 42f) * 0.35f;
                return (body * Decay(p, 16f) + click) * Attack(p) * 0.8f;
            });
        }

        private static AudioClip BuildInvalid()
        {
            return Make("sfx_invalid", 0.12f, (t, p) =>
            {
                float tone = Mathf.Sin(2f * Mathf.PI * 150f * t);
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 75f * t)) * 0.3f;
                return (tone + buzz) * Decay(p, 12f) * Attack(p) * 0.5f;
            });
        }

        private static AudioClip BuildClear(int lines)
        {
            // Rising sweep, starting higher and travelling further the more lines went at once.
            float startHz = 430f + 70f * (lines - 1);
            float endHz = startHz * (1.5f + 0.22f * lines);
            float length = 0.26f + 0.04f * lines;

            return Make($"sfx_clear{lines}", length, (t, p) =>
            {
                float hz = Mathf.Lerp(startHz, endHz, p * p);
                float fundamental = Mathf.Sin(2f * Mathf.PI * hz * t);
                float fifth = Mathf.Sin(2f * Mathf.PI * hz * 1.5f * t) * 0.4f;
                float sparkle = Mathf.Sin(2f * Mathf.PI * hz * 3f * t) * 0.18f * Decay(p, 6f);
                float noise = (Random.value * 2f - 1f) * Decay(p, 30f) * 0.12f;
                return (fundamental + fifth + sparkle + noise) * Decay(p, 4.5f) * Attack(p) * 0.62f;
            });
        }

        private static AudioClip BuildCombo(int step)
        {
            // Each combo step is a semitone higher, so a streak audibly climbs.
            float hz = 660f * Mathf.Pow(1.0595f, step * 2f);

            return Make($"sfx_combo{step}", 0.20f, (t, p) =>
            {
                float bell = Mathf.Sin(2f * Mathf.PI * hz * t);
                float overtone = Mathf.Sin(2f * Mathf.PI * hz * 2.76f * t) * 0.3f * Decay(p, 9f);
                return (bell + overtone) * Decay(p, 7f) * Attack(p) * 0.6f;
            });
        }

        private static AudioClip BuildPerfect()
        {
            return Make("sfx_perfect", 0.9f, (t, p) =>
            {
                // A rising arpeggio: four notes stepped through over the length of the clip.
                int note = Mathf.Clamp(Mathf.FloorToInt(p * 4f), 0, 3);
                float[] ratios = { 1f, 1.25f, 1.5f, 2f };
                float hz = 520f * ratios[note];
                float local = (p * 4f) - note;

                float tone = Mathf.Sin(2f * Mathf.PI * hz * t);
                float shimmer = Mathf.Sin(2f * Mathf.PI * hz * 2f * t) * 0.25f;
                return (tone + shimmer) * Decay(local, 4f) * Attack(local, 0.05f) * 0.55f;
            });
        }

        private static AudioClip BuildGameOver()
        {
            return Make("sfx_gameover", 0.85f, (t, p) =>
            {
                float hz = Mathf.Lerp(420f, 110f, p * p);
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t);
                float sub = Mathf.Sin(2f * Mathf.PI * hz * 0.5f * t) * 0.4f;
                return (tone + sub) * Decay(p, 2.6f) * Attack(p, 0.04f) * 0.6f;
            });
        }
    }
}
