# Changing the look yourself

The whole interface is built in code — the scene holds one empty object and everything else is
constructed at runtime. There is nothing to drag in the editor. That sounds worse than it is: it
means every value is a number you can find and change, rather than something buried in a scene file.

Four kinds of change, and they work differently.

---

## 1. Sizes, spacing, type — edit a number

**`Assets/Snapline/Scripts/UI/Design.cs`** holds everything shared across screens.

```csharp
public const int TitleSize   = 96;   // LEVELS, BEST SCORES, TOOLBOX
public const int ButtonSize  = 68;   // PLAY, CONTINUE
public const int BodySize    = 36;   // the line under a button
public const int  DisplayThreshold = 44;  // at/above this a label uses the heavy weight
public const float PressScale = 0.94f;   // how far a button squashes when held
```

Change one, save, press play. It applies everywhere that thing is used, so raising `BodySize` fixes
every secondary line at once instead of six of the eight.

**Where a particular thing sits** is per-screen, at the top of that screen's file under `Layout`.
`MainMenu.cs` has:

```csharp
// measured DOWN from the top
public const float StatusY    = 78f;    // gear, coins, toolbox, sound
public const float LogoY      = 242f;
public const float BoardTop   = 404f;   // top edge of the board area
public const float BoardSize  = 664f;   // its largest size; it shrinks below this

// measured UP from the bottom, then scaled (see below)
public const float DesignHeight = 2340f;
public const float PlayY      = 1140f;  // height 250
public const float ModesY     = 865f;   // height 250
public const float DailyY     = 540f;   // height 300
public const float BottomRowY = 240f;
```

**The canvas is always 1080 wide. Its height is whatever the phone's aspect makes it** — 1920 on an
old 16:9 screen, 2340 on most modern Android hardware. That is why the block is split in two:

- **Measured DOWN from the top:** the status bar, the wordmark, the board. These are attached to the
  top of the screen, so that is what they are measured from.
- **Measured UP from the bottom:** the play button, the modes, the daily strip, the icon row. A value
  measured from the top would put these off-screen on a short phone and strand them halfway up a
  tall one.

The bottom-measured values are authored against `DesignHeight = 2340f` and scaled by the real canvas
height over that, clamped to 0.76-1.12. So on a 2340 phone they are used exactly as written; on a
16:9 screen the whole lower block compresses by about 18% together, rather than one element
absorbing the entire 420-unit difference.

**The board takes up the slack.** It is the only thing on the screen measured from neither edge — it
sits below the wordmark and shrinks until it clears the play button. That is deliberate: it is the
one element that can lose size without losing meaning.

Widths are in the same units, where 1080 is the full screen width.

### If you are reading values out of Play Mode

Tell me the Game view resolution as well as the numbers. A Y of -2100 is perfectly sensible on a
1080x2340 view and off the bottom of the screen on 1080x1920, and I cannot tell which you meant from
the number alone.

---

## 2. Colours — usually a different sprite, not a different colour

This is the one that catches people.

**Button colours are artwork.** A pink button is the file `tile_pink`. It is not a white button with
a pink tint, and it cannot be made green by changing a colour value — multiplying a colour through
glossy artwork muddies its highlight rather than recolouring it. To change a button's colour you
change *which sprite it asks for*:

```csharp
// in MainMenu.cs
Button levels = CandyUI.SpriteButton("Levels", _root, ArtKit.Ui("tile_purple"), Image.Type.Sliced);
//                                                                  ^^^^^^^^^^^ change this
```

What exists today: `tile_pink`, `tile_purple`, `tile_cyan`, `tile_green`, `tile_yellow`, `tile_navy`
for wide buttons; `circle_pink`, `circle_purple`, `circle_blue`, `circle_green`, `circle_gold` for
round ones; `pill_red`, `pill_blue`, `pill_purple`, `pill_teal`, `pill_yellow`, `pill_green`,
`pill_white`, `pill_gold` for capsules. Anything else needs new art.

**Text colours are real values** and live in `Design.cs`:

```csharp
public static readonly Color Text       = Color.white;          // on saturated buttons
public static readonly Color TextOnPale = new Color(0.28f, 0.13f, 0.02f, 1f);  // on yellow/cream
public static readonly Color OutlineColour = new Color(0.20f, 0.07f, 0.24f, 0.55f);
public const float OutlineOffset = 2.5f;   // set to 0 to remove outlines entirely
```

---

## 3. Rounded corners — cannot be changed in code at all

Every button and panel is a drawn PNG. Its corners are painted pixels, not a radius. There is no
number anywhere that makes a button rounder or squarer; that needs new artwork.

What *is* controlled in code is how a sprite stretches, and getting that wrong looks like a corner
problem. In `Assets/Snapline/Editor/ArtImportSettings.cs`:

```csharp
private const float TileFraction  = 0.22f;   // rounded-square buttons
private const float FrameFraction = 0.14f;   // the board frame
```

That fraction is how much of each edge is treated as "corner" and left unstretched. Too low and the
corners distort when a button is made wide; too high and the middle of the sprite gets eaten. If a
button's ends look wrong at some width, this is the number — not the artwork.

**After changing it, run `Snapline → Reimport Art` from the Unity menu.** Import settings only apply
when an asset is imported, so existing files keep their old borders until forced through again.

---

## 4. Fonts — Heebo, in two weights

`Heebo-Bold.ttf` and `Heebo-Black.ttf` live in `Assets/Snapline/Resources/Snapline/Fonts/` and ship
inside the build. Which one a label gets is decided by its size:

```csharp
public const int DisplayThreshold = 44;   // at or above this, Black; below it, Bold
```

Lower it to make more text heavy, raise it for less. Adding a third weight — Medium for small print,
say — is one more `.ttf` in that folder and one line in `Design.For`.

The font is applied by a sweep over every label at startup rather than at each call site, because the
HUD, both result cards, `UIKit.Button` and the kit's popup pool all create labels where this game has
no call site to change. That sweep lives in `Bootstrap.Awake` and **must stay above the two
screenshot-harness branches**, which return early — below them the font applied in ordinary play and
in no captured screenshot, including the ones destined for the Play listing.

---

## The fastest way to find a number

Guessing coordinates from a screenshot is slow. Do this instead:

1. Open the project in Unity and press **Play**.
2. The interface builds itself, so the **Hierarchy** now shows every element by name —
   `MainMenu → Daily → Day3`, `BoardPreview → Frame`, and so on.
3. Select one and edit its **RectTransform** in the Inspector. Drag the numbers and watch it move
   live.
4. When it looks right, **write the numbers down** — Play Mode changes are discarded when you stop.
5. Put them in the `Layout` block, or send them over and say which element they belong to.

Names in the Hierarchy match the strings in the code, so anything you find there is greppable.

## Seeing a change without opening Unity

```bash
# build, then let it play itself and screenshot every screen
Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod StudioKit.EditorTools.BuildTool.BuildWindows -buildOut Builds/CandyShots.exe

Builds/CandyShots.exe -snapline-shots -snapline-shots-dir shots \
  -screen-width 720 -screen-height 1280 -screen-fullscreen 0
```

That writes thirteen PNGs covering the menu, gameplay, both result cards, level select and the score
table. It is how every screenshot in this redesign was checked, and it catches things that reading
the code does not — the empty cells rendering as filled blocks, and the checkerboard in the clear
effect, were both found this way and neither was visible in the source.
