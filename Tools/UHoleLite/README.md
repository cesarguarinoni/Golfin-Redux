# UHole Lite

Map-based golf hole pipeline — turns official course hole diagrams into walkable Unity terrain.

## What it does

Takes the official hole illustration GIFs from a golf course website and:

1. **Scrapes** all 18 hole maps + yardage data
2. **Extracts** the illustration area (crops info panel, removes legend)
3. **Classifies** surface zones via color segmentation (fairway, rough, green, bunker, water, trees)
4. **Detects** tee marker positions (colored dots)
5. **Generates** procedural elevation (plausible terrain from zone data + hole descriptions)
6. **Exports** a package compatible with the existing Unity HoleImporter

## vs UHole

| | UHole | UHole Lite |
|---|---|---|
| Texture | Satellite tiles (GSI) | Official hole map GIF |
| Elevation | Real DEM data | Procedural |
| Alignment | Manual control points | Automatic |
| Setup time | ~30 min/hole | ~2 min/hole |
| Zone data | None | Auto-classified |

## Quick Start

```bash
npm install

# Download course data
node scripts/scrape-course.mjs lomond-country-club

# Process all 18 holes
node scripts/run-all.mjs lomond-country-club --all

# Or just one hole
node scripts/run-all.mjs lomond-country-club 1
```

## First Course

Lomond Country Club (ローモンドカントリー倶楽部), Kameyama, Mie, Japan.
18 holes, par 72, 7,028 yards from back tees.

## Docs

- `docs/SPEC.md` — Full pipeline specification
- `docs/TASK.md` — Claude Code handoff instructions
