# Snapline — what's done, and what needs you

Updated 28 Aug 2026. The game is playable and verified end to end. Everything below is either a
decision only you can make, or a step that needs a credential I should not have.

## Store artwork — done, in `StoreAssets/`

Not in git: `.gitignore` excludes `StoreAssets/`, the studio convention for build outputs. The
tooling that makes them is committed, so they regenerate at any time.

| File | Size | Play slot |
|---|---|---|
| `icon-512.png` | 512×512 | Store listing icon |
| `icon-1024.png` | 1024×1024 | Source for the launcher icon (already installed in the project) |
| `feature-graphic-1024x500.png` | 1024×500 | Feature graphic |
| `1-menu.png` … `7-best-scores.png` | 1080×1920 | Phone screenshots (7 of the 8 allowed) |

Regenerate:

```bash
Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod Snapline.EditorTools.StoreAssets.GenerateIconsCLI

Builds/SnaplineShots.exe -snapline-feature -snapline-store-dir StoreAssets \
  -screen-width 1024 -screen-height 500 -screen-fullscreen 0

Builds/SnaplineShots.exe -snapline-store -snapline-store-dir StoreAssets \
  -screen-width 540 -screen-height 960 -screen-fullscreen 0
```

The screenshots are **real gameplay from a non-development build** — the harness wipes progress and
genuinely beats levels 1–8 and plays an endless run before capturing. Play rejects mock-ups, so this
matters. The stats visible in them (31,070 best, 8 levels, 24 stars) were earned during that run.

---

## Adding ads

**Decide first: where do ads go?** This is a design call, not a technical one. My recommendation for
a casual puzzle:

| Placement | Why |
|---|---|
| **Rewarded "CONTINUE"** on game over | Highest value per impression and player-initiated, so it never feels like an interruption. The single best fit for an endless scoring game. |
| **Interstitial** after game over | Capped — never on the first 2 games of a session, and no more than one every ~3 minutes. |
| **No banner** | It would eat board width, and the board is already the width-constrained element. |

