using NUnit.Framework;
using GameKit;

namespace Snapline.Tests
{
    /// <summary>
    /// Tests for interstitial pacing.
    ///
    /// Worth testing precisely because getting it wrong is invisible in development — an ad that
    /// fires too often looks fine on one playthrough and drives away real players over a week. The
    /// policy takes the clock as a parameter so three-minute gaps can be checked instantly.
    /// </summary>
    public class AdPolicyTests
    {
        private static AdPolicy Policy() => new AdPolicy
        {
            GamesBeforeFirstAd = 2,
            GamesBetweenAds = 1,
            SecondsBetweenAds = 180f,
        };

        [Test]
        public void NoAdBeforeTheGraceGamesAreFinished()
        {
            AdPolicy policy = Policy();

            Assert.IsFalse(policy.ShouldShowInterstitial(0f), "Never before a single game has finished.");

            policy.RecordGameFinished();
            Assert.IsFalse(policy.ShouldShowInterstitial(10f), "One game is still inside the grace period.");

            policy.RecordGameFinished();
            Assert.IsTrue(policy.ShouldShowInterstitial(20f), "The grace period is over after two games.");
        }

        [Test]
        public void TimeGateBlocksASecondAdTooSoon()
        {
            AdPolicy policy = Policy();
            policy.RecordGameFinished();
            policy.RecordGameFinished();

            Assert.IsTrue(policy.ShouldShowInterstitial(100f));
            policy.RecordInterstitialShown(100f);

            policy.RecordGameFinished();
            Assert.IsFalse(policy.ShouldShowInterstitial(150f), "Only 50s have passed of the 180s gap.");

            Assert.IsTrue(policy.ShouldShowInterstitial(300f), "200s later the gate should be open.");
        }

        [Test]
        public void GameGateBlocksTwoAdsInARow()
        {
            AdPolicy policy = Policy();
            policy.RecordGameFinished();
            policy.RecordGameFinished();

            policy.RecordInterstitialShown(0f);

            // Plenty of time has passed, but no further game has finished.
            Assert.IsFalse(policy.ShouldShowInterstitial(10000f),
                           "A second ad must not run before another game has been played.");

            policy.RecordGameFinished();
            Assert.IsTrue(policy.ShouldShowInterstitial(10000f));
        }

        [Test]
        public void AdsRemovedSuppressesInterstitialsEntirely()
        {
            AdPolicy policy = Policy();
            policy.RecordGameFinished();
            policy.RecordGameFinished();
            policy.AdsRemoved = true;

            Assert.IsFalse(policy.ShouldShowInterstitial(10000f));
        }

        [Test]
        public void SecondsUntilNextAllowed_CountsDownAndFloorsAtZero()
        {
            AdPolicy policy = Policy();
            policy.RecordGameFinished();
            policy.RecordGameFinished();

            Assert.AreEqual(0f, policy.SecondsUntilNextAllowed(0f), 1e-3,
                            "Before any ad has run there is nothing to wait for.");

            policy.RecordInterstitialShown(100f);

            Assert.AreEqual(130f, policy.SecondsUntilNextAllowed(150f), 1e-3);
            Assert.AreEqual(0f, policy.SecondsUntilNextAllowed(400f), 1e-3);
        }
    }
}
