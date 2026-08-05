# iOS Simulator verification loop

How Claude builds, runs and verifies a change on the iOS Simulator without Cesar.
Established 2026-08-04. Background: `Docs/Reports/2026-08-04_ios_simulator_build_blocker.md`.

---

## ‼️ STANDING RULE — do NOT wipe DerivedData

**`~/Library/Developer/Xcode/DerivedData/Unity-iPhone-hhexznokwpxjrwghdhvcvkbbncib` is infrastructure,
not scratch.** It is the Xcode.app-seeded state that makes headless `xcodebuild` work at all.

Deleting it is the single move that destroys the working state and re-creates the blocker documented in
§§1–7 of the 2026-08-04 report — a multi-hour dead end. Recovery requires Cesar to run a Build & Run
from Xcode.app again.

- **Never** wipe DerivedData as a debugging reflex. Not "to rule it out." Not "to start clean."
- **Never** `xcodebuild clean` on this project for the same reason.
- If a headless build fails: **report the failure and stop.** Wiping is a decision for Cesar to make
  explicitly, never a default.

Insurance against losing it anyway: a copy of the working build lives at
**`Builds/SimApp/Golfin.app`** (outside DerivedData, gitignored). The `Data/level<N>` swap
tier below works against that copy even if DerivedData is gone.

> **Renamed 2026-08-05** (`app_identity`): `RE2.app` → **`Golfin.app`**. `productName` is `Golfin`,
> so `PRODUCT_NAME_APP`, `CFBundleDisplayName` and the executable are all plain `Golfin` — no
> sanitisation involved. (An intermediate `Golfin: The Invitational` was tried and rejected — iOS
> truncated the springboard label to `Golfin:TheI…`, collapsing the spaces. The full title lives on
> the app icon and in the store listing instead.) Bundle id `com.nextinnovation.golfingame` did NOT
> change.

---

## The three-tier loop

Pick the cheapest tier the change allows.

| Tier | Use when | Cost | How |
|---|---|---|---|
| **1 — Data swap** | Scene / asset / CSV-only change, no serialized field-layout change | seconds | Copy the freshly-exported `Data/level<N>` over the installed `.app`'s, `xcrun simctl install`, relaunch |
| **2 — Headless incremental** | C# change (needs a real compile) | ~1 min | Unity **append** re-export to `Builds/iOS-Sim` → `xcodebuild … build` against the seeded DerivedData |
| **3 — Xcode.app bootstrap** | Fresh export, or the seed was lost | Cesar, ~5 min | Cesar: Unity → **iOS Target SDK = Simulator SDK** → Build → open in Xcode → destination iPhone 14 sim → Run |

Tier 2 only works **after** a tier-3 bootstrap has succeeded on that export. That ordering is measured,
not assumed (report §10).

Tier-2 command:

```bash
xcodebuild -project Builds/iOS-Sim/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator -destination 'id=CB1B2849-80AC-4E35-87DB-7810B690442C' build
```

Unity export for tier 2 must use `BuildOptions.AcceptExternalModificationsToPlayer` (append mode) —
a plain re-export rewrites every file's mtime and forces a full ~5 min `GameAssembly` rebuild.

### ‼️ Append-export trap — phantom Burst references (hit 2026-08-05, `app_identity`)

An append re-export can write a `project.pbxproj` that references **`Libraries/lib_burst_generated.cpp`
and `.a` — files Burst never produces for the simulator SDK.** The build then dies with:

```
error: Build input file cannot be found: '…/Builds/iOS-Sim/Libraries/lib_burst_generated.cpp'
        (in target 'UnityFramework' from project 'Unity-iPhone')
```

**This is NOT a DerivedData problem. Do not wipe anything.** The evidence that settles it:

| Export | `Libraries/` | burst refs in pbxproj |
|---|---|---|
| `iOS-Demo` / `iOS-Full` / `iOS-Dev` (**device** SDK) | `lib_burst_generated.cpp` + `.a` present | 8 |
| `iOS-ShaderFix` (**simulator** SDK, clean) | `no_plugins_were_generated.txt` | **0** |
| `iOS-Sim` after a bad append | `no_plugins_were_generated.txt` | 8 ← broken |

A correct simulator export has **zero** burst references. Fix — strip them, then rebuild:

```bash
grep -v "lib_burst_generated" Builds/iOS-Sim/Unity-iPhone.xcodeproj/project.pbxproj > /tmp/pbx.new && mv /tmp/pbx.new Builds/iOS-Sim/Unity-iPhone.xcodeproj/project.pbxproj
```

Re-check with `grep -c lib_burst_generated …` (expect `0`) and re-run the tier-2 command.

**The fix is permanent, not per-export.** Append mode *preserves* the existing pbxproj rather than
regenerating it, so once the refs are stripped they stay stripped — two subsequent append re-exports
both produced `0`. The 8 refs were legacy state inherited from an earlier device-SDK export into that
same folder, carried forward by every append since. Still worth a `grep -c` before a tier-2 build if
the export folder was ever re-pointed at a device build.

---

## Simulator validity boundary

The Simulator is a real verification surface for some classes of bug and worthless for others. Know
which side a claim sits on before citing a simulator result as evidence.

**VALID on Simulator — a sim result is real evidence:**
- Layout, anchoring, sizing, UI fidelity
- Safe-area insets (the sim applies real notch + home-indicator insets)
- Scene / serialized-data correctness — anything baked into `Data/level<N>`
- Boot flow, screen navigation, missing-reference and null-ref crashes

**⚠️ NOT valid, despite looking build-time — shader-variant stripping.** It is tempting to reason
"stripping happens at build time, so the Simulator build reproduces it." It does not: the Simulator
build targets a *different SDK* (`iphonesimulator`) than Cesar's device build (`iphoneos`), so the two
can strip different variant sets. A sim build therefore cannot prove what a device build stripped.
Measured 2026-08-04 on smoke issue #6 (trees don't sway on device): the Simulator build **does** sway —
canopy pixels change 54–57% frame-to-frame with a bit-identical static camera — so the sim cannot
reproduce the device symptom at all. Verifying a stripping fix here would return a guaranteed false PASS.

**INVALID on Simulator — must be confirmed on hardware by Cesar:**
- **Touch input** — real multi-touch, gesture timing, `Touchscreen.primaryTouch` behaviour
  (see memory `reference_mouse_current_null_on_device` — the device-only input scar)
- **GPU behaviour** — the sim runs a software GPU; shader visual output, render order, overdraw,
  compression artifacts are not representative
- **Performance** — frame rate, thermals, memory pressure, load times mean nothing here

Known-benign simulator noise, not bugs: `RGBA Compressed ASTC … not supported, decompressing`, and the
dev-console `shader` NPE.

---

## Reference

- Sim: iPhone 14, udid `CB1B2849-80AC-4E35-87DB-7810B690442C`, 1170×2532 (390×844 pt for taps)
- Bundle id `com.nextinnovation.golfingame`, process/executable `Golfin`
  (was `RE2` before the 2026-08-05 `app_identity` rename)
- Unity `Debug.Log` capture: relaunch with `xcrun simctl launch --console-pty --terminate-running-process`
  and grep the piped file — `log show --predicate 'process=="Golfin"'` returns nothing
- Full mechanics: memory `reference_ios_simulator_build_verify_pipeline`
