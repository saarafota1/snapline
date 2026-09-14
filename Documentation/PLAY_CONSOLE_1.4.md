# Snapline 1.4 — Play Console forms to update before submitting

1.4 moves ads from AdMob direct to **Unity LevelPlay (ironSource) mediation**, with Google's
`ads-mobile-sdk` under it as the AdMob adapter, and adds **Google Analytics for Firebase**. Meta and
TikTok are unchanged from 1.3.

The Data safety form is the one that got Plumbline v1.3 rejected with a build that was fine. Update
it **before** sending 1.4 for review, not after a rejection.

The vendor answers below were checked on 14 Sep 2026 (by the Bloomlane and Bloomgate sessions, for
the same SDK versions). The artifact facts were measured on Snapline's own builds.

---

## What changed in the artifact

Measured with `Tools/ArtifactCheck/verify_artifact.py`, 1.4 test APK against the live 1.3 bundle
(`Builds/SnaplineV1.3.aab`, versionName 1.3).

| | 1.3 (live) | 1.4 |
|---|---|---|
| Permissions | 12 | 13: **+ `android.permission.READ_BASIC_PHONE_STATE`**, from `ads-mobile-sdk` 1.3.1. Not a data type by itself |
| `com.google.android.gms.permission.AD_ID` | yes | yes |
| `getInstalledApplications` calls in the dex | **0** | **1**, from ironSource's Ad Quality SDK inside `mediation-sdk` 9.6.0 |
| `QUERY_ALL_PACKAGES` | no | no, so Android 11+ package visibility still limits what that call returns |
| AdMob Unity plugin classes | 376 | 0 |
| Firebase Analytics | no | yes, project `snapline-e1035` |

---

## App content → Data safety

### The data types

Snapline has **no accounts, no IAP, no `setUserId` and no UGS sign-in**, so there is no User IDs row.

That last one needs a sentence, because 1.4 does bring in a UGS package: LevelPlay depends on
`com.unity.services.core`. Core on its own initialises but cannot sign anyone in — the kit compiles
anonymous sign-in only when `com.unity.services.authentication` is installed, and it is not. So no
player id is created, even though `autoSignInAnonymously` is on in the config. **If UGS
Authentication, Cloud Save or Leaderboards are ever added, the User IDs row comes back** (collected
Yes, shared No).

| Category | Data type | Collected | Shared | Ephemeral | Required? | Purposes |
|---|---|---|---|---|---|---|
| Location | **Approximate location** | Yes | Yes | No | Required | Advertising or marketing · Analytics · Fraud prevention, security and compliance |
| App activity | **App interactions** | Yes | Yes | No | Required | Advertising or marketing · Analytics · Fraud prevention, security and compliance ← *fraud prevention is new* |
| App activity | **Other actions** ← *new* | Yes | Yes | No | Required | Advertising or marketing · Analytics · Fraud prevention, security and compliance |
| App info and performance | **Diagnostics** | Yes | Yes | No | Required | Analytics · Fraud prevention, security and compliance · Advertising or marketing ← *advertising is new* |
| Device or other IDs | **Device or other IDs** | Yes | Yes | No | Required | Advertising or marketing · Analytics · Fraud prevention, security and compliance |

Leave everything else unticked: precise location, personal info, financial info, messages, photos,
audio, files, calendar, contacts, web browsing, health, and in-app search history.

**Financial info stays off even though Play Billing is in the dex.** Meta's `appevents/iap` and
TikTok's `iap` pull `billingclient` in transitively; with no purchasing package and no purchase code,
no purchase can happen and nothing is collected.

### Where each row comes from

- **Unity LevelPlay.** Unity's own Data safety answers list Approximate location, App activity
  **"Other actions (interaction with ads)"**, Diagnostics (collected, *not* shared) and Device or other
  IDs, each for all three purposes. No User IDs row.
  https://docs.unity.com/en-us/grow/levelplay/platform/legal-resources/google-data-safety-questionnaire
- **AdMob adapter (`ads-mobile-sdk`).** Google says it collects and shares, for advertising, analytics
  and fraud prevention: IP address (to estimate approximate location), product interactions (app
  launch, taps, video views), diagnostics and device identifiers.
  https://developers.google.com/ad-manager/mobile-ads-sdk/android/next-gen/privacy/play-data-disclosure
  It shares diagnostics, so Diagnostics stays *Shared: Yes* even though LevelPlay alone says No.
