# Snapline 1.4.1 — Play Console changes for adding Unity Ads

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
