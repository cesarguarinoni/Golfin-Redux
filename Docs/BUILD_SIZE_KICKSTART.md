GOLFIN build-size diet — Architect session kickstart (from the GPS Architect session, 2026-09-04).
You are the Architect for the GAME build-size track (Notion 2121, P1). Before anything else read, in
this order: Docs/TellCode.md (CURRENT STATE block + SPEC_READY POINTERS), CLAUDE.md (pipeline rules —
two-gate review, Rule 18/21 gates, §23 deploy proofs), Docs/PIPELINE_HARDENING.md §20–§23,
Docs/BUILD_SIZE_AUDIT.md (the numbers: what the 711 MB .ipa / 1.9 GB install actually is, per bucket
and per file), Docs/Specs/Active/build_size_diet/SPEC.md + STATUS.md (SPEC_READY — five phases, the
kickoff is already in TellCode), and the finished reference task whose size half you inherit:
Docs/Specs/Completed/gps_standalone_shell/ (IMPLEMENTER_REPORT.md round 2: R2 Resources stash,
R4 scene strip, R5 texture overrides, and the .ipa-vs-Payload reading of a size target). Confirm
you've read them and summarise the plan back to me in three lines.
Workflow (follow exactly): spec folders go in Docs/Specs/Active/<slug>/ (SPEC.md + STATUS.md =
SPEC_READY, reference/, screenshots/, videos/); Docs/TellCode.md gets a SPEC_READY pointer AND the
kickoff text; every kickoff is ALWAYS re-pasted in chat as a fenced code block for me to give Claude
Code — never refer back to an earlier message; SQL for Supabase is pasted in chat (n/a here); Cowork
never runs git commit/add — Code commits. Use `git --no-optional-locks` from Cowork (stale
index.lock). Keep every doc IN THE REPO (Docs/…), never only in the session workspace — a previous
session's workspace notes were lost. When I say "Code is done": read the spec folder's reports,
verify the acceptance items against HEAD yourself — re-derive the numbers from the Build Report and
the .ipa, do not trust a table (this pipeline has a history of optimistic PASS rows), then report.
Repos on the Mac (folder grants): /Users/cesar/Documents/GolfinRedux (game; mounted at
$HOME/mnt/GolfinRedux in the device shell). Builds land in Builds/ipa/ (Golfin.ipa = game,
GOLFINGPS.ipa = standalone) with the Unity Build Report in Builds/unity-build-ios.log. Notion roadmap:
GOLFIN_Roadmap (data source 364b3e97-02b7-8190-b82b-000ba7847856), row 2121 build_size_diet.
Facts you inherit (verified 2026-09-03/04, do not re-derive from memory):
- The user-facing number is the INSTALL (~1.9 GB game / 236 MB standalone), not the .ipa; Symbols/
  inside the .ipa (~490 MB) are stripped by Apple. Targets in the spec are install ≤ 1.0 GB and
  Golfin.ipa ≤ 350 MB — state which measure you quote every time.
- heightmap.bytes is GHM1 = int32 Q16.16 fixed-point (physics is fp-deterministic), NOT float32 —
  compression must be LOSSLESS (GHM2 = row-delta + Deflate). Parity gate = decoded int[] identical
  + smoke-bot AtRest positions bit-identical. Heightmap resolution stays 2049 (perf-pass rule).
- Biggest buckets: sharedassets8.assets.resS 480 MB (vegetation-pack textures: Leave_4K_.psd 4096
  compression None, Simple Trees leaves at 8192, Mobile_Tree_Bundle); 18 hole TerrainData ~30 MB
  each; Resources/HoleData 389 MB on disk (+ ~100 MB pretty zones.json); Resources/Clubs 115 MB
  source; 93 textures with compression None; NotoSansJP TTF 9.1 MB via a Dynamic TMP atlas.
- Placed trees are Spruce 1/3 only (15,197 instances) — unused terrain prototypes drag pack
  textures into the scene bundle.
- The standalone's StandaloneBuildPreprocessor MOVES Resources/HoleData (and 12 other golf folders)
  out during its build; anything Phase 2 renames under HoleData must keep that stash/restore and
  the .standalone_moved sentinel working — the spec has it as an acceptance row.
- The whole golf codebase compiles into UnityFramework (110 MB, byte-identical in both apps).
  Carving it out is PARKED (backlog row in Docs/GPS/GPS_BACKLOG.md): loud path only (asmdef
  defineConstraints), never managedStrippingLevel High. Not in build_size_diet's scope.
Decisions already made by me (Cesar):
- Zero visible change on device, byte-identical physics. Anything that trades fidelity for bytes
  (terrain alphamaps, Phase 5) is measurement + an A/B capture pair for my approval — no terrain
  edit without my written "go" in STATUS.md. Same for the JP font: if a static atlas still ships the
  TTF because of the fallback, I pick (a) keep dynamic / (b) static+fallback — Code reports, I choose.
- Load times may not regress: hole-load wall time after ≤ before + 100 ms (gate in the spec).
- Each phase is its own commit with Build Report numbers cited against reference/*_before.txt.
Coordination: Claude Code works one task at a time in this repo. The GPS track's queue is EMPTY as of
2026-09-04 (its Architect session stays for device-pass defects, which arrive as quick specs and
can interleave); the game-polish track (design_consistency_audit → game_polish_a) has its own
Architect session — sequence build_size_diet behind whatever is IMPLEMENTER_WORKING in
Docs/Specs/Active/*/STATUS.md, edit only your own pointers/kickoffs in TellCode.md, never touch
Assets/Scripts/UI/Gps/* or the GPS specs. Phase 1's prototype edits and Phase 3's .meta sweep touch
files the polish track may also touch (UI textures) — check its STATUS before Phase 3 goes out.
First deliverable: read the spec, tell me if you disagree with any phase or its order (push back
with substance, not agreement), then re-paste the build_size_diet kickoff from TellCode for me to
give Claude Code.
