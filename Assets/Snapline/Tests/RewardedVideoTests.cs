using System.Threading.Tasks;
using GameKit;
using GameKit.Offline;
using NUnit.Framework;
using Snapline.App;
using Snapline.Core;
using UnityEngine;

namespace Snapline.Tests
{
    /// <summary>
    /// The refusal half of the reward gate, which had never once run.
    ///
    /// Every game in the portfolio built SimulatedAdService on its defaults, where every view
    /// completes and pays, so no test had ever taken the "do not pay" branch - and none could reach
    /// AdResult.TimedOut at all. That is how Flying Penguin Saga 1.2 shipped a gate that refused a
    /// reward the player had earned, and Snapline read the same answer the same way.
    ///
    /// So these drive all four results through the real wrapper and assert what the player is
    /// actually given, not that a log line appeared.
    /// </summary>
    public sealed class RewardedVideoTests
    {
        [Test]
        public void Earned_PaysOnAFinishedViewAndOnOneTheNetworkNeverReported()
        {
            Assert.IsTrue(RewardedVideo.Earned(AdResult.Completed), "A finished view earns its reward.");
            Assert.IsTrue(RewardedVideo.Earned(AdResult.TimedOut),
                          "An ad was on screen and an advertiser was billed: refusing takes the money and pays nothing.");

            Assert.IsFalse(RewardedVideo.Earned(AdResult.Skipped), "Closed before the reward point.");
            Assert.IsFalse(RewardedVideo.Earned(AdResult.Unavailable), "Nothing reached the screen.");
        }

        [Test]
        public void KeepOffering_OnlyWhenNoAdEverReachedTheScreen()
        {
            Assert.IsTrue(RewardedVideo.KeepOffering(AdResult.Unavailable),
                          "The player did nothing wrong; do not take the revive away for empty inventory.");

            Assert.IsFalse(RewardedVideo.KeepOffering(AdResult.Skipped), "They closed it themselves.");
            Assert.IsFalse(RewardedVideo.KeepOffering(AdResult.Completed));
            Assert.IsFalse(RewardedVideo.KeepOffering(AdResult.TimedOut));
        }

        [Test]
        public void SettleCoinVideo_PaysAndSpendsTheAllowanceOnTheTwoEarnedResults()
        {
            foreach (AdResult earned in new[] { AdResult.Completed, AdResult.TimedOut })
            {
                int before = Wallet.AdRewardsLeftToday;
                int coins = RewardedVideo.SettleCoinVideo(earned);

                Assert.AreEqual(Economy.AdReward, coins, $"{earned} should pay the store's coins.");
                Assert.AreEqual(Mathf.Max(0, before - 1), Wallet.AdRewardsLeftToday,
                                $"{earned} should spend one of the day's videos.");
            }

            foreach (AdResult refused in new[] { AdResult.Skipped, AdResult.Unavailable })
            {
                int before = Wallet.AdRewardsLeftToday;
                int coins = RewardedVideo.SettleCoinVideo(refused);

                Assert.AreEqual(0, coins, $"{refused} should pay nothing.");
                Assert.AreEqual(before, Wallet.AdRewardsLeftToday,
                                $"{refused} must not cost the player one of their five.");
            }
        }

        /// <summary>
        /// The whole path a reward takes: a simulated network forced to each result, through the
        /// game's own AdController, out to what the player is given.
        /// </summary>
        [Test]
        public async Task EveryResultThroughTheRealWrapper()
        {
            var host = new GameObject("AdControllerTestHost");
            try
            {
                var ads = host.AddComponent<AdController>();

                foreach ((AdResult forced, bool pays) in new[]
                         {
                             (AdResult.Completed, true),
                             (AdResult.TimedOut, true),
                             (AdResult.Skipped, false),
                             (AdResult.Unavailable, false),
                         })
                {
                    GameKitRuntime.InstallAdService(new SimulatedAdService { Duration = 0f, RewardedResult = forced });

                    AdResult result = await ads.ShowRewardedAsync();
                    Assert.AreEqual(forced, result, "The wrapper must report what the network said.");

                    int before = Wallet.AdRewardsLeftToday;
                    int coins = RewardedVideo.SettleCoinVideo(result);

                    Assert.AreEqual(pays ? Economy.AdReward : 0, coins, $"{forced} paid the wrong amount.");
                    Assert.AreEqual(pays ? Mathf.Max(0, before - 1) : before, Wallet.AdRewardsLeftToday,
                                    $"{forced} spent the allowance wrongly.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
