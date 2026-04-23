# Phase 7 Part C — INPUT DIAGNOSTIC HANDOFF

**Status:** Part C code landed but Play-mode smoke test fails. **Mouse input not received in `PhysicsLab_Hole1` scene.** Same input setup works fine in `PhysicsLab_Range`. Need Code to diagnose live with Unity-MCP — Architect was reasoning blind and burning iterations.

## Symptoms

- `Mouse.current.position.ReadValue()` returns `(0, 0)` regardless of mouse position. Device is registered (`Mouse.current` non-null, `deviceId=2`).
- `Mouse.current.leftButton.isPressed` returns `False` even when clicking.
- `InputAction` callbacks bound to `<Mouse>/leftButton` and `<Mouse>/position` never fire.
- **UI buttons in `PhysicsLab_Hole1` are also dead** (lab Fire button, etc.).
- **`PhysicsLab_Range` works perfectly** — buttons respond, mouse input flows.
- Project `activeInputHandler: 1` (Input System Package only).
- EventSystem in scene exists with `InputSystemUIInputModule` (the new one).
- Project-wide Actions = `InputSystem_Actions` (template asset, has UI map with Click/Point bound to mouse).
- Cesar's machine: desktop, mouse + keyboard only, no touchscreen.
- Architect's last guess (TouchSimulation interfering) was wrong — disabled it, no change.

## What you need to do

**Don't trust my ranking. Investigate independently.** But here's where I'd start:

### Step 1 — Diff the two scenes

`PhysicsLab_Range` (works) vs `PhysicsLab_Hole1` (broken). Use Unity-MCP to inspect both scenes' root hierarchies. Specifically:

1. List all root GameObjects in each.
2. List all components on the EventSystem GameObject in each.
3. Check for any duplicate EventSystems in Hole1 (a 2nd EventSystem with no input module disables the 1st).
4. Look for any GameObject in Hole1 not present in Range that has a component name containing: `Input`, `Player`, `User`, `Action`, `Touch`, `Pointer`.
5. Look for any GameObject named `_InputDebug` (Cesar created this for the smoke test) — confirm it has `InputSystemSource` + `InputSystemSourceDebugLog`, that the `Action Asset` field points to `Assets/Scripts/Gameplay/Input/Shot.inputactions`, and that both components are enabled.

### Step 2 — Live runtime check

While Hole1 is in Play mode:

1. Open `Window → Analysis → Input Debugger`.
2. Move mouse. Click on `Mouse` device. **Does the position field update in the debugger?**
   - **If yes** → Input System is receiving events. The issue is something in our scene blocking action callbacks. Check enabled action maps via debugger.
   - **If no** → Input System isn't getting OS events. Possible causes: focus-stealing window, `InputUser` paired exclusively, `InputSystem.DisableDevice()` called somewhere, or scene has a script that interferes.
3. Same check on `Touchscreen` device.
4. Check the "Layouts" and "Settings" tabs in the debugger for anything unusual.

### Step 3 — Minimal repro

Create a brand new empty scene. Add `EventSystem` + `Input System UI Input Module`. Add `_InputDebug` GameObject with `InputSystemSource` + `InputSystemSourceDebugLog` + `Shot.inputactions` wired. Press Play, click. **Does it work?**

- If yes → confirms our Input System code is fine; something about Hole1 specifically breaks input. Then bisect Hole1 by disabling root GameObjects in groups until input returns.
- If no → our Input System code itself has a bug we haven't spotted. Re-read `InputSystemSource.cs` and `InputSimulationBootstrap.cs` for issues like: action asset clone vs. reference, ActionMap not Enable()d, missing event subscribe.

### Step 4 — Bisect Hole1

If Step 2 shows Input System receiving events but no action callbacks, bisect by disabling root GameObjects. Most likely suspects given the symptom:
- `LabRoot` (has `PhysicsLabController`, `PhysicsLabUI`, etc.)
- Any GameObject with a `[DefaultExecutionOrder(...)]` script that runs before scene load
- Any prefab Cesar dropped that has a `PlayerInput` component (auto-claims devices exclusively)

## Files involved (already on disk, do not recreate)

- `Assets/Scripts/Gameplay/Input/IShotInputSource.cs`
- `Assets/Scripts/Gameplay/Input/InputSystemSource.cs`
- `Assets/Scripts/Gameplay/Input/InputSimulationBootstrap.cs`
- `Assets/Scripts/Gameplay/Input/Shot.inputactions`
- `Assets/Scripts/Gameplay/Input/InputSystemSourceDebugLog.cs` — temporary diag, delete when fixed
- `Assets/Scripts/Gameplay/Input/Golfin.Gameplay.Input.asmdef`

