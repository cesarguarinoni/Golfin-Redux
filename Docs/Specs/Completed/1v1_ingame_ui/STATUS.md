DONE

# STATUS — `1v1_ingame_ui`

**Current:** `DONE`
**Tier:** FULL PIPELINE
**Notion:** Order 343
**Phase:** 1 of 2 (UI). Phase 2 = bot AI + turn-flow + win/tie (not yet specced).

Approved by Cesar in chat ("If that is so, done.") on 2026-06-09, after confirming the
YOUR-TURN banner not firing on a real first shot is expected Phase-1 behavior (per-turn
banner triggering is Phase 2; Phase 1 drives the banner from the debug control only).

## Outcome

Phase-1 1v1 in-game HUD shipped: two-player cards (P1 active top-left, P2 clone mirrored
top-right, 1.0/0.50 opacity swap via `MatchContext.SetActive`), turn-announcement banner
(silver borders, Rubik-SemiBold, auto-size, left/right swipe by player index), versus-only
mini-map relocation (above Fade/Draw, image-only, right-edge aligned), and a `MatchContext`
versus data layer. Solo/Practice HUD byte-identical. All gated behind `GameSession.IsVersus`.

Took 13 implementer iterations across the pipeline + 2 hard Cesar rejections + 2 directed
polish rounds. Pipeline misses logged to `.claude/review_misses.log` (rounds 1-2).

## Deferred to Phase 2 (out of scope, NOT built)
- Bot AI / opponent shot-playing; turn-flow state machine; win/tie + winner banner.
- Driving the real active-player toggle + per-turn banner from gameplay (Phase 1 = debug control).

## Known cosmetic nit (non-blocking, Cesar-aware)
- nav video caption renders the `→` arrow as a tofu box (ffmpeg font-glyph limitation);
  HUD content correct. Trivially swappable in a future caption pass if desired.
