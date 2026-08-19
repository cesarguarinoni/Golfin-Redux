# tournament_restrictions — client half, handoff to Architect

**Status:** ready for review · **Suite:** 1478 EditMode tests, 1475 passed, 0 failed, 3 pre-existing skips
**New tests:** 43 · **Files touched:** 13 · **Committed:** no · **Server:** untouched (read for the contract only)

---

## What shipped

The 10 restriction fields travel `DTO → TournamentDefinition → mapper → modal`; the RULES block is
composed from data instead of five fixed strings; CONFIRM refuses an ineligible entry before the
payment path.

`Assets/Resources/Data/tournaments.csv` gained **no columns**. Every CSV row composes an unrestricted
definition, so the offline path behaves exactly as before — gated by a test that walks every row of
the real file.

The decision lives in `TournamentEligibility`, a pure evaluator taking *ranks* rather than managers,
mirroring the server's `_check_entry_eligibility` case for case including its deny-when-unresolvable
branches. The modal holds only the adapter that reads `CharacterManager` / `BagManager` and converts
to ranks.

---

## The bug worth knowing about

**A rarity-restricted tournament would have refused every player.**

The first adapter read rarity through `CharacterManager.GetCharacterTemplate` — the *ScriptableObject
fallback*, which in the shipped CSV-first configuration returns null and logs an error. Every
character would have reached the gate unranked, and an unranked character cannot prove it sits inside
a band, so all of them would have been denied.

Now CSV-first with the SO as fallback, matching the ladder `CharacterManager.GetMaxLevel` walks.

It surfaced only by driving the real CONFIRM button through the real modal. Every unit test of the
evaluator passed throughout — the evaluator was never wrong, the adapter feeding it was.

---

## Decisions to sanity-check

**Nullable enums, not defaulted ones.** `Category` / `DivisionType` / `GearRule` are `T?`. The RULES
block needs "the server said nothing" and "the server said `level`" to render differently — null
falls back to the pre-existing localized line, a value renders the new one. The backfilled defaults
live only on the `Effective*` accessors, which is what the gate reads, not what display reads.

**The gate sits after the already-entered short-circuit.** A player already entered must not be
thrown out by a rule they now fail (bag change, dashboard edit mid-tournament). It still runs ahead
of every payment path, local and remote.

**Unknown values degrade, never throw.** A newer dashboard can author a division type or rarity name
this build has never met; anything unrecognised normalises to null (unrestricted) rather than
dropping the row or throwing. A schedule must not go down over one string.

**One intended copy change.** `tourn.rules.gear` read "Supplied by GOLFIN", which was display fiction.
It now shows only where the field is genuinely absent; every server-fed tournament is backfilled to
`own`, so in practice the line reads "Own clubs".

**Rarity renders as its coloured letter** — `C U R M L S`, letter and colour both from `RarityHelper`,
the same source every card badge uses, so the block cannot drift into a second palette. Letters are
language-neutral, so EN and JA render identically and the `RARITY_*` rows are no longer consulted.
It also shortened the worst line: `CHARACTERS: RARE – LEGENDARY · Lv 80 – 160` →
`CHARACTERS: R – L · Lv 80 – 160`.

**No category tag — the sponsor line carries it** (Cesar, 2026-08-19). A tournament presented by
GOLFIN is the hardcore one. The header already renders `{SPONSOR} PRESENTS` and the shipped CSV
already authors `GOLFIN` literally on `hirono_invitational` and `kawana_fuji_open`, so the signal is
on screen today with no new element and no prefab surgery. `Category` is still carried end-to-end
because the contract mandates it; nothing in the UI reads it.

---

## Acceptance

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Mapper carries all 10; nulls unrestricted; unknown enums degrade; CSV unchanged | PASS | 4 tests. Each of the 10 asserted individually; absent `max_players` pinned as null not a cap of zero; every real CSV row walked. |
| 2 | Eligibility matrix | PASS | 25 tests. Inclusive bounds both ends, open-ended bands, deny-when-unresolvable firing only where the rule is set, null vs empty bag, server's rarity-before-level ordering. |
| 3 | Modal render, EN and JA | PASS | 5 tests. Unrestricted compared byte-for-byte against the five original keys; restricted asserted in both locales; coloured-letter format gated for all six rarities. |
| 4 | Ineligible CONFIRM registers nothing | PASS | 3 tests driving the real `onClick`. Register precedes navigation, so a missing entry rules out debit and navigation together. The eligible control proves the button was wired at all. |
| 5 | Full EditMode suite, swept per assembly | PASS, caveat below | 1478 / 1475 passed / 0 failed / 3 skipped. |

**Caveat on #5.** `tests-run` rejects `testAssembly` / `testClass` filters in this project and runs
the whole mode regardless. The sweep ran as a single whole-mode EditMode pass — a *superset* of every
per-assembly run. The failure the per-assembly rule guards against is a filtered run masking
failures, which is the opposite direction. Flagging it rather than claiming the letter of the rule.

