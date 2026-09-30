using GameKit;
using Snapline.Core;

namespace Snapline.App
{
    /// <summary>
    /// What a finished rewarded video is worth, in one place, because the answer is not obvious and
    /// getting it wrong costs either the player or the advertiser.
    ///
    /// The kit added <see cref="AdResult.TimedOut"/> on 1 Oct 2026: an ad WAS on screen and an
    /// advertiser WAS billed, but the network never said how it ended. A playable ad and its end card
    /// have no upper bound, so a long view used to come back as "no ad happened" and the player was
    /// refused a reward they had earned - that is what cost a live Flying Penguin Saga player their
    /// revive. Snapline read the same answer the same way.
    ///
    /// So: pay on Completed AND TimedOut. Refuse on Skipped, where the player closed it before the
    /// reward point. Refuse on Unavailable, where nothing reached the screen and nobody was billed.
    /// </summary>
    public static class RewardedVideo
    {
        /// <summary>True when the player has earned what the video was offered for.</summary>
        public static bool Earned(AdResult result) =>
            result == AdResult.Completed || result == AdResult.TimedOut;

        /// <summary>
        /// True when an offer withdrawn after a failure should be put back.
        ///
        /// Nothing was shown, so the player did nothing wrong and nothing was spent: taking the
        /// button away for the rest of the run would punish them for the network having no ad. A
        /// player who closed the ad themselves does lose it, which is what stops a second tap
        /// reading as a broken button.
        /// </summary>
        public static bool KeepOffering(AdResult result) => result == AdResult.Unavailable;

        /// <summary>
        /// Settles the store's coins-for-a-video offer: the coins to pay, and the day's allowance
        /// spent if there are any. Zero when the video earned nothing, and then the allowance is
        /// untouched - a video that never played must not cost the player one of their five.
        /// </summary>
        public static int SettleCoinVideo(AdResult result)
        {
            if (!Earned(result)) return 0;

            Wallet.RecordAdReward();
            return Economy.AdReward;
        }
    }
}
