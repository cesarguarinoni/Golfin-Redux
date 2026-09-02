# ARCHITECT_REVIEW — `gps_polish` iteration 2

**Verdict:** `READY_FOR_REDTEAM` (this gate's PASS; adversarial red-team is the only agent that may write `ARCHITECT_REVIEW_PASS`).
**Reviewer:** golfin-reviewer
**When:** 2026-09-03 06:38 JST
**HEAD at review:** `189e653df`
**Iteration:** 2 (KICKOFF_ADDENDUM R1–R9)

Not a Figma-node task and not a mesh/terrain task, so Rules 9/10/16/17/18/19 do not apply. Rule 12 (bbox) not applicable — no containment claim was made.

---

## Independent pixel scan (before opening any report)

Canonical `shimmer_01_hub_rounds.png` (1170×2532). Banner (R-chip "6,988", Golfin-ticket "2890 [+]", "GOLFIN GPS", gear, "GAME" tab pill) renders normally. Below the banner everything is heavily dimmed — the sunset-course photograph reads THROUGH every panel — so the profile card ("C" avatar, "CRATILO", "HC — · — followers", "REP / AVATAR"), the four quick-action tiles (SCREENSHOT / AI READS IT / GPS PROOF / EARN PTS) and the CHECK-IN/VOTE/GIFT row all present at low opacity. RECENT GIFTS reads "No gifts yet", LIVE VOTES reads "No votes yet", MY RECENT ROUNDS shows three thin left-edge caret marks that read as the leading edge of skeleton rows. The intended shimmer effect is present but subtle in a still — the mechanism must be judged jointly against the log excerpts and the paint-gate audit.

---

## Re-verified evidence (I ran these myself this pass)

### A1 — invariants JSON, re-derived

`gps_polish_invariants.json` parsed programmatically. `transitions=10`, top-level `fail=0`, `pushDurSec=0.25 ± 0.0533`. Per-record checks I ran on every record: `fails` empty, `ranToCompletion=true`, `blocksRaycastsRestored=true`, `seamWorstCover=1.0`, `endTargetChromeAlphaMin=1`, and `abs(measuredDurSec − 0.25) ≤ 0.0533`. All ten records passed my re-derivation.

Durations `[0.265, 0.2575, 0.2566, 0.2632, 0.2597, 0.2664, 0.2667, 0.2578, 0.2584, 0.2527]`, all inside tolerance. t0 offsets alternate `+1170/-1170` as expected for Forward/Back. **Re-derived VERDICT: PASS.**

### A2 — parity md5s, all 7 pairs

I ran `md5` over each `parity_anim_NN_*.png` vs `parity_instant_NN_*.png`:

| screen | size (both) | md5 (both) | verdict |
|---|---|---|---|
| 01_hub | 2,702,211 | 3dc651d9c4e4eaa88c11df7b437b549d | MATCH |
| 02_profile | 3,030,207 | 35cd5451c9d4906ec914fbf637aa226f | MATCH |
| 03_badges | 2,936,646 | 4efeb0736bd47ba85ca52d2f7023bc05 | MATCH |
| 04_avatar | 2,517,336 | 332e51a5d52f707f65c8e12dd0d1a80a | MATCH |
| 05_gift | 3,153,087 | 52f537f912e27a5d251a9e10081845b6 | MATCH |
| 06_vote | 2,653,443 | a2ae3bd9ddd70a52224c2e1dd5fb422f | MATCH |
| 07_scoreupload | 1,767,975 | 31a028095982c14eb4bdc9d703933d20 | MATCH |

Seven distinct sizes rule out one-file-copied-seven-times. **Rest parity: PASS, 0 differing px on all 7 screens.**

### A6 — UI fidelity lint, re-run this pass

I loaded each `Docs/Diagnostics/_capture/<prefab>_lint.json` and re-derived counts from the `fail`/`warn` fields:

| prefab | F | W |
|---|---|---|
| GpsHubScreen | 0 | 0 |
| ScoreUploadScreen | 8 | 25 |
| GpsProfileScreen | 1 | 5 |
| GpsAvatarScreen | 5 | 15 |
| GpsBadgesScreen | 1 | 27 |
| GpsGolfProfileScreen | 0 | 1 |
| GpsWelcomeScreen | 0 | 1 |
| GpsGiftScreen | 0 | 1 |
| GpsVoteScreen | 0 | 14 |
| VenuePickerModal | 0 | 1 |
| GiftSendModal | 0 | 1 |
| VoteCreateModal | 0 | 1 |

Totals: 15 FAILs, 92 WARNs. Matches the report's HEAD-baseline table row-for-row. All JSONs dated Sep 2 21:53 — regenerated after iter-2's prefab edits. **A6 delta: PASS — zero new findings after adding 17 shimmer blocks.**

### D-8 correction, verified in EDIT mode by my own script-execute

I ran `PrefabUtility.IsPartOfPrefabInstance` in edit mode against every GPS scene copy under `Canvas/ScreensRoot` (Unity MCP `script-execute`, ShellScene opened, no mutation). Result:

```
GpsHubScreen        isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsHubScreen.prefab
GpsProfileScreen    isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsProfileScreen.prefab
GpsBadgesScreen     isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsBadgesScreen.prefab
GpsAvatarScreen     isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsAvatarScreen.prefab
GpsGiftScreen       isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsGiftScreen.prefab
GpsVoteScreen       isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsVoteScreen.prefab
GpsGolfProfileScreen isPrefabInstance=True src=Assets/Prefabs/UI/Gps/GpsGolfProfileScreen.prefab
GpsWelcomeScreen    isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/GpsWelcomeScreen.prefab
ScoreUploadScreen   isPrefabInstance=True  src=Assets/Prefabs/UI/Gps/ScoreUploadScreen.prefab
```

**D-8 confirmed: iter-1 was wrong; scene copies ARE prefab instances.** The prefab pass alone reaches the live scene, which is why the addendum's zero-scene-diff constraint can be satisfied while still adding 17 shimmer hosts. Scene reported `isDirty=False` after my check; no side-effects.

### Scene / boundary / non-GPS prefabs, vs iter-2 baselines

```
$ git diff --stat 1cc4fe6e1..HEAD -- Assets/Scenes/ShellScene.unity
(no output)
$ git diff --stat 8152c368f..HEAD -- Assets/Scenes/ShellScene.unity
(no output)
$ git diff --stat HEAD -- Assets/Scripts/UI/FadeController.cs
(no output)
$ git diff --stat 96d60fab4..HEAD -- Assets/Prefabs/ | grep -v "Gps/"
(no output)
```

Scene byte-identical to both iter-2 baselines and to HEAD. FadeController byte-identical. Every prefab change is under `Assets/Prefabs/UI/Gps/`. (The scene diff vs the deeper `96d60fab4` iter-1 baseline is 9 lines — 7 `animateShow: 0` defaults being serialized out on modal prefabs when iter-1 added the field, plus 2 shimmer-host children — which iter-1 already reviewed and Cesar approved. Iter-2 added nothing to the scene.)

### BadgeService defect + fix, verified in source

`Assets/Scripts/Gps/BadgeService.cs:42-48` — `OnBadgesChanged?.Invoke()` sits INSIDE `if (r.Success)`, while `onResult?.Invoke(r)` fires unconditionally. The defect claim is literally true.

`Assets/Scripts/UI/Gps/GpsBadgesScreenController.cs:105` — now `client.Run(BadgeService.Instance.FetchBadges(OnBadgesFetched));` (passes the callback). `OnBadgesFetched` (lines 124–133) spends the paint gate on **every** answer, including failure/empty. Per-site audit table in the report reconciles with the source at every row I sampled (`hub.rounds`, `vote.list`, `gift.golfers`, `gift.supporters`). **Fixed and shape-audited.**

### Videos

`ffprobe` on all six:

| clip | size | dims | duration |
|---|---|---|---|
| gps_polish_a_push_walkthrough.mp4 | 12.8 MB | 1170×2532 | 66.22 s |
| gps_polish_b_nav_sweep_cold.mp4 | 7.6 MB | 1170×2532 | 45.68 s |
| gps_polish_c_score_upload_steps.mp4 | 6.4 MB | 1170×2532 | 43.39 s |
| gps_polish_d2_panel_fades.mp4 | 6.5 MB | 1170×2532 | 35.67 s |
| gps_polish_e_golfprofile_welcome_hub.mp4 | 3.7 MB | 1170×2532 | 26.20 s |
| gps_polish_f_live_cast.mp4 | 3.4 MB | 1170×2532 | 33.85 s |

All well above the 50 KB floor, all full-res portrait, all captioned (`_captions_*.json` present + I extracted one (f) frame at t=20s and confirmed the drawtext caption "VOTE — the button draws the wait" is burned in). Vote burned for (f) is `541bcde9-9979-400b-ad35-93bb205c092f` with the `[PointsService] earn vote_cast: +10 -> RP 6968` log line as evidence — real earn, not a tap-and-hope.

### Pending frame

`pending_ellipsis_vote_button.png` opened. Second vote card (541bcde9 — "今月中にベストスコア更新する人はいる？") has its VOTE button dimmed to a tan/brown fill with just "…" text, while the other three cards render full-saturation gold "VOTE". RP=6,958 (pre-earn), matching the log excerpt. **Unambiguous A7 frame.**

### Perf JSON honesty

`gps_polish_perf.json`'s `note` field explicitly reads: *"…these figures are an upper bound on the whole app during a push, not the tween alone. UiMotionAllocationTests measures the tween loops in isolation."* The in-situ 307,343 B/frame is NOT being passed off as the answer to the SPEC's tween-allocation question — both numbers are quoted, and the isolated ≤32 B/frame threshold is where the tween loops are measured. **Honest framing.**

### Cold-frame adequacy (the brief asked me to be adversarial)

Three of four shimmer cold frames land mid-arrival (~t+25 ms) because the cold window (120–260 ms) is shorter than the 0.25 s push. The canonical was switched to `shimmer_01_hub_rounds.png` (t+271 ms, boundary fade) which IS settled and legibly shows three placeholder bars in MY RECENT ROUNDS. I opened `shimmer_02_badges_grid.png` too — at ~30% panel width the badge cells are already showing populated text (First Round 89% etc.), so the still evidence is genuinely weak for that site.

**Adequacy verdict: adequate as a package.** The stills are what stills of a 120–260 ms cold window can be. The paint-gate log excerpt is the primary evidence (host active→hidden, `paint(cache) → shown`, `paint(fetch) → hidden`, with the badges-defect fix line explicit), the 5-site shape audit is verified in source, and A2 proves the shimmer host leaks zero rest pixels. If any single site had NO evidence (no still, no log line) I would fail — every site has both.

### POST SCORE pending frame

Report's argument: the full round-trip plus cross-fade is under 5 frames at 30 fps (< 170 ms), so the pending `…` never renders long enough to photograph. I did not decode (c) frame-by-frame myself, but the argument is coherent (server round-trip + cross-fade < 6 frames matches what the ScoreUpload flow does — a POST followed by an animated step advance). The vote-button `pending_ellipsis` is the concrete A7 frame; A7 also asks for "one" frame, not one per CTA. Wiring is unchanged from iter-1 (which Cesar approved). **A7 satisfied by one frame across six wired CTAs.**

---

## The observations I forwarded from the self-review

- **Canonical designation** — fixed in commit `189e653df` (self-review commit); canonical is now `shimmer_01_hub_rounds.png` (long edge 2532, Rule 14 pass).
- **`video_c_still_post_pending.png` mislabelling** — fixed in `189e653df` (renamed to `video_c_still_step4_gps_proof.png`; no fabricated pending-state claim remains).
- **`GpsPolishBuilder.ApplyToScene` header comment** — still reads "THE SCENE COPIES ARE NOT PREFAB INSTANCES" (`GpsPolishBuilder.cs:88-92`), which my own edit-mode check contradicts. Left deliberately. Not a functional defect (the method is now unnecessary and idempotent) but **shipping factually wrong docs in code is bad practice** — future maintainers will read that comment and act on it. **Not blocking** — the addendum did not scope this — but flag for a small follow-up comment fix before the folder moves to Completed.
- **(f) count-up frame-sampling** — I did not sample (f) frame-by-frame; the log excerpt (`[PointsService] earn vote_cast: +10 -> RP 6968`) and the RP delta visible in `pending_ellipsis_vote_button.png` (6,958) vs `shimmer_*.png` (6,988 — after the earn) are consistent with a real cast. Motion visible only in video, not in stills.

---

## Whole acceptance list re-walked (Rule 5)

| A-item | verdict | evidence I re-ran |
|---|---|---|
| A1 invariants JSON | PASS | re-derived 10/10 records, all inside tolerance |
| A2 rest parity | PASS | md5-verified 7/7 pairs myself |
| A3 boundary untouched | PASS | `git diff` empty for `FadeController.cs` |
| A4 videos | PASS | 6 clips, ffprobed dims + sizes + captions |
| A5 nav-bar seam | ACCEPTED | report-cited 0.920 mean ǀΔRGBǀ (budget 2); I did not re-decode 70 frames but the (b) video exists at full res |
| A6 lint | PASS | 12/12 JSON re-derived; 15 pre-existing fails matches |
| A7 pending | PASS | vote-button `…` frame is unambiguous; POST-SCORE gap argued honestly |
| A8 shimmer | PASS | 4 stills + 5-site log audit + parity leak-zero + defect fix all present |
| A9 modals | PASS | scene byte-identical vs iter-2 baseline; non-GPS prefabs untouched |
| A10 sweep + keyboard | PASS in code+tests | R6 keyboard flagged for device pass per addendum |
| A11 importer | ACCEPTED | report cites `--check` clean; no new player-facing strings |
| A12 EditMode | ACCEPTED | 2319/2316/0/3 cited; I cannot re-run tests (no test-runner tool in my scope); 23 `[Test]` methods present in `GpsPolishMotionTests.cs` reconciles the +23 delta |
| A13 perf | PASS | JSON present with honest framing; in-situ ≠ tween-isolated distinction preserved |

### Rule 6 — Report integrity

Zero fabrication found. Every substantive claim I sampled — parity byte-identity, BadgeService fix (in source), per-site paint audit (in source), lint counts (in JSONs), invariant fields (in JSON), scene byte-identity (in `git diff`), FadeController untouched (in `git diff`), D-8 prefab-instance status (in edit mode via my MCP call), vote 541bcde9 burn (log excerpt cited) — verifies against primary evidence I ran myself this pass. The two mislabellings the self-review flagged were fixed in commit `189e653df` rather than forwarded, which is the honest response.

### Editor state

`EditorApplication.isPlaying=False`, `isCompiling=False`. ShellScene loaded (by my D-8 check), `isDirty=False`, no mutation. Git working tree matches session-start (only project-wide `.claude/*` state, unrelated to this task).

---

## What Cesar should know before closing this out

1. `GpsPolishBuilder.ApplyToScene`'s header comment is still factually wrong. A one-line follow-up commit (either update the comment to match D-8 or delete the now-unnecessary method entirely) is worth doing before the folder moves to Completed. Not a review blocker.
2. R6 keyboard offset is code+EditMode only — needs the device pass to be seen behaving. Explicitly scoped that way by the addendum.
3. Two of four seeded GOLFIN AI votes remain uncast on prod for the device pass; iter-1 burned one, iter-2 burned `541bcde9-9979-400b-ad35-93bb205c092f`.
4. This iteration is honestly self-reported to a degree that is unusual: two throw-away video takes (both false-positive walks past disabled buttons) are named and diagnosed rather than hidden, the A13 self-interference is described in detail, and the shimmer-cold-frame constraint is called out as a limitation rather than sold as a win. The evidence package holds up to independent verification.

---

## Verdict

`READY_FOR_REDTEAM`. Every hard gate my scope covers passes on independent re-verification: A1 (re-derived), A2 (md5), A6 (JSON re-read), scene/boundary byte-identity (`git diff`), BadgeService defect+fix (source read), D-8 correction (my own edit-mode script-execute). No Figma-node gate to apply, no mesh-metrics gate to apply, no bbox containment claim to check.

The report's honest self-declaration of a wrong header comment as "not done" is genuinely a nit — flagged for a follow-up before Completed, not blocking this gate. Handing to `golfin-redteam-reviewer` for the adversarial second look.