**What you need to get (account steps I can't do):**

1. An **AdMob app** for `com.sciboxstudios.snapline` → gives an App ID (`ca-app-pub-…~…`)
2. Two **ad units** → an Interstitial ID and a Rewarded ID
3. The **Google Mobile Ads Unity plugin** `.unitypackage` downloaded

**What I do once you have those:**

4. Import it via the kit: `AdMobSetup.Import -admobPackage GoogleMobileAds.unitypackage`
5. Write `AdPolicy` (frequency caps, cooldowns) and the revive flow, behind the kit's
   `IAdService` — the game never touches the vendor SDK, so swapping networks later is one adapter
6. Wire the App ID into the manifest via the kit's AdMob sync

**One step only you can do, in the editor:** `Assets → External Dependency Manager → Android
Resolver → Resolve`. This is not optional and it fails *silently*: without it the manifest lacks
`com.google.android.gms.ads.APPLICATION_ID`, and the Google SDK deliberately throws on launch. The
build succeeds, the APK installs, the app dies instantly, and nothing appears in the build log. The
kit's `AndroidBuildGuard` now fails the build instead of shipping that.

**Non-negotiable:** ship Google's **test** ad IDs until the very last build. Clicking your own live
ads is the fastest way to get the AdMob account permanently banned, and a ban takes the whole
portfolio, not just this app.

**Cost:** adding ads puts three entries on the Data Safety form — Device IDs, Approximate location,
App activity, purpose Advertising. "No data collected" while shipping the ads SDK is a false
declaration and a common suspension reason; Play scans the bundle.

---

## Deploying to Google Play

Already done and verified: app id `com.sciboxstudios.snapline`, ARM64-only, every native library
16 KB aligned, targetSdk 36, portrait, launcher icon installed, store artwork ready.

**Needs you:**

1. **Keystore alias.** Studio convention is one keystore, a new alias per game:
   ```bash
   keytool -genkeypair -v -keystore "C:\Users\saara\Projects\Games\Flying Penguin Saga\user.keystore" \
     -alias snapline -keyalg RSA -keysize 2048 -validity 10000
   ```
2. **Signed bundle** — run it yourself so passwords stay out of any shared log (command in the
   section below). Play needs `.aab`, not `.apk`, and rejects debug-signed uploads.
3. **Play Console**: create the app, paste the listing, upload the graphics.
4. **Content rating** questionnaire → puzzle, no objectionable content → Everyone.
5. **Data safety** → whatever the ads decision above lands on.
6. **Target audience 13+.** Never tick a band under 13 on an ad-funded game: it triggers the
   Families programme, personalised ads stop, and revenue collapses.
7. **Privacy policy** — add Snapline to the list at `scibox-studios.com/privacy-policy`.
8. **Register the package** for Android developer verification, or the app gets removed.
9. Upload to **Internal testing** first. It is the only way to verify real ads on a device.

Run `Studio → Release → Check Readiness` before the final build; it enforces most of the above.

### Draft listing text

**Title** (30 char limit): `Snapline: Block Puzzle` — 22 characters.

**Short description** (80 char limit):
> Drop blocks, clear lines, chase combos. 60 levels plus an endless high-score run.

**Full description**:
> Fit blocks onto the grid. Fill a row or a column and it explodes.
>
> Snapline is a block puzzle built around one idea: it should never feel unfair. The pieces you are
> offered are chosen with the board in mind, so a run ends because of a decision you made, not
> because the game handed you something that could not fit.
>
> • ENDLESS — no timer, no levels, just you and your best score
> • 60 LEVELS — clear a line target inside a move budget, three stars for finishing with moves to spare
> • COMBOS — keep clearing to build a multiplier, with a little forgiveness so streaks are actually reachable
> • PICK UP WHERE YOU LEFT OFF — a run in progress survives closing the app
> • PLAYS OFFLINE — no account, no sign-in

---

## Added 28 Aug — levels, scores, leaderboard status

- **Level mode.** 60 levels, "clear N lines in M moves", three stars for finishing with moves to
  spare. Level grid with lock and star state, result card with staggered star pops, RETRY and
  NEXT LEVEL.
- **The ladder is verified, not guessed.** `dotnet run -- levels` plays all 60 levels 120 times each
  and reports the beat rate. The first curve had **20 unbeatable levels** — it costed levels purely
  per required line and ignored that a level starts on an empty board, where the opening pieces
  cannot complete anything. Fixed and swept; every level is now beatable, level 1 at 100% / 3 stars
  and level 60 at 73% / 1.16 stars against a strong autoplayer.
- **BEST SCORES screen** — your ten best runs with dates, plus lifetime stats.
- A level run **never writes the endless save slot**, so dipping into level 3 cannot destroy a long
  run sitting behind the menu's CONTINUE.

## Added 28 Aug — menu, share, combos

- **Main menu** — title, best score with lifetime stats, PLAY / CONTINUE + NEW GAME, SHARE, sound
  toggle, drifting blocks behind. A run in progress turns PLAY into CONTINUE and reveals NEW GAME,
  so a saved board is never thrown away by someone tapping the big green button.
- **MENU button** in the HUD and on the game-over card. The run saves after every move, so leaving
  is lossless and CONTINUE puts you straight back.
- **Share** — native Android share sheet, no SDK and no Data Safety entry. Falls back to the
  clipboard on desktop so the button is honest in test builds.
- **Combos made generous and loud.** Streaks now survive one dry move (measured: see below), and a
  clear can now show praise (`NICE!` → `LEGENDARY!`), the streak (`COMBO x3`), the multiplier
  (`x2 POINTS`) and the points earned, each gated so an ordinary clear stays quiet.
- **Live multiplier in the HUD** — the combo badge reads `COMBO x3 · x2 PTS`.

Save format went to version 2. A version 1 save from the build you were playing still loads, and
there is a test that proves it.

---

## Done and verified

| | |
|---|---|
| Rules engine | 8x8 bitboard, 33 shapes, placement, row/column clears, combos with grace, perfect clears, level objectives |
| Dealing | fairness-aware, measured against a control group, defaults chosen from a sweep |
| Save / resume | run survives being killed; RNG state saved so the next pieces are unchanged |
| High score | persisted, plus games played, lifetime lines and best combo, all shown on the menu |
| Art | fully procedural — gradients, bevels, inner glow, particle bursts, screen shake |
| Audio | fully synthesised at runtime, clear pitch rises with lines cleared |
| Tests | 21/21 EditMode, including v1-save compatibility and level objectives; engine self-check with a 3,729-tray audit |
| Input | every simulated drag lands on the intended cell; asserted on each screenshot run |
| Presentation | verified by screenshotting a self-playing non-development build |

App id is set to `com.sciboxstudios.snapline`, product name `Snapline`, ARM64 on, portrait.

Also verified against the real binary rather than assumed:

- **Save survives a hard kill.** I `kill -9`'d the player mid-run — no clean shutdown, no
  `OnApplicationQuit` — and on relaunch the board came back with all 14 blocks, score 351 and a full
  tray. That is the actual Android failure mode, not a graceful quit.
- **16 KB page alignment.** `llvm-readelf -l` on `libunity.so`, `libil2cpp.so` and `libmain.so` in
  the built APK reports `0x4000` for every LOAD segment. ARM64 only, no armv7 slice.

### How to try it

**On your phone** — `Builds/Snapline-test.apk` (22.7 MB) is built and ready to sideload:

```bash
adb install -r "C:\GamesProjects\Snapline\Builds\Snapline-test.apk"
```

It is debug-signed, which is fine for sideloading and is exactly why Play would reject it — the kit
warned about this during the build. The signed `.aab` is step 6 below.

**On desktop**, if the phone isn't to hand:

```bash
Builds/SnaplineSmoke.exe -screen-width 540 -screen-height 960 -screen-fullscreen 0
```

Drag with the mouse. Add `-snapline-shots -snapline-shots-dir shots` to watch it play itself and
write screenshots instead.

---

## Needs a decision from you

**1. The name, one last time.** *Snapline* has no app of that name on Play — I checked the store
search page directly and read the returned titles. But `SnapLine™` appears on Graco Inc's trademark
list, on construction equipment. Different Nice class, marked ™ rather than ®, so the risk is low —
but the package id is permanent from first upload, so this is the moment to change course if you'd
rather not have it near your brand at all. Verified free as of last night if you want alternatives:
**Tuckpoint**, **Chockfull**, **Setsquare**.

**2. Ads: in or out?** No ads SDK is installed right now, which keeps the Data Safety form to
essentially nothing. Adding AdMob means declaring Device IDs, Approximate location and App activity,
plus creating the AdMob app and two ad units. Say the word and I'll wire it through the kit's
`AdMobSetup` — it goes behind the kit's interface, so the game never touches the vendor SDK.

Whatever we do: ship Google's **test** ad ids until the very last build. Clicking your own live ads
gets the AdMob account banned, and a ban takes the whole portfolio.

**3. Global leaderboard needs a Unity Gaming Services project.** This is the one thing from your
list I could not finish, and it's an account step rather than a code one.

What exists now is a **local BEST SCORES table** — your ten best runs with dates, on its own screen,
reachable from the menu. That's complete and useful on its own, and it's what most casual games
actually show.

A *global* board needs: the UGS packages added, a project created on the Unity dashboard, and the
project ID pasted into Player Settings. It also adds an entry to the Data Safety form. The kit
already has `ILeaderboardService` with an offline fallback, so the wiring is small once the project
exists — say the word and give me the project ID and I'll finish it.

**4. Tuning against real players.** The dealer defaults come from an autoplayer, and a model of a
good player is not a player. The numbers say a strong player gets a median run of ~293 pieces. Play
ten runs and tell me whether it ends too early, too late, or about right — `CongestionBias` and
`PlacementAwareness` in `Dealer.cs` are the two knobs, and `Tools/Bench` re-measures any change in
seconds.

Worth knowing: the careless autoplayer sits at a median of 29 pieces under *every* configuration
tested. The dealer can stop good play being punished; it cannot rescue bad play. If early runs feel
short, that may be the honest answer rather than a bug.

---

## Needs a credential I shouldn't have

**5. GitHub remote.** I have been committing locally but did not create the remote — that's your
account and an outward-facing action. When you're ready:

```bash
cd C:\GamesProjects\Snapline
gh repo create saarafota1/snapline --private --source=. --remote=origin --push
```

**6. Signed bundle.** Run this yourself so the passwords stay out of any shared log:

```bash
"C:\UnityVersions\6000.2.7f2\Editor\Unity.exe" -batchmode -nographics -quit ^
  -projectPath "C:\GamesProjects\Snapline" ^
  -executeMethod StudioKit.EditorTools.BuildTool.BuildAndroidRelease ^
  -buildOut "Builds\Snapline.aab" ^
  -keystore "C:\Users\saara\Projects\Games\Flying Penguin Saga\user.keystore" ^
  -keystorePass <pw> -keyalias snapline -keyaliasPass <pw>
```

Note the alias: the studio convention is one keystore, a **new alias per game**, so `snapline`
rather than `user`. Create it first if it doesn't exist.

---

## Remaining before submission

- [ ] Icon: I have no source art. Give me a 1024x1024 PNG and `IconSetup.Install` handles every
      Android slot, or say the word and I'll generate one procedurally to match the in-game look.
- [ ] Store listing: title (**"Snapline: Block Puzzle"**, 22 of the 30 characters allowed), short
      and full description, feature graphic.
- [ ] Screenshots: `SmokeShots` already produces real gameplay captures from a non-development
      build, which is what Play requires — AI mockups violate listing policy. Needs a final pass at
      phone resolution once the look is signed off.
- [ ] Privacy policy: add Snapline to the list at `scibox-studios.com/privacy-policy`.
- [ ] Register `com.sciboxstudios.snapline` for Android developer verification.
- [ ] Target audience **13+**. Never tick a band under 13 on an ad-funded game — it triggers the
      Families programme and revenue collapses.
- [ ] Run `Studio → Release → Check Readiness` before building the final bundle.

---

## Things I deliberately did not do

- **Did not create the GitHub repo or push.** Outward-facing, your account.
- **Did not install any ads SDK.** That's decision 2 above, and it changes the Data Safety form.
- **Did not hand-tune the dealer past what the data supports.** The sweep picked the defaults; going
  further needs real play, not more simulation against my own autoplayer.
- **Did not switch the UI to TextMeshPro.** Legacy `UI.Text` is what's shipping and it renders
  correctly. `ProjectSetup` has since imported TMP's Essential Resources, so the upgrade is
  available if you want crisper large text — every label goes through `UIKit`, so it's contained.

---

## If something looks wrong

The engine is plain C# with no Unity dependency, so most questions can be answered without opening
the editor:

```bash
cd Tools/Bench
dotnet run -- check      # correctness assertions
dotnet run -- measure    # dealer policy comparison
dotnet run -- validate   # candidate configs across skill levels
```

Every run is seeded, so a run you report can be replayed exactly from the seed in your save.
