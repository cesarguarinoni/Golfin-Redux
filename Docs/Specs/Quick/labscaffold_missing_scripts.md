# Quick — `labscaffold_missing_scripts`

**DONE 2026-08-27.** Applied and verified in a healthy Editor after a machine reboot.

## Symptom

Two lines on **every hole load**, on device (13 of 13 runs of the Phase 1 pass):

```
The referenced script on this Behaviour (Game Object 'PuttPathRoot') is missing!
The referenced script on this Behaviour (Game Object 'LabRoot') is missing!
```

## Cause — exactly two orphans, both in `Assets/Scenes/Physics/LabScaffold.unity`

Not null `m_Script` fileIDs, which is why a naive `fileID: 0` grep finds nothing. They are script
references whose **GUID no longer resolves** because the `.cs` was deleted:

| GameObject | dead GUID | was |
|---|---|---|
| `PuttPathRoot` | `ccc48e5aae6235a4c95c81edcc035229` | `Golfin.Gameplay.UI.ShotUI.PuttPathRenderer` |
| `LabRoot` | `214108b3eee6815409554ccf16662fa5` | `Golfin.Physics.Viewer.PuttPathPredictor` |

Both are deleted putt-path classes. Every other MonoBehaviour GUID in the scene resolves.

## The fix, and the trap in applying it

`GameObjectUtility.RemoveMonoBehavioursWithMissingScript()` removes them correctly — but
**`EditorSceneManager.SaveScene` then bakes unrelated churn**: +133/−27 lines of TMP
`m_fontSize: 30 → 29.65`, `m_TextStyleHashCode`, and prefab anchor overrides. That is the known
scene-save churn scar, on a big shared scene.

The clean result is: run the Unity API removal, then **reverse-apply only the churn hunks**, leaving
the removal hunks. That yields **26 deletions, 0 insertions** — the two MonoBehaviour documents and
their two `- component:` entries, nothing else. The working patch is saved at
`Docs/Specs/Quick/media/labscaffold_missing_scripts.patch`.

## Verification already done on that patched file

Structural check — **PASS**:

| check | result |
|---|---|
| YAML documents / unique fileIDs | 1151 / 1151, no duplicates |
| dangling `- component:` refs | **0** |
| MonoBehaviours pointing at a missing GameObject | **0** |
| removed fileIDs referenced anywhere in `Assets/` | **0 files** |
| line count | 26758 → 26732 (−26, exactly the removal) |

## Applied and verified (2026-08-27)

The Editor was unavailable when this was diagnosed — editing the `.unity` on disk while Unity had it
open raised a blocking reload modal, and killing that Editor left an orphaned `gamedev-m` MCP server
holding port 21573 (socket answered, no Editor attached). After a machine reboot the Editor came up
clean and the patch was applied and checked properly:

| check | result |
|---|---|
| `git apply` of the saved patch | clean, 26 deletions / 0 insertions |
| scene opens in Unity | 22 roots, `isDirty=False` |
| **missing-script components** | **0** (was 2) |
| `LabRoot` | 15 components, 0 null refs |
| `PuttPathRoot` | 2 components, 0 null refs |
| console on load | no missing-script warnings |
| file after Unity opened it | still 26 deletions / 0 insertions |

Closed **without saving**, so the TMP/prefab save-churn was never re-introduced.

Note for anyone repeating this: opening `LabScaffold` triggers `PhysicsLabAutoRestore`, which
auto-loads the last hole (Hole 06 here) and runs `OnHoleLoaded` in edit mode. That is expected and
did not dirty the scene.