The 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips, each with its own explanatory
message. Untouched.

---

## Not verified

All blocked on the same thing: nothing in production carries restrictions yet, so none of this has
made a real round trip.

| What | Why it isn't gated |
|---|---|
| Live restricted tournament, end to end | Every field is covered by a synthetic payload; only the trip through the real `list_golfin` is unproven. Needs one dashboard-authored restricted tournament. |
| RULES block visual fit | Composition is gated as a *string*; whether it wraps in the body text box is a pixel question. Less pressing now the band reads `R – L`. |
| Server `full` / `ineligible` toasts on device | DTO parse and status mapping are gated; the remote path needs a signed-in session against a full or restricted tournament. |
| Japanese copy, 16 rows | Drafted here, flagged for native review — same handling as `tournament_signup_modal`. |

---

## Open questions

None block the review. These are the things I would not decide unilaterally.

**Q1 — Client and server measure character level differently.** The client checks the *selected
character's* `currentLevel`. The server checks `profiles.golfin_character_level`, one synced value per
profile, not per character. The server's own comment calls it a plausibility check, so it reads as
deliberate — but the two can disagree: a player who levels one character and enters with another can
pass the client gate and be denied by the server, or the reverse. The failure is soft (a toast, no
debit either way). Worth confirming it is the intended asymmetry rather than something to converge.

**Q2 — `club_rarity_max` is client-enforced only.** The server never sees the bag, so this cap exists
purely in the client and a modified client bypasses it. Fine if it is a design knob rather than an
integrity rule — worth stating explicitly, since it is the only restriction with no server backstop.

**Q3 — `gear_rule = supplied` renders and gates but does not swap clubs.** v1 shows "Supplied by
GOLFIN" and skips the bag check, which is what the spec asked for; actually playing a standard set is
the later standard-spec task. Until that lands, a `supplied` tournament tells the player their clubs
are provided and then hands them their own bag — **so don't author one in the dashboard yet.**

**Q4 — `category` is now inert on the client.** Since the sponsor line conveys "hardcore", nothing
reads it; it is carried only because the contract mandates it. `category` and `sponsor_name` are
independent dashboard columns and can disagree — a `competitive` tournament sponsored by PUMA would
not be identifiable by its sponsor. Harmless while nothing consumes it; it would matter if a real
category surface is later built assuming they are synonyms.

---

## Files

| File | | Change |
|---|---|---|
| `Assets/Scripts/Tournaments/TournamentRestrictions.cs` | NEW | Enums, tolerant parsers, and the 1-based rarity ladder mirroring the server's `RARITY_RANK`. |
| `Assets/Scripts/Tournaments/TournamentEligibility.cs` | NEW | The pure gate. Takes ranks, not managers. |
| `Assets/Scripts/TournamentsRuntime/TournamentRulesText.cs` | NEW | Composes the five RULES lines and the refusal toasts. Rarity via `RarityHelper`. |
| `Assets/Scripts/Tournaments/TournamentDefinition.cs` | | 10 appended-optional params normalised in the ctor, plus `Effective*` accessors and `HasEntryRestrictions`. |
| `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs` | | The 10 wire fields. Counts are `int?` so an absent cap cannot deserialise as zero. |
| `Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs` | | Pass-through, deliberately with no drop condition. |
| `Assets/Scripts/TournamentsRuntime/TournamentNetDtos.cs` | | `reason` and `max_players`; `IsFull` / `IsIneligible` beside `IsInsufficient`. |
| `Assets/Scripts/TournamentsRuntime/RemoteTournamentBackend.cs` | | Two new register statuses; `EnterRoutine` maps both 200-shaped denials. |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | | Data-driven `ApplyRules(def)`, the eligibility gate, and the server-denial routing. |
| `Assets/Localization/LocalizationText.csv` | | 16 rows, EN + JA. Table asset regenerated from it. |
| `Assets/Scripts/Tournaments/Tests/TournamentEligibilityTests.cs` | NEW | 25 tests — normalisation, the full matrix, unrestricted-admits-anyone. |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs` | NEW | 18 tests — mapper, ladder pin, RULES in both locales, refusal copy, denial DTOs, widget click. |

**Deliberately untouched:** `playlife` (read only), `Assets/Resources/Data/tournaments.csv` (no new
columns, per spec), and the `TournamentSignupModal` prefab (no surgery, so no prefab/scene churn in
the diff).

---

Nothing is committed. The working tree also holds unrelated pre-existing drift (Klyro club art, build
stamp, lessons) that is not mine to sweep into this commit.
Full per-item detail: `Docs/Specs/Active/tournament_restrictions/IMPLEMENTER_REPORT.md`.
