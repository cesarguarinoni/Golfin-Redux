# SPEC — `stamina_roster_ux`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Stamina/Condition Economy **Phase 4 (UX)**. Tier 3 (visual fidelity + new bind). Phases 1–3 (model, live wiring, tournament wiring) are DONE and merged (`3c22b0fa3`).

## Status

See `STATUS.md`. Set to `SPEC_READY`.

## Goal

Surface the live Condition economy on the roster detail panel. Two visual additions on `CharacterDetailPanel`, plus one cleanup:

1. **Ghost overlay bars on Strength + Club Control** — a translucent *base* fill behind a solid *effective* (degraded) fill. The exposed translucent tail = stat lost to low Condition.
2. **Stamina row becomes a Condition meter** — the fill = current Condition % (not the stamina stat/cap), recolored blue → yellow → red by `StaminaModel.MeterState`. The `9/27` number stays the Stamina **stat**.
3. **Remove** the vestigial unused `LOW_STAMINA_THRESHOLD = 0.25f` const.

The panel computes Condition with the **exact same call** the shot path (`LiveStatProviderHost`) already uses, so the roster display and gameplay can never disagree. The portrait low-stamina icon is already wired and dormant — it lights up for free once Condition can drop; **not part of this task**.

## Reference

- **Figma frame:** `Roster Screen Shae` / id `4065:14999` in file `5gEAHjl6xAtW8iYY7NMvWd`.
- **Parameters group:** `4059:7070`. Rows: Strength `4059:7071`, Club Control `4059:7090`, Recovery `4059:7109`, Stamina `4059:7126`.
  - **Strength bar fills:** ghost (translucent, behind) `4059:7080`; effective (solid, front) `4059:7082`.
  - **Stamina meter fills (drawn in the LOW state):** translucent `4059:7135`; solid `4059:7137`.
  - Bar track (every row): `Bar Container` = fill `#182430`, height 20px, corner radius 20px.
