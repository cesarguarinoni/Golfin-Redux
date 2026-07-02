# ARCHITECT REVIEW — 1v1_result_rewards_display (Stage 1, iter-3)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-02 06:32 CEST
**Iteration:** Stage 1 iter-3 (iter-2 CESAR-flagged / red-team FAIL — 265 out-of-scope prefab-instance mutations; iter-3 is git-hygiene surgery only)
**Iteration shape:** `scene-hygiene:out-of-scope-prefab-drift`
**Verdict:** **READY_FOR_REDTEAM** (I do NOT write ARCHITECT_REVIEW_PASS; red-team is the sole PASS-gate.)

## Scope of this pass

Iter-3 is a **scene-hygiene fix ONLY.** Functional wiring, captures, fonts, and data
binding were accepted by red-team Attacks 1–4 at iter-2 and are unchanged. I did not
re-litigate them; the entire audit below is on the ShellScene diff.

Accountability: at iter-2 I PASSed with a "scene audit CLEAN" claim that was FALSE
(a single `m_IsActive: 0` grep, no anchor/pos/size check). The red-team then found
265 anchor mutations across 11 out-of-scope prefab instances. That miss is logged in
`.claude/review_misses.log`. This time I ran the actual scans.

---

## Measured scene-diff numbers

```
$ git diff HEAD --stat -- Assets/Scenes/ShellScene.unity
 Assets/Scenes/ShellScene.unity | 226 +++++++++++++++++++++++++++++++++++++++++
 1 file changed, 226 insertions(+)

$ git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -cE "^-[^-]"
0

$ git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -cE "^\+[^+]"
226
```

- **226 insertions, 0 deletions** — exactly as advertised, pure-additive diff.
- Only leading-`-` line in the raw diff is `--- a/Assets/Scenes/ShellScene.unity`
  (file header, not a content deletion).
- Contrast with iter-2's ~5,000-line / 2,152-del volume — the drift is gone.

**PASS.**

---

## Anchor / position / size audit (the check I skipped last time)

```
$ git diff HEAD -- Assets/Scenes/ShellScene.unity | \
    grep -cE "m_AnchorMin|m_AnchorMax|m_AnchoredPosition|m_SizeDelta|m_LocalPosition"
18
```

Every one of the 18 hits classified:

| Diff line(s) | Owner | Type | Verdict |
|---|---|---|---|
| 12 | Canvas parent RT context header (`m_AnchorMin: {x:0,y:0}`) | Unchanged context, not a modification | OK |
| 61 | New `VersusResultHandler` root Transform (fileID 970830638) `m_LocalPosition` | Own new object | OK |
| 68–71 | New `VersusResultModal` RT (fileID 562993541) anchors + `AnchoredPosition` + `SizeDelta` | Own new object | OK |
| 98–166 | 11 `propertyPath: m_Anchor*/m_SizeDelta/m_LocalPosition/…` overrides, all `target guid: 15774d8cc9178455d93d18d71e5d1721` | Prefab-instance override block for **this task's own `VersusResultScreen.prefab`** | OK |
| 235 | New `VersusResultHandler` Transform `m_LocalPosition` (secondary block for the SceneRoots-child GO) | Own new object | OK |

