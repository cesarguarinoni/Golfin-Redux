# Lessons Learned

## A SPEC clause that contradicts an existing working asset Cesar points to — the asset wins

**Symptom (loop_v2_c1_result_modal, 2026-05-21):** The C1 SPEC said the Result modal should be a single card ("no Card 2 stacking") and the kickoff prompt repeated it. But the working lab `HoleCompleteWidget` Cesar referenced as "the design that already worked" is a TWO-card widget (Card 1 current hole + Card 2 next hole, with a Locked state). I followed the SPEC text and stripped Card 2 — twice. Iterations 4 and 5 both shipped a single-card modal; iter-5 even passed architect review. Cesar rejected it: *"The whole design in LabScaffold was correct. I'm not sure why it was changed."* Eight iterations total; the first five were spent before the design was even correct.

**Root cause:** I treated the SPEC text as authoritative over an observable, working, Cesar-endorsed asset. When Cesar said "reuse the lab widget" (the iteration-4 interrupt), I STILL let the SPEC's "one card" clause override what the lab widget actually IS. I even asked a clarifying question ("lab body vs spec shot-history") whose two options BOTH silently assumed a single card — so Cesar's terse answer ("1") inherited the wrong frame.

**Rules:**
1. When the SPEC and a concrete existing asset disagree, and Cesar points at the asset, the asset is the spec. Surface the contradiction explicitly ("SPEC §X says one card; the lab widget you referenced has two — which governs?") instead of silently following the SPEC text.
2. A terse answer ("1", "go", "yes") is only as correct as the question's premises. Before asking, audit your own options for a buried wrong assumption. If you later get redirected, re-examine the question you asked — not just the answer.
3. "Reuse the working asset" means reuse its STRUCTURE, not just its art tokens. Don't reinterpret "reuse" as "rebuild a subset."

## Architect investigates root causes BEFORE re-running implementer on a FAIL

**Symptom (loop_v1_2e_next_shot_handoff, 2026-05-13):** Self-reviewer iter-1 FAILed two procedural items — S2 visually unconvincing ("uniform dark brown around the ball — looks like OOB") and S3 byte-identical to S2. Reflex would be to hand the implementer a generic "redo the captures" and let them figure it out. Cesar's prompt was sharper: **"Go, but first understand why the visual evidence turned out wrong."** That instruction inverted the failure mode.

**What digging revealed (in ~5 min of code reading, before re-running):**
- S3 duplicate: two `CaptureCore.SnapPlayModeSafe` calls back-to-back in the same coroutine frame at `SmokeRunner2eHost.cs:280-285`, no `yield return null` between them. Pure sequencing bug, trivial to fix once spotted.
- S2 visual: `LoopCameraDirector.ModeMap[BallState.Aiming] = null` ("leave whatever was set") means the Director never promotes back to Chase after OB→Aiming. The SPEC § Architecture context's claim that "Director already returns Chase on OB→Aiming" was **factually wrong**. Compounded by Hole_06's drop-zone terrain rendering dark even when it's classified as Rough — making the resolver's behavior look broken to a reviewer even though it was correct.

**Why this matters:** without the root-cause dig, the implementer would have spent another full iteration churning on scenarios + camera reframes blindly, possibly landing wrong fixes. With the dig, the prompt I handed back named the actual mechanisms ("smoke runner needs yield + reframe", "Director mode-map gap is real but out-of-scope per L7 — use Chase-mode override in smoke runner only") and the implementer landed iter-2 in one shot.

**Rule:** When a pipeline FAIL surfaces a fact that *might* be a deeper bug ("the camera's wrong", "the surface looks wrong", "the value's wrong"), the architect (Claude.ai chat) does a read-only root-cause pass — `grep`, `Read`, `git log` — and writes the re-run prompt with named mechanisms, not vague directives. Don't delegate understanding to the implementer when the architect has the broader context. The implementer prompt should answer "what's actually broken and which file/line/concept" before "what to do about it."

