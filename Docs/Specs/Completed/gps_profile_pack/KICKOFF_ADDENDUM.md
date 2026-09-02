# Kickoff addendum — `gps_profile_pack`

> Written by the orchestrator (Claude Code main thread) at dispatch time, after pulling the three
> Figma nodes and verifying every codebase anchor `SPEC.md` cites. **`SPEC.md` remains authoritative
> for the work definition.** This file only resolves things the spec left open or got slightly wrong,
> so the implementer does not have to guess and does not burn an `ESCALATE` round-trip on them.
> Where this file and `SPEC.md` disagree on a *fact about the repo*, this file wins (it was measured);
> where they disagree on *intent*, `SPEC.md` wins.

---

## A. What is already done for you

`reference/` is populated with the three canonical node renders at 1170×2532, pulled with
`download_assets` at scale 1 on 2026-09-01:

| File | Node | Frame |
|---|---|---|
| `reference/gps_profile_14025-33087.png` | `14025:33087` | GPS Profile |
| `reference/gps_avatar_14026-33187.png`  | `14026:33187` | GPS My Avatar |
| `reference/gps_badges_14027-33298.png`  | `14027:33298` | GPS Badges |

These are the A/B ground truth for Rule 10 (reference-image diff) and for the § Figma Fidelity table.
**You still owe the Rule 9 node re-pull** (`get_design_context` on each node at step 0) — the renders
above are geometry/appearance evidence, not a substitute for reading the node's own tokens.