- **Firebase Analytics.** App-instance ID, Firebase installation ID and advertising ID → Device or other
  IDs. Coarse location from masked IP → Approximate location. Screen views, sessions and the game's
  events → App interactions. Not Diagnostics. Not Purchase history (no IAP). Not User IDs (no
  `setUserId`, Google Signals off). https://support.google.com/analytics/answer/11582702
  Firebase is a service provider, not a third party, so on its own it would not make a row "shared";
  the ad SDKs already share all of them. **If Google Signals or Analytics data sharing is ever turned
  on, re-read this.**
- **Meta and TikTok.** Unchanged from 1.3: advertising id and app activity, shared.

"Other actions" and the wider purposes are straight from the new vendors' own answers. Declaring
something an SDK does not collect costs nothing; missing something the scanner can see is what gets
an app rejected.

### The earlier questions

- *Does your app collect or share any of the required user data types?* **Yes.**
- *Is all user data encrypted in transit?* **Yes.**
- *Do you provide a way for users to request that their data is deleted?* **No.**
- *Account creation:* **"My app does not allow users to create an account."**

### Required, or users can choose

Keep **Required**. The consent form appears only in the EEA, the UK and Switzerland; everywhere else
collection runs with no choice, so "users can choose" would not be true for most players. If the 1.3
form says otherwise, keep the two consistent on purpose rather than by accident.

1.4 does make the choice real where the form exists: a player who declines, at launch or later from
PRIVACY SETTINGS, now stops being measured by Firebase, Meta and TikTok (`AnalyticsConsent`), and
Firebase records nothing before the form is answered (manifest defaults all off).

### Open decision: Installed apps

ironSource's **Ad Quality SDK** calls `PackageManager.getInstalledApplications`. **Confirmed in
Snapline's own 1.4 APK: one call site. The live 1.3 bundle has none.** ironSource's published Data
safety answers do not declare Installed apps; a Unity forum thread (Sep 2025) reports Google flagging
this call and asking for a prominent in-app disclosure.

Every LevelPlay game ships this, so it is one studio decision, already open for Bloomgate and
Bloomlane:

- **Declare it:** App activity → **Installed apps**, collected and shared, for fraud prevention and
  advertising. The safe side for the form — but a tick does not satisfy a prominent-disclosure finding
  if one arrives.
- **Follow ironSource's table:** leave it off, and be ready to answer a flag.
- Worth asking ironSource whether Ad Quality can be switched off for this app.

---

## App content → Advertising ID

- *Does your app use advertising ID?* **Yes.** LevelPlay, the AdMob adapter, Firebase, Meta and TikTok
  all read it, and `AD_ID` is in the manifest.
- *Why?* **Advertising or marketing**, **Analytics**, **Fraud prevention, security and compliance** —
  identical to the Device or other IDs row above. Play compares the two forms.
- **Personalisation stays unticked.** In Play's terms it means customising the app itself;
  personalised ads are covered by Advertising or marketing.
- **Account management stays unticked.** There are no accounts.

---

## Also check, same visit

- **ironSource dashboard** — register every phone that will run a test build as a **test device**
  before anyone taps an ad. A non-release LevelPlay build requests **live** ads; it only switches on
  diagnostics. Confirm **Google** is enabled under **Bidding**, not "Google AdMob Native".
- **Firebase → Project settings → Integrations** — confirm `snapline-e1035` is linked to the
  **SnapLine** GA4 property the Studio Hub dashboard reads. A Firebase project links to exactly one
  property, so this is the only place that decides where Snapline's events land.
- **AdMob console → Privacy and messaging** — the GDPR message must still be published and cover
  Snapline. Under LevelPlay the consent form is still Google's UMP, reading the AdMob app id from the
  manifest, so an unpublished message means no form and nothing in the log.
- **AdMob units** — nothing to change. With Google on Bidding, the AdMob app keeps earning through
  LevelPlay; the old unit ids are simply no longer called by the game.
- **Privacy policy** — `https://scibox-studios.com/privacy` must list Snapline.
- **Target audience** stays **13+ with no band under 13**. Any under-13 band pulls the game into the
  Families programme and ends personalised ads.