**Counter-rule (don't over-apply):** if the FAIL is purely cosmetic ("text is the wrong color"), skip the dig and just route it back. The architect-investigates pattern is for FAILs where the surfaced symptom contradicts the spec's stated mechanism — that's a signal there's a gap between spec assumption and runtime reality, and the implementer alone can't reconcile it.

**Bonus side-effect:** the dig surfaces real bugs that *are* out-of-scope for the current task but worth filing as backlog. From this session: `LoopCameraDirector.ModeMap[Aiming] = null` is a follow-up Director ticket; queued at `Docs/Specs/Queued/director_obfreeze_to_chase_on_aiming/`.

## "Functionally working" is not "matches the reference" — stop conflating them

**Symptom (hole_selection_screen, 2026-05-03):** After 5 iterations of pipeline work, the screen worked end-to-end (filters + cards + expand/collapse + PLAY → matchmaking modal), but Cesar's response was "looks nothing like the reference. I will fix it myself." Five rounds of "skeleton first, polish later" never converged on the polish.

**What I actually did wrong:**
- Treated matching the Figma TOKENS (gradient stops, corner radius, font sizes) as visual fidelity. It's not. Visual fidelity is the COMPOSITION — the Tutorial frame split (map LEFT spanning 749×288, description RIGHT — not a small thumbnail in the corner), the structural filter containers (rounded backgrounds + dividers + proper spacing, not text floating over a scenic backdrop), button proportions with sheen overlay, title typography hierarchy.
- When Cesar pushed back, added the canonical sprites (Arrow / Lock / Button-Play / Button-Replay / Background) but kept the procedurally-built layout. Wrong move. The layout itself is the gap.
- Repeatedly said "good enough for skeleton, polish later in architect-review" — but architect-review can't redesign a layout, only review one. Polish never happened, and the architect kept passing because I framed it as "follow-up nits" instead of "the layout doesn't match."

**Rule:** When implementing a UI task with a Figma reference, before declaring done:
1. Open both screenshots side-by-side and ask "would Cesar's designer eye see these as the same screen?" If the answer is no, it's not done.
2. If a Figma layout requires asset/RectTransform/structural changes that I can do via prefab YAML or Unity MCP, do them — don't ship a procedural approximation.
3. If a layout requires designer judgment I can't replicate (typography hierarchy, spacing rhythm, depth/shadow treatment), be honest in the report: "implementer reached functional parity, visual fidelity gap remains — Cesar to author the final layout in the Editor."
4. Never frame visual misses as "polish nits" if they're structural (image dimensions, container layouts, button proportions). Those are the layout itself.

**Pattern recognition:** if I find myself adding 3+ "this is borderline OK, will polish later" caveats, the iteration isn't done — call it FAIL on my side and re-attempt the layout, or hand off to Cesar explicitly with a "this is past my visual-fidelity ceiling" note.

## ActionButtonsBuilder regeneration WIPES Cesar's manual button configs

**Symptom:** Every time `ActionButtonsBuilder.BuildActionButtons()` is run, it destroys and recreates the button GameObjects, losing any manual changes Cesar made in the Inspector (text width, font style, auto-size, icon sprites, IconArea size).

**Rule:** Before regenerating buttons, check the current scene config. If values differ from the builder defaults, update the builder constants FIRST. The authoritative config snapshot is in the comment block at the top of `BuildActionButtons()`.

**Current authoritative values (as of 2026-04-30):**
- `IconArea` width = **135** (not 180)
- Text field width = **120** (not 0/stretch)
- `fontStyle` = **Bold**
- `enableAutoSizing` = true, min = 20, max = 30 (not hardcoded 30)
- `GolfinButton` icon = `S_Controls_Ball_GOLFIN.png`
- `DriverButton` icon = `S_Menu_Driver_GOLFIN.png`

**Fix applied:** Updated all `BuildButton()` and `BuildCardPrefabGo()` calls in `ActionButtonsBuilder.cs` to use these values. The scene was also patched in-place via script-execute (no regeneration needed).

**Critical Unity layout rule:** A fixed `sizeDelta.x` (e.g. width=120) is IGNORED when `anchorMin.x != anchorMax.x` (stretch anchor). To get a fixed pixel width, you MUST use a non-stretch anchor — e.g. `anchorMin = anchorMax = new Vector2(0.5f, y)` (Middle/Bottom Center). Cesar had to manually change the anchor from stretch to Middle Center to make width=120 work. All text fields in the builder now use center anchors.


## Unity Editor screenshots — `ScreenCapture.CaptureScreenshotAsTexture()` reads the OS swap chain, not the GameView

**Symptom (Phase 8 capture_helper task):** First implementation of `CaptureHelper.SnapGameView()` called `ScreenCapture.CaptureScreenshotAsTexture()` and wrote the resulting bytes as a PNG. In editor mode the result was either solid black, or showed the Editor chrome (Hierarchy / Inspector panels) instead of the Game View contents.

**Root cause:** `ScreenCapture.CaptureScreenshotAsTexture()` reads from the OS display's current swap chain frame, not from the Game View's render target. When the Editor is the focused window, that swap chain contains the Editor UI — not the GameView's RT. There is no Unity public API that returns the GameView RT directly.

**Compounding constraint:** the more obvious-looking `ScreenCapture.CaptureScreenshot(path)` (the file-writing variant) is async — it queues the write for the next end-of-frame. While the editor is paused, `WaitForEndOfFrame` never fires, so the capture silently never completes. Pause-then-capture yields nothing. Confirmed Unity bug, multiple issuetracker entries, no fix in user code.

**Fix:** Reflect into `UnityEditor.GameView`'s internal `RenderTexture` field, then `ReadPixels` into a `Texture2D`. Field name varies across Unity versions — try `m_RenderTexture` / `m_TargetTexture` / `m_RenderTarget` and use whichever resolves. Also Y-flip the result — `ReadPixels` returns OpenGL-coordinate-space data (origin bottom-left), so the PNG comes out upside-down without an explicit flip pass.

Code skeleton:
```csharp
var gvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gv = EditorWindow.GetWindow(gvType, false, null, false);
gv.Repaint();
foreach (var name in new[] { "m_RenderTexture", "m_TargetTexture", "m_RenderTarget" })
{
    var rt = gvType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                  ?.GetValue(gv) as RenderTexture;
    if (rt != null && rt.IsCreated()) { /* ReadPixels + Y-flip + EncodeToPNG */ break; }
}
```

**How to apply:** Use `CaptureHelper.SnapGameView()` for all editor-side captures. `ScreenCapture.CaptureScreenshot(path)` is banned project-wide (per `CLAUDE.md` § Screenshots and `RUNTIME_BLUEPRINT.md` §10). `ScreenCapture.CaptureScreenshotAsTexture()` is acceptable only as the internal fallback inside `CaptureHelper` for the case where reflection fails (future Unity field renames) — with a `Debug.LogWarning` so we notice.

**Bonus lesson:** the `WaitForEndOfFrame`-doesn't-fire-while-paused issue means **always capture FIRST, pause AFTER**, never the other way around. `CaptureHelper.SnapAtEndOfFrameAndPause(label)` does this in the right order.

---

## Unity asmdef — Cannot reference Assembly-CSharp from a named asmdef (build order)

**Symptom (Phase 8.3):** Added `"Assembly-CSharp"` to `Golfin.Gameplay.UI.asmdef` references so `PlayerCardWidget` could call `CharacterManager`/`CharacterDatabaseCSV`. Unity/Bee silently dropped the reference from the generated RSP and the DLL failed to build with no visible error. TundraBuildState updated but DLL timestamp stayed from the previous day.

**Root cause:** Unity's Bee build system compiles named asmdefs BEFORE Assembly-CSharp. Adding Assembly-CSharp as a reference creates an impossible build-order dependency (Assembly-CSharp must exist before the asmdef can compile, but Assembly-CSharp is compiled after). Bee detects this and silently drops the reference, leaving the asmdef with unresolved types → build failure with no console output.

**Fix:** Two-part:
1. Remove `"Assembly-CSharp"` from the asmdef references entirely.
2. Set `autoReferenced: true` on the asmdef — then Assembly-CSharp (and Assembly-CSharp-Editor) auto-reference your asmdef, so editor wire scripts in Assembly-CSharp-Editor can use the types.

**For cross-boundary data (e.g. CharacterManager):** Use a static bus pattern (like `HoleContext`) that lives in the asmdef and gets populated by Assembly-CSharp code. Don't reference Assembly-CSharp types directly from a named asmdef.

**How to apply:** If a named asmdef needs data from Assembly-CSharp types, create a static data class in the asmdef (e.g. `PlayerContext`) and have Assembly-CSharp code populate it. The UI reads from the static; the manager writes to it. Zero circular deps.

---

## Workflow — Before deleting a .cs file, grep for ALL public types it defines

**Symptom:** Phase F.4 deleted `SceneSurfaceProvider.cs`. The file's primary type was `SceneSurfaceProvider`, but it ALSO defined `Physics.Runtime.SurfaceMarker` inline as a second `public sealed class` in the same namespace. Spec hard rule 5 explicitly retained `SurfaceMarker` (load-bearing for the import → bake bridge) — but I read the spec as "keep this type" without verifying where the type actually lived. Deleting the file silently took the marker with it. Result: CS0234 across all importers; entire main-repo project failed to compile until I extracted `SurfaceMarker` to its own file.

**Rule:** Before `rm`-ing a `.cs` file, run a grep for `^\s*(public|internal|sealed|abstract|static)?\s*(class|struct|enum|interface|record)\s+\w+` over the file. If it defines more than one top-level type, treat each as a separate decision: which are kept, which are deleted, and where do the kept ones move to. Co-located types are common in legacy files.

**Why:** `Glob`/`Grep` for the type *name* against the project finds usages but not the *definition*. The definition lives where you don't expect it. A spec saying "keep type X, delete file Y" is silently wrong if X is defined inside Y.

**How to apply:** Anywhere a spec lists files to delete and types to retain, before deleting:

1. `grep -nE "^\s*(public|internal|sealed|abstract|static)?\s*(class|struct|enum|interface|record)\s+\w+" <file>` to enumerate every type it defines.
2. Cross-check each enumerated type against the spec's "retain" list and the codebase's references.
3. If any retained type lives inside the to-be-deleted file, extract it first into its own file (in the same namespace), commit that move separately, then delete the original.

---

## NUnit `Assert.AreNotEqual` has no delta-tolerance overload

**Symptom (`controls_i_ball_visual_rotation`, 2026-05-12):** Compiler error CS1503 — `Assert.AreNotEqual(0f, angle, 1e-4f, "message")` fails because argument 3 is `float`, not `string`. Only `Assert.AreEqual` has a `(expected, actual, delta, message)` overload; `AreNotEqual` does not.

**Fix:** Use `Assert.Greater(Mathf.Abs(value), threshold, "message")` to assert a float is meaningfully non-zero.

**How to apply:** Any time a test wants "value is not approximately zero", use `Assert.Greater(Mathf.Abs(x), epsilon)`, not `Assert.AreNotEqual(0f, x, epsilon)`.

---

## Ball visual rotation — cross product order determines spin direction

**Symptom (`controls_i_ball_visual_rotation`, 2026-05-12):** `Vector3.Cross(delta / deltaMag, Vector3.up)` produces a backspin-looking rotation — logo on the ball descends as the ball moves forward. The correct forward-rolling appearance requires the swapped order.

**Rule:**
- `Cross(delta_normalized, Vector3.up)` → axis produces **backspin appearance** (logo goes down as ball moves forward)
- `Cross(Vector3.up, delta_normalized)` → axis produces **forward-roll appearance** (logo rises as ball moves forward)

**How to apply:** For any position-delta–derived rotation where the intent is "ball rolling forward", always use `Cross(Vector3.up, delta_normalized)`.

---

## Subagent — stop Unity Play Mode via MCP rather than blocking

**Symptom (`controls_i_ball_visual_rotation`, 2026-05-12):** Implementer declared `IMPLEMENTER_BLOCKED` because Unity was in Play Mode, preventing test execution and screenshots. Unity MCP provides tools to stop play mode directly.

**Rule:** Before declaring `IMPLEMENTER_BLOCKED` for a locked Unity editor, attempt to stop Play Mode via Unity MCP (`editor-application-set-state` or equivalent). Only escalate to BLOCKED if the MCP call itself fails.

---

## Physics — IGroundProvider and Zone Mesh Height (PhysicsLab Hole1)

### HeightmapData only knows terrain — use SceneGroundProvider for scene with zone meshes
`HeightmapData.SampleHeight` returns the baked terrain heightmap Y value. Zone overlay meshes (greens, tees, bunkers, cart paths) sit 0.3–0.5m above the terrain — `HeightmapData` is unaware of them. If the ball simulation uses `HeightmapData` as its ground provider in a scene that has physical zone mesh colliders, the ball spawns and rolls at terrain height, visually below the green mesh surface.

**Fix:** `SceneGroundProvider : IGroundProvider` — raycasts from Y=500 downward, returns `hit.point.y` (the first physical surface). Hits the Green_1 MeshCollider (Y≈10.0m) before the terrain collider (Y≈9.6m).

**Rule:** For any PhysicsLab scene that has zone mesh colliders, use `SceneGroundProvider`. Reserve `HeightmapData` for headless/test scenarios or when you need slope normals for out-of-green simulation.

### SceneGroundProvider disables terrain slope — intentionally correct for greens
`BallSimulation.cs` uses `ground is HeightmapData hm` to get slope normals for the gravity-tangent term in `RunPuttPhase`. With `SceneGroundProvider` (not HeightmapData), `BallSimulation` uses flat normal (0,1,0) — no slope-gravity, no downhill pull. This is correct for the green surface (which should be effectively flat for putting). The putt stops naturally via rolling resistance.

### MeshRenderer changes in Play mode don't persist — use edit-mode script-execute
Enabling/disabling `Renderer.enabled` in Play mode (even via script-execute) reverts when Play mode exits. To persistently enable renderers on baked zone meshes: run the enable script in **Edit mode** with `EditorUtility.SetDirty(r)` on each modified component and `EditorSceneManager.SaveScene(scene)` after the loop.

### PhysicsLab camera reset: use trajectory.samples[0] not preset.Origin
`ShotPreset.Origin.y` may be 0 (preset-defined) even when the actual terrain/green is at Y≈10. After simulation runs, `trajectory.samples[0].position` is already terrain-snapped (ball starts at `groundHeight + ballRadius`). Always use the first sample position for the camera origin in `FireInternal`.

---

## Physics — Fixed-Point Precision (Phase 4)

### Use Dot(v, v) for stop detection, not Sqrt(|v|)
`fpMath.Sqrt` underestimates for small inputs. Newton's method initial guess (computed via bit-shift) can land BELOW the true square root; the first Newton step from below jumps above, triggering the `if (r >= prev) break` guard and returning the underestimate. Two consecutive `Sqrt` calls on slightly different small values can return the same raw integer, making `speed <= prevSpeed` fire spuriously and ending the roll phase before the ball has moved.
**Rule:** For stop detection, compare `fpMath.Dot(vel, vel)` (speed²) against `StopSpeed * StopSpeed`. Dot is pure multiply+add — no Sqrt, no precision loss at small magnitudes.
**Do not fix `fpMath.Sqrt` globally** — it is used throughout the aero model for velocity normalization, and changing its output shifts carry distances for all clubs, breaking previously-tuned tests.

### HeightmapData.SampleNormal — use one-sided differences at boundaries
Central differences at a grid boundary clamp the out-of-bounds sample to the boundary value (e.g. `SampleHeight(-cellX, z)` clamps to `SampleHeight(0, z)`), which halves the effective gradient. A 10° slope computes as only 5°, making rolling resistance win over gravity for the first few steps.
**Rule:** In `SampleNormal`, check `worldX <= OriginX` / `worldX >= OriginX + SizeX` (and same for Z) and use forward or backward differences at the boundary. Only use central differences for interior points.

### Assembly boundary: namespace collision with Golfin.Physics
Inside `namespace Golfin.Physics.Runtime`, the bare name `Physics` resolves to the `Golfin.Physics` namespace, not `UnityEngine.Physics`. Any call to `Physics.Raycast(...)` fails with `CS0234`.
**Rule:** Inside `Golfin.Physics.Runtime`, always qualify as `UnityEngine.Physics.Raycast(...)`. The same applies to any UnityEngine type whose name collides with a Golfin namespace segment.

### SurfaceConfig.Default must have per-surface values, not flat defaults
A flat default (e.g., Cr=0.40 for every surface) makes tests that use surface-specific properties (CartPath Cr=0.70, Sand Cr=0.15) meaningless — they all behave identically. Tests using `SurfaceConfig.Default` must be testing the real surface coefficients.
**Rule:** `SurfaceConfig.Default` must encode the canonical per-surface values from `surfaces.csv`. If the CSV changes, update `Default` to match. Water and OOB still need explicit overrides since they're terminal/special cases.

### Putt calibration: proportional rolling resistance model (Phase 5)
With `a = -k*v` (proportional rolling resistance), the stop distance is `d = v0/k * (1 - v_stop/v0)`, NOT `v0/k` (which ignores the stop threshold). For Green (k=0.10, v_stop=0.04), a 3m putt needs v0 ≈ 0.35 m/s. The spec's suggested 1.85 m/s was from a different (constant deceleration) model and would roll ~18.5m — not 3m.
**Rule:** When calibrating putt velocity for a target distance, compute `v0 = k*d / (1 - v_stop/(k*d + v_stop))` or solve iteratively. Read the model in `RunPuttPhase` before accepting spec velocity values at face value.

### Unity MCP: use scene-create / gameobject-create directly, not editor scripts
**Mistake:** When asked to create Unity scenes with GameObjects and components wired, wrote a `PhysicsLabSceneBuilder.cs` MonoBehaviour/editor script and had Cesar run it — then deleted the script after the user pointed out the Unity MCP has `scene-create`, `gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `gameobject-set-parent`, and `scene-save` skills.
**Rule:** For any task that creates or modifies Unity scenes, GameObjects, or components, drive it directly via Unity MCP tools. Do NOT write an editor script just to call Unity APIs — that is extra indirection that requires Cesar to run it. The MCP tools ARE the Unity Editor.
**Sequence:** `scene-create` → `gameobject-create` → `gameobject-component-add` → `gameobject-component-modify` → `gameobject-set-parent` → `scene-save`. Use `script-execute` only for logic that cannot be expressed as a sequence of those calls (e.g., complex Roslyn one-shots).

### fp3 is a readonly struct — fields cannot be mutated in-place
`fp3.x`, `fp3.y`, `fp3.z` are `readonly` fields. You cannot write `v.x = fp.FromFloat(...)`. You must construct a new instance: `new fp3(newX, v.y, v.z)`.
**Rule:** Whenever modifying a single component of an `fp3` (e.g., inside a lambda or loop), always construct a full `new fp3(...)` replacing all three components.

### Assembly reference: TMP in asmdef with overrideReferences
When an asmdef uses `"overrideReferences": true`, TextMeshPro is NOT auto-referenced — it must be added as a GUID reference: `"GUID:6055be8ebefd69e48b49212b09b47b2f"` (path: `Packages/com.unity.ugui/Runtime/TMP/Unity.TextMeshPro.asmdef`).
**Rule:** If a Viewer/UI asmdef has `overrideReferences: true` and uses TMP types, always add the TMP GUID reference explicitly.

---

## UHoleGeo Pipeline

### Topology-critical chain rescue in skeleton extraction
**Mistake:** Using a blanket endpoint-frequency check across ALL raw chains to identify branch nodes. Junction clusters in the downsampled skeleton produce many tiny 2-3px chains between adjacent branch pixels, all of which pass the "both endpoints are branch nodes" filter. Keeping them causes cascading 2-way merges that collapse the entire network into one chain.
**Rule:** Rescue short chains using the LONG chains set as the reference: compute 2-way junctions from longChains (len≥minSpinePixels) only. A short chain that touches a 2-way junction upgrades it to 3-way. Add minimum length floor (`dsFactor*2`) to exclude single-pixel intra-cluster fragments. Never compute branch nodes from all raw chains.

### "Both endpoints are branch nodes" filter is too broad
**Symptom:** Adding a "keep junction bridges" rule increased chain count from 8 to 29 and merged everything into 1 path — worse than before.
**Root cause:** The downsampled skeleton has junction clusters (several adjacent branch pixels), so EVERY pixel in a cluster is a branch node. Tiny 2-3px chains within the cluster all appear as bridges between branch nodes.
**Correct approach:** Check specifically whether the chain's endpoint is a 2-way junction in the long-chain set, not whether it's a branch node across all chains.

## Git / Version Control

### ALWAYS push after changes
User requested: push to GitHub after every change, not just on request.
Pattern: `git add <files> && git commit -m "..." && git push`

### git checkout reverts too much
**Mistake:** Used `git checkout -- <file>` to undo a specific change, but it reverted the file
to the last commit — erasing other unrelated fixes in the same file.
**Rule:** Before reverting, read the file carefully and do a targeted Edit instead.
If you must revert, cherry-pick only the specific lines that need to change.

### Revert removes multiple fixes at once
When `CharacterDetailPanel.cs` was reverted, it lost:
- `selectButton.interactable = !isSelected`
- Level Up / Boost button disabled state logic
**Rule:** Never use `git checkout` on a file that has multiple accumulated fixes.
Use `Edit` to surgically restore just the broken part.

## Unity Transform / Hierarchy

### SetParent worldPositionStays cancels parent Y offsets
**Mistake:** Set a parent GO's `localPosition.y = -0.03f` to lower all children,
but children were positioned in world space BEFORE parenting. Unity's
`SetParent(t, worldPositionStays: true)` auto-adjusts `localPosition` to
preserve the world position, so localPosition.y becomes +0.03f and the offset
is cancelled entirely.
**Rule:** Never apply a Y correction on a parent container to fix child mesh
positions. Bake the correction into the vertex Y values or into the child GO's
world position AFTER parenting (set localPosition explicitly).

## Physics LUT Tuning

### CSV values in test helper must exactly match the CSV files being tested
**Mistake:** `MakeLutConfig()` in the test file had stale drag LUT values (Cd=0.50 at 5-55 m/s) while the "finalized" values from script-execute diagnostics (Cd=0.16 at low speeds) were never applied to either the CSV or the test helper. Test 8 showed all clubs at ~50% of expected carry because of the stale high-Cd values.
**Rule:** Whenever you tune LUT values via script-execute, immediately update both (a) the CSV file and (b) the inline `MakeLutConfig()` equivalent in the test. They must stay in sync or tests become misleading.

### S-monotonicity: spin parameter only increases during a golf ball's flight
The spin parameter S = r·ω/|v| increases as the ball decelerates (v decreases). This means a club starting at S₀ will never sample LUT values at S < S₀ after launch. Safe to tune S > S₀ breakpoints in isolation without affecting that club's early flight.

### SpinDragFactor differentiates clubs with the same speed but different spin
A single 1D drag LUT on speed alone cannot distinguish Iron3 (65 m/s, 461 rad/s) from a hypothetical club at 65 m/s with higher spin. Adding `SpinDragFactor × S²` to Cd gives clubs at the same speed different effective drag based on their spin rate.

### Iron3 model limitation — 1D drag LUT cannot fix a speed-boundary club
Iron3 launches exactly at 65 m/s, the boundary between the low-Cd and high-Cd LUT zones. It spends almost no time in the high-Cd zone before decelerating into the low-Cd zone. Its low spin (S≈0.15) gives negligible spin-induced drag. Fixing Iron3 requires either a 2D drag LUT (speed × spin) or per-club drag parameters — the current 1D model cannot get Iron3 within 5%.

### Spin decay moves clubs toward higher Cl, not lower
Exponential spin decay (ω → ω×(1-k×dt)) reduces ω, which reduces S. Lower S means clubs spend more time on the rising/peak portion of the Cl curve, increasing lift and carry. This is the opposite of what's needed when trying to reduce carry for over-shooting clubs. Spin decay is useful for modeling reality but not for carry reduction tuning.

## Unity Package Manager

### Always commit manifest.json when a package is required by code
**Mistake:** `com.unity.recorder` was installed locally in Unity but never added to `Packages/manifest.json`. A package resolve wiped it, breaking compilation.
**Rule:** ANY time code has a `using UnityEditor.Recorder` (or any package namespace), verify the package is in `manifest.json` BEFORE writing that code. If it's missing, add it and commit `manifest.json` alongside the script — never let them diverge.
**Check:** `grep -r "com.unity.recorder" Packages/manifest.json` — must return a result if the Recorder API is used anywhere in the project.

## Unity / C# Patterns

### CS0136 — duplicate local variable in same scope
If a variable is declared at the top of a method (e.g., `int maxLevel`), don't redeclare it
in an inner block. Use the existing variable or rename.

### Singleton null guard in OnEnable/OnDisable
Always wrap event subscriptions in `if (SomeSingleton.Instance != null)` — singletons may not
be initialized when OnEnable fires during scene load.

### ContentSizeFitter required for HorizontalLayoutGroup to size content
Without `ContentSizeFitter.horizontalFit = PreferredSize`, content width collapses and cards
compress. Always add it to the Content object of a ScrollRect.

### CSV-first pattern for character data
`CharacterDatabaseCSV.Instance?.GetCharacter(id)` returns runtime data for all 12 characters.
`CharacterManager.Instance.GetCharacterTemplate(id)` returns ScriptableObject data (may only
have a subset of characters). Always try CSV first, SO as fallback.

### Viewport is the clipping boundary, not the layout group
Expanding layout group padding does NOT fix card clipping on scale-up.
The `ScrollRect.viewport` RectTransform is what clips. Expand it via `offsetMin`/`offsetMax`.
Guard with a bool (`viewportExpanded`) to prevent cumulative expansion on repeated calls.

### Image.enabled = false works for background hiding
Setting `rarityBadgeImage.enabled = false` correctly hides the background Image.
If it appears not to work, check that the SerializeField is wired to the correct Image component
in the Unity Inspector.

## UI / Design

### Gold color for selected state
Use `new Color(1f, 0.8f, 0.2f, 1f)` as gold for selected button tint.
Apply via `selectButton.GetComponent<Image>().color = goldColor`.

## Editor Fix Scripts — Use Generic Component Search, Not Hardcoded Paths

**Mistake:** FixBarImageTypes.cs used hardcoded Transform.Find() paths. When the user had reorganised the hierarchy for layout fixes, the script missed bars that had moved.

**Rule:** One-shot fix/patch editor scripts that target a component type across the scene should use `Object.FindObjectsOfType<T>()` or recursive search by component, filtered by name if needed — never hardcoded full paths. Hardcoded paths are brittle and break silently when the user adjusts the hierarchy.

**Pattern to use:**
```csharp
// Find ALL Image components in scene, filter by GameObject name
foreach (var img in Object.FindObjectsOfType<Image>())
{
    if (img.gameObject.name == "Bar" || img.gameObject.name == "BarPending")
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        EditorUtility.SetDirty(img);
    }
}
```
This survives any hierarchy reorganisation the user makes.

## ScreenManager Must Drive PersistentUIManager Bar Visibility

**Mistake:** `PersistentUIManager` had `ShowBars()`/`HideBars()` but nothing called them.
`Awake()` hides bars; there was no code to show them when navigating to Home or Roster.

**Rule:** Any screen manager that controls screen transitions MUST also call
`PersistentUIManager.Instance?.ShowBars()` / `HideBars()` in the same `ApplyScreen()` method.
Never leave bar visibility untriggered — it will silently stay hidden.

**Pattern:**
```csharp
bool showBars = screenId == ScreenId.Home || screenId == ScreenId.Roster;
if (Golfin.UI.PersistentUIManager.Instance != null)
{
    if (showBars) Golfin.UI.PersistentUIManager.Instance.ShowBars();
    else          Golfin.UI.PersistentUIManager.Instance.HideBars();
}
```

## UHole Geo — CLI regen must sync to export folder for Unity to pick it up

**Background:** CLI writes to `output/{courseId}/holes/{nn}/` but Unity's `HoleGeoImporter`
reads from `output/{courseId}/export/hole-{nn}/`. These are different paths.

**Fix applied (2026-04-17):** `generate-terrain.mjs` now copies `heightmap.raw` to the export
folder and patches `hole-manifest.json`'s terrain block (width/length/min/max/resolution) after
each regen. Unity import works directly after CLI regen — no UHole Geo GUI step needed.

**If export dir doesn't exist yet:** The script logs "Export dir not found — skipping sync".
In that case the user does need to run a full export from UHole Geo GUI first to create it.

---

## Course Importer — Shore Ramp Artifacts (2026-04-17)

### Never use chamfer distance for terrain ramps — use exact polygon-edge distance

**Problem:** The shore ramp computes a t-value from `distToWater` (chamfer distance transform
from the rasterized water mask) and lerps terrain height from `waterY` to `originalH`. The result
showed persistent vertical stripe/spike artifacts along the waterline.

**Root cause:** Any chamfer distance field computed from a rasterized polygon boundary has
**Voronoi boundaries** — where adjacent cells are "owned" by different boundary pixels, their
distances differ discontinuously. These discontinuities in t propagate directly into height
discontinuities in the lerp. The more the terrain rises above water (larger `originalH - waterY`),
the more visible the stripes become. **This cannot be fixed by blurring** — Voronoi edges are
real discontinuities, not noise. Blurring the distance field softens them but doesn't remove them.
Blurring the heights after the lerp creates new artifacts: the hard mask boundary turns into
visible stairs where blurred ramp cells meet restored non-ramp cells.

**Fix:** Compute the exact Euclidean distance from each terrain cell to the nearest **polygon
edge** of the water contour, not the chamfer distance from the rasterized water mask.

```csharp
// For each candidate cell (pre-culled by coarse chamfer):
float wx = terrainPos.x + x * cellW;
float wz = terrainPos.z + z * cellH;

