using System.Threading.Tasks;
using UnityEngine;
using GameKit;
using GameKit.Offline;
using Snapline.Core;

namespace Snapline.App
{
    /// <summary>
    /// Everything the game knows about ads.
    ///
    /// Talks only to GameKit's IAdService, never to AdMob, so the network is a config dropdown
    /// rather than a code change — and with no SDK installed the kit hands back a null service and
    /// every method here simply reports "no". Nothing in the game is gated on an ad succeeding.
    ///
    /// Two placements, both at the end of a run:
    ///   - a rewarded CONTINUE, which the player opts into for a clear benefit
    ///   - a paced interstitial, which they never asked for and so is capped hard
    /// </summary>
    public sealed class AdController : MonoBehaviour
    {
        /// <summary>Development flag: install a fake network so the flow can be exercised offline.</summary>
        public const string SimulateFlag = "-snapline-fake-ads";

        /// <summary>
        /// One rescue per run. More would turn a score into a measure of patience rather than skill,
        /// and the leaderboard would stop meaning anything.
        /// </summary>
        public const int MaxRevivesPerRun = 1;

        /// <summary>Rows cleared by a revive. Enough to be worth watching an ad for.</summary>
        public const int ReviveRowsCleared = 3;

        private readonly AdPolicy _policy = new AdPolicy();
        private bool _busy;

        public AdPolicy Policy => _policy;

        /// <summary>True while an ad is on screen, so the game can ignore input.</summary>
        public bool IsShowingAd => _busy;

        public void Init(GameKitConfig config)
        {
            if (config != null)
            {
                // Pace from the kit config so the numbers live with the other shipping settings.
                _policy.SecondsBetweenAds = config.secondsBetweenInterstitials;
                _policy.GamesBetweenAds = Mathf.Max(1, config.levelsBetweenInterstitials);
                _policy.GamesBeforeFirstAd = Mathf.Max(0, config.interstitialGraceLevels);
            }

            if (SimulationRequested())
            {
                // UseAdService, not InstallAdService: the override has to be sticky. Services start
                // asynchronously and finish after this runs, and under LevelPlay the real adapter
                // initialises even on Windows - InstallAdService lets it replace the fake, and the
                // self-playing harness then reports that no ad ever played. Found on Bloomgate.
                GameKitRuntime.UseAdService(new SimulatedAdService());
                Debug.Log("[Snapline] Simulated ad network installed (development flag).");
            }

            // Log what this build will actually request. Whether a build serves test or real ads is
            // the one thing here worth confirming from a device log rather than reasoning about - and
            // under LevelPlay the answer is always live: there is no test/live pair of unit ids, and a
            // non-release build only turns on diagnostics. Tap ads only on a device registered as a
            // test device in the ironSource dashboard.
            if (config != null)
            {
                Debug.Log(config.adNetwork == AdNetwork.LevelPlay
                    ? $"[Snapline] ads: LevelPlay, release build={GameKitConfig.IsReleaseBuild}, LIVE units " +
                      $"rewarded={config.LevelPlayRewardedUnit} interstitial={config.LevelPlayInterstitialUnit} " +
                      "- tap only on a registered test device"
                    : $"[Snapline] ad units: release build={GameKitConfig.IsReleaseBuild}, " +
                      $"using {(config.UsingAdMobTestUnits ? "GOOGLE TEST" : "LIVE")} units — " +
                      $"rewarded={config.AdMobRewardedUnit} interstitial={config.AdMobInterstitialUnit}");
            }

            Debug.Log($"[Snapline] ads: rewardedReady={GameKitRuntime.Ads.IsRewardedReady} " +
                      $"interstitialReady={GameKitRuntime.Ads.IsInterstitialReady} " +
                      $"firstAdAfter={_policy.GamesBeforeFirstAd} games, " +
                      $"gap={_policy.SecondsBetweenAds}s/{_policy.GamesBetweenAds} games");
        }

        private static bool SimulationRequested()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == SimulateFlag) return true;
            return false;
        }

        // --- rewarded continue --------------------------------------------------------------

        /// <summary>
        /// Can this finished run be offered a rescue? Levels are excluded: they have a move budget,
        /// so reviving one would just be a worse RETRY.
        /// </summary>
        public bool CanOfferRevive(GameRun run)
        {
            if (run == null || !run.IsGameOver) return false;
            if (run.Mode != GameMode.Endless) return false;
            if (run.RevivesUsed >= MaxRevivesPerRun) return false;

            return GameKitRuntime.Ads.IsRewardedReady;
        }

        /// <summary>
        /// Play the rewarded ad. True only if it ran to the end — a player who closed it early
        /// must not be paid, or the reward means nothing and the network eventually notices.
        ///
        /// Not revive-specific despite where it started: the store pays coins with the same unit.
        /// </summary>
        public async Task<bool> ShowRewardedAsync()
        {
            if (_busy) return false;
            _busy = true;

            try
            {
                AdResult result = await GameKitRuntime.Ads.ShowRewardedAsync();
                Debug.Log($"[Snapline] rewarded continue: {result}");
                return result == AdResult.Completed;
            }
            catch (System.Exception e)
            {
                // An ad network throwing must never take a run down with it.
                Debug.LogWarning($"[Snapline] rewarded ad failed: {e.Message}");
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        // --- interstitial ---------------------------------------------------------------------

        /// <summary>Call once each time a run ends, before asking about an interstitial.</summary>
        public void RecordGameFinished() => _policy.RecordGameFinished();

        /// <summary>
        /// Show an interstitial if the pacing allows one. Always safe to await; it returns
        /// immediately when the policy says no or nothing is loaded.
        /// </summary>
        public async Task MaybeShowInterstitialAsync()
        {
            if (_busy) return;
            if (!_policy.ShouldShowInterstitial(Time.realtimeSinceStartup)) return;
            if (!GameKitRuntime.Ads.IsInterstitialReady) return;

            _busy = true;

            try
            {
                AdResult result = await GameKitRuntime.Ads.ShowInterstitialAsync();
                if (result != AdResult.Unavailable)
                    _policy.RecordInterstitialShown(Time.realtimeSinceStartup);

                Debug.Log($"[Snapline] interstitial: {result}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Snapline] interstitial failed: {e.Message}");
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
