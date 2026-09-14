using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using GameKit.Art;
using Snapline.Art;

namespace Snapline.App
{
    /// <summary>
    /// The studio bumper that plays once at launch, before the menu.
    ///
    /// The whole thing is written to fail open. A splash is the least important part of the app and
    /// the earliest thing to run, which is a bad combination: anything that can hang here strands
    /// the player on a black screen before they have seen the game at all. So every path — missing
    /// clip, codec the device will not decode, a prepare that never returns — ends at the menu, and
    /// a tap skips it at any point.
    /// </summary>
    public sealed class SplashScreen : MonoBehaviour
    {
        /// <summary>Where the clip lives, relative to <c>Resources/</c>.</summary>
        private const string ClipPath = "Snapline/Splash/splash";

        /// <summary>
        /// Floor on how long the splash may hold the screen. Used as-is when the clip does not
        /// report a length — which is the failure this guard exists for, a clip that never
        /// prepared — and otherwise widened to the clip's own length plus <see cref="TimeoutSlack"/>.
        ///
        /// A fixed ceiling was wrong: it silently truncated any clip longer than itself, so a
        /// perfectly healthy splash would get cut off mid-animation and look like a bug in the video.
        /// </summary>
        private const float MinimumTimeout = 6f;

        /// <summary>Headroom over the clip's length, covering decode start-up on a slow device.</summary>
        private const float TimeoutSlack = 2.5f;

        private const float FadeOutSeconds = 0.35f;

        private VideoPlayer _player;
        private RenderTexture _target;
        private CanvasGroup _group;
        private bool _finished;
        private Action _onDone;

        /// <summary>
        /// True if a splash was started. False means the caller should show the menu immediately —
        /// there is no clip, or this is a batch-mode run that must not wait on one.
        /// </summary>
        public static bool TryPlay(RectTransform canvasRect, Action onDone)
        {
            // The screenshot and smoke harnesses drive the game themselves and would sit through
            // three seconds of video on every capture, or time out waiting for a menu that has not
            // appeared yet.
            if (StoreShots.StoreShotsRequested() || StoreShots.FeatureGraphicRequested() ||
                SmokeShots.RequestedOnCommandLine() || Application.isBatchMode)
                return false;

            var clip = Resources.Load<VideoClip>(ClipPath);
            if (clip == null)
            {
                Debug.Log($"[Snapline] no splash clip at Resources/{ClipPath}; going straight to the menu.");
                return false;
            }

            var go = new GameObject("SplashScreen");
            go.transform.SetParent(canvasRect, false);
            var splash = go.AddComponent<SplashScreen>();
            splash.Begin(canvasRect, clip, onDone);
            return true;
        }

        private RawImage Video(string name, RectTransform root, AspectRatioFitter.AspectMode mode, float aspect, Color tint)
        {
            var raw = new GameObject(name, typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(root, false);
            raw.texture = _target;
            raw.color = tint;
            raw.raycastTarget = false;
            RectTransform rt = raw.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = mode;
            fitter.aspectRatio = aspect;
            return raw;
        }

        private void Begin(RectTransform canvasRect, VideoClip clip, Action onDone)
        {
            _onDone = onDone;

            RectTransform root = UIKit.Stretch("SplashRoot", canvasRect);
            root.SetAsLastSibling();
            _group = root.gameObject.AddComponent<CanvasGroup>();

            // Opaque black behind the video. The clip's aspect will rarely match the device exactly,
            // and letterbox bars over a half-built menu would look like a rendering fault.
            UIKit.Image("SplashBackdrop", root, ArtKit.Solid(), Color.black);

            _target = new RenderTexture((int)clip.width, (int)clip.height, 0)
            {
                name = "SplashTarget"
            };

            float aspect = clip.width / (float)Mathf.Max(1, clip.height);

            // Two copies of the same frame. Behind: enlarged to cover the whole screen and dimmed, so
            // whatever the phone's shape there are no black bars. In front: the clip fitted INSIDE the
            // screen, so none of it is cropped.
            //
            // Covering with the front copy alone was the first version, and on a phone taller than
            // 9:16 it cut the sides off — the SNAPLINE wordmark ran off both edges of the screen.
            Video("SplashFill", root, AspectRatioFitter.AspectMode.EnvelopeParent, aspect, new Color(0.62f, 0.62f, 0.68f, 1f));
            Video("SplashVideo", root, AspectRatioFitter.AspectMode.FitInParent, aspect, Color.white);

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.clip = clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.isLooping = false;
            _player.playOnAwake = false;
            _player.waitForFirstFrame = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.loopPointReached += _ => Finish();
            _player.errorReceived += (_, message) =>
            {
                Debug.LogWarning($"[Snapline] splash video error, skipping: {message}");
                Finish();
            };

            _player.Play();

            float budget = clip.length > 0d
                ? (float)clip.length + TimeoutSlack
                : MinimumTimeout;
            StartCoroutine(Watchdog(Mathf.Max(MinimumTimeout, budget)));
        }

        /// <summary>Any tap skips. Checked here rather than on a button so the whole screen works.</summary>
        private void Update()
        {
            if (_finished) return;
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) Finish();
        }

        private IEnumerator Watchdog(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!_finished)
            {
                Debug.LogWarning($"[Snapline] splash exceeded {seconds:F1}s; showing the menu.");
                Finish();
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            // The menu is revealed underneath before the fade starts, so the fade lands on the game
            // rather than on black.
            _onDone?.Invoke();
            StartCoroutine(FadeAndDestroy());
        }

        private IEnumerator FadeAndDestroy()
        {
            if (_player != null) _player.Stop();

            float t = 0f;
            while (t < FadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (_group != null) _group.alpha = 1f - Mathf.Clamp01(t / FadeOutSeconds);
                yield return null;
            }

            Cleanup();
            Destroy(gameObject);
        }

        private void Cleanup()
        {
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }

        private void OnDestroy() => Cleanup();
    }
}
