# finished_tournament_leaderboard_route — LEADERBOARD on a finished tournament opened Play Hole

**Reported (Cesar, 2026-09-14):** *"Entering Leaderboard in a finished Tournament from Tournament
select screen goes to Play Hole. You should not be able to play in finished tournaments."*

## Root cause — a serialized enum shifted under its stored values

`ScreenId` is stored **by value**: every `[SerializeField] ScreenId` on a screen controller
(`_leaderboardTarget`, `_holeSelectionTarget`, `_backScreen`, `_backTarget`, `_returnTarget`,
`_target`, `_initialScreen`) is an integer in the prefab / scene YAML. On 2026-08-29
(`c0733f1f4`, missions_v1 Phase B) `MissionSelection` was inserted at index 8 with no explicit
value, so every stored id ≥ 8 written before that day began naming the screen one slot over:

| Site | Stored | Written | Read as (after 08-29) | Author's screen | Player-visible effect |
|---|---|---|---|---|---|
| `TournamentSelectionScreen.prefab` `_leaderboardTarget` | 10 | 06-25 | TournamentHoleSelection | TournamentLeaderboard | **The report** — LEADERBOARD on a finished/ended card opened Play Hole |
| `TournamentSelectionScreen.prefab` `_holeSelectionTarget` | 9 | 06-25 | Leaderboard (Rankings) | TournamentHoleSelection | CONTINUE on a live entered card opened the Rankings screen |
| `TournamentSignupModal.prefab` `_holeSelectionTarget` | 9 | 08-17 | Leaderboard (Rankings) | TournamentHoleSelection | Signup CONFIRM landed on Rankings |
| ShellScene `TournamentLeaderboardScreenController._backScreen` | 9 | 06-24 | Leaderboard (Rankings) | TournamentHoleSelection | Board BACK fallback (empty stack, after a round) → Rankings |
| ShellScene `TournamentDevEntryButton._target` (TOURNAMENTS (TEMP)) | 11 | 06-25 | TournamentLeaderboard | TournamentSelection | The temp button opened the board — the "unrelated bug" GamePolishProbe filed as a finding |
| `StaminaShopDetailScreen.prefab` `_backTarget` | 12 | 07-03 | TournamentSelection | StaminaShopSelection | Stamina detail BACK fallback → tournament list |
| ShellScene `TournamentHoleSelectionScreenController._backScreen` | 7 | 06-24 | ModeSelection | TournamentSelection (tooltip: "T7 TournamentSelection screen") | Not the shift — authored one day before T7 existed; CLOSE fallback skipped the tournament list |
| `StaminaShopSelectionScreen.prefab` `_returnTarget` | 4 | — | Roster | Roster | fine |
| ShellScene `ScreenManager._initialScreen` | 0 | — | Logo | Logo | fine |

Shape audit (PIPELINE_HARDENING §22): the nine rows above are **every** `[SerializeField]
ScreenId` in the project; no `[Serializable]` struct, public field, `(ScreenId)int` cast,
PlayerPrefs or CSV carries a ScreenId (hints use names). No prefab-instance override
(`propertyPath: _…`) exists for any of them. The `store_history` task (09-09) had added an
"append only" comment to the enum — advisory, and it never audited the damage already done.

## Fix

1. **Data** — the seven stale ints re-serialized through `SerializedObject` (prefabs via
   `PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset`; scene objects → `SaveScene`).
   Prefab diffs are exactly the four ints.
2. **Pinned enum** — `ScreenId` carries explicit values (`Logo = 0 … StoreHistory = 34`) and a
   type summary with the rule: a new member takes the next free number, whatever its position.
3. **Gate** — `Assets/Tests/EditMode/ScreenIdSerializationTests.cs`: (a) every pinned name keeps
   its value, values unique, new members above the pinned range; (b) every serialized site on
   disk (the table above) names the author's screen, read from the YAML by script GUID; (c) every
   `[SerializeField] ScreenId` declared under `Assets/Scripts` has a row.
4. **Play guard** — "you should not be able to play in finished tournaments" is now a property of
   the hole selection itself, not of routing: `TournamentCardStateMapper.IsPlayable(status,
   nowPastEnd)` (SPEC §2 Row 2 as a predicate — `Map` Row 2 calls it) is checked by
   `TournamentHoleSelectionScreenController` when binding (no "Next" card: remaining holes bind
   Locked) and in `BeginTournamentHole` (refuses with a warning and rebinds). `MapCardStateTests`
   +6 cases, including the exhaustive "IsPlayable ⇔ EnteredActive" sweep.

## Proof

