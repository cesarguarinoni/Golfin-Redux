# Red-Team Review — `tournament_screens_live_bind` — iter-1

**Reviewed:** 2026-06-27 07:59 CEST
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Verdict:** ARCHITECT_REVIEW_PASS

I tried to break this three ways and could not. Every claim was re-derived from
evidence I generated myself (runtime reflection, live backend execution, full
EditMode suite run, my own video-frame extraction). Below is the proof.

---

## Prior rejections replayed

None. This is iter-1; no `CESAR_REJECTION.md` exists. The two RELEVANT scars are
from the immediately-prior task `tournament_backend_bootstrap` (circular-test
gate; synthetic entry) — both re-checked below as priority attacks.

---

## Angle I captured myself (re-shot, not re-used)

Extracted my own frames from `videos/tournament_demo.mp4` (1170×2532, 19.5s) at
0/5/7/9/11/13/15/17/19s via ffmpeg. Inspected f_5s, f_7s, f_13s, f_15s.
- Selection (5s/7s/13s): 6 cards, badges LIVE/ENDED×3/UPCOMING, FREE ENTRY +
  "ENTRY R 500" (gotemba), rewards 20.000/20.000/5.000/20.000. No flip, no broken
  nav bar, full portrait res.
- Leaderboard (15s): live `fp_NNN` bots with varied levels/strokes/rarities,
  GALADRIEL player sticky at rank "--" / 80 strokes (provisional). Not the
  hardcoded 68/70/71/82 stubs.

Capture mechanism audit: `TournamentDemoRecorder` is an editor MENU item, NOT a
`*Gate` scenario in `Scenarios.cs` (diff empty). It boots ShellScene → real
splash/loading/Home → `ShowScreen(TournamentSelection)` → invokes the REAL
`TournamentSelectionCard`'s REAL `Button.onClick` → leaderboard. No
LabScaffold direct-load, no staged camera, no mid-clip camera switch. Permitted.

---

## Attack 1 — CIRCULAR-TEST GATE (the prior-task scar). RESULT: gate holds.

Re-derived via runtime reflection (`script-execute`), not narrative:
```
FIXTURE_ASM=Golfin.Tournaments.Tests ; TEST_METHODS=12
MAPPER_ASM=Golfin.Tournaments
CTRL_ASM=Assembly-CSharp ; CTRL_REFS_MAPPER_ASM=True
TEST_REFS_MAPPER_ASM=True
R1=Upcoming R2=EnteredActive R3=EnteredFinished R4=EnteredFinished
R5=Ending R6=Open R7=Ended
```
- `MapCardStateTests` (12 `[Test]` methods) and the production controller
  `TournamentSelectionScreenController.MapCardState()` (line 197) BOTH call the
  SAME symbol `Golfin.Tournaments.TournamentCardStateMapper.Map` in the SAME
  production assembly `Golfin.Tournaments` — NOT a local/fake copy.
- Removing `TournamentCardStateMapper.Map` breaks BOTH the test and the
  controller (asmdef references confirmed: Tests→Golfin.Tournaments;
  Assembly-CSharp→Golfin.Tournaments). This is the exact opposite of the prior
  task's tautological gate.
- I invoked the real mapper for all 7 SPEC rows at runtime; output matches the
  SPEC §2 table exactly.

**Why the attack failed:** the production type IS the tested type, proven by
assembly reflection, not by reading the report.

## Attack 2 — REAL-ENTRY (Rule 2). RESULT: real widget onClick, not synthetic.

- `TournamentSelectionCard.Awake()` wires `_ctaGoldButton/_ctaSilverButton.onClick`
  → `OnCtaClicked?.Invoke(this)`. Controller subscribes `card.OnCtaClicked +=
  HandleCtaClicked`, which sets `TournamentService.Instance.SelectedTournamentId =
  card.TournamentId` FIRST, then `ShowScreen(Leaderboard)`.
- The demo bot calls `target.GetComponentInChildren<Button>().onClick.Invoke()` on
  the REAL card found in the active scene (filtered by non-empty `scene.name` to
  exclude prefab assets) — no synthetic test-only button.
- Leaderboard `PopulateLive()` reads the same `SelectedTournamentId` and calls
  `GetLeaderboard(id)`. The handoff round-trips through 100% production code.
- Noted (NOT a blocker for this spec): no Home-screen button yet navigates to
  TournamentSelection — that entry point is a pre-existing T7/T9 scaffold gap and
  SPEC §0 explicitly scopes this task to data-binding only. The inter-screen CTA
  handoff that IS in scope uses the real widget path.

## Attack 3 — LIVE DATA (hardcoded-vs-backend). RESULT: fully live.

Executed the real `TournamentCsvLoader` + real prize tables + real mapper via
`script-execute`:
```
TOURNAMENTS=6  NOW=2026-06-27 05:57:59Z
kasumigaseki_open  card=Open(player-entered→LIVE in play) fee=0   topPrizeRP=20000
hirono_invitational card=Ended  fee=0   topPrizeRP=20000
lomond_championship card=Ended  fee=0   topPrizeRP=5000
gotemba_masters     card=Ended  fee=500 topPrizeRP=20000
kisarazu_cup        card=Upcoming fee=0 topPrizeRP=3000
kawana_fuji_open    card=Ended  fee=0   topPrizeRP=20000
```
Every on-screen pill matches a backend value: rewards 20k/20k/5k/20k, gotemba's
paid "ENTRY R 500", 6 tournaments. Reward resolves through prize-table band
lookup (`GetTopPrizeRP`), not constants. Leaderboard shows live bot field
(field_major=30 bots) — varied names/levels/strokes, not stubs.

---

## EditMode tests — run by me

`unity-mcp-cli tests-run` (full EditMode suite — the bridge ignores the filter
and runs everything):
```
Status=Passed  TotalTests=733  PassedTests=730  FailedTests=0  SkippedTests=3
```
0 failures. The 12 MapCardStateTests are confirmed present and discovered (12
`[Test]` methods via reflection) and are part of the 0-failure suite. The 3
skips are unrelated Physics `HoleCompleteDriverTests` (documented Stage-C1
no-ops).

## Banned-change / scene-mutation audit

- `git diff HEAD -- Assets/Scripts/Physics/` → empty (Rule 7).
- `git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → empty (no
  `*Gate`).
- `git diff --stat HEAD -- Assets/Scenes/` → empty (no `.unity` mutation).
- `*M_Splash*`, `*LabScaffold*` → empty.
- `git status --porcelain` matches the IMPLEMENTER_REPORT "Files modified or
  created" table exactly (4 modified .cs + 3 created .cs(+meta) + task docs). No
  drift outside the task folder (Rule 13).
- Every `.cs` ships its `.cs.meta`.

## Report-integrity notes (not blockers)

- Reviewer's narrative said gotemba "ENTRY R 600"; the actual frame + CSV show
  500. Transcription slip in the reviewer report, not a work defect.
- The only exception in Editor.log is MY OWN failed `Compose()` probe (EditMode
  has no SaveDataHost) — an artifact of my testing, not the recorded session.

---

## Verdict

**ARCHITECT_REVIEW_PASS.** The two scars (circular-test gate, synthetic entry)
that sank the prior task are concretely, reflectively disproven here. Live data,
real-widget CTA handoff, 0-failure test suite, clean audit. Single strongest
piece of evidence: runtime reflection proving test + production controller both
bind the SAME `Golfin.Tournaments.TournamentCardStateMapper.Map` symbol
(`CTRL_REFS_MAPPER_ASM=True`, `TEST_REFS_MAPPER_ASM=True`, `TEST_METHODS=12`).
