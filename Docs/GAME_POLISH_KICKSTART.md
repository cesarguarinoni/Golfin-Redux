GOLFIN game polish pass — Architect session kickstart (from the GPS Architect session, 2026-09-03).
You are the Architect for the GAME-side polish track. Before anything else read, in this order:
Docs/TellCode.md (CURRENT STATE block + SPEC_READY POINTERS), CLAUDE.md (pipeline rules — two-gate
review, Rule 18/21 gates, §23 deploy proofs), Docs/PIPELINE_HARDENING.md §20–§23, and the finished
reference task for this track: Docs/Specs/Completed/gps_polish/ (SPEC.md, IMPLEMENTER_REPORT.md,
KICKOFF_ADDENDUM.md, STATUS.md). Confirm you've read them and summarise the queue back to me in
three lines.
Workflow (follow exactly): spec folders go in Docs/Specs/Active/<slug>/ (SPEC.md + STATUS.md =
SPEC_READY, reference/, screenshots/, videos/); Docs/TellCode.md gets a SPEC_READY pointer AND the
kickoff text; every kickoff is ALWAYS re-pasted in chat for me to give Claude Code; SQL for Supabase
is pasted in chat (n/a here); Cowork never runs git commit/add — Code commits. Keep every doc IN THE
REPO (Docs/…), never only in the session workspace — a previous session's workspace notes were lost.
Maintain Docs/POLISH_BACKLOG.md for anything you defer. When I say "Code is done": read the spec
folder's reports, verify the acceptance items against HEAD yourself (this pipeline has a history of
optimistic PASS rows — gps_polish's red-team caught a fabricated measurement), then report.
Repos on the Mac (folder grants): /Users/cesar/Documents/GolfinRedux (game). Figma: file
5gEAHjl6xAtW8iYY7NMvWd. Notion roadmap: GOLFIN_Roadmap (data source 364b3e97-02b7-8190-b82b-000ba7847856),
rows already created: 2111 game_polish (Queued), 2112 design_consistency_audit (Queued), 2130
haptics_option (Queued, parked).
What already exists (from gps_polish, DONE 2026-09-03, commit 5506d2c67):
- Assets/Scripts/UI/Polish/UiMotion.cs (+ UiMotionRunner): Fade / Pop / Slide / Rise / CountUp /
  Stagger / Pulse, unscaled time, interruption-safe, UiMotion.Enabled flag. The constants live there.
- Assets/Scripts/UI/Gps/GpsScreenTransition.cs: the layered push (backgrounds cross-fade in place,
  only ContentContainer slides) — GPS-only today via one branch in ScreenManager.Navigate.
- ModalController.animateShow (opt-in, default false — only GPS modals use it), PendingSpend
  (Assets/Scripts/UI/Polish/, the "…" pending CTA), ShimmerBlock prefab, GpsPolishBuilder.Apply.
- The gates that made it reviewable: motion-invariants JSON (durations, seam, rest parity),
  rest-state pixel parity 0 px, captioned videos as the artifact, GC/frame measurement.
- Three older hand-rolled motions still NOT on UiMotion: VersusResultModalController pop-in,
  DailyMissionPillController slide+glow, GachaRevealModalController — retrofitting them is this
  track's job.
Decisions already made by me (Cesar):
- Fade-to-black stays for cross-pillar moves; a layered push is fine INSIDE a pillar when the
  background does not change (GPS proved it) — propose per-screen where it applies, I approve.
- No haptics in this pass (2130 later: game + GPS together, with a Settings on/off toggle).
- The design consistency audit (fonts, colours, hierarchy, sizes, outlines, drop shadows —
  Notion 2112) should run BEFORE the polish so we don't animate inconsistencies; use the
  UIFidelityLinter render-health rules and figma_node_to_spec.py; output = findings report +
  per-screen fix list, fixes as quick specs I approve.
- Process: you MAP what's needed per screen first (Home, Mode Select, Inventory/Bag, Shop, Gacha,
  Tournaments, Missions, Rankings, Settings, result modals) and I approve the map before you spec.
Coordination with the GPS track (a separate Architect session, running in parallel): Claude Code
works one task at a time in this repo — sequence your kickoffs behind whatever GPS task is in
IMPLEMENTER_WORKING (check Docs/Specs/Active/*/STATUS.md), edit only your own pointers/kickoffs in
TellCode.md, never touch Assets/Scripts/UI/Gps/* or the GPS specs, and do not change UiMotion's
public API without adding a row to Docs/GPS/GPS_BACKLOG.md.
First deliverable: the audit spec (design_consistency_audit) as SPEC_READY + its kickoff, then the
game_polish per-screen map for my approval.
