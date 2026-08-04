# iOS Simulator build blocker — headless `xcodebuild` fails in Unity's Bee/IL2CPP stage

**Date:** 2026-08-04 · **Author:** Claude Code · **For:** Architect review
**Status:** UNRESOLVED. Root cause not identified. Workaround proposed in §8.

---

## 1. What this is about

I need to produce and run an **iOS Simulator** build myself so I can reproduce and verify
device-only bugs without occupying Cesar. That capability was reportedly established earlier
(`Docs/Reports/2026-08-03_centralball_fix_and_compile_optimizations.md`, "Reusable win discovered"),
and it is how the CentralBall fix was verified.

It does not work now. I could not fix it, and I twice told Cesar it was fixed when it was not.
This report exists so the architect can take it further rather than me continuing to improvise.

**Important scoping fact: Cesar's own build path is NOT broken.** He builds Unity → Xcode → device
and that works; he built and on-device tested the tree-wind change today. The broken path is
specifically **headless `xcodebuild` driven from the CLI by me.**

---

## 2. Environment

| | |
|---|---|
| Unity | `6000.3.9f1` |
| Xcode | `16.4` (build `16F6`), `/Applications/Xcode.app/Contents/Developer` |
| Host | Apple Silicon (`arm64`), macOS `15.7.4` |
| Target | iPhone 14 simulator, iOS `18.6` (`CB1B2849-80AC-4E35-87DB-7810B690442C`) |
| Scripting backend | IL2CPP |
| Export | `BuildPipeline.BuildPlayer` → `Builds/iOS-Sim`, `iOSSdkVersion.SimulatorSDK`, `simulatorSdkArchitecture = ARM64` |

Unity export **always succeeds** (~50 s–2 min, 0 errors). The failure is always in the subsequent
`xcodebuild` compile of `GameAssembly`.

---

## 3. Failure signature

Always the same class: a header that **demonstrably exists on disk** is reported missing, and it is
usually a *sibling of a file the compiler just successfully opened*.

```
In file included from Il2CppOutputProject/Source/il2cppOutput/UnityEngine.UIElementsModule__22.cpp:1:
In file included from Il2CppOutputProject/IL2CPP/libil2cpp/pch/pch-cpp.hpp:7:
Il2CppOutputProject/IL2CPP/libil2cpp/utils/StringUtils.h:31:10: fatal error: 'StringViewUtils.h' file not found
```

Other observed variants (all the same shape):

- `'Baselib.h' file not found` — from `libil2cpp/os/Atomic.h:8`
- `'il2cpp-config.h' file not found` — from `libil2cpp/pch/pch-cpp.hpp:3`
- `'il2cpp-string-types.h' file not found` — from `libil2cpp/utils/StringUtils.h:24`
- `'il2cpp-config-api.h' file not found` — from `libil2cpp/il2cpp-config.h:9`
- `unable to rename temporary '…/xxxx-hash.o.tmp' to '…/xxxx.o': 'No such file or directory'`
- `Failed to create output directory for targetfile Il2CppTempDirArtifacts/Debug/artifacts/arm64/…`

### 3.1 The most diagnostic single line

```
Il2CppOutputProject/IL2CPP/external/baselib/Include/C/Internal/Compiler/Baselib_Atomic_Gcc.h:3:10:
  error: '../../../C/Baselib_Atomic.h' file not found, did you mean 'C/Baselib_Atomic.h'?
```

`Baselib_Atomic_Gcc.h` lives at `Include/C/Internal/Compiler/`. Three levels up is `Include/`, so
`../../../C/Baselib_Atomic.h` = `Include/C/Baselib_Atomic.h`, **which exists** (verified, 38,785 bytes).
Clang can see the file via the search path (`did you mean 'C/…'`) but the relative hop fails.

**That is only possible if the compiler is not reading these files from their real directory** — i.e.
inputs are being staged/symlinked into a flattened tree at a different depth. This rules out
"files are missing" and points squarely at Bee's input staging.

---

## 4. The mechanism (what Unity's generated Xcode project actually does)

From the `PBXShellScriptBuildPhase` in the generated `project.pbxproj`:

