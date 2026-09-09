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

            var raw = new GameObject("SplashVideo", typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(root, false);
            raw.texture = _target;
            raw.raycastTarget = false;
            RectTransform rawRect = raw.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            // Cover, not fit: fill the screen and let the overflow crop. The bumper is centred, so
            // cropping the edges costs nothing, whereas pillarboxing a 9:16 clip on a 9:20 phone
            // puts black bars down two thirds of the screen.
            var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = clip.width / (float)Mathf.Max(1, clip.height);

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
