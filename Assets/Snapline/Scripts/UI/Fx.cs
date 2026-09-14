using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Snapline.Art;
using GameKit.Art;

namespace Snapline.UI
{
    /// <summary>
    /// Every particle, flying coin, confetti shower, shout and screen shake in the game.
    ///
    /// One layer over the whole canvas, above every screen and popup, so an effect fired from a
    /// result card is never hidden by the card. Positions come in as world positions, so any
    /// screen can ask for sparkles "on this button" without knowing where that is on the layer.
    ///
    /// Particles are pooled Images walked in one loop; nothing is allocated once the pool is warm.
    /// The small sprites — sparkle star, glow, ring, sprinkle — are generated at startup rather
    /// than sliced from the sheets, because the sheet effects carry baked backgrounds and fixed
    /// compositions, and a particle has to be a single clean element that can be tinted and spun.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        private enum Fade : byte { Out, Flash, Twinkle }

        private struct Particle
        {
            public bool Alive;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Gravity;
            public float Drag;
            public float Life;
            public float MaxLife;
            public float StartSize;
            public float EndSize;
            public float Aspect;
            public float Angle;
            public float Spin;
            public Color Colour;
            public Fade Mode;
            public RectTransform Rect;
            public Image Image;
        }

        private const int PoolSize = 460;
        private const int TextPoolSize = 14;

        private RectTransform _layer;
        private RectTransform _shakeTarget;
        private Vector2 _shakeHome;
        private float _trauma;

        private Particle[] _particles;
        private int _next;

        private sealed class Shout
        {
            public RectTransform Rect;
            public Text Text;
            public CandyText Candy;
            public bool Alive;
            public float Life;
            public float MaxLife;
            public Vector2 Velocity;
        }

        private Shout[] _shouts;
        private int _nextShout;

        private Image _flash;

        /// <summary>The candy sprinkle colours, straight off the reference art.</summary>
        public static readonly Color[] CandyColours =
        {
            CandyText.Hex(0xFF4F9A), CandyText.Hex(0xFFD23A), CandyText.Hex(0x3BD6FF),
            CandyText.Hex(0x7C4DFF), CandyText.Hex(0x35E08A), CandyText.Hex(0xFF8A2A),
        };

        public RectTransform Layer => _layer;

        public static Fx Create(RectTransform canvas, RectTransform shakeTarget)
        {
            RectTransform layer = UIKit.Stretch("Fx", canvas);
            Fx fx = layer.gameObject.AddComponent<Fx>();
            fx._layer = layer;
            fx._shakeTarget = shakeTarget;
            if (shakeTarget != null) fx._shakeHome = shakeTarget.anchoredPosition;
            fx.Build();
            Instance = fx;
            return fx;
        }