`reference/nodes/` is empty **on purpose**: the three `*_geometry.json` files and the `*_spec.json`
linter inputs are yours to generate (Build rule 5 / Rule 21). Generate geometry from
`get_metadata` on each frame node, in the exact shape of
`Docs/Specs/Completed/gps_hub_entry/reference/nodes/GpsHubScreen_geometry.json`
(`{prefab, note, sites:[{path, node, figma:[x,y,w,h], adj?, note?}]}`, x/y Figma-style: right, and
**down** from the parent's top-left). Generate `*_spec.json` with
`Docs/Scripts/figma_node_to_spec.py`.

---

## B. Resolved ambiguities — do these, do not re-litigate them

### B1. The `‹ BACK` row is NOT in any of the three Figma frames

`SPEC.md` § Figma Fidelity → *Shared* mandates a `‹ BACK` strip row. I pulled all three frames'
metadata: **none of them contains one.** On every frame `Content Container` (978×1860 at canvas
(96,361)) begins at `y=0` with the first real panel (Profile Hero / Avatar Stage / Collection Panel).

**Resolution — follow the hub exactly.** `GpsHubScreen` solved this same shape: its
`ContentContainer/BackRow` sits at `y=0 h=45` and its first panel starts at `y=65`
(`Docs/Specs/Completed/gps_hub_entry/reference/nodes/GpsHubScreen_geometry.json`). So:

- Insert `ContentContainer/BackRow` at `[10, 0, 958, 45]`, cloned from the hub's.
- **Add +65 to the `y` of every node-derived rect inside `Content Container`** on all three screens.
- Record the +65 in each `_geometry.json` `note` field and carry it into the invariants audit as the
  expected value, so `N sites 0 FAIL` is honest rather than tuned.
- Put one row in each screen's § Figma Fidelity table: *"BackRow — not in node; added per SPEC
  § Shared; all node y +65, matching `GpsHubScreen`" → `PASS*`*, and list it under § Spec deviations.

It fits on all three after the v1 hides:

| Screen | Last node element ends at | +65 | Container | Verdict |
|---|---|---|---|---|
| Profile | 1607 (Main Buttons `y=1487 h=120`) | 1672 | 1860 | fits |
| Avatar | 1566 (Status Panel, after Unlock Panel's 230+24 is removed) | 1631 | 1860 | fits |
| Badges | 1497 (Section SPECIAL `y=1264 h=233`) | 1562 | 1860 | fits |

### B2. The backend source is NOT in this repository — do not claim to have read it

`SPEC.md` cites `playlife/backend/routers/score.py:358-416`, `badges.py:57-80`, and
`backend/migrations/2026_06_29_points_atomic.sql:47-49`. **None of those paths exist in this repo.**
I checked: there is no `playlife/`, no `backend/`, and the only `migrations/` is
`Tools/admin-dashboard/migrations`. The local record for these endpoints is
[`Docs/GPS/GPS_INTEGRATION_REFERENCE.md`](../../../GPS/GPS_INTEGRATION_REFERENCE.md) — line 100
(`GET /score/stats`, `/score/history`), line 101 (`GET /badges/progress`), line 125 (the `profiles`
columns incl. `avatar_level`, `avatar_xp`), line 133 (`badge_definitions`: `id, name, category, rarity,
target_pct`, **24 seeded**).

**This is a Rule 6 trap.** Do NOT write "verified against `points_atomic.sql:47-49`" — that would be a
fabricated tool result and a CRITICAL FAIL. Instead:

- Implement the XP rule exactly as `SPEC.md` § Implementation 1 states it: `avatar_xp` is the
  **remainder within the current level**, `next = 500 × avatar_level`, `track = avatar_xp / (500 ×
  avatar_level)`, and level-up carries the remainder (`while xp >= level * 500`).
- Pin it with the EditMode test the spec asks for, and in the report cite **the test** as the evidence,
  with one line saying the SQL rule-of-record lives in the backend repo and was taken from `SPEC.md`
  rather than read here.
- The frame's `650 / 1,000 XP` is mock (Lv.12 needs 6 000). Do not reproduce it as real.
- If a live signed-in `/user/detail` disagrees with the 500×level rule, that is a genuine
  `ARCHITECT_REVIEW_ESCALATE` — surface it, don't silently pick one.

### C3 below applies to `/score/stats` and `/badges/progress` shapes for the same reason: the payload
shapes in `SPEC.md` § Reference are the contract you build against, and the **live signed-in Editor
run** is what verifies them. Quote the real response log lines in the report.

### B3. Figma px = Unity px, 1:1 — do NOT apply the ÷1.2 rule

`ScoreUploadScreenBuilder.cs:109` defines `static float F(float figmaPx) => figmaPx;` with the
reasoning in the doc-comment above it: the canvas is 1170×2532 at scale 1 and the frames are 1170
wide, so a Figma px IS a Unity px, and the ÷1.2 TMP conversion made every glyph 0.84× the reference.
Reuse `F()`. This overrides the general shell-canvas note in memory `feedback_shell_canvas_font_conversion`
for these three screens, exactly as it did for the score-upload screens.

### B4. `A()` / `ADark()` and the whole GPS palette already exist — move, don't copy

They live in `Assets/Scripts/UI/Gps/Editor/ScoreUploadScreenBuilder.cs:110-150`, together with the
tokens Build rules 1–3 are about: `Gold #EEDC9A`, `Green #7ED488`, `Muted #B7C3D3`,
`Navy70 = ADark("#091B33",0.70)`, and the navy-disc-in-gold-ring atoms
**`BadgeNavy #112D4F` / `BadgeInk #15365B` / `BadgeRing #B2A379`** (Build rule 3 — the badge grid's 60
rings and the evolution rings use these, NOT accent tints). The composited backdrops (`BgCard`,
`BgTrust`, `BgStrip`, …) are measured off the node renders and are the second argument `A()` needs.

`SPEC.md` § Implementation 2 permits hoisting these into a shared `GpsUiColor` static "if that is the
smaller diff — one owner". **Do that**: three new screens copying a 40-line palette is exactly the
duplication CLAUDE.md § Core Principles forbids. Add the new measured backdrops for these frames
(the avatar stage's green gradient, the badge cell fills) to the same file.

### B5. The hub's Profile nav slot is inside the "not wired" loop — lift it out

`GpsHubScreenController.cs:176-184` loops `_navButtons`, sets `interactable = false`, and adds a
`"nav {label} — not wired yet"` log. **`score_upload_flow` already established the correct fix** at
`:193+`: `_navCameraButton` is wired *outside* that loop precisely because the loop would disable it.
Do the same for a new `[SerializeField] Button _navProfileButton` — lift it out, set
`interactable = true`, `onClick → GpsProfile`. Do not special-case inside the loop.

### B6. Where the three ScreenIds get registered

Measured, so you don't have to hunt:

| What | File:line |
|---|---|
| `ScreenId` enum (`GpsHub,`) | `Assets/Scripts/UI/ScreenManager.cs:37` |
| Screen GameObject `SetActive` switch | `Assets/Scripts/UI/ScreenManager.cs:504` |
| `bool isGpsScreen = … GpsHub \|\| … ScoreUpload` — the `ShowTopBarOnly` group | `Assets/Scripts/UI/ScreenManager.cs:576` |
| `ShowTopBarOnly()` + `HighlightScreen(screenId)` call site | `Assets/Scripts/UI/ScreenManager.cs:593-601` |
| `NavTitleKeyFor` switch (`case … GpsHub: return "GPS_HUB_TITLE";`) | `Assets/Scripts/UI/PersistentUIManager.cs:515-537` |

Note the comment at `:597`: passing `screenId` (not a hardcoded `GpsHub`) is what lets each screen
carry its own title. All three new ids join `isGpsScreen` and get their own `NavTitleKeyFor` case.

### B7. Module service shape

Copy `Assets/Scripts/Gps/ScoreHistoryService.cs` — it is the smallest correct example in
`Golfin.Gps` (plain C# singleton, `Instance` / `ConfigureForTest` / `ResetForTest`, `ApiClient` does
the bearer auth, the `{data:…}` unwrap, retries and the 401 replay). `SPEC.md` says "`PointsService`
shape" for the **caching** half (`Last*` + `On*Changed`); `PointsService` itself lives in
`Golfin.Economy` and carries an offline queue you do **not** want for a read. So: `ScoreHistoryService`'s
skeleton + `PointsService`'s `Last*`/`On*Changed`/`ApplyX` caching idiom.

`Golfin.Gps.asmdef` references **only** `Golfin.Net` and has `"overrideReferences": true` with
`Newtonsoft.Json.dll` precompiled. Keep it that way — the module stays game-free, and the
`CharacterManager` / `RarityStatCaps` reads live in the `Assembly-CSharp` controllers per
`SPEC.md` § Architecture context.

⚠️ `CharacterManager` is in namespace **`Golfin.Roster`**, not global as `CLAUDE.md` claims
(memory `reference_charactermanager_namespace`). Resolving it by the global name returns null and
makes a test vacuously pass.

### B8. The avatar Status panel's game-side APIs — measured, and one of them is misnamed in the spec

`SPEC.md` § Reference and `CLAUDE.md` both say `RarityStatCaps.GetCap(rarity, statName)`. **That
overload does not exist as a static.** The real API is:

```csharp
// Assets/Scripts/UI/Roster/Data/RarityStatCaps.cs:76
int cap = RarityStatCaps.GetStatCap(rarity, "Strength");   // ← GetStatCap, not GetCap
// GetCap(string) exists, but only as an INSTANCE method on the nested StatCapData
// (:31), reached via RarityStatCaps.GetStatCaps(rarity).GetCap(statName).
```

The four `statName` strings are exact and case-sensitive (`:31-41`):
`"Strength"`, `"ClubControl"`, `"Recovery"`, `"Stamina"`. Anything else silently returns **0**, which
would render four full bars over a zero cap rather than failing loudly — check for it.

The whole read, mirroring `CharacterDetailPanel.cs:308` + `:342-345`:

```csharp
string id            = CharacterManager.Instance?.GetSelectedCharacterId();
var    csvData       = CharacterDatabaseCSV.Instance?.GetCharacter(id);
CharacterRarity rar  = csvData?.rarity ?? CharacterRarity.Common;   // :308 fallback
PlayerCharacterData p= /* CharacterManager's player instance for id */;
// values: p.currentStrength / p.currentClubControl / p.currentRecovery / p.currentStamina
// caps:   RarityStatCaps.GetStatCap(rar, "Strength") … etc.
```

`PlayerCharacterData` fields are `currentStrength`, `currentClubControl`, `currentRecovery`,
`currentStamina` (`PlayerCharacterData.cs:39-48`). **Do not use `currentStaminaEnergy`** (`:65`) — that
is the runtime energy meter that drives the red bar on the roster screen, a different quantity from
the Stamina *stat*, and confusing the two is a documented trap in `CLAUDE.md` § Roster UI.

The sprite resolution is `HomeScreenController.cs:232-250`, verbatim:
`CharacterManager.Instance?.GetSelectedCharacterId()` →
`CharacterDatabaseCSV.Instance?.GetCharacter(id)?.characterName` →
`Resources.Load<Sprite>($"Characters/Homescreen/{charName}")`, with the `Placeholder` fallback. Note it
early-returns on a null/empty id — your screen must render the `—` state there, not a blank stage.

**The four stat labels already have published keys** — reuse them, do not author new ones
(`Assets/Localization/LocalizationText.csv:80-83`): `ROSTER_STRENGTH`, `ROSTER_CLUB_CONTROL`,
`ROSTER_RECOVERY`, `ROSTER_STAMINA`. That is what `SPEC.md` § Implementation 6's closing sentence
("Character stat names reuse the existing roster keys") means, and it takes 4 rows off the ~75.

### B9. Atom inventory — every sprite Build rule 9 mandates EXISTS. Verified, with GUIDs.

I checked all of them so you cannot end up hand-rolling a flat fill because a "mandated source
wasn't found" (the `tournament_signup_modal` / `tournament_selection_screen` scar, Rule 19). Use these
for the `## Clone provenance` table — the GUIDs below are read from the `.meta` files on disk:

| Asset | GUID |
|---|---|
| `Assets/Art/UI/Gps/S_HUB_HeroPanel.png` | `0056adb4473b549fc9f5df5d654703d8` |
| `Assets/Art/UI/Gps/S_HUB_ActionTile.png` | `8f34803b9e42844948f1940ba94230bb` |
| `Assets/Art/UI/Gps/S_HUB_RoundsPanel.png` | `94902406f0cd64752ac0c730bc419bd6` |
| `Assets/Art/UI/Gps/S_HUB_GiftsPanel.png` | `cc74f12e82c674babba044347fcc559a` |
| `Assets/Art/UI/Gps/S_HUB_VotesPanel.png` | `ec4baa335309f4d28afce5e81d9714ab` |
| `Assets/Art/UI/Gps/S_HUB_StepsStrip.png` | `3d17a2501dec8499fac5973c8cc63d32` |
| `Assets/Art/UI/Gps/S_GpsIconRing_Step.png` (64) | `933c988c0f9114b7a884f11e28e963e2` |
| `Assets/Art/UI/Gps/S_GpsIconRing_Tile.png` (88) | `3a0df18ed75f247a2b3212acbab3a2bb` |
| `Assets/Art/UI/Gps/ICO_GpsStar.png` | `32f9bc29881cc48be9531b342f344d64` |
| `Assets/Art/UI/Gps/ICO_GpsHeart.png` | `8e9a4189bb5744e4ea3513edab26d695` |
| `Assets/Art/UI/Gps/ICO_GpsPin.png` | `3e60cd8963d4d41b493e80d87ace3f38` |
| `Assets/Art/UI/Gps/ICO_GpsSparkle.png` | `b7e1679a31d2642afb1b7c91df108863` |
| `Assets/Art/UI/Gps/ICO_GpsRounds.png` | `990e556cbbf4149eaa55c93aa3ad15ac` |
| `Assets/Art/UI/Gps/ICO_GpsGift.png` | `78f92b772971c48168f8bc3755d74d5d` |

That covers **every icon the three frames reference** (Star, Sparkle, Heart, Pin, Rounds, Gift) — no
new icon art is needed. `S_HUB_ActionTile` is the natural source for the Profile *Shortcuts* tiles and
the Badges *cells*; `S_HUB_RoundsPanel` for *Recent Rounds*; `S_HUB_HeroPanel` for *Profile Hero*.

**What genuinely has no atom yet** (→ `Docs/Scripts/make_gps_profile_panels.py`, in the style of
`make_gps_hub_panels.py` / `make_score_upload_panels.py` — **edit the script, never the PNG**):
the Avatar Stage's green gradient panel, the XP / Evolution / Status panels, the Quick-Stats tiles,
the two Gift-Totals tinted panels, the Badges Collection panel and the four Section panels. Add each
new sprite to `Docs/Architecture/UI_ELEMENT_PALETTE.md` § *Baked-from-tokens sprites* (the table at
`:39-49`) with its baker, per Build rule 9's last sentence.

⚠️ `UI_ELEMENT_PALETTE.md:26-32` — the `S_HUB_*` sprites **draw INSIDE their RectTransform** and bake
their drop shadow into the 9-slice border. That is what the `adj` field in the hub's geometry JSON
compensates for (`[-15.625, -7.8125, 31.25, 31.25]` on HeroPanel). Carry the same `inset_rule` into
your three geometry JSONs or every reused-panel rect will read as a FAIL against the node box.

---

## C. Standing traps that have bitten this exact area before

1. **Unity is shared with other sessions tonight.** Check `editor-application-get-state` before you
   enter play mode, and leave the Editor clean when you finish (exit play, no dirty scene,
   `feedback_leave_editor_clean`). If you find yourself fighting someone else's play-mode state, say
   so in `HEARTBEAT.log` rather than forcing it.
2. **Capture Rule 0.** Use `mcp__ai-game-developer__screenshot-game-view`; a hand-rolled
   `script-execute` into `CaptureCore`/`ScreenCapture` is hard-blocked by a PreToolUse hook. Navigate
   as a real user (tap PLAY, then real widget `onClick`) — `ShowScreen(target)` behind the title gate
   is a false positive. **Look at the PNG before surfacing it.**
3. **A screenshot taken over MCP reaches nobody but you and writes no file**
   (`feedback_never_reference_an_unsent_screenshot`). Save it into `screenshots/` and cite the path.
4. **Publish every new key** (`feedback_always_publish_new_text`). CSV → `Tools/content/import_content.py`
   PLAN then `--apply` → publish `texts` → `Tools/content/export_content.py --check` clean → regenerate
   the Unity table. A CSV-only key renders as the raw key on screen. `gps_hub_entry`'s report
   (`Docs/Specs/Completed/gps_hub_entry/IMPLEMENTER_REPORT.md:146`) is the worked example, including
   the `min_build` gotcha at `:283`.
5. **`Assets/Resources/Data/content_version.txt` is already modified in the working tree** at kickoff,
   as is `Docs/TellCode.md`, and `.claude/launch.json` + `Docs/Specs/Completed/ball_data_wiring/ARCHITECT_REVIEW.md`
   are untracked. Those are **not yours**. Capture them in your `=== iter-1 kickoff baseline … ===`
   HEARTBEAT block (Rule 13) and attribute them correctly rather than reporting them as your changes.
6. **Rule 15 (second defect of a shape ⇒ audit the shape).** Three screens built from one builder is
   a shape factory. The moment two defects rhyme, stop fixing instances — enumerate every site and
   publish a per-site verdict table including the ones that were fine.

---

## D. Open questions the orchestrator could not resolve

These are genuine — surface them rather than inventing an answer if they block you.

1. **`GIFTS SENT` and `AVG PUTTS` have no data source.** `SPEC.md` already rules both to `—`. Confirm
   against the live `/user/detail` payload that no `gift_sent_pts` / putts field exists; if one does,
   that is a spec correction worth escalating rather than shipping a `—` over real data.
2. **Badge `name` is JA in the seed.** The 24 `BADGE_{id}_NAME` rows take JA = the DB `name` and EN
   authored from `SPEC.md` § Implementation 6. You cannot read the seed migration locally (see B2), so
   take the JA strings from the **live `/badges/progress` response** on the signed-in Editor run, and
   say in the report that that is where they came from. Verify all 24 ids match the spec's list.
