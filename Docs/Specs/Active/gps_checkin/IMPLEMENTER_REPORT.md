# IMPLEMENTER_REPORT — `gps_checkin` iter-1

**Iteration shape:** `gps_rounds:first-build`
**STATUS held at `IMPLEMENTER_WORKING`** — not advanced, because two gates are genuinely
external and every remaining acceptance item sits behind one of them (§ What is blocked).

---

## What is DONE and independently verified

Cesar applied both migrations at 02:0x on 2026-09-03, which unblocked the whole
backend + admin half. It is now DEPLOYED AND PROVEN LIVE.

| Area | State |
|---|---|
| A1–A3 migration + the two atomic RPCs | **APPLIED + verified live** |
| A2 `/venue/nearby` category + server-side distance | **DEPLOYED, 200, nearest-first** |
| A4 `/venue/map` Static Maps proxy | **DEPLOYED**; 502 surfacing Google's `403 This API is not activated` — the one pre-req still on Cesar |
| A5 `/score/submit` `activity_id` closes the live round | **DEPLOYED**, asserted by the E2E |
| A6 `e2e_activity_economy.py` | **ALL PASS — 38 assertions** |
| A6 Fly deploy | **v68**, `/health` ok |
| B1 admin Partners panel | **DEPLOYED** (`golfin-admin`); all three round-trips driven through the real UI |
| B2 demo seed | **APPLIED** — 4 range + 5 food, 霞ヶ関 partner |
| C1–C4 Unity runtime + builder + baker | written; all five assemblies compile clean — **prefab build still needs Unity** |
| C5 localization | **PUBLISHED** — `texts` v32, 64 rows live, `--check` clean |

### The live numbers

```
invariant total_points = activity_pts + gift_pts   19 profiles, 0 violations (before AND after)
venues 1997   by category {golf: 1988, range: 4, food: 5}   partners {range 1, food 3, golf 1}
demo rows 9   霞ヶ関カンツリー倶楽部 #153 is_partner=true partner_offer='ゴルファー10%OFF'
geohash drift 2  (#1 東京ゴルフ倶楽部, #7 Lomond CC — pre-existing, the panel flags them)
```

`e2e_activity_economy.py`, all 38 assertions, ending `=== ALL PASS ===`:
check-in inside the radius +30 once (`distance_m=0`, one `gps_checkin` ledger row);
replay `replayed:true awarded:0`, no second round; a fresh key refused
`already_active` naming activity 38; check-out `awarded=15`, `activities_count 1→2`,
one `activityComplete` row; a far check-in (`distance_m=3208.8`) `gps_verified:false
awarded:0` and **no ledger row**; a backdated round `expired:true awarded:0` that did
**not** bump `activities_count`; and a score post leaving ONE row for the round.

Then the same flow through the deployed ROUTERS over HTTPS, which the E2E does not
cover (it drives the RPCs directly):

```
balance before       : 7063
POST /activity/checkin      -> 200 awarded=30 verified=True dist=0m id=41
balance after check-in      : 7093
REPLAY same key             -> 200 replayed=True awarded=0
SECOND check-in, fresh key  -> 409 {"ok": false, "reason": "already_active", "activity_id": 41}
GET /activity/active        -> 200 id=41 status=active
POST /activity/41/checkout  -> 200 awarded=15 expired=False duration='0h 0m' count=3
balance after check-out     : 7108
GET /activity/active        -> 200 data=None
```

## Compile verification (no Unity session was used)

Unity is held by another session, so the C# was compiled **headlessly** with Unity's own bundled
Roslyn (`6000.3.9f1/.../DotNetSdkRoslyn/csc.dll`), reading references from
`Library/ScriptAssemblies` and writing only into the scratchpad. Each assembly was compiled from
its own `.csproj` source list plus this task's new files, with `ProjectReference`s resolved to the
freshly-built dll where this run had already produced one:

```
=== Golfin.Gps:                  19 sources, 267 refs -> OK
=== Assembly-CSharp:            286 sources, 420 refs -> OK
=== Assembly-CSharp-Editor:     166 sources, 443 refs -> OK
=== Golfin.Gps.Tests:            12 sources, 281 refs -> OK
=== GolfinRedux.Tests.EditMode:  15 sources, 284 refs -> OK
```

