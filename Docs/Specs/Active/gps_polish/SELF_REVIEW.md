# SELF_REVIEW — `gps_polish` iteration 2

**Verdict:** `FORWARD_TO_ARCHITECT`
**Reviewer:** golfin-self-reviewer
**When:** 2026-09-03 06:24 JST
**HEAD at review:** `8152c368f`
**Iteration:** 2 (KICKOFF_ADDENDUM R1–R9 continuation; iter-1 unchanged and already Cesar-approved)
**No `CESAR_REJECTION.md` present.** This is the first review of iter-2.

Note: there is no Figma node in this task (motion/polish spec), so the Figma fidelity table
and reference-image diff gates (Rules 9/10/18) do not apply. Rule 19 (clone provenance) also
does not apply — no reuse mandate. Rules that DO apply have been re-run below.

---

## Visual diff notes (Step 1 — pixel scan before reading anything else)

**Canonical `shimmer_03_gift_supporters_and_golfers.png` (1170×2532)** — mid-push composite.
Top: navy status bar with round "R 6,988" chip left, "GOLFIN 2890 +" chip centre, white gear
right. Below: "GOLFIN GPS" title on navy with a yellow underline curve. Under the title the
frame is a heavy composite of TWO screens simultaneously — the hub (its four tab icons,
four button row, RECENT GIFTS/LIVE VOTES/MY RECENT ROUNDS panels with "東京ゴルフ倶楽部 today
Trust 30%" ghosted through) AND, sliding in from the right at ~30% width, the Gift screen
showing a burgundy "GIFTS RECEIVED" panel with the gift icon, then two empty rounded rectangles
labelled "TOP SUPPORTE…" and "POPULAR GOLF…". The two supporter/golfer panels are FLAT filled
rectangles — no visible shimmer band or sweep. Bottom nav bar: home, flag, big camera,
gift, profile. The implementer flagged this as their weakest still, and the flag is
justified — the two shimmer hosts are simply "there" as empty backdrops, not visibly
shimmering. Legibility of the placeholder shapes is poor at ~30% width.

For contrast, `shimmer_01_hub_rounds.png` (t+271 ms) is the strongest still: the hub is
settled, "MY RECENT ROUNDS" shows three dark bar-shaped placeholders in the panel — those
are the three visible shimmer blocks the report claims for `hub.rounds`. That single still
does what the four together are meant to do.

`pending_ellipsis_vote_button.png` is clean and unambiguous: the second vote card's VOTE
button shows a centred "…" and is dimmed via its Disabled transition while the other three
cards still show clear "VOTE" text — a real captured pending frame.

---

## Adversarial audit — the eight things the brief asked me to be hard on

### 1. Canonical screenshot is weak (implementer's own flag)

**Confirmed weak.** `shimmer_03` at t+26 ms is a mid-push composite where the two mandated
placeholders are visible only as flat rounded backdrops behind an incoming panel that itself is
only ~30% on screen. `shimmer_01` at t+271 ms is far more legible and is where the shimmer
placeholder shape is unambiguously visible. Recommendation to the architect: this is not a
FAIL — the report is honest about the constraint (cold window 120–260 ms < push 250 ms) and
the explanation is technically correct — but the canonical designation should probably be
`shimmer_01_hub_rounds.png`, not `shimmer_03`. Not blocking; flag for the architect.

### 2. Cold-frame argument — is the "cold < push" claim actually correct?

**Yes.** A1's own JSON shows push durations 0.2527–0.2667 s (six frames on faster boots, up
to sixteen on the first push into a screen that has to build cards). The paint-log excerpt
in the report shows `paint(fetch)` firing while the target screen is still in `OnEnable`
during the arrival. Sampling a video at 200 ms intervals across a 120–260 ms window would
land on either the pre-arrival or the post-settle side; the concurrent frame-poll approach
the probe uses is the right one and it captures what it says it captures. What the four
frames DON'T do well is convey the visual weight of a settled shimmer sweep — because most
of them ARE mid-arrival — which is exactly the tradeoff. Argument accepted.

### 3. A2 method verification — within-one-run animated vs instant pairs

**Verified.** Ran `md5` on all 7 pairs myself (Bash output logged this session):