float minDistM = float.MaxValue;
foreach (var (pts, surfNorm) in waterContours)
{
    int n = pts.Length;
    for (int i = 0; i < n; i++)
    {
        int j = (i + 1) % n;
        float ax = pts[i].x, az = pts[i].z;
        float bx = pts[j].x, bz = pts[j].z;
        float edx = bx - ax, edz = bz - az;
        float len2 = edx * edx + edz * edz;
        float t2 = len2 > 1e-10f
            ? Mathf.Clamp01(((wx - ax) * edx + (wz - az) * edz) / len2)
            : 0f;
        float px = ax + t2 * edx - wx;
        float pz = az + t2 * edz - wz;
        float d = Mathf.Sqrt(px * px + pz * pz);
        if (d < minDistM) { minDistM = d; nearSurfY = surfNorm; }
    }
}
float t = minDistM / shoreRadiusM;
t = t * t * (3f - 2f * t); // smoothstep
```

**Why it works:** Polygon edges are smooth geometry. The distance from a point to a smooth
polygon boundary is a smooth function — no Voronoi artifacts, no stripes. Use a coarse chamfer
pass first to cull distant cells (performance), then exact distance only for the ramp zone.

---

## EditMode Physics Tests — Use BoxCollider, Not MeshCollider (Quad)

**Problem:** `CreatePrimitive(PrimitiveType.Quad)` adds a `MeshCollider`, which requires async mesh cooking. After `yield return null`, the collider is not yet registered in PhysX. `Physics.RaycastAll` returns 0 hits → all snap tests return `defaultY` → tests fail silently.

**Fix:** Create a bare `new GameObject()` and add `BoxCollider` directly:
```csharp
var go = new GameObject("FlatCollider");
go.transform.position = new Vector3(x, y, z);
go.AddComponent<BoxCollider>().size = new Vector3(size, 0.02f, size);
```
`BoxCollider` registers synchronously in PhysX. One `yield return null` is enough for it to appear in raycasts. The top face lands at `center.y + halfExtents.y = y + 0.01`, so assertions must account for this offset.

**Rule:** In EditMode tests that need physics raycasts, always use `BoxCollider` (or `SphereCollider`). Never use `MeshCollider` or `CreatePrimitive` variants (Quad, Plane, Cube) — they all add `MeshCollider` internally.

---

## NUnit Float Tolerance — Use Assert.That, Not Assert.AreEqual

**Problem:** `Assert.AreEqual(float expected, float actual, float delta, string msg)` causes `error CS1503` — NUnit's overload has `(object, object, string)` as the 3-arg form; the 4th arg expects `object` not `string`. Also `Assert.AreNotEqual` has no float-delta overload.

**Fix:** Always use `Assert.That` with constraint syntax:
```csharp
Assert.That(result, Is.EqualTo(10.15f).Within(0.05f), "message");
Assert.That(result, Is.GreaterThan(0.5f), "message");
Assert.That(result, Is.LessThan(10.17f), "message");
Assert.That(result, Is.LessThanOrEqualTo(0.5f), "message");
Assert.That(a, Is.Not.EqualTo(b).Within(0.05f), "message");
```
**Rule:** Never use `Assert.AreEqual(float, float, float)` — always `Assert.That(..., Is.EqualTo(...).Within(...))`.

---

## BallAnimator.DestroyInstance — DestroyImmediate in EditMode

**Problem:** `BallAnimator.DestroyInstance()` calls `Destroy(_instance)`. In EditMode tests (NUnit + UnityTest runner), Unity logs `[Error] Destroy may not be called from edit mode!` — the test runner treats unhandled error logs as test failures.

**Fix:** Guard with `#if UNITY_EDITOR`:
```csharp
#if UNITY_EDITOR
    DestroyImmediate(_instance);
#else
    Destroy(_instance);
#endif
```
**Rule:** Any production code that destroys GameObjects and may run in EditMode tests must use this pattern. `Destroy` is runtime-only; `DestroyImmediate` is the editor equivalent.

**What NOT to do (confirmed failures):**
- Blurring `distToWater` (Gaussian, any sigma) — reduces stripes but can't eliminate Voronoi edges
- Blurring ramp heights (separable Gaussian + restore non-ramp) — creates stair artifacts at mask boundary
- Masked 2D Gaussian on ramp cells only — also creates stair artifacts where ramp meets terrain
- Multiple blur passes — same failure modes, just slower

---

## Course Importer — Spline Cart Paths (2026-04-16)

### Spline cart paths: use `com.unity.splines` (v2.8.4)

`SplineUtility.CalculateLength<T>(T, float4x4)` requires a transform as second argument.
Always pass `float4x4.identity` when the spline is already in world/local space:
```csharp
float len = SplineUtility.CalculateLength(spline, float4x4.identity);
```

### `sed` corrupts C# comment lines starting with `//`

When using `sed -i 's/old/new/'` on Windows (Git bash `sed`), comment lines can get their
`//` replaced with `\`. Always use `Edit` tool for C# file changes — never `sed`.
A corrupted `\` on a line causes a compile error that Unity silently ignores by running the
last cached compiled version, making it look like the code ran but did nothing.

---

## Unity Serialization — One MonoBehaviour Per File

**Symptom (Phase 8.5):** `OutsideClickCatcher` was defined as a second class inside `SelectorOverlayWidget.cs`. In edit mode, `AddComponent<OutsideClickCatcher>()` worked and `GetComponent` returned it. In play mode after domain reload, `GetComponent<OutsideClickCatcher>()` returned null and the Inspector showed "Missing Script".

**Root cause:** Unity serializes MonoBehaviour component references using the MonoScript asset's file GUID. When two MonoBehaviours share a file, Unity can only reliably associate one script asset with one class per file. The secondary class gets an ambiguous MonoScript reference that fails to resolve during domain reload.

**Fix:** Move every MonoBehaviour to its own `.cs` file. Class name must match file name (Unity convention enforced by the serializer at domain reload).

**Rule:** Never define more than one `MonoBehaviour`/`ScriptableObject` subclass per `.cs` file. Non-MonoBehaviour helpers (plain C# classes, interfaces, enums) are fine to co-locate.

---

## Unity UI — Runtime Button is More Reliable Than Custom IPointerClickHandler for Close Triggers

**Symptom (Phase 8.5):** `OutsideClickCatcher : IPointerClickHandler` was added as a component to a full-screen transparent Image GO to detect outside-taps and close a panel. Even after fixing the serialization issue above, the callback (`OnOutsideClick`) required careful `OnEnable` timing and was fragile across domain reloads.

**Better pattern:** For "tap outside to close" overlays, add a `Button` component at runtime in `Open()` and wire `Close()` via `onClick.AddListener`. `Button` is built into Unity.UI, always serializes correctly, and its `onClick` event system is battle-tested:

```csharp
void ActivateDim()
{
    if (_dimGo == null)
        _dimGo = transform.parent?.Find("OutsideClickCatcher_Spin")?.gameObject;
    if (_dimGo == null) return;

    var btn = _dimGo.GetComponent<Button>() ?? _dimGo.AddComponent<Button>();
    var img = _dimGo.GetComponent<Image>();
    if (img != null) btn.targetGraphic = img;
    btn.onClick.RemoveListener(Close);
    btn.onClick.AddListener(Close);
    _dimGo.SetActive(true);
}
```

**Rule:** Prefer `Button.onClick.AddListener(Close)` over custom `IPointerClickHandler` for overlay close triggers. Use `RemoveListener` before `AddListener` to prevent duplicate registrations on repeated Opens.

---

## ActionButtonsBuilder — Never Run in Play Mode; Preserve Manual Inspector Changes

**Symptom (Phase 8.5):** Running `ActionButtonsBuilder.BuildActionButtons()` destroys and recreates the entire action button cluster, wiping all manual Inspector tweaks Cesar has applied (text auto-size settings, icon widths, custom sizes).

**Rule:** The builder is a one-time scaffold tool. After Cesar has manually adjusted values in the Inspector:
1. Do NOT re-run the builder unless absolutely necessary (e.g., adding a brand-new GO to the hierarchy).
2. If a re-run is unavoidable, document every manual change beforehand and restore them programmatically or remind Cesar to re-apply.
3. Prefer code-only fixes (modifying MonoBehaviour scripts) over builder re-runs when fixing runtime behavior.
4. The builder must never run in Play mode — `EditorSceneManager.MarkSceneDirty` throws in play mode.

### Splatmap painting of cart path texture causes a visible border around the mesh

The old splatmap code painted asphalt texture on the terrain using `BuildSpinePolygon()`,
which was wider than the spline mesh on curves. The painted asphalt texture showed up as a
dark border in the grass beyond the road edge.
**Rule:** When a road/path is a mesh overlay, remove all splatmap painting for that surface.
The mesh material handles the visual. Painting the terrain underneath is redundant and creates
visible artifacts at the edges.

### Cart path terrain depression: flat drop, not gradient ramp

**Wrong:** Original depression used a smoothstep gradient ramp (center=100%, edge=0%).
This left terrain at the mesh edge barely depressed — terrain poked through on concave slopes.

**Also wrong:** Outward ramp (full drop inside, taper outside) — depresses grass beyond the
road boundary, creating a visible dark ledge around the road.

**Correct:** Flat drop exactly under the mesh footprint. The mesh itself covers the edge so
no ramp or gradient is needed. Terrain outside the road stays at natural height.
```csharp
// Flat drop only
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (cartDepress[hz, hx])
            heights[hz, hx] = Mathf.Max(0f, heights[hz, hx] - dropNormalized);
