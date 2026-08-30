DONE

Approved by Cesar 2026-08-30. Phases A, B, C and the §21 end-to-end run are live on prod and
the mode is OPEN — `modes` v8 unlocked it, `texts` v16 carries the 131 keys Phase C renders.

Phases A, B and C are live. The door is OPEN: `modes` v8 flipped `missions.locked` to false,
which is what makes any of this reachable by a player — the mode opened with a publish, not
with a build.

## Deployed state (verified 2026-08-29)

  schema      2026_08_29_missions.sql applied — 6 tables, 2 functions, 5 earn actions,
              RLS on with no policies, anon refused (HTTP 401) on all six
  catalogs    all seven seeded at v1; missions v2, mission_tiers v2
  mirrors     golfin_mission_rewards 40 (10 per tier, RP 15/25/40/60)
              golfin_mission_tier_bonus 4 (50/100/200/300, missions_in_tier 10)
              the two agree, so the 10-of-10 bonus is reachable in every tier
  API         playlife-api image deployment-01M16C18A8ETDJ6HRPVJ0PNRWR (v63, nrt)
              /api/v1/missions/* answer 403 (auth), not 404
  dashboard   Cloudflare d1f9befc-b216-4b42-b34d-798d8789ef43
  content     mission 37 re-sited hole 13 -> hole 8, published

## Phase C state (Editor-verified 2026-08-29)

  screen      MissionSelectionScreen cloned from HoleSelectionScreen; MissionCard.prefab
              cloned from HoleCard.prefab (guid 6717663c8484640909c58d78cd02f8c2)
  entry       reached by invoking the real ModeCardController.playButton.onClick, in both
              ModeSelectScreenController and ModeCarouselController (the carousel keeps
              three copies of every card, so only the real onClick proves it)
  daily       GET /api/v1/missions/daily bound live — hole, reset countdown, and the reward
              the server decided (today's draw hit DOUBLE_RP, hence x60 vs a campaign x15)
  layout      daily card sized to its measured content (374px, symmetric 24px padding) and
              CardsContainer gives back the same 34px, so its bottom edge stays at worldY 344,
              identical to HoleSelectionScreen's
  outline     the daily card carries a gold rim: S_GachaCardBorder3 (a real stroke-on-transparency
              9-slice atom) tinted #EEDC9A at ppu 0.5. Measured on the frame: (238,220,154)

## Gates

  Unity EditMode   2035 tests / 2032 passed / 0 failed / 3 pre-existing skips
  Scene guardrail  ShellScene vs HEAD: 0 fileIDs lost, 0 active-state flips
  Backend          172 passed
  Dashboard        126 passed, tsc clean, next build green
  Tools/content     35 passed


## Video deliverable (2026-08-29)

  raw          Docs/Specs/Active/missions_v1/videos/raw.mp4 — 1170x2532, 29.7s, 30fps
  captioned    Docs/Specs/Active/missions_v1/videos/missions_v1_mission_selection.mp4
  report copy  Docs/Reports/Media/missions_v1_mission_selection.mp4
  recorder     Assets/Scripts/UI/Editor/MissionsDemoRecorder.cs
               (GOLFIN > Missions > Record Demo Video)

Driven through the real entry path: the title gate is tapped, then Home, then the PLAY
button on the Missions mode card itself — not ScreenManager.ShowScreen. The run spends the
real 50 RP entry fee, which is why the RP counter reads 458 rather than 508.

## §21 live end-to-end (2026-08-30) — the last gate, now closed

Every ledger row below was read back from the database, not from a client log.

  mission_clear:1      +15 RP   idempotency 6baf8da4-c27b-3215-8590-50a72f501a02
  mission_replay:1      +5 RP   idempotency 8d26dd77-8db8-3dc6-ae4b-392127369959
  daily_mission         +30 RP  idempotency 8d7a291a-062d-4a5a-85b7-5a0683be398b
                                daily_mission_claims 2026-08-30 streak=1 strokes=4

The daily round: `daily:2026-08-30 cleared=True strokes=4 putts=1
goals=[SHOTS:True, AVOID:True]` — four strokes against a cap of five, no bunker — played
through the mode card and then the daily card's own `actionButton.onClick`.

**The claim had never fired before this run.** `Endpoints.MissionsDailyClaim` had shipped in
Phase A as a string with no sender, so no daily had ever paid. Harness:
`Assets/Scripts/Editor/Missions/DailyClearHarness.cs`, which deliberately does NOT arm
`BotSessionOverride` the way every capture harness does — that override forces the points
backend OFF, which is exactly wrong when the point is to prove a payout.

**Determinism, demonstrated rather than asserted.** 2026-08-30's recipe was computed locally
before the server had ever seen the date, and the server's first generation matched field for
field: `H10 par4 TEE_FRONT CROSS_L SUP_FULL [SHOTS 5 · AVOID Bunker] NONE`.

**Recovery path walked.** `MissionSession.Clear()` does not null `PendingDaily`, so the
finished round survived a real quit-to-Home teardown (`ResetSession` + scene unload) and was
claimed on the next visit to Missions.

## Carried forward — NOT done, deliberately

  * **Recipe pinning** — blocked on `PLAYLIFE_API_URL` + `PLAYLIFE_ADMIN_KEY` on the
    `golfin-admin` Worker. The Pin button renders only inside a Daily-preview row, so no
    preview means no pin. Cesar's step; nothing in the repo unblocks it.
  * **The daily's result card wants one look.** `77436c36a` fixed the daily falling through to
    the generic hole cards (the played mission was resolved by scanning `MissionCatalog.All`,
    which holds campaign CSV rows only — the daily's runtime-composed definition was never in
    it). The daily is once-per-date and today's is claimed, so its card is disabled and could
    not be re-entered to see it render.
  * **The daily claim trusts the client.** `golfin_daily_claim` records `p_strokes` and never
    checks it against the recipe's goals; it enforces the hash, the once-per-date lock, the
    streak and DOUBLE_RP. Whether the daily was cleared is the client's word. Surfaced as a
    product decision, not patched.
  * **JA capture** and the **Figma fidelity table + UI lint** — Cesar waived the fidelity pass
    ("Design looks right. No fidelity pass needed until I give it a proper eye").