        private void Build()
        {
            _particles = new Particle[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("p", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_layer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                go.SetActive(false);
                _particles[i].Rect = rt;
                _particles[i].Image = img;
            }

            _shouts = new Shout[TextPoolSize];
            for (int i = 0; i < TextPoolSize; i++)
            {
                Text label = UIKit.Label("shout", _layer, "", 80, Color.white);
                label.font = Design.Display;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                RectTransform rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(900f, 160f);
                CandyText candy = CandyText.Apply(label, CandyStyle.White);
                label.gameObject.SetActive(false);
                _shouts[i] = new Shout { Rect = rt, Text = label, Candy = candy };
            }

            _flash = UIKit.Image("Flash", _layer, ProcArt.Solid(), new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;
            RectTransform fr = _flash.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(-40f, -40f);
            fr.offsetMax = new Vector2(40f, 40f);
            _flash.gameObject.SetActive(false);
        }

        private Vector2 Local(Vector3 world) => _layer.InverseTransformPoint(world);

        private int Take()
        {
            int idx = _next;
            _next = (_next + 1) % PoolSize;
            return idx;
        }

        private void Spawn(Vector2 pos, Vector2 vel, Sprite sprite, Color colour, float size, float endSize,
                           float life, Fade mode, float gravity = 0f, float drag = 0f, float spin = 0f,
                           float aspect = 1f, float angle = 0f)
        {
            int i = Take();
            ref Particle p = ref _particles[i];
            p.Alive = true;
            p.Position = pos;
            p.Velocity = vel;
            p.Gravity = gravity;
            p.Drag = drag;
            p.Life = life;
            p.MaxLife = life;
            p.StartSize = size;
            p.EndSize = endSize;
            p.Aspect = aspect;
            p.Angle = angle;
            p.Spin = spin;
            p.Colour = colour;
            p.Mode = mode;
            p.Image.sprite = sprite;
            p.Rect.SetAsLastSibling();
            p.Rect.gameObject.SetActive(true);
        }

        // --- emitters ------------------------------------------------------------------------

        /// <summary>One sparkle that swells and fades where it is. The idle glint.</summary>
        public void Twinkle(Vector3 world, float size = 60f)
        {
            Spawn(Local(world), Vector2.zero, StarSprite(), Color.white, 0f, size, UnityEngine.Random.Range(0.45f, 0.7f),
                  Fade.Twinkle, spin: UnityEngine.Random.Range(-90f, 90f));
        }

        /// <summary>A cluster of twinkling stars around a point.</summary>
        public void Sparkles(Vector3 world, int count, float radius, float size = 56f, Color? tint = null)
        {
            Vector2 c = Local(world);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Color col = tint ?? (UnityEngine.Random.value < 0.6f ? Color.white : CandyText.Hex(0xFFE58A));
                Spawn(c + offset, offset.normalized * UnityEngine.Random.Range(30f, 140f) + new Vector2(0f, 60f),
                      StarSprite(), col, 0f, size * UnityEngine.Random.Range(0.6f, 1.2f),
                      UnityEngine.Random.Range(0.45f, 0.85f), Fade.Twinkle, drag: 2f,
                      spin: UnityEngine.Random.Range(-160f, 160f));
            }
        }

        /// <summary>Candy sprinkles thrown out from a point, falling under gravity.</summary>
        public void Sprinkles(Vector3 world, int count, float speed = 1100f, float size = 30f)
        {
            Vector2 c = Local(world);
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float m = speed * UnityEngine.Random.Range(0.35f, 1f);
                var vel = new Vector2(Mathf.Cos(a) * m, Mathf.Sin(a) * m + speed * 0.35f);
                Color col = CandyColours[UnityEngine.Random.Range(0, CandyColours.Length)];
                float s = size * UnityEngine.Random.Range(0.75f, 1.25f);
                Spawn(c, vel, CapsuleSprite(), col, s * 2.4f, s * 2.0f, UnityEngine.Random.Range(0.8f, 1.3f),
                      Fade.Out, gravity: -2400f, drag: 1.3f, spin: UnityEngine.Random.Range(-540f, 540f),
                      aspect: 2.4f, angle: UnityEngine.Random.value * 360f);
            }
        }

