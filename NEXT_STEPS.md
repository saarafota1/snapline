# Snapline — what's done, and what needs you

Written 27 Aug 2026. The game is playable and verified end to end. Everything below is either a
decision only you can make, or a step that needs a credential I should not have.

---

## Done and verified

| | |
|---|---|
| Rules engine | 8x8 bitboard, 33 shapes, placement, row/column clears, combos, perfect clears |
| Dealing | fairness-aware, measured against a control group, defaults chosen from a sweep |
| Save / resume | run survives being killed; RNG state saved so the next pieces are unchanged |
| High score | persisted, plus games played, lifetime lines and best combo |
| Art | fully procedural — gradients, bevels, inner glow, particle bursts, screen shake |
| Audio | fully synthesised at runtime, clear pitch rises with lines cleared |
| Tests | 13/13 EditMode; engine self-check with a 3,729-tray audit |
| Input | 38/38 simulated drags landed on the intended cell |
| Presentation | verified by screenshotting a self-playing non-development build |

App id is set to `com.sciboxstudios.snapline`, product name `Snapline`, ARM64 on, portrait.

### How to try it

Desktop, if the APK is not to hand:

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

**3. Tuning against real players.** The dealer defaults come from an autoplayer, and a model of a
good player is not a player. The numbers say a strong player gets a median run of ~293 pieces. Play
ten runs and tell me whether it ends too early, too late, or about right — `CongestionBias` and
`PlacementAwareness` in `Dealer.cs` are the two knobs, and `Tools/Bench` re-measures any change in
seconds.

Worth knowing: the careless autoplayer sits at a median of 29 pieces under *every* configuration
tested. The dealer can stop good play being punished; it cannot rescue bad play. If early runs feel
short, that may be the honest answer rather than a bug.

---

## Needs a credential I shouldn't have

**4. GitHub remote.** I committed locally (3 commits) but did not create the remote — that's your
account and an outward-facing action. When you're ready:

```bash
cd C:\GamesProjects\Snapline
gh repo create saarafota1/snapline --private --source=. --remote=origin --push
```

**5. Signed bundle.** Run this yourself so the passwords stay out of any shared log:

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