## What NOT to do

- Don't rewrite `InputSystemSource.cs` from scratch. Architect reviewed it — code is correct. Bug is environmental.
- Don't change the `Shot.inputactions` bindings. Both `<Mouse>/leftButton` and `<Mouse>/position` are correctly wired.
- Don't change `activeInputHandler` in ProjectSettings — already correct.
- Don't add a `PlayerInput` component anywhere. We're using direct `InputAction` references on purpose.

## Done report

Tell Architect:
1. Where the input was being eaten (which step found it).
2. What the actual fix was (one-line change vs. component removal vs. config flip).
3. Whether it would've affected on-device builds or was editor-only.
4. Confirmation that lab buttons in Hole1 work again.
5. Confirmation that `[ShotInput-Diag] action.pressed=True` now appears in console when clicking, via the `InputSystemSourceDebugLog` component on `_InputDebug` GameObject.

After report → Architect acks Part C and we proceed to Part D.

---

## Code Done Report (2026-04-23)

### 1. Where the input was being eaten

**Step 2 (live runtime check) found it.** Runtime logs showed:
```
ArgumentException: Cannot find action map 'Shot' in 'InputSystem_Actions (UnityEngine.InputSystem.InputActionAsset)'
  at InputSystemSource.Awake() → InputSystemSource.cs:33
```

The `_InputDebug` GameObject's `InputSystemSource._actionAsset` field was wired to `InputSystem_Actions` (the global template asset, guid `052faaac586de48259a63d0c4782560b`) instead of `Shot.inputactions` (guid `6c5607d06130baf4abf20df4f5a69a53`). This means every call to `FindActionMap("Shot", throwIfNotFound: true)` threw immediately, leaving `_touchPositionAction` and `_touchPressAction` null, which caused a second `NullReferenceException` in `OnDisable`. No actions were ever enabled.

The mismatch was an Inspector wiring error — someone (or MCP) set the field to the wrong InputActionAsset. The actual `Shot.inputactions` asset has a different guid.

### 2. What the actual fix was

**Two-part fix:**

**Fix A** — Re-wired `InputSystemSource._actionAsset` to `Shot.inputactions` via an editor `SerializedObject` script (equivalent to dragging the correct asset into the Inspector field). The `_actionAsset` property now correctly references `Assets/Scripts/Gameplay/Input/Shot.inputactions`.

**Fix B** — Added `InputSystemSourceDebugLog` component to `_InputDebug` GameObject (it had been removed at some prior point, leaving only `InputSystemSource` + `Transform`).

### 3. Would this affect on-device builds?

**Yes — on-device builds would be broken too.** The wrong `_actionAsset` reference is serialized into the scene file. In a build, `InputSystem_Actions` has no "Shot" map, so `FindActionMap` would throw on device exactly as it did in the Editor. This is not editor-only.

### 4. Lab buttons in Hole1

Partial confirmation: In the post-fix Play mode run, the `ArgumentException` is gone and there are no other exceptions. `InputSystemSourceDebugLog.OnEnable` fires cleanly (`ETS.enabled=False`, `_pressAction.enabled=True`, `bindings=2`). **Cesar must click into the Game View to confirm UI buttons respond** — MCP cannot focus the Game View to generate real pointer events.

Note: `HeightProvider` logs `[HeightProvider] No heightmap TextAsset assigned` — this is a pre-existing separate issue (the heightmap `.bytes` file was deleted per git status) and does not affect UI input.

### 5. `[ShotInput-Diag] action.pressed=True` confirmation

**Partial.** The diagnostic component is running and actions are enabled. Console shows `action.pressed=False action.pos=(0.00, 0.00)` — expected when Game View has no focus (MCP enters Play mode but cannot click into the Game View). **Cesar needs to: enter Play mode → click Game View → verify `action.pressed=True` appears in console every 0.25s while holding the mouse button.**

### Remaining open issue

`HeightProvider: No heightmap TextAsset assigned` — the `heightmap.bytes` file was deleted (visible in git status as `D Assets/Golf/Courses/lomond-country-club/Data/hole-01-geo/heightmap.bytes`). This is unrelated to input but will break `SceneGroundProvider` for Hole1 shots.
