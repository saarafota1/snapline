using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Snapline.Art
{
    /// <summary>
    /// The background loop.
    ///
    /// A recorded track wins if one is ever shipped as <c>Resources/Snapline/Music/music</c>. Until
    /// then the loop is composed here: a music box over a soft pad, eight bars of I–vi–IV–V in C,
    /// which is about the gentlest progression there is and cannot clash with any cue in the sound
    /// bank. It is rendered once, on a worker thread, so the menu never waits for it.
    ///
    /// Every note that rings past the end of the loop is wrapped back onto its start, which is what
    /// makes the seam inaudible — a loop that simply stops mid-decay clicks every eighteen seconds.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float Bpm = 100f;
        private const int Bars = 8;
        private const float Volume = 0.42f;

        public static Music Instance { get; private set; }

        private AudioSource _source;
        private float _target;
        private bool _ready;

        public static Music Create(Transform parent, bool enabled)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            Music music = go.AddComponent<Music>();
            music._source = go.AddComponent<AudioSource>();
            music._source.playOnAwake = false;
            music._source.loop = true;
            music._source.spatialBlend = 0f;
            music._source.volume = 0f;
            music._target = enabled ? Volume : 0f;
            Instance = music;
            music.StartCoroutine(music.Load());
            return music;
        }

        /// <summary>Fades rather than cutting, so toggling it in the pause card is not a jolt.</summary>
        public void SetEnabled(bool on) => _target = on ? Volume : 0f;

        private IEnumerator Load()
        {
            AudioClip recorded = Resources.Load<AudioClip>("Snapline/Music/music");
            if (recorded != null)
            {
                Begin(recorded);
                yield break;
            }

            Task<float[]> render = Task.Run(Render);
            while (!render.IsCompleted) yield return null;

            if (render.IsFaulted || render.Result == null)
            {
                Debug.LogWarning("[Snapline] music: render failed, playing without it");
                yield break;
            }

            float[] data = render.Result;
            AudioClip clip = AudioClip.Create("music", data.Length / 2, 2, Rate, false);
            clip.SetData(data, 0);
            Begin(clip);
        }

        private void Begin(AudioClip clip)
        {
            _source.clip = clip;
            _source.Play();
            _ready = true;
            Debug.Log($"[Snapline] music: {clip.name} {clip.length:F1}s ready");
        }

        private void Update()
        {
            if (!_ready) return;
            _source.volume = Mathf.MoveTowards(_source.volume, _target, Time.unscaledDeltaTime * 0.8f);
        }

        // --- composition -----------------------------------------------------------------------

        private static float[] _left, _right;
        private static int _frames;

        private static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        private static float[] Render()
        {
            float beat = 60f / Bpm;
            _frames = Mathf.RoundToInt(Bars * 4 * beat * Rate);
            _left = new float[_frames];
            _right = new float[_frames];

            // Chord tones as semitones above C. The last bar adds the seventh, so the turnaround
            // leans back into the first bar instead of landing.
            int[][] chords =
            {
                new[] { 0, 4, 7 }, new[] { -3, 0, 4 }, new[] { -7, -3, 0 }, new[] { -5, -1, 2 },
                new[] { 0, 4, 7 }, new[] { -3, 0, 4 }, new[] { -7, -3, 0 }, new[] { -5, -1, 2, 5 },
            };

            // Melody, one row per bar, as indices into the chord (-1 is a rest). Written rather than
            // randomised: a generated tune wanders, and this one has to be hummable on the tenth loop.
            int[][] melody =
            {
                new[] { 2, -1, 1, 0 }, new[] { 2, -1, 3, -1 }, new[] { 1, 2, 1, -1 }, new[] { 2, -1, 1, -1 },
                new[] { 2, -1, 3, 2 }, new[] { 1, -1, 2, -1 }, new[] { 2, 1, 0, -1 }, new[] { 1, -1, -1, -1 },
            };

            int[] arpeggio = { 0, 1, 2, 3, 2, 1, 2, 1 };
            var random = new System.Random(7);

            for (int bar = 0; bar < Bars; bar++)
            {
                int[] c = chords[bar];
                float barStart = bar * 4 * beat;

                // Bass on beats one and three.
                AddNote(barStart, 1.8f * beat, Hz(36 + 12 + c[0]), 0.20f, 0f, 2);
                AddNote(barStart + 2 * beat, 1.8f * beat, Hz(36 + 12 + c[0]), 0.16f, 0f, 2);

                // A pad under the whole bar.
                for (int k = 0; k < 3; k++)
                    AddNote(barStart, 4 * beat + 0.7f, Hz(60 + c[k]), 0.040f, (k - 1) * 0.4f, 1);

                // Music box in eighths.
                for (int i = 0; i < 8; i++)
                {
                    int idx = arpeggio[i];
                    float tone = idx < 3 ? c[Mathf.Min(idx, c.Length - 1)] : c[0] + 12;
                    float amp = (i % 2 == 0 ? 0.085f : 0.06f) * (0.9f + 0.2f * (float)random.NextDouble());
                    AddNote(barStart + i * beat * 0.5f, 1.3f, Hz(72 + tone), amp, i % 2 == 0 ? -0.3f : 0.3f, 0);
                }

                // The tune on top, an octave up.
                for (int q = 0; q < 4; q++)
                {
                    int m = melody[bar][q];
                    if (m < 0) continue;
                    float tone = c[Mathf.Min(m, c.Length - 1)];
                    AddNote(barStart + q * beat, 1.6f, Hz(84 + tone), 0.07f, 0.1f, 0);
                }

                if (bar % 2 == 0) AddNote(barStart, 2.4f, Hz(96 + c[0]), 0.025f, -0.2f, 3);
            }

            // Normalise with a soft ceiling, then interleave.
            float peak = 0.001f;
            for (int i = 0; i < _frames; i++)
                peak = Mathf.Max(peak, Mathf.Abs(_left[i]), Mathf.Abs(_right[i]));

            float gain = 0.78f / peak;
            var data = new float[_frames * 2];
            for (int i = 0; i < _frames; i++)
            {
                data[i * 2] = Soft(_left[i] * gain);
                data[i * 2 + 1] = Soft(_right[i] * gain);
            }

            _left = _right = null;
            return data;
        }

        private static float Soft(float x) => x / (1f + Mathf.Abs(x) * 0.35f);

        /// <summary>
        /// Mixes one note into the loop. Kinds: 0 music box, 1 pad, 2 bass, 3 bell. Samples past the
        /// end of the loop wrap round to its start.
        /// </summary>
        private static void AddNote(float start, float duration, float hz, float amp, float pan, int kind)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(duration * Rate);
            float twoPi = Mathf.PI * 2f;
            float gainL = amp * (1f - Mathf.Max(0f, pan));
            float gainR = amp * (1f + Mathf.Min(0f, pan));

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float l, r;

                switch (kind)
                {
                    case 0:
                    {
                        float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4.2f);
                        float v = Mathf.Sin(twoPi * hz * t)
                                + 0.32f * Mathf.Sin(twoPi * hz * 2f * t) * Mathf.Exp(-t * 9f)
                                + 0.10f * Mathf.Sin(twoPi * hz * 3f * t) * Mathf.Exp(-t * 15f);
                        l = r = v * env;
                        break;
                    }
                    case 1:
                    {
                        float attack = Mathf.Clamp01(t / 0.6f);
                        float release = Mathf.Clamp01((duration - t) / 0.7f);
                        float env = attack * attack * (3f - 2f * attack) * release;
                        l = (Mathf.Sin(twoPi * hz * 0.9985f * t) + 0.3f * Mathf.Sin(twoPi * hz * 2.003f * t)) * env;
                        r = (Mathf.Sin(twoPi * hz * 1.0015f * t) + 0.3f * Mathf.Sin(twoPi * hz * 1.997f * t)) * env;
                        break;
                    }
                    case 2:
                    {
                        float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 2.4f);
                        l = r = (Mathf.Sin(twoPi * hz * t) + 0.22f * Mathf.Sin(twoPi * hz * 2f * t)) * env;
                        break;
                    }
                    default:
                    {
                        float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 2.6f);
                        l = r = (0.6f * Mathf.Sin(twoPi * hz * t)
                                 + 0.25f * Mathf.Sin(twoPi * hz * 2.76f * t) * Mathf.Exp(-t * 5f)) * env;
                        break;
                    }
                }

                int idx = (s0 + i) % _frames;
                _left[idx] += l * gainL;
                _right[idx] += r * gainR;
            }
        }
    }
}
