# IMPLEMENTER_REPORT — `gps_gifts_votes`

**Iteration shape:** `gps_gift_vote:first-build`
**Canonical screenshot:** `screenshots/gv_02_gift.png` (1170×2532)
**Second canonical:** `screenshots/gv_05_vote.png` (1170×2532)
**Run log:** `Docs/Diagnostics/_capture/gps_gifts_votes_run.log`
**Shipped:** GolfinRedux `b823510d5`, playlife `4206a56`

---

## ✅ APPLIED, DEPLOYED, E2E GREEN

Cesar applied `2026_09_02_gift_atomic.sql` 2026-09-02 08:2x UTC. His verification output:

```
proname              | prosecdef | proacl
---------------------+-----------+-----------------------------------------------
golfin_gift_pts      | true      | {postgres=X/postgres,service_role=X/postgres}
golfin_gift_purchase | true      | {postgres=X/postgres,service_role=X/postgres}
```

SECURITY DEFINER on both, and EXECUTE granted to `service_role` only — no `public`, `anon` or
`authenticated`, which is the posture that keeps a logged-in client from draining an account
through PostgREST.

§1 reconciliation landed: **19 profiles, 0 invariant violations** (Cratilo `total_points`
6808 → 7158, the one row that was out of balance).

Then deployed: `playlife-api` **v65 → v66**, image `deployment-01M1GKCHV15EH5S2BXZHYMCGK2`,
`/health` → `{"status":"ok","version":"0.1.0"}`.

---

## Checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Per-element A/B crops vs both renders; ΔRGB incl. plum hero, chips, bar fills | **PASS** | § A/B below — 18 non-text regions, gift mean 2.8, vote mean 3.7, worst 6.1 |
| 2 | Geometry JSON + invariants + lint `fail=0`, both screens | **PASS** | geometry `97 sites 0 FAIL 0 GONE`; lint `0 FAIL` on both prefabs |
| 3 | Migration applied before deploy; deployed; live send E2E quoted | **PASS** | § Live economy E2E — RPC and HTTP, both quoted |
| 4 | Purchase E2E | **PASS** | § Live economy E2E |
| 5 | Invariant audit after both E2Es | **PASS** | `19 profiles, 0 violations` before AND after |
| 6 | Vote E2E: list renders, cast handled, +10 once, create, MINE | **PARTIAL** | list/MINE/create-modal proven live (§ Live evidence); a CAST was NOT performed — see § Not done |
| 7 | Play-mode screenshots of both screens signed in, with service log lines | **PASS** | 7 frames in `screenshots/`, every one through real `onClick` |
| 8 | Both ScreenIds in GpsGate; hub Gift tab and Vote affordance navigate | **PASS** | `gate=True/True`; run log `nav GIFT interactable=True → ok GpsGift`, `tile VOTE interactable=True → ok GpsVote` |
| 9 | Balance in Top UI refreshes after send/purchase/cast | **PASS** | `GiftSendModalController.Committed` / `OnEarned` call `RefreshBalanceAsync`; `/points/balance` tracked every write in the HTTP round-trip (7078 → 7028 → 6998 → 6948) |
| 10 | Importer PLAN → APPLY → publish → `--check` clean; no hardcoded literals | **PASS** | `texts` v31, 958 rows, `--check: clean`; grep below |
| 11 | Full EditMode sweep green | **PASS** | `2258 total / 2255 passed / 0 failed / 3 skipped` |
| 12 | Deviations flagged | **PASS** | § Deviations |

---

## Live evidence (play-mode, real navigation, live API)

Signed in as **Cratilo** `f2636482-29aa-4233-a834-99526b202fe1`. Every navigation is a real
widget's `onClick` — boot → the real `StartButton` → Home → the Home `GpsPill` → the hub's own
`NavGiftButton` / `Tile_VOTE`.

