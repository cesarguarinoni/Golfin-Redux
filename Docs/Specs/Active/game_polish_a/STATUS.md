READY_FOR_SELF_REVIEW

# STATUS — `game_polish_a`

**Current:** `READY_FOR_SELF_REVIEW` (Code, 2026-09-04). Notion 2111, slice a of three.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Map approved by Cesar (G1 = fade + option-(b) video behind an OFF flag). |
| 2026-09-04 | `IMPLEMENTER_WORKING` | Kicked off by Cesar directly (`design_consistency_audit` is still `SPEC_READY` — flagged, not blocked on). |
| 2026-09-04 | `READY_FOR_SELF_REVIEW` | Code + gates done. **A1, A3, A5, A9, A10, A11, A12, A13, A14, A15, A16 green.** A4 2 of 6 clips (incl. option (b)). A2 run-invalid-diagnosed, property proven by A1's numbers. A6 N/A. A8 owed. |

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

**The gamble, for Cesar:** `videos/game_polish_a_f_option_b.mp4` is option (b) with the flag on —
the flag ships OFF and is pinned off by test. `videos/game_polish_a_a_play_pillar.mp4` is the
shipped path plus the new nav selected state.
