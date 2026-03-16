# GOLFIN Redux - AI Context

**Last Updated:** 2026-03-16 JST  
**Phase:** 2b IN PROGRESS (Detail Panel Data Binding)  
**Next Session:** Continue Phase 2b implementation with Claude Code

---

## 🎯 Current Status

### ✅ **Phase 2a: COMPLETE (2026-03-06)**
- Roster screen structure created and integrated
- CSV-driven character system (12 characters)
- Character carousel displaying characters
- Navigation working (Characters button → Roster)
- CharacterThumbnailCardGlowUp prefab functional
- All character data loading from CSV

### ✅ **Visual Polish: COMPLETE (2026-03-06)**
- CharacterThumbnailCardGlowUp prefab (170x343, polished design)
- Rubik-SemiBold font throughout
- Rarity background sprites integrated
- Badge text colors optimized for contrast

### ✅ **Detail Panel Hierarchy: BUILT (2026-03-16)**
- Full DetailPanel UI hierarchy manually created in Unity
- Layout matches visual references (Roster_Screen.png)
- Left panel: full-body character portrait
- Right panel: name, rarity/level, 4 stat rows, buttons, bio, compare, select
- Stat icons assigned (IconStrenght, etc.)
- Full-body portraits manually assigned
- **NOT yet data-bound** — shows placeholder values (999/999)

### 🔧 **Compilation Errors: FIXED (2026-03-16)**
Claude Code fixed missing methods on CharacterManager that previous AIs referenced but never implemented:
- `SelectCharacter(string)` — added
- `GetPlayerCharacter(string)` — alias for GetCharacterData()
- `GetCharacter(string)` — alias for GetCharacterTemplate()
- `GetCharacterTemplate(string)` — new, delegates to characterDatabase
- `GetMaxLevel(string)` — new, returns 199 (rarity-based future)
- `LoadRoster()` — needs implementation (currently empty, ownedCharacters never populated)

### 📋 **Next: Phase 2b (Detail Panel Data Binding)**
Spec is written and ready: `Docs/PHASE_2B_DETAIL_PANEL_SPEC.md` + `Docs/PHASE_2B_API_CORRECTIONS.md`

Remaining work:
- [ ] Implement `LoadRoster()` so ownedCharacters gets populated from CSV
- [ ] Add `lastName` and `bio` columns to Characters.csv
- [ ] Update CharacterDatabaseCSV.cs to parse new columns
- [ ] Add `characterLastName` field to CharacterData.cs
- [ ] Add stamina energy fields to PlayerCharacterData.cs (currentStaminaEnergy, maxStaminaEnergy, IsStaminaLow)
- [ ] Full rewrite of CharacterDetailPanel.cs (see spec for complete code)
- [ ] Wire all serialized fields in Unity Inspector
- [ ] Test carousel → detail panel data binding
- [ ] Wire Select button
- [ ] Add status icons (eye=selected, bolt=low stamina) if sprites available
- [ ] Button placeholders (Level Up, Boost, Compare log to console)

---

## 🗂️ Project Structure

### **Key Files:**

```
Assets/
├── Data/
│   ├── Characters.csv ⭐ (12 characters — NEEDS lastName, bio columns)
│   └── LevelUpCosts.csv (199 levels, universal costs)
│
├── Sprites/Characters/ (12 character portraits — thumbnails)
├── Art/Rarities/ (6 rarity background sprites)
│
├── Prefabs/UI/Roster/
│   ├── CharacterThumbnailCard.prefab (original)
│   ├── CharacterThumbnailCardGlowUp.prefab ⭐ (ACTIVE)
│   └── StatBar.prefab (created but not used — stat rows are raw hierarchy)
│
└── Scripts/
    ├── CharacterManager.cs ⭐ (Singleton, roster hub — LoadRoster empty!)
    │
    ├── UI/Roster/
    │   ├── Managers/
    │   │   ├── CharacterDatabaseCSV.cs (CSV loader — needs lastName, bio parsing)
    │   │   ├── CharacterDatabase.cs (ScriptableObject DB + CharacterData + RarityHelper)
    │   │   └── RewardPointsManager.cs
    │   │
    │   ├── UI/
    │   │   ├── RosterScreenController.cs
    │   │   ├── CarouselController.cs (fires OnCharacterSelected)
    │   │   ├── CharacterDetailPanel.cs ⭐ (STUB — needs full rewrite per spec)
    │   │   ├── CharacterThumbnailCard.cs
    │   │   └── StatBar.cs (functional but not attached to hierarchy stat rows)
    │   │
    │   └── Data/
    │       ├── PlayerCharacterData.cs (has pending SP system — needs stamina energy)
    │       ├── RarityStatCaps.cs
    │       ├── CharacterLevelUpData.cs
    │       └── StatAllocationStrategy.cs
    │
    ├── Audio/AudioManager.cs
    ├── UI/ScreenManager.cs
    ├── UI/FadeController.cs
    ├── UI/PersistentUIManager.cs
    └── UI/Modals/ModalController.cs (base class for modals)
```

