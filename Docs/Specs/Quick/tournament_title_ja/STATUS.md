DONE — approved by Cesar 2026-08-17

# tournament_title_ja — Unity half

**2026-08-17.** The name ladder gained its JP rung and the CSV round-trip gap in §4 is closed.
Server + dashboard halves were already deployed and were not touched.

```
1. localize(name_key)   — a key that resolves in the shipped build still wins
2. title_ja             — ONLY when LocalizationManager.CurrentLanguage == Language.Japanese
3. title
4. slug
```

## Files changed

| File | Change |
|---|---|
| [TournamentDefinition.cs](Assets/Scripts/Tournaments/TournamentDefinition.cs) | `string? TitleJa` property after `Title`; ctor param appended **last** (after `bannerUrl`) so every positional call site compiles untouched |
| [RemoteTournamentDtos.cs](Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs) | `[JsonProperty("title_ja")] public string? TitleJa` |
| [TournamentScheduleMapper.cs](Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs) | `titleJa: NullIfBlank(t.TitleJa)` — same trim/empty-to-null treatment as `title`. `DateParseHandling.None` untouched |
| [TournamentDisplayName.cs](Assets/Scripts/TournamentsRuntime/TournamentDisplayName.cs) | New rung 2, gated on `CurrentLanguage == Language.Japanese`. New 4-part overload; the 3-part `Resolve(nameKey, title, id)` is kept and delegates with `titleJa: null` |
| [TournamentCsvLoader.cs](Assets/Scripts/Tournaments/TournamentCsvLoader.cs) | §4 fix: the raw `nameKey` column value is also assigned to `Title`. No new CSV column |
| [TournamentSelectionScreenController.cs](Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs) | Subscribes to `LocalizationManager.OnLanguageChanged` — see §3.4 below |
| [RemoteScheduleTests.cs](Assets/Scripts/TournamentsRuntime/Tests/RemoteScheduleTests.cs) | +12 tests; `Fixtures.Tournament` gains `titleJa`; ladder suite saves/restores `CurrentLanguage` |
| [TournamentCsvLoaderTests.cs](Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs) | +1 test on the **real** loader against the **shipped** CSV |

## §3.4 — the language toggle IS reachable while T7 is open, so it is now subscribed

Settings is an *overlay*, not a `ScreenManager` screen (`ScreenManager.cs:33`, `:244`), opened from the
top-bar gear in `PersistentUIManager.OnSettingsButtonClick`. That top bar is shown on
`ScreenId.TournamentSelection` (`PersistentUIManager.cs:466`, `:493`). So the player can open Settings →
Language → 日本語 with T7 still active underneath, and `OnEnable` never re-fires when the overlay closes.
`OnLanguageChanged` is therefore subscribed in `OnEnable` / unsubscribed in `OnDisable`, reusing the same
`HandleScheduleChanged` repaint the server fetch already uses. Every card string is language-dependent
(the ladder's JP rung, the venue line, the date line), not just the name.

The signup and result modals resolve their title once on open; a language switch is not reachable while
either is up (they sit above T7 and the gear is behind them), so neither was subscribed.

## Acceptance

| # | Item | Result |
|---|---|---|
| 1 | `title_ja`, no `name_key`: JP sees `title_ja`, EN sees `title` | **PASS** (logic) — `Rung2_JapanesePlayerSeesTitleJa_EnglishPlayerSeesTitle`, plus a live in-editor probe through the real `TournamentDisplayName`. End-to-end card render: see *Needs a real run* |
| 2 | `title_ja` **and** a resolving `name_key`: both languages get the localized name | **PASS** — `Rung1_ResolvingKeyStillWinsInBothLanguages` (bilingual table, both directions) |
| 3 | `name_key` only (the six seeded rows): unchanged in both languages | **PASS** — `SeededKeyOnlyRowsAreUnchangedInBothLanguages`, and the real loader confirms all 6 shipped rows still carry their key |
| 4 | `title_ja` set, `title` empty, EN player: sees the slug, **never** the Japanese string | **PASS** — `EnglishPlayerFallsToTheSlugRatherThanEverShowingTitleJa`, over `title` = null / `""` / `"   "`, with and without a non-resolving key. **Tripwire-proven**: dropping the `Language.Japanese` guard in production fails this test and two others; restored and re-run green |
| 5 | Offline/CSV: a dashboard-named tournament shows its name, not its slug | **PASS** — `LoadTournaments_RealLoader_MirrorsNameKeyIntoTitle` runs the **production** loader against the **shipped** CSV (all 6 rows: `Title == NameKey`, `TitleJa == null`); `CsvExportedDashboardTournamentRendersItsNameNotItsSlug` covers the render half |
| 6 | Full EditMode suite green, swept per assembly | **PASS** — see below |

### Suite sweep (per assembly, EditMode)

`TournamentsRuntime 73` · `Tournaments 210` · `UI 5` · `Gameplay 302` · `Net 18` · `Auth 27` ·
`Economy 53` · `EconomyRuntime 6` · `Save 44` · `Course 26` · `Physics 357 (+3 skipped)` ·
`Tests.EditMode 36` · `UI.Shop 8` · `UI.Rankings 17` · `Core.Stamina 37` · `HoleCompleteModal 16` ·
`SceneSnapshot 8`

**1243 passed / 0 failed / 3 skipped = 1246**, which is exactly the suite total — every assembly is
accounted for, nothing was silently missed. The 3 skips are the pre-existing `HoleCompleteDriverTests`
Stage-C1 skips. Baseline was 1233 total; +13 is exactly the 13 tests added here.

The first `TournamentsRuntime` run returned the known spurious "No tests found"; retried, never read as green.

## Needs a real run (not verifiable from here)

1. **A tournament with `title_ja` rendering on the T7 card in JP.** Everything above verifies the ladder
   and the mapper; nothing here rendered an actual card from an actual server row, because that needs a
   dashboard tournament with `title_ja` populated and a logged-in session against prod. Play mode in the
   editor would do — a device is not strictly required, but a live row is.
2. **The §3.4 repaint.** The subscription is wired and compiles, but "open Settings → 日本語 → the T7 cards
   repaint in place" was not exercised. Same prerequisite as (1).
3. Item 1's EN/JP split at the card level, for the same reason.

Items 2–5 need nothing further: they are pure functions and are pinned by tests that were proven to run.

## Interim, on purpose

Two title columns do not scale to a third language. When localization moves into the editor,
`title` / `title_ja` migrate into whatever replaces them — nothing here should be read as a decision
that a third column would follow.