```
tapping the real StartButton
ok   Home
signed in as 'Cratilo' id=f2636482-29aa-4233-a834-99526b202fe1
tapping the Home GPS pill (GpsPill, interactable=True)
ok   GpsHub
nav GIFT interactable=True
ok   GpsGift
  ContentContainer/GiftHero/HeroValue = "0 pts"                 <- profiles.gift_pts, really 0
  ContentContainer/GiftHero/HeroSub   = "from 0 supporters"
  ContentContainer/Supporters/Supporter0/Name = <hidden>        <- no gift rows: rows hidden, header stays
  ContentContainer/Golfers/Golfer0/Name       = "Qnion92"       <- /user/discover, live
  ContentContainer/Golfers/Golfer0/Followers  = "0 followers"
  ContentContainer/BuyGifts/GiftItems/Item0/ItemName  = "グローブ"        30 pts
  ContentContainer/BuyGifts/GiftItems/Item1/ItemName  = "リストバンド"    40 pts
  ContentContainer/BuyGifts/GiftItems/Item2/ItemName  = "ベーシックキャップ" 50 pts
tapping SEND GIFT row 1 (interactable=True)      -> gv_03_gift_send_modal.png
tapping BUY item 1 (interactable=True)           -> gv_04_gift_buy_modal.png
tile VOTE interactable=True
ok   GpsVote
  card Card0 (530px) q="パット数30以下でラウンドできる？"       meta="0 votes · 0 days left" yes=0%
  card Card1 (232px) q="今月中にベストスコア更新する人はいる？"   meta="0 votes · 0 days left" yes=0%
  card Card2 (232px) q="朝一のティーショット、フェアウェイキープできる？"
  card Card3 (450px) q="ゴルフ場のランチ、カレーとラーメンどっちが人気？"
  card Card4 (232px) q="今週末、100切りを達成する人はいる？"
  cards rendered = 5 (service saw 5)
  story 0..5 = "Qnion92" "ken" "Apple Re…" "hashiy" "tomo" "Gutti11"
tapping chip MINE  -> gv_06_vote_mine.png
tapping + CREATE   -> gv_07_vote_create_modal.png
```

The five questions are the five live `votes` rows, and the six story names are the first six
`/user/discover` rows — nothing on either screen is the node's mockup data.

The modal's balance line reads **"Your balance: 7,158 pts"** while the top bar reads **6,808 RP**.
That is not a bug and it is the reason the line exists: the modal shows `activity_pts`, which is
what `golfin_gift_pts` will actually accept, and the top bar shows `total_points`. (The two differ
at capture time only because of the invariant violation the migration has since repaired —
after it, Cratilo reads 7,158 in both places.)

### Requests observed (Editor.log)

```
GET /api/v1/user/detail    → 200   GET /api/v1/user/discover  → 200
GET /api/v1/gifts/received → 200   GET /api/v1/gifts/items    → 200
GET /api/v1/points/history → 200   GET /api/v1/points/balance → 200
GET /api/v1/vote/list      → 200 (5 rows)
```

---

## Live economy E2E

### Through the RPCs — `e2e_gift_economy.py`, **ALL PASS**

```
  PASS  invariant total_points = activity_pts + gift_pts (before)   19 profiles, 0 violations
sender   Cratilo   {"activity_pts": 7158, "gift_pts": 0,   "total_points": 7158}
receiver ken       {"activity_pts": 510,  "gift_pts": 100, "total_points": 610}

-- golfin_gift_pts(50, key=7157929a-…)
   -> {"ok":true,"replayed":false,"amount":50,"sender_activity_pts":7108,
       "sender_total_points":7108,"receiver_gift_pts":150,"receiver_total_points":660}
  PASS  sender activity_pts -50    7158 -> 7108
  PASS  sender total_points -50    7158 -> 7108        <- the half the old router skipped
  PASS  receiver gift_pts    +50    100 -> 150
  PASS  receiver total_points +50    610 -> 660        <- and the other half
   sender ledger  : gift_sent     -50 activity  "ギフト送付: ken"      key 7157929a-…
   receiver ledger: gift_received +50 gift      "ギフト受取: Cratilo"  key abbeb4fc-…
  PASS  receiver row carries a DERIVED key, not the sender's

-- REPLAY with the SAME key
   -> {"ok":true,"replayed":true, …identical balances…}
  PASS  replay moved NOTHING

-- refusals
   self-gift  -> {"ok":false,"reason":"self_gift"}
   over-spend -> {"ok":false,"reason":"insufficient","required":999999999,…}
  PASS  refusals moved NOTHING

-- purchase グローブ (30 pts)
   -> {"ok":true,"price":30,"activity_pts":7078,"total_points":7078,
       "inventory_id":"7d07caa3-…"}
  PASS  purchase debited activity_pts + total_points   -30
   replay -> {"ok":true,"replayed":true,…}
  PASS  replay wrote NO second inventory row

  PASS  invariant (after)   19 profiles, 0 violations
=== ALL PASS ===
```