### **Docs (for AI handoff):**
```
Docs/
├── PHASE_2B_DETAIL_PANEL_SPEC.md ⭐ (implementation spec v2)
├── PHASE_2B_API_CORRECTIONS.md ⭐ (fixes for CharacterManager API gaps)
├── AI_CONTEXT.md (this file)
└── ARCHITECTURE_AUDIT.md (31 MonoBehaviours, dependency graph)
```

---

## 🏗️ Unity Hierarchy (Roster Screen)

```
Canvas > ScreensRoot > RosterScreen
├── CarouselSection
│   ├── LeftArrow / RightArrow
│   ├── ScrollView → Viewport → PaginationDots
│   └── DetailPanel ⭐
│       ├── LeftPanel
│       │   └── Character (Image: full-body portrait)
│       └── RightPanel
│           ├── CharacterNamePanel → CharacterNameText (single TMP, use \n)
│           ├── RarityPanel → RarityRow (3 TMP fields: rarity, current lv, max lv)
│           ├── CharacterStatsPanel
│           │   ├── CharacterStats1 (Strength: StatIcon + Name+Bar/StatsName/Bar + StatNumber)
│           │   ├── CharacterStats2 (Club Control: same structure)
│           │   ├── CharacterStats3 (Recovery: same structure)
│           │   └── CharacterStats4 (Stamina: same structure)
│           ├── ButtonsPanel → LevelUpButton / BoostButton
│           ├── BioPanel → BioHeader / BioText
│           ├── CompareButton → Text (TMP)
│           └── SelectButton → Text (TMP) / Rim
```

**Stat row internal structure (all 4 identical):**
```
CharacterStatsN
├── StatIcon          ← Image (already has sprite assigned, e.g., IconStrenght)
├── Name+Bar
│   ├── StatsName     ← TMP ("STRENGHT", "CLUB CONTROL", etc.)
│   └── Bar           ← Image (use fillAmount for progress)
└── StatNumber        ← TMP ("12/30")
```

---

## 📊 Character Data

### **Characters.csv Structure (current):**
```csv
id,name,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,maxLevel
```

### **Characters.csv Structure (needed for Phase 2b):**
```csv
id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,maxLevel,bio
```

### **12 Characters:**
Elizabeth, Shae, James, Olivia, Camila, Alejandro, Ean, Freda, Johan, Mike, Richard, Roshana
(Note: Alejandro removed from sprites, Guillermo & Rashonda added per 2026-03-06 session)

---

## 🎨 Design Reference Summary

### **Visual References Available:** `Assets/References/Roster Screen/`
- `Roster_Screen.png` — Elizabeth detail panel (SELECT state)
- `Roster_Screen_Shae.png` — Shae detail panel (SELECTED state)
- `Character_Level_Up.png` — Level Up modal (before level up, 0 SP)
- `Character_Level_Up-1.png` — Level Up modal (after level up, 1 SP available)
- `Character_Level_Up-2.png` — Level Up modal (SP allocated to Strength +1)
- `Character_Compare_Empty.png` — Compare mode, right side empty
- `Character_Compare.png` — Compare mode, both characters shown
- `Character_Swap.png` — Swap view (same as compare with SWAP button)

### **Key Design Details (from visual analysis):**
- **Name display:** First name + last name on two lines, single TMP field with \n
- **Rarity row:** 3 separate TMP fields (rarity label colored, current level, /maxLevel smaller)
- **Stat bars:** Blue = normal, Green = maxed, Red = low stamina ENERGY (runtime), Orange = pending SP allocation (Level Up modal only)
- **Status icons (top-right of info panel):** Eye = currently selected character, Lightning bolt = low stamina energy
- **Action button:** Gold "SELECTED" when active, Gold "SELECT" when not
- **BOOST button:** Opens experience booster item selection (future)

