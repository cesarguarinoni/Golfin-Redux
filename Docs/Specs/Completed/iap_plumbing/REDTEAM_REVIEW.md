# RED-TEAM REVIEW — iap_plumbing (iter-1)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-09-15 10:47 JST
**Verdict:** ARCHITECT_REVIEW_PASS — genuinely tried to break it across visual / geometric / spec-intent, re-ran every verifiable acceptance item with my own evidence, found no concrete blocker.

Environment: `mcp__ai-game-developer__*` was down; Unity driven via `Tools/unity-mcp-call.py`. Figma MCP (design server) was up and used for the live node re-pull. Server + live DB reached over curl + the sanctioned read-only PostgREST client.

## Prior rejections replayed
`CESAR_REJECTION.md` does NOT exist for this task (iter-1, no prior Cesar bounce). Nothing to replay.

## The two items handed to me — both RESOLVED, neither a blocker

1. **New Button without ButtonPressFeedback in the 3 prefabs?** NO. Authoritative live component graph (`BtnAudit` via script-execute, read-only asset load), not a YAML grep (the classic Button GUID `f70555…` appears in NO prefab — Unity 6 uGUI uses different script GUIDs, which is why a raw grep reads 0):
   - `StorePaymentModal.prefab`: 3 Buttons — `ModalPanel/Body/RpButton`, `MoneyButton`, `CancelButton` — each `ButtonPressFeedback_sibling=True`; ButtonPressFeedback total=3, **orphan=0**.
   - `GeneralShopCard_Club.prefab` / `_Ball.prefab`: 1 Button each (`CtaGoldButton`, pre-existing BUY), each with a ButtonPressFeedback sibling; orphan=0.
   - `git diff` on both card prefabs added **0** Button references; the +214 each is Image(`fe87c0e1`)+TMP(`f4688fdb`)+font+`S_DiscountBadge` sprite — the DiscountBadge, no Button. **Rule 11 PASS.**

2. **Modal title "GOLD TICKET" vs Figma "GOLDEN TICKET" — data or fidelity gap?** DATA. `ticket_types.csv` id=1 = `gold, Gold Ticket, ゴールドチケット, Ticket_Gold` — "Gold Ticket" is the real, pre-existing ticket name (the same one gacha uses); `shop_catalog` row `shop_ticket_gold_10_iap` has refId=1. The modal binds `card.DisplayName` = that real name, exactly as the SPEC mandates ("bound from the row, not a new key"), and SPEC §Reference explicitly calls the Figma copy a placeholder. The implementer invented no name. **Not a fidelity gap.**

## Angle I captured / re-derived myself
- Viewed the built store `screenshots/live_store_all_LIVECONFIG.png` and modal `screenshots/live_payment_modal_LIVECONFIG.png` at full 1170×2532 against `reference/iap_pricing_screen.png` / `reference/purchase_modal.png`.
- **Re-pulled the LIVE Figma node myself** (rule 9): `get_design_context(14289:33231)` → title text "GOLDEN TICKET ×10", gradient `rgb(255,255,255)→rgb(209,213,219)→rgb(129,142,161)`, style literally named **"Silver"**; `get_screenshot(14289:33223)` fresh render → title is **silver/slate**, NOT orange. The orange title in the spec-time `purchase_modal.png` is a STALE earlier design; the built silver title matches current node truth (Lesson AK). The reviewer's dismissal of the orange was correct — I re-derived it two independent ways instead of trusting it.

## Metrics I re-ran (my numbers, not the report's)
- **Backend golfin suite:** `pytest tests/test_iap_golfin.py` → **24 passed** (matches report).
- **Full EditMode suite (my run, unfiltered):** Status Passed, Total 3152, **Passed 3149, Failed 0, Skipped 3**, dur 2:42. `IapPlumbingTests` filter → 29 passed / 0 failed. (Report said 3148/4-skip; same total, 0 failures both ways — the 1 pass↔skip delta is a conditionally-skipped test, benign.)
- **UI fidelity lint (Rule 12/21) — RE-RAN fresh** (deleted old JSON, re-invoked `UIFidelityLinter.LintPrefab(path,null)`; mtime 10:41:23): `GeneralShopCard_Club` fail=0 warn=10; `_Ball` fail=0 warn=114; `StorePaymentModal` fail=0 warn=8. **0 FAIL-severity findings anywhere.**
- **Live server (my curls):** `POST /iap/golfin/verify` (no auth) → **403**; `GET /iap/golfin/config?platform=apple` → `enabled:true` + `test.tickets.x10`; partner `GET /iap/catalog?platform=apple` → exactly the 3 `com.wonderwall.playlife.pts*` rows, **no `test.`**.
- **Live DB re-derivation (Rule 6, read-only PostgREST):** `iap_purchases app=golfin` = **1 row** {txn `580af522-805a-4455-baa3-0f9bd8eb2d3d`, status `failed`, error `empty_receipt`, pts_credited 0, granted_ref null, product `test.tickets.x10`, created `2026-09-15T01:20:53Z` = 10:20:53 JST}; `points_transactions type=iap_purchase` since 09-14 = **0**; `golfin_ticket_transactions reason like 'iap:%'` = **0**; `golfin_shop_purchases paid_currency not null` = **0**. Every number matches the report exactly, and the txn timestamp aligns with the 10:21 capture — proving the live-config run was real (not a doctored frame) AND that **money wrote no pts / no grant on the refused path**.
- **Report-integrity 402-vs-404:** resolved. 404 in the console block = the disclosed 09:12 PRE-deploy run; the deployed endpoint's real behavior is the 402/failed-row above. No fabrication.
- **Strings:** `content_version.txt` texts=57; all 5 keys (`STORE_CHOOSE_PAYMENT`, `STORE_IAP_FAILED`, `STORE_IAP_UNAVAILABLE`, `STORE_IAP_PROCESSING`, `SHOP_HISTORY_PRICE_MONEY`) present EN+JA+active; zero `\.text = "…"` literals in the 4 touched shop runtime files.
- **Rule 13:** 51 non-task dirty paths, **all 51 in the report, 0 missing**.
- **Scene mutation:** `git diff Assets/Scenes/` empty; no `.unity` in working tree; only the 2 card prefabs modified (the DiscountBadge, part of the task). Editor left clean: ShellScene active, dirty=False, edit mode, not compiling. My lint-JSON regen is under the gitignored `Docs/Diagnostics/_capture/` — no tracked drift.