### Through the deployed routers — real JWT, real HTTP

Signed in as `cesar.guarinoni@gmail.com` / `f2636482-…`; `/points/balance` read before and after.

```
balance before: {"activity_pts":7078,"gift_pts":0,"total_points":7078}

POST /gifts/send-pts -> 200 {"amount":50,"receiver":"ken","remaining_activity_pts":7028,
                             "total_points":7028,"replayed":false}
REPLAY same key      -> 200 {…,"replayed":true}      balances IDENTICAL
self-gift            -> 400 {"detail":"Cannot gift yourself"}

POST /gifts/purchase -> 200 {"item":"グローブ","price":30,"activity_pts":6998,
                             "total_points":6998,"inventory_id":"8ef21344-…"}
REPLAY same key      -> 200 {…,"replayed":true,"inventory_id":null}   no second row

no key at all        -> 200 {…,"replayed":false,"idempotency_key":"43080075-…"}
                        <- the FLUTTER path: server-generated uuid4, still works
balance after:  {"activity_pts":6948,"gift_pts":0,"total_points":6948}
```

Final state — `ken` `{activity 510, gift 250, total 760}`, `Cratilo` `{activity 6948, gift 0,
total 6948}`, **19 profiles / 0 invariant violations**, two `user_inventory` rows for the two
non-replayed purchases, and every ledger row keyed.

The Flutter-compatibility line is the one worth keeping: `/gifts/send-pts` with NO
`idempotency_key` still returns 200 and moves points, exactly as it does today, with the server
minting the key. Nothing the PLAYLIFE app does had to change.

## A/B — node render vs live capture

Non-text regions only; a text region measures the FONT, not the fill. Full sheet:
`Docs/Diagnostics/_capture/gps_gifts_votes_ab.txt`.

| Region | node | built | mean \|ΔRGB\| |
|---|---|---|---|
| **GIFT** hero plum gradient (top) | (120,45,74) | (121,46,75) | **1.0** |
| hero plum gradient (bottom) | (79,34,55) | (74,29,50) | 5.1 |
| supporters panel fill | (87,89,83) | (90,88,82) | 1.5 |
| golfers panel fill | (58,68,74) | (56,65,72) | 2.4 |
| golfers row divider (1px) | (88,96,99) | (96,98,99) | 3.8 |
| buy panel fill | (75,77,66) | (79,71,61) | 4.9 |
| item cell fill | (40,54,73) | (40,56,72) | 1.3 |
| panel 3px white border | (253,253,253) | (255,255,255) | 2.0 |
| gold SEND GIFT button body | (149,119,64) | (148,118,56) | 3.4 |
| | | **gift mean** | **2.8** |
| **VOTE** stories strip fill | (36,46,56) | (31,45,67) | 5.9 |
| chips strip fill | (97,106,103) | (92,101,102) | 4.1 |
| photo placeholder (green) | (53,92,53) | (54,91,53) | **0.6** |
| card body fill | (93,115,133) | (100,118,136) | 4.7 |
| bar track (unfilled) | (109,131,149) | (118,135,152) | 4.9 |
| reward pill interior | (170,171,144) | (171,169,144) | 1.3 |
| card 3px white border | (253,253,253) | (242,248,251) | 6.1 |
| gold VOTE button body | (177,144,70) | (178,145,68) | 1.3 |
| silver GIFT button body | (151,158,168) | (155,162,172) | 4.2 |
| | | **vote mean** | **3.7** |

Pill widths, measured after a forced layout rebuild: `"500 pts"` → **114.2px** against the node's
**116**; `"5,000 pts"` → **134.5px** against the node's **136** for the same-length string.

---

## Figma fidelity

Node re-pulled 2026-09-02 (`get_metadata` + `get_design_context` on 14027:101843 / 14028:33534 and
seven sub-nodes); every SVG token below was read out of the node's own SVG, not off a render.

