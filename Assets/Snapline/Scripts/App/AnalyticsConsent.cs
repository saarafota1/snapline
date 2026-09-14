using System;
using System.Threading.Tasks;
using GameKit;
using UnityEngine;

namespace Snapline.App
{
    /// <summary>
    /// Keeps analytics collection in step with the player's consent, for the whole session.
    ///
    /// The kit decides analytics collection once, at startup, from <c>Consent.CanRequestAds</c>, and
    /// never again. Two things are wrong with that for this game, and both are privacy bugs rather than
    /// style:
    ///
    ///   * <c>CanRequestAds</c> is the wrong question. UMP still allows limited, non-personalised ads
    ///     to a player who tapped "Do not consent", so it stays true for them - and the kit's native
    ///     consent service only reports a refusal when UMP says no ads at all. A player who declined
    ///     was therefore still measured by Firebase, Meta and TikTok.
    ///     <see cref="GameKitRuntime.PersonalDataAllowed"/> is the right one: it also requires TCF
    ///     purpose 1 inside GDPR scope, fails open everywhere the rules allow, and is what the kit
    ///     itself hands LevelPlay as its GDPR flag.
    ///   * Nothing re-applied the answer to analytics when the player changed it from PRIVACY
    ///     SETTINGS. <see cref="GameKitRuntime.ConsentChanged"/> reached the LevelPlay adapter only, so
    ///     analytics kept the old answer until the next launch.
    ///
    /// So this applies the stricter rule once startup has created the analytics adapters, and again on
    /// every consent change. Game code, not kit code, on purpose: the kit is shared by five games and
    /// was read-only for this change. Taken from Bloomlane, which found it and reported it as a kit
    /// finding; delete this file once the kit applies the same rule itself.
    /// </summary>
    public static class AnalyticsConsent
    {
        private static GameKitConfig _config;

        /// <summary>
        /// Starts the kit exactly as <see cref="GameKitRuntime.InitializeAsync"/> would, then takes
        /// over the analytics consent decision. Not awaited by the caller: the game is playable
        /// throughout, as before.
        /// </summary>
        public static async Task InitializeAsync(GameKitConfig config)
        {
            _config = config;

            // Subscribed before startup so a form answered at launch is heard. Startup raises the
            // event before analytics exists; Apply ignores that call and the one below covers it.
            GameKitRuntime.ConsentChanged -= Apply;
            GameKitRuntime.ConsentChanged += Apply;

            try
            {
                await GameKitRuntime.InitializeAsync(config);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Snapline] Services startup threw: " + e.Message);
            }

            // Startup ends with analytics, and set collection from CanRequestAds on the way out.
            // Replace that answer with the one that respects a refusal.
            Apply();
        }

        private static void Apply()
        {
            if (_config == null) return;

            // Only the kit's placeholder is skipped: startup raises ConsentChanged before any analytics
            // adapter exists. Not IsAvailable - an adapter still starting would then keep the kit's
            // CanRequestAds answer, which is the one this class exists to replace.
            IAnalyticsService analytics = GameKitRuntime.Analytics;
            if (analytics == null || analytics is GameKit.Offline.NullAnalytics) return;

            bool personalData = GameKitRuntime.PersonalDataAllowed;
            bool allowed = _config.analyticsUsesAdvertisingId && personalData;

            try
            {
                // Set every time, not only on a difference: the composite reports true only when
                // every adapter agrees, so comparing first could skip one that is out of step.
                analytics.CollectionEnabled = allowed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Snapline] Could not apply analytics consent: " + e.Message);
                return;
            }

            Debug.Log($"[Snapline] analytics collection {(allowed ? "ON" : "OFF")} "
                      + $"(personalDataAllowed={personalData}, config={_config.analyticsUsesAdvertisingId})");
        }
    }
}
