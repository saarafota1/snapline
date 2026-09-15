"""
Artifact checks for a Snapline 1.4+ APK or AAB, per _StudioKit AGENTS.md "Verify against the artifact".
Run it against the release .aab before uploading, after BuildAndroidRelease's own audits pass:

    "C:/Program Files/Blender Foundation/Blender 4.2/4.2/python/bin/python.exe" Tools/ArtifactCheck/verify_artifact.py
        Builds/Snapline.aab
        C:/UnityVersions/6000.2.7f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/34.0.0/aapt2.exe

Ported from Bloomgate's, which was proven against its live AdMob-direct bundle first. Every count is
paired with a POSITIVE CONTROL - a string known to be in the artifact - so a failed read cannot pass as
an absence. That is the trap AGENTS.md records: aapt2 cannot read an .aab, prints an error, and a grep
over that error reports a clean zero.

For an .aab the protobuf manifest and resources are read as bytes, whose strings are plain UTF-8;
aapt2 is only used for an .apk.
"""
import re
import subprocess
import sys
import zipfile

PACKAGE = "com.sciboxstudios.snapline"
ADMOB_APP_ID = "ca-app-pub-1931205009793131~9498502442"
GOOGLE_APP_ID = "1:867429608864:android:0ae086cfbd048e51a948c5"   # Firebase project snapline-e1035

art = sys.argv[1]
aapt2 = sys.argv[2] if len(sys.argv) > 2 else None
is_aab = art.lower().endswith(".aab")
z = zipfile.ZipFile(art)
names = z.namelist()
print("artifact:", art, "entries:", len(names))

ok = True


def check(label, cond, detail=""):
    global ok
    print(("PASS " if cond else "FAIL ") + label + (" - " + detail if detail else ""))
    ok &= bool(cond)


def aapt(*args):
    return subprocess.run([aapt2, *args, art], capture_output=True, encoding="utf-8", errors="replace")


# ---- manifest --------------------------------------------------------------------------------------
if is_aab:
    manifest = z.read("base/manifest/AndroidManifest.xml").decode("latin-1")
    check("manifest readable (control: package)", PACKAGE in manifest, f"{len(manifest)} bytes")
    n_appid = manifest.count("com.google.android.gms.ads.APPLICATION_ID")
else:
    out = aapt("dump", "xmltree", "--file", "AndroidManifest.xml")
    manifest = out.stdout
    check("aapt2 read the manifest (control: package)", PACKAGE in manifest,
          f"{len(manifest)} chars, stderr={out.stderr.strip()[:120]!r}")
    # aapt2 prints each string attribute twice, ="x" (Raw: "x"), so count declaring lines, not hits.
    n_appid = sum(1 for l in manifest.splitlines() if "com.google.android.gms.ads.APPLICATION_ID" in l)

ids = sorted(set(re.findall(r"ca-app-pub-\d+~\d+", manifest)))
check("APPLICATION_ID declared exactly once", n_appid == 1, f"count={n_appid}")
check("AdMob app id is Snapline's", ids == [ADMOB_APP_ID], str(ids))
check("launcher is UnityPlayerGameActivity", "UnityPlayerGameActivity" in manifest)
check("not debuggable", not re.search(r"debuggable[^\n]*(true|0xffffffff)", manifest))

for key in ("firebase_analytics_collection_enabled",
            "google_analytics_default_allow_analytics_storage",
            "google_analytics_default_allow_ad_storage",
            "google_analytics_default_allow_ad_user_data",
            "google_analytics_default_allow_ad_personalization_signals"):
    present = key in manifest
    if is_aab:
        # A compiled boolean is not a string in the protobuf; presence is what can be proven here.
        check("consent default declared: " + key, present, "value not readable from an .aab; check the APK")
        continue
    # aapt2 compiles "false" to a boolean and prints it unquoted: android:value(0x01010024)=false
    m = re.search(re.escape(key) + r'"[^\n]*\n[^\n]*android:value\(0x01010024\)=("?)([^"\s]*)\1', manifest)
    val = m.group(2) if m else "?"
    check("consent default off: " + key, present and val == "false", "value=" + val)

for meta in ("com.facebook.sdk.ApplicationId", "com.facebook.sdk.ClientToken"):
    check("Meta meta-data present: " + meta, meta in manifest)

perms = sorted(set(re.findall(r"(?:android|com\.google\.android\.(?:gms|finsky))\.permission\.[A-Z_]+", manifest)))
print("permissions (%d):" % len(perms))
for p in perms:
    print("   ", p)

# ---- dex -------------------------------------------------------------------------------------------
dex = [n for n in names if re.search(r"(^|/)classes\d*\.dex$", n)]
blob = b"".join(z.read(n) for n in dex)