| Element | Node | Node value | Built | Verdict |
|---|---|---|---|---|
| Gift Hero | 14027:102100 | 958×288 r50, GRAD #6b2140→#3a1226 **opaque**, 3px white | baked `S_GV_GiftHero.png` | **PASS** ΔRGB 1.0 |
| Hero title | 14027:102110 | SemiBold 36 #f07f9c | SemiBold 32.2 (36×59/66) #F07F9C | **PASS** (Build rule 4) |
| Hero sub / note | :102111 / :102113 | Medium 26 / 24 #f4b8c8 | Medium 26 / 24 #F4B8C8 | **PASS** |
| Hero value | 14027:102112 | SemiBold 96 #ffffff | SemiBold 85.8 white | **PASS** |
| Panel gradient | :102114 / :102146 / :102190 | GRAD rgba(19,52,83,.6)→rgba(9,27,51,.6) r50 3px white | baked, fitted per footprint | **PASS** ΔRGB 1.5 / 2.4 / 4.9 |
| Panel title | 14027:102116 | SemiBold 42 #eedc9a | SemiBold 37.5 #EEDC9A | **PASS** |
| "SEE ALL ›" | 14027:102117 | Medium 28 #b7c3d3 | authored, **INACTIVE** | **DEVIATION** — no destination in v1 (SPEC § Out of scope) |
| Separator | 14027:102118 | white gradient α 0→0.9→0, 2px | baked `S_GV_Separator.png` from the SVG stops | **PASS** |
| Row divider | :102128 border-t | rgba(255,255,255,0.12) 1px | 1px Image, corrected alpha 0.0531 | **PASS** ΔRGB 3.8 |
| Rank | :102120 / :102129 | SemiBold 30, #eedc9a rank 1 / #b7c3d3 rest | SemiBold 26.8, same split | **PASS** |
| Avatar disc | 14027:102122 | 72px r34.5 stroke 3 **solid** #F3ECC2, grad pair per colour | baked at 72/88/48, four colours | **PASS** |
| Name / Followers | :102125 / :102126 | SemiBold 30 white / Medium 22 #b7c3d3 | SemiBold 26.8 / Medium 22 | **PASS** |
| Pts | 14027:102127 | SemiBold 32 #f07f9c right | SemiBold 28.6 #F07F9C right-aligned to 926 | **PASS** |
| SEND GIFT | 14027:102159 | Main Buttons Gold-Small 240×54 r20 | `Play Button.png` 9-sliced ppum 18/20, 240×54 | **PASS** ΔRGB 3.4 |
| Buy title / sub | :102191 / :102192 | SemiBold 34 gold / Medium 24 muted | SemiBold 30.4 / Medium 24 | **PASS** |
| Item cell | 14027:102194 | 287.33×168 **r28**, std panel fill | baked `S_GV_ItemCell.png` r28 | **PASS** ΔRGB 1.3 |
| Icon ring | 14027:102196 | 72px r34 stroke 4, #204B76→#0B203D fill, #F3ECC2→#98855B rim | reused `S_GpsIconRing_Tile.png` (88px atom at 72 → stroke 4.09) | **PASS** — Δ0.09px stroke |
| Item name / price | :102200 / :102201 | SemiBold 22 white / SemiBold 24 #eedc9a | SemiBold 19.7 / 21.4 | **PASS** |
| Stories row | 14028:33791 | 958×143 r32, #091b33 @0.70 | baked + fitted | **PASS** ΔRGB 5.9 |
| Story NEW | 14028:33794 | dashed gold disc 88, "+" SemiBold 44 #eedc9a, label Medium 18 gold | node PNG export + SemiBold 39.3 | **PASS** |
| Story avatar | 14028:33799 | 88px r42.24 stroke 3.52, initial SemiBold 37 | baked at 88, SemiBold 33.1 | **PASS** |
| Chips container | 14028:33827 | 958×78 r100, #091b33 @0.70 | baked + fitted | **PASS** ΔRGB 4.1 |
| Chip selected | 14028:33828 | GRAD #f3ecc2→#c9a94f, 1px #422100, r100, label SemiBold 24 #2a1a00 | `S_SU_GoldSegment` inside a #422100 capsule, label 21.4 #2A1A00 | **PASS** — but on **PUBLIC**, not TRENDING (deviation) |
| Chip unselected | 14028:33830 | no fill, 1px #818ea1, label SemiBold 24 white | `S_GV_ChipRing` (hollow) tinted #818EA1 | **PASS** |
| + CREATE | 14029:102236 | Gold-Small 230×54 | 230×54 | **PASS** |
| Vote card | 14028:33836 | 958×530 r50 std panel | baked | **PASS** |
| Photo area | 14028:33837 | 958×300 GRAD #3f6b3a→#1c3a1f + 80px Screenshot glyph | baked top-rounded, inset 3px | **PASS** ΔRGB 0.6 |
| Photo area 2 | 14029:102242 | 958×220 GRAD #6b4a2a→#3a2a16 | baked | **PASS** |
| Author strip | 14028:33843 | 48 avatar, name SemiBold 26, when Medium 22 muted | 48 disc, SemiBold 23.2, Medium 22 | **PASS** |
| Question | 14028:33851 | SemiBold 30 white | SemiBold 26.8 | **PASS** |
| Status pill | 14028:33852 | `rgba(238,220,154,0.18)` + 1px #eedc9a r100, label SemiBold 22 gold | corrected α 0.1299 fill + `S_GV_PillRing` rim | **PASS** ΔRGB 1.3 — content is `+10 pts` (deviation) |
| Bar label / track / pct | :33855 / :33856 / :33858 | Medium 26 white 70w / 16h r8 rgba(255,255,255,.15) / SemiBold 26 white | Medium 26 / 738×16 α 0.0893 / SemiBold 23.2 | **PASS** ΔRGB 4.9 |
| YES / NO fill | :33857 / :33862 | #7ed488 / #6fa5e8, driven by width | `S_PillStadium` ppum 88/8, width-driven | **PASS** |
| Meta | 14028:33865 | Medium 24 #b7c3d3 | Medium 24 #B7C3D3 | **PASS** |
| GIFT / VOTE | :33867 / :33872 | Silver-Small / Gold-Small 230×54 | 230×54 both | **PASS** ΔRGB 4.2 / 1.3 |
| Multi option pill | 14028:33908 | `rgba(126,212,136,.18)` + 1px #7ed488 r100, SemiBold 22 | same construction, four accents in node order | **PASS** — not exercised live (no >2-option vote exists) |

