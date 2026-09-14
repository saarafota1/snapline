using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Snapline.App;
using Snapline.Art;

namespace Snapline.UI
{
    /// <summary>Easing curves. Every one takes and returns 0..1, overshooting where named.</summary>
    public static class Ease
    {
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        public static float InCubic(float t) => Mathf.Pow(Mathf.Clamp01(t), 3f);

        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;

        /// <summary>Past the target and back: the pop that makes something land like candy.</summary>
        public static float OutBack(float t, float overshoot = 1.9f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        /// <summary>A wobbly settle, for jelly.</summary>
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }

    /// <summary>
    /// Coroutine tweens that do not care whether their target's GameObject is active.
    ///
    /// Everything runs on one hidden runner rather than on the object being animated: a coroutine
    /// started on an inactive object throws, and screens here are built inactive and switched on in
    /// the same frame they animate. Tweens are keyed, so punching the same button twice restarts the
    /// punch instead of stacking two that fight over its scale.
    /// </summary>
    public static class Tween
    {
        private sealed class Runner : MonoBehaviour { }

        private static Runner _runner;
        private static readonly Dictionary<object, Coroutine> Running = new Dictionary<object, Coroutine>();

        private static Runner Host
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("Tweens");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
                return _runner;
            }
        }

        public static void Stop(object key)
        {
            if (key == null || _runner == null) return;
            if (Running.TryGetValue(key, out Coroutine c) && c != null) _runner.StopCoroutine(c);
            Running.Remove(key);
        }

        /// <summary>Calls <paramref name="step"/> with 0..1 over the duration, on unscaled time.</summary>
        public static Coroutine Run(object key, float duration, Action<float> step, float delay = 0f,
                                    Action done = null)
        {
            Stop(key);
            Coroutine c = Host.StartCoroutine(Routine(key, duration, step, delay, done));
            if (key != null) Running[key] = c;
            return c;
        }

        public static Coroutine Delay(float seconds, Action then) => Run(null, 0f, null, seconds, then);

        private static IEnumerator Routine(object key, float duration, Action<float> step, float delay, Action done)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float t = 0f;
            while (t < duration)
            {
                if (key is UnityEngine.Object o && o == null) yield break;
                step?.Invoke(t / duration);
                yield return null;
                t += Time.unscaledDeltaTime;
            }

