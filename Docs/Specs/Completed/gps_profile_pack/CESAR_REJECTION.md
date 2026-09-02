# Cesar rejection — `gps_profile_pack` iter-3

**Verdict: rejected on sight.** "Those images are full of issues and do not match Figma… Just a few
observations." The four he named are *samples*, not the list. Per PIPELINE_HARDENING §15 (second
defect of a shape ⇒ audit the shape), iter-4 does NOT fix four instances — it audits the shape
**"built element diverges from its node"** across every element of all three screens and fixes them
in one pass.

> **Cesar's direction: "Check the Score Upload conversation to see how we fixed them there."**
> `score_upload_flow` is the approved precedent. Its builder already solves every one of these.
> Reuse its idioms verbatim; do not invent new ones.

---

## Cesar's four observations, with the mechanism and the fix

### 1. EDIT PROFILE button is the wrong size

Built: `Rect("EditProfileButton", col, 10, 1487, 958, 120)` — a hard 958-wide bar
(`GpsProfilePackBuilder.cs:327`). The node render shows a **content-hugging** button ≈545px wide,
centred.

`ScoreUploadScreenBuilder.MainButton` (`:1240-1281`) is the approved solution and must be reused:
a **958×120 ROW**, containing a child of **width 0** with
`HorizontalLayoutGroup{padding 48/48, MiddleCenter, childControlWidth/Height=true,
childForceExpandWidth/Height=false}` + `ContentSizeFitter{horizontal=PreferredSize,
vertical=Unconstrained}`, so the button sizes to its label. Sprite `SprSilver` sliced with
`pixelsPerUnitMultiplier = 25f/20f` (→ r20). Label **size 59** (Build rule 4 — not 66; the 12%
Rubik-SemiBold calibration is written out at `:1266-1272`), `LayoutElement.min/preferredHeight = 120`.

### 2. Avatar picture is the wrong picture — it must be the Home-screen art, CROPPED

Two separate bugs.

**(a) Wrong source.** `GpsAvatarScreenController.cs:149` uses `charData.portraitFullSprite` — the
ROSTER full-body portrait. SPEC § Reference mandates the *Home* resolution, and
`HomeScreenController.UpdateHomeCharacterImage` (`:232-250`) is the exact code:

```csharp
var selectedId = CharacterManager.Instance?.GetSelectedCharacterId();
var csvChar    = CharacterDatabaseCSV.Instance?.GetCharacter(selectedId);
var sprite     = Resources.Load<Sprite>($"Characters/Homescreen/{csvChar?.characterName}")
              ?? Resources.Load<Sprite>("Characters/Homescreen/Placeholder");
```

**(b) Letterboxed instead of cropped.** `preserveAspect = true` inside the stage leaves the wide
green margins Cesar is looking at. **The node says explicitly how to do it** — node `14026:33445` is
named **`Avatar Figure (Main Menu Character instance)`** and contains:

```
frame  14026:33445  "Avatar Figure"        x=199 y=28   w=560  h=600    ← CLIPS
  instance 14029:102277 "Main Menu Character"  x=-82.7 y=-400  w=725.4  h=1569.84
```

