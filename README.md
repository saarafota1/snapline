# Snapline

A block puzzle for Android. Eight-by-eight grid, three pieces offered at a time, drag one onto the
board and fill a row or a column to blow it up. Endless and score-driven — no levels, no timer. The
run ends when none of the three offered pieces fits anywhere.

Second game from Scibox Studios, after Plumbline. Built on `_StudioKit`.

---

## Layout

```
Assets/Snapline/
  Scripts/
    Core/          the rules engine — NO UnityEngine, enforced by asmdef
      Bits.cs          bitboard helpers for the 8x8 grid
      Board.cs         occupancy, placement, line detection and clearing
      Shapes.cs        the polyomino catalogue (33 shapes, stable save ids)
      Dealer.cs        fairness-aware piece dealing
      Scoring.cs       combos, multipliers, perfect clears
      GameRun.cs       board + tray + score + the game-over condition
      SaveCodec.cs     versioned, checksummed save serialisation
      Rng.cs           deterministic xorshift, so runs replay exactly
      Sim/             autoplayer and the measurement harness
    Art/           procedural sprites, palette, particles, synthesised audio
    View/          board, tray and drag input
    UI/            HUD and the game-over card
    App/           bootstrap, controller, persistence, screenshot harness
  Editor/          scene generation
  Tests/           EditMode tests for the engine
Tools/Bench/       console harness — plays the game headlessly and measures it
```

## The one structural rule

**`Snapline.Core` cannot reference `UnityEngine`.** This is not a convention — `Snapline.Core.asmdef`
sets `noEngineReferences: true`, so a stray `using UnityEngine` is a compile error rather than
something that quietly works until someone tries to run the engine off the main thread.

That is what makes `Tools/Bench` possible: it compiles the very same source files into a .NET 8
console app and plays millions of runs in seconds.

```bash
cd Tools/Bench
dotnet run -- check      # correctness assertions over the engine
dotnet run -- measure    # compare dealer policies at three skill levels
dotnet run -- sweep      # sweep the congestion weighting
dotnet run -- validate   # re-check candidates across the skill range
```

## Piece dealing

The single biggest factor in whether this genre feels good. Pure random dealing ends runs early and
feels unfair, so the dealer does two separable things:

1. **Congestion weighting** — a soft, always-on bias toward smaller shapes as the board fills, and
   toward shapes that currently have room. The player never notices it directly.
2. **Tray guarantee** — a hard filter after sampling. The default, `WholeTraySurvivable`, searches
   for an order and set of placements that gets all three pieces down. If one exists, the player
   cannot be killed by the tray, only by their own choices.

Every default is measured. From `dotnet run -- measure`, 400 runs per cell against the heuristic
autoplayer:

| policy | median run | runs under 20 pieces |
|---|---|---|
| pure random (control) | 44 pieces | 4.50 % |
| congestion weighting only | 137 pieces | 1.75 % |
| at-least-one-fits | 137 pieces | 1.75 % |
| whole-tray survivable | 173 pieces | 1.50 % |

Two results were not what the obvious guess predicted, and both changed the design:

- **`PlacementAwareness` dominates the size-based congestion bias.** With awareness at 0 the median
  run stays at 83 pieces no matter how hard congestion is pushed, across bias 0 through 5. How much
  room a shape currently has matters far more than how big it is.
- **`AtLeastOneFits` buys nothing.** Its numbers are identical to no guarantee at all, because once
  weighting is on, one piece essentially always fits. The expensive whole-tray search is the only
  guarantee that actually helps, and it costs 3.7 nodes and 26 µs per deal.

Shipped settings are `CongestionBias 2.2`, `PlacementAwareness 1.2`, `WholeTraySurvivable`:
median 293 pieces, 0.75 % of runs under 20.

**Caveat.** These are autoplayer numbers, and a model of a good player is not a player. The careless
autoplayer sits at a median of 29 pieces under *every* config tested, which says the dealer cannot
rescue bad play — only avoid punishing good play. Real session-length data should drive the final
tuning.

## Art and audio

