# Kit findings from Snapline's LevelPlay + Firebase migration, 14 Sep 2026

Written in the game repo on purpose. `_StudioKit` was read-only for this work, so none of this is
applied to the kit. Every item touches all five consuming games, so each is the owner's call.

Bloomgate and Bloomlane migrated the same day and wrote their own findings
(`Bloomgate/Documentation/KIT_FINDINGS_2026-09-14_LevelPlay.md`,
`Bloomlane/Documentation/KIT_FINDINGS_2026-09-14_LevelPlay.md`). Sections 1–3 are new here; section 4
lists what Snapline confirmed from theirs.

---

## 1. LevelPlay switches on the kit's UGS backend, and it throws on every launch

- LevelPlay 9.5.1 depends on `com.unity.services.core` 1.15.1.
- `Runtime/Ugs/StudioKit.Ugs.asmdef` defines `GAMEKIT_UGS` whenever `com.unity.services.core` is
  present. So every LevelPlay game compiles and registers the UGS backend, whether or not it wants
  UGS.
- At startup `UgsServices.InitializeAsync` calls `UnityServices.InitializeAsync`. In a project not
  linked to a Unity Cloud project, that throws `UnityProjectNotLinkedException`, rethrown as
  `ServicesInitializationException`. `GameKitRuntime` catches it and logs
  `[GameKit] Backend init failed, continuing offline`.

Seen in Snapline's self-playing Windows run. The device takes the same code path. Nothing breaks.
The cost is an exception and a stack trace at every launch, in exactly the log people read when they
diagnose ads. Sparkwick, Bloomgate and Bloomlane should show it too.

**Data safety consequence.** No user id is created: anonymous sign-in compiles only under
`GAMEKIT_UGS_AUTH`, and none of these games has `com.unity.services.authentication`. The PLAYBOOK
rule "a game with no UGS declares four — drop User IDs" needs one word changed, though. Every
LevelPlay game now *has* UGS Core, so the rule should read "no UGS **Authentication**".

**Fix:** key `GAMEKIT_UGS` on `com.unity.services.authentication`, or on an explicit define, rather
than on Core, which now arrives as a transitive dependency.

## 2. ReleaseCheck accepts any real AdMob app id, not this game's

- `ReleaseCheck.MissingUmpAppId` passes any app id that contains `~` and is not Google's test
  publisher.
- The LevelPlay branch of `BundleAudit` checks the app key and the unit ids only.

The headless LevelPlay install that games now use copies Sparkwick's `Assets/LevelPlay`. Sparkwick's
`LevelPlayMediatedNetworkSettings.asset` carries **Sparkwick's** AdMob app id (`…~5998939237`).
Forget to replace it, and the release ships another game's `APPLICATION_ID`. UMP then loads the
wrong app's consent message, and every gate stays green.

Snapline replaced it, and its `Tools/ArtifactCheck/verify_artifact.py` checks the exact id, so this
did not ship. Nothing in the kit would have caught it.

**Fix:** give `GameKitConfig` the game's expected AdMob Android app id, and have `ReleaseCheck` and
`BundleAudit` compare against that exact value. A well-formed id is not enough.

## 3. The ad-unit log games copied from the AdMob era turns dangerous under LevelPlay

Snapline's `AdController` logged `using GOOGLE TEST units` or `LIVE units` from
`GameKitConfig.UsingAdMobTestUnits`. Under LevelPlay that property still says "test" in a
non-release build, while LevelPlay requests **live** ads. A test build's own log therefore reassured
anyone reading it that tapping was safe. Snapline now logs the network, and says the units are live.

**Fix:** a network-aware `GameKitConfig` description, for example `AdUnitsSummary`, that the games
log instead. Grep every game for `UsingAdMobTestUnits`.

## 4. Confirmed on Snapline, already reported by Bloomgate or Bloomlane

- **Analytics ignores a consent refusal** (Bloomlane §1). Bloomlane's `AnalyticsConsent` is ported
  as `Assets/Snapline/Scripts/App/AnalyticsConsent.cs`. Its marker is in the 1.4 APK's metadata.
- **Non-release LevelPlay builds request live ads** (Bloomgate §1). Confirmed in
  `LevelPlayService.cs`: a non-release build gets only `SetAdaptersDebug`, `is_test_suite` and
  `ValidateIntegration`.
- **`InstallAdService` is not sticky** (Bloomgate §2). Snapline's `AdController` used it, and it is
  switched to `UseAdService`. The self-playing run then completed a rewarded continue with LevelPlay
  compiled in.
- **`LEVELPLAY_DEPENDENCIES_INSTALLED`** was written by hand. **`AdMobConfigurations.json`** was
  created holding `EnableAdMob: false` (Bloomlane §4, §5).
- **Ad Quality SDK and a new permission** (Bloomgate §3), measured on Snapline:
  - `getInstalledApplications` appears 0 times in the live 1.3 bundle and once in the 1.4 APK.
  - `READ_BASIC_PHONE_STATE` is new.
  - Neither is in PLAYBOOK "Play Data safety" yet.
- **`AdapterAudit` does not check a game's own services assembly**, and **nothing automatically
  guards against a leftover `GAMEKIT_RELEASE`**. Snapline's verifier covers the first. The second
  was checked by hand after all four builds and found absent.

## 5. Shared docs are out of date for Snapline

- **APP_REGISTRY "Snapline"** says "v1.0 (versionCode 1) is in review" and "1.1, versionCode 2 (in
  development)". **1.3 (versionCode 4) has been live on Play since 2 Sep 2026**, on full rollout to
  177 countries. 1.4 (versionCode 5) is built and verified on `feature/levelplay-firebase`.
- **The AGENTS.md table** lists Snapline as "In progress … not yet submitted".
- **Snapline's ids for the registry:**
  - LevelPlay app key `27f3fdd8d`
  - rewarded unit `01o2jtwe9iwyg6cg`
  - interstitial unit `ju6wcgbi3v5nq1og`
  - Firebase project `snapline-e1035`, app `1:867429608864:android:0ae086cfbd048e51a948c5`

## 6. Noise worth knowing

- **Headless import errors.** A headless import of LevelPlay 9.5.1 logs `NullReferenceException`
  while importing its editor-only mock ad prefabs (`MockBannerEditorAd`, `MockInterstitialEditorAd`,
  `MockRewardedEditorAd`). They are editor-only and never reach an Android build.
- **"LevelPlay 9.5.1" is only the Unity package.** The install copied from Sparkwick wraps native
  `mediation-sdk` **9.6.0**, according to `IronSourceSDKDependencies.xml`. The Ad Quality
  `getInstalledApplications` call arrives with 9.6.0.
