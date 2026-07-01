# WAT NOU?

A small educational puzzle game about keeping a smart power grid running.
Connect sources to houses and place smart-grid tools (sensors, switches,
batteries, demand response, a predictor, V2G) to solve each level's problem.
In-game text is Dutch; the code itself is English.

## Run it

Requires the **.NET 8 SDK**. MonoGame and FontStashSharp are pulled from
NuGet automatically.

```
dotnet run
```

Jump straight to a specific level while testing:

```
dotnet run -- --level 3
```

### Controls

- **Left click** a tile: place the selected tool.
- **Right click** a tile: remove what's placed there.
- **1-9**: pick a tool from the current level's toolbox.
- **R**: reset the current level.
- **&larr; / &rarr;**: switch levels (handy for testing; the real level
  progression comes later).
- **Esc**: quit.

## Status

This is mid-build, not the finished game:

- Simulation (`Source/Grid.cs`) and level data (`Source/LevelData.cs`,
  `Content/levels.txt`) are done.
- Tiles are drawn with plain rectangles and icon textures - no wires, no
  flow animation, no menu screens (start/won/message/end) yet. The grid is
  just always live and reacts to `Grid.IsSolved()`.
- No art yet either: every icon in `Content/Textures/` is a generated
  placeholder swatch until real PNGs are dropped in (see
  `Content/Textures/README.md`).

## Font

DejaVu Sans (free license, based on Bitstream Vera), loaded directly via
FontStashSharp - no content pipeline needed.
