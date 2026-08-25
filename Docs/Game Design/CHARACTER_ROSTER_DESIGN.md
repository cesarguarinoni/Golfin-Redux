# Character Roster Design — starter flow, stats & bios

**2026-08-21 · Architect + Cesar + Claude Code · applied to the repo (live).**
Data of record: `Assets/Data/Characters.csv` + `Assets/Localization/LocalizationText.csv`
(`CHAR_BIO_*` rows; the localization table auto-updates in Unity). Economy side:
`Docs/Economy/ECONOMY_MASTER.md` §3 + `GOLFIN_Economy_Model.xlsx` `CharacterEconomy` tab.

## 1. Starter selection flow (Code, 2026-08-20)

- After account creation the player picks **ONE** starter — **James or Olivia** — in a Starting
  Character Selection screen (reuses the Roster screen; bottom nav bar swapped for a text block,
  strings `ROSTER_STARTER_*`, localized EN+JA).
- Every other character is **locked** in Roster, **including the unpicked starter**.
- The selection appears only on first run, or again if the player was interrupted before picking.
- Both starters are **Common** (Cesar, 2026-08-21 — resolves the earlier James-Common /
  Olivia-Uncommon asymmetry). James is the power-leaning start, Olivia the control-leaning one.
- Unlocking the rest is an RP sink (proposed ladder 200→6,000 RP by rarity, ≈18,800 RP for the
  full roster — see ECONOMY_MASTER §3). **The unlock purchase flow is NOT built yet** — locked
  cards have no buy action; needs its own spec.

## 2. Stat identities (applied 2026-08-21)

Principles: each rarity keeps its previous **total** base-stat budget (no balance shift vs bots
or level curves); every line stays inside `RarityStatCaps`; stats redistribute so each character
has a readable archetype that the bio mirrors. Stats are Strength / Club Control / Recovery /
Stamina.

| Character | Rarity | S/C/R/St | Σ | Archetype | Was |
|---|---|---|---|---|---|
| James Cartwright | Common | 7/6/5/7 | 25 | big-swinging rookie | (Cesar's values, kept) |
| Olivia Guarinoni | Common | 6/7/6/6 | 25 | range-raised technician | (Cesar's values, kept) |
| Mike Millar | Common | 5/6/8/7 | 26 | ex-caddie endurance grinder | 6/6/7/7 |
| Ean McCormick | Uncommon | 7/7/7/7 | 28 | the flawless all-rounder (flat by design) | unchanged |
| Elizabeth Blackwood | Rare | 8/11/6/9 | 34 | links-bred tactician | 8/10/7/9 |
| Camila Perez | Rare | 11/8/7/8 | 34 | aggressive pin-hunter | 9/9/8/8 |
| Johan Christofferson | Rare | 7/10/7/11 | 35 | bad-weather endurance specialist | 8/10/7/10 |
| Richard Brenson | Mythic | 13/9/9/11 | 42 | 20-season power veteran | 11/10/10/11 |
| Guillermo Abravanel | Mythic | 9/13/8/12 | 42 | shot-shaping showman | 10/11/9/12 |
| Shae O'Connell | Legendary | 11/8/16/10 | 45 | recovery/escape specialist | 12/8/15/10 |
| Roshana Smith | Legendary | 14/9/13/11 | 47 | power + attrition grinder | 13/9/14/11 |
| Freda Faarlund | Supreme | 16/12/17/14 | 59 | world #1, no exploitable weakness | 15/12/18/14 |

Rarity caps (S/C/R/St): Common 25/25/18/22 · Uncommon 28/28/19/25 · Rare 30/30/20/27 ·
Mythic 35/35/25/32 · Legendary 40/40/40/40 · Supreme 50/50/50/50.

## 3. Bios (broadcast register, applied 2026-08-21)

- **Tone:** TV sports broadcast player-profile — third person, surnames, measured commentary.
  Not colloquial. Each bio's claims mirror the character's stat line (Cesar direction).
- **Length calibration (measured against the BioText box):** the panel's proven bounds are the
  old Elizabeth (EN 211 / JA 120) and Shae (EN 192 / JA 113) bios — EN fits at base font, JA
  triggers the shrink-to-fit (`CharacterDetailPanel` min 0.72×). New bios: **EN 177–198 chars,
  JA 61–75 chars** — longer than the old one-liners (~35–50), safely inside the box in both
  languages with the JA shrink never engaging. **Rule for future edits: EN ≤ ~205 chars,
  JA ≤ ~110 chars, or re-verify on device.**
- The English `bio` column in `Characters.csv` (CSV fallback) matches `CHAR_BIO_*` English
  exactly, so the fallback path can never show stale text.
- Dropped from the old Elizabeth/Shae bios: ages and hometowns (46/Cornwall, 23/County Clare) —
  cut for tonal consistency across the roster. Restore in a future pass if wanted.

Full copy lives in `Assets/Localization/LocalizationText.csv` rows `CHAR_BIO_JAMES` …
`CHAR_BIO_ROSHANA`.

## 4. Open items

1. Unlock purchase flow spec (locked Roster card → RP price → `PointsSpendGate` →
   `spend_pts` → unlock; same shape as the club shop path).
2. Unlock ladder numbers are proposals — tune in `GOLFIN_Economy_Model.xlsx` `CharacterEconomy`.
3. JA native review of the 12 bios (drafted by the Architect, same handling as the tournament
   modal copy).
4. `golfin_characters` server mirror (rarity truth for tournament entry checks) already matches
   this roster; if a character's **rarity** ever changes again, re-run the mirror UPSERT from
   `migrations/2026_08_18_tournament_restrictions.sql` §2.
