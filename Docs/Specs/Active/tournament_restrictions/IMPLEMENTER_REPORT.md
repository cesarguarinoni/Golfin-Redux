# IMPLEMENTER_REPORT — `tournament_restrictions` (client half)

Implemented directly in the main Claude Code thread at Cesar's request (no subagent chain).
Server (`playlife`) untouched — read only, for the contract.

---

## Files changed

| File | What changed |
|---|---|
| [TournamentRestrictions.cs](Assets/Scripts/Tournaments/TournamentRestrictions.cs) | **NEW.** The restriction vocabulary: `TournamentCategory` / `TournamentDivisionType` / `TournamentGearRule` enums plus tolerant parsers and the 1-based rarity ladder (the client mirror of the server's `RARITY_RANK`). Unknown input → null → unrestricted; never throws. |
| [TournamentEligibility.cs](Assets/Scripts/Tournaments/TournamentEligibility.cs) | **NEW.** The pure entry gate: `Evaluate(def, rarityRank, level, equippedClubRanks)` → `TournamentEligibilityFailure`. Takes ranks, not managers, so the whole matrix is EditMode-testable. Mirrors `_check_entry_eligibility` case for case, including the deny-when-unknown branches. |
| [TournamentRulesText.cs](Assets/Scripts/TournamentsRuntime/TournamentRulesText.cs) | **NEW.** Pure composition of the modal's five RULES lines and the refusal toasts from a `TournamentDefinition`. Each null falls back to its original localization key, so an unrestricted tournament renders exactly today's strings. Rarity renders as its coloured single letter via `RarityHelper` (`RarityTag`). |
| [TournamentDefinition.cs](Assets/Scripts/Tournaments/TournamentDefinition.cs) | 10 appended-optional ctor params (raw wire strings/ints) + 10 properties, normalised in the ctor. Adds `EffectiveGearRule` / `EffectiveCategory` / `EffectiveDivisionType` (the backfilled defaults the *gate* reasons with) and `HasEntryRestrictions`. Existing positional call sites untouched — same pattern as `Title` / `BannerUrl`. |
| [RemoteTournamentDtos.cs](Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs) | The 10 `JsonProperty` fields beside `league_key`. Counts are `int?`, not `int`, so an absent `max_players` cannot deserialise as a cap of zero. |
| [TournamentScheduleMapper.cs](Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs) | Pass-through of all 10, deliberately with no drop condition — nothing in the block can make a tournament undisplayable. |
| [TournamentNetDtos.cs](Assets/Scripts/TournamentsRuntime/TournamentNetDtos.cs) | `reason` + `max_players` on the enter response, and `IsFull` / `IsIneligible` beside the existing `IsInsufficient`. |
| [RemoteTournamentBackend.cs](Assets/Scripts/TournamentsRuntime/RemoteTournamentBackend.cs) | `TournamentRegisterStatus.Full` / `.Ineligible`; `MaxPlayers` + `IneligibleReason` on the outcome; `EnterRoutine` maps both 200-shaped denials before the entered/already-entered branch. |
| [TournamentSignupModalController.cs](Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs) | `ApplyRules(def)` is data-driven; `OnConfirm` gates eligibility before the payment path; the remote `Full` / `Ineligible` outcomes toast through the same copy; new `EvaluateEligibility` / `EquippedClubRarityRanks` adapters. |
| [LocalizationText.csv](Assets/Localization/LocalizationText.csv) | 16 new rows (EN + JA) — 11 RULES value forms, 5 refusal toasts. **JA is Architect-drafted and flagged for native review.** The `RARITY_*` rows are no longer consulted by this block: rarity is a letter now, which is language-neutral. |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated from the CSV via `Tools/Localization/Import Text CSV`. |
| [TournamentEligibilityTests.cs](Assets/Scripts/Tournaments/Tests/TournamentEligibilityTests.cs) | **NEW.** 25 tests — normalisation/degrade, the full rarity/level/club matrix, unrestricted-is-always-eligible. |
| [TournamentRestrictionsClientTests.cs](Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs) | **NEW.** 18 tests — mapper carry-through, rank-ladder pin, RULES rendering EN + JA, refusal copy, server denial DTOs, and the widget-click CONFIRM fixture. |

`Assets/Resources/Data/tournaments.csv` — **not touched**, as specified. The CSV path composes
unrestricted definitions and is gated by a test that walks every shipped row.

---

## Design notes worth reviewing

**Nullable enums, not defaulted enums.** `DivisionType` / `GearRule` / `Category` are `T?`. The RULES
block needs "the server did not say" and "the server said `level`" to render *differently* — null
falls back to the pre-existing localized line, a value renders the new one. The backfilled defaults
still exist, but only on the `Effective*` accessors, which is what the gate reads.

**The gate sits after the already-entered short-circuit.** A player who is already in must not be
thrown out by a rule they now fail (a bag change, a dashboard edit mid-tournament). It still runs
before every payment path — local `TrySpendAsync` and remote `RegisterAsync` alike.

**Rarity renders as a coloured letter (Cesar, 2026-08-19).** `R` / `L` / `M` in the rarity colour,
not `RARE` / `LEGENDARY` / `MYTHIC`. Both the letter and the colour come from `RarityHelper`, the
project's one source for rarity presentation — the badge on every card already reads C/U/R/M/L/S in
exactly these colours, so the RULES block cannot drift from them. It is rich text, and both
consumers render through TMP (the RULES body, and `ToastController`'s `TMP_Text`). It also shortens
the longest line materially: `CHARACTERS: RARE – LEGENDARY · Lv 80 – 160` became
`CHARACTERS: R – L · Lv 80 – 160`, which takes most of the pressure off the M2 wrap risk below.

**Intended copy change (SPEC §2).** `tourn.rules.gear` said "Supplied by GOLFIN", which was display
fiction. It survives *only* as the null fallback; a server-fed tournament (all backfilled to `own`)
now reads "Own clubs". Gated by a test that asserts both halves.

**A real bug the widget test caught.** The first adapter read rarity via
`CharacterManager.GetCharacterTemplate`, which is the *ScriptableObject fallback* — it returns null
and logs an error whenever the SO database is unassigned, which is the shipped CSV-first
configuration. Every character would have been unranked, and a rarity-restricted tournament would
have refused **everyone**. Now CSV-first with the SO as fallback, matching
`CharacterManager.GetMaxLevel`.

---

## Acceptance checklist

| # | SPEC acceptance item | Verdict | Evidence |
|---|---|---|---|
| 1 | Mapper carries all 10 fields; nulls → unrestricted; unknown enums degrade; CSV behaves as today | **PASS** | `RestrictionMapperTests` (4 tests). `All_ten_restriction_fields_are_carried_through_to_the_definition` asserts each of the 10 individually; `A_tournament_with_no_restriction_fields_maps_to_an_unrestricted_definition` pins the `int?` decision (absent `max_players` must not become 0); `An_unknown_vocabulary_from_a_newer_server_degrades_rather_than_dropping_the_row` feeds `category=seasonal_v2 / division_type=handicap / gear_rule=rental / char_rarity_min=Ultra`; `The_shipped_csv_composes_unrestricted_definitions` walks every row of the real `tournaments.csv`. |
| 2 | Eligibility matrix — rarity below/above/in, level below/above/in, club cap violated, `supplied` skips, all-null always eligible | **PASS** | `TournamentEligibilityTests` (25 tests), §2–§5. Includes inclusive bounds on both ends, open-ended bands, the deny-when-unresolvable branches (and that they fire *only* when the band is set), null vs empty bag, and the server's rarity-before-level ordering. |
| 3 | Modal render — authored restrictions show real values (EN and JA); no restrictions renders the same 5 strings as today | **PASS** | `RulesBlockTests` (5 tests). `An_unrestricted_tournament_renders_exactly_the_five_original_strings` compares against the five original keys byte-for-byte. EN asserts 64 / 32 / "Rarity band" / "Own clubs" / 80 / 160 plus the coloured letters for Mythic/Rare/Legendary, and that no spelled-out rarity name survives; JA asserts 最大参加人数：64 / ディビジョン：レアリティ別 / 自分のクラブ / the same letter, and that no English literal leaks through. `Every_rarity_renders_as_its_own_coloured_single_letter` gates the format itself for all six: exactly one character, the right letter, no spelled-out name, and six distinct colours. Both assert no raw `tourn.rules.` key survives, i.e. every key exists in the CSV in both columns. |
| 4 | Ineligible CONFIRM → toast, zero RP delta, no entry, no navigation (widget-click test) | **PASS** | `IneligibleConfirmWidgetTests` (3 tests) drives the **real** `_confirmButton.onClick.Invoke()` on the real `TournamentSignupModalController`, wired by the modal's own `Awake`. The two ineligible cases assert `LogAssert.Expect` on the gate's refusal log **and** that no entry exists — `CompleteSignup` registers *before* it navigates, so a missing entry rules out the debit and the navigation together. The third is the control: an unrestricted tournament still registers through the same button, which is also the proof that the button was wired at all. |
| 5 | Full EditMode suite green, swept per assembly | **PASS (with a caveat, see below)** | `tests-run` EditMode: **1478 total / 1475 passed / 0 failed / 3 skipped**. |

### Caveat on acceptance #5 — "swept per assembly"

`mcp__ai-game-developer__tests-run` **rejects** `testAssembly` / `testClass` filters in this project
(`No tests found matching assembly 'Golfin.Tournaments.Tests'`) and runs the whole mode regardless —
the known behaviour recorded in `reference_tests_run_ignores_class_filters`. The sweep was therefore
executed as a single whole-mode EditMode run, which *is* a superset of every per-assembly run. The
risk the per-assembly rule guards against (a filtered run masking failures) does not apply to a
whole-mode run; the reverse is what it forbids.

### The 3 skips are pre-existing

All three are `Golfin.Physics.Tests.HoleCompleteDriverTests`, each carrying its own Stage-C1
explanatory message ("HandleShotComplete is now a no-op…"). Untouched by this task.

---

## Needs manual / on-device verification

| # | What | Why it cannot be gated here |
|---|---|---|
| M1 | A **live restricted tournament** end-to-end: dashboard-authored restrictions → schedule fetch → RULES block → refused CONFIRM | Nothing in prod carries restrictions yet (SPEC § Verification data: *"none yet — ask Cesar to author one"*). Every field is covered by a synthetic payload; only the round trip against the real `list_golfin` is unverified. |
| M2 | **RULES block visual fit** — a restricted tournament's longest line is `CHARACTERS: R – L · Lv 80 – 160` vs the original `CHARACTERS: Unrestricted` | Much less pressing since rarity became a letter, but still a pixel question: the composition is gated as a *string*, and whether it wraps in the body text box (and that TMP rich text is enabled on that field, which it is by default) needs eyes. |
| M3 | **Server-denial toasts** (`full`, `ineligible`) on a device | The DTO parse and the status mapping are gated, but the remote path needs a real signed-in session against a full/restricted tournament. |
| M4 | **JA copy** — all 16 new rows | Architect-drafted, flagged for native review exactly as `tournament_signup_modal` did. |

---

## Deliberately not done

- **Category tag near the modal title** (SPEC §2, "optional, only if trivial"). **Dropped outright,
  not deferred** — Cesar, 2026-08-19: *"We don't need a Category tag in the modal right now. The
  Sponsor field can be used for that (if Sponsor = Golfin, then it is a hardcore tournament)."* The
  modal's header already renders `{SPONSOR} PRESENTS`, and the shipped `tournaments.csv` already
  authors `GOLFIN` literally on `hirono_invitational` and `kawana_fuji_open`, so the signal is on
  screen today with no new element and no prefab surgery.

  `Category` is still carried DTO → definition → mapper, because SPEC §1 mandates all 10 fields and
  the dashboard authors it; nothing in the UI reads it, by decision.

  ⚠️ One caveat for whoever picks this up later: `category` and `sponsor_name` are **independent**
  dashboard columns, so they can disagree — a `category='competitive'` tournament presented by PUMA
  would not be identifiable by its sponsor. Under this convention the SPONSOR line is the source of
  truth for what the player sees and `category` is inert, which is fine while nothing consumes it;
  they are not synonyms and should not be treated as such if a real category surface is ever built.
- Standard-spec stat normalization, the dashboard, division/bracket logic, remaining-slots display —
  all SPEC §4 out of scope.

## Handoff note for `tournament_async_board`

The `full` / `ineligible` mapping landed on the **existing** `RemoteTournamentBackend.EnterRoutine`
and the modal's existing `RegisterAsync` switch, so there is no orphaned mapping waiting for a
future Register path — the async-board client is already here and already routed.

## Editor state

`ShellScene` had **unsaved in-memory changes that were not mine** (the `.unity` file is clean in
git). Per Cesar's instruction the dirty flag was cleared with `EditorSceneManager.ClearSceneDirtiness`
— nothing written to disk, nothing reverted in memory. No scene was saved at any point in this task.
The editor is left with no scene dirty and no play mode.