Everything is generated at runtime — no art or audio files in the repo. Blocks are signed-distance
rounded rectangles with a vertical gradient, an inner glow biased toward the top, and a bevel;
particles are one soft dot sprite reused so the whole burst batches into a single draw call; sound
effects are synthesised oscillator envelopes, which is why a clear's pitch can rise with the number
of lines.

All of it is CPU pixel work on purpose. `Graphics.Blit` silently yields a blank texture under
`-nographics`, so GPU generation would come out empty in exactly the batch-mode runs used for store
screenshots.

The renderer is asset-agnostic: the engine deals in colour *indices* and the view maps those to
sprites, so generated blocks can be swapped for real artwork without gameplay changing.

## Saving

A run in progress is written to `PlayerPrefs` after every move and again on pause. Android does not
reliably call `OnApplicationQuit` when it kills a backgrounded app, so `OnApplicationPause` is the
only dependable moment to persist.

The payload is checksummed. A torn write is rejected at load and a fresh run starts, rather than
restoring half a board and looking like a gameplay bug. The RNG state is saved too, so a resumed run
deals exactly the pieces it would have dealt had the app never closed — there is a test for it.

## Verifying it actually works

Unit tests and the console harness prove the rules and the dealing. Neither can tell you the board
rendered. For that:

```bash
# build a non-development player, then let it play itself and screenshot the result
Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod StudioKit.EditorTools.BuildTool.BuildWindows -buildOut Builds/SnaplineSmoke.exe

Builds/SnaplineSmoke.exe -snapline-shots -snapline-shots-dir shots \
  -screen-width 540 -screen-height 960 -screen-fullscreen 0
```

It plays through the ordinary placement path, so the captures are what a player would see.

## Regenerating the scene

`Assets/Snapline/Scenes/Game.unity` holds one GameObject with `Bootstrap` on it and nothing else —
the whole interface is built in code. Rebuild it any time from **Snapline → Rebuild Game Scene**, or:

```bash
Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod Snapline.EditorTools.SceneBuilder.BuildSceneCLI
```

## Shipping

Process lives in `_StudioKit/Documentation~/NEW_GAME.md` and `PLAYBOOK.md`. Build a release with
**Studio > Release > Build Android Release...** — never File > Build Settings, which produces test
ad units and a debug signature.

### Permanent identifiers

Recorded here because they are painful to dig out later and some of them can never be changed.

| | |
|---|---|
| Package id | `com.sciboxstudios.snapline` — **permanent from the first upload** |
| Legal entity | Aplicatzia Software House LTD |
| IARC Global Rating ID | `b6f3e76f-65d4-8a2a-8a70-3f87c1ac943c` (rated 31 Aug 2026) |
| AdMob app id | `ca-app-pub-1931205009793131~9498502442` |
| AdMob rewarded (live) | `ca-app-pub-1931205009793131/6078132186` |
| AdMob interstitial (live) | `ca-app-pub-1931205009793131/4765050515` |
| Meta app id | `1726889715093878` |
| TikTok App ID | `7680497997288914964` — the numeric id, distinct from Plumbline's |
| Keystore | `C:\GamesProjects\_Keys\user.keystore`, alias `user` — shared by every Scibox game |
| Privacy policy | `https://scibox-studios.com/privacy` |

None of the above is a secret: the ad unit ids, the Meta app id and the client token all ship
inside the APK manifest and are readable by anyone who unzips it. The **Meta App Secret** and the
**keystore passwords** are the real secrets and appear nowhere in this repo.

### The IARC rating

The Play Console *Content rating* questionnaire is the IARC questionnaire; the Global Rating ID
above is its result and can be reused on any other storefront that licenses IARC (Amazon, Microsoft,
Epic) instead of retaking it.

**Retake it if an update would change the answers** — adding in-app purchases, user-generated
content, chat, or player-named leaderboards would. Ads alone do not, provided ads were declared the
first time.

Not to be confused with *Target audience and content*, which is a separate form and must stay
**13+**. Any band under 13 pulls the app into the Families programme, which bans personalised ads.
Rated Everyone, targeted 13+ is the correct combination for this game.