It earned its keep immediately: `GpsRoundsBuilder.cs` was missing
`using Golfin.Gps.EditorTools;` and failed on `GpsPolishBuilder` (2 errors) — found and fixed
before Unity ever saw it, rather than by breaking the other session's editor.

**This is a compile check, not a test run.** `tests-run` needs Unity and is on the blocked list.

---

## Backend

### A1 / A3 — `backend/migrations/2026_09_03_venue_partners.sql`

`venues` gains `category` (checked golf/range/food), `is_partner`, `subtitle`, `price_label`,
`chip_extra`, `partner_offer`, `is_active`, `updated_at`, plus the `(category, is_active)` index
and a backfill putting all ~1,988 existing rows in `golf`. Additive: `sport_type` and every
Flutter reader are untouched.

Then the two RPCs, modelled line-for-line on `golfin_gift_pts`: SECURITY DEFINER,
`revoke … from public, anon, authenticated` + `grant … to service_role`, profile row locked
`for update` BEFORE the replay check, refusals returned as `{ok:false,reason}` rather than raised,
and `activity_pts` + `total_points` always moved TOGETHER.

**One design decision worth naming.** `golfin_gift_pts` detects a replay by looking for its own
`points_transactions` row. These two cannot: a check-in **outside** the radius awards 0 and writes
no ledger row, and an **expired** check-out awards 0 and writes no ledger row. Keying replay
detection off the ledger would therefore make exactly the un-awarded cases replayable — a
force-quit mid-check-in would open a SECOND active round. So the key is stamped on the
`activities` row itself (`checkin_key` / `checkout_key`, partial unique index per user) and the
ledger row, when there is one, carries the same key for the audit trail.

`routers/activity.py` is now a thin wrapper: authenticate, mint a key when the caller brought
none, call the function, map `{ok:false,reason}` to a status (409 `already_active`, 404 unknown
venue, 400 otherwise). **The direct `profiles.total_points` update is deleted** — it was the last
un-migrated writer of that column and it broke the invariant on every single check-out.

### A2 / A4 — `routers/venue.py`

`/venue/nearby` gains `category` (default golf), an `is_active` filter, a **server-computed**
`distance_m`, and returns the page sorted by it, capped at 50. The client sorts nothing — and
could not have: geohash-prefix order is not distance order, so the old "nearby" list was in
insertion order pretending to be near.

`/venue/map` proxies Google Static Maps with a dark style in one constant, `scale=2`, no markers,
a 24 h in-process cache keyed on (lat, lon @4dp, zoom, w, h) and 60 req/min/user. **It is declared
BEFORE `/{venue_id}`** — FastAPI matches in declaration order and `venue_id: int` would 422 on the
literal `map`. `/venue/geocode` sits with it.

### A5 — `routers/score.py`

