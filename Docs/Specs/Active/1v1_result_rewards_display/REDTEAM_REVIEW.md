# RED-TEAM REVIEW — 1v1_result_rewards_display (Stage 1, iter-3)

**Red-team reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-07-02 06:26 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**
**Iteration shape:** `scene-hygiene:out-of-scope-prefab-drift`

---

## TL;DR

My iter-2 blocker is genuinely dead, measured by me. iter-3 restored ShellScene to
a surgically clean **226 insertions / 0 deletions** diff (was 5,078 lines / 2,152 dels
at iter-2 with 265 out-of-scope anchor mutations). Every one of the 18 anchor/pos/size
hits belongs to either the two new `VersusResultModal` / `VersusResultHandler`
GameObjects or the prefab-instance override block for THIS task's own
`VersusResultScreen.prefab` (guid `15774d8cc9178455d93d18d71e5d1721`, resolved from
`.prefab.meta`). Zero out-of-scope prefab GUIDs touched (all 10 = 0). MatchMakingModal
appears exactly ONCE, as the required `_matchmakingModal` SerializeField wiring line —
not a mutation. Functional flow (Attacks 1–4, accepted iter-2) did not regress from the
surgery. This advances to Cesar.

---

## Attack the fix — my own measurements

### 1. Diff volume (blocker was scene bloat)
```
$ git diff HEAD --stat -- Assets/Scenes/ShellScene.unity
 Assets/Scenes/ShellScene.unity | 226 +++++++++++++++++++++++++++++++++++++++++
 1 file changed, 226 insertions(+)
$ git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -cE "^-[^-]"   # deletions
0
$ git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -cE "^\+[^+]"  # insertions
226
```
**226 ins / 0 del** — pure additive, exactly as advertised. iter-2's 2,152 deletions are
gone. **GONE.**

### 2. Every anchor/pos/size hit classified (18 total)
```
$ git diff HEAD -- Assets/Scenes/ShellScene.unity | \
    grep -cE "m_AnchorMin|m_AnchorMax|m_AnchoredPosition|m_SizeDelta|m_LocalPosition"
18
```
I read the FULL 226-line diff. The 18 hits break down as:
- New `VersusResultModal` RT (fileID 562993541): `m_AnchorMin/Max`, `m_AnchoredPosition`,
  `m_SizeDelta`, `m_LocalPosition` — own new object.
- New `VersusResultHandler` Transform (fileID 970830638): `m_LocalPosition` — own new object.
- 11 `propertyPath: m_Anchor*/m_SizeDelta/m_LocalPosition/m_AnchoredPosition` entries in the
  PrefabInstance modification block — **every one targets `guid: 15774d8cc9178455d93d18d71e5d1721`**
  = this task's own `VersusResultScreen.prefab` (confirmed:
  `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab.meta` → `guid: 15774d8c…`).

**Not a single anchor mutation touches an out-of-scope prefab. GONE.**

