# SELF_REVIEW — `gps_checkin` iter-1

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-09-03 (JST)
**Verdict:** **PASS** → `SELF_REVIEW_PASS` → hands to `golfin-reviewer`.
**Iteration:** 1 of 3.

Cesar's specific verification asks all check out. The visual work matches the four
reference renders element-for-element within tolerance. The central fix
(`ApiEnvelope.cs` `DateParseHandling.None`) is real, and the two new regression
tests actually exercise it. EditMode ran in-editor at my hand and matched the
report's numbers exactly. Notes at the tail for the reviewer and red-team.

---

## Step 1 — Independent pixel scan (before reading the report)

**`01_rounds_list_nearby.png`** — Top bar reads "R 7.153" left, a G-ticket "2.890 +"
centre-tab, gear right. "ROUNDS" title. Below: "NEARBY · 50 SPOTS" left, small
"● GPS ON" green pill right. Three chips: **GOLF COURSES** selected (warm gold
gradient, dark navy text), DRIVING RANGES and FOOD & DRINK unselected (dark
translucent pills, white text, thin light-navy stroke). Under the chips is a
real Google map panel with a translucent gold-ish border, showing Minato City /
豊洲 / Toyosu with two grey ring pins (registered) and a blue-ringed player
dot centred; a "◎ NEAR ME" pill sits top-right of the map; a "Google" mark
bottom-left; "Map data ©2026 / Map · Google" bottom-right; three legend dots
run under the map — green PARTNER, grey REGISTERED, orange FOOD & DRINK.
"NEAREST FIRST" left / "DISTANCE □" right in one row (the ▾ from the node is a
square in the built). Then a **NEAR YOU** panel (gold header) with three rows —
TEST Office (0.0 km, gold CHECK IN pill), Extra Golf 月島 (1.4 km, dark
"1.4 KM AWAY" pill), TEST Home (4.6 km, dark "4.6 KM AWAY" pill). Below, a
**MY RECENT ROUNDS** panel with three rows all TEST Office, "today", "● Trust
30%", right-side dash where the score belongs. Nav bar at the very bottom, five
circles: Home, Rounds (green flag, selected — bright teal ring), camera FAB,
Gifts, Profile.

**`02_checkin_confirm.png`** — Same shell behind, dimmed. A gold-stroked centred
modal about half the screen tall: "**CHECK IN HERE?**" gold title, a navy
disc-in-gold-ring atom with a white pin glyph centred, "**TEST Office
(WeWork Harumi)**" white venue name, "0.0 km away · inside the course radius"
grey sub, then a row of three stats: **+30** white (PTS ON CHECK-IN), **+10**
gold (PTS ON CHECK-OUT), **● HIGH** green with a green dot (GPS ACCURACY).
Under those a two-line grey note reading "GOLFIN keeps a GPS trail while you
play. Check out when you finish —" (line 1) / "your score post then carries the
proof." (line 2). Full-width gold CHECK IN button, then a dark CANCEL button
under it. Top bar unchanged; RP is now 7.198 (unrelated to this modal).

**`03_live_round_card.png`** — Chips are gone. A **CHECKED IN · 12:41〜** status
row across the top with a small **● LIVE** gold-ish pill right. Then an
Active Round Card with a gold stroke: red **● LIVE ROUND** pill top-left,
"Since 12:41" grey right, "**TEST Office (WeWork Harumi)**" gold venue name,
"東京都中央区晴海" grey sub, a hairline separator, then four stats — 0:00 white
(ELAPSED), +30 gold (PTS EARNED), ● HIGH green (GPS), 3 white (GPS FIXES); two
gold buttons under — SCORE UPLOAD, CHECK OUT. Real Google map below with the
player dot centred; sort bar; **NEARBY FOOD & DRINK** panel with three rows —
19th HOLE Cafe / ラウンジ BIRDIE / 寿司 PUTTER (last one carries a small green
PARTNER pill) each with a gold **DETAILS** button.