---

## Clone provenance

| Element | Source | Kind |
|---|---|---|
| GPS nav bar (both screens) | `Assets/Prefabs/UI/Gps/GpsHubScreen.prefab` → `GpsNavBar` | `PrefabUtility.LoadPrefabContents` + `Instantiate` |
| Gold-Small / Main Buttons | `Assets/Art/HomeScreen/Play Button.png` | 9-sliced atom, ppum 18/20 |
| Silver-Small | `Assets/Art/RosterScreen/ButtonCancel.png` | 9-sliced atom, ppum 25/20 |
| Selected filter chip fill | `Assets/Art/UI/Gps/S_SU_GoldSegment.png` | the #f3ecc2→#c9a94f capsule score_upload already bakes |
| Gift-item icon ring | `Assets/Art/UI/Gps/S_GpsIconRing_Tile.png` | the 88px icon-ring atom |
| Bar track / fill, pill fill, chip ON rim | `Assets/Art/Tournaments/S_PillStadium.png` | the project's 9-sliced capsule, border 88 |
| Modal panel / row / field | `S_SU_ModalPanel.png` / `S_SU_ModalRow.png` / `S_SU_SearchField.png` | score_upload's venue-picker modal family |
| Gift background | `Assets/Art/Shop/Background - Rewards.png` | existing art; node variant "Rewards", 0.485 mean \|ΔRGB\| |
| Vote background | `Assets/Art/ClubsInventory/Background.png` | existing art; node variant "Rsnkings Day Illustration", 0.175 |
| Avatar discs (12) | `Docs/Scripts/make_gps_icon_ring.py::bake` | the atom's own routine, new sizes/colours |
| Story NEW disc | node 14028:33794 asset export | Figma export, not a crop |

Nothing on either screen is a hand-rolled flat fill. The linter's `flat-fill` WARNs are the two
modal backdrops and five transparent tap targets, all alpha ≤ 0.87 by design.