### 3. Out-of-scope prefab GUIDs — all zero
```
8bf3740e (RankingsScreen) : 0      2bd69f22 (MatchMakingModal): 0
08bcfc9e : 0   8041c091 : 0   2bb7999c : 0   9aa7bc30 : 0
0ec50b3d : 0   93756886 : 0   1ce887a2 : 0   c0f78052 : 0   (Tournament ×8)
```
The ONLY guids anywhere in the added diff are:
`15774d8cc9178455d93d18d71e5d1721` (this task's prefab) + the 3 this-task script guids
(`908888c8…` VersusResultScreenController, `9951fd44…` VersusResultModalController,
`9a8472d5…` VersusResultHandler — each resolved to its `.cs.meta` under
`Assets/Scripts/UI/`). **All in scope.**

MMModal instance fileID `4390230621042469647` occurs exactly ONCE in the diff:
```
+  _matchmakingModal: {fileID: 4390230621042469647}
```
A SerializeField **reference**, not a `propertyPath:` mutation — the integration seam
SPEC §6 mandates. That fileID is a real pre-existing MMModal MonoBehaviour at HEAD
(scene line 127433, already referenced by `matchmakingModal1v1` elsewhere). **GONE.**

### 4. No over-revert / no dropped wiring
- Deletion-side content lines = 0 → it added the intended delta onto a clean HEAD scene,
  didn't strip legit HEAD state.
- All 4 required wirings present:
  `_screen: {571272056}`, `_matchmakingModal: {4390230621042469647}`,
  `modalPanel: {571272057}`, `_resultModal: {562993540}`.
- Both new root GOs `m_IsActive: 1`; VersusResultScreen prefab instance override
  `propertyPath: m_IsActive → value: 0` (correctly hidden until `ModalController` reveals it).
- New modal parented to RT `1949345566`, which is the RectTransform component of GO
  `1949345562` = `m_Name: Canvas` (top-level scene Canvas). Correct parenting.
- Only two existing-collection mutations, both pure APPENDS (no reorder/remove):
  `+ - {fileID: 562993541}` → Canvas `m_Children`; `+ - {fileID: 970830638}` → SceneRoots.

### 5. No functional regression from the surgery
- `VersusResultScreen.prefab` is ABSENT from `git status` → byte-identical to the
  Cesar-approved Stage-0 output. Fonts/layout unchanged.
- `VersusResultHandler.cs` still `GameSession.OnMatchComplete += HandleMatchComplete`
  (line 39, OnEnable) / `-=` (line 48, OnDisable). Flow: real event → `ShowResultAfterBanner`
  → live `MatchContext.Players[0/1]` + `GameSession.CurrentHoleNumber` → `_resultModal.ShowResult`.
- `.cs` diffs are exactly the accepted iter-2 flow-swap (drop auto-home, show modal). No
  synthetic entry, no `LoadSceneAsync("LabScaffold")`, no `*Gate`. Scenarios.cs / Physics /
  M_Splash*.mat untouched (banned-path grep empty).

### 6. Report integrity
Every PASS above is backed by my own tool output pasted here. golfin-reviewer's iter-3
numbers (226/0, 18 hits, guid census) match mine exactly. No fabrication.

---

## Prior-rejection replay
- **CESAR_REJECTION #3 (Stage-0, RANK→separator 24px):** resolved at Stage-0 iter-11 in the
  byte-identical prefab (clean, absent from git status). **GONE / not regressed.**
- **"Keep intact: scene-safety (Physics/Scenes/MMModal untouched)":** iter-2 regressed this
  with 265 mutations (MMModal among them). iter-3 restores it — MMModal guid `2bd69f22` = 0
  hits, MMModal referenced only via the required wiring line. **GONE.**
- **My own iter-2 blocker (265 out-of-scope anchor mutations across 11 prefabs):** **GONE.**

---

## Three break-attempts (all failed)
1. **Removals/reorders hiding in the diff?** 0 deletion lines; only 3 hunks; the two
   existing-collection changes are appends. No entry removed or reordered. *Failed to break.*
2. **Wrong parent / orphaned modal?** Parent RT resolves to the real top-level Canvas GO.
   *Failed to break.*
3. **Over-revert dropping intended wiring, or a stray out-of-scope hit past the grep?** All 4
   wirings present; full 226-line read shows every line inside the 3 surgical hunks; every
   guid in scope. *Failed to break.*

---

## Carveouts (noted for close-out, NOT fail conditions)
`Packages/manifest.json` + `packages-lock.json` (MCP 0.82.2→0.82.3) and
`.claude/review_misses.log` are environmental dirt. Reward row = Stage-2 placeholder.
"DIAMOND LEAGE"/"CANCOL" typos live in the reused MMModal (not this task).

---

## Verdict
The iter-2 scene-pollution blocker is genuinely, measurably dead. The scene diff is
surgically clean (226/0), every mutation in scope, all wiring survived, no functional
regression. I tried three ways to break it and could not.

**STATUS → `ARCHITECT_REVIEW_PASS`** for Cesar's final approval.
