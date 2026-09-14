using UnityEngine;

namespace Snapline.App
{
    /// <summary>Player preferences that are not part of a run.</summary>
    public static class Settings
    {
        private const string SoundKey = "snapline.sound";
        private const string MusicKey = "snapline.music";
        private const string VibrationKey = "snapline.vibration";

        /// <summary>Sound on by default — a casual puzzle game with the audio off feels dead.</summary>
        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The background loop. On by default, and quiet enough to leave on.</summary>
        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Short taps of vibration on placements, clears and buttons.</summary>
        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
