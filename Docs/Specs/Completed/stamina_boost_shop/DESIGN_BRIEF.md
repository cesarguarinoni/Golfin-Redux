# DESIGN BRIEF — stamina_boost_shop (Order 517)

**Status:** Queued · **design pass = the next action** (NOT yet specced). Shop pillar, first shop.
**For:** a fresh Architect chat. Read this, resolve §2 decisions WITH Cesar, confirm Figma (§3),
THEN write `SPEC.md` in this folder and move to `Active/` on kickoff.

> **Do not spec until §2 decisions are answered by Cesar and the Figma frame is confirmed.**
> Rule: Figma is UI source-of-truth; don't guess what's sold or how it looks.

---

## §1 Verified grounding (2026-07-02 — reuse these, don't reinvent)

- **No Shop code exists yet** (`find *shop*` = empty). This is greenfield — the first Shop-pillar
  feature. Presentation pattern TBD (§2 D4): a `ScreenManager` screen vs a `ModalController` overlay.
- **Spend currency API (ready):** `RewardPointsManager.SpendPoints(int amount) → bool`
  (`Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs:82`) — returns false on insufficient
  funds; already used by `ClubLevelUpModalController` (RP cost), `ModeCardController` (entry fee),
  `CharacterManager`, tournaments. Balance ≠ earned (SpendPoints must NOT touch lifetime-earned).
- **Reward grant (ready, DRY):** shared `RewardGranter` exists (`Assets/Scripts/UI/RewardGranter.cs`,
  extracted during Order 347) — Points/RepairKit/Ball. Relevant only if the shop also *grants* items.
- **Stamina pool:** `PlayerCharacterData.currentStaminaEnergy` (float, default 100, clamp to
  `maxStaminaEnergy`). Condition % = `StaminaModel.ConditionPct(currentStaminaEnergy, currentStamina)`
  (mirrored by `LiveStatProviderHost:125`). Two pools exist from the Stamina Economy (P1–P5, all
  shipped): the **live Condition pool** and the **tournament pool** (does NOT regen between holes).
- **⚠ GAP — no top-up API:** nothing writes a *refill* to `currentStaminaEnergy` (only regen +
  editor/demo setters). A boost purchase needs a new small API (e.g. `StaminaRuntimeService.AddEnergy`
  / `PlayerCharacterData.RefillStamina`, clamp to max, persist). This is the main new-code piece.

---

## §2 Open design decisions — Cesar must answer before speccing

- **D1 — What's sold?** Instant Condition/stamina **refill** (partial vs full)? A **timed boost**
  (e.g. no-drain for N holes)? A **max-pool increase**? One SKU or several? (Simplest v1: instant
  refill SKUs.)
- **D2 — Which pool?** Live Condition pool / tournament pool / both? (Anti-cheat note: the tournament
  pool is deliberately non-regenerating — a paid top-up there has balance implications; confirm.)
- **D3 — Currency?** RP via `SpendPoints` (default, obvious)? Also items? Real money? (Assume RP-only
  v1 unless Cesar says otherwise.)
- **D4 — Surfacing / entry point?** Dedicated **Shop screen** (greenfield, sets the Shop-pillar
  pattern) vs a **modal** opened from the roster Condition meter / low-stamina prompt? Nav entry?
  **⚠ Cross-ref:** the roadmap already has **Order 610 "Shop UI" (Phase 06. Shop, P3)** for
  clubs/balls/cosmetics — decide whether 517 is a standalone stamina-only modal or the **first module
  of that general shop** (avoid building a throwaway one-off if 610's screen is coming).
- **D5 — Apply mechanism (impl):** add the new clamp-to-max top-up API (§1 gap); per-character
  (which character's pool?) vs account-wide.
- **D6 — Pricing model:** flat RP per SKU (CSV-driven, per the CSV-first convention) — mirror how
  `ModeCardController.entryFee` / club level-up RP costs are sourced.

*(Proposed sane v1 default if Cesar wants minimal scope: RP-buys-instant-Condition-refill for the
selected character's LIVE pool, one or two SKUs, CSV-priced, surfaced as a modal off the roster
Condition meter. Confirm — do not assume.)*

---

## §3 Figma (confirm before any UI build)

No Figma node captured yet. Ask Cesar: which page / frame is the stamina-shop design, and what's
placeholder vs canonical? Pull `get_design_context` on the component node at step 0 (Lesson AK).
File key `5gEAHjl6xAtW8iYY7NMvWd`.

---

## §4 Next-chat checklist
1. Read this brief.
2. Get Cesar's D1–D6 answers + the Figma frame.
3. Classify tier (likely Tier 3 if it's a new visual shop screen; Tier 2 if a simple modal).
4. Write `SPEC.md` here → move folder to `Active/` → kickoff line.
