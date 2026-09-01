# Kickoff — `ball_art_and_stats` (paste into a NEW Cowork conversation)

> Archive copy. The delivery is the chat block Cesar pastes; this file is so the task survives the
> session. Runner must be **Cowork/Architect** — it needs Chrome via claude-in-chrome plus device
> folder grants for `/Users/cesar/Documents/GolfinRedux`. Claude Code cannot drive Chrome.

```
Read Docs/Specs/Active/ball_art_and_stats/SPEC.md and BALL_IDENTITY.md in the same
folder, and execute the spec. You are the Cowork runner; you also write the Claude
Code spec at the end (SPEC §6 / D5) — do not expect one to exist.

Context:
- Balls are the last catalog on 2 rows. 20 ball designs exist as hand-made 1000x1000
  art in Assets/Art/Original UI/Ball Sprites/; only Golfin and Putt Ace have a full
  detail image, a Balls.csv row or stats.
- Art job: 18 x Assets/Resources/Balls/Full/<Name>.png, 537x900 RGBA, 30px rounded
  corners. It is a W1 scene swap — FIRST image is Full/Golfin.png (the scene to keep),
  SECOND is that ball's S_Controls_Ball_<BRAND>.png (colour and graphics only). Every
  ball already has reference art, so there is no bootstrapping this time.
- Stat job: all 20, tiered by rarity, budget in SPEC §4.3. The physics coefficients are
  in SPEC §4.1 — read them before you touch a number. +Roll means the ball rolls
  FARTHER, on the fairway and on the green; a ball that stops needs negative Roll.
- BALL_IDENTITY.md has a first-pass table for all 20. It is DRAFT. Take it to Cesar
  before generating blurbs from it.
- Reuse the club-run tooling: postprocess.full_scene for 537x900 + rounded corners,
  qa.py, pafix.py. Its lessons are in Docs/Specs/Active/club_art_batches/STATUS.md —
  read the failure-mode sections, they cost real money to learn.

Two decisions must go to Cesar BEFORE the stat table is approved:
- Where ball rarity lives (SPEC §4.2). Balls have no rarity today; it sits on the
  gacha/shop listing. Recommend adding a `rarity` column to Balls.csv — content_rows
  stores the row as jsonb of strings, so a new column needs no migration.
- Whether BallWindCutPerPoint goes up from 0.01 (SPEC §4.1). At 0.01 a maxed +10 wind
  ball buys only 0.10 of a 0.30 cap, so wind is worth a third of its headroom. Three
  balls in the draft table are built around wind.

Out of scope: per-ball 3D models (there are none — one shared Golfin-skinned prefab,
nothing swaps it by ballId), balls for the seven club brands with no ball art, gacha
and shop wiring, retuning Golfin or Putt Ace, rarity framing on the Balls screen UI.

Rules: Cowork never runs git commit — hand Code the file list and message. Review
sheets to Cesar on a neutral checkerboard, never magenta. New player-facing strings go
through the two-way importer (claude/WORKFLOW_NOTES.md), never code-only.

When done: 18 full images committed to the working tree, BALL_IDENTITY.md updated to
the approved table and marked APPROVED with the date, STATUS.md in this folder kept as
the running log, Docs/Specs/Active/ball_data_wiring/ written with SPEC.md + STATUS.md,
the pointer added to Docs/TellCode.md, and the Code kickoff block delivered in chat.
Update Docs/AI_CONTEXT.md.
```
