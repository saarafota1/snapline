# Snapline — what's done, and what needs you

Updated 28 Aug 2026. The game is playable and verified end to end. Everything below is either a
decision only you can make, or a step that needs a credential I should not have.

## Added 28 Aug

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
| Rules engine | 8x8 bitboard, 33 shapes, placement, row/column clears, combos, perfect clears |
| Dealing | fairness-aware, measured against a control group, defaults chosen from a sweep |
| Save / resume | run survives being killed; RNG state saved so the next pieces are unchanged |
| High score | persisted, plus games played, lifetime lines and best combo, all shown on the menu |
| Art | fully procedural — gradients, bevels, inner glow, particle bursts, screen shake |
| Audio | fully synthesised at runtime, clear pitch rises with lines cleared |
| Tests | 16/16 EditMode, including a version-1 save compatibility test; engine self-check with a 3,729-tray audit |
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

**3. Game modes and leaderboard — the design call.** You asked for two modes: endless-for-score
(built) and a level ladder from easy to hard. The menu is laid out with room for a mode picker and a
leaderboard button so neither moves anything when added.

What a "level" *is* changes the engine, so it's worth picking before I build it. The engine has no
concept of an objective today — a run just ends when nothing fits. The usual options:

| Level goal | What it needs | Feel |
|---|---|---|
| Clear N lines within M moves | a move counter and a line target | tight, puzzle-like, easy to tune |
| Reach a target score within M moves | a move counter and a score target | closest to the endless mode |
| Clear pre-placed "blocker" cells | blockers in the board and the generator | most visually distinct, most work |
| Survive N moves on a pre-filled board | a starting layout per level | quickest to build, least varied |

My recommendation is **clear N lines within M moves**, with a hand-tuned curve of ~60 levels. It
reuses everything that exists, the difficulty is a single pair of numbers per level so the harness
can verify every level is actually solvable, and it reads clearly on a level-select screen.

Leaderboard needs Unity Gaming Services packages and a project on their dashboard — that's an
account step, and it adds a Data Safety entry, so it's worth doing after the modes are settled.

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

**5. GitHub remote.** I committed locally (3 commits) but did not create the remote — that's your
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