- **Node render:** pull live via `mcp__figma__get_screenshot` on `4065:14999` (and the `4059:7070` Parameters group) at **step 0** — Figma MCP is live and this frame is canonical (Lesson AK; the asset URLs from `get_design_context` expire in 7 days, so don't trust a stale render).
- **Placeholder vs canonical content notes (READ — the mockup is NOT threshold-accurate):**
  1. The mockup **also draws a ghost on the Recovery row** (`4300:54910`). **IGNORE it.** Only `degraded_stats = Strength;ClubControl` degrade. Recovery + Stamina never ghost.
  2. Shae's stamina bar is drawn **orange/red at 9/27 (33%)**, but `meter_mid_pct = 0.30` → 33% renders **yellow**. The mockup color is illustrative; the live color comes from `StaminaModel.MeterState`.
  3. **Yellow/mid has no Figma token** — its gradient is authored in this spec (locked below).
  4. The mockup's Stamina row reuses the two-layer stat-bar component, but the Condition meter is a **single fill** (= Condition %). Ignore the mockup's second translucent stamina layer.

## Figma Fidelity (enumerate EVERY element — Rule 18)

| Element | Figma node | Property → value |
|---|---|---|
| Bar track (all rows) | `4059:7079` etc. | h 20px; radius 20px; fill `#182430` |
| Strength **ghost** (base, BEHIND) | `4059:7080` | blue gradient @ **0.5 alpha** `#5792E6→#2775DD→#1A55A4` (vertical); width = base/cap |
| Strength **effective** (solid, FRONT) | `4059:7082` | same blue gradient full alpha; width = effective/cap; renders ON TOP of ghost |
| Club Control ghost + effective | `4059:7090` group | identical treatment to Strength |
| Recovery bar | `4059:7115` | single solid blue fill, base/cap, **no ghost** (ignore mockup ghost `4300:54910`) |
| Stamina **Condition meter** | `4059:7132` group | single fill = **Condition %**; color by state (below); number = stamina stat |
| — meter HIGH (≥60%) | reuse `Parameter Bar` | blue `#5792E6→#2775DD→#1A55A4` |
| — meter MID (30–60%) | **authored, no token** | amber `#E6B847→#D6961E→#A46E14` |
| — meter LOW (<30%) | `Durability Bar Low` (`4059:7137`) | red `#D16A47→#C04000→#8E2D00` |

## Locked decisions

| # | Decision | Value |
|---|----------|-------|
| **D1** | Degraded-stat **number** | Shows the **EFFECTIVE** (degraded) value, e.g. `11/30`. Ghost tail shows base headroom; number agrees with the solid fill; recovery visibly climbs the number back. (Cesar 👍) |
| **C-yellow** | Mid/yellow meter gradient | `#E6B847 → #D6961E → #A46E14` (vertical top→bottom). Authored — no Figma token. (Cesar 👍) |
| **C-high** | High/blue meter fill | Reuse the `Parameter Bar` blue `#5792E6→#2775DD→#1A55A4`. (Cesar 👍) |
| **C-low** | Low/red meter fill | `#D16A47→#C04000→#8E2D00` (Figma `Durability Bar Low`). |
| **Thresholds** | Meter color breakpoints | From CSV, applied via `StaminaModel.MeterState`: **High ≥0.60, Mid 0.30–0.60, Low <0.30**. Do NOT hardcode — call the model. |

## Architecture context

- **Asmdef boundaries affected:** the assembly owning `CharacterDetailPanel` must reference the **`Golfin.Core.Stamina`** leaf to call `StaminaModel`. The leaf has `references: []`, so this is **cycle-free**; `LiveStatProviderHost` (Assembly-CSharp) already references it, proving the edge. Find the .asmdef that compiles `Assets/Scripts/UI/Roster/UI/` and add the reference. If `CharacterDetailPanel` compiles into Assembly-CSharp (no local asmdef), no asmdef edit is needed.
- **Existing code referenced:**
  - `CharacterDetailPanel` — `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` (the file this task edits; has the inline `UpdateStatBar` it binds with — it does **not** use `StatBar.cs`).
  - `StaminaModel` — `Assets/Scripts/Core/Stamina/StaminaModel.cs`.
  - `PlayerCharacterData` — `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` (pool fields).
  - `LiveStatProviderHost` — `Assets/Scripts/LiveStatProviderHost.cs` (the bind pattern to MIRROR — see `BuildCharacterStats`).
- **Manager / model APIs used (exact signatures):**
  - `StaminaModel.ConditionPct(float condition, int staminaStat) → float` (0..1, clamped; denominator = `MaxCondition(staminaStat)`)
  - `StaminaModel.EffectiveStat(int baseStat, float conditionPct) → int`
  - `StaminaModel.IsDegraded(string statName) → bool` (case-insensitive; matches `"Strength"`, `"ClubControl"`)
  - `StaminaModel.MeterState(float conditionPct) → MeterColorState` (enum `{ High, Mid, Low }`)
  - `StaminaModel.IsConfigured → bool` (guard)
  - `PlayerCharacterData.currentStaminaEnergy` (float, live Condition pool) · `.currentStamina` (Stamina **stat**) · `.currentStrength` · `.currentClubControl` · `.currentRecovery`
  - `RarityStatCaps.GetStatCap(rarity, "Strength")` (existing — unchanged)

## Implementation

### 0. Pull the live node render (`get_screenshot` on `4065:14999` + `4059:7070`) and A/B everything below against it.

### 1. Add the model reference
- `using Golfin.Core.Stamina;` at the top of `CharacterDetailPanel.cs`.
- Add the `Golfin.Core.Stamina` asmdef reference if the Roster UI compiles under a local asmdef (see Architecture context).

### 2. New serialized fields (ghost = base layer, drawn BEHIND the existing bar)
```csharp
[Header("Stat Bars — Ghost (base) overlays")]
[SerializeField] private Image? strengthGhostBar;     // behind strengthBar
[SerializeField] private Image? clubControlGhostBar;  // behind clubControlBar
```
Recovery and Stamina get **no** ghost field. The existing `strengthBar` / `clubControlBar` Images become the **effective (front)** layer.

For the meter colours, expose whatever the prefab needs — **3 gradient `Sprite` fields** (swap by state, matching the existing gradient-on-sprite convention) **OR** 3 `Color` fields if the meter sprite is a neutral/white gradient that tints cleanly. Implementer's call against the actual prefab; inline the hexes from the Locked-decisions table. NOTE the chosen mechanic in the report.

### 3. Compute Condition once per `UpdatePanel`
Right after `playerData` is resolved (and `rarity` is known), compute the panel-wide Condition %, identical to `LiveStatProviderHost`:
```csharp
float conditionPct = StaminaModel.IsConfigured
    ? StaminaModel.ConditionPct(playerData.currentStaminaEnergy, playerData.currentStamina)
    : 1f;   // not configured (boot race) → no penalty, full meter
```
**Denominator is `MaxCondition(staminaStat)`, NOT `maxStaminaEnergy`.** Do not divide by `maxStaminaEnergy`.

### 4. Replace the four `UpdateStatBar(...)` calls
- **Strength** → `UpdateGhostStatBar(strengthName, strengthGhostBar, strengthBar, strengthNumber, "Strength", ROSTER_STRENGTH, playerData.currentStrength, cap, conditionPct)`
- **Club Control** → `UpdateGhostStatBar(... "ClubControl" ...)`
- **Recovery** → keep the existing `UpdateStatBar(recoveryName, recoveryBar, recoveryNumber, ROSTER_RECOVERY, currentRecovery, cap)` **unchanged** (no ghost, no condition).
- **Stamina** → `UpdateConditionMeter(staminaName, staminaBar, staminaNumber, ROSTER_STAMINA, playerData.currentStamina, cap, conditionPct)`

(`cap` from `RarityStatCaps.GetStatCap(rarity, …)` exactly as today.)

### 5. `UpdateGhostStatBar(...)`
```csharp
private void UpdateGhostStatBar(TextMeshProUGUI nameField, Image? ghostBar, Image effectiveBar,
    TextMeshProUGUI numberField, string statKey, string label, int baseValue, int cap, float conditionPct)
{
    if (nameField != null) nameField.text = label;

    int effective = (StaminaModel.IsConfigured && StaminaModel.IsDegraded(statKey))
        ? StaminaModel.EffectiveStat(baseValue, conditionPct)
        : baseValue;

    float baseFill = cap > 0 ? (float)baseValue / cap : 0f;
    float effFill  = cap > 0 ? (float)effective / cap : 0f;

    if (effectiveBar != null) effectiveBar.fillAmount = effFill;   // solid, front
    if (ghostBar != null)
    {
        ghostBar.fillAmount = baseFill;                            // translucent, behind
        ghostBar.enabled = effective < baseValue;                 // hide when not degraded
    }
    if (numberField != null) numberField.text = $"{effective}/{cap}";   // D1: effective
}
```
**Hierarchy:** `ghostBar` must sit at a **lower sibling index** than `effectiveBar` (renders behind). Implementer wires the prefab: duplicate the existing bar Image → make it the ghost (translucent blue gradient / 0.5 alpha) → move behind → assign to the ghost field. The existing bar stays as the effective (front, full-alpha blue).

### 6. `UpdateConditionMeter(...)`
```csharp
private void UpdateConditionMeter(TextMeshProUGUI nameField, Image meterBar,
    TextMeshProUGUI numberField, string label, int staminaStat, int cap, float conditionPct)
{
    if (nameField != null) nameField.text = label;
    if (numberField != null) numberField.text = $"{staminaStat}/{cap}";   // STAT, unchanged

    if (meterBar != null)
    {
        meterBar.fillAmount = StaminaModel.IsConfigured ? conditionPct : 1f;   // Condition %, not stat/cap
        var state = StaminaModel.IsConfigured ? StaminaModel.MeterState(conditionPct) : MeterColorState.High;
        ApplyMeterColor(meterBar, state);   // High=blue / Mid=amber / Low=red — sprite-swap or tint per §2
    }
}
```
`ApplyMeterColor` maps `MeterColorState` → the locked blue/amber/red gradient via the mechanic chosen in §2.

### 7. Cleanup
Delete `private const float LOW_STAMINA_THRESHOLD = 0.25f;`. The real threshold lives in CSV `low_condition_flag_pct` (→ `StaminaModel.IsLowConditionFlag`); the portrait icon uses `PlayerCharacterData.IsStaminaLow` elsewhere, not this panel.

### 8. Refresh behaviour (no new ticking)
`UpdatePanel` already re-runs on carousel-select / level-up / selection-change / points-change / language-change. Condition is read at that moment — fresh as of the last `AccrueRegen` (load / persist), which is **the same value the next shot reads**. Do **not** add a per-frame meter tick or call `CharacterManager.PersistCondition` from the display path (it writes save). Live-ticking the meter while the panel is open is **P5 polish, out of scope**.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Strength & Club Control show a translucent **base** ghost behind a solid **effective** fill when Condition < comfort; the ghost is **hidden** when not degraded (no translucent edge at full Condition).
- [ ] Degraded-stat **number = effective** value (D1), agreeing with the solid fill.
- [ ] **Recovery row unchanged** — single blue fill, base/cap, no ghost (mockup's Recovery ghost was correctly ignored).
- [ ] **Stamina row fill = Condition %** (not stamina-stat/cap); number still reads the stamina stat (e.g. `9/27`).
- [ ] Stamina meter color: blue ≥60%, **amber 30–60%**, red <30% — driven by `StaminaModel.MeterState`, not hardcoded thresholds.
- [ ] Panel's `conditionPct` equals `LiveStatProviderHost`'s for the same character — both call `StaminaModel.ConditionPct(currentStaminaEnergy, currentStamina)` (cite the two call sites).
- [ ] `!StaminaModel.IsConfigured` fallback: raw base stats, no ghost, full blue meter, **no exceptions thrown**.
- [ ] `LOW_STAMINA_THRESHOLD` const removed; no remaining references.
- [ ] All new `[SerializeField]` refs (ghost Images + meter color sprites/colors) wired in the Inspector.
- [ ] No white-box placeholders visible in the screenshot.
- [ ] Unity Console has no errors related to this task.
- [ ] Figma fidelity table PASS against the live `4065:14999` render.
- [ ] Spec deviations (if any) flagged at the bottom of the report.

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — add `StaminaModel` bind, ghost fields, `UpdateGhostStatBar` + `UpdateConditionMeter` + `ApplyMeterColor`, remove vestigial const.
- The Roster-UI `.asmdef` (if one compiles `Assets/Scripts/UI/Roster/UI/`) — add `Golfin.Core.Stamina` reference. *(Confirm cycle-free; `LiveStatProviderHost` already references it.)*
- Roster scene/prefab (`RosterScreen` detail panel) — add 2 ghost Images behind the Strength/Club Control bars (translucent blue, lower sibling index); add the 3 meter color states (sprites or color fields) on the Stamina bar; wire the new serialized refs.

## Smoke evidence

PlayMode is required (visual fidelity — Lesson O). Load the roster, select a character whose live Condition is below comfort (drive a few holes first, or temporarily set `currentStaminaEnergy` low in the Inspector to force degradation), and capture the detail panel showing: ghost tails on Strength/Club Control, effective numbers, and the Stamina meter in its yellow/red state. Then verify a rested character shows no ghost + a full blue meter. Human-in-the-loop content-sanity description in the report. A position/value trace is acceptable as a supplement but the screenshot is the canonical evidence.

## Out of scope (do NOT do these)

- `StatBar.cs` (the separate component used by Compare/carousel) — its ghost/meter treatment is a **follow-up**, not P4.
- Per-frame / live ticking of the meter while the panel is open (P5 polish).
- Re-routing `PlayerCharacterData.IsStaminaLow` through `StaminaModel.IsLowConditionFlag` (optional alignment; the portrait icon already works).
- Any change to drain / regen / persistence (Phases 1–3, DONE).
- Reconciling `currentStaminaEnergy` seeding vs `maxStaminaEnergy` vs `MaxCondition` — that's a Phase-2 wiring concern. **P4 only mirrors the host.**

## Risk NOTE (surface, don't fix)

The meter denominator is `MaxCondition(staminaStat)` (≈ 60 + 27×6 = **222** for Shae), **not** `maxStaminaEnergy` (default 100). Phase 2's load path seeds `currentStaminaEnergy` to full (`AccrueRegen` treats a default `conditionUpdatedUtc` as "stamp now + full pool"; the parse-failure branch sets `currentStaminaEnergy = maxStaminaEnergy`), so a freshly-loaded **rested** character should read ~100% blue. If the meter instead reads low on a known-rested character, that is a **Phase-2 seeding bug to file separately** — not a P4 fidelity failure. Flag it in the report; don't patch it here.
