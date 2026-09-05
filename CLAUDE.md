# [CLAUDE.md](http://CLAUDE.md)

This file provides guidance to Claude Code ([claude.ai/code](http://claude.ai/code)) when working with code in this repository.

> **‼️ HOW TO END EVERY RESPONSE — READ THIS BEFORE ANYTHING ELSE**
>
> The last line of every response must be the file-summary table (or, if no files were touched, the most concrete next step). **Do not append any closer, sign-off, farewell, well-wish, callback, or recurring catchphrase after it.** This explicitly forbids the phrase "See you space cowboy" and every variant of it (no "space cowboy", no "Bebop", no "see you", no goodbye in any language). Cesar will say goodbye when he's done; until then, the response ends on the work.
>
> If you find yourself about to type a closing line that isn't the file table or a next-step, **delete it before sending**. This rule overrides any pattern from past sessions, jsonl history, or older `lessons.md` entries. It is non-negotiable.

## Multi-Agent Workflow (NEW 2026-04-28)

UI tasks go through an automated pipeline of three subagents. Cesar's only job is to kick off and approve at the very end. **Do not invent your own workflow when this one applies.**

For SMALL tasks where the full pipeline is overkill (bug fixes with obvious solutions, single-line tweaks, CSV field additions), use the lightweight workflow at `Docs/Specs/Quick/` instead — see `Docs/Specs/Quick/README.md`. Quick tasks skip the subagent chain entirely; Cesar eyeballs the result.

### The pipeline

```
Cesar (with Architect Claude on claude.ai) -> writes SPEC.md
                                            -> golfin-implementer (builds + screenshots + self-PASS/FAIL checklist)
                                            -> golfin-self-reviewer (catches false PASSes; routes back or forward)
                                            -> golfin-reviewer (visual fidelity + mesh metrics + cross-cutting; PASS -> READY_FOR_REDTEAM)
                                            -> golfin-redteam-reviewer (adversarial gate; ONLY it writes ARCHITECT_REVIEW_PASS)
                                            -> Cesar (final approval -> DONE)
```

Spec authoring is done by Cesar with the human Architect (claude.ai chat), NOT a subagent. The subagent chain handles implementation, self-review, and final review only.

### Surface iteration review images in the main chat (Cesar standing rule, 2026-06-09)

**Every time the implementer finishes an iteration (STATUS → `READY_FOR_SELF_REVIEW` or `READY_FOR_ARCHITECT_REVIEW`), the orchestrator (you, the main Claude Code thread) MUST display that iteration's canonical review image inline in the main chat BEFORE dispatching the next reviewer subagent.** Use the `Read` tool on the `Canonical screenshot:` path from `IMPLEMENTER_REPORT.md` (the `route_subagent.py` hook prints a `📸 SURFACE IN CHAT FIRST:` line with the exact path). For iterations whose deliverables are videos, also extract and display one representative still per new video and give Cesar the local `videos/` path.

Rationale (Cesar): *"I'm faster than the reviewer most of the time but I'm not always available."* Surfacing the image gives Cesar the earliest possible window to catch an issue and interrupt — saving the whole review chain — without making the pipeline depend on him being present.

Rules:
- **Non-blocking.** Display the image, then immediately proceed to dispatch the reviewer. Do NOT wait/pause for Cesar — if he's watching he'll interrupt; if not, the pipeline runs itself.
- **Every iteration, not just the final one.** The point is to catch issues early, so surface on each implementer→review handoff, including redo iterations.
- **Keep it signal, not noise.** Surface the canonical screenshot + any `Rejection follow-up` frames + one still per NEW video — not every intermediate frame the subagent dumped.
- Also continue surfacing the canonical image (and videos) at `ARCHITECT_REVIEW_PASS` when handing to Cesar for final approval, as already done.

### Where things live

- **Subagent definitions:** `.claude/agents/golfin-reviewer.md`, `golfin-implementer.md`, `golfin-self-reviewer.md`, `golfin-redteam-reviewer.md`
- **Hooks:** `.claude/hooks/route_subagent.py` (state router + desktop notify + email + alerts.log), `enforce_implementer_done.py` (PreToolUse blocker), `capture_screenshot.py` (Implementer's screenshot helper)
- **Notification config:** `.claude/notify_config.json` (toast always on; email opt-in)
- **Per-task folder:** `Docs/Specs/Active/<task_slug>/` containing `SPEC.md`, `STATUS.md`, `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md`, `CESAR_REJECTION.md` (when applicable), `HEARTBEAT.log`, `screenshots/` (still images: PNG/JPG), `videos/` (clips: MP4/MOV/WebM — see § Screenshots rule 5)
- **Template:** `Docs/Specs/Active/_TEMPLATE/` (copy this to start a new task)
- **Quick tasks:** `Docs/Specs/Quick/` (lightweight, no subagent chain)

### STATUS.md states

```
SPEC_READY -> IMPLEMENTER_WORKING -> READY_FOR_SELF_REVIEW
            -> (SELF_REVIEW_PASS | SELF_REVIEW_FAIL | READY_FOR_ARCHITECT_REVIEW)
            -> golfin-reviewer PASS -> READY_FOR_REDTEAM
            -> golfin-redteam-reviewer -> (ARCHITECT_REVIEW_PASS | ARCHITECT_REVIEW_FAIL | ARCHITECT_REVIEW_ESCALATE)
            -> (CESAR_REJECTED loops back) | (DONE finishes)

READY_FOR_REDTEAM    - golfin-reviewer passed; adversarial red-team gate runs next
IMPLEMENTER_BLOCKED  - implementer hit a circuit breaker; Cesar must unblock
CESAR_REJECTED       - Cesar manually rejected after architect-pass; loop back to implementer
```

**Two-gate review (added 2026-05-29).** `golfin-reviewer` no longer writes `ARCHITECT_REVIEW_PASS` — its PASS sets `READY_FOR_REDTEAM`, handing to `golfin-redteam-reviewer`, the adversarial gate that is the ONLY agent allowed to advance to `ARCHITECT_REVIEW_PASS`. This exists because single-reviewer PASSes were rubber-stamping work Cesar rejected on sight (`green_slope_height_bake` passed 3×). Every `ARCHITECT_REVIEW_PASS`-then-`CESAR_REJECTED` is logged to `.claude/review_misses.log` and the running miss count is surfaced at every pipeline tick.

The `route_subagent.py` hook prints the next step in the terminal automatically after every subagent run, so neither you nor Cesar needs to check a log file. When STATUS reaches a state that needs Cesar (`ARCHITECT_REVIEW_PASS`, `*_ESCALATE`, `IMPLEMENTER_BLOCKED`), notifications fire via Windows toast + email (if configured) + always-logged at `.claude/alerts.log`.

### PIPELINE_HARDENING rules (2026-06-19 — after `map_view_aiming` iter-15; now baked into all agents and route_subagent.py)

These rules convert previously-advisory lessons into hard stops. Full spec: `Docs/PIPELINE_HARDENING.md`.

1. **Iteration circuit-breaker.** `route_subagent.py` counts iterations per task by shape label (declared in `IMPLEMENTER_REPORT.md` as `**Iteration shape:** <subsystem>:<symptom>`). **3 failures of the same shape → forced `ARCHITECT_REVIEW_ESCALATE`**; `ARCHITECT_ESCALATION.md` is written; no iter-N+1 of that shape may run. (iter-15 should have tripped at ~iter-6 but there was no circuit-breaker.)
2. **Real-entry rule.** Any feature with a player entry point (button/card the real player sees in Practice/1v1) MUST be driven through the REAL widget's `onClick`. A synthetic/test-only button = automatic FAIL at all three review gates. The implementer's Gate-A proof section must cite `<RealWidget>.onClick.Invoke()`, not a test-only GO.
3. **Invariant-JSON gate.** For world→screen features (markers, overlays, projected geometry), the pass/fail gate is a deterministic invariant JSON dump (`*_invariants.json`) with per-assertion PASS/FAIL, NOT a human reading of a video. Video = artifact for Cesar; JSON = the gate. SPEC §11 is the template. Missing JSON = FAIL at all three review gates.
4. **Capture flip-free via TaggedCamera.** Unity Recorder `CameraInputSettings TaggedCamera` aimed at the feature camera — the same mechanism `HoleFlyoverRecorder` uses. BANNED: RT→RawImage, `uvRect` flips, `yflip_repair.py`, `ffmpeg -ss` keyframe sampling. Orientation proven by the invariant math (`ball.screenY > flag.screenY`), not pixel inspection.
5. **Reviewers re-run the ENTIRE acceptance list every pass** — not only the symptom the previous reviewer named. "Carried forward from prior iter" is not valid evidence at any gate.
6. **Report integrity.** Every PASS claim must be backed by a visible tool result or the invariant JSON. Unexplained PASS = auto-FAIL at review. Fabricated test result / approval quote / tool output = CRITICAL FAIL, logged to `.claude/review_misses.log`.
7. **Standing bans** (now gate-enforced): ZERO edits to `Assets/Scripts/Physics/`; no `*Gate` scenarios added to `Scenarios.cs`; no new subsystem baked exclusively into `LabScaffold.unity`; `M_Splash*.mat` files untouched.
8. **Clone-provenance gate** (reuse-table tasks). Every `§1 reuse / clone-from` row must cite a concrete source (prefab GUID / scene-object path+fileID); no provenance, or provenance pointing at a net-new object = FAIL. Enforced by `enforce_implementer_done.py` ("Rule 19") + the §-numbered spec; see `PIPELINE_HARDENING.md` §8 (not re-specified here).
9. **Figma node re-pull gate** (2026-06-29, after `tournament_signup_modal`). For any task whose SPEC references a Figma node, the implementer AND each reviewer MUST run `get_design_context` on that node at step 0 and diff live px/font/gap/sprite against the NODE — the SPEC token table is a reconcile-against-node convenience, never source of truth. No node-pull evidence in the report = FAIL. (`PIPELINE_HARDENING.md` §9.)
10. **Reference-image diff gate.** Built render vs `reference/` node render, side-by-side; FAIL on dissimilarity. Reviewer pastes both crops per mandated element instead of asserting "looks like Figma." (`PIPELINE_HARDENING.md` §10.)
11. **Clone-provenance read-back** (extends §8). Reviewer reads back the live `Image.sprite` GUID on every mandated-clone element; a flat-colour fill where a sprite is required = FAIL. Fabricated clone provenance (report claims a clone that isn't on the live object) = CRITICAL FAIL per rule 6, logged with iter number. (`PIPELINE_HARDENING.md` §11.)
12. **Unity authoring traps (C1–C8)** — implementer checklist: C1 dirty-on-write (`SerializedObject`/`SetDirty`/`RecordPrefabInstancePropertyModifications`); C2 modal-root-stays-active (`ModalController` toggles the child `modalPanel`); C3 layout-group vs fixed-size (pin a `LayoutElement`); C4 `childForceExpandWidth` widens gaps; C5 `Outline` component ≠ crisp Npx border; C6 flat layout vs nested groups for per-gap values; C7 edit-mode Game View does not repaint (verify in play mode); C8 app boots through a title/PLAY screen automation must drive. (`PIPELINE_HARDENING.md` §12.)
13. **Fast single-modal render harness** — boot → open one modal → screenshot, without the full loop, for UI-fidelity round-trips (tooling gap tracked). (`PIPELINE_HARDENING.md` §13.)
14. **Orchestrator scene-mutation guardrail** — never `scene-save` after a render-isolation/probe mutation without diffing GameObject active-state vs HEAD first (boot-critical containers: `ScreensRoot`, `PersistentUI`, active screen). (`PIPELINE_HARDENING.md` §14.)

15. **Second defect of a shape ⇒ audit the shape before the next review** (added 2026-08-28, after
    `content_art_bundling` spent five iterations and four gate rounds finding SEVEN defects of one
    shape, one at a time). When two defects in a task rhyme, stop fixing instances: name the shape
    as a *mechanically checkable question*, enumerate every candidate site in that file or subsystem
    (grep the operation class, do not sample), publish a per-site verdict table **including the
    sites that were fine**, fix everything in one commit, and only then re-enter the gates. Rule 5
    makes reviewers re-run the whole acceptance list, but an acceptance list enumerates *behaviours
    the spec asked for* — it is instance-level by construction and a shape passes straight through
    it. The scoreboard on that task: red-team 1 defect, self-review 1 process catch + 1 real
    candidate, reviewer 0 across three passes — and the implementer 6, by reading the file.
    **Corollary:** do not dispatch the next gate while holding an unexamined suspicion; two of those
    six were found while *writing the brief for the next reviewer*, which is a free review. Full
    spec: `Docs/PIPELINE_HARDENING.md` §22.

17. **Bots swing through `BotSwing.Play` / `BotSwing.PlayPerfect` — never `BeginExternalDrag`,
    `EndExternalDrag` or `CommitFlick` directly** (added 2026-09-05, `bot_scheme_parity` §3.5;
    Cesar: *"this should include any test bots we use in the future when developing features"*).
    Five bots each hand-rolled `BeginExternalDrag → SetExternalPower → EndExternalDrag`, which is
    **Flick's gesture spelled out longhand**. With any other control scheme selected the flick
    root is OFF, so those swings animated nothing — the ball left while the pendulum bar sat
    idle. `BotSwing.Play` resolves `ShotSchemeHost.ActiveExecutor`, so a bot written for some
    unrelated feature next year swings whatever scheme the player picked without ever having
    heard of schemes. `BotSwingOptions.ForceFlick` exists for deterministic captures and frozen
    baselines only and **requires a comment at the call site saying why**. A new bot that
    bypasses `BotSwing` fails review. Gate-enforced: **Rule 23** in
    `.claude/hooks/enforce_implementer_done.py` greps every `*Bot.cs` / `*CaptureRig.cs` / file
    under a `Bot*/` directory for BOTH the direct call and the reflection form
    (`GetMethod("BeginExternalDrag")` — half these bots live in assemblies that cannot name
    `ShotController`). Allow-list, each with a stated reason in the hook: `Scenarios.cs` and
    `PowerGaugeMarkerVerifyBot.cs` (Flick-specific regression instruments — routing them through
    `BotSwing` would mean they stopped testing Flick the moment a tester left another scheme
    selected), the scheme drivers and the seam itself, and `BotDriver.cs` (grandfathered; the
    loop-v2 smoke harness is migrated deliberately, not in passing). Coverage:
    `TestBotSwingDoor` in `.claude/hooks/test_enforce_implementer_done.py`.
    **Sister rule (`bot_scheme_parity` §5):** the three `execSigma*` columns in
    `Assets/Resources/Data/bot_difficulty.csv` come from `Tools ▸ Golfin ▸ Bots ▸ Calibrate
    Scheme Sigma`, never from a hand edit — re-run it after touching any grader tuning key
    (`Pendulum*Window*`, `*MissYawGain`, `Needle*Zone*`, `FreeSwingImpact*`, `ConeHalfAngle*`),
    or a bot's difficulty silently drifts away from its Flick bracket.

16. **Figma-node screens follow `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md`** (added
    2026-09-02, after `gps_profile_pack` cleared three rounds of gates and was rejected on sight
    each time — then converged in one sitting once it was driven by real navigation plus a
    per-element crop diff). The implementer works its checklists; every reviewer re-runs § 7
    (crop matched node/built regions and ENUMERATE the differences) rather than asserting
    "matches Figma". The four that cost the most rounds: a render harness instead of real
    navigation, a ΔRGB measured against the wrong backdrop, `Image.Type.Filled` on 9-sliced bars,
    and the pre-compositing `A()` used where real alpha was needed.

### Hard rules (these are enforced by hooks, not just convention)

1. **Implementer cannot mark itself done.** The `enforce_implementer_done.py` hook blocks any STATUS write to `READY_FOR_SELF_REVIEW` or `READY_FOR_ARCHITECT_REVIEW` unless `IMPLEMENTER_REPORT.md` has every checklist item filled with PASS/FAIL + non-trivial justification + a real screenshot path that points to an actual file. No placeholder text allowed. FAIL items also block the SELF_REVIEW transition (must use ARCHITECT_REVIEW path). **Since 2026-05-26** (green_authoring scar tissue), the hook also blocks the transition unless: (a) HEARTBEAT.log contains an `=== iter-N kickoff baseline … ===` block (HEAD SHA + DIRTY porcelain) for the current iteration, (b) every "pre-existing"/"from previous session"/"not introduced by"/"predates this"/"was already in" claim in IMPLEMENTER_REPORT.md has a backticked or fenced citation within ±5 lines that quotes a path from that DIRTY block, and (c) no PNG/JPG referenced under `screenshots/` has variance < 5.0 on a sampled patch (catches fabricated flat-colour frames). **Since 2026-05-26 21:25 CEST** (spin_and_shape scar tissue — Lesson AA), the hook additionally enforces Rule 13: every uncommitted path reported by `git status --porcelain --untracked-files=all` that lives OUTSIDE the task's `Docs/Specs/Active/<task>/` folder must appear in `IMPLEMENTER_REPORT.md`'s 'Files modified or created' table — implementer either reports the file or restores/discards it before transitioning. Rationale in `feedback_preflight_baseline_attribution.md` (user memory) and `.claude/hooks/test_enforce_implementer_done.py`.
2. **STATUS is authoritative.** Do NOT "correct" STATUS based on review file contents. If STATUS contradicts a review verdict, Cesar may have rejected manually — check for `CESAR_REJECTION.md`. If still uncertain, set STATUS to `IMPLEMENTER_BLOCKED` and ask.
3. **Implementer cannot write SELF_REVIEW.md or ARCHITECT_REVIEW.md.** Those are written by the other subagents.
4. **Self-reviewer cannot modify scenes or write code.** It's a vision-heavy reviewer only; tools are scoped to Read/Write/Edit + Figma MCP.
5. **Reviewer cannot modify scenes or write Unity code either.** Same scoping; the reviewer reads files and writes the review verdict.
6. **`STATUS.md = DONE` only after Cesar's manual approval.** No subagent writes DONE. Cesar moves the folder to `Docs/Specs/Completed/` when satisfied.
7. **No white-box placeholders.** If `[SerializeField]` references aren't wired, wire them BEFORE marking IMPLEMENTER_REPORT done. Use `_default*` slots specified in the spec for fallback sprites.
8. **Wait before screenshot.** After entering play mode, wait at least 3 seconds (5 if data-binding is involved) before capturing. Unity needs time to render the first few frames and run all OnEnable code.
9. **Append to HEARTBEAT.log** every ~5 minutes of work. Stale heartbeat (>15min) triggers a stuck-session alert to Cesar.
10. **Circuit breakers** — if the same Unity MCP tool fails 3 times, or you wait on Unity for >3 minutes with no progress, or you can't find an asset after 2 attempts: set STATUS to `IMPLEMENTER_BLOCKED` and stop. Don't loop indefinitely.
11. **Every new player-facing `Button` gets `Golfin.UI.Polish.ButtonPressFeedback`.** When adding any new `UnityEngine.UI.Button` to a production prefab or scene via Unity MCP, immediately follow `add_component(UnityEngine.UI.Button)` with `add_component(Golfin.UI.Polish.ButtonPressFeedback)` in the same operation. Defaults stay (`_pressedScale=0.95`, `_duration=0.12`). Full rationale in `tasks/lessons.md` Lesson S. Self-check at task close: grep new `.prefab` / scene diffs for Button GUID references; every match must have a sibling `ButtonPressFeedback` reference. One missing pair = task FAIL.
12. **Close-out commits run `git status` first.** Architect-driven close-out commits (the move-from-`Active`-to-`Completed/` commit) MUST run `git status --porcelain --untracked-files=all` and `git diff --stat HEAD` immediately before staging the folder rename. If ANY M / ?? / D path lives outside the task's spec folder, HALT. Either (a) commit those code/data files first in a separate, properly-attributed commit, or (b) restore/discard the drift before staging the close-out. Doing the move-to-Completed commit on top of uncommitted code is the failure mode that produced `7a1d2328 spin_and_shot_shape_wiring: DONE` — a docs-only commit while 14 implementation files lived uncommitted in the working tree for over 10 hours. Full rationale: Lesson AA in `tasks/lessons.md`. Sister rule: Lesson R (always commit `.cs.meta` alongside `.cs`). This rule is architect-side: there is no subagent for close-out, so it lives here in CLAUDE.md rather than as a hook gate.
13. **Review-hardening gates (added 2026-05-29, after `green_slope_height_bake` was PASSed 3× and Cesar rejected each in seconds).** `enforce_implementer_done.py` now also enforces:
    - **Rule 14 — canonical-screenshot resolution floor.** A report that cites any `screenshots/*.png` must declare exactly one canonical frame (`Canonical screenshot: \`screenshots/X.png\``) and that frame's long edge must be ≥ 900px. iter-9 designated a 256px top-down — a boundary defect is physically unresolvable at that size, so the reviewer rubber-stamped. Blocks the implementer→review transition.
    - **Rule 15 — reproduce-the-rejection gate.** When `CESAR_REJECTION.md` exists, `IMPLEMENTER_REPORT.md` must carry a `## Rejection follow-up` section with an explicit GONE/RESOLVED/STILL-PRESENT verdict per flagged defect AND a same-angle full-res screenshot citation. No re-shoot of the exact defect = no advance.
    - **Rule 16 — mesh-metrics gate.** For mesh/terrain tasks (SPEC mentions ≥2 of: `green.json`, `TerrainData`, mesh-cut/deform, `GreenTopology`, skirt, vertex normal, contour, triangulate…), the reviewer's `ARCHITECT_REVIEW.md` must contain a numeric `## Mesh metrics` section before it can write `READY_FOR_REDTEAM`. 3D tasks have no Figma/bbox gate, so numbers (min collar normal.y, max boundary Δy, vert count) are the objective gate. Coverage: `.claude/hooks/test_enforce_implementer_done.py`.
    - **Rule 17 — mesh-bake video deliverable (added 2026-05-30).** For the same mesh/terrain tasks, `IMPLEMENTER_REPORT.md` must declare a `Canonical video: \`videos/<file>.mp4\`` line pointing at a real (≥50KB), non-placeholder orbit fly-around clip in the task's `videos/` folder, or the implementer→review STATUS write is blocked. A green/terrain bake is reviewed from chat as a **video**, not stills — stills are supporting evidence only (standing "always show me video" rule; `green_slope_height_bake` reached Cesar on stills at iter-7/8/9/12 and was bounced every time). Render+caption it with the `build_bot_video.py` / `textfile=` drawtext idiom — do NOT hand-roll inline `drawtext`. Scoped to mesh tasks so UI-layout tasks (stills + Figma) aren't gated. Coverage: `.claude/hooks/test_enforce_implementer_done.py` (`TestVideoDeliverable`).
    - **Rule 18 — Figma fidelity table (added 2026-06-09, after `1v1_ingame_ui` passed the full pipeline 2× and Cesar rejected both — once for an EXPLICIT spec token rendered absent (3px `#818EA1` banner border), once for a mini-map placed below instead of above the Fade/Draw button and carrying a data card it shouldn't).** The UI counterpart of Rule 16 (mesh metrics): when `SPEC.md` references a Figma NODE (a figma.com URL or a `<n>:<n>` node-id), both `IMPLEMENTER_REPORT.md` (implementer→review gate) and `ARCHITECT_REVIEW.md` (reviewer→red-team gate, `READY_FOR_REDTEAM`) must carry a `## Figma fidelity` section — a real per-element table (≥1 row, a cited Figma node, PASS/FAIL verdicts), NOT a blanket "matches Figma." The pipeline already DEMANDED a per-element Figma diff ("'matches' is not acceptable") but it was unenforced and got rubber-stamped; it is now a hard gate. The architect drops the canonical node renders into the task's `reference/` folder at spec time so the A/B is unavoidable; enumerate EVERY element (especially borders/outlines and relocated/derived elements) in the SPEC's § Figma Fidelity table. Coverage: `.claude/hooks/test_enforce_implementer_done.py` (`TestFigmaFidelity`). Full post-mortem: Lesson AE.
    - **Rule 19 — Clone provenance table (added 2026-06-28, after `tournament_round_loop`'s signup modal was hand-built from default Unity Images with flat-colour fills and ZERO sprites — no source prefab, nothing cloned — while the report marked every "navy panel clone / silver button clone" row PASS).** The provenance counterpart of Rule 18: Rule 18 checks the result LOOKS like the design; Rule 19 checks reused elements WERE actually cloned from a real source. When `SPEC.md` declares a REUSE / clone-and-modify mandate (a "§0 REUSE MANDATE", "Author ZERO new panels/buttons", "clone the existing …"), `IMPLEMENTER_REPORT.md` must carry a `## Clone provenance` section — a per-element table where EVERY row cites the concrete source the element was cloned/rebound from (a `.prefab` path, an `Assets/...` sprite/material path, or a 32-hex GUID). A prose-only row ("matches the modal family") or a row flagged "not found / built from scratch / hand-rolled" is a hard block. **Surface, don't rebuild (Cesar standing rule 2026-06-28):** *"If no elements mentioned are found to clone SURFACE it, don't build from scratch without telling me."* — if a mandated source can't be located, the implementer sets `IMPLEMENTER_BLOCKED` and surfaces, never hand-rolls. This is the same scar as `tournament_selection_screen` (memory `feedback_reuse_map_clone_provenance_gate`: "rebuilt from scratch, hand-rolled buttons, passed 3 gates before Cesar stopped it") — no gate had ever verified the §1 clone-and-modify mandate. Coverage: `.claude/hooks/test_enforce_implementer_done.py` (`TestCloneProvenance`). Reviewers must VERIFY the table by reading back the live GO's `Image.sprite` (must be the real sprite, not `<NONE>` + a flat colour), not just trust the prose.
    - **Rule 21 — UI fidelity lint gate (added 2026-07-02, after `stamina_boost_shop`'s menu row: Cesar had to catch — by eye, over many iterations — an oval pill (S_PillStadium 9-sliced with no `pixelsPerUnitMultiplier` → collapsed corners), a distorted BUY corner radius (non-9-sliced sprite stretched non-uniformly), a dark-tinted panel, wrong 16px gaps, and fabricated flat-fill boxes).** The AUTOMATED counterpart of Rules 16/18: for a Figma-node UI task the objective gate is a deterministic lint JSON, not a human reading a render. `Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab(prefab, spec.json)` (`Assets/Editor/UIFidelity/`) instantiates each built prefab under a temp canvas and writes `Docs/Diagnostics/_capture/<prefab>_lint.json` with a `fail` count from two layers: **render-health** (no reference needed — 9-slice collapse→oval, non-9-slice corner distortion, null-sprite flat-fill fabrication, `Outline`-as-border, tiny text) and **node-spec** (size/gap/radius/sprite/color/font vs a per-element `spec.json` generated from the node by `Docs/Scripts/figma_node_to_spec.py` (get_metadata XML + get_design_context JSX → spec.json; wired into `golfin-implementer.md` step 6e — no longer hand-authored); `requireSprite` HARD-FAILs a flat fill where the node shows a sprite). `IMPLEMENTER_REPORT.md` must carry a `## UI fidelity lint` section citing each JSON with `fail == 0`; the hook blocks the transition on a missing section, an uncited/missing JSON, or any `fail > 0`. Both reviewers RE-RUN the linter themselves (never trust the cited JSON). It already earned its keep: on the row Cesar signed off, render-health caught a `PillFill` corner-collapse that both the implementer AND Cesar had missed. Also: `Docs/Scripts/figma_diff.py` (render-vs-node pixel-diff) is the visual backstop. Full tooling + usage: memory `reference_ui_fidelity_linter`. Coverage: `validate_ui_lint` in `.claude/hooks/enforce_implementer_done.py`. **Proactive counterpart (Fix #1):** `Docs/Architecture/UI_ELEMENT_PALETTE.md` is the reusable-atom catalog (verified paths + GUIDs) the implementer maps each node element to BEFORE building — see Rule 22 in `golfin-implementer.md`. The palette is the first line (reuse the real pill/button/badge); the linter is the backstop (fails fabrication after the fact).
    - **Adversarial second reviewer + scoreboard.** See § STATUS.md states "Two-gate review" — `golfin-redteam-reviewer` is the only agent that may write `ARCHITECT_REVIEW_PASS`, and PASS→reject misses are logged to `.claude/review_misses.log`.

### Visual review checklist (enforced by both reviewer agents)

Drafted 2026-05-13 after `loop_v1_2d_hole_complete_and_result_screen` iter-6, 8, 11, 12 all green-lit visible text-outside-container bugs that Cesar caught in seconds. Full diagnosis in `Docs/Architecture/REVIEW_PIPELINE_FIXES.md`. Both `.claude/agents/golfin-self-reviewer.md` and `.claude/agents/golfin-reviewer.md` enforce these in order:

1. **Independent pixel scan FIRST.** Reviewer opens the canonical screenshot and writes a 3–5 sentence pixel-level description BEFORE reading IMPLEMENTER_REPORT, SELF_REVIEW, or any prior verdict. Confirmation bias is the named failure mode this fixes.
2. **Figma side-by-side comparison.** Per-element differences with specific pixels/colors; "matches" is not acceptable. **ALWAYS check font WEIGHT and RENDERED size-vs-reference for every text element (standing rule, Cesar 2026-07-01).** For each text element the fidelity table must state built weight (Bold/SemiBold/Medium/Regular) vs node weight — a mismatch is a FAIL — and the built text's rendered cap-height must be A/B'd against the `reference/` node render at matched scale, FAILing if it looks smaller/larger than the reference even when `node_px ÷ divisor` math "matches" (the reference render is ground truth for visual size, not the arithmetic). This is unconditional on every Figma-node task, not only when a text problem was flagged. Scar: `1v1_result_rewards_display` cleared all three gates then Cesar rejected it for bold-vs-regular labels/Vs./usernames and too-small fonts that matched a divisor.
3. **Bbox geometry MCP check for containment claims.** Programmatic `script-execute` for any "text inside BG", "child inside parent", "modal inside canvas" claim. ANY `inside=false` → hard FAIL.
4. **Scene-mutation audit via `git diff`.** No `m_IsActive: 0`, `sizeDelta`, or position changes to GameObjects outside the documented fix. Capture paths that mutate scene state are a hard FAIL.
5. **Implementer-graded PARTIAL → FAIL default.** Uncertainty in the implementer's report = FAIL unless the reviewer can articulate specific pixel-level reasoning for PASS.
6. **Production-flow capture verification.** Layout-affecting changes need a real-gameplay-path screenshot in addition to any smoke-runner output.
7. **Read implementer narrative ONLY AFTER 1–6.** If narrative contradicts pixel evidence, FAIL.

The reviewer agents have full pixel access, Figma access, and read-only Unity MCP + Bash access for bbox/git-diff checks. The pipeline has all the tools — what was previously missing was the discipline to use them in this specific independent order.

### How to start a new UI task (Cesar)

For a complex UI task: write the spec with the human Architect (Cesar's claude.ai chat). The Architect will:

1. Confirm the Figma page/frame/placeholder-vs-canonical with you (per Blueprint §8 standing rule).
2. Create `Docs/Specs/Active/<task_slug>/` from the template.
3. **Pull the Figma node renders into `reference/` and fill the § Figma Fidelity table (Rule 18).** For EVERY frame/component the task touches — *including relocated/derived elements* (a moved map, a mirrored card) — pull the node render via `mcp__figma__get_screenshot` into `Docs/Specs/Active/<task_slug>/reference/`, then enumerate every element (borders/outlines + position + content shown/hidden, not just prose) in `SPEC.md` § Figma Fidelity. This is the highest-leverage step: the `1v1_ingame_ui` map/border misses were only caught once the real node renders were dropped in `reference/`. Whoever dispatches the implementer (claude.ai Architect, or Claude Code at kickoff) should verify the renders + table are present before kicking off.
4. Fill the rest of `SPEC.md`.
5. Set `STATUS.md` to `SPEC_READY`.

The SubagentStop hook will then print: `[<task_slug>] STATUS=SPEC_READY -> Use the golfin-implementer subagent on "<task_slug>"`. You paste that command into Claude Code and the pipeline runs itself.

For a small task: just say `Read Docs/Specs/Quick/<task_slug>.md and implement.` after writing the quick spec.

### How to redo a failed iteration

If the reviewer or self-reviewer kicks the task back, STATUS goes to `*_FAIL` and the hook prints `Use the golfin-implementer subagent on "<task_slug>"`. The Implementer reads the latest review file, addresses the fail list, and re-submits.

If YOU manually reject after reviewer-pass: write `CESAR_REJECTION.md` in the task folder explaining why, then set STATUS to `CESAR_REJECTED`. The hook will route the implementer to redo with your notes.

### When to escalate to claude.ai (Architect Claude in this chat)

The Claude.ai chat (Opus 4.7, full repo access via filesystem MCP) is for:
- Project-wide reasoning that doesn't fit one task (e.g., "should we restructure asmdefs?").
- Ambiguous escalations where the reviewer subagent writes `ARCHITECT_REVIEW_ESCALATE`.
- Authoring a new spec for a task that affects multiple subsystems.
- Workflow / pipeline improvements.

For a single task in flight, prefer the subagent chain. Only ping Cesar's claude.ai chat when STATUS reaches `ARCHITECT_REVIEW_ESCALATE` or `IMPLEMENTER_BLOCKED`.

### Migration from old TellCode.md workflow

`Docs/TellCode.md` is the legacy handoff file. New tasks use the per-task folder convention above (or Quick for small ones). TellCode is being phased out; do not write new active tasks there. The completion log at the bottom of TellCode is preserved for historical reference.

---

## Screenshots — MANDATORY rules

Code's screenshot history is full of timing failures. These rules eliminate the common ones.

> **‼️ CAPTURE RULE 0 (2026-07-16, hook-enforced) — when driving Unity over MCP, captures go through the `mcp__ai-game-developer__screenshot-game-view` tool, NEVER a hand-rolled `script-execute`.** A `PreToolUse` hook (`.claude/hooks/enforce_capture_tool.py`) HARD-BLOCKS any `script-execute` that reflects into `CaptureCore`/`CaptureHelper`/`SnapPlayModeSafe`/`SnapGameView`/`ScreenCapture.*`. If you need a saved file, `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` is allowed. **And capture AS A REAL USER:** the app boots to a Title/PLAY/LOGIN gate that ScreenManager does NOT manage — `ShowScreen(target)` swaps screens *behind* the gate and `CurrentScreen==target` is a FALSE POSITIVE, so the frame stays on the title screen. Click PLAY, navigate via real widget `onClick`, then snap. **Always look at the PNG before surfacing it.** Full rationale: memory `reference_playmode_capture_runinbackground`. This exists because captures were repeatedly hand-rolled and kept returning the splash/title frame (gacha_history, 2026-07-16).

**Hard rules:**

1. **NEVER call `ScreenCapture.CaptureScreenshot(path)`.** It is async, unreliable, and silently fails when paused. Use `CaptureHelper.SnapGameView()` instead — it is synchronous and works in EditMode, paused playmode, and running playmode.

2. **NEVER pause before capturing.** The render loop stops emitting frames during pause, so any queued capture never completes. Always capture-then-pause, never pause-then-capture. `CaptureHelper.SnapAtEndOfFrameAndPause()` does this in the right order.

3. **For UI-only verification, do NOT enter playmode.** Use `GOLFIN > Capture > Fake State - <preset>` from the Editor menu (or call `CaptureHelper.FakeMidAim()` etc. from a `[MenuItem]` script), then `CaptureHelper.SnapGameView()`. The static-bus contexts (PlayerContext, HoleContext, etc.) make this work without any game loop running.

4. **For mid-animation verification,** start a coroutine that runs `yield return CaptureHelper.SnapAtEndOfFrameAndPause("label")`. Do NOT pause first.

5. **Output location.** Still captures (PNG/JPG) land in `Docs/Diagnostics/_capture/`. After capture, copy/rename the relevant one(s) into the task's `screenshots/` folder under `Docs/Specs/Active/<task>/screenshots/`. Don't litter the diagnostics folder with task-specific names. **Video clips (MP4/MOV/WebM) go to `Docs/Specs/Active/<task>/videos/`, NOT `screenshots/`.** Bot recordings written by `BotVideoRecorder` to `tasks/loop_v2_smoke_bot/<scenario>/video/raw.mp4` get copied to the task's `videos/` folder. Frame extracts pulled from a video (JPG/PNG stills) go to `screenshots/`. Convention established by `puttpath_predictor_perf_and_design`; codified 2026-05-25 after `live_stat_provider_wiring` initially stashed MP4s in `screenshots/`.

6. **`CaptureHelper` / `CaptureCore` is the only sanctioned capture path (Lesson 2026-05-13).** No per-task screenshot workarounds. If `SnapGameView`, `SnapAtEndOfFrameAndPause`, or `SnapPlayModeSafe` fails in your environment (MCP-frozen-time, domain reload in flight, anything else), STOP and surface the blocker per the `IMPLEMENTER_BLOCKED` protocol. Do NOT invent a custom capture path. Iter-12 of `loop_v1_2d_hole_complete_and_result_screen` hit MCP-frozen-time and the implementer wrote a custom ortho-camera-render workaround that deactivated 10 ShotUI GameObjects in `LabScaffold.unity` as a side effect; the scene corruption was invisible until Cesar launched normal play. If `CaptureCore` doesn't cover a case, that's a backlog item to extend `CaptureCore` (see `Docs/Specs/Queued/capture_core_frozen_time_fallback/SPEC.md`), not a license to bypass it.

**Quick reference:**

| Situation                              | Tool                                                 |
|----------------------------------------|------------------------------------------------------|
| UI layout check, no playmode needed    | Fake State preset → `SnapGameView()`                 |
| Verify scene contents in EditMode      | `SnapGameView()`                                     |
| Frozen moment from playmode            | `SnapAtEndOfFrameAndPause("label")` in coroutine     |
| Series of frames during animation      | Multiple `SnapGameViewWithLabel("step1"/"step2"/…)`  |
| Play-mode coroutine that must keep running (smoke runner) | `CaptureCore.SnapPlayModeSafe("label")` — sync, returns absolute path, **does NOT** call `AssetDatabase.Refresh` (which would force a domain reload and kill the coroutine), does NOT pause |
| `ScreenCapture.CaptureScreenshot(path)` | **DO NOT USE — banned by this project**             |
| Physics-lab ball-at-rest after a shot   | `SnapAtEndOfFrameAndPause("shotN_<config>_atrest")` in coroutine — `mcp__ai-game-developer__screenshot-game-view` does NOT refresh between calls in the same `script-execute` scope and will return the pre-shot frame |

**`SnapPlayModeSafe` vs `SnapAtEndOfFrameAndPause`:** use `SnapPlayModeSafe` when a long-running coroutine needs to capture *and continue* (e.g. fire shot → capture → reload hole → capture again). It is synchronous, returns the path string for logging, never pauses, and never calls `AssetDatabase.Refresh()` — so the coroutine survives. Use `SnapAtEndOfFrameAndPause` when you want a single frozen moment and the coroutine can end — it yields one frame and pauses (or skips pause with `skipPause: true`), and is the right choice for at-rest verification snaps. Both live in `Golfin.Diagnostics.Runtime.CaptureCore` and are mirrored on the editor-side `CaptureHelper`.

**Physics-lab capture rule (controls_c_fix postmortem):** when a SPEC asks for ball-at-rest evidence after firing a lab shot, the spec's verification step MUST mandate `CaptureHelper.SnapAtEndOfFrameAndPause` — NOT `screenshot-game-view`. The MCP tool reads the Game View RT, which is not synchronously refreshed inside one `script-execute` call, so two sequential `screenshot-game-view` calls after two different shots produce visually identical PNGs of the pre-shot tee. Self-reviewer/reviewer must FAIL any physics-lab task whose two at-rest captures show the same pre-shot frame, regardless of byte-count delta.

**Adding new fake-state presets:** when a new static-bus context is added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, the same task that adds it must (a) extend `CaptureHelper.FakeMidAim` to set sensible values for the new context, (b) extend `CaptureHelper.FakeReset` to call its `Reset()`, and (c) add a dedicated preset if the context has interesting variation. See `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol.

---

## Session Startup (EVERY SESSION)

Before doing anything else:

1. Generate the architecture audit (use the variant for your platform):
   - **Windows:** `powershell -File Docs/Scripts/generate_audit.ps1 > Docs/Architecture/ARCHITECTURE_AUDIT.md`
   - **macOS / Linux:** `bash Docs/Scripts/generate_audit.sh > Docs/Architecture/ARCHITECTURE_AUDIT.md`
2. Read `Docs/AI_CONTEXT.md` (tiny — current status and active work)
3. Read `Docs/Tasks.md` (current checklist — what to do)
4. Read `Docs/TellCode.md` for any pending architect instructions
5. If working on UI/design: read `Docs/Rules.md` (design constraints, Figma specs, conventions)
6. If working on UI: read `Docs/Architecture/UI_HIERARCHY.md` (scene UI paths) and `Docs/Architecture/PATTERNS.md` (recurring patterns)
6b. **If the task builds a screen from a Figma node: READ `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md` FIRST and work its checklists.** It is the distilled cost of `gps_profile_pack`, where three full pipeline iterations passed every gate and Cesar rejected each on sight.7. If needed: read `Docs/Architecture/ARCHITECTURE_AUDIT.md` (file tree, singletons, events)
8. Read `tasks/lessons.md` for relevant project lessons
9. **Know the ship words before they are used:** `Docs/PUNCH_IT_ROUTINE.md` defines `punch it`,
   `punch it GPS` and `punch it standalone` as TestFlight commands, and Cesar abbreviates them
   ("Punch GPS"). A session that has not read it does not recognise the request as a build at all.

## Session End (EVERY SESSION)

Before closing:
1. Update `Docs/AI_CONTEXT.md` with:
   - What was completed this session
   - Current phase status (checkboxes)
   - Any new issues or blockers discovered
   - What's next
2. Update `tasks/lessons.md` if any corrections were made
3. If UI hierarchy changed (new panels, modals, stat rows, buttons): update `Docs/Architecture/UI_HIERARCHY.md`
4. If new patterns emerged or existing ones changed: update `Docs/Architecture/PATTERNS.md`
5. Commit with descriptive message

## Debugging Unity

### Reading Unity Console without copy-paste
Unity Editor writes to a log file you can tail directly. Path differs by OS:

- **Windows:** `%LOCALAPPDATA%\Unity\Editor\Editor.log`
- **macOS:** `~/Library/Logs/Unity/Editor.log`
- **Linux:** `~/.config/unity3d/Editor.log`

**Windows (PowerShell):**
```powershell
# Last 100 lines (quick check)
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 100

# Filter for errors only
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 500 | Select-String "Error|Exception|NullReference"

# Filter for game logs only
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 500 | Select-String "\[CharacterManager\]|\[CarouselController\]|\[ScreenManager\]|\[RosterScreenController\]|\[LevelUpModal\]|\[CompareController\]"

# Watch live (keep running while testing in Unity)
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Wait -Tail 10
```

**macOS / Linux (bash):** (substitute the Linux path for `LOG` if applicable)
```bash
LOG=~/Library/Logs/Unity/Editor.log

# Last 100 lines
tail -n 100 "$LOG"

# Filter for errors only
tail -n 500 "$LOG" | grep -E "Error|Exception|NullReference"

# Filter for game logs only
tail -n 500 "$LOG" | grep -E "\[CharacterManager\]|\[CarouselController\]|\[ScreenManager\]|\[RosterScreenController\]|\[LevelUpModal\]|\[CompareController\]"

# Watch live
tail -f "$LOG"
```

Note: Log resets each time Unity Editor starts. Contains a lot of noise from asset imports and compilation — always filter.

### Screenshots for visual review
Take a screenshot of the Game View for Claude (architect) to compare against references:
- In Unity Play mode, navigate to the screen you want to capture
- Menu: **GOLFIN > Screenshot > Capture Game View**
- Screenshot saves to `Assets/Screenshots/screenshot_YYYY-MM-DD_HH-mm-ss.png`
- Claude (architect) reads it directly via filesystem access (the local clone of this repo, wherever it lives — e.g. `C:\Users\<you>\GolfinRedux` on Windows or `~/Documents/GolfinRedux` on Mac)
- Reference images are in `Assets/References/` with `_compressed` subfolders for comparison
- Screenshots and references must be compressed (max 800px wide) for Claude to read them. Use the cross-platform Python script:
  ```bash
  pip install Pillow  # first time only
  python Docs/Scripts/compress_screenshots.py Assets/Screenshots
  ```
  (Windows users may also still run the PowerShell wrapper: `powershell -File Docs/Scripts/compress_screenshots.ps1 "Assets/Screenshots"`.)

Workflow:
1. Claude Code builds/changes UI
2. Navigate to the screen in Play mode
3. Run GOLFIN > Screenshot > Capture Game View
4. Compress: `python Docs/Scripts/compress_screenshots.py Assets/Screenshots`
5. Claude reads `Assets/Screenshots/_compressed/` and compares against references

### TellCode.md workflow
- Claude (architect) writes instructions to `Docs/TellCode.md`
- Claude Code reads this file at the start of each task
- After completing, add a status line at the bottom of the file

---

## Basic Rules

### 0. Pre-Commit Code Verification (MANDATORY)
**Before committing ANY C# file, verify it will compile. This is not optional.**

For EVERY new or modified .cs file, check these before saving:

1. **Using directives:** Read the top of the file you're editing. Does every type you reference have a corresponding `using` statement? Common ones missed:
   - `CharacterRarity` → needs `using Golfin.Roster;`
   - `ClubType`, `ClubDataRuntime`, `PlayerClubData` → needs `using Golfin.Inventory;`
   - `TextMeshProUGUI` → needs `using TMPro;`
   - `Image`, `Button`, `ScrollRect` → needs `using UnityEngine.UI;`
   - `Keyboard`, `Key` → needs `using UnityEngine.InputSystem;`
   - `DOTween` → needs `using DG.Tweening;`
   - `List<>`, `Dictionary<>` → needs `using System.Collections.Generic;`
   - `Action`, `Func` → needs `using System;`
   - `IEnumerator` → needs `using System.Collections;`

2. **Namespace consistency:** If the file is in `Golfin.Inventory` namespace, and it references a type from `Golfin.Roster`, it MUST have `using Golfin.Roster;`. Cross-namespace references are the #1 source of compile errors.

3. **Method signatures:** When calling a method on another class, READ that class first to verify the method exists with the expected name and parameters. Don't guess.

4. **Null safety:** Use `== null` not `??` for Unity objects (see lessons.md).

5. **After writing a file, scan it once more for red flags:**
   - Any type name you're not 100% sure about → grep the codebase for it
   - Any method call on a singleton → verify the method exists on that singleton
   - Any event subscription → verify the event exists with the correct delegate signature

**If in doubt, READ THE FILE you're referencing before writing code that depends on it.**

### 1. Plan Mode Default
- Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
- If something goes sideways, STOP and re-plan immediately — don't keep pushing
- Use plan mode for verification steps, not just building
- Write detailed specs upfront to reduce ambiguity

### 2. Subagent Strategy
- Use subagents liberally to keep main context window clean
- Offload research, exploration, and parallel analysis to subagents
- For complex problems, throw more compute at it via subagents
- One task per subagent for focused execution

### 3. Self-Improvement Loop
- After ANY correction from the user: update `tasks/lessons.md` with the pattern
- Write rules for yourself that prevent the same mistake
- Ruthlessly iterate on these lessons until mistake rate drops
- Review lessons at session start

### 4. Verification Before Done
- Never mark a task complete without proving it works
- Diff behavior between main and your changes when relevant
- Ask yourself: "Would a staff engineer approve this?"
- Run tests, check logs, demonstrate correctness

### 5. Demand Elegance (Balanced)
- For non-trivial changes: pause and ask "is there a more elegant way?"
- If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
- Skip this for simple, obvious fixes — don't over-engineer

### 6. Autonomous Bug Fixing
- When given a bug report: just fix it. Don't ask for hand-holding
- Point at logs, errors, failing tests — then resolve them
- Zero context switching required from the user

### Task Management
- Plan First: Write plan to `tasks/todo.md` with checkable items
- Verify Plan: Check in before starting implementation
- Track Progress: Mark items complete as you go
- Explain Changes: High-level summary at each step
- Document Results: Add review section to `tasks/todo.md`
- Capture Lessons: Update `tasks/lessons.md` after corrections

### Core Principles
- **Simplicity First:** Make every change as simple as possible. Impact minimal code.
- **No Laziness:** Find root causes. No temporary fixes. Senior developer standards.
- **Don't Duplicate:** Use existing utilities (RarityHelper, RarityStatCaps, ModalController) — never rewrite what exists.
- **Don't Rebuild Hierarchies:** If UI is already built in Unity, bind data to it. Don't recreate.

---

## Architect Handoff Workflow

Claude (claude.ai) acts as architect and produces spec files in `Docs/`. When specs exist:
1. Read the spec carefully before coding
2. The `_API_CORRECTIONS.md` file (if present) overrides the main spec where they conflict
3. Flag method names with `// NOTE:` if the spec's assumed API doesn't match actual code
4. After implementing a spec, move it to `Docs/archive/` and update `AI_CONTEXT.md`

---

## Project Overview

**Golfin Redux** is a golf-themed mobile game built in Unity (C#). The current focus is on the character roster management system — players collect characters, level them up by spending Reward Points, and allocate Skill Points (SP) across four stats.

## Build & Development

This is a Unity project — there are no custom CLI build commands. Development workflow:
- Open in Unity Editor via `GolfinRedux.sln` or by opening the project folder in Unity Hub
- Main scene: `Assets/Scenes/ShellScene.unity` (all UI screens live here)
- Gameplay scene: `Assets/Scenes/GameplayScene.unity`
- Editor tools for building UI hierarchies are in `Assets/Scripts/UI/Roster/Editor/`

## Architecture

### Screen Navigation Flow
```
Logo → Splash → Loading → Home (Hub)
                            ├→ Roster (character management)
                            ├→ Inventory (clubs, bags, balls, items)
                            ├→ Settings (modal overlay)
                            ├→ Gacha (not yet implemented)
                            └→ Gameplay (not yet implemented)
```

`ScreenManager` controls transitions with fade animations via `FadeController`. `PersistentUIManager` handles the top bar and bottom nav bar, showing them only on Home, Roster, and Inventory screens.

### Core Singletons
| Class | Location | Purpose |
|---|---|---|
| `CharacterManager` | `Assets/Scripts/CharacterManager.cs` | Central hub — roster, selection, level-up, stat allocation |
| `ClubManager` | `Assets/Scripts/ClubManager.cs` | Club ownership, equip/unequip, bag management |
| `RewardPointsManager` | `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | R-point currency, persisted via PlayerPrefs |
| `CharacterDatabaseCSV` | `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | Runtime CSV character loader (preferred over ScriptableObjects) |
| `ClubDatabaseCSV` | `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs` | Runtime CSV club loader |
| `CharacterLevelUpDatabase` | `Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs` | Level economy CSV lookup |
| `AudioManager` | `Assets/Scripts/Audio/AudioManager.cs` | Music/SFX playback |
| `ScreenManager` | `Assets/Scripts/UI/ScreenManager.cs` | Screen activation/deactivation with fade transitions |
| `PersistentUIManager` | `Assets/Scripts/UI/PersistentUIManager.cs` | Top/bottom nav bars visibility |
| `FadeController` | `Assets/Scripts/UI/FadeController.cs` | Screen fade transitions |

### Namespaces
| Namespace | Contents |
|---|---|
| `Golfin.Roster` | CharacterData, PlayerCharacterData, RarityHelper, RarityStatCaps, CharacterRarity, all roster UI scripts |
| `Golfin.Inventory` | ClubData, ClubDataRuntime, PlayerClubData, ClubType, ClubDatabaseCSV, all inventory UI scripts |
| (global) | CharacterManager, ClubManager, LocalizationManager, ScreenManager, PersistentUIManager |

### Character System

**Two-layer data model:**
- **`CharacterData`** (ScriptableObject) — base template: stats, portraits (`portraitThumbnail`, `portraitFull`), rarity, identity, localization keys
- **`PlayerCharacterData`** (plain C#) — player instance: level, SP earned/spent, pending SP allocation, selection state, stamina energy

**CSV-first architecture:** `CharacterDatabaseCSV` loads character data from `Assets/Data/Characters.csv` at runtime. `CharacterManager` tries CSV first, falls back to ScriptableObject database.

**Four stats:** Strength, Club Control, Recovery, Stamina
**Stat caps:** Rarity-based, defined in `RarityStatCaps.cs` (Common 25 → Supreme 50)
**Six rarities:** Common, Uncommon, Rare, Mythic, Legendary, Supreme
**Starting levels by rarity:** Common 10, Uncommon 40, Rare 80, Mythic 120, Legendary 160, Supreme 200
**Max levels by rarity:** Common 39, Uncommon 79, Rare 119, Mythic 159, Legendary 199, Supreme 239

**SP allocation** uses Strategy pattern: `ManualSPAllocation` (player-controlled) or `AutomaticStatAllocation`, both implementing `StatAllocationStrategy`.

**Level-up economy** is CSV-driven: `Assets/Data/LevelUpCosts.csv` — 240 levels, cost = level × 5 RP, SP reward = 1 per level. Shared between characters and clubs.

**Existing utilities (USE THESE, don't duplicate):**
- `RarityHelper.GetRarityColor(rarity)` — standard rarity colors
- `RarityHelper.GetRarityLabel(rarity)` — single letter labels (C/U/R/M/L/S)
- `RarityHelper.GetRarityBadgeTextColor(rarity)` — card badge text colors
- `RarityStatCaps.GetCap(rarity, statName)` — stat maximums
- `ModalController` — base class for modal dialogs (fade, backdrop, show/hide)

### Club System

**Two-layer data model (mirrors character system):**
- **`ClubDataRuntime`** — template from Clubs.csv: stats, sprites, rarity, type, brand
- **`PlayerClubData`** — player instance: level, durability, equip slot

**Six club stats:** Power, Accuracy, Lie Resistance, Loft, Durability (consumable), Distance (derived, no bar)
**Club types:** Driver, Wood, Iron, A.Wedge, P.Wedge, S.Wedge, Putter
**Same rarity/level system as characters**

### Roster UI Hierarchy (Unity)
```
Canvas > ScreensRoot > RosterScreen
├── CarouselSection
│   ├── LeftArrow / RightArrow
│   ├── ScrollView → Viewport → PaginationDots
│   └── DetailPanel
│       ├── LeftPanel → Character (full-body Image)
│       └── RightPanel
│           ├── CharacterNamePanel → CharacterNameText (single TMP, use \n for first/last)
│           ├── RarityPanel → RarityRow (3 TMPs: rarity label, current lv, /max lv)
│           ├── CharacterStatsPanel
│           │   ├── CharacterStats1 (StatIcon + Name+Bar/StatsName/Bar + StatNumber)
│           │   ├── CharacterStats2 (same structure)
│           │   ├── CharacterStats3 (same structure)
│           │   └── CharacterStats4 (same structure)
│           ├── ButtonsPanel → LevelUpButton / BoostButton
│           ├── BioPanel → BioHeader / BioText
│           ├── CompareButton
│           └── SelectButton → Text (TMP) / Rim
```

**Stat row binding (Transform.Find paths):**
- `statRow.transform.Find("Name+Bar/Bar")` → `Image.fillAmount`
- `statRow.transform.Find("StatNumber")` → TMP text `"{current}/{cap}"`

**Stat bar colors:**
- Blue — normal
- Green — stat equals rarity cap (maxed)
- Red — stamina bar only, when `currentStaminaEnergy` is low (runtime energy, NOT the stat value)
- Orange — Level Up modal only, pending SP allocation preview

### Roster UI Scripts
| Script | Purpose |
|---|---|
| `RosterScreenController` | Top-level roster screen, displays RP, subscribes to manager events |
| `CarouselController` | Horizontal card carousel, pagination, fires `OnCharacterSelected` |
| `CharacterThumbnailCard` | Individual carousel card (portrait, rarity badge, level) |
| `CharacterDetailPanel` | Full character detail view (portrait, stats, buttons, bio) |
| `StatBar` | Reusable stat visualization (icon, label, fill bar, value text) |

### Events
| Publisher | Event | Subscribers |
|---|---|---|
| `CharacterManager` | `OnCharacterLeveledUp(string)` | RosterScreenController, CharacterDetailPanel |
| `CharacterManager` | `OnCharacterSelected(string)` | RosterScreenController, CharacterDetailPanel |
| `CharacterManager` | `OnRosterChanged()` | RosterScreenController, CarouselController |
| `RewardPointsManager` | `OnPointsChanged(int)` | RosterScreenController, HomeScreenController |
| `CarouselController` | `OnCharacterSelected(string)` | CharacterDetailPanel |
| `ClubManager` | `OnClubEquipped(string)` | ClubDetailPanel |
| `ClubManager` | `OnClubLeveledUp(string)` | ClubDetailPanel |
| `ClubManager` | `OnInventoryChanged()` | ClubCarouselController |

### Data Files
| File | Purpose |
|---|---|
| `Assets/Data/Characters.csv` | Character data (CSV-first, loaded by CharacterDatabaseCSV) |
| `Assets/Data/Clubs.csv` | Club data (CSV-first, loaded by ClubDatabaseCSV) |
| `Assets/Data/CharacterDatabase.asset` | ScriptableObject character templates (fallback) |
| `Assets/Data/LevelUpCosts.csv` | Level economy: 240 levels, cost = level × 5, SP = 1 (shared) |
| `Assets/Data/HoleDatabase.csv` | Hole definitions |
| `Assets/Data/HoleDatabase.asset` | ScriptableObject hole collection |

### Localization
`LocalizationManager` loads CSV files from `Assets/Localization/`. Key prefixes: `HOLE_*`, `CHAR_*`, `ROSTER_*`, `HOME_*`, `CLUB_*`, `MODAL_*`, `COMPARE_*`. Currently supports English and Japanese.

### Key Patterns
- **Events:** C# `System.Action` delegates, subscribe in `OnEnable`, unsubscribe in `OnDisable`
- **Namespaces:** `Golfin.Roster` for roster, `Golfin.Inventory` for clubs
- **Modals:** Extend `ModalController` base class
- **Sprites:** Load via `Resources.Load<Sprite>()`, NOT Inspector arrays
- **Prefab Builder Tools:** Editor scripts in `Assets/Scripts/UI/Roster/Editor/` and `Assets/Scripts/UI/Inventory/Editor/`
- **UIAutoWire:** `Assets/Scripts/Utilities/UIAutoWire.cs` for component auto-discovery
- **Unity null checks:** Always `== null` not `??` (see lessons.md)
- **Input system:** Always `UnityEngine.InputSystem`, never `UnityEngine.Input`
- **Platform:** Cross-platform team — contributors work on both Windows (PowerShell) and macOS (bash/zsh). Use the platform-appropriate variant of any helper script (`.ps1` on Windows, `.sh` / `.py` on Mac/Linux). Don't hardcode absolute paths or shell-specific syntax in shared docs or tooling.

---

## Conventions

### Asset & File Naming
Follow `Docs/Game Design/ASSET_NAMING_CONVENTION.md` for ALL new assets. Key rules:
- **Prefixes:** `S_` sprite, `BG_` background, `ICO_` icon, `T_` texture, `MESH_` 3D model
- **No spaces in filenames or folders** — use PascalCase or hyphens
- **Characters:** `S_Char_{Name}`, `S_CharFull_{Name}`, `S_CharHome_{Name}`
- **Clubs:** `S_Club_{Type}-{Brand}`, `S_ClubFull_{Type}-{Brand}`
- **UI elements:** `ICO_{Name}`, `S_Btn_{Name}_{State}`, `S_Rarity_{Name}`, `S_Rim_{Variant}`
- **Scripts:** `{System}Manager`, `{System}DatabaseCSV`, `{System}DetailPanel`, `{System}CompareController`
- **Unity hierarchy:** `{Screen}Screen`, `{Name}Panel`, `{Action}Button`, `{Name}Text`, `{Name}Row`
- **Localization keys:** `{SCREEN}_{ELEMENT}` (e.g., `CLUB_POWER`, `ROSTER_LEVEL_UP`)
- **CSV IDs:** `char_{name}`, `club_{type}_{brand}`
- **DO NOT rename files in Resources/** without updating the corresponding CSV values

### Localization
- All **new** user-facing text should use localization keys from the start: `LocalizationManager.Get("KEY")`
- Use the pattern `SCREEN_ELEMENT` (e.g., `ROSTER_LEVEL_UP`, `HOME_PLAY_BUTTON`, `MODAL_CONFIRM`)
- Add both EN and JP entries to the localization CSV when creating new text
- Legacy hardcoded text will be migrated in a dedicated localization pass (not yet scheduled)
- Rich text tags like `<color=#EEDC9A>` are supported in localization values — TMP handles them natively

---

### Asset & File Naming Convention
**Full reference:** `Docs/Game Design/ASSET_NAMING_CONVENTION.md` — READ THIS before creating any new assets.

Quick rules:
- **No spaces** in filenames or folder names — use PascalCase or hyphens
- **Prefixes:** `S_` sprite, `T_` texture, `MESH_` 3D model, `BG_` background, `ICO_` icon, `FX_` effect, `SFX_` sound, `MUS_` music
- **Characters:** `S_Char_{Name}`, `S_CharFull_{Name}`, `S_CharHome_{Name}`
- **Clubs:** `S_Club_{Type}-{Brand}`, `S_ClubFull_{Type}-{Brand}`
- **UI elements:** `ICO_{Name}`, `S_Btn_{Name}_{State}`, `S_Rarity_{Name}`, `S_Rim_{Variant}`
- **Scripts:** `{System}Manager`, `{System}DatabaseCSV`, `{System}DetailPanel`, `{System}CompareController`
- **Unity hierarchy:** `{Screen}Screen`, `{Name}Panel`, `{Action}Button`, `{Name}Text`, `{Name}Row`
- **Localization keys:** `{SCREEN}_{ELEMENT}` (e.g., `CLUB_POWER`, `ROSTER_LEVEL_UP`)
- **CSV IDs:** `char_{name}`, `club_{type}_{brand}`
- **DO NOT rename files in Resources/** without updating the corresponding CSV values

---

## Development Docs

| File | Purpose |
|---|---|
| `Docs/README.md` | Index map — what's where in Docs/ |
| `Docs/AI_CONTEXT.md` | Tiny core memory — current status, active work |
| `Docs/Tasks.md` | Current checklist and backlog |
| `Docs/Rules.md` | Design constraints, Figma specs, conventions |
| `Docs/TellCode.md` | Architect instructions for Claude Code |
| `Docs/PUNCH_IT_ROUTINE.md` | **What "punch it" means.** The three TestFlight ship phrases (`punch it` / `punch it GPS` / `punch it standalone`), their lanes and profiles, and the standing permissions around them — the phrase itself is the authorization. Mechanics: `Docs/TESTFLIGHT_RUNBOOK.md`. |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | Auto-generated — file tree, singletons, events |
| `Docs/Architecture/PATTERNS.md` | Recurring patterns across the codebase |
| `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md` | **Read before building any Figma-node screen.** Capture instrument, per-screen backgrounds, panels/fills/bars, text, controller-owned state, node-asset traps, and the self-diff to run before surfacing. |
| `Docs/Architecture/UI_HIERARCHY.md` | Scene UI paths reference |
| `Docs/Architecture/INVENTORY_REFERENCE.md` | Inventory system patterns + APIs |
| `Docs/Scripts/generate_audit.ps1` / `Docs/Scripts/generate_audit.sh` | Script to regenerate the audit (PowerShell on Windows, bash on Mac/Linux) |
| `Docs/Scripts/compress_screenshots.py` / `Docs/Scripts/compress_screenshots.ps1` | Compress screenshots to ≤800px (Python is cross-platform; .ps1 is a Windows wrapper) |
| `Docs/Game Design/GAME_DESIGN_CHANGELOG.md` | Game design changes from original GDD |
| `Docs/Game Design/ASSET_NAMING_CONVENTION.md` | Asset & file naming rules |
| `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md` | Simplified gameplay formulas (proposal) |
| `Docs/Reference/GAME_DESIGN_AGENT.md` | AI agent for evaluating GDD systems |
| `Docs/Pipeline/` | Course-pipeline lessons + specs (ADD_HOLE, BUNKER_*, TEE_SKIRT, fringe meshes) |
| `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` | **READ before touching fairway/tee fringe/border code.** Hard-won lessons on submesh baking, dilated CDT, and the Lite vs Geo importer trap. |
| `Docs/Pipeline/TREES_AND_GENERATED_SCENES.md` | **READ before touching hole trees.** Generated scenes are per-machine; trees live in tracked TerrainData + `Data/hole-NN-geo/standalone_trees.csv`. Rebuild + Validate after pulling; never re-import a hole to fix trees. |
| `Docs/Physics/` | Physics architecture, tuning targets, and post-mortem lessons |
| `Docs/Specs/` | Active / Queued / Completed specs |
| `Docs/Diagnostics/` | In-flight diagnostic outputs (CSVs, milestone done reports) |
| `Docs/Backups/` | Restore points for risky migrations |
| `Docs/Archive/` | Completed phase specs (historical) |
| `tasks/lessons.md` | Accumulated corrections and patterns |