```

### Depression polygon must be INSET from mesh edge, not flush or extended

Building the depression polygon at exactly the mesh edge width still marks some cells outside
the mesh (floating point boundary effects + cell-center sampling). Building it wider makes it
visibly bleed into the grass.
**Rule:** For overlay meshes with no fringe, inset the depression polygon by ~0.3m from the
mesh edge: `depHalfWidth = halfWidth - 0.3f`. The mesh covers the inset gap invisibly.

### Depression polygon: use spline right-vector offsets, not mesh edge vert positions

Building the polygon from `leftVerts`/`rightVerts` (actual mesh verts) seems exact but those
verts include terrain height variation (Y) and the XZ positions can drift from the spline
centerline on curves. Using `pos ± right * depHalfWidth` from `SplineUtility.Evaluate` is
cleaner and more predictable for a 2D polygon.

### Static field for cross-method polygon passing

When `CreateSplineCartPaths()` needs to pass depression polygons to `DepressTerrainUnderOverlays()`,
use a `private static List<Vector2[]> _splineCartPathPolygons` field. Reset it at the start of
`CreateSplineCartPaths()` and check for null/empty in `DepressTerrainUnderOverlays()` with a
fallback to the old approach.

### `pos.y` from spline evaluation is NOT reliable for terrain conformance

If spine points are sparse (e.g., one knot every 5-10m), the AutoSmooth Bézier Y between
knots can deviate significantly from actual terrain height — causing the mesh to float or sink.
Fixing this by subdividing the spine to 1m knots and using `pos.y` made the mesh worse (the
Bézier Y overshoots/undershoots between dense knots). Per-sample `terrain.SampleHeight()` at
the centerline is the correct approach for terrain-conforming paths.

### Spline tangent degenerate case

When `tangentFlat = new float3(tangent.x, 0, tangent.z)` has near-zero length (vertical
segment or path doubling back), `math.normalize` produces NaN. Always guard:
```csharp
if (math.lengthsq(tangentFlat) < 0.001f)
    tangentFlat = new float3(1, 0, 0); // fallback to X axis
else
    tangentFlat = math.normalize(tangentFlat);
```

### `MarkWorldContourCells` vs `MarkContourCells`

- `MarkContourCells` — takes `ContourPoint[]` in local meter coords, applies `DepressionInsetMeters` inset automatically
- `MarkWorldContourCells` — takes `Vector2[]` in world XZ coords, NO inset applied
Always use `MarkWorldContourCells` for polygons already in world space (e.g. built from spline verts).

## Never Use ?? With Unity Objects — Use == null Instead

**Mistake:** Used `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()` in `GetOrAddCG`.
C#'s `??` operator uses reference equality (`ReferenceEquals`), NOT Unity's overloaded `==`.
A destroyed/missing Unity component passes `??` but throws `MissingComponentException` on access.

**Rule:** Always use `== null` / `!= null` when checking Unity `UnityEngine.Object` references.
Never use `??` or `?.` for the null-coalescing/null-conditional part of Unity object checks.

**Pattern:**
```csharp
// WRONG — ?? can miss Unity-null objects:
var cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

// CORRECT — == null respects Unity's overloaded equality:
var cg = obj.GetComponent<CanvasGroup>();
if (cg == null) cg = obj.AddComponent<CanvasGroup>();
```

## Pre-Add CanvasGroup in Builder — Never Rely on Runtime AddComponent for Fades

**Rule:** If a GameObject will be faded (FadeIn/FadeOut via CanvasGroup), add the CanvasGroup
in the Editor builder script at build time, not lazily at runtime. Runtime AddComponent on objects
that may be inactive or mid-animation can produce stale references.

**Pattern (in builder):**
```csharp
var cg = clone.GetComponent<CanvasGroup>() ?? clone.AddComponent<CanvasGroup>();
cg.alpha = 0f;  // start transparent; FadeIn animates to 1
```

## Clone RightPanel for Compare Panel — Never Build From Scratch

**Rule:** When a compare/secondary panel must visually match an existing panel, clone it with
`Object.Instantiate(rightPanel.gameObject, parent, false)` rather than building from scratch.
Building from scratch requires duplicating every font/color/size the user set manually.
Cloning preserves all those settings automatically.

**After cloning:**
- Override the clone's RectTransform anchors to position it correctly
- Wrap all cloned children in a new empty container (CompareInfoPanel) for show/hide control
- Add ComparePlaceholder as a full-stretch overlay on top
- Strip any left-column-specific buttons from the clone's ButtonsPanel

## AutoWire Paths Must Be Verified Against Scene YAML — Don't Assume Names

**Mistake:** Assumed child names (RarityLabel, Text) without checking the actual scene YAML.
4 paths failed because real names were: RarityText, LevelPanel/LevelText, LevelPanel/LevelTextMax,
and "Text (TMP)" (not "Text").

**Rule:** For any AutoWire paths that aren't directly from CLAUDE.md documentation, grep the
ShellScene.unity for the actual `m_Name:` values. Use `m_Father` fileID cross-references to
confirm parent-child relationships before coding the paths.

## After Compare Swap — Explicitly Push New Character Into Detail Panel

**Problem:** CharacterDetailPanel.OnSelectionChanged refreshes the CURRENTLY DISPLAYED character's
button state, but never switches currentCharacterId to the newly selected character.
After a swap from compare mode, the panel kept showing the old character.

**Fix:** Add a public `ShowCharacter(string id)` method to CharacterDetailPanel that sets
`currentCharacterId` and calls `UpdatePanel`. Call it from CompareController after any swap,
AFTER CleanupAndExit() (which sets _isCompareMode = false so UpdatePanel doesn't early-return).

```csharp
// In CompareController:
private void CommitSwapAndExit(string characterId)
{
    CharacterManager.Instance.SelectCharacter(characterId);
    CleanupAndExit();  // sets _isCompareMode = false first
    GetComponent<CharacterDetailPanel>()?.ShowCharacter(characterId);
}
```

## HorizontalLayoutGroup Overrides LayoutElement Preferred Sizes for Thin Dividers

**Mistake:** Used `LayoutElement.preferredWidth = 1f` for thin divider Images inside a HLG. The HLG auto-sizes children based on `childForceExpand` and available space, overriding the preferred width entirely.

**Rule:** For absolutely-positioned overlays (dividers, indicators) inside a layout group, use `LayoutElement.ignoreLayout = true` and position manually via RectTransform anchors/sizeDelta.

**Pattern:**
```csharp
var le = divGO.AddComponent<LayoutElement>();
le.ignoreLayout = true;

var rt = divGO.GetComponent<RectTransform>();
float xPos = (float)(i + 1) / buttonCount; // normalized position between buttons
rt.anchorMin        = new Vector2(xPos, 0.15f);
rt.anchorMax        = new Vector2(xPos, 0.85f);
rt.sizeDelta        = new Vector2(1f, 0f);   // 1px wide, height from anchors
rt.anchoredPosition = Vector2.zero;
```

## FadeController GameObject May Be Inactive in Editor — Causes Missing Screen Transitions

**Mistake:** FadeController is left inactive in the scene during editing. Because `Awake()` never runs, `FadeController.Instance` stays null. ScreenManager's `FadeOutThenIn` call is skipped, and the Inventory screen either appears instantly or not at all depending on timing.

**Rule:** In `ScreenManager.Awake()`, find FadeController including inactive GameObjects and activate it before any screen transitions are attempted.

**Pattern:**
```csharp
if (FadeController.Instance == null)
{
    var fc = FindObjectOfType<FadeController>(includeInactive: true);
    if (fc != null) fc.gameObject.SetActive(true);
}
```

## Always Use New Input System — Never UnityEngine.Input

**Mistake:** Used `Input.GetKeyDown(KeyCode)` in a debug script. Project uses the New Input System package, so the legacy `UnityEngine.Input` class throws InvalidOperationException at runtime.

**Rule:** ALWAYS use `UnityEngine.InputSystem` in this project. Never use `UnityEngine.Input`.

**Pattern:**
```csharp
using UnityEngine.InputSystem;

// Key check (replaces Input.GetKeyDown):
if (Keyboard.current != null && Keyboard.current[Key.Backquote].wasPressedThisFrame) { }

// Mouse button (replaces Input.GetMouseButtonDown(0)):
if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { }
```

## Never Use Namespace `Golfin.Debug` — Shadows UnityEngine.Debug

**Mistake:** Created `WalkCamera.cs` in `namespace Golfin.Debug`. Every file with `using UnityEngine;` that calls `Debug.Log` now resolves `Debug` to `Golfin.Debug` instead of `UnityEngine.Debug`, causing 100+ compile errors across the entire project.

**Rule:** Never create a namespace called `Golfin.Debug` (or any `*.Debug` namespace). The name collides with `UnityEngine.Debug` which is used everywhere. Put debug utilities in the global namespace or a non-colliding namespace like `Golfin.DebugTools`.

## Raycast Target on Decorative Images Blocks Button Clicks

**Repeat offender.** This has happened on both the Roster DetailPanel and the Club DetailPanel.

**Symptom:** Buttons exist, look correct, but don't respond to taps/clicks.

**Cause:** A non-interactive Image component (background, rim, portrait, decorative element) has `Raycast Target = true` and sits on top of or overlaps the button in the hierarchy. It eats the click before the button receives it.

**Fix:** Disable `Raycast Target` on ALL non-interactive Image components — backgrounds, rims, portraits, dividers, icons (unless the icon IS a button). Only Buttons and interactive elements should have Raycast Target enabled.

**Prevention:** When creating any new Image component in code or Inspector, immediately set `raycastTarget = false` unless it's intentionally interactive.

## ModalController — Root Must Stay Active

**Rule:** `ModalController` expects the **root GameObject to always be active**. It only toggles the `modalPanel` child via `Show()`/`Hide()`. If the root is inactive, `Show()` still runs (called directly in code, not via Unity events) and calls `modalPanel.SetActive(true)`, but nothing renders because the parent is inactive.

**Symptom:** Console shows `[Modal] X shown` but nothing appears in the hierarchy as active.

**Fix:** Ensure the modal root GameObject is enabled in the scene. Save the scene in that state so Play mode doesn't revert it.

## GameObject.Find() Misses Inactive Objects — Use FindObjectOfType in AutoWire Scripts

**Rule:** `GameObject.Find("Name")` silently returns null for inactive GameObjects. Since modals start hidden (`ModalController.Awake()` deactivates `modalPanel`), the root may be active but if it was ever saved inactive it won't be found.

**Pattern for all AutoWire scripts:**
```csharp
// WRONG — misses inactive objects:
var go = GameObject.Find("MyModal");

// CORRECT — finds inactive too:
var controller = Object.FindObjectOfType<MyModalController>(includeInactive: true);
var go = controller?.gameObject;
```

## Modal Anchor Repositioning Only Works at Canvas Root

**Mistake:** Copied anchor-repositioning logic (world→local coord math) from `LevelUpModalController` into `ClubLevelUpModalController`. The character modal lives at the Canvas root so the math works. The club modal lives inside `InventoryScreen/ContentArea`, which has its own transform offsets — the math lands in the wrong spot and overwrites the correct inspector position every `Open()` call.

**Rule:** If a modal is parented inside a screen hierarchy (not at Canvas root), remove all runtime repositioning code. Set position in the editor; it will hold at runtime.

## Rarity Color Switch — Match Project Canonical Colors, Not Intuition

**Mistake:** In `ItemDetailPanel.GetRarityColor()`, wrote Uncommon as green and Rare as blue —
the opposite of the project standard. The project canonical colors are:
- Common    → grey-blue  `~#BFBFCC`
- Uncommon  → blue       `new Color(0.29f, 0.56f, 0.89f)`  (matches RarityHelper)
- Rare      → green      `#50C878`  `new Color(0.314f, 0.784f, 0.471f)`
- Mythic    → amber      `#FFC107`  `new Color(1.00f, 0.757f, 0.027f)`
- Legendary → orange     `new Color(1.00f, 0.65f, 0.10f)`
- Supreme   → red        `new Color(1.00f, 0.30f, 0.30f)`

**Rule:** When writing a local rarity color switch (for string-based rarities that can't use
`RarityHelper` enum), always cross-check against `RarityHelper.GetRarityColor()` in
`CharacterDatabase.cs` before writing the values. Don't assume which color maps to which rarity.

---

## Editor Scripts — Always Search Including Inactive Objects

**Mistake (repeated):** Used `GameObject.Find("DetailPanel")` and `Object.FindObjectOfType<T>()` in editor scripts. Both silently return null for inactive GameObjects, which is the normal state for screens and modals in this project.