```sh
export BEE_CACHE_BEHAVIOUR="RW"
export BEE_CACHE_DIRECTORY="$HOME/Library/Unity/cache/bee"      # MACHINE-GLOBAL, outside the project
mkdir -p "$CONFIGURATION_TEMP_DIR/artifacts/arm64/buildstate/"
mkdir -p "$PROJECT_DIR/Il2CppTempDirArtifacts/$CONFIGURATION"
ln -sF  "$CONFIGURATION_TEMP_DIR/artifacts" "$PROJECT_DIR/Il2CppTempDirArtifacts/$CONFIGURATION/artifacts"
...
ARGS=(
    --compile-cpp --platform=iOS
    --baselib-directory="Libraries"
    --generatedcppdir="Il2CppOutputProject/Source/il2cppOutput"
    --custom-il2-cpp-root="Il2CppOutputProject/IL2CPP"
    --cachedirectory="Il2CppTempDirArtifacts/$CONFIGURATION/artifacts/$LIB_ARCH"
    --outputpath="$CONFIGURATION_BUILD_DIR/libGameAssembly.a"
    --architecture="$LIB_ARCH" --configuration="$IL2CPP_CONFIG"
)
```

Three properties matter:

1. **All IL2CPP paths are RELATIVE** (`Libraries`, `Il2CppOutputProject/…`, `Il2CppTempDirArtifacts/…`)
   and the script **never `cd`s** — it depends entirely on the working directory Xcode gives the
   script phase. Any CWD difference between Xcode.app and `xcodebuild` breaks all of them at once.
