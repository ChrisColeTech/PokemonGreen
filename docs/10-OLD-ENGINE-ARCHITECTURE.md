# Old Pokemon Game Engine Architecture Reference

Complete reference documentation for the original PokemonGameEngine by Kermalis, located at `D:\Projects\PokemonGameEngine\PokemonGameEngine`. This document serves as a comprehensive guide for rebuilding features in the new MonoGame-based engine (PokemonGreen).

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Rendering System](#2-rendering-system)
3. [Font System](#3-font-system)
4. [UI/GUI System](#4-uigui-system)
5. [Color System](#5-color-system)
6. [Battle System](#6-battle-system)
7. [World/Overworld](#7-worldoverworld)
8. [Script System](#8-script-system)
9. [Input System](#9-input-system)
10. [Asset Pipeline](#10-asset-pipeline)
11. [3D Models](#11-3d-models)
12. [Audio](#12-audio)

---

## 1. Project Structure

### Root Namespace
`Kermalis.PokemonGameEngine`

### Target Framework
.NET 6.0 (`net6.0`), C# with `AllowUnsafeBlocks` enabled.

### Entry Point
`Kermalis.PokemonGameEngine.Core.Engine.Main()`

### Directory Layout

```
PokemonGameEngine/
  Assets/                    # All game assets (shared in Debug, copied in Release)
    BattleBG/                # 3D battle background models (.dae)
      Cave/
      Dark/
      Grass/
      PlatformCaveAlly/
      PlatformCaveFoe/
      PlatformDark/
      PlatformGrassAlly/
      PlatformGrassFoe/
      PlatformRotation/
      PlatformTallGrassAlly/
      PlatformTallGrassFoe/
    Blockset/                # Tileset block data
    Encounter/               # Encounter data (JSON source, compiled binary)
    Fonts/                   # .kermfont files
      Default.kermfont
      DefaultSmall.kermfont
      PartyNumbers.kermfont
      Battle.kermfont
      Braille.kermfont
    Layout/                  # Map layout data
    Map/                     # Map definition data
    ObjSprites/              # Overworld character sprites
    Pkmn/                    # Pokemon sprite data
    Pokedata/                # Pokemon base stats, evolution, etc.
    Script/                  # Compiled script binary (Scripts.bin)
    Shaders/                 # GLSL shader source files
      Battle/
      GUIs/
      Transitions/
      World/
    Sound/
      BGM/                   # Background music (.wav)
      Cries/                 # Pokemon cries (.wav)
    Sprites/                 # General sprites
    Tileset/                 # Tileset image data and animations
  Core/                      # Engine core, game loop, asset loading
    AssetLoader.cs
    BackTask.cs
    BattleEngineDataProvider.cs
    BattleMaker.cs
    ConnectedList.cs
    Engine.cs
    FlagsVars.cs
    Game.cs
    GameStats.cs
    OTInfo.cs
    StringBuffers.cs
    Utils.cs
  Debug/                     # Debug logging
    Log.cs
  Dependencies/              # External DLLs (SimpleGIF.dll)
  Input/                     # Input management
    AxisData.cs
    Controller.cs
    InputManager.cs
    Key.cs
    Keyboard.cs
    Mouse.cs
    MouseButton.cs
    PressData.cs
  Item/                      # Item system
    Inventory.cs
    InventoryPouch.cs
    InventorySlot.cs
    ItemData.cs
    ItemEnums.cs
    ItemType.cs
  Pkmn/                      # Pokemon data structures
    BoxPokemon.cs
    PartyPokemon.cs
    Party.cs
    Moveset.cs
    Evolution.cs
    Pokerus.cs
    Pokedata/                # Static pokemon data
      BaseStats.cs
      EggMoves.cs
      EvolutionData.cs
      LevelUpData.cs
  Player/                    # Player state
    Daycare.cs
    PlayerInventory.cs
    Pokedex.cs
    Save.cs
  Render/                    # All rendering code
    Battle/                  # Battle GUI (partial classes)
    GUIs/                    # Font, Window, StringPrinter, GUIChoices
    Images/                  # AnimatedImage, Image, PokemonImageLoader
    OpenGL/                  # FrameBuffer, GLTextureUtils, InstancedData
    Pkmn/                    # PartyGUI, SummaryGUI, PCBoxesGUI
    Player/                  # BagGUI
    R3D/                     # 3D rendering (Camera, Model, Mesh, Assimp)
    Shaders/                 # Shader C# wrapper classes
      Battle/
      GUIs/
      Transitions/
      World/
    Transitions/             # Fade, battle transition effects
    World/                   # Overworld rendering, MapRenderer, DayTint
  Script/                    # Script VM and commands
    ScriptCommands.cs        # Main command dispatch (switch statement)
    ScriptCommands_Messages.cs
    ScriptCommands_Pokemon.cs
    ScriptCommands_Daycare.cs
    ScriptCommands_Field.cs
    ScriptCommands_Trainer.cs
    ScriptContext.cs          # Script VM state machine
    ScriptEnums.cs
    ScriptLoader.cs
    ScriptMovement.cs
    ScriptUtils.cs
  Sound/                     # Audio system
    MusicPlayer.cs
    SoundChannel.cs
    SoundChannelState.cs
    SoundControl.cs
    SoundMixer.cs
    WaveFileData.cs
  Trainer/                   # Trainer battle data
    TrainerConstants.cs
    TrainerCore.cs
  World/                     # World logic (maps, objs, warps)
    Data/
    Maps/
    Objs/
    Overworld.cs
    WorldConstants.cs
```

### Key NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Silk.NET.OpenGL` | 2.13.0 | OpenGL 4.2 bindings |
| `Silk.NET.SDL` | 2.13.0 | SDL2 windowing, input, audio |
| `Silk.NET.Assimp` | 2.13.0 | 3D model importing |
| `EndianBinaryIO` | 1.1.2 | Binary file reading/writing |
| `Newtonsoft.Json` | 13.0.2 | JSON serialization |
| `SixLabors.ImageSharp` | 2.0.0 | Image loading/saving |
| `Microsoft.Data.Sqlite` | 6.0.2 | SQLite database access |

### External Dependencies (DLLs)
- `PokemonBattleEngine.dll` -- Battle logic engine
- `PokemonBattleEngine.DefaultData.dll` -- Default AI and data
- `SimpleGIF.dll` -- GIF animation handling

---

## 2. Rendering System

### Graphics API
**OpenGL 4.2 Core Profile** via Silk.NET, using SDL2 for window management.

OpenGL 4.2 is specifically required for `glDrawArraysInstancedBaseInstance`, used in instanced rendering of font characters and map tiles.

### Display System (`Display.cs`)

The `Display` class is the central rendering coordinator:

```csharp
internal static unsafe class Display
{
    private const string WINDOW_TITLE = "Pokemon Game Engine";
    private const int AUTOSIZE_WINDOW_SCALE = 3;

    public static readonly GL OpenGL;
    public static readonly Sdl SDL;
    public static bool AutosizeWindow = true;
    public static Vec2I ViewportSize;
    public static Vec2I ScreenSize;
    public static Rect ScreenRect;
    public static float DeltaTime;
}
```

**Key details:**
- Window created via SDL2 with `WindowFlags.WindowOpengl | WindowFlags.WindowResizable`
- Fullscreen toggled via `FULLSCREEN` compile flag (`WindowFlags.WindowFullscreenDesktop`)
- VSync enabled (`GLSetSwapInterval(1)`)
- Alpha blending enabled globally: `BlendFunc(SrcAlpha, OneMinusSrcAlpha)`
- Aspect-ratio preserving viewport scaling: the `ScreenRect` is computed to maintain the aspect ratio of `ScreenSize` within the actual window size
- `DeltaTime` is capped at 1.0 second maximum
- Screenshot support via F12 key, saves to `Screenshots/` folder as PNG

### FrameBuffer Architecture

The engine renders everything to **FrameBuffers** (FBOs), not directly to the screen. Each major system has its own framebuffer(s):

- **Overworld**: `_frameBuffer` (320x180) + `_dayTintFrameBuffer` (320x180)
- **Battle**: `_frameBuffer` (480x270) + `_dayTintFrameBuffer` (480x270) + `_shadowFrameBuffer` (512x512)
- **Windows**: Each `Window` has its own `FrameBuffer` for its content

The final framebuffer is blitted to screen via `_frameBuffer.BlitToScreen()` which calls `Display.RenderToScreen(texture)`.

### Render Sizes

| Context | Resolution | Aspect Ratio |
|---------|-----------|--------------|
| Overworld | 320 x 180 | 16:9 |
| Battle | 480 x 270 | 16:9 |
| Shadow Map | 512 x 512 | 1:1 |

### Main Loop (`Engine.cs`)

```
Init()
  -> Display static ctor (SDL + OpenGL init)
  -> SoundMixer static ctor (SDL Audio init)
  -> AssimpLoader static ctor (Assimp init)
  -> AssetLoader.InitBattleEngineProvider()
  -> InputManager.Init()
  -> RenderManager.Init()
  -> new Game()

Main Loop:
  while (!QuitRequested)
    InputManager.Prepare()
    HandleOSEvents()
    Display.PrepareFrame(ref time)
    Game.Instance.RunCallback()   <-- THE callback-driven game loop
    Display.PresentFrame()
```

### Callback-Driven Architecture

The game uses a **single callback** pattern rather than a traditional state machine or game state stack. `Game.Instance.Callback` is a single `Action` delegate that is swapped out to change what the game does each frame:

```csharp
public void SetCallback(Action main) { Callback = main; }
public void RunCallback() { Callback.Invoke(); }
```

Each callback typically: updates logic, renders to the framebuffer, then blits to screen.

### Shader Pipeline

All shaders are loaded from GLSL source files in `Assets/Shaders/`. The base class `GLShader` handles compilation and linking:

```csharp
internal abstract class GLShader
{
    public readonly uint Program;

    protected GLShader(GL gl, string vertexAsset, string fragmentAsset)
    {
        // Creates program, loads and compiles vertex + fragment shaders,
        // links program, then detaches and deletes shader objects
    }
}
```

`Shader2D` extends `GLShader` with a viewport uniform for 2D rendering.

### Complete Shader List

| Shader Class | Vertex | Fragment | Purpose |
|-------------|--------|----------|---------|
| `EntireScreenTextureShader` | `EntireScreenTexture.vert.glsl` | `EntireScreenTexture.frag.glsl` | Blit texture to full screen |
| `FontShader` | `GUIs/Font.vert.glsl` | `GUIs/Font.frag.glsl` | Multi-color font rendering (instanced) |
| `GUIRectShader` | `GUIs/GUI_Rect.vert.glsl` | `GUIs/GUI_Rect.frag.glsl` | Rectangles with rounded corners, lines, textures |
| `TripleColorBackgroundShader` | `GUIs/TripleColorBackground.vert.glsl` | `GUIs/TripleColorBackground.frag.glsl` | 3-color gradient backgrounds |
| `BattleModelShader` | `Battle/BattleModel.vert.glsl` | `Battle/BattleModel.frag.glsl` | Lit 3D models with shadow mapping |
| `BattleSpriteShader` | `Battle/BattleSprite.vert.glsl` | `Battle/BattleSprite.frag.glsl` | 2D sprites in 3D battle space |
| `BlocksetBlockShader` | `World/BlocksetBlock.vert.glsl` | `World/BlocksetBlock.frag.glsl` | Blockset tile rendering |
| `MapLayoutShader` | `World/MapLayout.vert.glsl` | `World/MapLayout.frag.glsl` | Instanced map block rendering |
| `VisualObjShader` | `World/VisualObj.vert.glsl` | `World/VisualObj.frag.glsl` | Overworld character sprites |
| `DayTintShader` | (uses `EntireScreen.vert.glsl`) | `World/DayTint.frag.glsl` | Day/night color tinting |
| `FadeColorShader` | (uses `EntireScreen.vert.glsl`) | `Transitions/FadeColor.frag.glsl` | Fade to/from color transitions |
| `BattleTransitionShader_Liquid` | (uses `EntireScreen.vert.glsl`) | `Transitions/BattleTransition_Liquid.frag.glsl` | Liquid distortion battle transition |

### Initialization Order (`RenderManager.Init()`)

```csharp
internal static class RenderManager
{
    public static void Init()
    {
        // Shader singletons
        _ = new BattleTransitionShader_Liquid(gl);
        _ = new BlocksetBlockShader(gl);
        _ = new DayTintShader(gl);
        _ = new EntireScreenTextureShader(gl);
        _ = new FadeColorShader(gl);
        _ = new FontShader(gl);
        _ = new GUIRectShader(gl);

        // Mesh + other singletons
        RectMesh.Instance = new RectMesh(gl);
        _ = new TripleColorBackgroundMesh(gl);
        Blockset.Init();
        Font.Init();
    }
}
```

### Transition System

Transitions implement `ITransition`:

```csharp
internal interface ITransition
{
    bool IsDone { get; }
    void Render(FrameBuffer target);
    void Dispose();
}
```

**Available transitions:**
- `FadeFromColorTransition` -- Fade from a solid color (default: 0.5 seconds from black)
- `FadeToColorTransition` -- Fade to a solid color (default: 0.5 seconds to black)
- `BattleTransition_Liquid` -- Liquid distortion effect for entering battle (default: 2.5 seconds)

---

## 3. Font System

### The `.kermfont` Format

A custom binary font format. File structure:

```
byte   FontHeight     -- Height of every glyph in pixels
byte   BitsPerPixel   -- Bits per pixel (color index depth, e.g. 2 = 4 colors)
int32  NumGlyphs      -- Number of glyphs in the font

For each glyph:
  uint16  CharCode       -- Unicode character code (key)
  byte    CharWidth      -- Width of the glyph in pixels
  byte    CharSpace      -- Spacing after the glyph in pixels
  byte[]  PackedBitmap   -- Packed pixel data: (FontHeight * CharWidth * BitsPerPixel) bits
```

Each pixel in the packed bitmap is a **color index** (not an actual color). The index maps into the color array provided at render time. With 2 BPP, each pixel can be one of 4 values:
- **0** = Transparent (background)
- **1** = Main color (the text body)
- **2** = Outline/shadow color

### Font Atlas Generation

Glyphs are packed into a texture atlas at load time:

1. Each glyph's packed bitmap is decoded bit-by-bit
2. The color index (0-3) is written to the R channel of an `R8ui` texture
3. Glyphs are packed left-to-right, top-to-bottom with 1px spacing
4. The atlas texture uses `Nearest` filtering (no interpolation)

The atlas format is `R8ui` (unsigned integer red channel only) -- the shader reads the integer value and uses it as an index into the color uniform array.

### Font Instances

```csharp
public static Font Default { get; }      // "Fonts\\Default.kermfont", atlas: 1024x1024
public static Font DefaultSmall { get; }  // "Fonts\\DefaultSmall.kermfont", atlas: 1024x1024
public static Font PartyNumbers { get; }  // "Fonts\\PartyNumbers.kermfont", atlas: 64x64
```

Additional font files exist in the Assets folder but are not loaded by default:
- `Battle.kermfont`
- `Braille.kermfont`

### Character Overrides

Fonts support string-to-glyph overrides for special characters:

**Default / DefaultSmall fonts:**
| String | Mapped Glyph Code |
|--------|------------------|
| `\u2642` (male symbol) | `0x246D` |
| `\u2640` (female symbol) | `0x246E` |
| `[PK]` | `0x2486` |
| `[MN]` | `0x2487` |

**PartyNumbers font:**
| String | Mapped Glyph Code |
|--------|------------------|
| `[ID]` | `0x0049` |
| `[LV]` | `0x004C` |
| `[NO]` | `0x004E` |

### Special Character Handling

- `\r` -- Completely ignored
- `\n` -- Newline: `cursor.X = 0; cursor.Y += FontHeight + 1`
- `\v` -- Same as newline visually, but triggers a "vertical tab" pause in `StringPrinter`
- `\f` -- Form feed: `cursor.X = 0; cursor.Y = 0` (used for page breaks in StringPrinter)
- Unknown characters fall back to `?` glyph

### Font Rendering Pipeline

**GUIString** creates instanced render data for a string:

```csharp
internal sealed class GUIString
{
    public readonly string Text;
    public readonly Font Font;
    public Vector4[] Colors;       // Array of colors indexed by pixel value
    public readonly Vec2I Origin;
    public Vec2I Translation;
    public readonly int Scale;

    public uint VisibleStart;      // First visible instance
    public uint NumVisible;        // Number of visible instances
}
```

The `FontShader` receives:
- `u_texture` -- The font atlas (R8ui texture)
- `u_translation` -- Translation offset
- `u_numColors` -- Number of colors in the palette
- `u_colors[0..255]` -- Color palette (Vector4 RGBA values)

Each visible glyph is rendered as an instanced quad. The shader reads the R channel of the atlas to get the color index, then looks up the color from the uniform array.

### Font Style Convention

All font colors are specified as 3-element `Vector4[]` arrays: `[transparent, mainColor, outlineColor]`, which correspond to pixel values 0, 1, 2 in the font bitmap.

---

## 4. UI/GUI System

### Window System (`Window.cs`)

Windows are the primary UI container. Each window has its own FrameBuffer.

```csharp
internal sealed class Window
{
    public enum Decoration : byte
    {
        None,       // No border, inner = total size
        GrayRounded, // 5px padding all sides, gray rounded border
        Battle       // 5px top/bottom gradient lines, no side padding
    }
}
```

**Decoration dimensions:**

| Decoration | Size Addition | TopLeft Offset | Description |
|-----------|---------------|----------------|-------------|
| `None` | (0, 0) | (0, 0) | Raw inner area |
| `GrayRounded` | (10, 10) | (5, 5) | Rounded rect with 2px gray `(80, 80, 80)` border, corner radius 10 |
| `Battle` | (0, 10) | (0, 5) | Gradient lines at top and bottom |

**GrayRounded decoration:**
- Inner color filled as the background
- Border drawn as a rounded rectangle with `GUIRenderer.Rect(innerColor, Colors.V4FromRGB(80, 80, 80), rect, lineThickness: 2, cornerRadii: new(10))`

**Battle decoration (gradient lines):**
Top (from y=0 downward):
```
y=0: RGBA(0, 0, 0, 200)    -- 1px
y=1: RGBA(30, 30, 30, 200)  -- 1px
y=2: RGBA(60, 60, 60, 200)  -- 1px
y=3: inner color             -- 2px
```
Bottom (from bottom upward, mirrored):
```
y=h-5: inner color           -- 2px
y=h-3: RGBA(60, 60, 60, 200) -- 1px
y=h-2: RGBA(30, 30, 30, 200) -- 1px
y=h-1: RGBA(0, 0, 0, 200)   -- 1px
```

**Factory methods:**
```csharp
static Window CreateFromInnerSize(Vec2I pos, Vec2I innerSize, Vector4 innerColor, Decoration decoration)
static Window CreateFromTotalSize(Vec2I pos, Vec2I totalSize, Vector4 innerColor, Decoration decoration)
static Window CreateStandardMessageBox(Vector4 innerColor, Vec2I availableSize)
```

**Standard message box parameters:**
- `OFFSET_X = 4`, `OFFSET_Y = 4`
- `INNER_HEIGHT = 32`
- Decoration: `GrayRounded`
- Inner width: `availableSize.X - 8 - decoration (10)` = `availableSize.X - 18`
- Position: `(4, availableSize.Y - 4 - totalHeight)`

### GUIRenderer (`GUIRenderer.cs`)

Static class for immediate-mode 2D drawing:

```csharp
internal static class GUIRenderer
{
    // Draw a textured quad
    static void Texture(uint texture, in Rect rect, in UV uv, float opacity = 1f)

    // Draw a solid or outlined rectangle with optional corner radii
    static void Rect(in Vector4 color, in Rect rect, int lineThickness = 0, Vector4D<int> cornerRadii = default)

    // Draw a filled rectangle with an outlined border
    static void Rect(in Vector4 color, in Vector4 lineColor, in Rect rect, int lineThickness, Vector4D<int> cornerRadii = default)
}
```

All drawing goes through `GUIRectShader`, which supports:
- Position and size
- Per-corner radii (`Vector4D<int>` = topLeft, bottomLeft, topRight, bottomRight)
- Line thickness (0 = filled)
- Opacity
- Texture or solid color mode
- UV coordinates for textures

### TextGUIChoices (`TextGUIChoices.cs`)

The primary menu system. A vertical list of text choices with arrow selection indicator.

```csharp
internal sealed class TextGUIChoices : GUIChoices<TextGUIChoice>
{
    public bool BottomAligned;     // If true, choices are laid out from bottom to top
    public Font Font;
    public Vector4[] TextColors;
    public Vector4[] SelectedColors;
    public Vector4[] DisabledColors;
}
```

**TextGUIChoice structure:**
```csharp
internal sealed class TextGUIChoice : GUIChoice
{
    public GUIString ArrowStr;         // Always "right arrow" character
    public GUIString Str;              // The choice text
    public Vector4[] TextColors;
    public Vector4[] SelectedColors;
    public Vector4[] DisabledColors;
}
```

**Rendering behavior:**
- Each choice shows its text, indented by the width of "right-arrow + space"
- The selected choice displays a "right-arrow" arrow before the text
- Colors switch between `TextColors`, `SelectedColors`, and `DisabledColors` based on state
- Standard spacing between items: 3 pixels

**Standard Yes/No dialog:**
```csharp
static void CreateStandardYesNoChoices(Action<bool> clickAction, Vec2I viewSize,
    out TextGUIChoices choices, out Window window, float x = 0.75f, float y = 0.35f)
```
Uses `Font.Default`, `FontColors.DefaultDarkGray_I` for text, `FontColors.DefaultYellow_O` for selection, `Colors.White4` inner color, `GrayRounded` decoration.

### GUIChoices Base (`GUIChoices.cs`)

```csharp
internal abstract class GUIChoices<T> : IEnumerable<T>, IDisposable where T : GUIChoice
{
    public Vector2 Pos;          // Position as ratio of viewport (0.0-1.0)
    public float Spacing;        // Pixel spacing between items
    public Action BackCommand;   // Called when B is pressed
    public int Selected = 0;     // Currently selected index

    public void HandleInputs()   // Processes Up/Down/A/B
}
```

Input handling:
- **Down** -- Move selection down (if not at bottom)
- **Up** -- Move selection up (if not at top)
- **A** -- Execute selected choice's `Command` (if enabled)
- **B** -- Execute `BackCommand` (if set)

### StringPrinter (`StringPrinter.cs`)

Typewriter-style text reveal system.

```csharp
internal sealed class StringPrinter : IConnectedListObject<StringPrinter>
{
    private const float PRINT_SPEED_SLOW = 10f;   // chars/second (default)
    private const float PRINT_SPEED_FAST = 70f;   // chars/second (when A/B held)
}
```

**States:**
- `EnoughChars` -- Still revealing characters
- `FormFeed` -- Hit `\f`, waiting for A/B press to clear window and continue
- `VerticalTab` -- Hit `\v`, waiting for A/B press to continue
- `Ended` -- All text shown, waiting for A/B press to mark as done

**Behavior:**
- When A or B is held down, text speed increases from 10 to 70 chars/second
- `\f` clears the window and resets the cursor (page break)
- `\v` pauses without clearing (scroll pause)
- `IsDone` becomes true when the message has ended AND the user has pressed A/B
- `IsEnded` becomes true when all text is revealed (but user hasn't confirmed yet)
- All active StringPrinters are tracked in `AllStringPrinters` linked list

### TripleColorBackground

A full-screen background rendered with three blended colors, used for menu screens (Party, Summary, etc.):

```csharp
internal sealed class TripleColorBackground
{
    public void SetColors(in Vector3 color1, in Vector3 color2, in Vector3 color3)
    public void Render(GL gl)
}
```

---

## 5. Color System

### Base Colors (`Colors.cs`)

```csharp
internal static class Colors
{
    static Vector4 Transparent => (0, 0, 0, 0)
    static Vector3 Black3 => (0, 0, 0)
    static Vector4 Black4 => (0, 0, 0, 1)
    static Vector3 White3 => (1, 1, 1)
    static Vector4 White4 => (1, 1, 1, 1)
    static Vector4 Red4 => (1, 0, 0, 1)
    static Vector4 Green4 => (0, 1, 0, 1)
    static Vector4 Blue4 => (0, 0, 1, 1)

    static Vector3 FromRGB(uint r, uint g, uint b)         // 0-255 to 0.0-1.0
    static Vector4 V4FromRGB(uint r, uint g, uint b)       // 0-255 to 0.0-1.0, alpha=1
    static Vector4 FromRGBA(Vector3 rgb, uint a)
    static Vector4 FromRGBA(uint r, uint g, uint b, uint a)
}
```

### Font Colors (`FontColors.cs`)

Each font color is a `Vector4[]` of 3 elements: `[transparent, mainColor, outlineColor]`.

The `_I` suffix means "Inner outline" (lighter outline, good on dark backgrounds).
The `_O` suffix means "Outer outline" (darker outline, good on any background).

| Name | Transparent | Main Color (RGB) | Outline Color (RGB) |
|------|-------------|-------------------|---------------------|
| `DefaultDisabled` | (0,0,0,0) | **(133, 133, 141)** | **(58, 50, 50)** |
| `DefaultBlack_I` | (0,0,0,0) | **(15, 25, 30)** | **(170, 185, 185)** |
| `DefaultBlue_I` | (0,0,0,0) | **(0, 110, 250)** | **(120, 185, 230)** |
| `DefaultBlue_O` | (0,0,0,0) | **(115, 148, 255)** | **(0, 0, 214)** |
| `DefaultCyan_O` | (0,0,0,0) | **(50, 255, 255)** | **(0, 90, 140)** |
| `DefaultDarkGray_I` | (0,0,0,0) | **(90, 82, 82)** | **(165, 165, 173)** |
| `DefaultRed_I` | (0,0,0,0) | **(230, 30, 15)** | **(250, 170, 185)** |
| `DefaultRed_O` | (0,0,0,0) | **(255, 50, 50)** | **(110, 0, 0)** |
| `DefaultRed_Lighter_O` | (0,0,0,0) | **(255, 115, 115)** | **(198, 0, 0)** |
| `DefaultYellow_O` | (0,0,0,0) | **(255, 224, 22)** | **(188, 165, 16)** |
| `DefaultWhite_I` | (0,0,0,0) | **(239, 239, 239)** | **(132, 132, 132)** |
| `DefaultWhite_DarkerOutline_I` | (0,0,0,0) | **(250, 250, 250)** | **(80, 80, 80)** |
| `DefaultDebug` (DEBUG only) | Red4 | Green4 | Blue4 |

### Usage Patterns

- **Overworld message boxes**: `Colors.White4` inner color, `FontColors.DefaultDarkGray_I` text
- **Start menu choices**: `FontColors.DefaultDarkGray_I` text, `FontColors.DefaultYellow_O` selected
- **Battle message box**: `Colors.FromRGBA(80, 80, 80, 200)` inner color, `FontColors.DefaultWhite_I` text
- **Battle action choices**: `FontColors.DefaultWhite_I` text, `FontColors.DefaultYellow_O` selected, `FontColors.DefaultDisabled` disabled
- **Gender symbols**: Male = `FontColors.DefaultBlue_O`, Female = `FontColors.DefaultRed_O`
- **Yes/No dialog**: `FontColors.DefaultDarkGray_I` text, `FontColors.DefaultYellow_O` selected

---

## 6. Battle System

### Architecture Overview

The battle GUI is implemented across multiple partial class files:
- `BattleGUI.cs` -- Core setup, party management, callbacks
- `BattleGUI_Render.cs` -- 3D rendering, terrain, shadow mapping, HUD
- `BattleGUI_Actions.cs` -- Player action selection (Fight/Pokemon/Bag/Run menus)
- `BattleGUI_Events.cs` -- Battle event processing (damage, status, etc.)
- `BattleGUI_FaintReplacement.cs` -- Faint replacement logic
- `BattleGUI_Tasks.cs` -- Background tasks (camera motion, sprite effects, messages)

### Render Size
**480 x 270 pixels** (16:9 aspect ratio)

### Battle Backgrounds

3D models loaded from `.dae` (COLLADA) files:

| Background | BG Model | Platform Name |
|-----------|----------|---------------|
| `Cave` | `BattleBG/Cave/Cave.dae` | Cave (team-specific: CaveAlly, CaveFoe) |
| `Grass_Plain` | `BattleBG/Grass/Grass.dae` | Grass (team-specific: GrassAlly, GrassFoe) |
| `Grass_Tall` | `BattleBG/Grass/Grass.dae` | TallGrass (team-specific: TallGrassAlly, TallGrassFoe) |
| Other | `BattleBG/Dark/Dark.dae` | Dark (same for both teams) |

For rotation battles, both platforms use: `BattleBG/PlatformRotation/Rotation.dae`

### Camera System

The battle uses a 3D camera with a custom projection matrix that mimics the BW2 DS projection:

```csharp
var projection = new Matrix4x4(
    3.20947265625f,              0f,           0f,  0f,
                0f, 4.333251953125f,           0f,  0f,
                0f,              0f, -1.00390625f, -1f,
                0f,              0f, -2.00390625f,  0f);
```

**Named camera positions:**
- `DefaultCamPosition`: Position `(7, 7, 15)`, Rotation `(-22, 13, 0)` -- Standard overview
- `_camPosThrowBall`: Position `(16.9, 7.5, 30.6)`, Rotation `(-32.4, 8.8, 0)` -- Ball throw view
- `_camPosViewAll`: Position `(9, 8, 22)`, Rotation `(-22, 13, 0)` -- All pokemon view (double/triple)

**Camera motion speeds:**
- `CAM_SPEED_FAST = 0.25f` seconds
- `CAM_SPEED_DEFAULT = 0.5f` seconds

Camera motion uses `PositionRotationAnimator` with linear or smooth interpolation (Slerp for rotation, Lerp for position).

### Battle Intro Sequences

**Wild battle intro:**
1. Camera starts at `(8.4, 7, 2.3)` with rotation `(-32, 13.3, 0)`
2. Foe pokemon sprites start darkened (blackness = 0.75)
3. Camera animates to `(6.85, 7, 4.55)` with rotation `(-22, 13, 0)`
4. Fade from white (1 second)
5. Pokemon cries play
6. Blackness fades from 0.75 to 0 over 1 second
7. Camera moves to default position

**Trainer battle intro:**
1. Camera starts at `(10, 5, -25)` with rotation `(-70, 15, 0)`
2. Trainer sprite starts fully black (blackness = 1.0)
3. Camera smoothly animates to default position (2 seconds)
4. After 0.75s delay, trainer silhouette reveals over 1 second
5. Trainer animation plays
6. Challenge message shown
7. Trainer fades away (moves -7.5 on Z axis, opacity fades to 0)

### 3D Rendering Pipeline

```
1. Clear shadow buffer
2. Render battle sprites to shadow buffer (sprite shader, shadow output mode)
3. Clear main framebuffer
4. Render 3D models with shadow mapping (model shader):
   - Shadow color texture on unit 0
   - Shadow depth texture on unit 1
   - Diffuse textures on units 2+
5. Render battle sprites on top (sprite shader, normal mode)
6. Disable depth test
7. Apply day tint post-process
8. Render HUD (info bars, message window) on top
```

### Lighting

4 animated point lights with attenuation:

```csharp
private readonly PointLight[] _testLights = new PointLight[4]
{
    new(new Vector3(-5, 3, -5), new Vector3(5.00f, 2.25f, 0.60f), new Vector3(1f, 0.01f, 0.002f)),
    new(new Vector3( 5, 3, -5), new Vector3(0.85f, 0.52f, 4.00f), new Vector3(1f, 0.01f, 0.002f)),
    new(new Vector3(-5, 3,  5), new Vector3(0.85f, 3.52f, 0.88f), new Vector3(1f, 0.01f, 0.002f)),
    new(new Vector3( 5, 3,  5), new Vector3(0.85f, 0.52f, 0.88f), new Vector3(1f, 0.01f, 0.002f)),
};
```

Lights 0 and 1 animate in a circular pattern over a 5-second cycle.

### Shadow Mapping

- Shadow texture: 512x512
- Orthographic projection: 30x30 units, 0-30 depth range
- Fake light position: `(7, 10, 20)`
- Center of world: `(0, 0, -5)`
- Shadow conversion matrix includes a bias matrix (`scale(0.5) * translate(0.5, 0.5, 0.5)`) for sampling

### Battle Message Window

```csharp
_stringWindow = Window.CreateFromInnerSize(
    new Vec2I(0, RenderSize.Y - 52),  // Position: bottom of screen, 52px from bottom
    new Vec2I(RenderSize.X, 32),       // Inner size: full width, 32px tall
    Colors.FromRGBA(80, 80, 80, 200),  // Semi-transparent dark gray
    Window.Decoration.Battle           // Gradient line decoration
);
```

Battle messages use `FontColors.DefaultWhite_I` with position offset `(16, 0)`.

### Battle Action Menus

Action choices rendered at position `(0.75, 0.75)` (bottom-right, bottom-aligned):
- **Fight** -- Always enabled, shows move list
- **Pokemon** -- Disabled if TempLockedMove exists
- **Bag** -- Disabled if TempLockedMove exists
- **Run** -- Only on first pokemon, only in wild battles, only if flee is valid
- **Back** -- Only on subsequent pokemon (replaces Run)

Move choices also at `(0.75, 0.75)`, bottom-aligned, with back command returning to action choices.

### Platform Positions

| Format | Platform Scale | Foe Position | Ally Position |
|--------|---------------|--------------|---------------|
| Single | 1.0 | (0, -0.20, -15) | (0, -0.20, 3) |
| Double | 1.2 | (0, -0.25, -15) | (0, -0.25, 3) |
| Triple | 1.5 | (0, -0.30, -15) | (0, -0.30, 3) |
| Rotation | 1.0 | (0, -0.20, -17.25) | (0, -0.20, 8) |

---

## 7. World/Overworld

### World Constants

```csharp
// Tile: 8x8 pixels
const int Tile_NumPixelsX = 8;
const int Tile_NumPixelsY = 8;

// Block: 2x2 tiles = 16x16 pixels
const int Block_NumTilesX = 2;
const int Block_NumTilesY = 2;
const int Block_NumPixelsX = 16;
const int Block_NumPixelsY = 16;

const int NumElevations = 8;
const int MaxSubLayers = 256;

const ushort PlayerId = 65535;
const ushort CameraId = 65534;
```

### OverworldGUI (`OverworldGUI.cs`)

Render size: **320 x 180 pixels** (16:9)

The comment in the source documents the screen size rationale:

```
| Console   | Resolution       | Num Blocks |
|-----------|------------------|------------|
| GB && GBC | 160 x 144 (10:9) | 10 x  9    |
| GBA       | 240 x 160 ( 3:2) | 15 x 10    |
| NDS       | 256 x 192 ( 4:3) | 16 x 12    |
| 3DS Lower | 320 x 240 ( 4:3) | 20 x 15    |
| 3DS Upper | 400 x 240 ( 5:3) | 25 x 15    |
|-----------|------------------|------------|
| This Game | 320 x 180 (16:9) | 20 x 11.25 |
```

### Map Rendering (`MapRenderer.cs`)

The MapRenderer uses **instanced rendering** for blocks:
- One framebuffer per elevation layer (8 elevation layers)
- One obj framebuffer per elevation layer
- Instanced block data per elevation

**Rendering pipeline:**
1. Update tile and blockset animations
2. Clear all elevation framebuffers
3. Calculate camera rect (visible blocks based on camera obj position)
4. Determine visible maps (current map + connected maps recursively)
5. Add layout blocks for all visible maps
6. Add border blocks for out-of-bounds areas
7. Render blocks per elevation layer (instanced, blend disabled for speed)
8. Render objects per elevation layer (shadows first, then sprites sorted by Y)
9. Composite all layers onto target framebuffer

**Camera centering:**
The camera follows `CamAttachedTo` (an `Obj`). The viewport centers on that obj's position, converting block coordinates to pixel coordinates.

### Overworld Render Pipeline

```
OverworldGUI.Render():
  1. Clear framebuffer to black
  2. _mapRenderer.Render(_frameBuffer)   -- Map tiles + objects
  3. DayTint.Render(...)                 -- Day/night tint post-process
  4. Window.RenderAll()                  -- Any open windows (messages, menus)
```

### Day/Night Tint System (`DayTint.cs`)

A post-processing color multiplier based on time of day. When enabled (via `MapFlags.DayTint`), the entire overworld/battle scene is tinted.

**Hourly color values (RGB multipliers, 0.0-1.0):**

| Hour | R | G | B |
|------|-------|-------|-------|
| 00 | 0.160 | 0.180 | 0.330 |
| 01 | 0.160 | 0.180 | 0.330 |
| 02 | 0.160 | 0.180 | 0.330 |
| 03 | 0.170 | 0.185 | 0.345 |
| 04 | 0.225 | 0.235 | 0.375 |
| 05 | 0.350 | 0.265 | 0.415 |
| 06 | 0.500 | 0.400 | 0.500 |
| 07 | 0.720 | 0.660 | 0.555 |
| 08 | 0.900 | 0.785 | 0.815 |
| 09 | 0.950 | 0.980 | 0.905 |
| 10 | 1.000 | 0.985 | 0.945 |
| 11 | 1.000 | 1.000 | 0.950 |
| 12 | 1.000 | 1.000 | 1.000 |
| 13 | 1.000 | 1.000 | 0.985 |
| 14 | 1.000 | 1.000 | 0.955 |
| 15 | 0.995 | 1.000 | 0.950 |
| 16 | 0.955 | 0.975 | 0.850 |
| 17 | 0.845 | 0.885 | 0.740 |
| 18 | 0.700 | 0.690 | 0.560 |
| 19 | 0.545 | 0.460 | 0.390 |
| 20 | 0.490 | 0.320 | 0.380 |
| 21 | 0.250 | 0.235 | 0.370 |
| 22 | 0.180 | 0.205 | 0.350 |
| 23 | 0.160 | 0.180 | 0.330 |

Colors are linearly interpolated between hours based on the current minute. Transition speed: 0.05 seconds per minute when catching up.

### Start Menu (`OverworldGUI_StartMenu.cs`)

Position: `(192, 8)` -- Top-right corner of the 320x180 screen.

**Choices:**
- "Pokemon" -- Opens Party GUI (`PartyGUI.Mode.PkmnMenu`)
- "Bag" -- Opens Bag GUI
- "Close" -- Closes menu
- "Debug Menu" (DEBUG only) -- Opens debug submenu

**Debug submenu:**
- "PC" -- Opens PC Boxes GUI
- "MapRenderer Menu" (DEBUG_OVERWORLD only) -- Toggle debug overlays
- "Back" -- Returns to main start menu
- "Quit" -- Exits game

Window style: `Colors.White4` inner, `GrayRounded` decoration.

### Map System

- `Map` -- A single map with a layout, connections, events, and details
- `MapLayout` -- Block grid with border blocks
- `MapDetails` -- Music, section, weather, flags
- `Map.Connection` -- Links maps together at edges
- `MapEvents` -- Warps, signs, NPCs

**MapFlags:**
```csharp
[Flags]
public enum MapFlags : byte
{
    None = 0,
    DayTint = 1 << 0,
    Bike = 1 << 1,
    Fly = 1 << 2,
    Teleport = 1 << 3,
    Dig_EscapeRope = 1 << 4,
    ShowMapName = 1 << 5
}
```

### Object System

- `Obj` -- Base class for all world objects (position, movement)
- `VisualObj` -- Obj with a sprite texture
- `EventObj` -- NPC with scripts, trainer data, movement patterns
- `PlayerObj` -- The player character
- `CameraObj` -- A virtual camera attachment point

**Movement types (`ObjMovementType`):**
`None`, `Face_South`, `Face_Southwest`, `Face_Southeast`, `Face_North`, `Face_Northwest`, `Face_Northeast`, `Face_West`, `Face_East`, `Face_Randomly`, `Sleep`, `Wander_Randomly`, `Wander_SouthAndNorth`, `Wander_WestAndEast`, `Walk_WestThenReturn`, `Walk_EastThenReturn`

**Facing directions:**
`South`, `North`, `West`, `East`, `Southwest`, `Southeast`, `Northwest`, `Northeast`

---

## 8. Script System

### Script Architecture

Scripts are compiled from text to a binary format (`Scripts.bin`). The `ScriptLoader` reads a header table mapping label names to byte offsets, then creates a `ScriptContext` with a binary reader positioned at the correct offset.

```csharp
internal static class ScriptLoader
{
    private const string FILE = @"Script\Scripts.bin";
    private static readonly Dictionary<string, uint> _globalScriptOffsets;

    public static ScriptContext LoadScript(string label, Vec2I viewSize)
}
```

### ScriptContext (VM)

The script VM is a simple bytecode interpreter:

```csharp
internal sealed partial class ScriptContext
{
    private readonly Vec2I _viewSize;
    private readonly EndianBinaryReader _reader;
    private readonly Stack<long> _callStack = new();
    private ushort _msgScale = 1;
    public bool IsDead;
}
```

**Wait states (checked each frame):**
- Delay timer
- Object movement completion
- Message box read/completion
- Multichoice selection
- Return to field
- Cry playback completion

The VM runs commands until it hits a wait state, then returns control to the caller. On the next frame, it checks wait states again before continuing.

### Script Commands (Complete List)

```
End, GoTo, Call, Return,
HealParty, GivePokemon, GivePokemonForm, GivePokemonFormItem,
MoveObj, AwaitObjMovement, CreateCameraObj, AttachCamera,
Delay, SetFlag, ClearFlag, Warp,
Message, MessageScale, AwaitMessageRead, AwaitMessageComplete,
LockObj, UnlockObj, LockAllObjs, UnlockAllObjs,
SetVar, AddVar, SubVar, MulVar, DivVar,
RshftVar, LshiftVar, AndVar, OrVar, XorVar, RandomizeVar,
GoToIf, GoToIfFlag, CallIf, CallIfFlag,
BufferSpeciesName, BufferPartyMonNickname,
WildBattle, TrainerBattle, TrainerBattle_Continue,
AwaitReturnToField, CloseMessage,
UnloadObj, LookTowardsObj, LookLastTalkedTowardsPlayer,
BufferSeenCount, BufferCaughtCount,
GetDaycareState, BufferDaycareMonNickname, StorePokemonInDaycare,
GetDaycareCompatibility, SelectDaycareMon, GetDaycareMonLevelsGained,
GiveDaycareEgg, DisposeDaycareEgg, HatchEgg,
YesNoChoice, IncrementGameStat,
PlayCry, AwaitCry,
CountNonEggParty, CountNonFaintedNonEggParty, CountPlayerParty,
CountBadges, BufferBadges,
CheckPartyHasMove, UseSurf
```

### Script Conditionals

```csharp
public enum ScriptConditional : byte
{
    Equal, GreaterEqual, LessEqual, NotEqual, Less, Greater
}
```

### Script Movements

```csharp
public enum ScriptMovement : byte
{
    End,
    Face_S, Face_N, Face_W, Face_E, Face_SW, Face_SE, Face_NW, Face_NE,
    Walk_S, Walk_N, Walk_W, Walk_E, Walk_SW, Walk_SE, Walk_NW, Walk_NE,
    Run_S, Run_N, Run_W, Run_E, Run_SW, Run_SE, Run_NW, Run_NE
}
```

### Message System

Script messages use the overworld's standard message box:

```csharp
private void CreateMessageBox(string text)
{
    _messageBox = Window.CreateStandardMessageBox(Colors.White4, _viewSize);
    _stringPrinter = new StringPrinter(_messageBox, text, Font.Default,
        FontColors.DefaultDarkGray_I, new Vec2I(8, 0), scale: _msgScale);
}
```

The text offset starts at `(8, 0)` inside the message window's inner area.

String buffers (like `{0}`, `{1}`) are applied before rendering via `Game.Instance.StringBuffers.ApplyBuffers(str)`.

---

## 9. Input System

### Key Enum

```csharp
internal enum Key : byte
{
    Down, Left, Right, Up,
    A, B, X, Y,
    L, R,
    Start, Select,
    Screenshot
}
```

### Keyboard Bindings (Default)

| SDL KeyCode | Game Key |
|------------|----------|
| `Q` | L |
| `W` | R |
| `Left Arrow` | Left |
| `Right Arrow` | Right |
| `Up Arrow` | Up |
| `Down Arrow` | Down |
| `Return/Enter` | Start |
| `Right Shift` | Select |
| `A` | X |
| `S` | Y |
| `Z` | B |
| `X` | A |
| `F12` | Screenshot |

### Controller Bindings (Default -- Nintendo layout)

| SDL Controller Button | Game Key |
|----------------------|----------|
| Left Shoulder | L |
| Right Shoulder | R |
| DPad Left | Left |
| DPad Right | Right |
| DPad Up | Up |
| DPad Down | Down |
| Start | Start |
| Back | Select |
| Y (XBOX) | X (Nintendo) |
| X (XBOX) | Y (Nintendo) |
| A (XBOX) | B (Nintendo) |
| B (XBOX) | A (Nintendo) |

Note: XBOX buttons are mapped to Nintendo layout (swapped A/B and X/Y).

### InputManager

The input manager supports keyboard, gamepad, and mouse. It provides:

```csharp
internal static class InputManager
{
    public static bool CursorMode;  // Mouse cursor visibility

    public static bool IsDown(Key k)        // Currently held
    public static bool JustPressed(Key k)   // Pressed this frame
    public static bool JustReleased(Key k)  // Released this frame

    // Mouse hit testing with corner radius support
    public static bool IsHovering(in Rect buttonInVirtualScreen, Vector4D<int> cornerRadii)
    public static bool JustPressed(in Rect buttonInVirtualScreen, Vector4D<int> cornerRadii)
}
```

**Mouse hit testing** transforms window coordinates to virtual screen coordinates, accounting for aspect ratio scaling, and supports rounded corner hit detection using distance-from-center math.

**Cursor mode** auto-toggles: keyboard/controller input hides the cursor, mouse movement shows it.

### Left Stick Support

The controller left analog stick simulates DPad inputs with deadzone handling via `AxisData`.

---

## 10. Asset Pipeline

### Asset Loading (`AssetLoader.cs`)

```csharp
internal static class AssetLoader
{
    // Debug: Assets loaded from relative path (project source)
    public const string DEFAULT_ASSET_PATH = @"..\..\..\Assets";
    // Release: Assets copied to output directory
    public const string DEFAULT_ASSET_PATH = @"Assets";

    // Pokemon sprites from PokemonBattleEngine shared assets
    public const string PKMN_SPRITE_ASSET_PATH = @"..\..\..\..\..\PokemonBattleEngine\Shared Assets\PKMN";

    public static string GetPath(string asset, string basePath = DEFAULT_ASSET_PATH)
    {
        // Combines base path with asset path, throws if file doesn't exist
    }
}
```

### Asset Types

| Asset Type | Format | Location |
|-----------|--------|----------|
| Fonts | `.kermfont` (custom binary) | `Assets/Fonts/` |
| Shaders | `.glsl` (GLSL source) | `Assets/Shaders/` |
| 3D Models | `.dae` (COLLADA) | `Assets/BattleBG/` |
| Textures | PNG (loaded via ImageSharp) | Various |
| Scripts | `Scripts.bin` (compiled binary) | `Assets/Script/` |
| Sound | `.wav` (PCM8, PCM16, IEEE32) | `Assets/Sound/` |
| Pokemon Data | Binary + JSON source | `Assets/Pokedata/` |
| Map Data | Binary + JSON source | `Assets/Map/`, `Assets/Layout/` |
| Encounters | Binary + JSON source | `Assets/Encounter/` |
| Tilesets | Binary | `Assets/Tileset/`, `Assets/Blockset/` |

### Release Build Asset Handling

In release builds, assets are copied to the output directory with `PreserveNewest`. Source JSON files are excluded from the release build:
- `Encounter/**/*.json`
- `Map/**/*.json`
- `ObjSprites/ObjSprites.json`
- `Pokedata/**/*.json`
- `Script/**/*.txt`
- `Tileset/Animation/**/*.json`

---

## 11. 3D Models

### AssimpLoader (`AssimpLoader.cs`)

Uses Silk.NET.Assimp to import 3D models:

```csharp
internal unsafe static class AssimpLoader
{
    public static List<Mesh> ImportModel(string asset)
    {
        // Import with flags: Triangulate | GenerateSmoothNormals | FlipUVs
        // Recursively processes all nodes
        // Loads diffuse textures from material
    }
}
```

**Post-processing flags:**
- `Triangulate` -- Convert all faces to triangles
- `GenerateSmoothNormals` -- Generate smooth normals
- `FlipUVs` -- Flip texture coordinates for OpenGL

### Mesh (`Mesh.cs`)

Each mesh stores:
- VBO with `VBOData_BattleModel` vertices (Position, Normal, UV)
- EBO with unsigned int indices
- List of diffuse texture handles

Rendering binds diffuse textures starting at texture unit 2 (units 0-1 reserved for shadow maps).

### Model (`Model.cs`)

A model is a collection of meshes with a transform:

```csharp
internal sealed class Model
{
    private readonly List<Mesh> _meshes;
    public Vector3 Scale;
    public PositionRotation PR;

    public Matrix4x4 GetTransformation()
    {
        return Matrix4x4.CreateScale(Scale)
            * Matrix4x4.CreateFromQuaternion(PR.Rotation.Value)
            * Matrix4x4.CreateTranslation(PR.Position);
    }
}
```

### Camera (`Camera.cs`)

```csharp
internal sealed class Camera
{
    public readonly Matrix4x4 Projection;
    public PositionRotation PR;

    public Matrix4x4 CreateViewMatrix()
    {
        // View = Translate(-pos) * Rotate(Conjugate(rot))
        return Matrix4x4.CreateTranslation(Vector3.Negate(pos))
            * Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(rot));
    }
}
```

### PositionRotation

```csharp
internal struct PositionRotation
{
    public Vector3 Position;
    public Rotation Rotation;  // Internally stores Quaternion, constructed from Yaw/Pitch/Roll degrees

    public static PositionRotation Slerp(in PositionRotation from, in PositionRotation to, float progress)
}
```

### PointLight

```csharp
internal sealed class PointLight
{
    public Vector3 Pos;
    public Vector3 Color;
    public Vector3 Attenuation;  // (constant, linear, quadratic)
}
```

### BattleModelShader Uniforms

The 3D model shader supports:
- Projection, view, and transform matrices
- Shadow texture conversion matrix
- Camera position (for specular)
- Shine damper (default: 5.0) and specular reflectivity (default: 0.0)
- Up to 4 point lights (position, color, attenuation)
- Shadow color texture (unit 0) and shadow depth texture (unit 1)
- Up to N diffuse textures (units 2+)

---

## 12. Audio

### Sound Mixer (`SoundMixer.cs`)

Custom software audio mixer running at **48,000 Hz**, stereo, F32 format.

```csharp
internal static unsafe class SoundMixer
{
    private const int SAMPLE_RATE = 48_000;
    // Buffer: 4096 samples per channel
}
```

**Initialization:**
- Opens SDL audio device with F32 format, 2 channels, 4096 sample buffer
- Audio callback `MixAudio` is called by SDL to fill the buffer
- Starts playing immediately

**Mixing process (in audio callback):**
1. Calculate audio delta time
2. Run sound tasks (music fading, etc.)
3. Clear mixing buffer
4. For each active channel:
   - If fading: mix to temp buffer, apply fade, add to main buffer
   - If not fading: mix directly to main buffer
5. Marshal.Copy buffer to SDL stream

### Wave File Data (`WaveFileData.cs`)

Adapted from NAudio. Supports:
- **PCM 8-bit unsigned** (U8)
- **PCM 16-bit signed** (S16)
- **IEEE 32-bit float** (F32)
- Mono and stereo
- Loop points via the `smpl` chunk (standard WAV loop markers from FL Studio / Edison)
- RF64 format support

**Caching:** `WaveFileData` uses reference counting with a static `Dictionary<string, WaveFileData>` cache. Each `Get()` call increments the reference count; `DeductReference()` decrements it and disposes when it reaches 0.

### SoundChannel (`SoundChannel.cs`)

Individual sound playback channel with:
- Volume (0.0 to unbounded -- F32 audio legally exceeds 1.0)
- Panpot (-1.0 left, 0.0 center, +1.0 right)
- Priority (higher = mixed first)
- Pitch shifting via frequency adjustment: `freq = SampleRate * pow(2, pitch / 768f)`
- Fade support (linear fade between two volume levels over time)

**Panning formula:**
```
leftVol = Volume * (1 - (Panpot / 2 + 0.5))
rightVol = Volume * (Panpot / 2 + 0.5)
```

**Mixing implementations:** 12 total variants covering all combinations of:
- Format: U8, S16, F32
- Channels: Mono, Stereo
- Looping: Loop, NoLoop

Looping uses a "trail" system where audio from the end of the loop cross-fades with the start to avoid clicks.

### Music Player (`MusicPlayer.cs`)

Singleton `MusicPlayer.Main` manages background music with fade transitions:

```csharp
internal sealed class MusicPlayer
{
    private const float STANDARD_FADE_OUT_LENGTH = 1f;

    public Song Music;

    // Key methods:
    void FadeToNewMusic(Song newMusic)
    void BeginNewMusicAndBackupCurrentMusic(Song newMusic)  // For battle
    void FadeToBackupMusic()                                 // Return from battle
    void QueueMusicIfDifferentThenFadeOutCurrentMusic(Song newMusic)  // For warps
    void FadeToQueuedMusic()
}
```

**Battle music flow:**
1. `BeginNewMusicAndBackupCurrentMusic(battleSong)` -- Saves current music state (including playback position), immediately starts battle music
2. After battle: `FadeToBackupMusic()` -- Fades out battle music, restores previous music from saved state with fade-in

### Songs

```csharp
public enum Song : ushort
{
    None,
    Town1, Route1, Cave1,
    BattleWild, BattleWild_Multi, BattleTrainer,
    BattleGymLeader, BattleEvil1, BattleLegendary
}
```

**Song file mappings:**
| Song | File |
|------|------|
| Town1, Route1 | `Sound/BGM/Town1.wav` |
| Cave1 | `Sound/BGM/Cave1.wav` |
| BattleWild, BattleWild_Multi, BattleTrainer | `Sound/BGM/BattleTrainer.wav` |
| BattleLegendary, BattleGymLeader | `Sound/BGM/BattleGymLeader.wav` |
| BattleEvil1 | `Sound/BGM/BattleEvil1.wav` |

### Pokemon Cries (`SoundControl.cs`)

```csharp
public static SoundChannel PlayCry(PBESpecies species, PBEForm form,
    PBEStatus1 status, float hpPercentage, float vol = 0.5f, float pan = 0f)
```

- Default volume: 0.5
- Pitch distortion based on HP: `pitch = (1 - hpPercentage) * -96` (so fainted = -96, about -0.125 semitones)
- Status condition forces `hpPercentage` to 0.5 max for distortion
- Battle cry panning: ally team = -0.35 (left), foe team = +0.35 (right)
- Special form-specific cries: Shaymin_Sky, Tornadus_Therian, Thundurus_Therian, Landorus_Therian, Kyurem_White, Kyurem_Black

---

## Appendix: Debug Compile Flags

| Flag | Effect |
|------|--------|
| `FULLSCREEN` | Starts in fullscreen desktop mode |
| `DEBUG` | Enables debug output, GL error callback, debug logging |
| `DEBUG_DAYCARE_LOGEGG` | Logs daycare egg generation |
| `DEBUG_FRIENDSHIP` | Logs friendship changes |
| `DEBUG_DISABLE_DAYTINT` | Disables day/night tinting |
| `DEBUG_OVERWORLD` | Enables overworld debug overlay (block grid, statuses, texts) |
| `DEBUG_CALLBACKS` | Logs callback changes with caller info |
| `DEBUG_BATTLE_CAMERAPOS` | Enables manual camera control in battles |
| `DEBUG_DATA_CACHE` | Logs data cache operations |
| `DEBUG_BATTLE_WIREFRAME` | Renders battle 3D models as wireframe |
| `DEBUG_ANIMIMG_HITBOX` | Shows animated image hit boxes |
| `DEBUG_AUDIO_LOG` | Draws audio level bars in console |

---

## Appendix: Block Behaviors

Complete list of `BlocksetBlockBehavior` values:

```
None, AllowElevationChange,
Sign_AutoStartScript, Warp_WalkSouthOnExit, Warp_Teleport,
Grass_Encounter, Surf, Waterfall,
Ledge_S/N/W/E/SW/SE/NW/NE,
Blocked_S/N/W/E/SW/SE/NW/NE,
Spin_S/N/W/E/SW/SE/NW/NE,
Grass_SpecialEncounter, Tree_Headbutt, Tree_Honey,
Stair_W, Stair_E,
Warp_NoOccupancy_S,
Cave_Encounter, AllowElevationChange_Cave_Encounter,
Bridge, Bridge_Cave_Encounter
```

---

## Appendix: Encounter Types

```
Default, Surf, SuperRod, DarkGrass,
RareDefault (Rustling Grass / Dust Clouds),
RareSurf (Rippling Water),
RareSuperRod (Rippling Water),
HeadbuttTree, HoneyTree
```

---

## Appendix: Battle Background Mapping

| Block Behavior | PBE Terrain | Battle Background |
|---------------|-------------|-------------------|
| `Cave_Encounter` | Cave | Cave |
| `AllowElevationChange_Cave_Encounter` | Cave | Cave |
| `Grass_Encounter` | Grass | Grass_Tall |
| `Grass_SpecialEncounter` | Grass | Grass_Tall |
| `Surf` | Water | Water |
| Other | Plain | Unspecified |