**Rules:**
- For finding by name: use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.name == "X" && go.scene.isLoaded`
- For finding by type: use `Object.FindObjectOfType<T>(true)` (the `true` = includeInactive)

**Pattern:**
```csharp
// By name (finds inactive):
foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
    if (go.name == "DetailPanel" && go.scene.isLoaded) return go.transform;

// By type (finds inactive):
var modal = Object.FindObjectOfType<LevelUpModalController>(true);
```

---

## Terrain Distance Fields: Chamfer vs Exact Polygon-Edge Distance

**Mistake (repeated twice — water shore ramp and tee skirt):** Used a chamfer distance transform as the distance input to a smoothstep lerp that was driving a height ramp. Produced visible banding/stripes on the resulting slope.

**Two distinct failure modes, both from chamfer, but requiring different fixes:**

1. **1-cell Voronoi noise.** Chamfer from a rasterized polygon mask has ~1-cell-wide radial "spokes" of equal distance. If the polygon contour has very fine vertex spacing (< 1 cell), these are the only artifacts — fixable with a Gaussian blur on the distance field.

2. **N-cell polygon-edge plateaus.** If the contour has vertices spaced ~Ncell apart (tees on this project: ~1.5m spacing = ~13 cells), each polygon edge rasterizes into a row of cells sharing identical chamfer distance. The "spokes" are N cells wide. A Gaussian blur of kernel width < N does NOTHING — it just averages identical values together.

**Diagnostic signature:** If you blur the distance field with progressively larger kernels and the banding doesn't move, it's N-cell edge-plateau banding, not 1-cell Voronoi noise.

**Fix for case 2:** Replace chamfer with exact perpendicular distance to the polygon edge. Use the chamfer as a cheap cull (coarse ring of cells), then iterate polygon edges per cell and take the min perpendicular distance. Exact distance is a continuous function of world position — no plateaus, no stripes.

**Reference implementation:** `HoleGeoImporter.cs::FlattenTerrainUnderTees` (line ~3189, exact-distance pass) and the water shore ramp at line ~3453.

---

## Serrated Grass Texture = Ramp Is Too Steep (Not a Boundary Discontinuity)

**Mistake:** Spent three rounds chasing a "C1 gradient discontinuity at the skirt outer boundary" hypothesis for a tee-mound rendering artifact. Specified fade-out write weights, median platform heights, and dual cut-and-fill merges. None worked.

**The actual cause:** Unity's terrain grass shader stretches grass texture vertically when a triangle face is steeper than ~45°. The tee's smoothstep ramp was trying to drop 7.93m over 2m horizontal — a 76° slope — rendered as a vertical cliff face with stretched-grass artifact.

**Diagnostic signatures:**

- **Serrated/streaked grass texture in a band** = Unity rendering a slope steeper than ~45°. It's a rendered ramp face, not a boundary crease.
- **Smooth, visible crease in a thin line** = lighting discontinuity from mismatched triangle normals at a C1-discontinuous boundary.

The two look superficially similar but have completely different fixes.

**Fix:** If the ramp is too steep, the ramp WIDTH must increase. Per-cell adaptive skirt radius based on `|platformY - baseline| / maxSlope` is how real courses handle this — flat sides get a small skirt, steep sides get a long gentle grade-merge that stays below ~19°.

**Key data point that unlocked this:** sampled the natural DEM and found the surrounding hillside naturally drops 8m over ~13m (avg 32°). Our 2m skirt was compressing the first 8m of that drop into a 2m-wide band, artificially 4× steeper than nature. Widening the skirt to match the natural slope ADDS ≤ 1m of lift in the adaptive region — visually invisible, but enough to restore a walkable ramp face.

**Reference implementation:** `HoleGeoImporter.cs::FlattenTerrainUnderTees`, `TeeMaxRampSlope = 0.35f`, per-cell `adaptiveM = clamp(1.5 × dropAbs / maxSlope, base, cap)`.

---

## When a Fix Fails 2–3 Times, Stop Iterating — Do an Adversarial Review

**Mistake (a meta-lesson reinforcing what `Rules.md` already says):** When the tee-mound fix didn't work after three attempts, the fourth attempt was another variation of the same shape ("reduce the height differential" → median platform → dual cut/fill). It also didn't work, AND it broke something else.

**The pattern:** after each failure I was specifying the "next natural step" along the same solution-shape — narrower, wider, re-center, cut, fill. Each variation felt small and justified. But they were all rationalizing the same underlying (wrong) hypothesis.

**Signal that you're in this failure mode:** Your fourth spec would also be "another small variation of the same idea."

**What broke the loop:** An explicit adversarial review. Attack your own hypothesis with "but why would this cause X?" questions. For the tee issue, the attack that killed the hypothesis was: *"If this is a C1 boundary discontinuity, why is it serrated instead of a smooth crease?"* — which forced investigation of what Unity's terrain shader actually renders under various conditions, and uncovered that the "serration" was a steep-slope rendering artifact, not a C1 kink.

**Rule:** After 2 failed attempts at the same conceptual fix, write a spec for an adversarial review instead of another variation. Attack:
1. **The visual signature** — does the actual appearance match what your hypothesis would produce?
2. **The symmetry** — if the bug is in code X, why does it appear in some places and not others?
3. **The math** — simulate it with sampled data. Numbers catch wrong assumptions that prose doesn't.
4. **The sampled reality** — measure the actual DEM/data. Your assumed values are often wrong by an order of magnitude.

For tees: simulation of `dR = drop / maxSlope` showed my first adaptive formulation didn't bound the ramp slope (attack 3, math). Data sampling of Hole 4 showed the real drop was 7.93m, not my assumed 2m (attack 4, data). Both caught before writing a spec.

**The adversarial review cost ~30 min of thinking. The three wrong specs before it cost a day of implementation and rework.**

---

## Water Shore — Inner Collar Fixes Boundary Cliff (2026-04-20)

### Depression polygon boundaries always need a matching inner ramp

**Problem:** `DepressTerrainUnderOverlays` set all cells inside the water polygon to bed level (`surfaceNorm - 0.3m`). The shore ramp on the OUTSIDE set boundary cells (distance=0) to `surfaceNorm`. This created a 0.3m cliff at every polygon-edge cell → per-cell vertical pillars → stretched grass shader → serration artifact.

**Attempted wrong fix:** Moving `CreateWaterMeshes` to run after depression. This caused `terrain.SampleHeight()` at contour vertices to return depressed bed values → `waterY` sank the entire water mesh underground.

**Correct fix:** Inner collar ramp. For cells inside the polygon, compute chamfer distance from the boundary inward. Cells within `ShoreRadius` smoothstep-lerp from `surfaceNorm` (at the edge) to `waterFloorY` (at ShoreRadius cells in). Both sides of the boundary are now co-planar at `surfaceNorm` → no cliff → no serrations.

**Rule:** Any time terrain is abruptly depressed inside a polygon, the cells just inside the boundary must ramp back up to meet whatever surface the outside is transitioning from. The outside shore ramp (surfaceNorm → originalH outward) must be mirrored by an inner collar (surfaceNorm → floorY inward). Both ramps use the same width (`ShoreRadius`) for symmetric transitions.

**CreateWaterMeshes must always sample original (undepressed) terrain** for `waterY` computation. Keep it before `DepressTerrainUnderOverlays`.

---

## Unity Error Pause Kills Input — Debug.LogError in Awake Pauses Play Mode

**Symptom:** ALL input dead in a scene — mouse reads (0,0), leftButton=False, UI buttons completely unresponsive. The same Input System code works perfectly in a different scene. `InputSystemSourceDebugLog` logs `action.pressed=False` every 0.25s with no change even when clicking.

**Root cause:** A MonoBehaviour's `Awake()` fires `Debug.LogError()`. Unity's Console has "Error Pause" enabled by default. Any `LogError` causes Unity to pause play mode after the current frame. In paused state, each Game View click only steps ONE frame — so buttons never complete their click cycle and mouse position is frozen from the previous (pre-click) frame. Input appears completely dead.

**Diagnosis:** Disable all root GameObjects one at a time in the broken scene. When disabling `HeightProvider` made buttons work instantly, that was the culprit. The `HeightProvider.Awake()` called `Debug.LogError` because its `heightmapAsset` field referenced a deleted `.bytes` file.

**Fix:** Remove the offending GameObject. If the component is unused (as `PhysicsLabController._heightProvider` was — a serialized field never read in code), delete the GO entirely from the scene YAML.

**Rule:** If input appears dead in a scene but works elsewhere, check Console for any `LogError` firing in `Awake()`/`Start()`. The Error Pause feature is the most likely culprit. Toggle Error Pause off temporarily to confirm (red stop-button icon in Console toolbar).

---

## Unity Additive Scene Lighting — CopyHoleLighting Pattern

### RenderSettings are per-active-scene; additive loads don't inherit environment

When a hole scene is loaded additively (`LoadSceneMode.Additive`), `RenderSettings` are still driven by the **active scene** (LabScaffold). Renderers in the hole scene (e.g. URPWater with `_REFLECTIONMODE_PROBES`) sample the active scene's environment — which may be a default skybox with no probes, causing the water to render gray.

**Fix — `CopyHoleLighting(Scene holeScene)`:**
1. Temporarily call `SceneManager.SetActiveScene(holeScene)` — this makes `RenderSettings` read from the hole.
2. Snapshot every field: `skybox`, `ambientMode`, `ambientSkyColor/Equator/Ground`, `ambientLight`, `ambientIntensity`, `fog*`, `defaultReflectionMode`, `reflectionIntensity/Bounces`, `customReflectionTexture`, `sun`.
3. Restore LabScaffold as active: `SceneManager.SetActiveScene(scaffoldScene)`.
4. Write all snapshotted values into the now-active LabScaffold's `RenderSettings`.
5. Call `DynamicGI.UpdateEnvironment()` to regenerate the ambient probe and env cubemap.

**Call site:** at the end of `OnHoleLoaded`, BEFORE `SetupAtTee`.
**Also restore active scene** in `OnHoleUnloaded` (set LabScaffold active again).

### ReflectionProbeClearFlags — use `.Skybox` not `.Sky`

`ReflectionProbeClearFlags.Sky` does not exist — it's `.Skybox`. CS0117 compile error otherwise.

---

## MCP script-execute Runs in Editor Context — Cannot Test Runtime Material Changes

`script-execute` always executes in the Unity Editor (not play mode). `renderer.material` in a script-execute creates an **edit-mode material instance**, not the runtime play-mode instance. Any keyword changes made there will NOT be visible during play mode — the runtime creates its own instance.

**Rule:** Do not use `script-execute` to verify or patch runtime material keywords. To confirm runtime material state, check `Debug.Log` output via Unity Console in play mode, or check the scene screenshot after entering play mode.

---

## Struct Fields Cannot Be Null-Checked — Use a Bool Flag Instead

`AeroConfig`, `WindConfig`, `SurfaceConfig`, `PuttConfig` are **value types (structs)**. The compiler will reject `if (AeroCfg == null)` with CS0019 ("operator == cannot be applied to struct").

**Pattern — `EnsureConfigsLoaded()` with bool guard:**
```csharp
bool _configsLoaded;
void EnsureConfigsLoaded()
{
    if (_configsLoaded) return;
    AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
    WindCfg    = PhysicsConfigLoader.LoadWindConfig();
    SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
    PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
    _configsLoaded = true;
}
```
Call from both `Awake()` and any method that needs configs (e.g. `ComputeMaxCarryYards`) for edit-mode safety.

---

## MCP script-execute — Use Skill/stdin, Not tmp JSON Files

Use the `script-execute` MCP skill directly via `Skill` tool or stdin pipe, never intermediate JSON files:
```bash
npx unity-mcp-cli run-tool script-execute --input-file - &lt;&lt;'EOF' {"csharpCode": "...", "className": "Script", "methodName": "Main"} EOF
```

JSON files are no faster, add repo noise, and get left behind in the project root.

**Rule:** For complex multi-line code, use a heredoc. Only write to a temp file if the shell escaping is genuinely unresolvable. Never leave `tmp_*.json` files in the project root.

---

## Session Conventions (Cesar's standing rules)

### End responses with the work output — no sign-offs, no farewells, no catchphrases

End every response with the actual work output (file summary table, status, next step). Do NOT append goodbyes, well-wishes, sign-off lines, or recurring catchphrases of any kind — not at the end of a task, not at the end of a session. Cesar will say goodbye when he is done; until then, every response ends on substance. This rule overrides any prior session conventions that introduced a sign-off phrase.

### Always end task reports with a file summary

After completing any task, end the report with a table listing every file written/modified and its status (done, pending, etc.). Example:

FileStatus`Assets/Scripts/Physics/Tests/StatResolverTests.cs`✅ done`Docs/AI_CONTEXT.md`✅ done

### Always use Unity MCP to interact with Unity

Use Unity MCP tools (`tests-run`, `script-execute`, `gameobject-create`, etc.) for all Unity Editor interactions. If Unity MCP is unavailable (not connected, Unity not open), say so explicitly — do NOT fall back to batch-mode CLI, editor scripts, or other workarounds without telling Cesar first.

```---
```
```
## Physics — Surface-Aware Ground Sampling (Terrain Fallthrough Fix, 2026-04-24)

### Two separate SurfaceType enums and SurfaceMarker components exist — don't conflate them
`Golfin.Physics.SurfaceType` and `Golfin.Physics.Runtime.SurfaceMarker` live in the Physics assembly. `Golfin.Course.SurfaceType` and `Golfin.Course.SurfaceMarker` live in Assembly-CSharp. The migration (`SyncPhysicsSurfaceMarkers`) must iterate `Golfin.Course.SurfaceMarker` and ADD `Golfin.Physics.Runtime.SurfaceMarker` where missing — NOT the reverse. The original design iterated Physics markers and tried `GetComponent<Course.SurfaceMarker>()` on the same GO, finding zero results because most GOs had only Course markers.
**Rule:** Migration direction is Course → Physics (Course markers are the source of truth from UHole import; Physics markers are what BallSimulation consumes).