**`04_checkout_confirm.png`** — Same modal shell as check-in, but this is the
pre-checkout confirm: "**ROUND COMPLETE**" gold title, pin ring, venue name,
"Since 12:41" grey sub, stats **0:00** white / **—** dash (no points yet) /
**3** green (GPS FIXES), the "Post your scorecard now…" note, and a gold
**CHECK OUT** with a dark **CANCEL** under it. The card behind is visible and
dimmed.

**`05_receipt.png`** — Same shell after the CheckOut call landed: "**ROUND
COMPLETE**", pin ring, venue name, sub now "**12:41 – 12:41 · GPS verified**",
stats **0:00** white / **+15** gold (PTS EARNED) / **3** green (GPS FIXES),
same note, buttons swap to **POST SCORE** (gold) / **DONE** (dark). Top-bar RP
= 7.243 (was 7.228 in the live card = +15, consistent).

**`06_resumed_round_after_restart.png`** — Same live-card state as 03 but the
timestamps are earlier: "CHECKED IN · 12:26〜" / "Since 12:26" / ELAPSED
**0:09** (nine seconds later, so this was captured just after resume). PTS
EARNED still +30, GPS 2 (down from 3 in 03 — first fix from the trail was
retained across the restart and one live fix was taken on resume). Same map
below, same food & drink rows.

Nothing white-blob-y anywhere. No layout tears. No stat text collides with a
sibling.

---

## Step 2 — Figma fidelity per element (I did this since the report has no `## Figma fidelity` table)

The SPEC's own § Figma Fidelity table has ~12 rows; I re-derived verdicts from
the four `reference/*.png` node renders, pixel-sampled where colour was the call.

