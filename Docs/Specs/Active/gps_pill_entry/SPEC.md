# SPEC — `gps_pill_entry`

> Amends `punch_it_gps_variants` (Completed). **Reads this file only for the work definition.**

## Goal

Move the GPS entry point from the Home cross-promotion banner to a **GPS pill**, reproduced
exactly from Figma node `14060:4638`, and **restore the banner to ordinary behaviour**.

After this task:

- **The pill is the GPS door.** It is what `GpsGate` turns on and off — visible and tappable in a
  "punch it GPS" build, absent in a "punch it" build. The GPS screens stay gated exactly as they
  are now.
- **The banner is just a banner again.** `BannerSlotBinder.Apply()`'s gated-route hide (added by
  `punch_it_gps_variants`) is REVERTED: whatever the admin publishes shows, on both variants.

## Reference

- **Frame:** `5gEAHjl6xAtW8iYY7NMvWd` node `2098:8490` ("New - UK Female") →
  `reference/home_frame_2098-8490.png`. Shows the pill AND the banner coexisting.
- **Pill node:** `14060:4638` → `reference/gps_pill_14060-4638_4x.png` (4× export).
- Re-pull `14060:4638` with `get_design_context` at step 0 (PIPELINE_HARDENING §9) — the table
  below is a convenience, the node is the source of truth.

## Figma fidelity (Rule 18) — per element, from the node

| Element | Node | Spec | Built |
|---|---|---|---|
| Pill box | `14060:4638` | **138 × 92**, radius 50 (a true capsule — 50 > half of 92) | |
| Border | `14060:4638` | **3px `#FCF195`** | |
| Fill | `14060:4638` | vertical gradient **`#133453` → `#091B33`** | |
| Inner rule | `I14060:4638;13994:1743` | **1px `#0A1D35`**, radius 50, inside the gold | |
| Label | `I14060:4638;13994:1745` | text **"GPS"**, Rubik **SemiBold**, **45px**, line-height 60, tracking **−0.69**, colour **`#EEDC9A`**, centred | |
| Label padding | `I14060:4638;13994:1744` | 24px horizontal, 16px vertical | |
| Position | frame `2098:8490` | pill at **x 1008, y 251** (top-right, notched into the username ridge). Canvas is 1170×2532 at scale 1, so a Figma px IS a Unity px | |

## Implementation

### 1. Art — DONE, no Unity needed

`Docs/Scripts/make_gps_pill.py` → `Assets/Art/HomeScreen/S_GpsPill.png` (276×184, 2× of 138×92).

Baked whole and drawn `Image.Type.Simple`, **not** 9-sliced, and **not** reusing
`S_DailyPillPanel.png` despite identical tokens: that sprite is 9-sliced with a 50px corner
authored at height 122, and 50+50 corners cannot fit in a 92-high box — the corner-collapse oval
Rule 21's render-health check exists to catch. Safe here because the box is FIXED (see §3).

Verified against the node by sampling the bake: gold band `#FCF195`, inner rule present, body
`#133453` at top → `#091B33` at bottom.

⚠️ **Import it as a Sprite** (2D/UI). A default-imported texture returns null from
`LoadAssetAtPath<Sprite>` and the pill renders as a white box (playbook §3).

### 2. The pill object

Home-screen child (the node places it inside the Home frame, not the shared top bar — it must not
appear on Roster/Inventory). Structure, mirroring `DailyMissionPill`:

```
GpsPill                 RectTransform 138x92, anchored top-right per the node's (1008, 251)
├── Panel               Image  S_GpsPill  Type.Simple      ← the Button's targetGraphic
└── Label               TMP  "GPS"  Rubik SemiBold  45  #EEDC9A  centred
```

- `Button` + **`Golfin.UI.Polish.ButtonPressFeedback`** (CLAUDE.md hard rule 11 — every new
  player-facing Button gets it, defaults kept).
- `onClick` → `ScreenManager.Instance.ShowScreen(ScreenId.GpsHub)`.

### 3. Localization — fixed box, autosized text

Cesar, 2026-09-02: *"keep the pill size and autosize text"*.

- The 138×92 rect is **fixed**. Do not size-to-content, do not add a `ContentSizeFitter`.
- The label uses TMP **auto-size** with the node's 45 as the MAXIMUM, a sane floor (≈28), and
  wrapping off, so a longer localized value shrinks inside the box instead of overflowing it.
- New key **`HOME_GPS_PILL`**, EN `GPS` / JA `GPS` (brand, same both languages today — the key
  exists so a future change is a publish, not a build). Add to
  `Assets/Localization/LocalizationText.csv`, re-import the table, **and publish it** — a changed
  row that is not published loses to the bundled table at runtime (standing rule).

### 4. Gating — the pill replaces the banner as what GpsGate toggles

- The pill's root is active only when `GpsGate.Enabled`. Put that in the Home controller beside
  the existing wiring; the GPS-screen deny-list in `GpsGate` is unchanged.
- **Revert** the `punch_it_gps_variants` hunk in `BannerSlotBinder.Apply()` (the
  `TryGetInternalRoute` + `Hide()` block). The banner returns to "show whatever is live".
- `ScreenManager`'s three gate points and `GpsGate` itself are otherwise untouched.

⚠️ **Do the revert and the pill in ONE change.** Reverting alone would ship a "punch it" build
whose banner is visible but whose tap hits the ScreenManager gate — the dead strip Cesar
explicitly rejected.

## Acceptance checklist

- [ ] Pill matches the node: 138×92, r50 capsule, 3px `#FCF195`, `#133453→#091B33`, 1px `#0A1D35`
      inner rule, "GPS" Rubik SemiBold 45 `#EEDC9A` — **crop the node render and the live capture,
      stack them, and enumerate the differences** (playbook §7), not "matches Figma".
- [ ] Measured built rect is exactly 138×92 at the node's position; no `ContentSizeFitter`.
- [ ] Label autosizes: forcing a long string keeps the box at 138×92 and shrinks the text.
- [ ] `S_GpsPill.png` imported as a Sprite; the pill is NOT a white box; `Image.Type` is Simple.
- [ ] `ButtonPressFeedback` present on the pill's Button.
- [ ] Tapping the pill opens `GpsHub` (real widget `onClick`, through real navigation).
- [ ] GPS OFF (temporary const flip): the pill is absent, the five GPS screens still refuse, and
      **the banner shows normally** and is tappable to whatever it links to.
- [ ] GPS ON: pill present, banner present — both, as in the node's frame.
- [ ] `HOME_GPS_PILL` added to the CSV, table re-imported, and **published** (`--check` clean).
- [ ] EditMode suite green; UI fidelity lint `fail == 0` on the touched prefab.
- [ ] Unity Console: no errors from this task.

## Files this task touches

- `Docs/Scripts/make_gps_pill.py` — NEW (done)
- `Assets/Art/HomeScreen/S_GpsPill.png` — NEW (done; needs Sprite import)
- Home screen prefab/scene — the `GpsPill` object
- `Assets/Scripts/UI/HomeScreenController.cs` — pill wiring + `GpsGate.Enabled` visibility
- `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` — REVERT the gated-route hide
- `Assets/Localization/LocalizationText.csv` (+ table asset) — `HOME_GPS_PILL`
- `Docs/PUNCH_IT_ROUTINE.md` — the on-device tell changes from "banner" to "GPS pill"

## Out of scope

- No change to `GpsGate`'s screen deny-list, `ScreenManager`, the build profiles, the lanes, or
  anything else `punch_it_gps_variants` shipped.
- No banner/server changes — the `home_promo` row stays exactly as published.
- No new GPS screens.