Zero of these anchor mutations target an out-of-scope prefab. `15774d8cc9178455d93d18d71e5d1721`
resolves to `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab` (this task's Stage-0 prefab).

**PASS.**

---

## Out-of-scope GUID / fileID grep

```
$ grep -c "8bf3740e"  # RankingsScreen
0
$ grep -c "2bd69f22"  # MatchMakingModal prefab GUID
0
$ grep "4390230621042469647"  # MatchMakingModal instance fileID
+  _matchmakingModal: {fileID: 4390230621042469647}   # ONE match, in the new SerializeField wiring block
```

Full unique-GUID census on the diff:

- `15774d8cc9178455d93d18d71e5d1721` → `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab` (this task).
- `9951fd443c897495dbee9e6c2f11f59c` → `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs` (new).
- `9a8472d5816b84ee792fe3327d171ec6` → `Assets/Scripts/UI/Modals/VersusResultHandler.cs` (this task).
- `908888c87ff584bb299438780daa2d9b` → `Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs` (this task).

All in-scope. MMModal fileID `4390230621042469647` appears exactly ONCE, on the
`_matchmakingModal:` SerializeField wiring line — a wiring **reference**, not a
`propertyPath:` mutation, i.e. exactly the integration seam SPEC §6 mandates.

None of RankingsScreen (`8bf3740e`), TournamentResultModal, TournamentSignupModal,
Tournament card/row/screen GUIDs appear anywhere in the diff.

**PASS.**

---

## Wiring survival + IsActive state

```
$ grep -E "_screen:|_matchmakingModal:|modalPanel:|_resultModal:" diff
+  modalPanel: {fileID: 571272057}
+  _screen: {fileID: 571272056}
+  _matchmakingModal: {fileID: 4390230621042469647}
+  _resultModal: {fileID: 562993540}
```

All 4 required wirings present with the fileIDs the coordinator called out. `m_IsActive`
lines in the diff:

- Line 33 (`+  m_IsActive: 1`) — new `VersusResultModal` root GO, active.
- Line 211 (`+  m_IsActive: 1`) — new `VersusResultHandler` root GO, active.
- Line 86–87 (`propertyPath: m_IsActive` → `value: 0`) — prefab-instance override on
  the VersusResultScreen prefab's inner GO, correctly starts hidden and is revealed by
  the modal controller (standard `ModalController` pattern).

Parent of the new `VersusResultModal` RT (`m_Father: {fileID: 1949345566}`) resolves
to the top-level scene `Canvas` GameObject (`m_Name: Canvas`, fileID 1949345562) —
correct parenting.

Additions to existing collections (the only two mutations of existing YAML):
1. `+  - {fileID: 562993541}` appended to Canvas's `m_Children` list — parent-child link
   for the new modal.
2. `+  - {fileID: 970830638}` appended to `SceneRoots` `m_Roots` list — new handler
   registered as a scene root.

Both are single-line list APPENDS on the objects the new GOs must attach to. No
existing entries were reordered, retargeted, or deleted.

**PASS.**

---

## Banned-path check

```
$ git diff HEAD --stat -- Assets/Scripts/Physics/ Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs 'Assets/**/M_Splash*.mat'
(empty)

$ git diff HEAD --stat -- Assets/
 Assets/Scenes/ShellScene.unity                          | 226 +++++++++++++++++++++
 .../UI/Matchmaking/VersusResultScreenController.cs      | 183 ++++++++++++++++-
 Assets/Scripts/UI/Modals/VersusResultHandler.cs         |  94 ++++-----
```

Zero edits to `Assets/Scripts/Physics/`, `Scenarios.cs`, or any `M_Splash*.mat`. Only the
three in-scope files under `Assets/` were touched.

**PASS.**

---

## Rule 13 (out-of-task uncommitted files reported)

Untracked / modified paths outside the task folder:

- `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs` (+`.meta`) — listed in
  `IMPLEMENTER_REPORT.md` "Files modified or created" table (lines 39–40, verified).
- `Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs` — listed.
- `Assets/Scripts/UI/Modals/VersusResultHandler.cs` — listed.
- `Packages/manifest.json`, `Packages/packages-lock.json` — MCP 0.82.2→0.82.3 environmental drift; noted in report as environmental (not this task's fault).
- `.claude/review_misses.log` — environmental (miss log append).

**PASS** on the code files. Env dirt (Packages + review_misses) flagged for close-out;
per the coordinator's carveout, not a fail condition.

---

## Scope carveouts (per coordinator, NOT reviewed here)

- Reward row is a Stage-2 placeholder — out of scope.
- "DIAMOND LEAGE" / "CANCOL" typos live in the reused MMModal, not this task — out of scope.
- Env dirt (`Packages/manifest.json`, `Packages/packages-lock.json`, `.claude/review_misses.log`) — noted for close-out, not blocking.

---

## Verdict

The scene diff is surgically clean. 226 insertions / 0 deletions; every anchor mutation
belongs to either the new `VersusResultModal` / `VersusResultHandler` GameObjects or the
prefab-instance override block for this task's own `VersusResultScreen.prefab`; zero
out-of-scope prefab GUIDs touched; MatchMakingModal referenced ONLY by the required
`_matchmakingModal` wiring line; banned paths untouched; wiring survived; Rule 13 satisfied.

The iter-2 blocker (out-of-scope prefab-instance drift) is genuinely resolved. This audit
was run with the actual anchor/pos/size grep that I failed to run at iter-2.

**STATUS → `READY_FOR_REDTEAM`** — handing to golfin-redteam-reviewer for the adversarial
gate. I do NOT write `ARCHITECT_REVIEW_PASS`.
