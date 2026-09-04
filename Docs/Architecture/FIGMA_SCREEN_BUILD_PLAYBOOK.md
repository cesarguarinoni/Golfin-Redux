# Figma → Unity screen build playbook

Distilled from `gps_profile_pack` (2026-09-02), where three pipeline iterations passed all gates and
were rejected on sight, and from `score_upload_flow` before it. Narrative version: `tasks/lessons.md`
Lesson BS. **Cite this file from the SPEC of any new Figma-node screen** and work the checklists.

---

## 0 · The instrument comes first

A wrong measuring instrument costs more than a wrong build, because it makes you confident.

- [ ] **Capture through REAL navigation.** Boot, tap PLAY, drive the actual widget `onClick`,
      screenshot. Do not build a preview-scene render harness — one produced two false readings in
      twenty minutes (raw loc keys, and a background swap that silently did nothing).
- [ ] **Two edit-mode gaps a harness will hit:** `LocalizationManager` is only `Initialize()`d at
      boot, so `Get()` returns the KEY; and `LocalizedText.Refresh()` runs in `OnEnable`, which
      never fires for an edit-mode instantiation.
- [ ] **The screen prefab paints its OWN `Background` child.** Anything placed behind it is invisible.
- [ ] **Match the backdrop before trusting any ΔRGB.** Same UI measured 23.03% over the shipped
      background and 8.17% over the node's own plate.
- [ ] **Sample colours at 1:1.** A downscaled crop will lie about hue.

## 1 · Backgrounds are per screen, and the match is measurable

- [ ] Pull each frame's `Backgrounds` node render.
- [ ] Diff it against every project background before importing anything new — Badges matched an
      existing `BG_SU_GpsProof.png` at **mean |ΔRGB| 0.000, max 0**.
- [ ] Only if nothing is close, import the plate as a real asset (`BG_<TASK>_<Frame>.png`), the way
      `BG_SU_*` were added. Precedent: `ScoreUploadScreenBuilder:82-86`.

## 2 · Geometry

- [ ] **Read every rect off THIS node.** Never inherit a sibling screen's number — a hero built at
      the hub's 296 instead of this node's 449 stacked the avatar disc on the player name.
- [ ] Canvas is 1170x2532 at scale 1, so **a Figma px IS a Unity px** (`F(x) => x`). Do not ÷1.2.
- [ ] Re-pull the node with `get_design_context` (not just the SPEC table) — the SPEC is a
      convenience, the node is the source of truth.
- [ ] Verify by **measuring the built rects and printing them beside the node's**, not by eye.

## 3 · Panels, fills and bars

- [ ] Every card in this family: `bg-gradient rgba(19,52,83,.6) → rgba(9,27,51,.6)`, **3px white
      border**, r50 on big cards / r32 on small tiles. Bake it (`bake_card`), never tint a flat sprite.
- [ ] **Progress bars are WIDTH-driven.** `Image.Type.Filled` discards 9-slicing and renders the cap
      as a thin wedge. Use the shared `Bar()` + `GpsUiColor.SetBarFill()`.
      Reference: `ScoreUploadScreenBuilder:844-847`.
- [ ] **Pick the right alpha helper.** Builder-local `A(overlay, α, backdrop)` PRE-COMPOSITES to an
      opaque colour (right for a chip on a known panel). `GpsUiColor.A(c, α)` / `ADark()` are truly
      translucent (right for anything over a photo).
      *Measured cost of getting this wrong (`auth_golf_profile`, 2026-09-02):* the node's
      `fill-opacity="0.35"` white pager dots authored with `GpsUiColor.A(White, 0.35f)` rendered
      **(161,170,180)** against the node's **(103,137,158)** — 45 per channel, plainly visible as
      four pale dots instead of dim ones. The direction FLIPS with the overlay (a white one lands
      too bright, a dark one too dark), so there is no blanket correction. On a known backdrop,
      sample the node's own composite at 1:1 and author it OPAQUE: `#67899E` here, Δ 0.3.
- [ ] Need a per-instance colour (rarity border, level pill)? **Bake it WHITE and tint at runtime** —
      `bake_frame` / `bake_pill`. A solid capsule tinted as a "border" paints over the fill.
- [ ] **Force `TextureImporterType.Sprite` on every freshly baked PNG.** A default-imported texture
      returns null from `LoadAssetAtPath<Sprite>` and Unity draws a **white box**.

## 4 · Text

- [ ] Pass a **`localizeKey`**, never a build-time `LocalizationManager.Get()` (that bakes the raw key).
- [ ] **Read the localized VALUE before adding a glyph** — `GPS_PROFILE_TRUST` already contains `✓`.
- [ ] **Confirm the font has the glyph** (`TMP_FontAsset.HasCharacters`). Rubik has no U+1F512.
- [ ] Check **weight** and **rendered size** against the node, not the arithmetic.
- [ ] **The `Main Buttons` 66→59 calibration is a property of the FONT, not of buttons.** Every
      SemiBold run authored at the node's nominal px comes out 10–12 % oversize (measured across
      four runs on `auth_golf_profile`). Author SemiBold as `node_px * 59/66` everywhere and let the
      button's 59 derive from the same constant.
- [ ] **Never hard-code the x of a text run.** The node's mock is usually the SHORT case; a longer
      string or Japanese overflows. Centre/right-align with a content-sized `HorizontalLayoutGroup`.

## 5 · State belongs to the controller

- [ ] Do not bake runtime state into the prefab (the "current" evolution stage's size sat on
      whichever stage was seeded, not the player's level).
- [ ] `Populate()` must clear **every** child of its container, not just what it created — otherwise
      the builder's seeded grid stays visible underneath the live cells.
- [ ] Controllers must **fire their own fetch** in `OnEnable`, not just subscribe. Pattern:
      `GpsHubScreenController:128-136` — paint cache → subscribe → `client.Run(...)`.
- [ ] `interactable = false` uses Unity's default `disabledColor` (**alpha 128**). Set it opaque if
      the design draws the control solid.
- [ ] An empty list is a STATE, not a reason to hide the panel. Keep the panel, show the empty line
      (`GPS_HUB_NO_ROUNDS`).

## 6 · Assets from the node

- [ ] **Image-instance offsets in a node are authored for FIGMA's art.** The avatar's
      `(-82.7,-400) 725.4x1569.84` crop suits Figma's character; our sprite is 1090x1907, so it
      stretched and framed the torso. Compute a cover-crop from the REAL sprite dimensions.
- [ ] Reuse project atoms (`S_HUB_*`, `S_GpsIconRing_*`, `ICO_Gps*`) before baking anything new.
- [ ] **`S_GpsIconRing_*` is a FILLED circle, not an annulus** — `make_gps_icon_ring.bake()` paints
      its fill to the OUTER radius and strokes over it. Anything placed BEHIND one is completely
      invisible. A coloured disc under the ring rendered identically navy in all four colours while
      every inspection (sprite, serialized array, prefab overrides) read as correct. Put the colour
      on the ring's OWN fill and collapse the two Images into one.

## 7 · Before you surface it

- [ ] **Crop matched regions from node and live capture, stack them, and enumerate the differences
      yourself.** In this task Cesar named four defects; the same crop sheet then produced six more
      in one pass, including six of 24 rarity tags being wrong.
- [ ] Report ΔRGB per screen AND say what the residual is — data differences (the node mocks
      populated state) are not fidelity defects, and should be named as such.
- [ ] **Publish new text keys.** Regenerating the Unity table (`Tools/Localization/Import Text CSV`)
      makes them resolve in the Editor only; an unpublished key renders raw on device.
