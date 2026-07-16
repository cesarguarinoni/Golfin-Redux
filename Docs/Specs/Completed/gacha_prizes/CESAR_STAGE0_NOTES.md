# gacha_prizes Stage 0 — Cesar live-review notes (2026-07-16)

Cesar steered Stage 0 live. Corrections relayed during the build (gold PULL clone, one-line labels,
separator above COST, Level Up/Repair stripped-visually, no scrollbar). Scrollbar "sighting" was the
blurred background image — NOT an issue.

## OPEN — the ONLY remaining issue: uneven VISIBLE gaps

Orchestrator pixel-measured the canonical (visible navy-to-content, NOT RectTransform edges):
- **VISIBLE TOP gap** (panel top navy edge → first card visible top) = **19px**
- **VISIBLE BOTTOM gap** (BACK button visible bottom → panel bottom navy edge) = **38px**

They must be **EVEN**. The implementer's GetWorldCorners read 42/42 and looked fine, but that's the
RT-edge gap — the BagClubCard clone has ~19px of TRANSPARENT padding above its visible art, so the
visible top gap renders ~19px smaller than the RT gap. Same RT-vs-visible trap as the ball card.

**FIX:** increase the panel's TOP padding by ~19px (top ≈ 61 RT, or reposition the first row down) so
the **VISIBLE** top gap ≈ the visible bottom gap (~38px, matching the bottom which is near the intended
42). **VERIFY by PIXEL-measuring the canonical** (visible navy→card top vs BACK→panel bottom), NOT by
GetWorldCorners on the RectTransforms — those lie here because of the card's transparent top inset.
Acceptance: visible top gap == visible bottom gap (±3px).

## Stage 0 — CESAR APPROVED 2026-07-16 ("I'll approve it this time")
Gaps accepted at visible 42.9 top / 42.0 bottom. Cesar flagged 42.9 != 42 — **be EXACT**.
STAGE 1 TODO: tighten the top padding so the VISIBLE top gap is 42.0 (currently 61 RT padding -
18.1 card inset = 42.9; set to ~60.1 RT so visible = 42.0). Do it while the controller work
re-touches the prefab.

Stage 1 scope (per Cesar's fork decisions): GachaPrizesScreenController spawns the MOCK pool
(varied rarity cards — silver/blue/green/gold — to replace the all-green "Test" placeholders and match
the node variety); PULL x10 = stub; BACK -> back to gacha main; register ScreenId + entry from the
gacha main screen's prizes/pull action. Mock pool, no real ticket spend (fork: mock).
