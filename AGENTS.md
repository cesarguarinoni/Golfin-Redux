# GOLFIN Agent Pipeline

Every feature request runs through 3 agents in sequence. No exceptions.

---

## 🎨 Agent 1: UX/UI

**Priorities (in order):**
1. **Fidelity to Figma references** — extract exact values via Figma API, compare against reference screenshots
2. **Responsiveness** — all interactions must feel instant; loading states, transitions, feedback on every tap
3. **Best practices** — iOS HIG / Material Design guidelines, accessibility (44pt min touch targets), color contrast

**Outputs:**
- Element specs (positions, sizes, colors, fonts) from Figma
- Missing screen/component audit
- Accessibility flags
- Animation/transition recommendations

---

## ⚙️ Agent 2: Backend

**Priorities (in order):**
1. **Sheet-editable data** — every value that could change (text, rates, prices, timers, thresholds) goes into CSV/Google Sheets, never hardcoded
2. **Localization files** — all player-facing text goes through `localization.csv`, new keys added automatically
3. **Data schema updates** — ScriptableObjects, JSON configs, or Sheets for all game data

**Outputs:**
- New/updated CSV columns and keys
- Localization entries (all languages if applicable)
- ScriptableObject or data class changes
- Sheet structure recommendations

**Rules:**
- No magic numbers in code — everything is a config value
- Every new feature = new localization keys added to CSV
- Data flows: Google Sheet → CSV export → Unity loads at runtime

---

## 🔧 Agent 3: Unity Implementation

**Priorities (in order):**
1. **Automation** — update `CreateUIScreen.cs` and editor tools so screens can be regenerated
2. **Preserve manual adjustments** — read `screen_values.json` first; never overwrite Cesar's tweaks
3. **Debug options** — every new feature gets a debug toggle or test button in a debug panel

**Outputs:**
- Updated `CreateUIScreen.cs` with new/changed elements
- New MonoBehaviour scripts as needed
- Debug menu entries for testing
- Updated `screen_values.json` schema

**Rules:**
- Always run `QA/lint_cs.py` before pushing
- Use `FindOrCreate` pattern (safe to re-run)
- Font references via `TrySetFont` (never hardcode font assets)
- All positions use Figma coordinates (1170×2532 reference)
- Check `screen_values.json` for overrides before applying defaults

---

## Pipeline Order

```
Request → UX/UI Analysis → Backend Data → Unity Code → Lint → Push
```

Each agent's output feeds the next. Final push includes all three layers.

---

## Adding Gameplay (Future)

When gameplay features start, add:
- 🎮 **Gameplay Agent** — physics, input, scoring, game state
- 📊 **Analytics Agent** — event tracking, funnel analysis, A/B test hooks

Update this file when that happens.
