READY_FOR_SELF_REVIEW

# STATUS — `game_polish_a`

**Current:** `READY_FOR_SELF_REVIEW` — **iteration 2**. The self-reviewer passed iteration 1; I then
found and fixed a real defect (the shared top-bar title snapped after every push), so the code has
changed and the gates must re-run on it. Notion 2111, slice a of three.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Map approved by Cesar (G1 = fade + option-(b) video behind an OFF flag). |
| 2026-09-04 | `IMPLEMENTER_WORKING` | Kicked off by Cesar directly (`design_consistency_audit` is still `SPEC_READY` — flagged, not blocked on). |
| 2026-09-04 | `READY_FOR_SELF_REVIEW` | Code + gates done. A4 2 of 6 clips. A2 run-invalid-diagnosed. A6 N/A. A8 owed. |
| 2026-09-04 | **OPTION (b) SHIPPED** | Cesar approved the clip. The flag is REMOVED (not flipped); different backdrops now push and cross-fade within a pillar. Re-measured: **84 pushes, `fail == 0`**, 32 of them cross-backdrop across **16 ordered pairs that used to fade**. Polish suites 91/0. |
| 2026-09-04 | **ALL GATES CLOSED** | A4 all six clips (e re-recorded — the first take was 16 identical frames). A2 **PASS**: 16 states, worst 1.232 %, residuals localised to the RP counter. A8 **PASS**: six mid-rise frames + SkippedForPush=94 over 84 pushes. A13 **PASS**. Full EditMode sweep **2425 passed / 0 failed**. |
| 2026-09-04 | `SELF_REVIEW_PASS` | Full acceptance re-walked (Rule 5); A1 JSON re-parsed (87 pushes, fail=0); A2 confirmed via independent pixel-bbox on 8 pairs (all bboxes anchor at y=147, deepest y=892, five identical RP-counter rectangles); scene mutation audit clean (263+/3-, zero anchor/isActive lines); GPS scope = exactly one authorised file; standing bans clean; option-b flag verified removed by grep. Three hygiene notes (stale caption on `a4_option_b_transition_strip.png`, §A9 prose drift, 84↔87 count drift) surfaced but not blocking. |

| 2026-09-04 | `READY_FOR_SELF_REVIEW` | **iter-2.** Found while rebuilding the A4 strip after the pass, not by a gate: the shared top-bar centre title carried the LEAVER's name for the whole 0.25 s push and then hard-cut in one frame (`ApplyScreen` is deferred to `Settle` by design; that pair used to fade to black, which hid it). Fixed — the title now dissolves over `FadeDur` from push START, landing before the content settles. It shipped broken once via `??` on a `GetComponent` returning a fake-null, so both fades silently no-oped; `CenterTitleDissolveTests` (5, tripwire-verified) pins it. Shape audit per §15 done for both shapes, sites-that-were-fine included. Also cleared the self-review's three hygiene notes. Sweep **2430 passed / 0 failed**. |

**Read the report's §0 first** — three things carry outside this task:

1. The Editor's active build profile was `iOS-Standalone`; it is now **`iOS-Full-GPS`**, which is
   what this task's two bars live in. Switch it back when the standalone lane is next built.
2. `264ee64f5` also carries `map_view_v2`, `content_art.txt`, `GPS_BACKLOG.md` and `TellCode.md`
   from Cesar's own in-flight session. Verified intact; history not rewritten because that session
   was live on this branch.
3. Two findings that are NOT this task's code, both flagged for separate work:
   **(a)** every arrival at `GachaHistory` allocates ~290 MB and stalls > 1 s (`RebuildList` in
   `OnEnable`); **(b)** tapping `ModeSelection/TournamentTempEntry` ends the play session — it
   killed four separate measured runs, and that pair is a FADE, so `LayeredPush` is not on it.

**Shipped behaviour:** `videos/game_polish_a_f_cross_backdrop.mp4` is the cross-backdrop push —
this is what the app does now. `videos/game_polish_a_a_play_pillar.mp4` is the same-backdrop push
plus the new nav selected state. Home and cross-pillar still fade to black.

**A9 is void** — there is no flag left to pin off. (The report's §A9 body has been rewritten to say so; it previously still quoted the removed declaration.) Its replacements are
`LayeredPushTests.TheOptionBFlag_IsGone` and `SameBackground_IsNoLongerRequiredByTheGate` (both
pass; source-grep confirms `AllowBackgroundCrossFade` exists ONLY inside those tests, asserting
absence).

**Every acceptance item is now closed**: A1 A2 A3 A4 A5 A7 A8 A9(void) A10 A11 A12 A13 A14 A15 A16
pass; **A6 is N/A** with the reason stated (no Figma node, no prefab layout touched). The three
carry-over items in this file are environment and other people's code, not open work on
this task.