### **Stat Bar Color Rules:**
- Blue (#3399FF) — normal stat value
- Green — stat equals its rarity cap (maxed)
- Red — stamina bar ONLY, when currentStaminaEnergy is low (runtime energy, NOT the stat value)
- Orange — Level Up modal ONLY, shows the +N pending SP allocation preview segment

---

## 🎯 Design Decisions & Patterns

### **1. CSV-First Architecture**
Character data in CSV, not ScriptableObjects. CharacterDatabaseCSV loads at runtime.
CharacterManager tries CSV first, falls back to ScriptableObject database.

### **2. Dual Data Model**
- `CharacterData` (ScriptableObject) = base template (rarity, base stats, portraits, bio key)
- `PlayerCharacterData` (plain C#) = player instance (level, SP spent, selection state, stamina energy)

### **3. Event-Driven UI**
- `CarouselController.OnCharacterSelected` → static Action<string>
- `CharacterManager.OnCharacterLeveledUp/OnCharacterSelected/OnRosterChanged`
- UI subscribes in OnEnable, unsubscribes in OnDisable

### **4. Existing Utilities to USE (don't duplicate):**
- `RarityHelper.GetRarityColor(CharacterRarity)` — in CharacterDatabase.cs
- `RarityHelper.GetRarityLabel(CharacterRarity)` — single letter labels
- `RarityHelper.GetRarityBadgeTextColor(CharacterRarity)` — card badge colors
- `RarityStatCaps.GetCap(rarity, statName)` — stat maximums by rarity
- `ModalController` — base class for modal dialogs (use for Level Up modal in Phase 2c)

### **5. Stat Rows are Raw Hierarchy (not StatBar prefab)**
The 4 stat displays in DetailPanel are built as manual hierarchy objects (CharacterStats1-4), NOT using the StatBar.cs component. Access child elements via Transform.Find:
- `statRow.transform.Find("Name+Bar/Bar")` → Image.fillAmount
- `statRow.transform.Find("StatNumber")` → TMP text

### **6. Roster = Main Screen (Not Overlay)**
Managed by ScreenManager like Home/Logo. ScreenId.Roster enum.

---

## 🔑 Critical Issues to Resolve

### **ISSUE: LoadRoster() is Empty**
`CharacterManager.LoadRoster()` never populates `ownedCharacters`. This means `GetCharacterData()` returns null for everything. Must be implemented before detail panel can work.

**Investigate:** How does CarouselController currently get character data? Does it bypass CharacterManager and read from CharacterDatabaseCSV directly?

### **ISSUE: Dual Database Systems**
Both `CharacterDatabase` (ScriptableObject) and `CharacterDatabaseCSV` exist. CSV is preferred per architecture decisions. Need to ensure CharacterManager reads from CSV and creates PlayerCharacterData instances.

---

## 📋 Phase Roadmap

### ✅ Phase 1: Data Architecture
### ✅ Phase 2a: Carousel + Navigation  
### ⏳ Phase 2b: Detail Panel Data Binding (IN PROGRESS)
### ⏳ Phase 2c: Level-Up Modal
### ⏳ Phase 2d: Character Compare + Swap
### ⏳ Phase 3: Gameplay Mechanics (Shot system, physics, courses)

---

## 🤖 AI Workflow

### **Claude (claude.ai) — Architect**
- Analyzes visual references → produces implementation specs
- Reviews code architecture → identifies gaps and patterns
- Writes spec documents (PHASE_2B_*.md) for Claude Code to implement
- Cannot access repo directly — needs file uploads or copy/paste

### **Claude Code — Implementer**
- Has full filesystem access to Unity project
- Reads spec documents from Docs/ folder
- Implements, compiles, tests
- Updates AI_CONTEXT.md after completing work

### **Handoff Process:**
1. Claude (architect) produces spec → downloads as .md file
2. User drops .md into project Docs/ folder
3. User tells Claude Code: "Read Docs/PHASE_2B_*.md and implement"
4. Claude Code implements, resolves any ambiguities by checking actual code
5. After session, update AI_CONTEXT.md

---

**Last Modified:** 2026-03-16 by Claude (Architect)  
**Next Update:** After Phase 2b completion