        /// <summary>
        /// Candy shards: small copies of a block's own artwork flying apart. A cleared block breaks
        /// into pieces of itself, not into generic dots.
        /// </summary>
        public void Shards(Vector3 world, Sprite sprite, int count, float speed, float size)
        {
            Vector2 c = Local(world);
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float m = speed * UnityEngine.Random.Range(0.3f, 1f);
                var vel = new Vector2(Mathf.Cos(a) * m, Mathf.Sin(a) * m + speed * 0.5f);
                float s = size * UnityEngine.Random.Range(0.28f, 0.52f);
                Spawn(c, vel, sprite, Color.white, s, s * 0.3f, UnityEngine.Random.Range(0.5f, 0.85f), Fade.Out,
                      gravity: -2800f, drag: 1.1f, spin: UnityEngine.Random.Range(-600f, 600f),
                      angle: UnityEngine.Random.value * 360f);
            }
        }

        /// <summary>A soft flash that swells and fades.</summary>
        public void Glow(Vector3 world, Color colour, float size, float life = 0.4f)
        {
            Spawn(Local(world), Vector2.zero, GlowSprite(), colour, size * 0.35f, size, life, Fade.Flash);
        }

        /// <summary>An expanding shockwave ring.</summary>
        public void Ring(Vector3 world, Color colour, float size, float life = 0.5f)
        {
            Spawn(Local(world), Vector2.zero, RingSprite(), colour, size * 0.2f, size, life, Fade.Flash);
        }

        /// <summary>The golden sweep along a cleared row or column, from the delivered art.</summary>
        public void LineSweep(Vector3 worldFrom, Vector3 worldTo, float thickness)
        {
            Sprite line = ArtLoader.Sprite("FX/fx_line_clear");
            Vector2 a = Local(worldFrom);
            Vector2 b = Local(worldTo);
            Vector2 mid = (a + b) * 0.5f;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            if (line != null)
            {
                float aspect = line.rect.width / Mathf.Max(1f, line.rect.height);
                float width = Mathf.Max(length * 1.25f, thickness * aspect * 0.6f);
                Spawn(mid, Vector2.zero, line, Color.white, width * 0.8f, width * 1.08f, 0.5f, Fade.Flash,
                      aspect: width / (thickness * 2.6f) * (width * 0.8f / width), angle: angle);
            }

            Glow((Vector3)_layer.TransformPoint(mid), new Color(1f, 0.92f, 0.5f, 0.9f), length * 0.9f, 0.35f);

            for (int i = 0; i < 9; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, UnityEngine.Random.value);
                Sparkles(_layer.TransformPoint(p), 1, thickness * 0.6f, thickness * 0.9f);
            }
        }

        /// <summary>Confetti from the top of the screen, fluttering down.</summary>
        public void Confetti(int count = 90)
        {
            Rect r = _layer.rect;
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(UnityEngine.Random.Range(r.xMin, r.xMax), r.yMax + UnityEngine.Random.Range(10f, 420f));
                var vel = new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-520f, -180f));
                Color col = CandyColours[UnityEngine.Random.Range(0, CandyColours.Length)];
                bool capsule = UnityEngine.Random.value < 0.6f;
                float s = UnityEngine.Random.Range(22f, 36f);
                Spawn(pos, vel, capsule ? CapsuleSprite() : SquareSprite(), col,
                      capsule ? s * 2.4f : s, capsule ? s * 2.4f : s, UnityEngine.Random.Range(2.6f, 3.8f),
                      Fade.Out, gravity: -260f, drag: 0.35f, spin: UnityEngine.Random.Range(-360f, 360f),
                      aspect: capsule ? 2.4f : 1f, angle: UnityEngine.Random.value * 360f);
            }
        }

        /// <summary>A white flash over the whole screen, for the very biggest moments.</summary>
        public void ScreenFlash(float strength = 0.55f)
        {
            _flash.gameObject.SetActive(true);
            _flash.rectTransform.SetAsLastSibling();
            _flash.color = new Color(1f, 1f, 1f, strength);
            Tween.Run(_flash, 0.45f, k =>
            {
                _flash.color = new Color(1f, 1f, 1f, strength * (1f - Ease.OutCubic(k)));
            }, 0f, () => _flash.gameObject.SetActive(false));
        }

        /// <summary>
        /// A candy shout — DOUBLE!, COMBO x4, +320 — that pops in over a point and floats up.
        /// </summary>
        public void Text(Vector3 world, string message, CandyStyle style, float fontSize, float life = 1f,
                         float rise = 220f, float delay = 0f)
        {
            if (delay > 0f)
            {
                Tween.Delay(delay, () => Text(world, message, style, fontSize, life, rise));
                return;
            }

            Shout s = _shouts[_nextShout];
            _nextShout = (_nextShout + 1) % _shouts.Length;

            s.Alive = true;
            s.Life = life;
            s.MaxLife = life;
            s.Velocity = new Vector2(0f, rise);
            s.Text.text = message;
            s.Text.fontSize = Mathf.RoundToInt(fontSize);
            s.Text.font = Design.Display;
            s.Text.color = Color.white;
            s.Candy.Set(style);

            Vector2 p = Local(world);
            float half = _layer.rect.width * 0.5f;
            float margin = s.Text.preferredWidth * 0.62f;
            p.x = margin < half ? Mathf.Clamp(p.x, -half + margin, half - margin) : 0f;

            s.Rect.anchoredPosition = p;
            s.Rect.localScale = Vector3.one * 0.3f;
            s.Rect.SetAsLastSibling();
            s.Text.gameObject.SetActive(true);
        }

        /// <summary>
        /// Coins that burst out of a point and fly into a coin pill, one clink each, the pill
        /// bumping as they land. The balance itself rolls up as they arrive.
        /// </summary>
        public void CoinFly(Vector3 fromWorld, RectTransform target, int count, float size = 70f, Action onArrive = null)
        {
            if (target == null) return;
            count = Mathf.Clamp(count, 1, 16);
            StartCoroutine(FlyCoins(Local(fromWorld), target, count, size, onArrive));
        }

        private readonly List<Image> _coins = new List<Image>();

        private Image CoinImage()
        {
            foreach (Image img in _coins)
                if (!img.gameObject.activeSelf) return img;

            Image coin = UIKit.Image("coin", _layer, ArtLoader.Sprite("UI/coin"), Color.white);
            coin.raycastTarget = false;
            coin.preserveAspect = true;
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _coins.Add(coin);
            return coin;
        }

        private IEnumerator FlyCoins(Vector2 from, RectTransform target, int count, float size, Action onArrive)
        {
            for (int i = 0; i < count; i++)
            {
                Image coin = CoinImage();
                RectTransform rt = coin.rectTransform;
                rt.sizeDelta = new Vector2(size, size);
                rt.SetAsLastSibling();
                coin.gameObject.SetActive(true);

                Vector2 scatter = from + UnityEngine.Random.insideUnitCircle * size * 1.6f;
                float delay = i * 0.055f;
                float arc = UnityEngine.Random.Range(-260f, 260f);

                rt.anchoredPosition = from;
                rt.localScale = Vector3.zero;

                Tween.Run(rt, 0.26f, k =>
                {
                    rt.anchoredPosition = Vector2.LerpUnclamped(from, scatter, Ease.OutCubic(k));
                    rt.localScale = Vector3.one * Ease.OutBack(k);
                }, delay, () =>
                {
                    Vector2 start = rt.anchoredPosition;
                    Tween.Run(rt, 0.55f, k =>
                    {
                        Vector2 end = target != null ? Local(target.position) : start;
                        Vector2 control = (start + end) * 0.5f + new Vector2(arc, 220f);
                        float e = Ease.InCubic(k) * 0.7f + k * 0.3f;
                        Vector2 a = Vector2.Lerp(start, control, e);
                        Vector2 b = Vector2.Lerp(control, end, e);
                        rt.anchoredPosition = Vector2.Lerp(a, b, e);
                        rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, e);
                    }, 0.05f, () =>
                    {
                        coin.gameObject.SetActive(false);
                        Sound.Coin();
                        if (target != null)
                        {
                            Twinkle(target.position, 70f);
                            Tween.Punch(target, 0.22f, 0.22f);
                        }
                        onArrive?.Invoke();
                    });
                });
            }

            yield break;
        }

        // --- shake ---------------------------------------------------------------------------

        /// <summary>Adds screen shake. Trauma accumulates and decays; the offset follows its square.</summary>
        public void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void SetShakeTarget(RectTransform target)
        {
            if (_shakeTarget != null) _shakeTarget.anchoredPosition = _shakeHome;
            _shakeTarget = target;
            if (target != null) _shakeHome = target.anchoredPosition;
        }

        // --- simulation ----------------------------------------------------------------------

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            SimulateParticles(dt);
            SimulateShouts(dt);
            SimulateShake(dt);
        }

        private void SimulateParticles(float dt)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ref Particle p = ref _particles[i];
                if (!p.Alive) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    p.Rect.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity.y += p.Gravity * dt;
                if (p.Drag > 0f) p.Velocity *= Mathf.Exp(-p.Drag * dt);
                p.Position += p.Velocity * dt;
                p.Angle += p.Spin * dt;

                float t = 1f - p.Life / p.MaxLife;
                float size;
                float alpha;

                switch (p.Mode)
                {
                    case Fade.Flash:
                        size = Mathf.Lerp(p.StartSize, p.EndSize, Ease.OutCubic(t));
                        alpha = 1f - Ease.OutCubic(t);
                        break;
                    case Fade.Twinkle:
                        float s = Mathf.Sin(t * Mathf.PI);
                        size = p.EndSize * s;
                        alpha = Mathf.Clamp01(s * 1.4f);
                        break;
                    default:
                        size = Mathf.Lerp(p.StartSize, p.EndSize, t);
                        alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                        break;
                }

                p.Rect.anchoredPosition = p.Position;
                p.Rect.sizeDelta = new Vector2(size, size / Mathf.Max(0.01f, p.Aspect));
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Angle);
                Color c = p.Colour;
                c.a *= alpha;
                p.Image.color = c;
            }
        }

        private void SimulateShouts(float dt)
        {
            for (int i = 0; i < _shouts.Length; i++)
            {
                Shout s = _shouts[i];
                if (!s.Alive) continue;

                s.Life -= dt;
                if (s.Life <= 0f)
                {
                    s.Alive = false;
                    s.Text.gameObject.SetActive(false);
                    continue;
                }

                float t = 1f - s.Life / s.MaxLife;
                s.Velocity *= Mathf.Exp(-3.2f * dt);
                s.Rect.anchoredPosition += s.Velocity * dt;

                float scale = t < 0.22f ? Mathf.LerpUnclamped(0.3f, 1f, Ease.OutBack(t / 0.22f, 2.6f)) : 1f;
                s.Rect.localScale = Vector3.one * scale;

                Color c = s.Text.color;
                c.a = t < 0.65f ? 1f : Mathf.Clamp01(1f - (t - 0.65f) / 0.35f);
                s.Text.color = c;
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

            _trauma = Mathf.Max(0f, _trauma - 1.9f * dt);
            float magnitude = _trauma * _trauma * 38f;
            float time = Time.unscaledTime * 34f;
            float x = (Mathf.PerlinNoise(3.1f, time) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(17.3f, time) - 0.5f) * 2f;
            _shakeTarget.anchoredPosition = _shakeHome + new Vector2(x, y) * magnitude;
        }

        // --- generated sprites ---------------------------------------------------------------

        private static readonly Dictionary<string, Sprite> Generated = new Dictionary<string, Sprite>();

        private static Sprite Make(string key, int width, int height, Func<float, float, Color> pixel)
        {
            if (Generated.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    px[y * width + x] = pixel((x + 0.5f) / width * 2f - 1f, (y + 0.5f) / height * 2f - 1f);

            tex.SetPixels(px);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
                                          0, SpriteMeshType.FullRect);
            sprite.name = key;
            Generated[key] = sprite;
            return sprite;
        }

        /// <summary>A four-pointed sparkle with a hot core.</summary>
        public static Sprite StarSprite() => Make("fx_star", 128, 128, (u, v) =>
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            float core = Mathf.Exp(-(u * u + v * v) * 26f);
            float rays = Mathf.Exp(-au * 30f) * Mathf.Pow(1f - Mathf.Min(1f, av), 2.2f)
                       + Mathf.Exp(-av * 30f) * Mathf.Pow(1f - Mathf.Min(1f, au), 2.2f);
            float halo = Mathf.Exp(-(u * u + v * v) * 5f) * 0.25f;
            float a = Mathf.Clamp01(core * 1.3f + rays + halo);
            return new Color(1f, 1f, 1f, a);
        });

        public static Sprite GlowSprite() => Make("fx_glow", 96, 96, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a * a);
        });

        public static Sprite RingSprite() => Make("fx_ring", 160, 160, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float band = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.08f, 2f));
            float inner = Mathf.Clamp01(1f - r) * 0.18f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(band + inner));
        });

        /// <summary>A glossy capsule, shaded in greys so a tint colours it without losing the gloss.</summary>
        public static Sprite CapsuleSprite() => Make("fx_capsule", 120, 50, (u, v) =>
        {
            const float halfW = 1f, halfH = 1f;
            float x = u * 60f, y = v * 25f;
            float d = ProcArt.RoundedBoxDistance(x, y, 58f * halfW, 23f * halfH, 23f);
            float a = Mathf.Clamp01(0.5f - d);
            float shade = Mathf.Lerp(0.72f, 1f, (v + 1f) * 0.5f);
            float gloss = Mathf.Exp(-Mathf.Pow((v - 0.45f) / 0.18f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(u) * 1.2f);
            float g = Mathf.Clamp01(shade + gloss * 0.5f);
            return new Color(g, g, g, a);
        });

        public static Sprite SquareSprite() => Make("fx_square", 56, 56, (u, v) =>
        {
            float d = ProcArt.RoundedBoxDistance(u * 28f, v * 28f, 26f, 26f, 8f);
            float a = Mathf.Clamp01(0.5f - d);
            float g = Mathf.Lerp(0.75f, 1f, (v + 1f) * 0.5f);
            return new Color(g, g, g, a);
        });

        /// <summary>A soft vertical band of light, for the button shine.</summary>
        public static Sprite BandSprite() => Make("fx_band", 48, 8, (u, v) =>
        {
            float a = Mathf.Exp(-u * u * 5f);
            return new Color(1f, 1f, 1f, a);
        });
    }
}
