# taiheiyo_club_gotenba_content — NOTES

> **Status:** QUEUED (sidequest). Depends on `multi_club_architecture_refactor` shipping first.
> **Notion:** TBD (Order 300 — after multi-club refactor at 290, before Loop v2 350-ish picker work).
> **Tier:** 1 mostly (Cesar runs UHoleGeo + Unity import; Code can run the bake-all script).
> **Source:** https://www.taiheiyoclub.co.jp/course/gotenba/information.html (Cesar provided 2026-05-17).

---

## Why

Second course in the catalogue. Off the back of multi-club refactor, this is pure content drop following the canonical `Docs/Pipeline/ADD_HOLE.md` flow under a new course slug.

---

## The course (anchors for the config)

- **Display name:** Taiheiyo Club Gotenba Course (太平洋クラブ 御殿場コース)
- **Slug:** `taiheiyo-club-gotenba`
- **Location:** Gotenba City, Shizuoka Prefecture, Japan, at the foot of Mt. Fuji.
- **Center lat/lon:** TBD — Cesar to grab from Google Maps. Course is at the foot of Mt. Fuji; rough anchor ~35.30°N, 138.92°E (refine before importing).
- **Designer:** Shunsuke Kato (1977), renovated 2018 by Rees Jones with Hideki Matsuyama as consultant.
- **Tournament heritage:** 三井住友VISA太平洋マスターズ (since 1977), WGC-EMC World Cup 2001, マルハンカップ太平洋クラブシニア (since 2020).
- **18 holes, par 72** (par 36/36 OUT/IN).
- **6 tee sets:** Tournament / Back / Regular / Middle / Front / Ladies (this is the reason 6-tee schema is the lock).

### OUT (front 9)

| Hole | Par | HDCP | Tournament | Back | Regular | Middle | Front | Ladies |
|------|-----|------|------------|------|---------|--------|-------|--------|
| 1    | 4   | 5    | 465        | 445  | 415     | 409    | 395   | 335    |
| 2    | 4   | 13   | 435        | 395  | 385     | 355    | 345   | 315    |
| 3    | 5   | 17   | 565        | 530  | 515     | 500    | 480   | 435    |
| 4    | 3   | 9    | 220        | 195  | 180     | 158    | 130   | 126    |
| 5    | 4   | 7    | 400        | 385  | 370     | 350    | 330   | 315    |
| 6    | 5   | 15   | 540        | 525  | 510     | 490    | 450   | 425    |
| 7    | 3   | 11   | 178        | 157  | 152     | 140    | 130   | 127    |
| 8    | 4   | 3    | 447        | 415  | 400     | 375    | 350   | 310    |
| 9    | 4   | 1    | 465        | 425  | 410     | 385    | 360   | 305    |
| OUT  | 36  |      | 3,715      | 3,472| 3,337   | 3,162  | 2,970 | 2,693* |

*Ladies OUT sum from the page was truncated mid-line; double-check on second fetch.

### IN (back 9) — TODO

The page got truncated mid-fetch at the OUT row. Second fetch needs to capture holes 10-18 and the IN totals before SPEC writing.

---

## Deliverables (in order)

### Step 1 — UHoleGeo config

New file: `Tools/UHoleGeo/config/taiheiyo-club-gotenba.json`. Mirror `lomond-country-club.json` structure:
```json
{
  "course_id": "taiheiyo-club-gotenba",
  "display_name": "Taiheiyo Club Gotenba Course",
  "native_name": "太平洋クラブ 御殿場コース",
  "center": { "lat": 35.30, "lon": 138.92 },
  "sources": { "official_url": "https://www.taiheiyoclub.co.jp/course/gotenba/information.html" },
  "gsi_zoom_default": 18,
  "terrain_defaults": { ... }
}
```

Terrain defaults: course is in Mt. Fuji foothills, expect moderate-to-high relief. Re-tune `base_undulation_m`, `tree_ridge_m` after the first hole's accuracy review.

### Step 2 — Course meta JSON

`Tools/UHoleGeo/output/taiheiyo-club-gotenba/course.json` mirroring `lomond-country-club/course.json`. 18 holes, 6 tees each, par + hdcp + descriptions from the official site. The site's `crs_inf_pkup_*` hole highlights become the JP descriptions on holes that have them; others stay null until a manual write pass.

### Step 3 — Source assets

- **Course PNG/GIF maps** from the official site (one per hole) into `Tools/UHoleGeo/output/taiheiyo-club-gotenba/source/`.
- **GSI satellite tiles** for the 18 holes — UHoleGeo's fetch-satellite step handles this automatically once the config is in place.

### Step 4 — Anchors (~2 hours of manual work)

Per hole, source tee + green lat/lon from Google Earth or Cesium Viewer using the official hole map as alignment guide. Populate `anchors.json` per hole. This is the meaty manual chunk and the single biggest cost item.

### Step 5 — Pipeline run

Per `Docs/Pipeline/ADD_HOLE.md`:
```powershell
cd Tools/UHoleGeo
node scripts/run-all.mjs taiheiyo-club-gotenba --all
```

Then in Unity:
- `Import > Geo > Normal > Import Hole XX Geo` x 18  *(menu entries auto-generated for the new course by multi-club refactor)*
- `Import > Bake Physics Heightmap > Bake All Holes`  *(course-aware post-refactor)*
- `GOLFIN > Tools > Bake Zone JSON (All Holes)`  *(course-aware post-refactor)*

### Step 6 — Smoke per hole

`PhysicsLab + LabScaffold + Hole Picker` (Hole Picker is course-aware post-refactor): load each hole, ball-place each surface category, fire one shot, confirm sane settle.

### Step 7 — `HoleDatabase` rows

18 new `HoleData` rows under `taiheiyo-club-gotenba` course. Strategy text from the site for highlight holes; placeholder for the rest. Reward sets reuse Lomond's reward scale until economy is per-course tuned.

---

## Out of scope

- **In-game course splash card** (story + designer credits + photos). Drops with Loop v2 §3b picker polish; spec'd separately.
- **Per-course skybox / lighting tuning.** Inherits Lomond's setup at first; revisit if Mt. Fuji backdrop looks wrong.
- **Per-hole Figma reference art.** Reuse Lomond's hole-card visual style until art bandwidth allows.

---

## Estimate

Realistic: **2 days end-to-end** mostly Cesar-driven.
- ½ day: configs + anchors + first hole roundtrip (catch surprises early)
- 1 day: bulk import + bake of holes 2-18
- ½ day: smoke + HoleDatabase population

Multi-club refactor (Phase 1) is the hard precondition. Without it, this can't begin without data collision.