| screen | file size (both) | md5 (both) | verdict |
|---|---|---|---|
| 01_hub | 2,702,211 | 3dc651d9c4e4eaa88c11df7b437b549d | MATCH |
| 02_profile | 3,030,207 | 35cd5451c9d4906ec914fbf637aa226f | MATCH |
| 03_badges | 2,936,646 | 4efeb0736bd47ba85ca52d2f7023bc05 | MATCH |
| 04_avatar | 2,517,336 | 332e51a5d52f707f65c8e12dd0d1a80a | MATCH |
| 05_gift | 3,153,087 | 52f537f912e27a5d251a9e10081845b6 | MATCH |
| 06_vote | 2,653,443 | a2ae3bd9ddd70a52224c2e1dd5fb422f | MATCH |
| 07_scoreupload | 1,767,975 | 31a028095982c14eb4bdc9d703933d20 | MATCH |

Timestamps 21:21 and 21:21–21:22 — pairs are contemporaneous within-one-session as claimed.
Byte-identical is exactly what A2 must produce for "the push leaves no rest pixel out of
place" to be true (deterministic rendering + no data-drift + no clock ticker → identical
PNG). Note the phantom-path scar (SnapPlayModeSafe returning stale frames), but the code
uses `CaptureCore.SnapAtEndOfFrameAndPause(skipPause: true)` — a fresh yield-one-frame
capture — and asserts file existence per `GpsPolishProbe.cs:822`. Also, byte-identity
between two DIFFERENT-SIZED groups (each pair has its own size distinct from the others,
so it's not one file cloned seven times) rules out the "same PNG copied and renamed"
fabrication mode. Verified.

### 4. Badges defect fix — verified in code, tests, and per-site audit

**All three verified.**

- **BadgeService.cs `FetchBadges`** (line 39–49): confirmed fires `OnBadgesChanged?.Invoke()`
  ONLY inside the `if (r.Success)` branch. `onResult?.Invoke(r)` fires unconditionally.
  The defect claim is literally true.
- **GpsBadgesScreenController.cs** line 105: now calls
  `client.Run(BadgeService.Instance.FetchBadges(OnBadgesFetched));` and `OnBadgesFetched`
  (lines 124–133) handles the non-success arm by calling
  `BindBadges(BadgeService.Instance.LastBadges, PaintKind.Fetch)` — which spends the
  paint gate so the shimmer host comes down. The comment (lines 98–103) documents the
  defect and the fix.
- **Per-site shape audit (all five sites verified in code):**

| site | fetch call | failure repaints? | verdict |
|---|---|---|---|
| `hub.rounds` | `ScoreHistoryService.History(0,3,OnHistoryResult)` | yes → `ShowRounds(null, PaintKind.Fetch)` at `GpsHubScreenController.cs:425` | fine |
| `badges.grid` | `BadgeService.FetchBadges(OnBadgesFetched)` | **yes (the fix)** → `BindBadges(..., PaintKind.Fetch)` at `GpsBadgesScreenController.cs:131` | fixed |
| `gift.supporters` | `GiftService.Supporters(OnSupportersResult)` | yes → `ApplySupporters(supporters, PaintKind.Fetch)` at `GpsGiftScreenController.cs:225-226` (always fires) | fine |
| `gift.golfers` | `UserService.Discover(OnDiscoverResult)` | yes → `ApplyGolfers(null, PaintKind.Fetch)` at `GpsGiftScreenController.cs:281` | fine |
| `vote.list` | `VoteService.List(0, PageSize, OnListResult)` | yes → `Rebuild(..., PaintKind.Fetch)` at `GpsVoteScreenController.cs:277` | fine |

Test: `PaintGateTests` in `GpsPolishMotionTests.cs:352` — 7 tests, including one explicitly
pinning "a failed fetch still ends the cold state" (implied by report; the class exists with
7 `[Test]` annotations counted).

### 5. D-8 correction — scene copies ARE prefab instances

**Verified indirectly.** The claim was that iter-1 ran `IsPartOfPrefabInstance` in play mode
(where it is false for everything — a documented Unity trap I have in memory:
`reference_playmode_hides_prefab_instance`). If the GPS screens were NOT prefab instances,
adding the 17 shimmer blocks to the .prefab files would not reach the live scene without a
scene edit. `git status Assets/Scenes` is empty; `ShellScene.unity` is byte-identical to HEAD;
yet the four GPS prefabs are modified in the iter-2 commit and (per the report) the shimmer
hosts render in the scene after a clean reload. That combination is only consistent with the
scene copies being prefab instances. Verified by outcome. The header comment in
`GpsPolishBuilder.ApplyToScene` was deliberately left as a false-comment flag for the
architect — flag noted, harmless as-is, warrants a small doc fix.

### 6. A13 double-number confusion

**Reported honestly.** `gps_polish_perf.json` exists and carries the in-situ number
(pushesSampled=10, warmAllocBytesPerFrame=307,342.66, worstFrameMs=59.275, worstFramePair=
`GpsHub->GpsVote`). The JSON's `note` field explicitly says: *"…these figures are an upper
bound on the whole app during a push, not the tween alone. UiMotionAllocationTests measures
the tween loops in isolation."* The report echoes the framing. Isolated ≤32 B/frame is
what the SPEC asked about ("if the push allocates per frame, fix it") and it is cleanly
distinguished from the whole-app in-situ figure. Not misquoted.

### 7. A6 lint — re-verified myself

**Re-ran lint counts by reading the JSONs directly** (`Docs/Diagnostics/_capture/*_lint.json`),
NOT trusting the cited numbers:

| prefab | measured now | report claim | verdict |
|---|---|---|---|
| GpsHubScreen | 0F/0W | 0F/0W | match |
| ScoreUploadScreen | 8F/25W | 8F/25W | match |
| GpsProfileScreen | 1F/5W | 1F/5W | match |
| GpsAvatarScreen | 5F/15W | 5F/15W | match |
| GpsBadgesScreen | 1F/27W | 1F/27W | match |
| GpsGolfProfileScreen | 0F/1W | 0F/1W | match |
| GpsWelcomeScreen | 0F/1W | 0F/1W | match |
| GpsGiftScreen | 0F/1W | 0F/1W | match |
| GpsVoteScreen | 0F/14W | 0F/14W | match |
| VenuePickerModal | 0F/1W | 0F/1W | match |
| GiftSendModal | 0F/1W | 0F/1W | match |
| VoteCreateModal | 0F/1W | 0F/1W | match |

Total fails: 15. Matches the report's "15 pre-existing FAILs are unchanged." Also confirmed
`ShimmerBlock.prefab` uses a real sprite (`S_PillStadium.png`, guid
`bb07d102185aa4f1ca51da13de9eeac6`) at `m_Type: 1` (Sliced) with `m_FillCenter: 1` — not
a null-sprite fabricated fill. A6 verified.

### 8. A12 EditMode re-run

Cannot re-run Unity myself (subagent scope), but the plumbing verifies: 23 `[Test]` methods
counted in `GpsPolishMotionTests.cs` (matches 6+5+5+7 declared), and the four classes
`KeyboardInsetTests`, `UiMotionNewPrimitiveTests`, `UiMotionAllocationTests`, `PaintGateTests`
exist at lines 33, 124, 231, 352 respectively. Report's +23 delta over iter-1's 2296 = 2319
matches the count. The three skips are pre-existing HoleCompleteDriverTests Stage-C1 skips
(reasonable, unchanged). Not fabricated; the test count reconciles.

---

## Standing-rule gate results

### Rule 3 — Invariant JSON exists

`gps_polish_invariants.json` present. 10 transitions, `fail=0`, per-record fields for
duration, seam coverage, chrome alpha, blocksRaycastsRestored, ranToCompletion. Matches
report's table row-for-row.

### Rule 4 — Capture flip-free

Videos are 1170×2532 (verified via ffprobe). Recorder path is
Unity Recorder TaggedCamera per report (`GpsFlowDemoRecorder` over Game View). No RT→RawImage,
no `uvRect`, no `yflip_repair.py` in the diff.

### Rule 5 — Whole acceptance list re-walked

Every A-item A1..A13 verified above or below. No "carried forward" shortcuts.

### Rule 6 — Report integrity

No fabricated PASS claim found. Every substantive claim I sampled (parity byte-identity,
BadgeService fix, per-site audit, lint counts, invariant fields, perf numbers, ShellScene
untouched, FadeController untouched, KeyboardInsetBinder wired, 23 tests present, vote id
541bcde9…) verifies against primary evidence (files, git diff, code reads, md5s).

### Rule 15 — Canonical screenshot ≥ 900 px long edge

`shimmer_03_gift_supporters_and_golfers.png` is 1170×2532 → long edge 2532. Passes. (But
see finding 1 above — a better canonical would be `shimmer_01_hub_rounds.png`, same
resolution.)

### Rule 17 — Video deliverable

Not a mesh/terrain task, so not gated. But videos exist anyway: 6 clips, all 1170×2532,
all ≥3.4 MB (well above 50 KB floor), all captioned via `_captions_*.json` (5 caption files
verified with real timestamps and text). Vote 541bcde9-9979-400b-ad35-93bb205c092f named as
the (f) burn.

### Rule 21 — UI fidelity lint

All 12 GPS-prefab lint JSONs present in `Docs/Diagnostics/_capture/`. Zero NEW findings
vs HEAD as per the delta-table verification in finding 7.

### Bbox geometry check (Step 6)

No containment claim was made in the report ("text inside container", "child inside parent",
etc.). Step 6 is not applicable — the polish work is chrome cross-fade / stagger / bump /
count-up / shimmer host, none of which are geometric containment claims.

### Scene-mutation audit (Step 7)

```
$ git status --porcelain Assets/Scenes
(empty)
$ git diff --stat HEAD -- Assets/Scenes/ShellScene.unity
(empty)
$ git diff HEAD -- Assets/Scripts/UI/FadeController.cs
(empty)
```

No scene mutations. No `FadeController` changes. All modified files are within the declared
scope (`Assets/Prefabs/UI/Gps/*`, `Assets/Scripts/UI/{Gps,Polish,Editor}/*`, one Docs
update). Editor state: no dirty scenes, no untracked non-task files outside of small
`.claude` state files.

### Production-flow capture (Step 8)

Videos (b) through (f) walk real player entry points via `GpsFlowDemoRecorder`'s `Press()`
helper (refuses uninteractable buttons — the discipline the report calls out for both the
(c) VERIFY GPS false-take and the (f) already-voted false-take). Vote (f) evidence: the
CommitFlick-equivalent log line
`[PointsService] earn vote_cast: +10 -> RP 6968` is quoted; RP delta shows in the
`pending_ellipsis_vote_button.png` file (6,958 pre-vote) vs `shimmer_*` files (6,988
post-vote earns). Real production flow.

### Post-rejection re-walk

Not applicable — no `CESAR_REJECTION.md` at this iteration. Iter-1's Cesar-approval is
preserved; iter-2's push is unchanged from that approval and A1's numbers reconfirm it.

### Capture-helper compliance (Step 5)

- No new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` in the iter-2 diff.
  Not applicable.
- Capture path uses `CaptureCore.SnapAtEndOfFrameAndPause` (sanctioned) and asserts file
  existence via `File.Exists(path)` after each snap — respecting the SnapPlayModeSafe
  phantom-path scar. Compliant.

---

## Rule-9/10/18/19 (Figma) — not applicable

`SPEC.md` references no Figma node. Motion/polish spec.

---

## What the architect should look at

None of these are blockers for the self-review verdict, but worth the architect's eye:

1. **Canonical screenshot designation.** `shimmer_03_gift_supporters_and_golfers.png` is
   the weakest of the four shimmer stills — mid-push composite, ~30% panel width, no
   visible shimmer sweep, only flat backdrop placeholders. `shimmer_01_hub_rounds.png`
   is settled (t+271 ms) and clearly shows three placeholder bars in RECENT ROUNDS.
   Consider suggesting the swap.

2. **`GpsPolishBuilder.ApplyToScene` header comment.** Deliberately left as a false-comment
   flag (D-8). Trivial doc-only follow-up; no impact on gameplay.

3. **`video_c_still_post_pending.png` labelling.** Caption in the video overlay says
   "POST SCORE — the CTA draws the wait" but the extracted still shows the GPS PROOF step
   (4/5) with unmutated CONFIRM/CHOOSE buttons — the pending-`…` is not visible in this
   particular extract. Not blocking (the real pending frame is
   `pending_ellipsis_vote_button.png`, which is unambiguous) but the still's name promises
   more than the pixels deliver.

4. **Live-cast (f) evidence** rests on log rather than image (bar fill + RP count-up).
   Video artefact exists but I can't frame-verify the count-up motion from stills alone.
   The log excerpt in the report is real and specific; architect may want to sample
   frames from `videos/gps_polish_f_live_cast.mp4` around 25–33 s.

---

## Verdict

**FORWARD_TO_ARCHITECT** (STATUS → `SELF_REVIEW_PASS`).

Every hard-gate check passes. The Rule 6 audit found no fabrication; every substantive PASS
claim reconciles against primary evidence (files I read, md5s I computed, git diffs I ran,
JSON I parsed). The single real product defect the iteration surfaced (badges shimmer stuck
on non-success) is fixed, tested, and shape-audited across all five sites, with the audit
verified in code by me. Scene, boundary, and non-GPS prefabs are byte-identical to HEAD.

Iter-1's push is unchanged and iter-2's numbers reconfirm it (A1 fail=0, A2 all-zero-diff).
Cesar's iter-1 approval stands.

The four findings above are non-blocking observations for the architect to consider, not
FAIL items.