def c(s):
    return blob.count(s.encode())


control = c("com/facebook/appevents")
check("dex readable (control: com/facebook/appevents)", control > 0, f"{len(dex)} dex, {control} hits")
check("LevelPlay mediation classes in dex", c("unity3d/mediation") > 0,
      f"unity3d/mediation={c('unity3d/mediation')}, ironsource={c('ironsource')}")
# The one that discriminates: the plugin's own classes. The new Google ads SDK path and
# Lcom/google/android/gms/ads/MobileAds; are both non-zero in an AdMob-direct build too (Bloomgate).
check("no AdMob Unity plugin classes", c("com/google/unity/ads") == 0, f"com/google/unity/ads={c('com/google/unity/ads')}")
check("UMP library in dex", c("com/google/android/ump") > 0, f"{c('com/google/android/ump')}")
check("Firebase Analytics in dex", c("com/google/firebase/analytics/FirebaseAnalytics") > 0,
      f"{c('com/google/firebase/analytics/FirebaseAnalytics')}")
check("TikTok in dex", c("com/tiktok") > 0, f"{c('com/tiktok')}")
# Unity Ads as a third LevelPlay network (1.4.1): the network SDK and the ironSource adapter for it.
# Without the adapter, the Unity Ads bidding instances set up in the LevelPlay dashboard never bid.
# Not the bare "com/unity3d/ads" path: LevelPlay's own mediation-sdk carries a few strings under it,
# so it is non-zero in a build with no Unity Ads at all (the 1.4 APK). The SDK's init interface is not.
check("Unity Ads SDK in dex", c("com/unity3d/ads/IUnityAdsInitializationListener") > 0,
      f"IUnityAdsInitializationListener={c('com/unity3d/ads/IUnityAdsInitializationListener')}, "
      f"com/unity3d/ads (not discriminating)={c('com/unity3d/ads')}")
check("Unity Ads LevelPlay adapter in dex", c("com/ironsource/adapters/unityads") > 0,
      f"com/ironsource/adapters/unityads={c('com/ironsource/adapters/unityads')}")
# Data safety: ironSource's Ad Quality SDK calls getInstalledApplications; the AdMob-direct builds had none.
for s in ("getInstalledApplications", "getInstalledPackages", "QUERY_ALL_PACKAGES"):
    print(f"INFO dex {s}: {c(s)}   manifest: {manifest.count(s)}")

# ---- google_app_id ---------------------------------------------------------------------------------
if is_aab:
    res = z.read("base/resources.pb").decode("latin-1")
    found = sorted(set(re.findall(r"1:\d+:android:[0-9a-f]+", res)))
    check("google_app_id is Snapline's Firebase app", "google_app_id" in res and found == [GOOGLE_APP_ID], str(found))
else:
    out = aapt("dump", "resources").stdout
    check("aapt2 dumped resources (control: app_name)", "string/app_name" in out, f"{len(out)} chars")
    # The value sits on the line after the resource name, as: () "1:...". Read line by line: a
    # pattern allowed to cross newlines picks up a neighbouring resource's empty value instead.
    lines = out.splitlines()
    values = sorted({re.search(r'"([^"]*)"', lines[i + 1]).group(1)
                     for i, l in enumerate(lines[:-1])
                     if l.strip().endswith("string/google_app_id") and '"' in lines[i + 1]})
    check("google_app_id is Snapline's Firebase app", values == [GOOGLE_APP_ID], str(values))

# ---- global-metadata.dat ---------------------------------------------------------------------------
gm = [n for n in names if n.endswith("global-metadata.dat")]
meta = z.read(gm[0]) if gm else b""


def m_(s):
    return meta.count(s.encode())


check("global-metadata.dat readable (control: GameKitRuntime)", m_("GameKitRuntime") > 0, f"{gm} {len(meta)} bytes")
# The LevelPlay app key and unit ids live in the serialized config asset, not in IL2CPP metadata:
# the kit's BundleAudit checks those.
for marker in ("FirebaseAnalyticsService", "[Snapline] Firebase", "Snapline.Services",
               "LevelPlayService", "asking UMP directly, without the AdMob plugin",
               "[Snapline] analytics collection", "PRIVACY SETTINGS", "Privacy options failed"):
    check("metadata carries " + repr(marker), m_(marker) > 0, str(m_(marker)))
check("metadata free of 'GoogleMobileAds.Api'", m_("GoogleMobileAds.Api") == 0, str(m_("GoogleMobileAds.Api")))

print("\nRESULT:", "ALL PASS" if ok else "FAILURES ABOVE")
sys.exit(0 if ok else 1)