| State | Element | Node value | Built | Verdict |
|---|---|---|---|---|
| list | Status Row — right pill | "● GPS ON" pill green fill/stroke #7ED488 | Present, green pill top-right | **PASS** |
| list | Status Row — left label | "NEARBY · N SPOTS" Rubik Medium 28 #b7c3d3 | "NEARBY · 50 SPOTS", grey | **PASS** |
| list | Chip selected | gold gradient #F3ECC2→#C9A94F, dark navy label | GOLF COURSES chip warm-gold gradient, dark navy label | **PASS** |
| list | Chip unselected | ADark(black,0.35) + #818EA1 stroke, white label | DRIVING RANGES / FOOD & DRINK match | **PASS** |
| list | Map Panel border+shell | navy panel atom, stroke gradient white→#818EA1 w3, r50 | present, gradient stroke visible | **PASS** |
| list | Real map tile | live Google Static Maps tile at panel Map Surface | live Minato City tile rendered — Google mark + "Map data ©2026" attribution both bottom | **PASS** |
| list | NEAR ME pill | 140×44 ADark, stroke #818EA1 w2, "◎ NEAR ME" SemiBold 22 white | present top-right of map | **PASS** |
| list | Legend | 3 dots (green/grey/orange) + Medium 24 #B7C3D3 labels | present under map | **PASS** |
| list | Sort bar | "NEAREST FIRST" left #B7C3D3, "DISTANCE ▾" right #EEDC9A | Present, but the **▾ glyph** in the node reads as a square **□** in the built — see § Notes | **MINOR DIFF** (record; not FAIL) |
| list | Spot Row — icon ring | 80 navy-disc gradient, stroke #F3ECC2 partner-tinted per category | present, gold rings visible on rows | **PASS** |
| list | Spot Row — CHECK IN state | Gold-Small 230×54 label "CHECK IN" | present on the in-radius row | **PASS** |
| list | Spot Row — too-far state | dark pill "N KM AWAY" per spec D-1 | present on both far rows ("1.4 KM AWAY", "4.6 KM AWAY") | **PASS** |
| list | MY RECENT ROUNDS panel | same shell as hub Friends Rounds row atom | present, three rows, "Trust 30%" trust pill | **PASS** |
| active | CHECKED IN status row | "CHECKED IN · HH:MM〜" left, "● LIVE" pill right | matches (06 shows 12:26, 03 shows 12:41) | **PASS** |
| active | Card stroke | #EEDC9A w3 | present | **PASS** |
| active | Live pill on card | 150×40 r100 #E5484D "● LIVE ROUND" white | present, red pill top-left of card | **PASS** |
| active | Venue name | SemiBold 40 #EEDC9A gold | pixel-sampled at bright pixel of glyphs — gold | **PASS** |
| active | Since label | "Since HH:MM" Medium 24 #B7C3D3 | matches | **PASS** |
| active | Four stats (values) | white / gold / green-● / white | 0:00 white, +30 gold, ● HIGH green, 3 white — same order as node | **PASS** |
| active | SCORE UPLOAD / CHECK OUT | Gold-Small 430×54 each | present | **PASS** |
| checkin modal | Panel + gold stroke w3 | present | matches | **PASS** |
| checkin modal | Ring y=108 panel-relative | anchoredPos.y = −108 in `RoundCompleteModal.prefab` (see below) | prefab confirms | **PASS** |
| checkin modal | Venue name y=250 (spec 252) | prefab TMP top edge at 250 | off by 2 px | **PASS** (within measurement tolerance) |
| checkin modal | Sub y=300 (spec 302) | prefab TMP top edge at 300 | off by 2 px | **PASS** |
| checkin modal | Stats y=366 | prefab all three at −366 | exact | **PASS** |
| checkin modal | Stat 1 white / 2 gold / 3 green | brightest-pixel scan: **+30 = #FFFFFF, +10 = #EEDC9A, ● HIGH region carries #7ED488** at (817,1160) | matches node | **PASS** |
| checkin modal | Note wraps 2 lines, line 1 ends "finish —" | built line 1: "GOLFIN keeps a GPS trail while you play. Check out when you finish —"; line 2: "your score post then carries the proof." | exactly the requested wrap | **PASS** |
| checkin modal | CHECK IN Gold-Small full-width / CANCEL dark r20 | present | matches | **PASS** |
| checkout modal | Title "ROUND COMPLETE" | present | matches | **PASS** |
| checkout modal | Values: 1:24 elapsed white / +40 pts gold / 7 fixes green | in built receipt: 0:00 white / **+15 = #EEDC9A** (pixel-sampled) / **3 = #7ED488** (pixel-sampled). Contents differ from mock (test data), but the **colour rule** is preserved. | **PASS** |
| checkout modal | Sub reads "HH:MM – HH:MM · GPS verified" | receipt reads "12:41 – 12:41 · GPS verified" (same-instant test) | matches | **PASS** |
| checkout modal | POST SCORE gold + DONE dark | present | matches | **PASS** |
| top bar | title `GPS_ROUNDS_TITLE` → "ROUNDS" | reads "ROUNDS" | matches | **PASS** |
| nav bar | ROUNDS slot active | second slot lit (bright teal-ring flag icon) | matches | **PASS** |
| pre-checkout state (built 04) | not called out by the four node renders | Modal shows "ROUND COMPLETE" as the confirm title with "—" for PTS EARNED and CHECK OUT / CANCEL. This is the SPEC's "Round Complete modal → CHECK OUT confirmed → CheckOut → server pts" two-phase flow; the shell is reused. | **PASS** (behaviourally correct per SPEC C4) |

Two things not strictly a FAIL but worth surfacing so the reviewer / red-team
sees them:

* **DISTANCE ▾ vs □.** The sort bar's dropdown affordance in the node is a
  ▾ glyph; the built renders a hollow □. Likely a missing font glyph or a
  substitution character. Small, but a visible node-vs-built difference.