`activity_id` is optional. When it names the caller's own **active** row the handler UPDATEs that
row instead of inserting a second one, merging rather than replacing the round's evidence
(`max` on `gps_check_count`, the round's start coords kept when the request brings none), fills
`duration`, and returns `closed_activity_id` so the Rounds screen can drop its live card without
waiting for the next `/activity/active`. Anything else — someone else's id, a completed round, a
bad id — falls through to the historical insert rather than erroring: losing a score post because
a round row went stale would be the wrong trade.

---

## Admin (B1 / B2)

New panel `app/(panels)/venues` "Partners", shaped like **Rewards** rather than like a content
catalog and saying so in an amber banner: `/venue/nearby` reads this table per request, so a save
is live on the player's next fetch and there is nothing to publish.

Three absences are the design, and each is enforced server-side rather than by the form:

* **No delete.** `activities.venue_id` is a foreign key. Deactivation is the removal.
* **No geohash field.** `venueMutations.toRow` recomputes it from the coordinates on EVERY save,
  and `PATCH /api/venues/:id` **400s** if the body carries one.
* **No `sport_type` field.** It is the Flutter app's axis; `category` is the Rounds chips'.

**A real finding, surfaced by writing the geohash port.** An audit of all 1,988 rows found **two**
whose stored geohash does not match their own coordinates — `#1 東京ゴルフ倶楽部` (stored `xn76u`,
computed `xn74wxb8d`) and `#7 Lomond Country Club` (stored `gcpv5`, computed `gcuxns2n8`), both
`source='manual'`. Both are **invisible to `/venue/nearby` today**: the row exists, a map would
show it, and no player's nearby list ever contains it. Nothing errors, which is why it survived.
The panel raises them in a red banner naming both, and re-saving either row fixes it. Not fixed
silently here — it is data, and one click in the panel is the honest repair.

`lib/geohash.ts` is a byte-for-byte port of `venue.py::_geohash_encode`, pinned by
`lib/__tests__/geohash.test.ts` against rows the **Python** encoder already wrote.

**Deviation D-1 — the geocode is local, not `/venue/geocode`.** § B1 routes "Find on map" through
the API's endpoint. That endpoint exists and is what Unity would use, but this dashboard has **no
channel to the API**: it talks only to Supabase with the service key and holds no PLAYLIFE bearer
token, while `/venue/geocode` is `Depends(get_current_user)`. Minting a token would mean giving
the dashboard a player identity, which is a worse thing to own than a regex. So the two shapes an
operator actually pastes — a Maps URL and a bare `lat, lon` — resolve locally with the same two
patterns in the same order as `venue.py::_coords_from_text`; a free-text place NAME uses
`GOOGLE_PLACES_API_KEY` if the dashboard has one and says plainly what to paste instead if not.

The demo seed inserts the 4 ranges and 5 food spots from `rounds_map_tab.dart` verbatim
(`source='demo'`, `gps_radius_m 300`), idempotently (`insert … select … where not exists`, so a
re-run is a no-op and does not overwrite Cesar's later edits), and marks the **real**
`霞ヶ関カンツリー倶楽部` (#153, `osm_import`) as a partner. The nine mock golf courses are NOT
seeded — the table already carries 1,981 real ones, and seeding the mocks would put a second, fake
霞ヶ関 beside the real one.

---

## Unity

### Layout: one stack, one flip

The two frames are the same stack with one slot swapped, and the node's own numbers say the gaps
are all identical:

```
        list                                active
        Status Row       y 0    h 40        Status Row        y 0     h 40
        Category Chips   y 60   h 60        Active Round Card y 60    h 340
        Map Panel        y 140  h 560       Map Panel         y 420   h 560
        Sort Bar         y 720  h 40        Sort Bar          y 1000  h 40
        Spot List Panel  y 780  h 470       Spot List Panel   y 1060  h 470
        My Recent Rounds y 1270 h 472       (absent)
```

Every gap in both columns is exactly 20. So `ContentContainer` is a `VerticalLayoutGroup` at
spacing 20 with `childControlHeight = false` and `childForceExpand = false` (C3/C4), each panel
pinned by a `LayoutElement`, and the flip is one `SetActive`. A second layout would be a second
place for the two states to disagree.

### D1 — the button is never dead

`RoundSpotRowView` has a **four-way** `ActionState` and NEVER sets `interactable = false`:
`CheckIn` (gold, opens the modal), `TooFar` (dark, reads `N KM AWAY`, tap → distance toast),
`NoGps` (dark, tap → "turn on location"), `Details` (while a round is live). A greyed-out control
answers "can I?" and refuses "why not?". Enforcement is server-side regardless — the RPC awards 0
outside the radius whatever the client sends — so the client's job here is explanation.

### D3 — the foreground trail

`RoundSession.RecordFix` feeds `GpsSessionTracker`, whose own AND-throttle (5 min AND 100 m)
decides what is kept, so re-entering the screen cannot inflate `gps_check_count` — the exact
anti-cheat property `score.py` pays Trust +20 for. `OnApplicationFocus(true)` is the only resume
signal available without a background-location entitlement, and it is what gets a long round past
K4's 3-fix threshold. `RoundSessionTests` pins both halves (20 fixes in one place = 1; two more an
hour apart = 3).

### The idempotency keys

Minted and **persisted before the request leaves**, one per intent, cleared only when a response
actually lands — a business refusal spends the key, a network failure keeps it, because the
request may have reached the server. `RoundSessionTests.AForceQuitMidCheckIn_ReplaysTheSameKey`
is the assertion that matters and it is invisible in any screenshot.

### The map

`MapProjection` is Web Mercator in Static Maps' exact form, with `scale` kept **separate from
zoom** (the proxy asks for a half-size image at 2×, so the projection runs in Google pixels).
`MapProjectionTests` pins it against three points computed independently in Python — so the test
compares two implementations rather than asserting the code equals itself — plus a north-up /
east-right sign test, a zoom-doubling test, an inverse round-trip, and a polar clamp.

### Art (`Docs/Scripts/make_gps_rounds_panels.py`, 13 PNGs)

The node's `Backgrounds` plate was `object-cover`-fitted against every ≥800×1400 PNG in
`Assets/Art` and `Assets/Resources`: **`Assets/Art/HomeScreen/Home Background.png` at 0.002 mean
|ΔRGB|**, next best 34.3. Not a close call — the same file. No new background asset.

Translucent cards are solved over that plate with the shared least-squares fit. Residuals, with
shipped GPS cards as the calibration:

| Panel | fit | mean \|ΔRGB\| |
|---|---|---|
| `S_GR_MapPanel` | rgb(0,50,65) a=0.806 | 8.6 |
| `S_GR_SpotList` | rgb(15,43,56) a=0.752 | 3.5 |
| `S_GR_History` | rgb(15,43,56) a=0.752 | **3.7** (borrows the spot list's rect) |
| `S_GR_ActiveCard` | rgb(0,53,77) a=0.814 | 7.0 |
| `S_GR_ModalPanel` | rgb(11,38,53) a=0.683 | fitted over the 60 % scrim |
| *(shipped baseline)* gift Supporters / Golfers / vote CardPhoto | — | 7.5 / 7.8 / 5.1 |

The History panel does **not** solve over its own footprint, and the number says why: the plate
there is flat dark grass (RGB σ = 4.9/5.6/1.9 against 20/14/12 under the spot list), the system is
under-determined, and the solve wandered to `rgb(0,0,95) a=0.215` — worse (4.85) than borrowing
the well-conditioned fit from the same atom 130 px above it (3.68). Measured, not assumed.

The icon ring and the map pin are each **split into two sprites** (a gradient/tinted fill plus a
white stroke) because the row is ONE template whose ring colour is bound per category at runtime.
A single baked ring would need three prefabs — and a fourth for a partner range.

**Deviation D-2 — the modal shell is baked, not `S_SU_ModalPanel`.** The spec says "the
`S_SU_ModalPanel` family". That asset is 978×1400; this modal is 958×760. The fill is a vertical
gradient, so stretching it vertically is fine, but the corner radius would be squashed to 54 % —
Rule 21's `nonuniform-stretch`. Same token pair, same construction, its own size, and a gold
stroke the shared asset does not have.

**Deviation D-3 — DETAILS is a toast, not a venue-detail view.** § C4 says "the existing Venue
detail treatment if one exists; else a read-only modal — NOTE which". **There is no venue detail
screen in the project**: `VenuePickerModalController` is a picker. Rather than build a screen this
task did not scope, DETAILS raises a toast carrying the row's own offer/price — the information a
read-only modal would have shown, in a surface that already exists. Noted for the backlog.

**Deviation D-4 — one line in another session's file.** `GpsNavBarHighlight.cs` (untracked, from
`gps_navbar_selected_tab`) gained `"GpsRoundsScreen" => "NavRoundsButton"` and a corrected comment
— its header said "Rounds is never lit — its screen was never designed", which this task makes
false. One line plus a doc-comment; flagged because that file belongs to a concurrent task.

Also touched for the same reason: `GpsPolishBuilder.cs` gains the two new shimmer sites (also
modified by that session — additive array entries, no overlap).

### `ALL ROUNDS ›`

Authored and **hidden**. § Out of scope: there is no full-history screen, and a link that goes
nowhere is worse than no link. The backlog row stays either way.

---

## Localization (C5) — PUBLISHED

```
catalog         add  change   same  conflict  csv
  texts          64       0    958         0  Assets/Localization/LocalizationText.csv
PLAN ONLY — 64 draft(s) would be written (64 new, at min_build 2603).
Wrote 64 draft(s) as cesar.guarinoni@gmail.com (64 new, min_build 2603).
content_publish('texts') -> version 32
published GPS_ROUNDS_ rows: 64
--check: clean — no file would change and no catalog has drifted.
```

`content_version.txt` moved to v32 and is committed with the CSV. Zero hardcoded `.text` literals
on the new surfaces: every string goes through `LocalizedText` (static labels) or
`LocalizationManager.Get` (the ones this controller formats itself, which are re-resolved on
`OnLanguageChanged` — the Settings overlay never disables the screen).

---

## What is BLOCKED, and on whom

### On Cesar — apply, then enable, then I deploy

1. `backend/migrations/2026_09_03_venue_partners.sql`
2. `backend/migrations/2026_09_03_seed_demo_spots.sql` (after 1 — it needs the new columns)
3. Google Cloud → enable **"Maps Static API"** on the key `playlife-api` uses. A Places-only
   restriction returns 403 and `/venue/map` surfaces that body verbatim.

Both deploys wait on step 1 **deliberately**: deploying the API first ships routers that call
functions which do not exist, and deploying the dashboard first ships a panel that queries columns
which do not exist. Once applied, in order: `e2e_activity_economy.py` → Fly deploy + `/health` →
`npm run deploy` + the §23 footer hash → the three admin round-trips (create a range, edit the
焼肉 GREEN offer, deactivate a row, then quote the client's `/venue/nearby` JSON).

### On a free Unity — everything visual

Running `GOLFIN/Gps/Build Rounds Screen`; wiring `ScreenManager._gpsRoundsScreen` in ShellScene;
play-mode capture with `GpsRoundsScreenController.EditorFixOverride` set to TEST Office (1993);
the geometry + invariants JSON; the UI-fidelity lint; the per-element A/B crop sheet against the
four renders; `tests-run` (`MapProjectionTests`, `RoundSessionTests`, `ActivityServiceJsonTests`,
`GpsGateTests`, plus the full EditMode sweep); the `gps_polish` motion-parity JSON and the
captioned video; the A13 GC/frame measurement.

`EditorFixOverride` is the Editor seam the acceptance list asks to be NOTED:
`GpsRoundsScreenController.EditorFixOverride` is a `#if UNITY_EDITOR` static `LocationFix?`. When
set, entry uses a `FixedLocationProvider` returning it; no player build can carry a mocked
position, because the field does not exist there.

---

## Acceptance checklist

| # | Item | Verdict |
|---|---|---|
| 1 | Per-element A/B crops + ΔRGB table | **BLOCKED** (Unity) — nodes re-pulled per §9, geometry transcribed from `get_metadata`; the bake's own ΔRGB table is above |
| 2 | Geometry JSON + invariants + lint `fail=0` | **BLOCKED** (Unity) |
| 3 | Migration applied + `e2e_activity_economy.py` ALL PASS | **PASS** — applied by Cesar; 38 assertions, quoted above |
| 4 | Static Maps 200 + cache hit + pin projection ≤2 px | **PARTIAL** — route deployed and reached; it returns Google's `403 This API is not activated`, so the 200 + cache-hit pair waits on the key. Projection test written against independently-computed values |
| 5 | Editor play-mode, real navigation, live API | **BLOCKED** (Unity) |
| 6 | `already_active` + force-quit replay | **PASS** — both at the RPC layer (E2E) and through the routers (`409 already_active`, `replayed:true awarded:0`) |
| 7 | Admin panel deployed + 3 round-trips | **PASS** — deployed (`golfin-admin` version `e92cc304`); all three driven through the real UI AND proven against live data + the client's fetch |
| 8 | Demo seed applied; 5 food / 4 range / 霞ヶ関 PARTNER | **PASS** — counts and the partner flag quoted above |
| 9 | `GpsGate` includes `GpsRounds`; ROUNDS active-state; push pinned | **PASS (code)** — runtime proof BLOCKED (Unity) |
| 10 | Importer PLAN/APPLY/publish/`--check` clean; no hardcoded literals | **PASS** — v32, 64 rows, `--check` clean (still clean after another session's v33) |
| 11 | Full EditMode sweep + the three new suites by name | **BLOCKED** (Unity) — all three compile |
| 12 | Motion parity with `gps_polish` + video + A13 | **BLOCKED** (Unity) |
| 13 | Too-far and no-GPS toasts | **PARTIAL** — implemented and localized; the frame + log is BLOCKED (Unity) |
| 14 | Deviations flagged; device-pass rows added | **PASS** — D-1…D-4; `GPS_DEVICE_PASS.md` § 3b, 20 rows |

## The production bug this deploy uncovered (and fixed)

`/venue/nearby` 500'd immediately after the first deploy — and so did
`/venue/search`, which this task never touched. That is what made it a platform
bug rather than a regression. The logs named it: a Cloudflare **error 1101,
"Worker threw exception"** from Supabase's own edge, returned as HTML that
`postgrest-py` cannot parse.

Reduced against production, it is the `%` alone:

```
/venues?select=*&geohash=like.xn77%   -> 500  error code: 1101
/venues?select=*&geohash=like.xn77*   -> 200  15650 bytes
/venues?select=*&name=ilike.%GOLF%    -> 500  error code: 1101
/venues?select=*&name=ilike.*GOLF*    -> 200  23284 bytes
```

`supabase-py` puts a filter value into the query string RAW, so `.like("geohash",
"xn77%")` ships a bare `%`. Both `supabase==2.10.0` and `httpx==0.27.0` are PINNED
and did not move — the edge changed underneath us, which is why endpoints that had
been working stopped without a deploy.

Rule 15 applies (two defects of one shape ⇒ audit the shape). All **seven**
`like`/`ilike` call sites in the backend were enumerated with grep, not sampled,
and every one routed through a new `backend/pgrest.py` that expresses the pattern
with PostgREST's `*` wildcard so no `%` reaches the URL. Three were broken for
every user — venue nearby, venue search and **user search**; four were latent.
Redeployed as v68; both endpoints 200.

## The admin bug the UI pass uncovered (and fixed)

Driving the real panel is the only thing that could have found it: mock mode's
create answered *"Created パンチ・イット練習場."* and the row did not appear, and
editing it answered *"Venue #9003 not found."* Two bugs, one cause — the fixtures
were a module-level array that `venueMutations` never wrote to, and making it
mutable then exposed that Next's dev server gives the page bundle and each
route-handler bundle their own module instance. `lib/mockStore.ts` already solves
that (`globalThis`) and every other mock entity already uses it; venues now do too.

## The three § B1 round-trips, proven twice

**Through the real panel UI** (mock mode, so the React form + route handlers are
what is exercised):

| # | Action | Result |
|---|---|---|
| 1 | Create | *"Created パンチ・イット練習場 (#9003)."* — row appears with the PARTNER badge, Driving ranges, `¥1,200/30分`, `深夜`, the offer, Active Yes |
| 2 | Edit | *"Saved パンチ・イット練習場."* — offer becomes `GOLFIN プレイヤー 20%OFF（平日限定）` in the table |
| 3 | Deactivate | leaves the `Active = Yes` filter; under `Active = No` it is there, amber **Inactive** — the row still exists, because there is no delete |

**Find on map** was verified in the same pass: a pasted Maps link filled Latitude
`35.6595`, Longitude `139.7005` and the DERIVED Geohash `xn76fgwg9` — matching the
value computed independently in Python — in a field the DOM reports as
`disabled: true, readOnly: true`.

**Against live data**, the half mock mode cannot prove — the same three writes made
with the exact column set `venueMutations.toRow` produces, then the CLIENT's own
fetch:

```
RANGE near Shibuya      0.0 m  パンチ・イット練習場   src=admin partner=True  GOLFIN プレイヤー 15%OFF
FOOD (5 -> 4 rows)      焼肉 GREEN  ゴルファー15%OFF（ラウンド当日）   ← edited offer visible
                        ラウンジ BIRDIE ABSENT                        ← deactivated row gone
```

All three demo rows were then RESTORED to their seeded state, and the row this pass
created was deactivated rather than deleted (the panel's own rule, applied to my own
test row). Cesar can delete or re-activate `#2004 パンチ・イット練習場` in the panel.

## Files changed

See § Files in the chat hand-off. Not this task's, and left alone: the
`gps_profile_prompt_on_entry` / `gps_navbar_selected_tab` working set, and the
`game_polish` / `design_consistency_audit` files that appeared mid-session from a third session.
