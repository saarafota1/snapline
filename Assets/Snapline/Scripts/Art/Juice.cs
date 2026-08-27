using UnityEngine;
using UnityEngine.UI;

namespace Snapline.Art
{
    /// <summary>
    /// Particles, screen shake and floating popups.
    ///
    /// Particles are pooled UI Images sharing one generated sprite, so however many are alive they
    /// stay in a single draw call. The simulation is a flat array walked in Update — no per-particle
    /// GameObject churn, no allocation once the pool is warm.
    /// </summary>
    public sealed class Juice : MonoBehaviour
    {
        private struct Particle
        {
            public bool Alive;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float EndSize;
            public float Spin;
            public float Angle;
            public Color Colour;
            public RectTransform Transform;
            public Image Image;
        }

        private const int PoolSize = 512;

        private Particle[] _particles;
        private int _nextParticle;

        private RectTransform _particleLayer;
        private RectTransform _popupLayer;
        private RectTransform _shakeTarget;
        private Vector2 _shakeHome;

        private float _trauma;
        private float _traumaDecay = 1.8f;
        private float _shakeSeed;

        private PopupLabel[] _popups;
        private int _nextPopup;
        private const int PopupPoolSize = 24;

        /// <summary>Gravity applied to debris, in canvas units per second squared.</summary>
        public float Gravity = -2600f;

        /// <summary>Air drag, per second. Keeps bursts from flying off screen.</summary>
        public float Drag = 1.6f;

        public float MaxShakeOffset = 34f;

        private sealed class PopupLabel
        {
            public RectTransform Transform;
            public Text Text;
            public Outline Outline;
            public float Life;
            public float MaxLife;
            public Vector2 Velocity;
            public float StartSize;
            public float PeakSize;
            public bool Alive;
        }

        public void Init(RectTransform particleLayer, RectTransform popupLayer, RectTransform shakeTarget)
        {
            _particleLayer = particleLayer;
            _popupLayer = popupLayer;
            _shakeTarget = shakeTarget;
            if (_shakeTarget != null) _shakeHome = _shakeTarget.anchoredPosition;
            _shakeSeed = Random.value * 100f;

            BuildParticlePool();
            BuildPopupPool();
        }

        private void BuildParticlePool()
        {
            Sprite dot = ArtKit.SoftCircle();
            _particles = new Particle[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("p", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_particleLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = go.GetComponent<Image>();
                img.sprite = dot;
                img.raycastTarget = false;

                go.SetActive(false);

                _particles[i].Transform = rt;
                _particles[i].Image = img;
            }
        }

        private void BuildPopupPool()
        {
            _popups = new PopupLabel[PopupPoolSize];
            for (int i = 0; i < PopupPoolSize; i++)
            {
                var go = new GameObject("popup", typeof(RectTransform), typeof(Text), typeof(Outline));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_popupLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600f, 140f);

                var text = go.GetComponent<Text>();
                text.font = UIKit.Font;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.fontStyle = FontStyle.Bold;

                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
                outline.effectDistance = new Vector2(3f, -3f);

                go.SetActive(false);

                _popups[i] = new PopupLabel { Transform = rt, Text = text, Outline = outline };
            }
        }

        // --- emitters --------------------------------------------------------------------

        /// <summary>Radial burst of debris. Used for every cleared cell.</summary>
        public void Burst(Vector2 position, Color colour, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float mag = speed * Random.Range(0.35f, 1.25f);
                var vel = new Vector2(Mathf.Cos(angle) * mag, Mathf.Sin(angle) * mag);
                Spawn(position, vel, colour, Random.Range(size * 0.5f, size), Random.Range(0.38f, 0.78f));
            }
        }

        /// <summary>Bright flash that expands and fades. One per cleared line, at its centre.</summary>
        public void Flash(Vector2 position, Color colour, float size, float life = 0.32f)
        {
            Particle p = Take();
            if (p.Transform == null) return;
            int idx = _nextParticle == 0 ? PoolSize - 1 : _nextParticle - 1;

            _particles[idx].Alive = true;
            _particles[idx].Position = position;
            _particles[idx].Velocity = Vector2.zero;
            _particles[idx].Life = life;
            _particles[idx].MaxLife = life;
            _particles[idx].Size = size * 0.4f;
            _particles[idx].EndSize = size * 1.8f;
            _particles[idx].Spin = 0f;
            _particles[idx].Angle = 0f;
            _particles[idx].Colour = colour;
            _particles[idx].Transform.gameObject.SetActive(true);
        }

        private void Spawn(Vector2 position, Vector2 velocity, Color colour, float size, float life)
        {
            Take();
            int idx = _nextParticle == 0 ? PoolSize - 1 : _nextParticle - 1;

            _particles[idx].Alive = true;
            _particles[idx].Position = position;
            _particles[idx].Velocity = velocity;
            _particles[idx].Life = life;
            _particles[idx].MaxLife = life;
            _particles[idx].Size = size;
            _particles[idx].EndSize = size * 0.15f;
            _particles[idx].Angle = Random.value * 360f;
            _particles[idx].Spin = Random.Range(-420f, 420f);
            _particles[idx].Colour = colour;
            _particles[idx].Transform.gameObject.SetActive(true);
        }