### `Golfin.Physics.Core` has `noEngineReferences: true` — use callback pattern for logging
`BallSimulation.cs` is in a Core assembly with `noEngineReferences: true`. `UnityEngine.Debug.LogError` is unavailable. Use a static `Action<string>` callback (`DiagErrorLogger`) that callers in Runtime assemblies wire to `Debug.LogError`. Wrap all calls in `#if UNITY_EDITOR` to zero cost at runtime.

### Overlapping BoxCollider geometry is the right approach for deterministic physics tests
Real hole scenes cause unpredictable ball trajectories (ball flies off large colliders, minimum-Y gap becomes −1000m). Synthetic BoxCollider geometry (CreateFlat helper: `box.size = new(w, 0.02f, d)`, top face at exact worldY) is fully deterministic. The fallthrough scenario requires TWO overlapping colliders: higher-Y surface (fringe/fairway) covering a larger area PLUS lower-Y surface (green/bunker) covering a smaller area. `SceneGroundProvider.SampleHeight(x, z, preferred)` must pick the lower Y for balls on the lower-marked surface.

### `overrideReferences: true` asmdef requires explicit Physics.Runtime reference
`Golfin.Gameplay.Tests.asmdef` has `overrideReferences: true`. Auto-referenced assemblies are excluded. If tests reference `SceneGroundProvider` or `SurfaceMarker`, add `"Golfin.Physics.Runtime"` to the asmdef references array explicitly.

### MCP tests-run has ~60s timeout — run stress tests separately
Full EditMode suite with stress tests (~45s) + other tests risks timeout. Run stress tests (`testClass: "TerrainStressTests"`) and non-stress tests in separate `tests-run` calls. Both pass individually; combined run may time out the MCP tool.

---

## Shot UI — Cone Height Shared Between View and Dragger (Phase 8.1/8.2, 2026-04-27)

## Editor Tooling — CaptureHelper (2026-04-29)

### `ScreenCapture.CaptureScreenshotAsTexture()` is NOT the Game View — use RT reflection

`ScreenCapture.CaptureScreenshotAsTexture()` reads the OS display swap chain. In the Unity Editor, the Game View renders to an **internal** `RenderTexture`, NOT to the swap chain. Calling it from a MenuItem produces black or Editor chrome.

**Fix:** Read the GameView's internal RT via reflection:
```csharp
var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
gv.Focus(); gv.Repaint();
string[] candidates = { "m_RenderTexture", "m_TargetTexture", "m_RenderTarget" };
RenderTexture rt = null;
foreach (var name in candidates)
{
    var f = gameViewType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    rt = f?.GetValue(gv) as RenderTexture;
    if (rt != null && rt.IsCreated()) break;
}
```
Also: `ReadPixels` returns bottom-up (OpenGL). Flip Y before `EncodeToPNG()`.

**Rule:** All future screenshot capture in Editor code must use `CaptureHelper.SnapGameView()` — never `ScreenCapture.CaptureScreenshot(path)` (async, banned) or `CaptureScreenshotAsTexture()` from a MenuItem (reads wrong buffer).

### MenuItem mouse-stuck state — always call `ReleaseMouseAfterMenu()`

When a `[MenuItem]` executes via mouse click, the corresponding MouseUp is never delivered to the Game View. Unity's input state thinks the left button is still held — moving back to the game canvas pans the camera.

**Fix:** At the end of every MenuItem handler that runs while a Game View is open:
```csharp
private static void ReleaseMouseAfterMenu()
{
    GUIUtility.hotControl = 0;
    EditorApplication.delayCall += () => {
        GUIUtility.hotControl = 0;
        var gvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gv = gvType != null ? EditorWindow.GetWindow(gvType, false, null, false) : null;
        gv?.Repaint();
    };
}
```
Call it as the last line of every `[MenuItem]` method that could be clicked while a Game View scene is loaded.

### InGame portraits, not Thumbnails

For in-game HUD widgets the correct portrait subfolder is `Resources/Portraits/InGame/` (the circular/framed in-game versions). `Resources/Portraits/Thumbnails/` and `Resources/Portraits/Rankings/` are for roster/leaderboard screens only.

---

### `ClubHandleDragger._coneHeightPx` must stay in sync with `ShotConeView._coneHeightPx`
Both classes have a `_coneHeightPx` serialized field. `ShotConeView` uses it to position the ClubHandle visual; `ClubHandleDragger` uses it to map drag positions to power values. When Phase 8.1 changed `ShotConeView._coneHeightPx` from 600→1009, the dragger was not updated — all pointer positions above y=600 clamped to zero power and the handle appeared frozen.
**Rule:** When changing cone height in `ShotConeView`, call `SetConeHeight(_coneHeightPx)` on the `ClubHandleDragger`. This is now wired in `ShotConeView.Awake()` via `_clubHandle.GetComponent<ClubHandleDragger>()?.SetConeHeight(_coneHeightPx)` so they always stay in sync automatically.

### When adding a new cone height field, check if other components duplicate it
Before adding `_coneHeightPx` to any new component, grep the codebase for existing holders (`ClubHandleDragger`, `ShotConeView`, `TimingSlabGraphic`). Use `SetConeHeight()` / `SetConeParams()` APIs to propagate from the single authoritative source (`ShotConeView`).

---

## matchmaking_modal closeout (2026-05-02)

### `ModalController.modalPanel` wires to ContentArea, NEVER the root
`ModalController.Awake()` calls `modalPanel.SetActive(false)`. If `modalPanel` is wired to the controller's own root GameObject, the controller self-deactivates before any of its coroutines (dot cycle, opponent scan) can run. Wire `modalPanel` to a child GameObject — typically `ContentArea` — that holds everything the modal animates. The controller stays active; only the visible content gets toggled.

**Rule:** Every `ModalController` subclass must declare its `modalPanel` reference as a child sub-tree (e.g. `ContentArea`), never the same GameObject the controller component lives on. Codified into the matchmaking_modal spec on 2026-05-02 — now treat as canonical for all future modal tasks.

### Figma reward / level / content values are placeholders, NOT canonical
When a UI surface mirrors a CSV or database value (rewards, hole names, character stats, etc.), the values shown in Figma are typically placeholders. The implementer must NOT "fix" the runtime to match Figma if the runtime is correctly sourcing from CSV. Spec must explicitly tag these fields as placeholder-vs-canonical.

**Concrete example from matchmaking_modal:** Figma frame `12865:1095` shows reward x10/x10/x10 across all three slots. The actual contract was "modal matches home screen / CSV" — Lomond 5 in `LevelUpCosts.csv` is x100/x10/x30. iter-1 self-review caught the desync where the modal was reading from a stale `.asset` while the home screen was reading from the CSV.

**Rule:** Any UI spec covering data-driven content must include a "Placeholder vs canonical content notes" section listing which Figma values are placeholders and which are pixel-precise contracts.

### `Application.runInBackground = true` for editor screenshot workflows
When capturing play-mode screenshots from `script-execute` or any agent-driven editor pipeline, the Game View often loses OS focus during the capture sequence. Unity's default `runInBackground=false` then stops driving frames — coroutines pause, animations freeze, the modal never reaches its final state, and `CaptureHelper.SnapGameView()` returns whatever was rendered when focus was lost (often the splash logo if capture happens early in the boot flow).

**Rule:** At the start of any agent-driven editor capture session that needs play-mode coroutines to advance, set `Application.runInBackground = true` via `script-execute`. Pair with `EditorApplication.isPaused = false` after `EditorApplication.isPlaying = true`. Both are needed: `isPaused=false` unfreezes the editor's frame loop; `runInBackground=true` keeps the loop driving when focus shifts. Belt-and-suspenders: also call them again immediately before the snap.

### Editor scripts run by agents must log via `Debug.Log`, not `EditorUtility.DisplayDialog`
When an agent runs an editor script (AutoWire, capture helpers, scene migrators), each `EditorUtility.DisplayDialog` call opens a modal popup that blocks the editor until clicked. Across multiple agent invocations these popups stack, requiring manual dismissal of each. `Debug.Log("[Tag] result: ...")` writes to the Console — non-blocking, accumulates cleanly, filterable via the existing log-grep recipes in CLAUDE.md.

**Rule:** New editor utilities target `Debug.Log` with bracketed prefixes. The exception is one-shot `[MenuItem]` actions Cesar himself invokes from `GOLFIN/...` — a single dialog there is fine. Agent-driven, repeated, or batched invocations must use the Console.

### Editing `.unity` / `.asset` YAML directly triggers a Unity Reload modal
Direct YAML mutations of scene or `.asset` files via `assets-modify` while Unity has them open trigger a "Scene/asset has been modified externally — Reload?" modal. The popup blocks Unity's main thread; no frames render; the GameView render texture freezes; every subsequent MCP call hangs or returns stale data. To the agent it looks like a Unity bug.

**Rule:** Prefer Unity-API mutations: `gameobject-modify`, `gameobject-component-modify`, `scene-save`, `assets-prefab-open`/`save` for prefabs, `object-modify` for ScriptableObjects. Only fall back to raw `assets-modify` on YAML when the API path can't accomplish the change, and explicitly tell Cesar the popup will appear before the next snap. If the GameView returns identical pixels across 3+ snap attempts after a scene/asset write, assume a popup is up — surface to Cesar, do not loop.

### Unity playmode entry does NOT guarantee `IsPaused=false`
`editor-application-set-state isPlaying:true` enters play mode, but if "Pause On Enter Play Mode" is enabled (or pause carries over from prior state), play mode starts paused. `script-execute` calls during pause set `activeSelf` flags immediately at editor time, but no Awake/Start/Update runs and no frame renders until unpaused. So a "scene state shows X active" diagnostic from `script-execute` while paused is meaningless for what the user actually sees.

**Rule:** After every `editor-application-set-state isPlaying:true`, follow with an explicit `script-execute` call setting `EditorApplication.isPaused = false`. Then assert `IsPaused=false` via `editor-application-get-state` before any timing-sensitive operation. If a screenshot looks like a stale splash, suspect pause first — not focus, not the capture helper.

### `CaptureHelper.SnapGameView()` does NOT need GameView focus
The capture helper reads the GameView's RenderTexture via reflection, bypassing the focus requirement. If a screenshot looks wrong, focus is almost never the cause — pause state and/or `runInBackground=false` are. Don't blame focus before checking those two.

### Use `CaptureHelper.SnapGameView()`, not `mcp__ai-game-developer__screenshot-game-view`
The MCP tool `screenshot-game-view` is a generic capture in the IvanMurzak package — it returned `Response data is null` repeatedly under the local-stdio MCP build, with no actionable error. The project's own `Golfin.EditorTools.CaptureHelper.SnapGameView()` (mandated in CLAUDE.md) reads the GameView's internal `RenderTexture` via reflection across known field names (`m_RenderTexture` / `m_TargetTexture` / `m_RenderTarget`), Y-flips for the OpenGL coordinate space, and writes a PNG synchronously to `Docs/Diagnostics/_capture/`. It works from EditMode, paused playmode, and running playmode — exactly the matrix CLAUDE.md cares about.

**Rule:** For every play-mode or scene screenshot, invoke `Golfin.EditorTools.CaptureHelper.SnapGameView()` (or `SnapGameViewWithLabel("tag")`) via `script-execute`. Do NOT call `mcp__ai-game-developer__screenshot-game-view` — it bypasses the project's tested capture path. After capture, copy the PNG from `Docs/Diagnostics/_capture/` into the relevant `Docs/Specs/Active/<task>/screenshots/` folder before committing.

### Surface MCP issues clearly BEFORE falling back to manual
When an MCP tool fails ("tool not available", "transport dropped", "no such tool", null response) the wrong move is to silently default to "Cesar do it manually." The right sequence:

1. **Retry first** — per `feedback_unity_mcp_transport_recovers.md`, transport errors are transient. Retry every 30–60s for up to 5 attempts.
2. **Surface in chat clearly** — name the tool, the input, the exact error, retry count, and the fallback (if any). Quote Cesar's rule when relevant: *"If you run into MCP issues and have to surface them, do so. Do not just fallback to me manually doing things without mentioning the issues clearly first."*
3. **Don't lead with "you do it manually"** — the chat must lead with the failure context. Manual instructions are the last resort, not the default.

**Specific rule for test runs:** `mcp__ai-game-developer__tests-run` is granted to `golfin-implementer` only. Reviewer/self-reviewer cannot run tests. If a SPEC requires test results and the implementer didn't capture them, the correct verdict is `ARCHITECT_REVIEW_FAIL` routing back to the implementer — NOT escalation to Cesar with "manually run tests" as the fix. The implementer agent definition (`.claude/agents/golfin-implementer.md` Hard rules) now mandates this and the reviewer definition (`.claude/agents/golfin-reviewer.md` "Test runner verification" section) routes back accordingly.

Symptom that this rule is being violated: Cesar reads "Window → General → Test Runner → EditMode → Run All" without first being told why the automated path failed.

---

## Defense-in-Depth Fixes Can Mask the Original Regression Site (controls_g, 2026-05-07)

**Symptom:** controls_g shipped `AeroConfig.AssertValid()` wired into `LoadAeroConfig` to defend against zero-init structs causing a `DivideByZeroException` at `AeroModel.cs:78`. The fix worked — 240/240 tests PASS, driver shots no longer crash. But the ACTUAL code path that was producing a zero-initialized `AeroConfig` was never identified. Implementer's stated "Hypothesis C — zero-init struct" was empirically wrong: an architect grep for `new AeroConfig()` and `default(AeroConfig)` across the entire `Assets/` tree returned ZERO hits.

**Why this matters:** AssertValid catches the symptom (zeroed `SpinRateReference`) at config-load time with a clear error message. So practical risk is contained. But the *mechanism* that produced the zero value is still in the codebase — likely either (a) an `AeroConfig` field cached on a long-lived object and read before `LoadAeroConfig` populated it (race / order-of-init), (b) Unity serializer round-trip on a struct field zeroing it during scene reload or domain reload, or (c) a different code path that AssertValid happened to also cover, distinct from the one that originally crashed.

