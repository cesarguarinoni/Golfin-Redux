# Quick — `duplicate_eventsystem_fix`

**Done 2026-08-26.** Small fix, no subagent chain.

## Symptom

Unity logs **"There can be only one active Event System."** on **every hole load** — 13 out of 13
runs of the Phase 1 device pass (build 2316), once each. Found while bisecting per-frame GC.

## Cause

`ShellScene` and `LabScaffold` each ship their own `EventSystem`. From the moment LabScaffold loads
there are two active, and **LabScaffold's won `EventSystem.current`** simply by registering last.

Both carry an enabled `InputSystemUIInputModule` bound to the same input actions. Unity keeps one as
`current` and the loser's `Update()` early-returns, so the frame cost is small — measured at
−2,730 B/frame with both disabled, inside the run-to-run noise of a 77 KB Editor baseline. **The
reason to fix it is correctness, not performance:** which module owns input was load-order luck, and
LabScaffold's is the wrong one to own it because LabScaffold is unloaded on every hole exit — so
`EventSystem.current` was being destroyed on every exit.

## Fix

`PhysicsLabController.ReconcileEventSystems()`, called at hole load beside `DisableShellCamera()`,
and restored from **both** `OnDestroy()` and `OnHoleUnloaded()` — the same pattern, and the same
lifecycle, already proven on device for the shell camera and light.

Rules: keep the **ShellScene** one (it is persistent; an EventSystem is global, not per-scene, so it
drives LabScaffold's canvases fine), disable the other, hold it for restore. **No-op when there is
only one** — the standalone lab-rig path has no ShellScene and must keep its own.

## Verification (Editor, Hole 08, production entry path)

| state | EventSystems | detail |
|---|---|---|
| in hole | 2 present | LabScaffold `enabled=False`; **ShellScene `enabled=True`, `isCurrent=True`** |
| after `UnloadGameplayScenes()` | 1 | ShellScene's, enabled and current — shell input intact |
| hole reloaded | 2 present | reconciles again correctly |

## Known limitation

**The warning line itself will still appear once per hole load.** Unity logs it from
`UIElementsRuntimeUtility.RegisterEventSystem` when the second EventSystem *registers*, which happens
as LabScaffold's scene loads — before `OnHoleLoaded` runs. The duplicate is resolved a moment later;
the log line is not. Silencing it too would mean shipping LabScaffold's EventSystem disabled in the
scene asset, which would break the standalone lab rig. Not worth it.

## Filed separately, not fixed here

The same device log region shows two orphaned missing-script components in LabScaffold:

```
The referenced script on this Behaviour (Game Object 'PuttPathRoot') is missing!
The referenced script on this Behaviour (Game Object 'LabRoot') is missing!
```

That is the known deleted-MonoBehaviour-orphan pattern; the component has to be removed via the
Unity API, not by hand-editing scene YAML. Own task.