## Three break-attempts (all failed)
1. **Visual.** Harshest (only) angle for a modal is the full-screen 1170×2532 frame. The single visual concern — orange node title vs silver built — I chased to ground by re-pulling the live node twice (CSS "Silver" + fresh render silver); the orange was stale spec art, built matches current node. Dual card matches the node treatment (white R450 plate + red −25% badge on top, navy money plate below, gold BUY; coin+number, never the word "RP"). FakeStore `$0.01` is disclosed (device shows ¥100). Nav bar intact, no y-flip (real-flow stills), no broken/missing icons. Could not break.
2. **Geometric / threshold.** Lint fail=0 (not near a threshold). Plate box 160 vs node 152 (~5% over) is the shipped, approved geometry, disclosed. Button gaps exactly 24/24. Font caps within ~1px of ref. The one weight deviation (RP digits SemiBold vs node Medium) is a project-wide constraint — the only static Rubik SDF in the project, already used by every shipped RP plate — disclosed, and rendered cap-height matches ref. Nothing fragile. Could not break.
3. **Spec-intent.** Core intent = real-money pipeline, sandbox-only, money NEVER touches pts, OFF-by-default kill switch, dual pricing + payment modal. Money-never-pts verified LIVE (0 `points_transactions`) AND at code level (24 tests incl. `…writes_no_pts`). Kill switch 409 tested. Test SKU walled off from live players by `min_build 2943` + the withhold rule even with the flag on. Dual pricing + modal built and node-matched. Intent satisfied. Could not break.

## Judged but NOT failed on (surfaced for Cesar)
- **`SELF_REVIEW.md` is the unfilled template.** The pipeline compressed implementer→reviewer on the main thread (both are "main-thread stand-in"); the self-review stage was effectively skipped. Process irregularity, not a deliverable defect — STATUS still traversed legitimately to golfin-reviewer PASS → READY_FOR_REDTEAM.
- **No "skip-modal" video** (acceptance line asks for the single-available-method dual row skipping the modal, on video). Genuinely un-produceable in-Editor: FakeStore makes both methods always available, so the modal always shows; turning FakeStore off withholds the row (deviation 1). The branch (`HandleBuy` → `money && rp ? modal : …`) is unit-tested. Analogous to a device-gated visual; acceptable when code is verified.
- **`iap_enabled` is currently TRUE on prod**, while SPEC says "stays OFF in production." This is Cesar's explicit, documented operational call for the sandbox window ("ASAP"); the kill switch is fail-closed and works, and the test row is walled off by `min_build`. Not an implementer defect.
- **Stale `reference/purchase_modal.png`** (orange title) vs the live node (silver) — spec-hygiene, not the implementer's fault; they correctly followed "node is truth."

## Device-gated FAIL rows (honest, Cesar-gated — NOT counted against this gate)
The 4 report FAIL rows all reduce to the one on-device sandbox pass (TestFlight build ≥ 2943 + sandbox Apple ID → StoreKit `¥100` sheet, the granted-path SQL rows, the replay count, the Store-History screenshot). Physically impossible in the Editor (FakeStore only). The refusal path, idempotency logic, kill switch, and money-never-pts invariant are all verified from Editor + server + code as above. Per the gate's contract these device rows escalate to Cesar and are not a reason to fail.

## Verdict
**ARCHITECT_REVIEW_PASS.** Advances to Cesar's final approval. Remaining work is exclusively the on-device sandbox purchase, which only Cesar can run.