**Rule:** When a fix lands via defense-in-depth (an assertion, a guard, a fallback) without identifying the actual regression site, document the gap explicitly in the implementer report and architect review. The masked cause may resurface in a different config struct (`WindConfig`, `SurfaceConfig`, `PuttConfig`, `StatCoefficients`, `StatCaps`) under the same mechanism. Do NOT assume "it's a different bug" — assume it MAY be the same masked mechanism resurfacing, and run the equivalent grep + add equivalent AssertValid for the new struct.

**Diagnostic checklist when zero-init suspected:**
1. `grep -rn "new <StructName>()"` and `grep -rnE "default\(<StructName>\)"` across `Assets/` — zero hits means it's NOT a direct construction site.
2. Search for serialized fields of the struct type on MonoBehaviours / ScriptableObjects — these can deserialize as zero.
3. Search for static fields of the struct type — these initialize to zero before `Awake` runs in any subscriber.
4. Search for `[NonSerialized]` fields and per-frame structs — these stay zero unless explicitly assigned.

Without finding a root cause via at least one of those four, document the unresolved mystery in the architect review and lessons file. Don't pretend the AssertValid "explained" the bug — it backstopped it.

---

## Smoke-Runner Timed Waits Are Fragile Against Shot-Power and Carry Changes (controls_g, 2026-05-07)

**Symptom:** `SmokeTestRunner2b.cs` in controls_g used 3-second `WaitForSeconds(3f)` waits to schedule camera-mode captures (Downrange after the 65%-carry cinematic cut). The wait was tuned for a specific lab driver power level. When the actual shot at 0.8 power didn't reach 65% carry within 3 seconds, the capture fired during the Aiming charge frame instead of mid-flight — captured the HUD power ring, not the Downrange cinematic.

**Three failure modes from this pattern:**
1. **Wrong moment captured** — the obvious one. Shot was still in earlier state.
2. **Inconclusive frame** — the capture fires somewhere but with no terrain backdrop loaded (LabScaffold doesn't include Hole_01_Geo by default), so the captured frame technically shows the right state but visually proves nothing.
3. **Drift over time** — even if you tune the timing perfectly today, any future change to lab power calibration or carry distance breaks the gating, and the smoke runner silently captures wrong frames again.

**Rule:** Prefer state-driven captures (`CaptureCore.SnapWhenStateReached(sm, BallState.X, label)` for SM transitions, or a future `SnapWhenModeReached(director, ChaseCamera.Mode.X, label)` for Director mode changes) over time-driven captures (`yield return new WaitForSeconds(N)` followed by snap) whenever the state machine or director exposes the transition.

**When state-driven isn't available:** if the moment of interest is INSIDE a state (e.g. Director's mid-flight cinematic cut at 65% carry happens inside `Flying` state, not at a state boundary), the right fix is to ADD an event for the moment (e.g. `LoopCameraDirector.OnModeChanged`) rather than time-gate around it. Adding the event is usually <30 minutes of dev and pays back across every future smoke runner that needs to observe that transition.

**Last resort — if you must time-gate:** compute the time threshold from sim data, not a magic number. For a 65%-carry cut: read `controller.LastTrajectory` after the shot fires, compute predicted carry, time-gate on a percentage of the predicted carry. NEVER hardcode `WaitForSeconds(3f)` for a moment that depends on physics.

**Pattern recognition:** if the smoke runner writes any `WaitForSeconds(N)` with N > 0.5s and N is not a deliberately-chosen settling delay, that's a code smell. Replace with state-gating.


---

## Dress Up Designs at Build Time, Even When Runtime Overrides Them (Cesar workflow rule, 2026-05-12)

**Rule:** Every Editor-built UI prefab/scene hierarchy must include realistic placeholder content (real sprites, real-looking text strings, real fonts, real colors) at BUILD time, not at runtime only. This applies even when `Show()` / data-binding overrides the content at play time.

**Why:** Cesar does the Inspector work — tweaking RectTransforms, adjusting fonts, repositioning anchors. He can't see if a layout works if every TMP says "Sample Text" or every Image has no sprite. Empty / placeholder-only widgets force him to enter Play mode just to validate a layout tweak, which is slow and error-prone.

**Pattern in builders:**
- Card 1 (current hole) — assign actual Hole 1 map sprite, fill stats with realistic example strings ("STROKES: 4 (BIRDIE)", "BEST: 5 (PAR)", "TIME: 00:02:34"), use real reward icons + plausible counts ("x10").
- Card 2 (next hole) — assign actual Hole 2 map sprite, fill description with a realistic 3-4-line hole tip string, not "TBD" or "Lorem ipsum".
- Headers, subheads, buttons — real localized-style strings ("Lomond Country Club - Hole 1 - Par 5"), not "[HEADER]" or "Card1.Subhead.Default".

**What this is NOT:** baking gameplay state into the prefab. The runtime `Show(data)` still overrides everything based on the actual hole/strokes/score. The build-time content is purely for Editor preview fidelity.

**Pattern recognition:** if a builder method writes `tmp.text = ""` or `img.sprite = null` for any UI element that will eventually display content, that's a workflow regression. Always write a realistic placeholder instead.

---

## 5-Minute Surface Rule for MCP / Unity Blockers (Cesar workflow rule, 2026-05-13)

**Rule:** If a subagent (especially the implementer) is not making productive progress for 5 wall-clock minutes — for ANY reason — it must IMMEDIATELY surface the blocker to Cesar by setting STATUS to `IMPLEMENTER_BLOCKED`, writing the symptom to `HEARTBEAT.log`, and returning to the orchestrator.

**Forbidden:** silent waiting beyond 5 minutes hoping Unity MCP recovers. Cesar has no other signal that the agent is stuck; if you wait 30 minutes silently, that is 30 minutes of his wall-clock time lost.

**What counts as "not making productive progress":**
- MCP `tools/list` returns empty after a domain reload
- `script-execute` returns success but the actual side effect (recompile, builder bake, scene save) never lands
- `editor-application-get-state` reports `IsCompiling=true` indefinitely
- A modal dialog has Unity frozen (`ContainerWindow::MakeModal` stuck — Unity logs show this)
- Same MCP tool call retried 3-5 times with the same null/error response
- ANY tool call sequence where the elapsed clock time has crossed 5 minutes since the last useful tool result

**What does NOT count:**
- A single long-running call that's making known progress (e.g., a test run that takes 3 minutes to complete and prints incremental output)
- A `Bash sleep 30` followed by a working retry — that's productive, you got the result

**The implementer-agent definition (`.claude/agents/golfin-implementer.md`) enforces this rule. The 5-minute wall-clock starts at the first symptom, not at the agent's activation. After surfacing, Cesar restarts Unity / dismisses modal / does whatever's needed and reactivates the implementer.**

**Postmortem motivating this rule:** iter-11 surgical fix took 66 minutes wall-clock. ~22 minutes was actual work; ~33 minutes was MCP unresponsive after a domain reload while the agent retried silently. Cesar had no way to know the agent was stuck until it finally returned. Surfacing at 5 minutes would have cut wall-clock to ~25 minutes total.

---

## Capture Paths Must Never Mutate Live Scene State Without Try/Finally Restore (Cesar workflow rule, 2026-05-13)

**Rule:** Any screenshot / capture path that toggles scene state (SetActive, RectTransform position/size, component enabled, color/alpha overrides) MUST wrap the mutation in `try/finally` with full restoration in the `finally` block. If the capture saves the scene mid-execution, the toggled state will persist into normal play — gameplay UI elements vanish, RectTransforms shift permanently.

**Forbidden:**
- `gameObject.SetActive(false)` followed by capture followed by `gameObject.SetActive(true)` outside a try/finally. An exception or async-await break between the toggle and the restore leaves the scene corrupted.
- Capture paths that call `EditorSceneManager.SaveScene` while mutated-state is live.
- Capture paths that "temporarily" reposition RectTransforms for framing without an unconditional restore.

**Required pattern:**
```csharp
var snapshot = TakeStateSnapshot(targets); // captures m_IsActive, sizeDelta, color, etc.
try {
    foreach (var go in targets) go.SetActive(false);
    CaptureCore.SnapPlayModeSafe("label");
} finally {
    RestoreFromSnapshot(snapshot); // unconditionally restores every mutated field
}
```

**Stronger guarantee:** use `CaptureHelper.SnapGameView` / `SnapPlayModeSafe` — they're rendering-pipeline-level captures that don't require scene mutation. If you find yourself needing to SetActive things to suppress HUD for a screenshot, the right answer is to raise the result-screen Canvas sortingOrder ABOVE all HUDs (iter-9 F1 approach) so visible suppression is rendering-order-based, not scene-state-based.

**Postmortem motivating this rule:** iter-12 (2026-05-13) implementer's custom ortho-camera capture path deactivated 10 ShotUI GameObjects in `LabScaffold.unity` (PowerHUD, ActionButtons_Cluster, HoleCard, SettingsButton, DebugShotPanelController, WindIndicator, HoleIndicator, ConeRoot, PlayerCard, CentralBall) and repositioned several RectTransforms. Restore step was missing. Scene saved with the broken state. Result: gameplay shotUI was invisible on next play. Required emergency `git restore` of the scene file. Architect-pass + self-reviewer both missed it because the reported screenshots looked correct (they were taken in the deactivated state).

**Reviewer note:** when reviewing a capture-heavy iter, ALWAYS diff `LabScaffold.unity` (or whichever scene was modified) at the m_IsActive / sizeDelta / position level. False PASSes happen when the screenshot looks right but the scene was mutated in unintended ways to make it look right.

---

## Bounding-Box Containment Must Be Programmatically Verified, Not Eyeballed (Cesar workflow rule, 2026-05-13)

**Rule:** For any review where the question is "is child element X visually contained inside parent container Y" — modals containing text, panels containing children, BGs covering content stacks — the reviewer MUST verify programmatically via MCP, not by eyeballing a screenshot.

**Why eyeballing fails:**
- Screenshots are typically cropped tight around the element being reviewed. Text floating OUTSIDE the container's BG against a similar-color backdrop is indistinguishable from text INSIDE the container against the same color in a single static crop.
- The reviewer agent inherits the implementer's chosen framing — if the implementer cropped to hide the violation (intentionally or accidentally), the reviewer has no signal.
- Production play-flow layout timing differs from smoke-runner timing. A CSF/SetSize trick can produce a clean smoke screenshot while breaking in actual gameplay (iter-11 hit this exact failure mode).
- The card BG color and the dimmed-screen backdrop color are often both dark — the boundary between them is hard to see by eye.

**Required verification pattern:**

```csharp
// MCP script-execute or direct reflection
var card = GameObject.Find("Card2");
var cardRT = card.GetComponent<RectTransform>();
var cardRect = cardRT.GetWorldRect();
foreach (var childName in new[] { "LockedHeader", "Subhead", "RewardsRow" }) {
    var child = card.transform.Find($"ContentRoot/{childName}");
    if (!child) continue;
    var childRT = child.GetComponent<RectTransform>();
    var childRect = childRT.GetWorldRect();
    bool contained = cardRect.Contains(childRect.min) && cardRect.Contains(childRect.max);
    Debug.Log($"[bbox-check] {childName}: contained={contained} child={childRect} card={cardRect}");
}
```

The reviewer reads the log and verifies `contained=true` for every child that's supposed to be inside the BG. ANY `contained=false` → FAIL.

**Also required for modal/panel reviews:**
1. Capture BOTH a tight crop AND a full-screen frame so the BG outline is visible against the dimmed backdrop.
2. For any "does container size to its content" change, verify in normal play flow (trigger via gameplay debug button), not just smoke-runner — layout-pass timing differs between contexts.
3. If the implementer used `LayoutRebuilder.ForceRebuildLayoutImmediate` + manual `SetSizeWithCurrentAnchors` to work around CSF/VLG limits, that's a yellow flag — the runtime path can race against parent VLG layout in production. Prefer structural fixes (add a VLG, fix anchors, etc.) when allowed.

**Reviewer note for the future Cesar/Architect chats:** the iter-6 / iter-8 / iter-11 / iter-12 false-PASS pattern for this task was rooted in this exact qualitative-eyeball failure. The full pipeline (implementer + self-reviewer + architect-reviewer) all green-lit modals that had text floating outside the BG on multiple iterations. Cesar caught every one in live play. The fix is programmatic geometry verification, not better screenshots.

**Postmortem motivating this rule:** iter-12 (2026-05-13) was approved by self-reviewer AND architect-reviewer with text floating mostly ABOVE the LOCKED card BG. The smoke screenshot looked clean (partly because the capture path had also broken ShotUI GO state in the scene, which itself was a separate regression). Cesar saw the floating text immediately in live play and had to manually add 144px top padding to LockedHeader's HLG to push the content inside the BG.

---

## Bbox Containment Rule — Padding Edge Case (refinement 2026-05-13)

**Refinement to the bbox-containment rule:** when a child element is a `HorizontalLayoutGroup` / `VerticalLayoutGroup` container with `padding.top > 0`, the LG's RectTransform bounds INCLUDE the padding area. Its `GetWorldCorners()` will report a top edge `padding.top` pixels ABOVE the visible rendered content. Naive bbox check (`childCorners[2].y <= parentCorners[2].y`) will report `inside=false` even when the visible content is inside the parent.

