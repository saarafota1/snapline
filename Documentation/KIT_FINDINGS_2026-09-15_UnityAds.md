# Kit findings from Snapline 1.4.1 (Unity Ads + ad-reload fix), 15 Sep 2026

Written in the game repo on purpose: `_StudioKit` was read-only for this work, so nothing here is
applied to the kit. Each item is the owner's call.

## 1. AdapterAudit does not know about LevelPlay's network adapters

`AdapterAudit` reports "LevelPlay ads present" from the mediation SDK alone. A build with the Unity Ads
adapter and a build without it produce the same audit output. Nothing in the kit's release gates
would catch a Unity Ads dependency file that was deleted, never resolved, or stripped.

Snapline checks it in its own `Tools/ArtifactCheck/verify_artifact.py`, with a negative control:

| Marker in the dex | 1.4 APK (no Unity Ads) | 1.4.1 APK |
|---|---|---|
| `com/unity3d/ads` (the check the task suggested) | **10** | 2375 |
| `com/unity3d/ads/IUnityAdsInitializationListener` | 0 | 2 |
| `com/ironsource/adapters/unityads` | 0 | 23 |

**The bare `com/unity3d/ads` grep does not discriminate.** LevelPlay's own `mediation-sdk` carries a
few strings under that path, so it is non-zero in a build with no Unity Ads at all. Checked only
against the new build, it looks like proof and is not. Snapline's verifier uses the SDK's init
interface and the adapter package instead; both are 0 without Unity Ads.

**Fix:** for LevelPlay games, have `AdapterAudit` read `Assets/LevelPlay/Editor/IS*AdapterDependencies.xml`
and require each declared network's adapter package in the artifact (`com/ironsource/adapters/<network>`).
It then follows whatever networks a game actually installs. And change the PLAYBOOK step "confirm
`com/unity3d/ads` classes are in the dex" to a discriminating marker.

## 2. The retry fix needs a rebuild of every LevelPlay game, and nothing tells you which ones have it

Kit 4ea1037's `Reloader` only reaches a game through a new build. The string
`asking again in` is in the 1.4.1 APK's IL2CPP metadata and absent from the 1.4 build, which is a
cheap way to tell a fixed build from an unfixed one without a device.

**Fix:** an `AdapterAudit` marker for it, so a release built from a stale kit is refused.

## 3. Meta Audience Network 5.7.0.0 cannot build on Unity 6000.2.7f2

The adapter file handed down for this task (5.7.0.0 = `facebook-adapter:5.4.0` + `audience-network-sdk:6.22.0`)
fails every Android build on the studio's editor:

```
Execution failed for task ':launcher:checkReleaseAarMetadata'.
  Dependency 'androidx.browser:browser:1.9.0' requires Android Gradle plugin 8.9.1 or higher.
  This build currently uses Android Gradle plugin 8.7.2.
```

- Gradle's dependency tree shows `audience-network-sdk:6.22.0` is the only thing asking for browser 1.9.0.
  `ads-mobile-sdk` wants 1.8.0; Meta's app-events SDK wants 1.0.0.
- **Pinning browser to 1.8.0 is not safe.** Audience Network ships its real code as
  `assets/audience_network/classes.dex`, and that dex references three classes that exist only in browser
  1.9.0: `CustomTabsCallback$NavigationEvent`, `CustomTabsIntent$ContentTargetType` and
  `CustomTabsIntent$OpenInBrowserState`. A check of `classes.jar` alone finds no browser references at all,
  which is misleading.
- **The ironSource SDK version is not the problem.** LevelPlay's own `LevelPlayVersions.json`
  (the project copy and the live `s3.amazonaws.com/ssa.public/Unity/Package/V2/LevelPlayVersions.json`)
  gives every Meta and Unity Ads version `ironSourceSdkVersion [9.0.0, 10.0[`. 9.6.0 is the newest SDK.
- **What Snapline ships (owner's choice):** LevelPlay's official **5.5.0.0** file,
  `facebook-adapter:5.3.0` + `audience-network-sdk:6.21.0`. The 6.21.0 pom has no androidx.browser
  dependency.
- **`com/facebook/ads` in the dex does not prove Audience Network is present:** it is 1 in a build
  without it. Use `Lcom/facebook/ads/AudienceNetworkAds;` or `com/ironsource/adapters/facebook`.

**Fix:** until the editor moves past AGP 8.7.2, the PLAYBOOK should name 5.5.0.0 as the Audience
Network file for this portfolio. A resolve-time check that refuses any AAR needing a newer AGP would
catch the next one before a build.

## 4. Confirmed on Snapline

- **Headless route works for Unity Ads:** adding `ISUnityAdsAdapterDependencies.xml` 5.12.0.0 and
  running `StudioKit.EditorTools.AndroidDependencies.Resolve` added `unityads-adapter:5.12.0` and
  `unity-ads:4.20.0` to `mainTemplate.gradle`. No `com.unity.ads` package, no `GAMEKIT_UNITY_ADS`.
- **No duplicate classes** beside `ads-mobile-sdk` 1.3.1, `admob-adapter` 5.13.0, `mediation-sdk`
  9.6.0 and `firebase-analytics` 23.2.0.
- **Permissions are identical** between the 1.4 and 1.4.1 APKs (14 each): the Unity Ads SDK adds none.
- **Signing environment variables are not set on this machine**, so `BuildAndroidRelease` cannot
  run headless here; the owner builds the signed bundle from Studio › Release › Build Android
  Release. After the 1.4 release build, `androidUseCustomKeystore: 1` was left in
  ProjectSettings.asset, which is the intended behaviour (`KeystoreScope`), and is committed.