2. **IL2CPP artifacts are not in the export.** They are a **symlink into DerivedData**
   (`$CONFIGURATION_TEMP_DIR`). Export and DerivedData are therefore coupled: wiping one while
   keeping the other leaves a dangling symlink. This also means **the built `.app` only ever lives in
   DerivedData**, which is why it repeatedly "disappeared" between my attempts (Cesar's own diagnosis).
3. **The Bee cache is machine-global** (`~/Library/Unity/cache/bee`), shared with Cesar's device
   builds — so it is not isolated per project or per build.

The clang invocation logged by `--print-command-line` uses relative includes:

```
-I"." -I"Il2CppOutputProject/Source/il2cppOutput" -I"Il2CppOutputProject/IL2CPP/libil2cpp/pch"
-I"Il2CppOutputProject/IL2CPP/libil2cpp" -I"Il2CppOutputProject/IL2CPP/external/baselib/Include" …
```

(Whether the literal `"` characters are real arguments or an artifact of Unity's own echo is
**not established** — worth checking, see §7.)

---

## 5. What has been ruled out (with evidence)

| Hypothesis | Test | Result |
|---|---|---|
| `incrementalIl2cppBuild` (enabled in `35beb2723`) | Toggled on/off across builds | **Not causal.** Failures occur with it OFF; one success occurred with it OFF but so did many failures |
| `Dev-iOS` profile `m_Development 1` | Toggled 1/0 | **Not causal.** Active build profile is `(none / classic)`; profile isn't even used by `BuildPipeline` |
| Project-local `Library/Bee` (1.4 GB) | Deleted, rebuilt | Same failure |
| Machine-global `~/Library/Unity/cache/bee` (145 MB) | Quarantined, rebuilt | Same failure. **Restored afterwards** |
| Stale Xcode DerivedData | Deleted before each attempt | Same failure |
| Xcode script sandboxing | `ENABLE_USER_SCRIPT_SANDBOXING=NO` | Same failure |
| Wrong simulator arch (x86_64 under Rosetta) | Set `simulatorSdkArchitecture = ARM64` | Export now `artifacts/arm64`; same failure |
| Custom `-derivedDataPath` outside the project | Removed; used Xcode default | Same failure |
| Working directory | Ran `xcodebuild` from repo root **and** from inside the export dir | Both fail; failing path merely shifts |
| `-arch` conflicting with `-destination` | Removed `-arch` | Fixed *that* usage error only |
| Incomplete / corrupt Unity export | Compared sim export vs known-good device export | **Identical structure.** baselib `Include` = 126 files in both; real directories, no symlinks; every "missing" header present at the exact referenced path |
| Export/DerivedData race (build started too soon) | Rebuilt a fully settled export, no re-export | Same failure |

**Success rate: 1 of ~10 attempts.** The single success (arm64, incremental OFF, `m_Development 0`,
default DerivedData) produced a working `.app` that installed and ran to the title screen. I have
not been able to reproduce it, and I cannot explain what was different — which is the single most
suspicious fact in this report and the best lead for whoever picks it up.

---

## 6. Why this matters

Without it I cannot verify device-only defects myself. Concretely, this session:

- The `WalkCamera` `ArgumentNullException: Parameter name: shader` fix (`c941e55de`) is proven
  statically (shader-inclusion audit) and compiles, but is **not** confirmed in a player build.
- The per-hole tree-wind data is verified numerically and in editor play mode, but the per-hole
  variation is unverified on hardware.

Both currently depend on Cesar building, which is exactly the dependency the pipeline was meant to remove.

---

## 7. Open questions for the architect

1. **What was different about the one successful build?** Nothing I varied deliberately explains it.
   Ambient candidates: machine load (a concurrent Unity import/compile), Bee cache warm vs cold, or an
   Xcode/simulator state that has since changed.
2. **Are the `-I"…"` quotes literal arguments or a display artifact of `--print-command-line`?**
   If literal, every include path is wrong-by-construction and the question becomes why it ever worked.
   Cheap to settle: inspect the Bee dag/response file rather than Unity's echo.
3. **What CWD does Xcode.app give the script phase vs `xcodebuild`?** The script relies on it entirely
   and never `cd`s. Adding `cd "$PROJECT_DIR"` to the phase would be a one-line experiment — but it edits
   generated output, so it would need to be reapplied per export (or scripted post-export).
4. **Does `xcodebuild` on this Xcode/Unity combination need `-workspace`/scheme handling we're missing?**
   We build `-scheme Unity-iPhone -configuration Debug -sdk iphonesimulator -destination id=…`.
5. **Is a simulator build even the right target**, or should the loop be a device build + physical phone,
   given Cesar builds for device anyway?
6. **Is there a supported Unity path we should be using instead** — e.g. `BuildOptions.AutoRunPlayer`
   (tried: it exported but did **not** invoke `xcodebuild`), or Unity's `-buildTarget`/batchmode CLI.

---

## 8. Proposed workaround (bootstrap from the path that works)

Rather than fix headless `xcodebuild`, use Cesar's working Xcode flow **once** to produce the artifact,
then decouple from it:

1. Cesar: Unity → **iOS Target SDK = Simulator SDK** → Build → open in Xcode → destination
   **iPhone 14 simulator** → Run. (Identical to his phone flow except the destination.)
2. Claude: copy the produced `.app` to a **stable location outside DerivedData** so it survives
   simulator resets, Xcode cleanups and DerivedData wipes — the failure mode Cesar identified.
3. Thereafter, for **scene / data / asset-only** changes: swap the changed `Data/level<N>` into the
   preserved `.app` and relaunch — **seconds, no compile**. This is the documented fast path from the
   2026-08-03 report and covers the majority of verification needs.
4. Only **C# changes** would require a fresh compile, i.e. a new build from Cesar.

This restores most of the self-verification capability without solving the underlying blocker.

---

## 9. Current state of the machine / repo

Everything touched during investigation has been reverted:

- `ProjectSettings.asset`: `iPhoneSdkVersion` restored to **988 (DeviceSDK)** — Cesar's phone builds
  are unaffected. Matches commit `af4563d49`.
- `~/Library/Unity/cache/bee`: **restored** to its original contents.
- All experimental exports removed (`Builds/iOS-Sim`, `Builds/iOS-ShaderFix`, `Builds/iOS-Simulator`)
  — several GB reclaimed. `Builds/iOS-Demo` (Cesar's device export) untouched.
- Build residue Unity dropped into `Assets/` (`PerformanceTestRunInfo.json`,
  `PerformanceTestRunSettings.json`, `packages-merged-link/`) removed; none were tracked.

**One standing repo change from this investigation**, committed in `af4563d49`:
`incrementalIl2cppBuild {iPhone: 1} → {iPhone: 0}`. It is **conservative, not a proven fix** — it only
restores the setting that was in place when headless builds last worked. It costs device build speed
and nothing else. Re-enabling it is safe if the real cause is found.

---

## 10. Controlled measurement: headless `xcodebuild` on a warm, Xcode-seeded state — **PASS**

**Setup.** Cesar completed a successful **Build & Run via Xcode.app** on the export at `Builds/iOS-Sim`
(re-exported after the §9 cleanup). The `.app` launched to the title screen. Caches warm, DerivedData
fresh. Nothing was cleaned, deleted, re-exported, or modified before the measurement — the *only*
variable changed from the known-good run was the **driver**.

**Command (run exactly once, no retries, no variations):**

```
xcodebuild -project Builds/iOS-Sim/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator \
  -destination 'id=CB1B2849-80AC-4E35-87DB-7810B690442C' build
```

**Result: `** BUILD SUCCEEDED **`** — Outcome A.

Environment as reported by the build's own exported settings:

| Key | Value |
|---|---|
| Xcode | 16F6 (`XCODE_VERSION_ACTUAL=1640`) |
| SDK | `iPhoneSimulator18.5.sdk` |
| Destination | `CB1B2849-…` — iPhone14,7, iOS 18.6, `iphonesimulator` |
| DerivedData | `Unity-iPhone-hhexznokwpxjrwghdhvcvkbbncib` (the one Xcode.app just seeded) |
| Unity runtime | 6000.3.9f1, scripting backend `il2cpp` |
| Product | `Debug-iphonesimulator/RE2.app`, signed "Sign to Run Locally" |

The Il2Cpp/GameAssembly script phase (`Script-D4BF5B85….sh`) — the phase that failed in every earlier
headless attempt — **ran to completion under `xcodebuild`**, followed by CodeSign, Validate and Touch.
Only the four benign "Run script build phase … will be run during every build because it does not
specify any outputs" warnings were emitted. No errors.

### What this establishes

- Headless `xcodebuild` is **not** categorically broken on this machine/Unity/Xcode combination.
- The failures documented in §§1–7 are **state-dependent, not driver-dependent in isolation**. On a
  cold/unseeded tree the script phase fails under `xcodebuild`; on a tree Xcode.app has already built
  successfully, the same command succeeds. Whatever the script phase needs (working directory,
  environment, or — most likely — build intermediates/caches Xcode.app populates on the first pass),
  Xcode.app supplies it on the bootstrap build and `xcodebuild` then inherits it.
- This does **not** identify the missing ingredient. It narrows it to "something the first successful
  build leaves behind," which is consistent with, but does not prove, the Bee/Il2Cpp cache hypothesis.

### Operational consequence

The §8 workaround stands, with the ordering now confirmed by measurement:

1. **Bootstrap** — Cesar runs Build & Run once from Xcode.app on a fresh export. (Still required; still
   the dependency we wanted to remove, but now only once per export rather than per change.)
2. **Incremental** — thereafter **`xcodebuild` is a working headless driver** against that DerivedData,
   including for **C# changes**, which the level-swap fast path cannot cover.
3. **Scene / data-only** changes continue to use the `Data/level<N>` swap — still the fastest path, no
   compile at all.

Caveat: this is a single successful observation. It is not yet known whether the warm state survives a
DerivedData wipe, a simulator reset, or an Xcode restart — and deliberately was not tested, since doing
so would have destroyed the state under measurement. Treat "headless works after bootstrap" as
provisional until it has held across several real edit→build cycles.

**Investigation status: closed.** This section records a measurement, not a resumption. No further
attempts were made.

---

## 11. Premise test for the first incremental-tier trial — tree-wind is NOT Simulator-valid

The first real trial of the tier-2 (headless incremental) loop was to be smoke issue **#6, "trees don't
sway on device,"** classified at kickoff as Simulator-valid on the reasoning that its suspected causes
(build-time shader-variant stripping, or the `Mobile_RPAsset` quality tier dropping the wind path) bake
into the player build rather than depending on GPU behaviour.

`Docs/AI_CONTEXT.md:51` disagreed, listing #6 alongside the camera-drag issue as won't-reproduce. The
disagreement was settled by measurement **before** any edit was made.

**Method.** Launched the preserved simulator build through the real player path (title PLAY → home tee →
PRACTICE → Lomond Hole 1, a heavily wooded hole; HUD wind read 8.9 mph). Camera left idle at aim.
Captured 6 full-res frames 1 s apart via `simctl io … screenshot` and diffed regions numerically.

**Result — trees sway in the Simulator:**

| Region | mean abs Δ | max Δ | pixels changed |
|---|---|---|---|
| Left canopy | 7.6–8.2 | 172 | **54–57 %** |
| Right canopy | 1.6–1.9 | 163 | 9–10 % |
| Fairway (control) | **0.0000** | **0** | **0.000 %** |
| Sky (control) | **0.0000** | **0** | **0.000 %** |

The two controls being *bit-identical* rules out camera drift, jitter and capture noise — the canopy
delta is real motion. A change-overlay confirms the moving pixels trace **leaf detail**: foliage masses
and canopy edges change while trunks, ground, ball and HUD do not, and the motion band is confined to
y 400–1000 with y > 1200 bit-identical. That is vertex-animated foliage sway, not LOD cross-fade dither
(which would blanket whole billboards, trunks included, in blocky regions).

**Consequences.**

1. **The trial did not run.** The sequence was stopped before the edit. With sway present in the sim
   *before* any change, the "verify in sim" step has no signal — it would have returned PASS on an
   unfixed binary. This is precisely the false-PASS trap AI_CONTEXT flags in red, and it caught a live
   attempt to walk into it. **A substitute vehicle is needed for the tier-2 trial** (a change whose
   effect the sim can actually show a before/after on).
2. **Validity boundary corrected.** "Build-time ⇒ Simulator-valid" is wrong for shader stripping,
   because the sim build targets `iphonesimulator` and Cesar's targets `iphoneos` — different SDKs can
   strip different variant sets. `Docs/Pipeline/IOS_SIMULATOR_LOOP.md` has been amended.
3. **Free narrowing of #6 itself.** The sim build is a real il2cpp iOS player build off the same scenes
   and quality assets, and its wind path survives. So any cause that would break *every* iOS player
   build is unlikely — including a scene-data cause (already suspected) and the wind path simply being
   absent from the player pipeline. The **`Mobile_RPAsset` quality-tier hypothesis is weakened**, though
   not eliminated: that holds only if the sim build resolves to the same quality tier as the device
   build, which was **not** verified here. What survives best is a cause that *differs between the two
   SDK targets* — i.e. device-target variant stripping — or something genuinely device-GPU-side.
   None of this was investigated further; it falls out of the measurement above.

**Seed fragility: no data.** The append re-export step was never reached, so nothing was learned about
whether it survives the warm state. FINDING #1 remains open and untested.

---

## 12. Tier-2 loop trial — **VALIDATED**, and FINDING #1 is negative

Because tree-wind proved Simulator-invalid (§11), the first tier-2 trial was run as a **pure loop
exercise**: a deliberately trivial runtime C# edit with no product value, carried end-to-end and then
reverted. The point was to isolate the build-loop question from any feature question.

**Probe.** One line added to `ScreenManager.Awake()` (a guaranteed-compiled, boot-path runtime script —
`BuildStamp.cs` was rejected as a target because it compiles out unless `GOLFIN_TESTBUILD` is defined,
which this export does not set):

```csharp
Debug.Log("[TIER2TRIAL] headless-incremental probe token=A7F3C1");
```

**Round 1 — does an addition propagate?**

| Stage | Result |
|---|---|
| Unity compile | clean, 0 errors |
| Append re-export → `Builds/iOS-Sim` | `Succeeded`, 0 errors, 21 scenes, ~130 s |
| mtime preservation | 3466 files before and after; `Classes/*.h`, `main.mm` mtimes **unchanged**; 894 changed (all `Data/*`, `project.pbxproj`, `Libraries/`) |
| Token in export | present in `Data/Managed/Metadata/global-metadata.dat` |
| **Headless `xcodebuild`** | **`** BUILD SUCCEEDED **` in 56.5 s** (1 `CompileC`) |
| Install + launch | token present in built `.app` |
| **Runtime console** | **`[TIER2TRIAL] … token=A7F3C1` at line 518** |

**Round 2 — negative control, does a removal propagate?** Probe reverted (source byte-identical to
HEAD), then the identical loop re-run: export `Succeeded` ~75 s → `xcodebuild` **succeeded in 48.5 s** →
token **absent** from `global-metadata.dat` **and** absent from the runtime console, with the app still
booting normally (1162 console lines). The loop propagates additions *and* removals; it is not
returning a stale binary in either direction.

### FINDING #1 (seed fragility): **negative — the append re-export does NOT break the warm state**

This was the pre-registered risk. Two consecutive Unity append re-exports were performed over the
Xcode.app-seeded tree, and headless `xcodebuild` succeeded after **both**. Append mode did its job:
the C++ sources kept their mtimes, so only what actually changed recompiled — one `CompileC` and a
~50 s turnaround against the ~5 min cold rebuild.

Caveat unchanged from §10: still untested against a DerivedData wipe, simulator reset, or Xcode restart.
What is now established is narrower and useful — *ordinary iteration* (edit → export → build) does not
consume or corrupt the seed.

### ⚠️ Two cleanup items left open (Unity went busy mid-cleanup — see below)

1. **`ProjectSettings.asset` → `iPhoneSdkVersion: 989` (SimulatorSDK); it must be `988` (DeviceSDK).**
   The export script set Simulator SDK and restored `DeviceSDK` in a `finally`, but the restore did not
   reach disk. **This is drift I introduced and it matters: a device build left at 989 targets the wrong
   SDK.** The other two hunks in that file (`buildNumber.iPhone: 2029`, `AndroidBundleVersionCode: 2029`)
   are **pre-existing** — `ProjectSettings.asset` was already dirty at session start, from the build
   stamp generator during Cesar's own Xcode build.
   This is a **third instance of `build_stamp_hardening` defect (B)** — the postprocess/`finally` restore
   pattern updates the in-memory `PlayerSettings` but does not force the asset to disk. Any fix should
   `AssetDatabase.SaveAssets()` (and ideally verify by re-reading the file), not assume assignment
   persists.
2. **Build residue back under `Assets/`** (untracked, not gitignored, so it would pollute a future
   commit): `Assets/Resources/PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json` (+ metas),
   `Assets/packages-merged-link/`. Same residue §9 removed once already — Unity re-drops it on every
   iOS export. Worth gitignoring rather than hand-deleting each time.

Neither was completed because Unity became unresponsive to MCP partway through the cleanup — its log
shows it opening every hole scene in sequence (`Hole_15_Geo`, `Hole_16_Geo`, `Hole_17_Geo`, ~5 s each),
saturating the main thread. Four consecutive MCP calls failed; the circuit-breaker rule was applied and
the attempt stopped rather than retried blindly. **The `iPhoneSdkVersion` fix must be made through the
Unity API, not by editing the YAML** — Unity is open and holds the authoritative in-memory copy, so a
raw edit would be silently overwritten on its next project save.

---

## 13. Runaway build storm — cause, stop, and an orphan-process discovery

### What happened

After the §12 trial, Unity ran **complete player builds back-to-back**, ~125–141 s each, chained
continuously. Cesar saw it as "Unity recompiling all the time." It was not recompiling — it was
rebuilding the whole player, repeatedly.

**Cause (self-inflicted).** `BuildPipeline.BuildPlayer` blocks Unity's main thread, so the MCP
`script-execute` call times out — and **the MCP plugin retries the invocation 10 times, each retry
re-executing the build.** Two export calls therefore queued up to ~20 full player builds, which then
drained sequentially. The existing memory note warns *"never re-invoke (double build)"* about the
**caller** re-invoking; it does not account for the **plugin's own retry loop** doing exactly that.

🔴 **RULE — never call `BuildPipeline.BuildPlayer` through `script-execute`.** The retry-on-timeout
behaviour makes it a build-storm generator. Any long blocking editor operation driven over MCP must be
launched **fire-and-forget** — e.g. schedule it via `EditorApplication.delayCall` / a menu item and
return immediately, so the MCP call completes instantly and there is nothing to retry — with the marker
file remaining the completion signal.

**Stop.** `SIGTERM` to the Unity pid (graceful; no `SIGKILL` needed). Log stopped advancing
immediately; queue cleared.

### Cleanup completed

`ProjectSettings.asset` `iPhoneSdkVersion` restored **989 → 988 (DeviceSDK)**. With Unity closed a
direct YAML edit is safe and authoritative (the earlier objection — Unity holding the in-memory copy and
overwriting on save — no longer applies). Verified: the only remaining diff in that file is the two
**pre-existing** hunks (`buildNumber.iPhone: 2029`, `AndroidBundleVersionCode: 2029`) from the stamp
generator during Cesar's own build. `ScreenManager.cs` confirmed byte-identical to HEAD.

### 🔍 Discovery: ten orphaned il2cpp processes hung since this morning — possibly relevant to §§1–7

Killing Unity surfaced processes that are **not from this session**:

| Started | Count | Parent | CPU | Points at |
|---|---|---|---|---|
| 06:31–06:38 today | 9 | `PPID 1` (reparented — parent died) | **0.0 %** | `Builds/iOS-SimVerify/…/il2cpp`, `il2cpp-compile` |
| 11:08 today | 1 | `PPID 1` | 0.0 % | DerivedData `…cynopocyerlohfduadmgfwkrnkxb` |

They had been hung for **~7 hours**, and they reference paths that **no longer exist**:
`Builds/iOS-SimVerify` was deleted in the §9 cleanup, and both DerivedData dirs they name
(`…ekflpcifltjyfnbfdnppzrwgmvfe`, `…cynopocyerlohfduadmgfwkrnkxb`) are gone — only the seeded
`…hhexznokwpxjrwghdhvcvkbbncib` remains. At 0.0 % CPU they are blocked, not spinning, so they were not
causing the slowness Cesar noticed; that was the build storm.

**Timing.** 06:31–06:38 is squarely inside the original §§1–7 investigation window — these are the
carcasses of the headless builds that failed then.

⚠️ **Stated as a hypothesis only; the investigation stays closed.** Hung il2cpp processes holding the
il2cpp/Bee cache would be a plausible mechanism for a script phase that fails under `xcodebuild` but
succeeds under Xcode.app, and would fit §7 open question 1 (*"What was different about the one
successful build? Nothing I varied deliberately explains it."*) — a stale lock that happened to be free.
It is **not evidence**, it was **not tested**, and nothing here was chased. Recording it so that if the
blocker ever recurs, `ps` for orphaned il2cpp is the first cheap check rather than another multi-hour dig.

The orphans were left running pending Cesar's call — they are inert, and reaping them is a one-line
`kill` whenever he wants.

### §13 addendum — orphan reap completed, and two process-hygiene corrections

**Reaped: 34 orphaned il2cpp/GameAssembly processes**, not the 19 first counted. The initial figure was
low because the first survey only matched paths seen in one `ps` slice. The full set dated back to
**Aug 3 21:01** and spanned every abandoned export of the investigation — `iOS-SimVerify`,
`iOS-ShaderFix`, `iOS-Simulator`, `/tmp/simbuildlog/DD`, and DerivedData dirs `ekflp…`, `cynopo…`,
`azvghr…`, all of which no longer exist on disk. Final `il2cpp` process count: **0**. The live Unity
tree (relaunched by Cesar mid-cleanup, pid 49412 + 8 children) was verified untouched.

Notably the leak is **not** specific to failed builds: three orphans traced to Cesar's own *successful*
Xcode.app Build & Run at 11:24–11:29. il2cpp appears to leak a hung child on every build regardless of
outcome.

Two corrections worth keeping, both of which produced wrong answers before being caught:

1. **A pid comparison is not an age test.** The first reap guard skipped anything with a pid higher than
   the live Unity's, assuming "higher pid = newer." Pids had wrapped: Unity sat at 49412 while genuine
   day-old orphans held 63323 and 99712, so the guard protected exactly the processes it should have
   killed. **Use `ps -o lstart=` and compare start times.**
2. **A verification loop must not share the broken loop's syntax.** The first `kill` pass used
   `for p in $PIDS` — zsh does not word-split, so it signalled the literal string `"2256 2264 …"`,
   failed into `/dev/null`, and the `kill -0` check *used the same construct*, so it also failed and the
   script reported **"none — all exited on SIGTERM"** while all 19 were alive. An independent `pgrep`
   exposed it. When a check shares the bug's mechanism, it confirms the bug.