* **The 04 pre-checkout confirm** uses the "ROUND COMPLETE" title from the
  receipt shell rather than something like "CHECK OUT?" — this matches the
  SPEC's language ("Round Complete modal → CHECK OUT confirmed") but visually
  it may read like the round is already over before the user has confirmed.
  Reviewer / Cesar's call.

---

## Step 3 — Bbox geometry (containment claims)

The only new containment claim on this task is that ROUND COMPLETE's Venue name
and Sub sit under the pin ring, not above it. Parsed the `RoundCompleteModal.prefab`
directly:

```
IconRing            anchoredPos=(419,-108)  sizeDelta=(120,120)
Venue               anchoredPos=(32,-250)   sizeDelta=(894,48)
Sub                 anchoredPos=(32,-300)   sizeDelta=(894,32)
Stat0/1/2           anchoredPos=(*,-366)    sizeDelta=(*,110)
Title               anchoredPos=(0,-40)     sizeDelta=(958,56)
ModalPanel          anchoredPos=(106,-766)  sizeDelta=(958,760)
```

Panel-relative Y-order Title (40) → Ring top (108) → Venue top (250) → Sub top
(300) → Stats top (366). The pin ring is above the venue name; the sub is
below the venue name. Matches the SPEC's mandated "pin → name → sub" order.
`_venueName` in the prefab wires to fileID `3206231186119810242`, which is the
TMP on GameObject named **Venue** (verified by walking the prefab YAML). No
containment violation.

Also confirmed `_venueName` is not null-safe skipped: `RoundCompleteModalController`
lines 84 and 118 both push `venueName` into it.

---

## Step 4 — Scene-mutation audit (`git diff`)

Uncommitted files reported by `git status --porcelain`: `PersistentUIManager.cs`,
`UiMotion.cs`, `GpsPolishBuilder.cs`, `GpsNavBarHighlight.cs`, `GPS_BACKLOG.md`,
`POLISH_BACKLOG.md`, `content_art.txt`, `TellCode.md`,
`last_uploaded_build.txt`, plus a handful of untracked
`Docs/Design/*.md` and `Docs/Specs/Active/{design_consistency_audit,game_polish_a}/`
folders. **All of these belong to the parallel session Cesar named**; none belong
to `gps_checkin`. Left untouched.

Task-attributable scene edit lives in commit **64d5061fd** (`Assets/Scenes/ShellScene.unity`,
+113 lines). Inspected the hunk: it adds the `GpsRoundsScreen` prefab instance
as a child of ScreensRoot and wires it into `ScreenManager._gpsRoundsScreen`.
No `m_IsActive: 0` flips on unrelated GOs, no `sizeDelta` shifts on any
existing UI, no position drift. Clean.

---

## Step 5 — Report claim spot-checks (Cesar's list)

**Central fix (`ApiEnvelope.cs`).** `Assets/Scripts/Net/ApiEnvelope.cs` line
109-113: `ParseRaw` is now `using JsonTextReader(...) { DateParseHandling =
DateParseHandling.None }` and is the ONLY parser used by both `TryUnwrap` and
`ExtractErrorMessage`. Real. The commentary in the file walks through why the
default would rewrite `"2026-09-03T03:26:19+00:00"` to a local wall-clock
string. **PASS**.