---

## UI fidelity lint

* `Docs/Diagnostics/_capture/GpsGiftScreen_lint.json` — **0 FAIL**, 1 WARN, `RESULT: PASS (health)`
* `Docs/Diagnostics/_capture/GpsVoteScreen_lint.json` — **0 FAIL**, 14 WARN, `RESULT: PASS (health)`

Spec: `reference/nodes/GpsGiftScreen_spec.json` (26 elements) /
`reference/nodes/GpsVoteScreen_spec.json` (32 elements).

The 15 WARNs, each accounted for rather than waved off:

* 7 × `flat-fill` — the two modal backdrops (`#000000DD`, deliberate) and five fully transparent
  tap targets (`#00000000` on the chips and the NEW story cell).
* 8 × `unlocalized-text` — the reward pill and the four option-pill labels. They carry an authored
  PLACEHOLDER (`+10 pts`, `Name 00%`) so the `ContentSizeFitter` has something to measure; the
  controller overwrites every one of them on bind, from a localized key.

## Geometry

`Docs/Diagnostics/_capture/gps_gifts_votes_geometry_audit.txt` — **`97 sites 0 FAIL 0 GONE`**
across `GpsGiftScreen_geometry.json` (42 sites) and `GpsVoteScreen_geometry.json` (55).

## Tests

Full EditMode sweep: **`2258 total / 2255 passed / 0 failed / 3 skipped`**. The three skips are the
pre-existing `HoleCompleteDriverTests` Stage-C1 skips.

**Tripwire.** The 19 new `Golfin.Social.Tests` genuinely execute: the run BEFORE last failed
`CreateJson_IsYesNoWithTwoOptions` with `Expected: "2026-09-09T00:00:00Z" But was:
"09/09/2026 00:00:00"` — `JObject.Parse` defaults to `DateParseHandling.DateTime` and had rewritten
the string. The assertion, not the code, was wrong; both were fixed.

## Localization

53 new keys, EN + JA, no hardcoded literals:

```
texts   53 add   0 change   905 same   0 conflict     (PLAN)
Wrote 53 draft(s) ... (53 new, min_build 2578)         (APPLY)
content_publish -> 200  31                             (PUBLISH: texts v31)
  texts  v31  958 rows  unchanged                      (EXPORT)
--check: clean — no file would change and no catalog has drifted.
```

`Assets/Resources/Data/content_version.txt` bumped and committed.

```
$ grep -nE '\.text\s*=\s*"' Assets/Scripts/UI/Gps/Gps{Gift,Vote}ScreenController.cs \
      Assets/Scripts/UI/Gps/{VoteCardView,GiftSendModalController,VoteCreateModalController}.cs
(no matches — every user-visible string is a LocalizedText binder or LocalizationManager.Get)
```

---

## What the gates caught that the eye would not have

1. **`GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()`.** Unity's fake-null defeats
   `??`, so this returned a destroyed reference and threw `MissingComponentException` on the Vote
   screen's first frame — which took `OnEnable` down with it, so `/vote/list` was never requested
   and the whole list was empty. CLAUDE.md Basic Rules #4 names exactly this. Fixed with `== null`,
   and the builder now authors the CanvasGroup so the branch is a guard rather than the path.
2. **Every translucent overlay pre-composited against an assumed opaque backdrop.** Measured against
   the node: bar track 64 too dark, reward pill 48 too dark, row divider invisible (+1 where the
   node has +23) — while the OPAQUE baked gradients landed at ΔRGB 1.0. That control is what made
   it a diagnosis. All four sites now carry a linear-corrected real alpha (`LinearAlpha`, the C#
   twin of `alpha_over()` in the bake script).
3. **A pill whose opaque rim hid its own translucent fill.** The interior measured (238,220,154) —
   solid gold — because the root was a tinted `S_PillStadium` and the fill composited over IT. Now
   a hollow `S_GV_PillRing` over the fill.
4. **An `Image` on a `ContentSizeFitter` root.** `Image` is an `ILayoutElement` and a 9-sliced
   sprite reports its native 176px as a preferred width, which beat the layout group's 110 and made
   every pill 60 % too wide. Both visuals moved to ignore-layout children.