`GOLFIN ▸ Tournaments ▸ Verify — finished tournament routes (no video)`
(`Assets/Scripts/UI/Editor/TournamentRouteVerify.cs`): boots through the real StartButton, bottom-nav
TEE, the Tournaments mode card's PLAY, then the finished card's own CTA `onClick`. Verdict JSON +
log + stills in `Docs/Specs/Quick/media/finished_tournament_leaderboard_route/`:

- A1 / A3 LEADERBOARD on an ended card, then on the entered+ended card → `TournamentLeaderboard`
- A2 / A4 the board's CLOSE → `TournamentSelection` — on an empty board that is the empty state's
  own button (F1 fix); the bot records a finding and leaves through the bottom-nav TEE only if
  neither CLOSE is usable
- B1 hole selection opened with the finished tournament selected (a stale route) spawns no Next card
- B2 `BeginTournamentHole` on it refuses (warning logged, no gameplay load); B3 its CLOSE goes back
- C0–C3 CONTINUE on a live entered tournament still opens its Next card (positive control)
- Z the planted entries are removed from the save again

## Run log — 2026-09-14 17:34 JST (re-run after F2/F3; F1 run 15:02, first run 14:44), Editor 6000.3.9f1, signed-in dev profile, schedule from the disk cache

`route_verify.json` → **PASS**, 16/16 route checks (the bot also CREATES the reported state:
`Register()` on the local backend has no time guard, so an entry is planted on the ended
`kisarazu_cup` — a player who entered and whose tournament then ended with holes left — and one
synthetic live tournament is applied in memory for the positive control; both entries are removed
from the save again at the end, check Z):

| # | Check | Result |
|---|---|---|
| A1 | LEADERBOARD on ended `kisarazu_cup` (never entered) | → `TournamentLeaderboard` |
| A1s | shimmer after the (failing) board fetch settles | host inactive (F1b) |
| A2 / A4 | the empty board's CLOSE (the empty state's own button) | → `TournamentSelection` (F1) |
| F2 | a schedule applied after sign-in reaches the session's backend | `Backend (Remote) sees 4 of 4 defs` |
| F3 | hole selection header + first card for `kisarazu_cup` | `SPONSORED BY MIZUNO` / `KISARAZU CUP` / `Kisarazu Higashi CC - Hole 1 - Par 5` |
| S1 | after planting, the card reads | `EnteredFinished` / LEADERBOARD |
| A3 | LEADERBOARD on the entered+ended card (**the report**) | → `TournamentLeaderboard` |
| B1 | hole selection opened with it selected (the stale route) | next=0 locked=18 finished=0 |
| B2 | `BeginTournamentHole` on it (reflection) | refused: `Refusing to begin hole` logged, still on the screen, `GameSession.IsTournament=false` |
| B3 | hole selection CLOSE (wired `_closeButton`) | → `TournamentSelection` |
| C0–C3 | live entered `route_verify_live` (positive control) | CONTINUE → hole selection, next=1 locked=17, CLOSE → selection |
| Z | planted entries removed | 2/2, backend sees none |

Stills (real frames, md5 in the JSON): `A1_leaderboard_from_ended_card.png`,
`A3_leaderboard_from_finished_entered_card.png`, `B1_hole_selection_finished_tournament.png`,
`C1_hole_selection_live_tournament.png`; `route_verify_sheet.jpg` is the four side by side.

EditMode (17:34, full unfiltered run 3120 passed / 0 failed / 3 pre-existing skips of 3123, after F4): `ScreenIdSerializationTests` 14/14, `RemoteBackendAdoptTests` 3/3, `VenueClubNameTests` 5/5 (and the `_leaderboardTarget` site test proven to FAIL
on the HEAD prefab — *"stored 10 (TournamentHoleSelection), expected 11"*), `GolfinRedux.Tests.EditMode`
380/380, `Golfin.Tournaments.Tests` 251/251 (incl. 6 new `IsPlayable` cases).

## Found on the way

- **F1 — an empty tournament board had no CLOSE — FIXED (Cesar: "Fix F1", 2026-09-14 15:02).**
  `TournamentCloseButton` lived only at `Bottom97/ScrollArea/Viewport/GridContent/CloseSlot`, and
  `ApplyBoardChrome` deactivates the whole `ScrollArea` when `rankedCount == 0`, so every ENDED
  tournament with no finishers — exactly the boards the fixed LEADERBOARD button opens — could only
  be left through the nav bar. SPEC B5 places the CLOSE *"after the last ranked row"*; with no rows
  that is after the message. Fix: `TournamentLeaderboardEmptyState.prefab` ends with a `CloseSlot`
  (LayoutElement 144 = 48 gap + 96 button) holding a nested instance of the SAME
  `TournamentCloseButton.prefab` (308×96, bottom-centred, `japaneseFontScale 0.85` like the grid's
  instance); `TournamentLeaderboardScreenController.Awake` wires it to `Close()` by path
  (`EmptyCloseButtonPath`). Measured live on the empty board: Title y 850–894, Body 910–938, CLOSE
  x 431–739 / y 1002–1098 (canvas px), 64 px under the body, centred, inside Bottom97, top raycast
  hit = the button. Bot A2/A4 now PASS through that button; still `A1_leaderboard_from_ended_card.png`.
