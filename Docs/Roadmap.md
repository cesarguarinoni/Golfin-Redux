# GOLFIN Redux — Roadmap

**Last updated:** 2026-04-30 19:45 JST

## Sequence

```
Putter P1 → Loop v1 (incl. Putter P2) → Loop v2 → Save System
                                                       ↓
                   Rankings → Matchmaking → Perf Baseline
                                                       ↓
              Shop → Gacha → Optimization → Polish → Server
```

---

## 1. Putter Mechanics — Phase 1
- 1a. Lab UI: putt mode toggle, green-only camera, distance-only power
- 1b. Putt physics validation (known issue: green sits ~11cm above heightmap Y)
- 1c. Aim-line on green (slope arrows v1)
- 1d. *Defer to Loop v1: full break visualization*

## 2. Gameplay Loop v1 (single hole, lab-launched)
- 2a. Ball state machine: Aiming → Flying → Rolling → AtRest → InCup | OB
- 2b. Camera transitions: tee → flight → rest → green → cup
- 2c. Turn counter + shot history (in-memory)
- 2d. Hole-complete detection + result screen (strokes, par, score)
- 2e. "Next shot" handoff (ball at rest → re-arm controls)
- 2f. Putter Phase 2: in-context tuning

## 3. Gameplay Loop v2 (menu-to-menu)
- 3a. Menu wiring: Character → Clubs → Hole → Play
- 3b. Hole picker UI (mini-map, par, distance)
- 3c. Result screen polish (score breakdown, optional shot replay link)
- 3d. Next Hole / Back to Menu transitions
- 3e. Save state: persist character/clubs/score across sessions

## 4. Rankings
- 4a. Local leaderboard (per hole + total)
- 4b. Score persistence + history
- 4c. Ranking calc (handicap, raw score, or both — TBD)
- 4d. Leaderboard UI

## 5. Matchmaking (faked)
- 5a. Bot opponent pool (seeded scores, varied skill)
- 5b. Matchmaking surface (find opponent → score appears alongside)
- 5c. Async result UI

## 6. Shop (offline)
- 6a. Currency display + sources (rewards from Loop v1/v2)
- 6b. Shop UI (clubs, balls, cosmetics)
- 6c. Purchase flow
- 6d. Inventory integration

## 7. Gacha (offline)
- 7a. Pull mechanics + rates
- 7b. Pull animation/reveal
- 7c. Pity system
- 7d. Pull history

## 8. Perf Baseline
- 8a. FPS capture on target devices, identify hotspots
- 8b. Memory profile (texture, mesh, audio budgets)

## 9. Optimization
- 9a. Quality settings (Low/Med/High presets)
- 9b. Texture compression audit
- 9c. Tree LOD / culling validation
- 9d. Mobile device testing

## 10. UI/UX Polish
- 10a. Animation pass (transitions, button feedback)
- 10b. Audio pass (SFX, music, mix)
- 10c. Localization completion (JP/EN gap fill)
- 10d. Tutorial / onboarding flow

## 11. Server Infrastructure
- 11a. Auth
- 11b. Real rankings (replace local)
- 11c. Real matchmaking (replace bots)
- 11d. Shop/Gacha server-side validation
- 11e. Analytics + Crashlytics
- 11f. Save sync

---

## Architectural foundations (bake in early)

1. **Interface-first for online-deferred systems.** `IRankingService`, `IMatchmakingService`, `IShopService`, `IGachaService` — local impls now, server impls later, same call sites.
2. **Reactive save layer.** Single `SaveData` with `OnChanged` event. All systems write through it. Cloud sync swaps the persister.
3. **Replay determinism.** Physics is bit-exact. Record `(seed, club, ball, charYaw, power, finetune)` per shot — ~100 bytes per hole. Enables replays, share-a-shot, anti-cheat.
4. **Event bus for rewards/currency.** "Hole complete" → multiple listeners (currency, ranking, achievements, gacha tickets). Avoid hardcoded chains.
5. **Headless mode for bots.** Ball state machine runs with no visuals so faked-matchmaking bots simulate scores via real sim.

---

## Closed out
- Holes 2–18 import (all 18 already in `Assets/Resources/HoleData/`)
- Power Gauge (done in 8.2 or 8.3)
- Phase 8.5 — action buttons + selectors + central ball + targeting line


---

## Tooling & workflow (candidates, not scheduled)

Patterns worth borrowing from OpenAI Symphony (read 2026-04-30). Full Symphony adoption not pursued — the win is decoupling implementation from supervision, but Cesar is the only reviewer, so higher throughput just moves the bottleneck. These three patterns are the parts that pay even at solo-dev scale. Stub: `Docs/Specs/Queued/SYMPHONY_PATTERNS.md`.

- **STATUS dashboard command.** A single script (PowerShell or Python) that scans `Docs/Specs/Active/*/STATUS.md` and reports: per-task state, idle-since duration, blocked vs ready-for-review counts, missing artifacts (e.g. IMPLEMENTER_REPORT exists but SELF_REVIEW doesn't). Run on demand, not autonomous. Cheap. Removes "where am I?" cognitive load when context-switching between days or machines.
- **Proof-of-work bar in `IMPLEMENTER_REPORT.md`.** Tighten the implementer agent definition so every report MUST include: (a) screenshot diff vs reference (when visual), (b) test output snippet, (c) `git diff --stat` summary. Procedural rejection if missing. Mirrors Symphony's "agents provide proof of work" pattern. Reduces self-review and architect-review churn.
- **`depends_on:` field in spec front-matter.** Formalize task dependencies (Architect already does this implicitly — 8.5 a→b→c→d was dependency-aware). Adding `depends_on: [task-slug, task-slug]` to spec front-matter makes the dependency machine-readable and lets the dashboard surface "X is ready, but waiting on Y."