5. **Story labels rendering as nothing.** TMP resolves `Ellipsis` during line layout, so it needs
   wrapping ON — and a wrapped 18px line does not fit the node's 21px box, so TMP drew nothing at
   all while `.text` held the right string. The string is truncated instead of the box.

---

## Deviations

1. **The reward pill reads `+10 pts`, not a pool.** Pre-flagged in the SPEC. `votes.sponsor_pool`
   does exist in the schema and is **0 on every live row** — nothing writes it — which is the
   evidence behind that decision, now confirmed rather than assumed.
2. **TOP SUPPORTERS reads two sources, not the one the SPEC names.** `/gifts/received` reads the
   `gifts` table; only the item-gifting path (out of scope) inserts there, and it held **0 rows** in
   production. An RP gift is recorded solely as a `points_transactions` row, so the panel pages
   `/points/history?currency=gift` alongside it and merges by name. Consequence: `SenderId` is null
   for RP supporters — the panel needs a name and points, not an id.
3. **`— followers` on supporter rows.** Neither source carries a follower count, and a profile fetch
   per supporter is three round trips for one line. The node's "N followers" run renders the em dash
   the rest of the GPS surface uses for "not known".
4. **The BUY strip is the three CHEAPEST basic rows, not the SPEC's named trio.** The SPEC asserts
   "ベーシックキャップ 50 · サンバイザー 80 · ポロシャツ（白）100"; the live catalog has 21 rows and
   that trio is not the first three under any ordering (the router orders by `category`, which puts
   リストバンド 40 first). Price-ascending is deterministic and reproduces the node's ascending
   50→100→500 shape. Live result: グローブ 30 · リストバンド 40 · ベーシックキャップ 50.
5. **Two of the three item glyphs are the same.** The SPEC's mapping is hat→Star, tops→Sparkle,
   shoes→Pin, else→Heart; the strip's live categories are gloves / accessory / hat → Heart, Heart,
   Star. Implemented verbatim rather than quietly extended.
6. **PUBLIC is the selected chip, not TRENDING.** TRENDING and FRIENDS have no backend and are
   rendered at 45 % and non-interactive per the SPEC, so the gold chip has to be one of the two live
   ones.
7. **The photo header is a POSITION rule, not a data rule.** The SPEC keeps the photo areas "static
   per Figma", and the frame puts a green photo on card 1 and a brown one on card 4. No live vote
   carries a photo (`related_activity_id` is null on all five), so `TemplateFor` reproduces the
   node's rhythm by index. That is the one function to change when votes carry a round's screenshot.
8. **A cast means YES.** The node's footer draws ONE Gold-Small labelled VOTE, not a YES and a NO
   button, so v1 casts the first option. A NO path needs a control the design does not have.
9. **The small-button label is 34.9, not the node's 39.** `SemiBoldSize` (59/66) is applied to every
   SemiBold run per auth_golf_profile's whole-face finding. `ScoreUploadScreenBuilder.SmallButton`
   still ships the raw 39; this builder deliberately differs rather than diverging silently.
10. **`/points/earn` is not idempotent.** It calls the unkeyed `earn_activity_pts`. The only thing
    stopping a double credit is that the CAST before it can only succeed once, so the earn is
    reachable ONLY from the successful-cast branch — never from already-voted. A keyed earn action
    is a server change and is out of scope.
11. **The modals have no Figma node.** The SPEC asks for "Pop-up panel language like
    `VenuePickerModalController`", which is what they are: the same `S_SU_ModalPanel` family at the
    sprite's native 978×1400 (drawing it at another aspect distorts its r50 corners — the linter's
    `nonuniform-stretch`, caught and fixed).

## Not done

* **No cast was performed against production.** The five live votes are GOLFIN AI seeds from April
  that are already expired; casting on one would write a `user_votes` row and mint 10 RP against a
  dead poll, and the cast path cannot then be re-tested on that vote from this account. The
  already-voted branch, the repaint and the earn are covered by EditMode tests over the real service
  and the real `ApiClient`. Cesar's call whether to burn one.
* **No device pass**, per standing rule.

## Files modified or created

See § below in the parent brief; every path outside this spec folder is in `b823510d5`
(GolfinRedux) or `4206a56` (playlife), and `git status` is clean of code drift.
