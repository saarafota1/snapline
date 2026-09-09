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
public const float PrimaryButtonHeight = 162f;
public const float PressScale = 0.94f;   // how far a button squashes when held
```

Change one, save, press play. It applies everywhere that thing is used, so raising `BodySize` fixes
every secondary line at once instead of six of the eight.

**Where a particular thing sits** is per-screen, at the top of that screen's file under `Layout`.
`MainMenu.cs` has:

```csharp
public const float StatusY  = 78f;    // gear, coins, toolbox, sound
public const float LogoY    = 242f;
public const float BoardY   = 684f;
public const float PlayY    = 1108f;
public const float ModesY   = 1288f;
public const float DailyY   = 1494f;
public const float BottomRowY = 1742f;
```

**Every Y is distance down from the top of the screen, on a 1920-tall canvas.** Raise a number to
move that thing down. They share one budget deliberately — the screen used to anchor its top half to
the top and its bottom half to the bottom, and the moment a section grew, the two halves collided.
The numbers currently total about 1850 of 1920, so there is roughly 70 spare before something has to
shrink to make room.

Widths are in the same units, where 1080 is the full screen width.

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

## 4. Fonts — one place, but it needs a font file

Everything uses Unity's built-in Arial, chosen through `UIKit.Font` in the shared kit. There is no
font file in the project.

To change it: drop a `.ttf` into `Assets/Snapline/Resources/Snapline/`, then have the font loaded
there instead. It is a contained change because every label in the game is created through one
function — but it does touch the shared kit, so ask before doing it; the same file is used by five
other games.

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