So: a 560×600 **masked** container at (199,28) inside the stage, holding the Home sprite at
**725.4 × 1569.84**, offset **(-82.7, -400)** Figma-style (right, DOWN from the container's top-left)
— scaled to cover and pushed up so the head sits at the top, with the overflow clipped. Add a
`RectMask2D` on the 560×600 container. At that explicit size the aspect already matches, so
`preserveAspect` is irrelevant — set the rect, don't fit it.

### 3. Avatar container has no curved corners

The baked `S_PROF_AvatarStage.png` has square corners. Two ways, and Build rule 1 decides which:
the stage is a **gradient**, so it must stay a baked sprite — **bake the corner radius into
`make_gps_profile_panels.py`** (edit the SCRIPT, never the PNG) and export it 9-sliceable, or emit it
at final size with the radius already in the alpha.

For every **flat** panel, use the score-upload idiom instead of a bespoke PNG —
`ScoreUploadScreenBuilder.Panel(name, parent, x, y, w, h, fill, radius)` (`:1186-1191`) →
`Img(go, SprPill, fill, Image.Type.Sliced, radius)` with
`pixelsPerUnitMultiplier = PillBorder / radius` (`:1142-1143`). That is how every rounded panel on
the approved screens is made. **Audit all 11 `S_PROF_*` bakes**: any that is a flat fill should be
deleted and replaced with `Panel(...)`; any that is a real gradient keeps its baker, with the radius
baked in.

### 4. Badge colours, transparencies and missing icons

Pulled fresh from node `14027:33577`. The built cells are wrong on **every** axis:

| Property | Node (earned `14027:33578`) | Node (locked `14027:33611`) | Built |
|---|---|---|---|
| Fill | `rgba(255,255,255,0.10)` | `rgba(0,0,0,0.25)` | opaque cream `#efdc98` / slate `#495970` |
| Border | **2px, in the RARITY colour** | **1px `#4a5a6e`** | `S_PillStadium` painted opaque over the fill |
| Corner radius | `24px` | `24px` | — |
| Padding / gap | `px 6, py 10`, gap 4 | same | — |
| Icon Ring | 60×60 ellipse | 60×60 **at `opacity 0.60`** | present |
| **Star Icon** | **28×28 at (16,16), tinted the rarity colour** | 28×28, inside the 0.6 ring | **MISSING ENTIRELY** |
| `✓` | `#7ed488`, 18px SemiBold, top-LEFT | blank space (still occupies the slot) | — |
| Rarity label | 14px SemiBold, rarity colour, top-RIGHT | **still the rarity colour** (e.g. EPIC) | `—` on locked cells |
| Badge name | **18px SemiBold, white** | **18px SemiBold `#b7c3d3`** | too small, and the raw seed id |
| Badge pct | **16px Medium `#b7c3d3`** | same | `100%`/`0%` — see §5 |

Rarity palette: COMMON `#b7c3d3` · RARE `#6fa5e8` · EPIC `#b48cf0` · LEGEND gold.
The transparency is Build rule 2 — solve `A(white,.10,bg)` / `ADark(black,.25)` against the real
backdrop via `GpsUiColor`, then **do not over-paint it**. The current `Border` child laying
`S_PillStadium` fully across the 210×210 cell is what destroys the correctly-computed fill.
The ring is the navy-disc-in-gold-ring atom (Build rule 3), and the icon is a real
`Assets/Art/UI/Gps/ICO_Gps*.png` sprite — they exist, they are simply not being placed.

---

## Everything else the same audit turns up — fix in the same pass

**Avatar (node `14026:33187`), measured against the metadata tree:**
- Equip-slot Icon Rings are **84×84** with a **40×40 icon at (22,22)** — built uses 44px rings with
  no icon at all. Each slot has its OWN icon: CAP=Star, SHIRT=Sparkle, GLOVE=Heart, SHOES=Pin,
  CLUB=Rounds (`14026:33454/33461/33469/33476/33483`).
- Evolution rings are **68×68 (icon 32 at 18,18)**, and ONLY the current stage is **88×88
  (icon 44 at 22,22)** — built uses 88 for all five, so the current-stage emphasis is invisible.
  Locked stages 0.55 opacity.
- Level Row: Pill **99×45** at x=0, Rank Title at **x=113** — built centres a 260-wide pill.
- Status Panel: label 190 wide at x=32, track **592×14 at x=238**, value at x=846, delta at x=898
  (delta hidden per SPEC). Note the track is **14px**, not the 16px used elsewhere.

**Profile (node `14025:33087`):**
- Hero: avatar disc **170** at (394,30), Name at y=210, Sub at y=284, Stats Row **878×98 at y=327**.
- Quick stats are **three 307.33-wide tiles** at x=0 / 325.33 / 650.67 — check the built gaps.
- Recent Rounds panel is absent from the build entirely (`14025:33440`, 958×343, two `GpsHubRoundRow`).
- `SEE ALL ›` hidden and EDIT PROFILE `Enabled=No` per SPEC — keep both.

**Badges (node `14027:33298`):**
- SPECIAL section's last row is overlapped by the bottom nav bar — a screen-level containment bug.
- Section header icons: GOLF=Rounds, SOCIAL=Heart, TRUST=Pin, SPECIAL=Sparkle, 28×28 at (32,20).

**The four causes already diagnosed in `ARCHITECT_ESCALATION.md` — still open, still required:**
1. Controllers never fire their fetch. Copy `GpsHubScreenController.cs:128-136`: paint cache →
   subscribe → `client.Run(UserService.Instance.Detail(...))` (+ the stats/badges/history calls).
   Without this every field stays `—` no matter how good the layout is.
2. Badge key case: builder emits `BADGE_FIRST_ROUND_NAME` (`GpsProfilePackBuilder.cs:745`), CSV has
   `BADGE_first_round_NAME` (`LocalizationText.csv:859-882`). Stop uppercasing the id.
3. **All 75 new localization rows are unpublished** (`git diff --stat HEAD` → 75 insertions; zero at
   HEAD). Build rule 7: importer PLAN → APPLY → publish `texts` → `export_content.py --check` clean.
   Fixing #2 without this still renders raw keys.
4. `AVG PUTTS` / `GIFTS SENT` must render `—` permanently (SPEC deviations). They currently show
   seeded literals (`33.2`, `24`) because no controller field owns them — give the controller the
   fields, or drop the literals from the builder.

**Not a defect — do not "fix" it:** `ButtonCancel.png` IS this project's silver Main Button
(`ScoreUploadScreenBuilder.cs:38` `SprSilver`, used `:1251`/`:1286`). Item 2 of the iter-2 list is
closed.

---

## How iter-4 must be evidenced

Cesar rejected three builds by eye in seconds. The gate is no longer "did the linter pass".

- **Per-element A/B against the reference renders**, which are already in
  `reference/gps_profile_14025-33087.png`, `gps_avatar_14026-33187.png`,
  `gps_badges_14027-33298.png`. Paste the built crop beside the node crop for every mandated
  element (Rule 10). "Matches Figma" is not acceptable.
- **Re-pull each node with `get_design_context` and diff live px/font/gap/sprite against the NODE**
  (Rule 9). The SPEC table is a convenience, never source of truth — this rejection was written from
  a fresh node pull, and it found values no prior report had.
- **The ΔRGB reference-diff table required by Build rule 5 has never once appeared in a report.**
  Produce it this time.
- Regenerate `reference/nodes/*_spec.json` so the linter actually constrains the elements that keep
  shipping broken — `Background`, badge cell fill/border/radius, the avatar stage radius, the icon
  rings. A `fail == 0` from a spec that omits the broken element is worthless, which is exactly what
  happened in iter-2.
- Screens signed in, with the service response log lines quoted.

**Do not mark anything PASS you have not looked at.** One Rule 6 fabrication is already logged for
this task.