- **F1b — the cold-open shimmer never ended when the board fetch failed — FIXED with F1.** The three
  grey bars in the first A1 still were `Shimmer_tournament_leaderboard`: `RefreshRemoteBoard` only
  repainted on `changed == true`, and `PaintGate` is only spent by a Fetch paint, so a failed /
  deduped / unparseable fetch (offline, or a tournament the server no longer serves) left
  `answerStillComing` true forever. The callback now calls `EndBoardWait(Fetch, 0)` on
  `changed == false` — the file's own rule ("every arm which ENDS a wait has to say so"), applied to
  the failing arm. Bot check A1s: shimmer host inactive after the fetch settles.
- **F2 — a schedule applied after sign-in never reached the wrapper the session plays on — FIXED
  (Cesar: "Fix F2 and F3", 2026-09-14 17:34).** `TournamentService` builds one
  `RemoteTournamentBackend` per session and reuses it across schedule swaps on purpose (board
  snapshots, in-flight guards and the submit queue live in it), but `EnsureBackendForSession` did
  `_remoteBackend ??= new …` and the wrapper's `_local` was readonly, so after any `Apply()` that
  landed after sign-in (a live refetch, the disk cache arriving second) a signed-in player kept the
  previous definitions. Fix: `RemoteTournamentBackend.Adopt(local, prizeTables)` + `Local`, called
  by the service whenever the composed local backend changed. Tests: `RemoteBackendAdoptTests`
  (serves the new schedule; keeps the queue and the entries made before the swap; refuses null).
  Bot check F2: `schedule applied after sign-in: Backend (Remote) sees 4 of 4 defs` — the same
  probe that saw 3 of 4 and had to swap the field itself before.
- **F3 — the hole-selection header was authored text — FIXED with F2.** `TournamentHeaderPills`
  (`Assets/Scripts/UI/Tournaments/`) is the one binder for the identity pills both tournament
  screens carry: `Bind(root, sponsorPath, namePath, def, tag)` writes "SPONSORED BY {SPONSOR}" and the
  `TournamentDisplayName` ladder's name (so a dashboard-created tournament reads its title, not a raw
  key); the board's `BindHeader` delegates to it, the hole selection calls it on every rebuild. The
  hole cards read `{TournamentVenueLine.ClubName(def)} - Hole N - Par P` — the club half of the
  localized `tourn.venue.*` row (EN "Kisarazu Higashi CC", JP "木更津東カントリークラブ"), the id on
  the unlocalized fallback — instead of the template's "Lomond Country Club". Tests:
  `VenueClubNameTests` (EN, JP, fallback id, no-separator row, empty). Bot check F3 on the finished
  `kisarazu_cup`: `sponsor='SPONSORED BY MIZUNO', name='KISARAZU CUP', first card='Kisarazu Higashi
  CC - Hole 1 - Par 5'`; stills `B1_…png` / `C1_…png`.
- **F4 — ShellScene.unity was structurally malformed since `a231c1a78` (scroll-lists sweep) — FIXED
  on the way.** That commit's raw-YAML patch landed at stale offsets on the board's `Viewport`: its
  two new `- component:` lines went after `m_Icon` instead of into `m_Component:`, and its new
  Image + CanvasRenderer blocks were inserted *inside* GameObject `1679869180` (the `Text` under
  `UsernameInputField`), leaving that object's tail dangling off the CanvasRenderer. Unity
  self-healed on every open and logged *"Problem detected while opening the Scene file"*, which is
  why `PressFeedbackCoverageTests.EnumeratedTapTargets_HavePressFeedback` and
  `LoadingTipCatalogTests.ShellSceneCard_…` failed in the full EditMode run (3118/3123). Repaired by a
  12-line structural edit (the `Text` block is byte-identical to `d8fdab6ac` again), reloaded in the
  Editor without a save; both tests green.

## DONE — approved by Cesar 2026-09-14 ("Done")

Commits `12a9aadf9` (the route fix: ScreenId pinned, seven stored ids re-serialized, the hole
selection refuses finished tournaments), `a9c7b8ae3` (F1 + F1b), `f01240190` (F2 + F3 + F4),
`76d2a6f1d` (Lesson CK). Media stays in `Docs/Specs/Quick/media/finished_tournament_leaderboard_route/`.
