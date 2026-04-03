# Funity Memory

## Current State

- Local browser UI exists and runs from `http://127.0.0.1:4173`.
- Direct Figma API import works with personal access token input or `FIGMA_TOKEN`.
- Browser stores the token and form inputs in local storage.
- Unity artifacts generate successfully:
  - `unity-import.cs`
  - `scene.yaml`
  - `screen-spec.md`
  - `asset-manifest.json`
  - `debug-export.json`
- `Download All Output` exists in the UI.
- App theme was switched to dark mode.

## Asset Export Status

- Image-fill asset downloading is implemented.
- Rendered node export by node ID is also implemented using Figma's image render endpoint.
- In the user's real Figma screen (`Bags Screen - Menu`), both counters still showed:
  - `Image fill assets downloaded: 0`
  - `Rendered layer assets exported: 0`

## Known Issues / Investigation Thread

- The user said the Figma design uses layers but no groups.
- The user is pasting a share link from the main layer/frame and is unsure whether that is correct.
- A share link from the target frame/component should be valid if it includes the correct `node-id`.
- We added a `Debug` tab and `debug-export.json`, but the user reported the Debug tab was empty.
- We then made `debug-export.json` unconditional and added a browser fallback so the tab should never be blank.
- We also added Figma API retry and short-lived caching to reduce `429 Rate limit exceeded` errors.

## Most Likely Next Step

Run a fresh generation after the rate limit cools down, then inspect `debug-export.json` from the UI response.

The next debugging goal is to determine which of these is true:

1. Funity is selecting the wrong screen node from the share link.
2. Candidate renderable nodes are being detected incorrectly.
3. Figma is returning no render URLs for the requested node IDs.

## Files Touched During This Thread

- `src/server.mjs`
- `src/cli.mjs`
- `src/convert.mjs`
- `src/figma/api.mjs`
- `src/figma/load.mjs`
- `src/assets.mjs`
- `src/render-assets.mjs`
- `src/generate/csharp.mjs`
- `src/generate/spec.mjs`
- `public/index.html`
- `public/app.js`
- `public/styles.css`
- `README.md`
- `start-funity.bat`

## Suggested Resume Prompt

"Continue debugging Funity's Figma asset export. Read `MEMORY.md`, inspect `debug-export.json` output for the real Figma frame, and determine whether the wrong node is selected or Figma is returning no rendered asset URLs."