            if (key is UnityEngine.Object dead && dead == null) yield break;
            step?.Invoke(1f);
            if (key != null) Running.Remove(key);
            done?.Invoke();
        }

        /// <summary>Grows from small to full size with an overshoot.</summary>
        public static void PopIn(Transform t, float delay = 0f, float duration = 0.42f, float from = 0.2f)
        {
            if (t == null) return;
            t.localScale = Vector3.one * from;
            Run(t, duration, k => { if (t != null) t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, Ease.OutBack(k)); }, delay);
        }

        /// <summary>A quick bump in size and back.</summary>
        public static void Punch(Transform t, float amount = 0.18f, float duration = 0.3f)
        {
            if (t == null) return;
            Run(t, duration, k =>
            {
                if (t == null) return;
                float s = 1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.35f);
                t.localScale = Vector3.one * s;
            });
        }

        /// <summary>Slides in from an offset, landing with an overshoot.</summary>
        public static void SlideIn(RectTransform rt, Vector2 offset, float delay = 0f, float duration = 0.5f)
        {
            if (rt == null) return;
            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + offset;
            Run(rt, duration, k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(home + offset, home, Ease.OutBack(k, 1.2f)); }, delay);
        }

        /// <summary>A sideways shudder, for "no".</summary>
        public static void Shake(RectTransform rt, float amplitude = 18f, float duration = 0.38f)
        {
            if (rt == null) return;
            Vector2 home = rt.anchoredPosition;
            Run(rt, duration, k =>
            {
                if (rt == null) return;
                float x = Mathf.Sin(k * Mathf.PI * 7f) * amplitude * (1f - k);
                rt.anchoredPosition = home + new Vector2(x, 0f);
            });
        }

        public static void Fade(CanvasGroup group, float to, float duration, float delay = 0f)
        {
            if (group == null) return;
            float from = group.alpha;
            Run(group, duration, k => { if (group != null) group.alpha = Mathf.Lerp(from, to, Ease.OutCubic(k)); }, delay);
        }

        public static void FadeImage(Graphic g, float to, float duration, float delay = 0f)
        {
            if (g == null) return;
            float from = g.color.a;
            Run(g, duration, k =>
            {
                if (g == null) return;
                Color c = g.color;
                c.a = Mathf.Lerp(from, to, k);
                g.color = c;
            }, delay);
        }
    }

    /// <summary>
    /// The feel of every candy button: a squash when pressed, a springy overshoot on release, a
    /// click and a tick of vibration — plus optional idle life, so the screen never sits dead.
    ///
    /// Scale is spring-simulated rather than tweened, so a quick double tap reads as two bounces
    /// instead of the second press snapping the first one flat.
    /// </summary>
    public sealed class CandyPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float PressedScale = 0.9f;
        public bool Click = true;

        /// <summary>Breathing, as a fraction of size. The primary call to action only.</summary>
        public float Pulse;
        public float PulseSpeed = 2.6f;

        /// <summary>Gentle float, in canvas units.</summary>
        public float Bob;
        public float BobSpeed = 1.7f;

        /// <summary>Occasional wiggle, in degrees, for things that want attention.</summary>
        public float Wiggle;
        public float WiggleEvery = 3.5f;

        private float _scale = 1f;
        private float _velocity;
        private float _target = 1f;
        private bool _down;
        private float _phase;
        private float _lastBob;
        private float _wiggleClock;
        private Selectable _selectable;

        private void Awake()
        {
            _phase = UnityEngine.Random.value * 10f;
            _wiggleClock = UnityEngine.Random.value * WiggleEvery;
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            _down = true;
            _target = PressedScale;
            if (Click)
            {
                Sound.Tap();
                Haptics.Light();
            }
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_down) Release();
        }

        private void Release()
        {
            _down = false;
            _target = 1f;
            _velocity += 2.2f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            // A stiff, lightly damped spring: fast enough to feel instant, loose enough to wobble.
            float accel = (_target - _scale) * 420f - _velocity * 18f;
            _velocity += accel * dt;
            _scale += _velocity * dt;

            float idle = Pulse > 0f ? 1f + Pulse * Mathf.Sin((Time.unscaledTime + _phase) * PulseSpeed) : 1f;
            transform.localScale = Vector3.one * (_scale * idle);

            if (Bob > 0f)
            {
                float bob = Mathf.Sin((Time.unscaledTime + _phase) * BobSpeed) * Bob;
                Vector3 p = transform.localPosition;
                p.y += bob - _lastBob;
                transform.localPosition = p;
                _lastBob = bob;
            }

            if (Wiggle > 0f)
            {
                _wiggleClock += dt;
                if (_wiggleClock > WiggleEvery) _wiggleClock = 0f;
                float w = _wiggleClock < 0.5f ? Mathf.Sin(_wiggleClock * Mathf.PI * 8f) * Wiggle * (1f - _wiggleClock * 2f) : 0f;
                transform.localRotation = Quaternion.Euler(0f, 0f, w);
            }
        }

        private void OnDisable()
        {
            _down = false;
            _target = 1f;
            _scale = 1f;
            _velocity = 0f;
            transform.localScale = Vector3.one;

            if (Bob > 0f)
            {
                Vector3 p = transform.localPosition;
                p.y -= _lastBob;
                transform.localPosition = p;
                _lastBob = 0f;
            }

            if (Wiggle > 0f) transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>Turns something slowly. Starbursts behind rewards.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = 28f;

        private void Update() => transform.Rotate(0f, 0f, -Speed * Time.unscaledDeltaTime);
    }

    /// <summary>Breathes an image's alpha and size. Glows behind things.</summary>
    public sealed class Breathe : MonoBehaviour
    {
        public float Speed = 2f;
        public float ScaleAmount = 0.06f;
        public float AlphaMin = 0.55f;
        public float AlphaMax = 1f;

        private Graphic _graphic;
        private float _phase;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            _phase = UnityEngine.Random.value * 6f;
        }

        private void Update()
        {
            float s = (Mathf.Sin((Time.unscaledTime + _phase) * Speed) + 1f) * 0.5f;
            transform.localScale = Vector3.one * (1f + ScaleAmount * s);
            if (_graphic == null) return;
            Color c = _graphic.color;
            c.a = Mathf.Lerp(AlphaMin, AlphaMax, s);
            _graphic.color = c;
        }
    }

    /// <summary>
    /// Now and then, a sparkle somewhere over this rect. Cheap life for the things a player should
    /// notice: the current level, the reward, the primary button.
    /// </summary>
    public sealed class Glint : MonoBehaviour
    {
        public float Every = 1.6f;
        public float Size = 64f;
        public int Count = 1;

        private float _clock;

        private void OnEnable() => _clock = UnityEngine.Random.Range(0f, Every);

        private void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_clock < Every || Fx.Instance == null) return;
            _clock = UnityEngine.Random.Range(-0.3f, 0.3f);

            var rt = (RectTransform)transform;
            Rect r = rt.rect;
            for (int i = 0; i < Count; i++)
            {
                var local = new Vector3(UnityEngine.Random.Range(r.xMin, r.xMax) * 0.9f,
                                        UnityEngine.Random.Range(r.yMin, r.yMax) * 0.9f, 0f);
                Fx.Instance.Twinkle(rt.TransformPoint(local), Size);
            }
        }
    }

    /// <summary>
    /// A band of light that sweeps across a button now and then, clipped to the button's own drawn
    /// shape.
    ///
    /// The clip is a Mask using the button's sprite, not a rectangle: a RectMask2D would let the
    /// band shine across the transparent corners of a rounded button, which looks like a glitch.
    /// </summary>
    public sealed class ShineSweep : MonoBehaviour
    {
        public float Every = 3.4f;
        public float Duration = 0.75f;

        private RectTransform _band;
        private RectTransform _clip;
        private float _clock;

        public static ShineSweep Add(Image target, float every = 3.4f, float alpha = 0.42f)
        {
            if (target == null || target.sprite == null) return null;

            var clipGo = new GameObject("ShineClip", typeof(RectTransform), typeof(Image), typeof(Mask));
            var clip = (RectTransform)clipGo.transform;
            clip.SetParent(target.transform, false);
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = Vector2.zero;
            clip.offsetMax = Vector2.zero;
            clip.SetSiblingIndex(0);

            var clipImage = clipGo.GetComponent<Image>();
            clipImage.sprite = target.sprite;
            clipImage.type = target.type;
            clipImage.pixelsPerUnitMultiplier = target.pixelsPerUnitMultiplier;
            clipImage.preserveAspect = target.preserveAspect;
            clipImage.raycastTarget = false;
            clipGo.GetComponent<Mask>().showMaskGraphic = false;

            var bandGo = new GameObject("Band", typeof(RectTransform), typeof(Image));
            var band = (RectTransform)bandGo.transform;
            band.SetParent(clip, false);
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            var bandImage = bandGo.GetComponent<Image>();
            bandImage.sprite = Fx.BandSprite();
            bandImage.color = new Color(1f, 1f, 1f, alpha);
            bandImage.raycastTarget = false;
            band.localRotation = Quaternion.Euler(0f, 0f, -22f);

            ShineSweep sweep = target.gameObject.AddComponent<ShineSweep>();
            sweep._band = band;
            sweep._clip = clip;
            sweep.Every = every;
            sweep._clock = UnityEngine.Random.Range(0f, every);
            return sweep;
        }

        private void Update()
        {
            if (_band == null) return;

            _clock += Time.unscaledDeltaTime;
            if (_clock > Every) _clock -= Every;

            Rect r = _clip.rect;
            _band.sizeDelta = new Vector2(Mathf.Max(60f, r.height * 0.45f), r.height * 2.4f);

            float k = _clock / Duration;
            if (k > 1f)
            {
                _band.anchoredPosition = new Vector2(r.width * 2f, 0f);
                return;
            }

            _band.anchoredPosition = new Vector2(Mathf.Lerp(-r.width * 0.75f, r.width * 0.75f, Ease.InOutSine(k)), 0f);
        }
    }
}
