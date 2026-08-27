using UnityEngine;

namespace Snapline.App
{
    /// <summary>Player preferences that are not part of a run.</summary>
    public static class Settings
    {
        private const string SoundKey = "snapline.sound";

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
    }
}
