# SPEC — `tournament_restrictions` (client half)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

Tournaments now carry a category and entry restrictions, authored in the admin dashboard and
served by `GET /api/v1/tournaments/golfin` (LIVE in prod since 2026-08-18). The client must:

1. Carry the 10 new fields through DTO → `TournamentDefinition` → mapper (CSV fallback = today's
   behaviour).
2. Render the signup modal's RULES block from **data** instead of the 5 hardcoded localization
   strings in `ApplyRules()`.
3. **Gate CONFIRM client-side**: an ineligible signup (character rarity/level, gear rule, club
   rarity) never reaches payment. The server independently enforces max_players + character
   bands at `POST /golfin/{slug}/enter` — the client gate is UX + the offline-path enforcement,
   the server is the authority when entries are remote.

## Server contract (deployed — read, do not change)

`list_golfin` now emits, per tournament (all nullable, null = unrestricted):

```json
"category": "sponsor" | "competitive",          // backfilled: sponsor
"max_players": 100,                              // human cap, bots excluded
"players_per_division": 100,
"division_type": "open" | "level" | "rarity_band",  // backfilled: level
"char_rarity_min": "Common"…"Supreme",
"char_rarity_max": "Common"…"Supreme",
"char_level_min": 1, "char_level_max": 999,
"gear_rule": "own" | "supplied",                 // backfilled: own
"club_rarity_max": "Common"…"Supreme"
```

`POST /golfin/{slug}/enter` (async-board endpoint) denies BEFORE the fee debit with 200-shaped
payloads, same contract family as `insufficient`:

```json
{"entered": false, "status": "full", "max_players": 100}
{"entered": false, "status": "ineligible", "reason": "char_rarity" | "char_level"}
```

Rarity order: Common < Uncommon < Rare < Mythic < Legendary < Supreme (matches
`CharacterRarity` / `RarityHelper`).

## Scope

### 1. Data plumbing

- `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs`: add the 10 `JsonProperty`
  fields beside `league_key`.
- `Assets/Scripts/Tournaments/TournamentDefinition.cs`: **appended optional** properties
  (same pattern as `Title` / `BannerUrl` — additive, never reorder existing ctor args).
  Category/DivisionType/GearRule as string enums or parsed enums — implementer's call, but
  unknown values from a NEWER server must degrade to "unrestricted/sponsor", never throw.
- `Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs`: pass-through.
- `Assets/Resources/Data/tournaments.csv`: NO new columns required. The shipped-CSV fallback
  path composes definitions with all restriction fields null + category `sponsor`,
  division `level`, gear `own` — i.e. exactly today's behaviour offline.

### 2. RULES block (TournamentSignupModalController.ApplyRules, ~line 542)

Replace the 5-string hardcoded join with per-tournament lines:

| Line | Data | Null fallback (current strings) |
|---|---|---|
| MAX PLAYERS | `MaxPlayers` | `tourn.rules.max_players` ("Unlimited") |
| DIVISIONS | `DivisionType` | `tourn.rules.divisions` ("Level based") |
| PER DIVISION | `PlayersPerDivision` | `tourn.rules.per_division` ("100") |
| GEAR | `GearRule` (+ `ClubRarityMax` when set) | `tourn.rules.gear` — ⚠️ current string says "Supplied by GOLFIN" which is display fiction; backfilled data says `own`, so the rendered default CHANGES to "Own clubs". Intended. |
| CHARACTERS | rarity band and/or level band | `tourn.rules.characters` ("Unrestricted") |

New localization rows (EN + JA, `Assets/Resources/Localization/LocalizationText.csv`) with
`{0}`-style format args for the value forms, e.g. `tourn.rules.max_players_n`,
`tourn.rules.divisions_open|level|rarity`, `tourn.rules.gear_own|supplied`,
`tourn.rules.chars_rarity_band`, `tourn.rules.chars_level_band`. Keep the existing 5 keys as
the null fallbacks. JA copy: Architect-drafted, flag for native review like
`tournament_signup_modal` did.

Optional, only if trivial: a small category tag ("SPONSOR" / "COMPETITIVE") near the title —
if it needs prefab surgery, SKIP and note it; this spec must not become a layout task.

### 3. Eligibility gate (OnConfirm, ~line 195)

Before the payment path runs:

- **Character rarity/level**: the character the entry will use (the same `charId` passed to
  `CompleteSignup`) checked against `CharRarityMin/Max` + `CharLevelMin/Max` via the
  character's template rarity and `playerData.currentLevel` (`CharacterManager`).
- **Gear**: when `ClubRarityMax` set and `GearRule == own`, check
  `BagManager.Instance.GetClubsInBag(EquippedBagSlot)` — any club whose template rarity
  exceeds the cap blocks entry. `GearRule == supplied` skips the club check entirely
  (standard-set enforcement in play is OUT of scope — a later standard-spec task).
- Ineligible → CONFIRM denied with a toast naming the failed rule (reuse the modal's existing
  refusal toast pattern from the spend path; localized, EN+JA). No debit, no navigation, modal
  stays open.
- When the backend path is remote (`tournament_async_board`'s `RemoteTournamentBackend`), map
  the server's `full` / `ineligible` denials to the same toasts. If this task lands BEFORE the
  async-board client, put the mapping where that spec's Register path will find it and note the
  handoff in the report.

### 4. Out of scope

- Standard-spec stat normalization (Ken doc §03/§07) — later phase, not implied by `category`.
- Admin dashboard (Architect builds separately). Server changes (deployed).
- Max-players display of remaining slots; division assignment logic; bracket UI.

## Acceptance

1. EditMode: mapper carries all 10 fields; null server fields → unrestricted definition;
   unknown enum strings degrade, never throw; CSV-composed definitions behave exactly as today.
2. EditMode: eligibility matrix — rarity below min / above max / in band; level below/above/in;
   club cap violated by one equipped club; `supplied` skips club check; all-null = always
   eligible.
3. Modal render: a tournament with authored restrictions shows the real values (EN and JA);
   a tournament with none renders the exact same 5 strings as today.
4. Ineligible CONFIRM → toast, zero RP delta, no entry, no navigation (widget-click test like
   `tournament_signup_modal`'s flag-ON denial test).
5. Full EditMode suite green, swept per assembly (filtered runs mask failures).

## Verification data

Admin has set `kasumigaseki_open` restrictions in prod for testing:
none yet — ask Cesar to author one restricted tournament in the dashboard when the client half
is ready for a live check (dashboard editor ships in parallel this week).
