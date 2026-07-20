# Rules — Golfin Redux Design & Development Constraints

Everything that stays constant across sessions. Read once per new chat if working on UI or design.

---

## Output & Handoffs
- **Any text meant to be copy-pasted goes in a fenced code block — every time, no exceptions.** This includes: new-chat kickoff/handoff/recap blocks, kickoff lines (`Use the implementer subagent on "..."`), commit messages, and any multi-line snippet. A handoff block in prose is a defect — Cesar copies these into a fresh chat and prose forces manual cleanup.
- Acknowledging a formatting rule only in chat does NOT fix it — the acknowledgement does not survive into the next chat. Durable rules live here in Rules.md.
- **Pipeline handoff is ONE line only:** `Use the implementer subagent on "<slug>"` in a fenced block, plus at most a one-line spec path + status note. The SPEC is the source of truth and Code reads it — do NOT reproduce its contents (context/why, stages, gates, traps, repo facts, architect findings) in the kickoff. Fat multi-section kickoff blocks are padding and a repeated miss. Brevity governs: one line, not a wall.

---

## Figma
- **Company file key:** `hXFadl4O6HGKWakiEKgZbW`
- **Personal file key:** `5gEAHjl6xAtW8iYY7NMvWd` (BLOCKED — Starter plan)
- **Rate limit:** Starter plan, use sparingly. Extract specs once, save to this file.
- **Font size ratio:** Figma ÷ 1.4 = Unity TMP size

### Font Sizes (Figma → Unity)
```
66 → 47  (EQUIP/SELECT big buttons)
51 → 36  (screen titles)
48 → 34  (section headers like INFO, BIO)
45 → 32  (names, rarity labels, level)
39 → 28  (button text, RP counter)
33 → 24  (stat names, values, body text)
30 → 21  (tab labels)
20 → 14  (filter bar labels)
```

### Design Tokens (extracted from Figma)
```
TYPOGRAPHY: Rubik font family (Regular, Medium, SemiBold, Bold)

COLORS:
Panel background gradient:    #133453 → #091B33
Panel border:                 rgba(255,255,255,0.9), 3px, radius 20px
Stat bar fill gradient:       #5792E6 → #2775DD → #1A55A4
Stat bar background:          #182430
Stat bar height:              20px Figma / 14px Unity
Active tab/filter text:       Gold gradient top #FCF195 → bottom #BB7F1D
Inactive tab/filter text:     Silver gradient top #FFFFFF → bottom #818EA1
EQUIP/SELECT button:          Gold gradient #FCF195 → #D6AB42 → #BB7F1D, border #FFE48B
Regular buttons:              Silver gradient #FFFFFF → #D1D5DB → #818EA1
Button text:                  #1E293B (dark slate)
Dark blue:                    #001E39
Text blue:                    #2775DD

RARITY TEXT COLORS:
Common:     #7E848A    Uncommon:   #ABC9F5    Rare:       #C0EAC9
Mythic:     #FFF5D3    Legendary:  #ECB5A3    Supreme:    #C6B8DE
Rare stat:  #50C878 (green)

SPACING (Figma → Unity at ÷1.4):
Horizontal padding:           48 → 34
Section gaps:                 24 → 17
Content to tab bar:           12 → 9
Carousel card height:         343 → 245
Detail panel padding:         24 → 17
Stat row gap:                 24 → 17
```

---

## UI Rules
- **Screen titles** go in PersistentUI top bar (username area), not as separate headers
- **Rim/outline images** (not Outline component) for gradient borders
- **Tab/filter active state** = gold gradient text, inactive = silver (no underline indicators)
- **Reuse existing sprites** from other screens — don't create new ones
- **Don't change fonts/paddings/layouts** without user's explicit request — user fine-tunes manually
- **Raycast Target** = false on ALL non-interactive Images (backgrounds, rims, portraits, icons)
- **Image.Type.Filled** for all stat bars, Fill Method Horizontal, Fill Origin Left
- **Canvas + GraphicRaycaster** required together on any child panel with buttons
- **Clone panels** (Object.Instantiate) for compare mode — never build from scratch

## Code Rules
- **CSV-first** — CharacterDatabaseCSV / ClubDatabaseCSV, not ScriptableObjects
- **Resources.Load** for sprites — no Inspector arrays
- **== null** not **??** for Unity objects
- **UnityEngine.InputSystem** always, never UnityEngine.Input
- **Localization:** `LocalizationManager.Get("KEY")` for all new text
- **Events:** subscribe OnEnable, unsubscribe OnDisable
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`
- **Builder scripts clone** styled panels, not build from scratch

## Script Execution Order
```
RuntimeActiveStateManager: -300
CharacterDatabaseCSV:      -200
ClubDatabaseCSV:           -200
CharacterManager:          -100
ClubManager:               -100
```

## Leveling Economy
```
Starting levels by rarity:  Common 10, Uncommon 40, Rare 80, Mythic 120, Legendary 160, Supreme 200
Max levels by rarity:       Common 39, Uncommon 79, Rare 119, Mythic 159, Legendary 199, Supreme 239
Cost per level:             level × 5 RP
SP per level:               1 (always)
Stats per entity:           4 stats, max 20 SP each
Shared CSV:                 LevelUpCosts.csv (240 rows)
```

## Asset Naming Convention
Full reference: `Docs/Game Design/ASSET_NAMING_CONVENTION.md`
- **No spaces** in filenames — PascalCase or hyphens
- **Prefixes:** S_ sprite, ICO_ icon, BG_ background, T_ texture, MESH_ 3D model
- **Characters:** S_Char_{Name}, S_CharFull_{Name}
- **Clubs:** S_Club_{Type}-{Brand}, S_ClubFull_{Type}-{Brand}
- **DO NOT rename Resources/** files without updating CSV values

## Nav Bar Heights (ShellScene)
```
TopBar:       321px (top-anchored)
BottomNavBar: 196px (bottom-anchored)
```
