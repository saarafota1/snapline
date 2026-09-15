# Snapline 1.4.1 — Play Console changes for adding Unity Ads and Meta Audience Network

1.4.1 adds **Unity Ads as a third LevelPlay network** (ironSource adapter
`com.unity3d.ads-mediation:unityads-adapter:5.12.0` + `com.unity3d.ads:unity-ads:4.20.0`) and picks up the
kit's ad-reload fix. Nothing else in the data flow changes: LevelPlay, the AdMob adapter, Firebase,
Meta and TikTok are as in 1.4.

Start from `PLAY_CONSOLE_1.4.md` — that is the current form. This file lists **only what changes**.

Source for Unity Ads: Unity's own table, "Google Play data safety section for Unity Ads",
https://docs.unity.com/ads/en-us/manual/GoogleDataSafety (read 15 Sep 2026). Unity says its
disclosure covers the Unity Ads SDK only; the rest of the app stays as declared.

---

## Data safety: what changes

### Rows already declared — add a purpose

Unity Ads uses these for **App functionality** as well. Tick it; leave every other answer as it is.

| Data type | 1.4 purposes | 1.4.1 purposes |
|---|---|---|
| Location › **Approximate location** | Advertising · Analytics · Fraud prevention | + **App functionality** |
| App info and performance › **Diagnostics** | Analytics · Fraud prevention · Advertising | + **App functionality** |
| Device or other IDs › **Device or other IDs** | Advertising · Analytics · Fraud prevention | + **App functionality** |

App activity › **App interactions** already covers Unity's "page views and taps" (in the ad itself,
not gameplay) and "app usage times", with the same three purposes. No change.

### New rows — two decisions for you

Unity's table lists two types the 1.4 form does not have.

| Data type | Unity says | Recommendation |
|---|---|---|
| Personal info › **User IDs** (Unity: "Personal identifiers") | Collected Yes, Shared Yes, Required, **App functionality** | **Declare it.** Collected Yes, Shared Yes, not ephemeral, Required, App functionality. |
| Financial info › **Purchase history** | Collected Yes, Shared Yes, Required, Advertising · Analytics | **Declare it**, unless you confirm in the Unity Monetization dashboard that nothing purchase-related is on. Snapline has no in-app purchases, so there is little to collect, but Play compares the form with the SDK vendor's published answers, and declaring costs nothing. |

The 1.4 form said "no User IDs row" because no Unity Gaming Services sign-in exists. That is still
true for UGS; the new row is Unity Ads' own identifier, not a player account.

### Earlier questions

- *Collect or share required data?* — Yes (unchanged).
- *Encrypted in transit?* — Yes (unchanged; Unity confirms for Unity Ads).
- *Way to request deletion?* — **No**, unchanged. Unity offers deletion for its own data, but Snapline
  has no in-app deletion route of its own; keep the answer consistent with 1.4.
- *Account creation* — unchanged: the app does not allow creating an account.

---

## Meta Audience Network (also new in 1.4.1)

1.4.1 also adds **Meta Audience Network** as a LevelPlay network: LevelPlay's adapter file 5.5.0.0,
which is `facebook-adapter:5.3.0` + `audience-network-sdk:6.21.0`. This is separate from the Meta app-events SDK
that 1.3 already had.

**Meta does not publish a Data safety table for Audience Network.** Its guidance is the narrative
"Resources for completing app store data practice questionnaires" (Facebook and Audience Network
SDKs), https://developers.facebook.com/blog/post/2022/07/18/resources-for-completing-app-store-data-practice-questionnaires-apps-facebook-or-audience-network-sdk/
and its Data Processing Options page, which confirms IP-derived location. Mapped onto the form:

| Data type | Audience Network | Change to the form |
|---|---|---|
| Device or other IDs (advertising id, device identifiers) | Collected, shared with Meta; advertising, analytics, fraud prevention | None — already declared, same purposes |
| App activity › App interactions (ads shown, viewed, clicked) | Collected, shared; advertising, analytics, fraud prevention | None — already declared |
| Location › Approximate location (inferred from IP) | Collected, shared; advertising, analytics, fraud prevention | None — already declared |
| App info and performance › Diagnostics (SDK and ad-request performance, errors) | Collected, shared; analytics, app functionality, fraud prevention | None beyond the **App functionality** purpose Unity Ads already adds |

So Audience Network adds **no new rows and no new purposes** on top of the Unity Ads changes above.
Every type it touches is already declared with those purposes. Two things to keep true:

- **Shared stays Yes** on all four. The data goes to Meta as a third party.
- **Meta's Limited Data Use** (US state privacy laws) is not switched on in code. If the studio ever
  enables it, nothing on this form changes, but the privacy policy should mention it.

**Meta Monetization Manager:** Meta serves nothing until payout details are added and the SnapLine
property passes review. Until then Audience Network shows as present in LevelPlay but never fills.

---

## App content › Advertising ID

Keep it identical to the Device or other IDs row, as Play cross-checks the two: add
**App functionality** to the reasons, beside Advertising, Analytics and Fraud prevention.

---

## Outside Play Console

- **app-ads.txt** at `scibox-studios.com/app-ads.txt` needs Unity's lines, or Unity Ads bids on
  Snapline's inventory can be discounted. The LevelPlay dashboard lists the lines to add for each
  network under the app's settings.
- **LevelPlay dashboard**: Unity Ads should be listed under **Bidding** on SnapLine's Instances page,
  for both rewarded and interstitial, beside ironSource and Google.
- **Test devices**: the phone with advertising id `ce538cef-5897-4f88-8b32-d863a80b4ed3` must be registered as a
  test device in both LevelPlay (Setup › Test devices) and the Unity dashboard before anyone taps an
  ad on a build.
- **Privacy policy**: `https://scibox-studios.com/privacy` should name Unity Ads among the ad
  partners.