**Example (iter-13):** LockedHeader is an HLG with `padding.top = 144` (Cesar's manual fix). The container top extends 124.5px above the card BG top, but the visible lock icon + "LOCKED" text are positioned at `top + 144`, well inside the card. Bbox check on LockedHeader.rect reports `inside=false`; visual check reports content inside.

**Refined rule:** when bbox check returns `inside=false` for a Layout-Group-with-padding child, also compute the LG's RENDERED content rect:
```csharp
var lg = child.GetComponent<HorizontalLayoutGroup>(); // or VerticalLayoutGroup
if (lg != null) {
    // Account for padding: the actual content rect is the LG bounds inset by padding.
    var contentMinX = childCorners[0].x + lg.padding.left;
    var contentMaxX = childCorners[2].x - lg.padding.right;
    var contentMinY = childCorners[0].y + lg.padding.bottom; // Unity Y is bottom-up
    var contentMaxY = childCorners[2].y - lg.padding.top;
    bool visualInside = contentMinX >= parentCorners[0].x && contentMaxX <= parentCorners[2].x &&
                         contentMinY >= parentCorners[0].y && contentMaxY <= parentCorners[2].y;
    Debug.Log($"[bbox-rendered] {childName}: visualInside={visualInside}");
}
```

If `visualInside=true`, the failed naive bbox is a padding-layout artifact — PASS that element. If `visualInside=false`, real overflow — FAIL.

**For non-LG children (plain images, TMPs without LG wrapping), the naive bbox check is correct as-is.**

**The reviewer must run both checks** — naive bbox AND padding-adjusted visualInside — and treat ONLY a `visualInside=false` as a hard FAIL. Document both values in the review.


---

## Lesson Q — Iteration Spirals Signal Structural Debt, Not Bugs To Patch (2026-05-14 JST)

**Origin:** `putter_aim_yaw_in_groundlevel`, iterated 5× by the implementer before architect-executed rollback.

**The spiral:**
- iter-1 — original SPEC's L4 said "Reuse `ChaseCamera.GroundLevel`" for putter Aiming. Implementer added orbit-driven `Mode.GroundLevel` framing. First putt camera wrong.
- iter-2 — patched the framing math. Wobble appeared on 2nd putt.
- iter-3 — added defensive guard. Wobble moved to different state.
- iter-4 — added another guard. Wobble moved again.
- iter-5 — early-return in `ChaseCamera` for `GroundLevel + null target`. Wobble fixed *but* first-putt camera now unseeded ("doesn't update unless you move the mouse"). 5th iter introduced a new bug while fixing an old one.

**Root cause** (only spotted at iter-5 close): `GroundLevel` and `ApplyCameraYaw` were both trying to own the camera transform during putter Aiming. Every iter was an attempt to make them coexist. They structurally can't — one of them has to cede ownership.

**The fix:** delete `GroundLevel` from the putter code paths entirely. Putter uses `Mode.Chase` for everything. No camera divergence between putter and iron.

**The rule:**

> When a task enters its 3rd consecutive iteration on the same bug class (camera wobble, ball position drift, UI element misalignment, etc.), STOP. The iteration count is the signal — not a goal-post to push through.
>
> The architect must ask: "What invariant is the implementer trying to preserve that's making each fix break something else?" If the answer is a design decision from an earlier task (in this case, `loop_v1_2f`'s L4), revisit that decision. Often the design decision is the bug.
>
> Tactical fix → 6th iter. Structural revert → 0th iter of new approach.

**Specific anti-patterns to watch for in implementer reports:**

1. **Defensive guards stacking up.** Each iter's diff adds an `if (someEdgeCase) return;` or a `bool wasInPutterMode = ...; if (wasInPutterMode) ...`. Three of these in one method = structural problem, not edge cases.
2. **"This iteration's fix had a side effect."** Frequently followed by another fix that introduces another side effect. Defense-in-depth masking the root cause (cf. `Docs/Diagnostics/2026-05-12-physics-lab-postmortem.md` failure class C).
3. **The fix description references the previous iter's fix.** ("iter-5 fixed the wobble *but* now the camera doesn't seed..."). Means each iter is patching the patch.
4. **The implementer asks the architect for a fundamentally different approach.** Trust them. They've seen the code more recently than the architect has.

**Architect-execution vs implementer-routing escape valve:**

When this pattern emerges, the architect should consider executing the surgery directly rather than routing back to the implementer. The implementer's pattern-matching defaults to the "add a guard" reflex. A clean architect-written revert + guardrail can finish in one pass what 6+ implementer iterations couldn't.

**Mea culpa from this case:** the §2f L4 ("Reuse `ChaseCamera.GroundLevel`") was architect-locked, not implementer-introduced. The architect carries the design debt, not the implementer. When the architect's earlier design decision is the root cause, the architect should own the cleanup.

**Pre-iter-3 architect questions (use these at every implementer-stop callback):**
1. What invariant from a previous spec is the implementer trying to preserve?
2. Is that invariant still load-bearing, or is it the actual bug?
3. Has the implementer added defensive guards in this method that didn't exist before this task started?
4. If yes to #3, what's the structural alternative that removes both the guards and the bug?

---

## Lesson — headless play mode + iteration discipline (2026-05-20, loop_v2_smoke_bot)

**`Application.runInBackground = true` is mandatory for any automated play-mode run.**
When the Unity Editor is not the foreground OS app — i.e. every MCP/headless run — Unity
throttles the play-mode loop to a halt: the game freezes at frame 1, `Time.time` stuck
near 0, while `EditorApplication.update` keeps ticking (so MCP still answers — misleading).
Symptom misdiagnosed for a full iteration as "Game View not visible". Any tool that enters
play mode unattended must set `Application.runInBackground = true` at play-mode entry.

**Don't build fragile log-grep wait loops to detect run completion.** A background
`until grep "<line>" <(tail -c NK Editor.log)` loop silently fails when the target line
scrolls out of the tail window — the loop spins forever and no notification ever fires
(cost Cesar a 20-minute idle stall). Poll authoritative state directly
(`editor-application-get-state` → `IsPlaying`), or check the actual artifact file.

**Match the tool to the user's mode.** When the user is iterating live (watching Unity,
correcting every few minutes), do NOT delegate to a background subagent — it can't be
steered mid-run and there's no message-in channel, so you end up structurally stuck
waiting. Background agents are for genuinely independent long work, not hands-on co-iteration.

**Drive player-facing UI through its real input path, not a debug seam, when visuals must
look real.** Firing shots via `ShotController.FireDebugShot` works physically but is
instant — the cone/ball/club-handle never hide and the handle never animates. Mirroring the
real drag path (`BeginExternalDrag` → ramped `SetExternalPower` → `EndExternalDrag`, exactly
as `ClubHandleDragger` does) runs the real state machine and the UI behaves correctly.

---

## Lesson R — Always Commit `.cs.meta` When Shipping a New Unity Script via SURGICAL (2026-05-22, loop_v2_f_button_press_feedback)

**Origin:** Stage F Part A. Architect committed `Assets/Scripts/UI/ButtonPressFeedback.cs` without its `.cs.meta`. Code spotted the gap in Part B's IMPLEMENTER_REPORT (Finding 4) and included the meta in its Part B commit, closing the hole before it caused damage.

**Why it matters:** Unity script GUIDs only live in the `.cs.meta` sidecar file. Every prefab, scene, and ScriptableObject reference to a script is by GUID, not by path or class name. Without the meta in version control:

- The script appears as `<Missing Mono Script>` on any other machine — Cesar's PC, CI, a fresh Code subagent session.
- Prefab/scene asset operations later in the pipeline that reference the script (e.g. Part B's MCP `add_component` calls) silently bind to a phantom GUID. The reference works on the original dev box because Unity has its own AssetDatabase entry; everywhere else, it's a missing-script warning at best and a runtime null at worst.
- The bug is invisible until someone else opens the project. Both authoring machines (Cesar's Mac + PC) plus CI mean an architect-only test never catches it.

**The rule:**

> When Architect ships a SURGICAL new `.cs` file, the commit MUST include the matching `.cs.meta`. Before staging:
>
> 1. Confirm Unity Editor has been opened in the project at least once since the file was created — meta is generated on import.
> 2. `git status` should show BOTH `Foo.cs` AND `Foo.cs.meta` as untracked or modified. If only the `.cs` shows up, the meta hasn't been generated yet — open Unity, let it import, then re-check.
> 3. Scope the commit accordingly: `git add path/to/Foo.cs path/to/Foo.cs.meta`. Never rely on `git add .` to catch it — the meta might be gitignored or hidden under a folder you didn't intend to stage.
>
> Same rule applies to new `.asmdef` files (need `.asmdef.meta`), new prefab variants, new ScriptableObject assets, and any other new Unity-imported asset — the `.meta` IS the asset's identity.

**Symptom to watch for** in IMPLEMENTER_REPORTs after a SURGICAL script ships: prefab/scene operations succeed locally but Code reports `<Missing Mono Script>` warnings in the Editor console on first open of the modified asset. Almost always traces back to a missing meta.

**Self-check sequence Architect runs before pushing any SURGICAL new-file commit:**
```
git status path/to/new/file.cs path/to/new/file.cs.meta
# Expect both lines. If only the .cs is listed, STOP. Open Unity. Re-check.
```

**Why this happened:** Architect operates on claude.ai with Filesystem MCP, which can write files but does NOT run the Unity Editor or trigger asset imports. Cesar's Unity Editor on the Mac would have generated the meta the next time it gained focus, but the commit went out before that focus event happened. Future SURGICAL ships should either (a) ask Cesar to focus Unity briefly before push, or (b) trust the next Code session to catch it and bundle the meta into its first commit — which is what happened here.

---

## Lesson S — Every new player-facing Button gets `ButtonPressFeedback` (2026-05-22, loop_v2_f + loop_v2_f-followup)

**Origin:** Cesar's note on Stage F shipping: *"Buttons working beautifully. Make sure any new buttons in the future match this behavior."* This is a permanent UX rule, not a per-task call.

**Why it matters:** Tactile press-feedback (1.0 → 0.95 → 1.0 over 0.12s) is the floor for what a 2026-era mobile golf game's UI is expected to feel like. Without it, taps feel dead even when the game responds. The component is universal (drop-on, zero config), runs on unscaled time so it fires during paused state, respects `Button.interactable`, and costs nothing at idle.

**The rule:**

> Every Unity `Button` that a player can tap from a production surface MUST have `Golfin.UI.Polish.ButtonPressFeedback` attached. Defaults stay (`_pressedScale=0.95`, `_duration=0.12`) unless Cesar requests a feel tweak for a specific button.
>
> This applies to:
>
> - New buttons added to existing prefabs (HoleCard, HoleCompleteWidget, etc.)
> - New buttons in new prefabs or scenes
> - New buttons in screens still being built (Rankings, Shop, Gacha, Settings sub-panels, etc.)
> - New buttons in modal overlays
>
> Exception: matchmaking-modal-style auto-dismiss buttons (cancel during a timed scan) — the pulse can race the dismiss animation. When in doubt, attach it; remove later if it visibly conflicts.
>
> Implementer convention: when adding a new Button via MCP `add_component(UnityEngine.UI.Button)`, immediately follow with `add_component(Golfin.UI.Polish.ButtonPressFeedback)` in the same operation. Treat them as a pair.

**Self-check for Code at task close:** before reporting a UI task DONE, grep new `.prefab` / `ShellScene.unity` diffs for `m_Script: {fileID: 11500000, guid: <Button-GUID>}` references; for every match, confirm there's also a sibling reference to the `ButtonPressFeedback` GUID. Single missing pair = missed button.

**Test surface:** if a smoke-bot scenario exercises a new button surface (typical for new screens), the visual gate from the bot recording automatically covers Lesson S — the pulse is visible on every tapped button. A button that doesn't pulse in the bot video = lesson violation.

**Sister rule:** see Lesson R — if Architect ships a new `Button` styling component via SURGICAL, the `.cs.meta` must ship with it. Same reflex applies.

## Lesson T — Kickoff visual-gate criteria are separate deliverables from the SPEC DoD

**Symptom (puttpath_predictor_perf_and_design, 2026-05-22 → 23):** The kickoff
prompt listed Cesar's visual-gate criteria including *"Bot recording from
PutterAimGreenReaderVisible serves as the primary visual gate."* The SPEC DoD
covered the same scenario differently — "Smoke-bot scenario added, captures
rendered grid on Hole 1" — i.e. a screenshot via the scenario, not a video.
Two reviewers cleared the work on the SPEC DoD; the chain passed all the way
to `ARCHITECT_REVIEW_PASS` with a still production screenshot. At Cesar's gate
the gap surfaced: the kickoff named a *video*, the pipeline shipped a
*screenshot*. Cesar called it out directly: *"If you know the video is
required, why did it not get produced?"*

**Root cause:** the orchestrator transmitted the SPEC DoD line into each
implementer prompt faithfully but never separately surfaced the kickoff's
"bot recording" line as a distinct deliverable. The pipeline subagents work
from the SPEC; if the SPEC DoD doesn't list an artifact, no agent in the
chain will produce it, even when the kickoff names it as the primary gate.
The SPEC and the kickoff were each correct in isolation — the bug was at the
translation layer, owned by the orchestrator.

**Rule (orchestrator at kickoff parse):** when the kickoff names a specific
artifact (bot recording, Profiler capture, Frame Debugger screenshot, log
dump, scene snapshot, etc.) as a visual-gate / acceptance mechanism, treat
it as a **MANDATORY deliverable** and propagate it verbatim into the
implementer prompt **in addition to** transmitting the SPEC DoD. If the
SPEC DoD's verification artifact differs in kind from the kickoff's gate
artifact (screenshot vs video, programmatic measurement vs GUI capture,
etc.), include BOTH explicitly. Do not assume the SPEC subsumes the kickoff.

**Sub-rule:** for any task touching animation, gameplay flow, or render
lifecycle (appear / disappear / cull / animate), the bot-recorded video via
Unity Recorder is the default visual-gate artifact — not a still screenshot
— even when the SPEC DoD only names a screenshot. The project has
`BotVideoRecorder` wired into `LoopV2SmokeBotMenu.OnPlayModeStateChanged`;
setting `BotVideoRecorder.RecordVideo = true` before arming the smoke bot
auto-records the run. See `feedback_prefer_bot_videos.md`.

**Counter-rule:** if the kickoff doesn't name a video and the task is purely
static (UI layout with no state transitions, a one-off data file, a
refactor), a still screenshot is fine — don't over-deliver.

**Sister rules:** Lesson R (Architect ships `.cs.meta` files with new `.cs`)
and Lesson S (every new player-facing Button gets `ButtonPressFeedback`)
share the same pattern: a standing rule that must be translated by the
orchestrator/architect into per-task implementer deliverables. Standing
rules don't self-propagate through the pipeline.
