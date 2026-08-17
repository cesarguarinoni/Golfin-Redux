# Quick — `venue_hole_count_duplicate_jp`

**Status:** BUILT 2026-08-17 by the Architect (Cowork), awaiting Cesar's verification.
Two follow-up steps below are NOT done and cannot be done from Cowork.

## The bug

A Japanese player saw the hole count twice on the tournament venue line, the second time in
English:

```
霞ヶ関カントリー倶楽部 · 18ホール  -  18 Holes
```

## Cause

Every `tourn.venue.*` row in `Assets/Localization/LocalizationText.csv` already carries its own
hole count in its own language — `Kasumigaseki Country Club · 18 Holes` /
`霞ヶ関カントリー倶楽部 · 18ホール`. Three controllers appended a second count on top of it and
guarded against the duplicate by sniffing the rendered string:

```csharp
bool alreadyHasHoles = venueName.IndexOf("Holes", StringComparison.OrdinalIgnoreCase) >= 0;
```

That substring test cannot see `ホール`, so the guard passed and the English suffix was appended.

Sniffing the output for a word is the wrong shape of fix, so it was removed rather than extended
to a second language.

## What was found

The ladder was copy-pasted into **three** call sites, and they had already drifted:

| Call site | Behaviour before | Had the bug? |
|---|---|---|
| `TournamentSignupModalController` (~325) | appends + sniffs | **yes** |
| `TournamentResultModalController` (~180) | identical copy | **yes** |
| `TournamentSelectionScreenController` (~222) | appends only on the fallback, separator `" · "` | no — but a third copy, with a hardcoded English `"Holes"` |

The implementer report for `tournament_signup_modal` recorded the card as also affected. It is
not — worth correcting there.

## The fix

**New** `Assets/Scripts/TournamentsRuntime/TournamentVenueLine.cs` — a sibling of
`TournamentDisplayName`, same shape, same echo-check idiom, `Golfin.Tournaments`, Assembly-CSharp.

```csharp
public static string Resolve(TournamentDefinition? def);
public static string Resolve(string? clubId, int holeCount);
```

Ladder:

1. `tourn.venue.<clubId>` resolves → **return it verbatim.** It is authoritative and already
   carries its count. Nothing is appended, so there is nothing to guard against.
2. No row → `"{clubId}  -  {N} {Holes}"`, where the word comes from the new key
   `tourn.venue.holes_suffix` and falls back to the English literal until that key ships.
3. Empty club id → empty string. Zero holes → the id alone.

All three call sites now call it. The separator on the fallback path is `"  -  "`, matching the
two modals and Figma fidelity row 2c; the card's fallback therefore changes from `" · "` to
`"  -  "`. That path is only reachable for a club with no localization row — which a
dashboard-created tournament on a new course can now produce, which is precisely why the
hardcoded English word needed to go.

## Files

- **New:** `Assets/Scripts/TournamentsRuntime/TournamentVenueLine.cs` (no `.meta` — Unity will
  generate one on next focus)
- **Modified:** `TournamentSignupModalController.cs`, `TournamentResultModalController.cs`,
  `TournamentSelectionScreenController.cs` — each a ~12-line inline block replaced by one call
- **Modified:** `Assets/Localization/LocalizationText.csv` — one row,
  `tourn.venue.holes_suffix,Holes,ホール`

## ⏭ Not done — needs the Unity Editor

1. **Compile + run.** This was written without an Editor. Braces balance, no call site still
   references the removed locals, and `System` is still needed in both modals — but it has not
   been through a compiler.

**The CSV re-import is automatic** (Cesar, 2026-08-17 — correcting an earlier note here that
called for a manual import). `Assets/Localization/Editor/LocalizationPlaymodeHook.cs` is
`[InitializeOnLoad]` and calls `LocalizationTextImporter.ImportCsv(logResult: true)` on
`PlayModeStateChange.ExitingEditMode`, so entering Play regenerates
`LocalizationTextTable.asset` from the CSV. Confirmed: the asset currently has the six
`tourn.rules.*` keys but **not** `tourn.venue.holes_suffix`, because that row was added from
outside the Editor — the next Play picks it up.

~~The hook fires on entering Play, not on build.~~ **Closed 2026-08-17** — see below.

## Follow-on: the CSV now imports on build too

`LocalizationPlaymodeHook` fired on `PlayModeStateChange.ExitingEditMode` and nothing else, so a
build made after a CSV edit but without entering Play shipped whatever was in the committed
`.asset`. That failed silently: the build succeeded, the strings were just stale, and a key added
since the last Play rendered as the key itself on a tester's device.

**New** `Assets/Localization/Editor/LocalizationBuildHook.cs` — an `IPreprocessBuildWithReport`
(`callbackOrder = -100`) that calls the same `LocalizationTextImporter.ImportCsv`. The two hooks
are independent and both cheap; the importer already ends in `EditorUtility.SetDirty` +
`AssetDatabase.SaveAssets`, so the regenerated table is on disk before player data is written.

It **fails the build** when `LocalizationText.csv` is missing rather than warning: the importer
would otherwise leave the previous table in place and the build would succeed with stale text,
and a warning in a batchmode log is a warning nobody reads.

If a future pipeline ever consumes the table before preprocess callbacks run, the deterministic
fallback is running `Tools → Localization → Import Text CSV` as an explicit step ahead of the
build. Not needed today.

## Verification

- JP, `kasumigaseki_open` sign-up modal → `霞ヶ関カントリー倶楽部 · 18ホール`, count once.
- EN, same → `Kasumigaseki Country Club · 18 Holes`, unchanged from today.
- Selection card, both languages → unchanged for all six shipped courses.
- A tournament on a club id with no `tourn.venue.*` row → `<clubid>  -  18 Holes`, and after the
  re-import, `18ホール` in Japanese.
- Result modal (T8) → same as the sign-up modal.

## ⚠️ Concurrency note

`TournamentSignupModalController.cs` is also touched by `tournament_banners`
(`TryResolveModalBanner`, ~line 532). Different region, but if Claude Code rewrites that file
wholesale this edit can be lost — re-check the venue branch after that task lands.
