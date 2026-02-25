# GOLFIN Redux — Screen Roadmap & Figma Values Needed

> Last updated: 2026-02-25

## Reference Image Inventory (39 screens found in Drive)

### Priority 1: Character Selection
| File | Screen |
|------|--------|
| ref_18P26WSXi9 | **Character Select** (main picker) |
| ref_1J6YiXh5er | Character Profile |
| ref_1Gsdw1FQfu | Character Level-Up |
| ref_17conQWFMl | Character Level Up (confirm) |
| ref_1e6KRol44y | Player Level Up |
| ref_1ns4geWjl1 | Player Roster |
| ref_1KQdPqi63o | Roster Compare |
| ref_165V6nlZBf | Character Compare |

### Priority 2: Main Screen (Home)
| File | Screen |
|------|--------|
| ref_19D_2wotGi | **Home Screen** (main hub) |

### Priority 3: Club Inventory
| File | Screen |
|------|--------|
| ref_18wsvBPlIA | **Bag Loadout** (club slots) |
| ref_1k24FLXzd6 | Bag Loadout (alt) |
| ref_13WGVRVGX9 | Club Detail |
| ref_1KEvMfNH_o | Club Detail (Equipped) |
| ref_1Yceq9AbMA | Club Detail (Unequipped) |
| ref_1d_6AtPDjY | Wedge Detail (Equipped) |
| ref_11QiS2s3 | Club Swap |
| ref_1ErID1ScXO | Club Swap (alt) |
| ref_11TWidhjXz | Club Add |
| ref_1amhqrwoxP | Club Compare |
| ref_1kTzh7lZ6I | Club Compare (Left) |
| ref_1nkNFSzWZX | Club Compare (Right) |
| ref_1j-SraSXPO | Club Level Up |
| ref_1jWQz0xb35 | Club Level Up (SP Available) |
| ref_1pqHWVqHKu | Club Level Up (No SP) |
| ref_1RRkB1fqKz | Club Repair Select |
| ref_1AqPyh1lM_ | Repair Kit |
| ref_1pTWAg3dnh | Choose a Bag |

### Priority 4: Settings
| File | Screen |
|------|--------|
| ref_15SksyU_6p | **Settings** (main) |
| ref_1AzQQ9-Q7x | Settings (alt) |
| ref_1uVR4DPZYO | Settings (alt) |
| ref_1-0lU90U | Settings - Language |
| ref_13JB3FppXj | Settings - Contact Form |
| ref_1VQYFQMqE0 | Settings - Privacy Policy |
| ref_1jWQz0xb35 | Settings - FAQ |
| ref_1GgC4Ew8W8 | Delete Account |

### Other Screens (found in references)
| File | Screen |
|------|--------|
| ref_1-InpiMU | Leaderboard |
| ref_1EkQ_7lvoD | Leaderboards |
| ref_18oC_IO0Cd | Ball Stats |
| ref_1sB-jzbfDD | Loading Screen |

---

## Figma Values Needed Per Screen

### Character Selection Screen
```
SCREEN: Character Selection (1170×2532)

ELEMENT: Header bar
  y: 
  height: 
  bgColor: 
  title fontSize/weight/color:
  back button size/position:

ELEMENT: Character model area
  y: 
  height: 
  (3D model viewport or static image?)

ELEMENT: Character name
  y: 
  fontSize: 
  fontWeight: 
  color: 

ELEMENT: Character stats area
  y: 
  width: 
  height: 
  bgColor: 
  stat label fontSize/color:
  stat value fontSize/color:
  stat bar width/height/colors:

ELEMENT: Character selection carousel/tabs
  y: 
  tab width/height:
  active tab color:
  inactive tab color:
  
ELEMENT: Select/Confirm button
  y: 
  width: 
  height: 
  bgColor: 
  text fontSize/color:
  cornerRadius: 
```

### Main Screen (Home)
```
SCREEN: Main Screen (1170×2532)

ELEMENT: Top bar (player info)
  y: 
  height: 
  avatar size/position:
  player name fontSize/color:
  level badge size/position:
  currency display position/fontSize:

ELEMENT: Center area (3D course preview or character)
  y: 
  height: 

ELEMENT: Play/Match button
  y: 
  width/height:
  bgColor:
  text fontSize/color:
  cornerRadius:

ELEMENT: Bottom navigation bar
  y: 
  height: 
  bgColor: 
  icon size:
  label fontSize:
  active color:
  inactive color:
  tab names: [Home, Bag, Shop, Leaderboard, Settings?]
```

### Club Inventory (Bag Loadout)
```
SCREEN: Club Inventory (1170×2532)

ELEMENT: Header bar
  (same template as Character Selection)

ELEMENT: Bag slots grid
  y: 
  columns: 
  slot width/height:
  slot bgColor:
  slot borderColor:
  slot cornerRadius:
  equipped indicator:

ELEMENT: Club card (inside slot)
  club name fontSize/color:
  club icon size:
  rarity indicator (color/border):
  level badge size/position:

ELEMENT: Club detail panel (when selected)
  y: 
  width/height:
  stats layout:
  action buttons (Equip/Level Up/Compare):
```

### Settings Screen
```
SCREEN: Settings (1170×2532)

ELEMENT: Header bar
  (same template)

ELEMENT: Settings list
  row height: 
  row bgColor:
  row separatorColor:
  label fontSize/color:
  icon size:
  toggle switch size/colors:

ELEMENT: Section headers
  fontSize:
  color:
  bgColor:

ELEMENT: Categories (from reference):
  - Language
  - Sound/Music
  - Notifications
  - Contact/Support
  - Privacy Policy
  - FAQ
  - Delete Account
  - Sign Out
```

---

## Images Needed Per Screen

### Character Selection
1. Character model renders (4 characters) — 3D renders or 2D art
2. Character portrait thumbnails (for carousel)
3. Stat bar fill sprite (can reuse pill_bar.png)
4. Header background (if different from card_bg)
5. Back button icon

### Main Screen (Home)
1. Background art (course panorama)
2. Player avatar frame
3. Currency icons (coins, gems, etc.)
4. Bottom nav icons (5-6 icons)
5. Play button sprite (or use code-generated)

### Club Inventory
1. Club type icons (Driver, Wood, Iron, Wedge, Putter)
2. Rarity border sprites (Common, Uncommon, Rare, Legendary)
3. Club card background sprite
4. Empty slot sprite
5. Equipped badge/checkmark
6. Stats comparison arrows (up/down)

### Settings
1. Settings row icons (language, sound, notifications, etc.)
2. Toggle switch sprites (on/off)
3. Section header background (if any)
4. Back button icon (shared)
