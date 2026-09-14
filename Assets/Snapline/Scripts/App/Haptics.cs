using UnityEngine;

namespace Snapline.App
{
    /// <summary>
    /// Short vibration taps.
    ///
    /// Not <c>Handheld.Vibrate</c>: on most Android phones that is a fixed half-second buzz, which on
    /// every placement feels like an error rather than a click. The system vibrator is asked for a
    /// one-shot of a few milliseconds at a chosen strength instead, which is what a "tick" is.
    ///
    /// Handheld.Vibrate is still referenced below, deliberately: its presence is what makes Unity
    /// declare the VIBRATE permission, and without that permission the JNI call is silently refused.
    /// </summary>
    public static class Haptics
    {
        /// <summary>A button, a piece picked up or set down.</summary>
        public static void Light() => Pulse(12, 70);

        /// <summary>A line clear.</summary>
        public static void Medium() => Pulse(24, 150);

        /// <summary>A big clear, a win, a smash.</summary>
        public static void Heavy() => Pulse(45, 255);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk = -1;
        private static bool _broken;
#endif

        private static float _last;

        private static void Pulse(long milliseconds, int amplitude)
        {
            if (!Settings.VibrationEnabled) return;

            // A burst of clears fires several of these inside one frame; the motor cannot render
            // them separately anyway, and queuing them turns a crisp tick into a drone.
            if (Time.unscaledTime - _last < 0.035f) return;
            _last = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_broken) return;
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                }

                if (_vibrator == null) { _broken = true; return; }

                if (_sdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effects.CallStatic<AndroidJavaObject>(
                               "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception e)
            {
                // A phone without a vibrator, or one that refuses: stop asking rather than throwing
                // on every placement for the rest of the session.
                _broken = true;
                Debug.LogWarning($"[Snapline] haptics unavailable: {e.Message}");
                if (milliseconds > 1000) Handheld.Vibrate();
            }
#endif
        }
    }
}
