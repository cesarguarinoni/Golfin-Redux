# phone_build_smoke_test — SPEC (iOS, physical device)

> **Status:** ACTIVE. **Notion Order 420, P1.** Target: **iOS, physical iPhone** (Cesar confirmed device on hand 2026-07-24).
> **Goal:** first real-hardware build. Prove the core loop runs on device without crashing. NOT a perf/polish pass (that's Order 930 `9d — Mobile device testing`).
> **Deep-dive audit run against HEAD `81d768f8a` on 2026-07-24** — findings below. The audit is the point of this doc: catch device-only failures the Mac editor never exercises.

---

## 0. Audit result — TL;DR

The runtime code is in **good shape** for iOS. No ATS/networking blockers, no AOT hazards, clean touch input, GPS not wired (so no missing-usage-string crash). **The blockers are all in Player Settings**, plus one guaranteed visual issue (safe area). Everything below is either a settings flip or a Cesar/Xcode action — very little code.

---

## 1. Signing / device-install prerequisites (Cesar/Xcode)

**UPDATE 2026-07-27: paid Apple Developer account is UNBLOCKED + TestFlight is available.** The old "dev account" gate is resolved — signing is now a config step, not an account wait. Two items remain before a tethered install:

1. **Bundle identifier is the template default.** Current: `com.Unity-Technologies.com.unity.template.urp-blank` (malformed — double `com.`). **Cesar sets a real reverse-DNS id** (e.g. `com.<org>.golfinredux`) in Player Settings > Identification. Claude Code should NOT invent the string. **Pick the FINAL id now** — it becomes the app's permanent identity once a TestFlight/App Store Connect record is created and can't easily change later.
2. **Signing:** in Xcode's Signing & Capabilities, enable Automatically manage signing and select the **paid Team** (no longer a Personal Team → no 7-day expiry, no 3-app limit). Account/entitlement step, not code.

Neither blocks §2 (Phase A) — that proceeds in parallel.

---

## 2. PHASE A — code/settings preflight (Claude Code CAN do these; no signing needed)

### A1. Lock orientation to Portrait — REAL device issue
`ProjectSettings.asset` currently allows all 4 orientations (`allowedAutorotateToPortrait/PortraitUpsideDown/LandscapeRight/LandscapeLeft` all `1`). The game is portrait (authored at 1170×2532). On device it WILL rotate to landscape and the portrait UI breaks — invisible in the editor Game view. Set LandscapeLeft + LandscapeRight to `0` (keep Portrait; PortraitUpsideDown optional). `defaultInterfaceOrientation` → Portrait.

### A2. SafeAreaFitter — GUARANTEED visual issue on notch/Dynamic Island devices
**Zero `Screen.safeArea` handling exists anywhere in `Assets/Scripts`.** Every current test iPhone has a notch or Dynamic Island + home indicator. Top-bar UI will render UNDER the Dynamic Island; bottom UI under the home indicator. Editor Game view never shows this.
- **For the smoke test: this is EXPECTED, not a regression.** Do not scramble to reflow the whole UI mid-build.
- **Minimal code option (recommended):** add `Assets/Scripts/UI/Core/SafeAreaFitter.cs` — a small MonoBehaviour that reads `Screen.safeArea` and insets a RectTransform. Attach to the top-level canvas panel(s) via Unity MCP. This is additive, reversible, and doesn't touch existing layout math.
- **Decision for Cesar:** whether to apply safe-area insets now or defer the full pass to Order 930. If deferring, ship the smoke build as-is and just NOTE what's clipped. Do not treat clipped edges as a bug in the smoke report.

### A3. Verify iOS Quality tier points at Mobile_RPAsset (report-only, fix if wrong)
Two URP assets exist: `Assets/Settings/PC_RPAsset.asset` and `Assets/Settings/Mobile_RPAsset.asset`. Confirm the iOS/default Quality level references **Mobile_RPAsset**, not PC. If iOS inherits the PC tier (heavier shadows/MSAA), perf tanks on device. Check `ProjectSettings/QualitySettings.asset` + the Quality matrix. Report finding; only change if it's pointing at PC.

### A4. Editor-gate MapViewCaptureDriver (hygiene, low priority)
`Assets/Scripts/Gameplay/UI/ShotUI/MapViewCaptureDriver.cs` ships in the player (Runtime folder), uses `System.Reflection` over **NonPublic** members, and calls `CaptureCore.SnapPlayModeSafe` which writes to a RELATIVE path (`Docs/Diagnostics/_capture`) that is unwritable on device. **It is NOT wired to normal gameplay** (no runtime instantiator — only doc-comment references), so it won't fire during the smoke test. But it's dead diagnostic weight + a stripping/relative-path landmine if ever triggered. Wrap the class in `#if UNITY_EDITOR` or move it to an Editor asmdef. **Defer if it risks touching the MapViewController public surface it reads — do NOT destabilise map view for a cleanup.**

### A5. MapViewController retail invariant-dump (optional hygiene)
`MapViewController.cs` writes `map_view_invariants_*.json` to `persistentDataPath` on every map open. This is SAFE on device (writable path, try/catch, repo-write guarded by `Directory.Exists`) — **not a crash, not a blocker.** Optional: gate the whole dump behind a debug flag so retail players don't get diagnostic I/O each map open. Skip if time-boxed.

---

## 3. PHASE B — build to device (Cesar, Unity → Xcode)

1. Resolve §1 blockers (bundle id + signing).
2. Unity: Build Settings → iOS → Build (produces the Xcode project).
3. Open in Xcode → select the physical device → Run.
4. Watch the **Xcode console** during launch + core loop for IL2CPP exceptions (`ExecutionEngineException`, `MissingMethodException`) or unauthorized-file-access throws. None are expected from the audit, but the console is the ground truth.
5. **locationUsageDescription:** leave empty FOR NOW — GPS is not wired to runtime (verified). Fill it ONLY when the GPS port spec (`claude_GPS_UNITY_PORT_SPEC.md`) lands, or the first location call will crash. Not a blocker today.

---

## 4. On-device smoke checklist (the payoff — run once installed)

Core-loop, crash-focused. Pass = it runs; note anything visual for Order 930.

- [ ] Cold boot → Logo → Splash → Loading → Home (ScreenManager flow completes)
- [ ] Portrait lock holds — rotate the device, UI must NOT rotate
- [ ] Safe area: note what (if anything) is clipped under Dynamic Island / home indicator (expected per A2 — record, don't panic)
- [ ] Roster screen: characters load, portraits + art render (no pink/missing-shader magenta)
- [ ] JP + EN both render (Noto dynamic atlas works on Metal — verify no tofu boxes)
- [ ] Enter Lomond Hole 1 → drive from tee → ball simulates + settles (physics runs on device)
- [ ] Map view opens and closes cleanly
- [ ] **Hole Selection cards + Hole Complete modal show REAL hole art, not the Missing placeholder** (cross-check for the HoleImages path — see multi_club SPEC §1.7; if these are wrong, the image load path is broken)
- [ ] Gacha screen, Shop, Inventory/Bag each open without crash
- [ ] Audio plays (AudioManager) — SFX on shot
- [ ] Xcode console clean of IL2CPP/managed exceptions through the above

---

## 5. Verified CLEAN — do not re-audit (checked 2026-07-24)

- **ATS / networking:** no `http://` URLs, no localhost/127.0.0.1/dev-port refs in runtime. HTTPS-only.
- **AOT / IL2CPP:** no `dynamic` keyword, no `Reflection.Emit`, no `Expression.Compile`, no generic-virtual-value-type hazards. (`iPhoneStrippingLevel: 0` = stripping disabled, so even the NonPublic reflection in the un-shipped MapViewCaptureDriver would survive.)
- **Touch input:** ZERO `Input.mousePosition` / `Input.GetMouseButton` in gameplay/UI. Touch + EventSystem only.
- **GPS/Location:** NOT wired to runtime (only a compass-heading comment in WindIndicatorWidget). Empty `locationUsageDescription` is therefore safe today.
- **Runtime file writes:** the only runtime-reachable writer (`MapViewController`) targets `persistentDataPath` (writable on iOS) with the repo-write guarded + try/catch. `CaptureCore` is `#if UNITY_EDITOR` gated with a non-editor stub. All other `dataPath`/`File.Write` sites are in `Editor/`, `Tests/`, or `Physics/Viewer` harnesses — none ship or run in a player build.
- **CSV parsing:** `HoleDatabaseLoader` splits on `\n` but `.Trim()`s every field, so CRLF is absorbed; `TournamentCsvLoader` uses the robust multi-delimiter split. Line endings are file content (platform-identical) — not an iOS issue regardless.
- **Save system:** `LocalJsonPersister` uses `persistentDataPath` — correct iOS target.
- **iOS SDK:** `iPhoneSdkVersion: 988` (Device, not Simulator) · `iOSTargetOSVersionString: 15.0` · scripting backend forced IL2CPP on iOS. All correct for physical device.

---

## 6. Implementer scope

Claude Code owns **Phase A (A1–A3 required; A4–A5 optional/defer-if-risky)**. Phase B and §1 are Cesar (account, signing, Xcode, device). §4 is Cesar's on-device run. Do NOT attempt signing, bundle-id invention, or the Xcode build from Code.

---

## 7. TestFlight distribution lane (added 2026-07-27 — paid account + TestFlight now available)

**Sequence: tethered smoke (§3–§4) FIRST, TestFlight SECOND.** Do not burn a TestFlight processing cycle + Ken's time on a build that hasn't cleared hardware smoke. Upload a build that already passed §4.

**Three upload gates — none block the tethered smoke, ALL block a TestFlight upload (verified 2026-07-27):**
1. **No app icon.** `m_BuildTargetPlatformIcons` empty; no 1024×1024 marketing icon on disk. App Store Connect **rejects uploads without a valid 1024² icon (no alpha).** Needs ART FROM CESAR (a placeholder logo is fine for beta but must be valid). Once the PNG exists, wiring it into Player Settings iOS icon slots is Code-able.
2. **Build number empty** (`buildNumber:` blank, `bundleVersion: 0.1.0`). App Store Connect requires a build number, unique + incrementing per upload. Start at `1`. Code-able.
3. **Export compliance** — no `ITSAppUsesNonExemptEncryption` flag → Xcode prompts per upload. Golf game = effectively exempt; set flag `false` once. Code-able (Info.plist / Player Settings).

**Flow once gates cleared (Cesar):** create App Store Connect app record matching the bundle id → Xcode **Product → Archive** → Organizer → **Distribute → App Store Connect → Upload** → answer export compliance → wait for processing → TestFlight tab → add Ken as tester (internal = instant; external = light Beta App Review). Ken installs TestFlight app + accepts invite → plays OTA.

**This is arguably its own Notion order** (App Store Connect record, icon asset, tester onboarding) distinct from the smoke test. Fileable as `testflight_distribution` if Cesar wants it tracked separately.
