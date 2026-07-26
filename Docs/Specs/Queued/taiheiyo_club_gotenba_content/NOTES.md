# taiheiyo_club_gotenba_content — NOTES

> **Status:** PARTIAL_PROGRESS (UHoleGeo prep landed 2026-06-01). Full pipeline run blocked on Phase 1 (multi-club Unity refactor) AND on manual content gathering (anchors, hole imagery, DEM5A tiles, center lat/lon confirmation).
> **Notion:** Order 295 — entry [`36331e0e-9a36-8102-8bb3-def1ab401908`](https://www.notion.so/36331e0e9a3681028bb3def1ab401908). Status=Queued.
> **Tier:** 1 mostly (Cesar runs UHoleGeo + Unity import; Architect/Code can run the bake-all script once Phase 1 lands).
> **Source:** https://www.taiheiyoclub.co.jp/course/gotenba/information.html (Cesar provided 2026-05-17, full data captured 2026-06-01).

---

## Why

Second course in the catalogue. Off the back of multi-club refactor, this is pure content drop following the canonical `Docs/Pipeline/ADD_HOLE.md` flow under a new course slug.

UHoleGeo prep (config + course meta) is the "shovel-ready" portion of this work and can be done out-of-Unity while Code is busy on greens. Filed in this session 2026-06-01.

---

## The course (anchors for the config)

- **Display name:** Taiheiyo Club Gotenba Course (太平洋クラブ 御殿場コース)
- **Slug:** `taiheiyo-club-gotenba`
- **Location:** Gotenba City, Shizuoka Prefecture, Japan, at the SE foot of Mt. Fuji.
- **Address:** 〒412-0048 静岡県御殿場市板妻 941-1 / 941-1 Itazuma, Gotemba, Shizuoka 412-0048, Japan
- **Center lat/lon:** **PLACEHOLDER** `35.291, 138.902`. Gotenba city centroid is `35.308694, 138.934611`; course is southwest of city in Itazuma, near the East Fuji JGSDF training grounds. **Cesar TODO:** confirm in Google Earth before running fetch-satellite.
- **Original designer:** Shunsuke Kato (加藤俊輔), 1977.
- **2018 renovation:** Rees Jones with Bryce Swanson, supervised by Hideki Matsuyama (松山英樹).
- **Tournament heritage:** 三井住友VISA太平洋マスターズ (since 1977), WGC-EMC World Cup 2001, マルハンカップ太平洋クラブシニア (since 2020).
- **18 holes, par 72** (par 36/36 OUT/IN).
- **6 tee sets:** Tournament / Back / Regular / Middle / Front / Ladies. Course rate 74.2 from Tournament. Total yardages 7,327 / 6,902 / 6,539 / 6,194 / 5,794 / 5,305.
- **Course area:** 910,000 m² (vs Lomond's 1,370,000 m² — smaller area despite longer total yardage).

### Full yardage table (per `course.json`)

| # | Par | HDCP | Tournament | Back | Regular | Middle | Front | Ladies |
|---|-----|------|------------|------|---------|--------|-------|--------|
| 1 | 4 | 5  | 465 | 445 | 415 | 409 | 395 | 335 |
| 2 | 4 | 13 | 435 | 395 | 385 | 355 | 345 | 315 |
| 3 | 5 | 17 | 565 | 530 | 515 | 500 | 480 | 435 |
| 4 | 3 | 9  | 220 | 195 | 180 | 158 | 130 | 126 |
| 5 | 4 | 7  | 400 | 385 | 370 | 350 | 330 | 315 |
| 6 | 5 | 15 | 540 | 525 | 510 | 490 | 450 | 425 |
| 7 | 3 | 11 | 178 | 157 | 152 | 140 | 130 | 127 |
| 8 | 4 | 3  | 447 | 415 | 400 | 375 | 350 | 310 |
| 9 | 4 | 1  | 465 | 425 | 410 | 385 | 360 | 305 |
| **OUT** | **36** | | **3,715** | **3,472** | **3,337** | **3,162** | **2,970** | **2,693** |
| 10 | 4 | 8  | 401 | 385 | 365 | 355 | 320 | 280 |
| 11 | 5 | 12 | 540 | 520 | 505 | 485 | 452 | 445 |
| 12 | 4 | 14 | 451 | 430 | 385 | 374 | 317 | 312 |
| 13 | 3 | 16 | 203 | 192 | 173 | 152 | 145 | 130 |
| 14 | 4 | 4  | 422 | 390 | 365 | 345 | 338 | 315 |
| 15 | 4 | 18 | 378 | 368 | 355 | 342 | 300 | 290 |
| 16 | 4 | 2  | 462 | 440 | 400 | 355 | 343 | 310 |
| 17 | 3 | 6  | 230 | 195 | 164 | 144 | 139 | 115 |
| 18 | 5 | 10 | 525 | 510 | 490 | 480 | 470 | 415 |
| **IN** | **36** | | **3,612** | **3,430** | **3,202** | **3,032** | **2,824** | **2,612** |
| **TOTAL** | **72** | | **7,327** | **6,902** | **6,539** | **6,194** | **5,794** | **5,305** |

### Per-hole JP descriptions (from official site PICK UP highlights)

Only two highlighted on the official information page (the first is mislabeled in the HTML as "HOLE 18" but the image filename `crs_inf_img_06.jpg` confirms it's hole 6):

- **Hole 6 (par 5):** "ランディングゾーン左にバンカーを新設。ガードバンカーも再造形及び新設した。松山プロの意見を受けてPar5／Par4（510ヤード）どちらの設定も可能なレイアウトに。Par4ならば難度が相当上がる。"
- **Hole 18 (par 5):** "フェアウエイバンカーを再造形し、\"考えさせるティーショット\"へ。グリーン左のバンカーも再造形し、グリーン右の池を奥に8ヤード増設。ターゲットがシビアになり、よりドラマチックな最終ホールとなった。"

Other 16 holes have `description_jp: null`. Cesar can fill from Gora / Shotnavi or leave null.

---

## Deliverables — what landed 2026-06-01

✅ **`Tools/UHoleGeo/config/taiheiyo-club-gotenba.json`** — course config in UHoleGeo format. Mirrors Lomond config; placeholder center lat/lon flagged inline.

✅ **`Tools/UHoleGeo/output/taiheiyo-club-gotenba/course.json`** — full 18-hole metadata: par, hdcp, 6-tee yardages, JP descriptions on holes 6 and 18, tee colors NULL (no on-site knowledge).

---

## Deliverables — TODO (in order)

### Manual content gathering (Cesar)

1. **Confirm center lat/lon** in Google Earth — open `https://www.google.com/maps/place/%E5%A4%AA%E5%B9%B3%E6%B4%8B%E3%82%AF%E3%83%A9%E3%83%96+%E5%BE%A1%E6%AE%BF%E5%A0%B4%E3%82%B3%E3%83%BC%E3%82%B9` and read the actual course centroid. Update `config/taiheiyo-club-gotenba.json` `center` block. The placeholder is rough; this matters for fetch-satellite zoom-level framing.
2. **Source hole-map images** from the official site. Per-hole pages may exist (Lomond uses `course_e01.gif`-style URLs); spot-check the navigation menu. If only per-hole detail pages exist with photos, screenshot the hole layout diagrams. Place under `Tools/UHoleGeo/output/taiheiyo-club-gotenba/source/`. Update `source_gif` fields in `course.json`. Optional — UHoleGeo doesn't strictly need these for pipeline, but they're valuable for the in-game course-info card later.
3. **Anchor data — the meaty chunk (~2h).** For each of 18 holes, source tee + green lat/lon from Google Earth or Cesium Viewer using the satellite imagery as alignment guide. Populate `anchors.json` per hole in `Tools/UHoleGeo/output/taiheiyo-club-gotenba/holes/NN/`.
4. **Tee marker colors (optional).** The course doesn't publish them. Fill in if you find a brochure or visit; otherwise leave null — UHoleGeo doesn't use tee colors for the pipeline.

### Infrastructure (shared, one-time)

5. **GSI DEM5A tiles for Gotenba area.** `dev-server.mjs` line ~268 sources DEM5A from `Tools/UHole/output/<courseId>/basemap/gsi-dem5a-z15` (legacy UHole directory). Tiles covering the Gotenba course extent need to land there before `generate-terrain.mjs` will work for Taiheiyo. Manual fetch from https://maps.gsi.go.jp/. Lomond's tile set is the template.

### Pipeline run (Cesar, after 1-5 complete)

6. Run per-hole:
   ```powershell
   cd Tools/UHoleGeo
   node scripts/run-all.mjs taiheiyo-club-gotenba 1   # one hole first
   node scripts/run-all.mjs taiheiyo-club-gotenba --all  # then all 18
   ```
   This produces the export package at `Tools/UHoleGeo/output/taiheiyo-club-gotenba/export/hole-XX/`.

### Unity-side (Phase 1 `multi_club_architecture_refactor` — DONE 2026-07-27)

> **⚠️ CODE PREREQUISITE before a second course ships — discovered by Phase 1 red-team, NOT yet fixed.**
> `Assets/Scripts/Course/Runtime/GreenTopologyCache.cs:42` caches by `Dictionary<int, GreenTopology>` — **hole number alone, not course-namespaced.** `GreenTopology.LoadFromResources` beneath it IS course-aware, but the cache in front of it is not: with a second course loaded, `GetForHole(1)` silently returns Lomond's green topology for Taiheiyo's Hole 1. Its own doc comment (`:26`) claims `HoleSessionDriver.OnHoleUnloaded` invalidates the cache — **that call does not exist**; every real `Invalidate`/`InvalidateAll` caller is test- or editor-only. Correct for every state reachable *today* (single course), which is why Phase 1 correctly left it untouched (outside §1.2/§1.3/§1.8 surface). **But SPEC §7's "Taiheiyo becomes content-only, no code" is false until this cache is course-keyed.** Fix: key the cache on `(courseSlug, holeNumber)` and wire a real invalidation on hole unload. Do this BEFORE importing Taiheiyo holes, or greens will be silently wrong.

7. **Unity importer course-aware — DONE.** Phase 1 replaced the 36 hardcoded menu lines with `GOLFIN > Course Importer` (`CourseImporterWindow`) and namespaced `Assets/Resources/HoleData/<course-slug>/`. **NOTE:** the window compiles but was never exercised on a real import during Phase 1 (the 40 legacy `HoleGeoImporter` menu items were intentionally retained per SPEC §2 until it is). First Taiheiyo import doubles as the window's real-world verification — if it misbehaves, the legacy menu items are still available as fallback.
8. **Then per-hole:**
   ```
   Unity > Import > Geo > Normal > Import Hole XX Geo (Taiheiyo)   # menu added by Phase 1
   Unity > Import > Bake Physics Heightmap > Bake Hole XX
   Unity > GOLFIN > Tools > Bake Zone JSON (Active Hole)
   ```
9. **Smoke test** each hole per `Docs/Pipeline/ADD_HOLE.md` Step 6.

### Game data (Cesar, smaller)

10. **`HoleDatabase` rows** for Taiheiyo's 18 holes. Strategy text from `course.json` for holes 6 + 18; placeholder for the rest. Reward sets reuse Lomond's reward scale until economy is per-course tuned.

---

## Out of scope (defer)

- **In-game course splash card** (story + designer credits + photos). Drops with Loop v2 §3b picker polish; spec'd separately.
- **Per-course skybox / lighting tuning.** Inherits Lomond's setup at first; revisit if Mt. Fuji backdrop looks wrong.
- **Per-hole Figma reference art.** Reuse Lomond's hole-card visual style until art bandwidth allows.

---

## Estimate (remaining work)

Realistic with Phase 1 complete: **~2 days end-to-end** mostly Cesar-driven.
- ½ day: lat/lon confirm + anchors + DEM tile download + first hole roundtrip (catch surprises early)
- 1 day: bulk import + bake of holes 2-18
- ½ day: smoke + HoleDatabase population

Phase 1 must land first for the Unity-side steps. UHoleGeo work above can proceed in parallel.

---

## Session log

- **2026-05-17** — Initial NOTES authored. Path A locked. Notion entry created Order 295 P3.
- **2026-06-01** — Full 18-hole data captured from official site (back-9 was truncated in first fetch). Wrote `config/taiheiyo-club-gotenba.json` and `output/taiheiyo-club-gotenba/course.json`. Confirmed UHoleGeo scripts are course-agnostic by arg/query-param; `dev-server.mjs` defaults to lomond but accepts any course slug. Flagged legacy `UHole/output/<course>/basemap/gsi-dem5a-z15` DEM tile path as a manual prereq.
