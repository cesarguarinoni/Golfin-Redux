# QUICK SPEC — tournament_title_ja (a Japanese name for dashboard-created tournaments)

**Status:** SPEC_READY
**Author:** Architect (Cowork session), 2026-08-17, from Cesar: *"Since we are bilingual, add a Japanese title to use when no key is present as well. We will move the Localization to the editor in the future but for now that should be enough."*
**Size:** one nullable field threaded through the mapper and the name ladder, plus tests. No new systems.
**Server + dashboard halves are already done** — see §2. This spec is the Unity half only.

---

## 1. Why

GOLFIN ships EN + JP. Until now the only bilingual path for a tournament name was `name_key`, a localization key resolved against `LocalizationText.csv` — which ships **inside the build**, so the dashboard can only reference keys that already exist. A tournament named in the panel therefore had exactly one name, in one language, and Japanese players saw the English one.

The fix is deliberately the smallest thing that works: a second title column. The name ladder gains one rung.

⚠️ **Interim by design.** Cesar: *"we will move the Localization to the editor in the future."* Two columns do not scale to a third language and nothing here should be read as a decision that they would. When the editor owns localization, `title`/`title_ja` migrate into whatever replaces them.

## 2. Already shipped (do not redo)

- **DB:** `tournaments.title_ja text` — `playlife/backend/migrations/2026_08_17_tournaments_title_ja.sql`.
- **API:** `GET /api/v1/tournaments/golfin` now selects and returns `title_ja` (`backend/routers/tournaments.py`).
- **Dashboard:** a **Title (Japanese)** field next to Title, bounded at 80 chars, carried through create / update / duplicate and audited like every other field. The Title field also warns when a `name_key` is set, since a resolving key beats both titles.

## 3. The change

### 3.1 `TournamentDefinition` (`Assets/Scripts/Tournaments/TournamentDefinition.cs`)

Add `string? TitleJa` as a **nullable field appended after `Title`**, keeping the positional ctor a minimal diff. Null for CSV rows, exactly like `Title` and `BannerUrl`.

### 3.2 Mapper (`Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs`)

Map `title_ja` → `TitleJa`. Same treatment as `title`: trimmed, empty-to-null. It is a plain string — do not let the JSON reader touch it (the file already runs `DateParseHandling.None`; nothing new needed, but do not "simplify" that away).

### 3.3 The ladder (`Assets/Scripts/TournamentsRuntime/TournamentDisplayName.cs`)

```
1. localize(name_key)   — a key that resolves in the shipped build still wins
2. title_ja             — ONLY when LocalizationManager.CurrentLanguage == Language.Japanese
3. title
4. slug
```

Rung 1 stays first: a shipped key is a real translation pair and beats an operator's single-language string. Rung 2 is skipped entirely on English — an English player must never see `title_ja`, even if `title` is empty. If a JP player has no `title_ja`, they fall to `title`; that is correct and intended, not a gap to paper over.

`LocalizationManager.CurrentLanguage` (`Assets/Localization/LocalizationManager.cs:10`) is the check; `LocalizedText.cs:58` uses the same comparison.

Keep `Resolve(nameKey, title, id)` working, or update every caller — the raw-parts overload is what the tests exercise.

### 3.4 Language switching

`LocalizationManager.OnLanguageChanged` exists (`:13`). `TournamentSelectionScreenController.OnEnable` (`:92`) already rebuilds cards on entry, so switching language and returning to T7 repaints correctly. **Check whether the language toggle is reachable while T7 is open.** If it is, subscribe and rebuild; if it is not, do nothing and say so in the report — do not add a subscription for a state that cannot occur.

## 4. Known gap, worth one cheap fix

The shipped `tournaments.csv` has no title column — only `nameKey`. The dashboard's CSV export writes `nameKey ?? title` into that column, so a dashboard-named tournament exported to CSV comes back with `NameKey = "Cesar Championship"` (which resolves nowhere) and `Title = null`, and the ladder falls through to the **slug**. Offline, that tournament shows `cesar_championship`.

Cheap fix, and take it: in `TournamentCsvLoader`, also assign the raw `nameKey` column value to `Title`. A key that resolves is unaffected (rung 1 wins); a non-resolving one becomes a readable name instead of a slug. Do not add columns to the CSV — that file is the offline fallback, not a second source of truth.

## 5. Acceptance

1. A tournament with `title_ja` set and **no** `name_key`: JP player sees `title_ja`, EN player sees `title`.
2. A tournament with `title_ja` **and** a resolving `name_key`: both languages see the localized name — the key still wins.
3. A tournament with `name_key` only (all six seeded rows): unchanged in both languages.
4. `title_ja` set, `title` empty, EN player: sees the slug, **never** the Japanese string.
5. Offline/CSV path: a dashboard-named tournament shows its name, not its slug (§4).
6. Full EditMode suite green, swept **per assembly** — a filtered run reports `FailedTests` for the filter only, and `tests-run` intermittently reports "No tests found" for a valid assembly; retry, never read that as green. New tests cover all four ladder rungs plus the EN-never-sees-JP case.
