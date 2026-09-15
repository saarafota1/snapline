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

## 3. Confirmed on Snapline

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