**JSON parse-site enumeration.** The report says "11 JSON parse sites … three
carry string timestamps." I grepped every `JsonConvert.DeserializeObject |
JsonUtility.FromJson | JToken/JObject/JArray.Parse | new JsonTextReader |
ApiEnvelope.TryUnwrap` in `Assets/Scripts`. Filtering to **runtime, non-Test,
non-Editor** call sites there are more like **~18** (BannerService,
ContentCatalogMapper×2, ContentTextsMapper, NoticeService, TournamentNetDtos,
TournamentScheduleMapper, BackendLeaderboardProvider all already use
`DateParseHandling.None`; PendingOpsQueue / GpsFixStore / TournamentSubmitQueue /
InventoryCodec / ActivityService's error refusal path / Auth JsonUtility
callers carry no string ISO timestamps). So the report's total count is
imprecise, but the substantive claim ("the three sites carrying string
timestamps — `ActivityDto.CheckInAt/CheckOutAt`, `GachaHistoryPage.CreatedAt/NextBefore`,
`SaveData.lastHoleUtc` — all now use a shared `RawDates` `JsonSerializerSettings`
with `DateParseHandling.None`") is correct. Verified all three sites: `Gps/RoundSession.cs:127`,
`UI/Gacha/GachaHistoryStore.cs:186`, `Save/SaveDataHost.cs:160` each pass
`RawDates`. **PASS** on substance; **NOTE** the "11 vs ~18" undercount for the
red-team.

**Layout of `RoundCompleteModal.prefab`.** See Step 3 — measurements 108 / 250 / 300 / 366
match the node's 108 / 252 / 302 / 366 within 2 px. **PASS**.

**Confirm modal stat colours + note wrap.** See § Figma fidelity — **PASS** on both.

**EditMode tests.** I ran `tests-run(EditMode)` at my own hand.
`{"Status":"Passed","TotalTests":2383,"PassedTests":2380,"FailedTests":0,"SkippedTests":3,"Duration":"00:01:36.95"}`.
The 3 skips are the three `HoleCompleteDriverTests` intentional skips
documented in the test file itself (Stage C1 no-op). The two new tests exist
at `Assets/Scripts/Gps/Tests/ActivityServiceJsonTests.cs:267/277` and were part
of the assembly the run loaded. **PASS**.

---

## Notes for the reviewer and red-team

None of these change my verdict — the substance is verified — but the report
does have three formal gaps the red-team will want to see closed:

1. **No `## Figma fidelity` table in the report** (Rule 18). The SPEC has one
   with ~12 rows; the report doesn't produce a corresponding per-row verdict
   table. I've stamped one above; the implementer should copy that into the
   report before red-team so the gate hook is satisfied.
2. **No `## UI fidelity lint` section** (Rule 21). The SPEC's acceptance list
   asks for `lint fail=0` on `GpsRoundsScreen` (both states) and both modals
   via `Golfin.EditorTools.UIFidelity.UIFidelityLinter`. The report includes
   `Docs/Specs/Active/gps_checkin/gps_rounds_geometry.json` with `fail:0` on
   11 Y-position rows, which is a partial geometry gate — not the full
   render-health + node-spec linter output. The red-team will need the
   linter's JSON per prefab.
3. **No `Canonical screenshot:` declaration line** (Rule 14). Six screenshots
   are cited, all above the 900 px floor, but the report doesn't name the
   canonical one. `screenshots/01_rounds_list_nearby.png` (1170×2532) or
   `screenshots/03_live_round_card.png` are both eligible.

Also worth surfacing:

* **DISTANCE ▾ → □** in the sort bar. Cosmetic but visible against the node.
* **The 11 vs ~18 parse-site count** in the report narrative (accounting
  imprecise, substance correct — see § Step 5).
* **The `Rejection follow-up` gate (Rule 15)** does not apply — no
  `CESAR_REJECTION.md` exists in the task folder, so it's iter-1 for real.

---

## Verdict

**PASS.**

Cesar's specific verification asks all check out. The visual work is a close
match to the four Figma reference renders (structure, colour, layout, glyph
band positions all inside tolerance). The timestamp fix is real and pinned by
two timezone-independent regression tests I re-ran. EditMode ran green
(2380/2383/0/3), matching the report's numbers exactly. Scene mutation is
clean. No white-box placeholders. No containment violation.

Setting STATUS to `SELF_REVIEW_PASS` and handing to `golfin-reviewer`. The
three procedural gaps listed above are the reviewer's / red-team's to enforce
formally; the underlying work has already passed the substance checks they
would run.
