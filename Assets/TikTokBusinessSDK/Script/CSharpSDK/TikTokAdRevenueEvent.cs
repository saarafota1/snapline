using System.Collections.Generic;

namespace SDK
{
    public class TikTokAdRevenueEvent : TikTokBaseEvent
    {
        public TikTokAdRevenueEvent(Dictionary<string, object> adRevenue, string eventId) : base(
            TikTokEventConstants.GetEventName(TTEventName.TTEventNameImpressionLevelAdRevenue), new Dictionary<string, object> { { "ad_revenue", adRevenue } }, eventId)
        {
        }
    }
    
}