        /// <summary>Round-robin over the pool. Oldest particle is recycled when it wraps.</summary>
        private Particle Take()
        {
            Particle p = _particles[_nextParticle];
            _nextParticle = (_nextParticle + 1) % PoolSize;
            return p;
        }

        public void Popup(Vector2 position, string message, Color colour, float fontSize = 64f, float life = 0.95f)
        {
            PopupLabel label = _popups[_nextPopup];
            _nextPopup = (_nextPopup + 1) % PopupPoolSize;

            // Popups are anchored over the piece the player dropped, which near the board edge puts
            // half the word off screen. Clamp so the text always reads in full.
            float halfWidth = _popupLayer.rect.width * 0.5f;
            float margin = Mathf.Min(halfWidth * 0.5f, message.Length * fontSize * 0.28f);
            position.x = Mathf.Clamp(position.x, -halfWidth + margin, halfWidth - margin);

            label.Alive = true;
            label.Life = life;
            label.MaxLife = life;
            label.Velocity = new Vector2(Random.Range(-40f, 40f), 320f);
            label.StartSize = fontSize * 0.4f;
            label.PeakSize = fontSize;
            label.Transform.anchoredPosition = position;
            label.Transform.localScale = Vector3.one;
            label.Text.text = message;
            label.Text.color = colour;
            label.Text.fontSize = Mathf.RoundToInt(fontSize);
            label.Transform.gameObject.SetActive(true);
        }

        /// <summary>Add screen shake. Trauma accumulates and decays; offset scales with its square.</summary>
        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        // --- simulation ------------------------------------------------------------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f; // a hitch must not teleport every particle off screen

            SimulateParticles(dt);
            SimulatePopups(dt);
            SimulateShake(dt);
        }

        private void SimulateParticles(float dt)
        {
            if (_particles == null) return;

            float dragFactor = Mathf.Exp(-Drag * dt);

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Alive) continue;

                _particles[i].Life -= dt;
                if (_particles[i].Life <= 0f)
                {
                    _particles[i].Alive = false;
                    _particles[i].Transform.gameObject.SetActive(false);
                    continue;
                }

                bool isFlash = _particles[i].EndSize > _particles[i].Size;

                if (!isFlash)
                {
                    _particles[i].Velocity.y += Gravity * dt;
                    _particles[i].Velocity *= dragFactor;
                    _particles[i].Position += _particles[i].Velocity * dt;
                    _particles[i].Angle += _particles[i].Spin * dt;
                }

                float t = 1f - _particles[i].Life / _particles[i].MaxLife;
                float size = Mathf.Lerp(_particles[i].Size, _particles[i].EndSize, isFlash ? EaseOut(t) : t * t);

                Color c = _particles[i].Colour;
                c.a *= isFlash ? 1f - EaseOut(t) : Mathf.Clamp01(1f - t * t);

                RectTransform rt = _particles[i].Transform;
                rt.anchoredPosition = _particles[i].Position;
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.Euler(0f, 0f, _particles[i].Angle);
                _particles[i].Image.color = c;
            }
        }

        private void SimulatePopups(float dt)
        {
            if (_popups == null) return;

            for (int i = 0; i < _popups.Length; i++)
            {
                PopupLabel p = _popups[i];
                if (!p.Alive) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }

                float t = 1f - p.Life / p.MaxLife;

                p.Velocity.y -= 620f * dt;
                p.Transform.anchoredPosition += p.Velocity * dt;

                // Overshoot on the way in, then settle: the pop that makes it read as a reward.
                float scale = t < 0.25f
                    ? Mathf.Lerp(0.4f, 1.18f, EaseOut(t / 0.25f))
                    : Mathf.Lerp(1.18f, 1.0f, EaseOut(Mathf.Clamp01((t - 0.25f) / 0.25f)));
                p.Transform.localScale = Vector3.one * scale;

                Color c = p.Text.color;
                c.a = t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
                p.Text.color = c;

                Color oc = p.Outline.effectColor;
                oc.a = 0.65f * c.a;
                p.Outline.effectColor = oc;
            }
        }

        private void SimulateShake(float dt)
        {
            if (_shakeTarget == null) return;

            if (_trauma <= 0f)
            {
                _shakeTarget.anchoredPosition = _shakeHome;
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - _traumaDecay * dt);

            // Squaring the trauma makes small hits subtle and big ones violent, rather than every
            // shake feeling like the same buzz.
            float magnitude = _trauma * _trauma * MaxShakeOffset;
            float time = Time.unscaledTime * 34f;

            float x = (Mathf.PerlinNoise(_shakeSeed, time) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(_shakeSeed + 17.3f, time) - 0.5f) * 2f;

            _shakeTarget.anchoredPosition = _shakeHome + new Vector2(x, y) * magnitude;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
