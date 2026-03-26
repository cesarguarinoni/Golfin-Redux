# GOLFIN Redux — Game Design Changelog

Tracks all changes from the original GDD to the Redux implementation.

---

## 2026-03-21 — Gameplay Formulas Simplification (PROPOSAL)

### What Changed
**Proposed replacement of all old gameplay formulas with linear, predictable alternatives.**

Full proposal: `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md`

### Key Simplifications
- **No more square roots, pow, or log** — all formulas are add/multiply only
- **No more hidden randomizer** on every shot (old: random 1-15 yards added)
- **Shot controls reduced** from 5 inputs to 3 (aim, optional curve, swing)
- **Spin system removed** from stat allocation — loft is fixed per club type
- **Each stat maps to exactly one effect** — no convoluted interactions
- **Only randomness** comes from player mistakes (bad timing, overpower) — bounded, not invisible

### Stat Mapping (New)

**Clubs:**
| Stat | Effect | Formula Type |
|---|---|---|
| Power | Distance (yards) | Linear: base + SP × yards_per_point |
| Accuracy | Max fade/draw angle | Linear: 10° + SP × 8.5° |
| Terrain Resistance | Reduces terrain penalty | Linear: penalty × (1 - SP × 2.5%) |
| Durability | Degrades all other stats when low | Linear: 0.5 + 0.5 × (current/max) |
| Loft | Launch angle | Fixed by club type (not a stat) |

**Characters:**
| Stat | Effect | Formula Type |
|---|---|---|
| Strength | Overpower error reduction | Linear: error × (1 - SP × 3.75%) |
| Club Control | Arrow/timing speed | Linear: speed × (1 - SP × 2.5%) |
| Stamina Regen | Recovery rate per hour | Linear: 50% + SP × 2.5% |
| Stamina | Character durability | Same as club durability formula |

### Status
PROPOSAL — needs review and playtesting before implementation. Open questions remain about wind, spin beyond fade/draw, ball types, and elevation.

---

## 2026-03-21 — Leveling Economy Overhaul

### What Changed
**Replaced the old flat-tier leveling system with a rarity-based linear curve.**

### Old System
| Aspect | Value |
|---|---|
| Max Level | 100 (all rarities) |
| Starting Level | 1 (all rarities) |
| SP per level | 1-6 (increases at tier boundaries) |
| RP cost curve | Starts at 300, jumps at tiers (300→3600→4800→7200→10200→13800) |
| Total RP to max | ~619,200 RP |
| Time to max | ~590 hours |

### New System
| Aspect | Value |
|---|---|
| Max Level | Rarity-dependent (Common 39 → Supreme 239) |
| Starting Level | Rarity-dependent (Common 10 → Supreme 200) |
| SP per level | Always 1 (flat) |
| Stats per entity | 4 stats, max 20 SP each (80 total cap) |
| RP cost formula | Level × 5 (linear) |
| Total RP to max (Common) | ~3,675 RP (~1.2 hours) |
| Total RP to max (Supreme) | ~43,900 RP (~14.6 hours) |

### Applies To
- Characters (Strength, Club Control, Recovery, Stamina)
- Clubs (Power, Accuracy, Lie Resistance, Durability)
- Putters (Control, Accuracy, Weight, Durability)

### Files Updated
| File | Change |
|---|---|
| `Assets/Data/LevelUpCosts.csv` | 200 → 240 rows, cost = level × 5, SP = 1 |
| `Assets/Data/Characters.csv` | maxLevel per rarity |
| `Assets/Data/Clubs.csv` | maxLevel per rarity |
| Code: CharacterManager, ClubManager | GetStartingLevel(rarity) — pending in TellCode.md |

### Open Questions
1. Max level 239 feels high — consider UI or compression
2. Commons max in ~5 sessions — need post-max progression
3. SP allocation vs starting stats power gap

---

## 2026-03-17 — NFT System Removal

### What Changed
**Stripped all NFT/blockchain mechanics from the original GDD.**

### What Was Removed
- NFT marketplace, token economy, blockchain wallet, minting, play-to-earn, royalties

### What Replaced It
- Reward Points (RP) earned from gameplay, shared with Golfin GPS partner app
- No real-money currency exchange, no blockchain dependency

---

## 2026-03-26 — Club Repair Simplified (No Modal)

### What Changed
**Replaced the Confluence-designed Repair Kit Selection screen + modal flow with instant auto-repair.**

### Old Design (Confluence GDD)
- Player taps REPAIR → opens a Repair Kit Selection screen
- Player browses available kits, selects one, taps USE
- Separate screen with sorting, filtering, kit info display
- Explicit kit choice required every time

### New Design (Redux)
- Player taps REPAIR → system **automatically picks the best kit** and uses it instantly
- No modal, no selection screen, no extra taps
- Auto-selection logic:
  - ≤50% durability missing → prefer **Standard Kit** (50% restore)
  - >50% durability missing → prefer **Premium Kit** (100% restore)
  - Falls back to whichever kit type is available
- Button grayed out when: club at full durability OR no kits owned
- Toast notification shows result (when toast system is built)

### Why
- Reduces friction — repair is maintenance, not a fun decision
- Players don't need to think about which kit to use; the system optimizes for them
- Original design had a full selection screen because it was part of a larger Items inventory system; since Items screen isn't built yet, this is simpler and may stay permanent

### Kit Types Unchanged
| Kit | Effect | How to Obtain |
|---|---|---|
| Standard Repair Kit 🛠️ | Restores 50% of maxDurability | Mission rewards |
| Premium Repair Kit ⭐ | Restores 100% of maxDurability | Mission rewards |

### Files
| File | Change |
|---|---|
| `Assets/Scripts/RepairKitManager.cs` | New singleton — manages kit inventory + auto-selection |
| `Assets/Scripts/ClubManager.cs` | Added `OnClubRepaired` event + `RepairClub()` method |
| `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` | Repair button calls auto-use directly |
| `Assets/Scripts/UI/Inventory/ClubCompareController.cs` | Same auto-use wiring |

---

## Design Reference Files

| File | Description |
|---|---|
| `Docs/Game Design/Old Levels.xlsx` | Original GDD leveling tables (archived) |
| `Docs/Game Design/New Levels.xlsx` | Current leveling economy (3 sheets: Clubs, Putter, Characters) |
| `Docs/Game Design/Old Gameplay Formulas.xlsx` | Original complex formulas (archived) |
| `Docs/Game Design/Old Control.docx` | Original control scheme issues and proposals |
| `Docs/Game Design/GAMEPLAY_FORMULAS_PROPOSAL.md` | New simplified formulas (PROPOSAL) |
| `Docs/Game Design/Golfin - Confluence.pdf` | Original full GDD |
| `Docs/Game Design/Golfin - Confluence.txt` | GDD text extract |
| `Docs/GAME_DESIGN_AGENT.md` | AI agent for evaluating GDD systems (use "game design mode") |
