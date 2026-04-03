# Funity

Funity is a dependency-free Node app that ingests Figma JSON and emits Unity reconstruction artifacts:

- A Unity C# editor/import script
- A YAML scene description
- A Markdown spec for implementation parity
- A local browser interface for generation and download

## What It Does

The first version focuses on turning a Figma frame into a Unity-friendly scene model with:

- Hierarchy and naming
- Absolute layout and sizes
- Anchor and pivot suggestions
- Fill and stroke colors
- Typography properties
- Image placeholders

## Input

Funity can now work from:

- A Figma URL with `node-id`
- A Figma file key and optional node ID
- Raw Figma JSON pasted into the UI
- A local JSON export from disk

For direct API import, provide a Figma personal access token in the UI or set `FIGMA_TOKEN` before starting the server.
When asset downloading is enabled, Funity can fetch both image-fill assets and rendered layer exports from Figma and emits an `asset-manifest.json` plus binary asset files.

## Browser Interface

Start the local UI:

```powershell
node ./src/server.mjs
```

Then open [http://127.0.0.1:4173](http://127.0.0.1:4173).

If you want a double-click Windows launcher, use [start-funity.bat](C:/Users/cesar/Funity/start-funity.bat).

## CLI Quick Start

1. Put a Figma API response or node export JSON in `./examples/figma-sample.json`, or point to your own file.
2. Run:

```powershell
node ./src/cli.mjs --input ./examples/figma-sample.json --output ./dist/sample --screen "Sample Screen"
```

3. Generated files:

- `unity-import.cs`
- `scene.yaml`
- `screen-spec.md`
- `asset-manifest.json`

For direct Figma import from the CLI:

```powershell
$env:FIGMA_TOKEN="your-token"
node ./src/cli.mjs --figmaUrl "https://www.figma.com/design/FILE_KEY/Screen?node-id=12-34" --output ./dist/from-api
```

Or:

```powershell
node ./src/cli.mjs --fileKey "FILE_KEY" --nodeId "12:34" --token "your-token" --output ./dist/from-api
```

By default, direct Figma CLI imports also try to download image-fill assets into `./assets`. To disable that:

```powershell
node ./src/cli.mjs --figmaUrl "https://www.figma.com/design/FILE_KEY/Screen?node-id=12-34" --includeAssets false --output ./dist/from-api
```

Rendered layer exports are also enabled by default. To disable those separately:

```powershell
node ./src/cli.mjs --figmaUrl "https://www.figma.com/design/FILE_KEY/Screen?node-id=12-34" --includeRenderedAssets false --output ./dist/from-api
```

## Browser Flow

1. Paste a Figma URL, or enter a file key and optional node ID.
2. Provide a Figma token in the UI, or set `FIGMA_TOKEN` in your environment.
3. Click `Import From Figma API` to inspect the fetched JSON, or click `Generate Unity Artifacts` directly.
4. Review the generated C#, YAML, spec, and asset manifest in the UI.
5. Download individual files or use `Download All Output`.
6. In the browser UI, image files download to your browser's default Downloads folder. In CLI mode, they are written to `output/assets/`.

## Unity Compare Export

To generate a Unity hierarchy dump for Compare mode:

1. Copy [FunityCompareExporter.cs](C:/Users/cesar/Funity/docs/unity/FunityCompareExporter.cs) into an `Editor/` folder in your Unity project.
2. In Unity, select the root GameObject for the UI screen you want to compare.
3. Run `Funity > Export Selected UI For Compare`.
4. Save the generated `.txt` file.
5. Paste that output into the `Unity hierarchy / prefab / scene dump` field in Compare mode.

The exporter includes:

- object names
- RectTransform values
- TextMeshPro text, font, size, color, and alignment
- Image and RawImage color/asset info
- common layout components

## Recommended Product Architecture

For a production-grade pipeline, structure the app in four stages:

1. Ingestion
   Read Figma from API, URL/file, or raw JSON.
2. Normalization
   Convert Figma nodes into a renderer-agnostic scene graph.
3. Unity targeting
   Map the scene graph into RectTransform, TextMeshPro, sprites, and panels.
4. Validation
   Compare rendered output against Figma screenshots for pixel-diff QA.

## Unity Output Strategy

The generated C# file is an editor utility scaffold intended to be pasted into or adapted for a Unity project. It currently:

- Rebuilds hierarchy
- Creates `RectTransform`-style layout calls
- Applies colors and typography comments
- Flags unsupported properties clearly

The generated YAML and spec document act as deterministic debug artifacts and fallback implementation docs.

## Limitations

- No auto-layout-to-layout-group conversion yet
- No effects, gradients, or blend modes beyond documentation
- No direct Unity project writing from this repository

## Next Recommended Steps

- Add a Figma API client using personal access token auth
- Export assets and map fills to Unity sprites
- Add Auto Layout to Horizontal/Vertical/LayoutElement translation
- Add TextMeshPro font mapping rules
- Add screenshot diff validation
