# GOLFIN Redux — iOS → TestFlight Runbook

**Target:** `iOS-Full` build → TestFlight internal testers
**Machine:** Cesar's MacBook Pro · repo `/Users/cesar/Documents/GolfinRedux` · Unity **6000.3.9f1**
**Record:** Golfin Game — `com.nextinnovation.golfingame`, NEXT INNOVATION PTE. LTD. (Cesar = Admin)
**Verified against the repo, the Mac, and live App Store Connect:** 2026-08-17

---

## ⚠️ Read this first — 3 blockers, all FIXED 2026-08-17 (see Phase 1)

| # | What | Was | Now |
|---|---|---|---|
| 1 | **Version string** | `bundleVersion: 0.1.0` | ✅ **`1.5.7`** — live App Store is 1.5.4, but **TestFlight already has `1.5.6 (5)`** uploaded 2025-12-11. A new upload must clear the highest *uploaded* train, not just the live one. `1.5.5` would be rejected. |
| 2 | **Signing (Unity side)** | `appleEnableAutomaticSigning: 0`, `appleDeveloperTeamID:` *(empty)* | ✅ Automatic signing ON, Team ID **`TCUV4A9VTJ`**. Your Aug 4 Xcode project already has `DEVELOPMENT_TEAM = TCUV4A9VTJ` set by hand — but Unity overwrites that on every **Replace** build. Setting it in Unity is what makes it stick. |
| 3 | **Dev define shipping to device** | Scripting Define Symbols, iPhone: `UNITY_MCP_READY` | ⚠️ Stripped on disk — but **re-added by the MCP package on every domain reload**, and re-checking the box before building does not help (see Phase 1). **Accepted as-is for this upload**; tracked as Order 428. |

Export compliance (Phase 3 step 3) is now automated too — `Assets/Editor/iOSPostProcess.cs`
writes `ITSAppUsesNonExemptEncryption = false` into the generated `Info.plist` on every iOS build.

### ✅ Correction: icons are NOT a blocker

An earlier draft of this doc called the empty `m_BuildTargetPlatformIcons → iPhone → m_Textures: []` a blocker. **That was wrong.** Empty platform icons only mean "no iOS-specific override" — Unity falls back to the project default icon, which is set:

- Source: `Assets/Icons/Golfin-Icon2.png` — **1024×1024** ✅
- Unity's generated output in `Temp/StagingArea/` (from your build at 09:16 today) contains the **full iOS icon set**, `Icon-Store-1024.png` through `Icon-iPhone-Notification-40.png`
- Generated `Icon-Store-1024.png`: 1024×1024, **`hasAlpha: no`** ✅ — Unity flattens the alpha on the source automatically, so `ITMS-90717` won't fire

No icon work needed. Nothing to assign.

### Other things verified fine

- Bundle ID `iPhone: com.nextinnovation.golfingame` ✅
- `buildNumber → iPhone: 2113` ✅ set (the "empty build number" note in `DEMO_BUILD_PLAN.md` is stale)
- `uIRequiresFullScreen: 1`, `uIStatusBarHidden: 1` ✅
- Camera / location / mic usage descriptions empty — **safe**, no `Input.location` / `LocationService` / permission API usage anywhere in `Assets/**/*.cs`. Once the GPS work lands (`GPS_UNITY_PORT_SPEC.md`), `locationUsageDescription` becomes mandatory or the upload gets `ITMS-90683`.
- `iOSTargetOSVersionString: 15.0` — live app declares iOS 17+. Lowering a minimum on an update is allowed. Consider matching 17.0 anyway.

---

## Phase 0 — Toolchain ✅ DONE

Since **April 28, 2026** Apple rejects any upload not built with **Xcode 26+ / iOS 26 SDK**.

| Check | Result |
|---|---|
| Xcode | **26.6** (build 17F113) ✅ |
| iOS SDK | **26.5** ✅ |
| `xcode-select -p` | `/Applications/Xcode.app/Contents/Developer` ✅ *(fixed 2026-08-17)* |
| Unity iOS Build Support (6000.3.9f1) | installed ✅ |
| Provisioning profile for `com.nextinnovation.golfingame` | exists ✅ |

### Your Team ID is `TCUV4A9VTJ`

NEXT INNOVATION PTE. LTD. — read off the provisioning profile on your disk.

⚠️ **Three Apple Development identities in your keychain, on three different teams:**

| Certificate | Team ID | Organization |
|---|---|---|
| `Apple Development: Cesar Guarinoni (NWQPSKM8S9)` | **`TCUV4A9VTJ`** | **NEXT INNOVATION PTE. LTD.** ← this one |
| `Apple Development: cesar@clumsydwarf.com (K96GY3R6SU)` | `K5UYF3DNXA` | Cesar Guarinoni (personal) |
| `Apple Development: cesar.guarinoni@wonderwallgp.com (X82N8SDN94)` | `458CY8ZZ2B` | Cesar Guarinoni |

### Distribution certificate — cleared ✅

You have no `Apple Distribution` cert yet, only Development ones. Xcode mints one on first archive **if the signing account has the rights**. Your ASC roles, checked 2026-08-17:

| Apple ID | Role on NEXT INNOVATION | Can create a distribution cert? |
|---|---|---|
| **`cesar.guarinoni@wonderwallgp.com`** | **Admin** | ✅ yes |
| `cesar@clumsydwarf.com` | Developer | ❌ no |

Both Apple IDs are signed into Xcode, and `TCUV4A9VTJ` resolves under the wonderwallgp account as a **Company** team (`isFreeProvisioningTeam = 0`). Phase 3 will work — **as long as Xcode signs with that account and team.** The two "Cesar Guarinoni (Personal Team)" entries in the dropdown are free teams and cannot do App Store distribution at all.

Other Admins if you get stuck: Ken Komatsu (`ken@next-innovation.tech`). Account Holder is 賢 小松 (`greedisland.k.k@gmail.com`).

---

## Phase 1 — Fix the blockers in Unity ✅ DONE 2026-08-17

Applied directly to `ProjectSettings/ProjectSettings.asset` (4 changed lines) **and** synced into
the running Editor's in-memory `PlayerSettings`, so the next save writes them back rather than
reverting. Nothing to do by hand in *Edit → Project Settings → Player*.

| # | Setting | Now |
|---|---|---|
| 1 | `bundleVersion` | **`1.5.7`** ← clears `1.5.6`, the highest train already uploaded to TestFlight (not just the live `1.5.4`) |
| 1 | `buildNumber → iPhone` | `2113` — unchanged, fine as is |
| 2 | `appleEnableAutomaticSigning` | **`1`** |
| 2 | `appleDeveloperTeamID` | **`TCUV4A9VTJ`** |
| 3 | Scripting Define Symbols, iPhone | **empty** (was `UNITY_MCP_READY`); every other platform untouched |

### ⚠️ Blocker #3 does not stay fixed by itself — check it right before you build

`com.ivanmurzak.unity.mcp` (`RecompileGate.EnsureReadyDefine`, via an `[InitializeOnLoad]`
resolver) **force-re-adds `UNITY_MCP_READY` to every build target on every domain reload** —
i.e. after any script recompile, and in batchmode builds too. The disk value above is correct
and committed, but the in-memory value it builds from will silently come back.

**Do not bother clearing the box before building — it cannot work.** Clearing a define itself
triggers a recompile and domain reload, which re-runs the resolver, which re-adds it. The system
is only stable with the define **on**; a strip always loses the race. Same reason a pre-build
gate that strips-and-retries would loop forever.

**Decision 2026-08-17: accepted for the TestFlight smoke test.** The `[RuntimeInitializeOnLoadMethod]`
hooks that would connect are commented-out doc examples, so nothing opens a socket. The real cost
is `MainThreadDispatcher` initialising at `BeforeSceneLoad` plus IL2CPP bloat — confirmed present
in the Aug 4 build (`com.IvanMurzak.Unity.MCP.Runtime__1.cpp`, `McpPlugin.cpp` in `il2cppOutput`).
Not an App Review 2.3.1 emergency, and it no longer gates this upload.

**Fix before anything public.** Tracked as GOLFIN_Roadmap Order **428** `unity_mcp_define_strip`
(estimate raised XS → S). Two live options — embed the package and add
`"includePlatforms": ["Editor"]` to its Runtime asmdef (~15 min), or strip the package from
`manifest.json` around release builds. The old build-profile plan is dead: Unity 6 profile defines
are additive, so a profile can add a define but never subtract a global one.

**4. While you're here** (optional, not blockers) — *Other Settings → Configuration*

- **Target minimum iOS Version** (15.0 today; 17.0 recommended)
- **Architecture: ARM64**, **Scripting Backend: IL2CPP** (iOS forces both)

All in `ProjectSettings/ProjectSettings.asset` — one file, low merge risk.

---

## Phase 2 — Unity → Xcode project (20–45 min)

⚠️ **You have an existing Xcode project at `~/Documents/GolfinBuilds` from Aug 4.** Do **not** archive it — its `Info.plist` still reads `CFBundleShortVersionString 0.1.0` / `CFBundleVersion 2023`. It predates every Phase 1 fix. Rebuild over it.

1. **File → Build Profiles** → **iOS** → **Switch Platform** if not already on iOS. A cold switch reimports everything (1400+ prefabs, 100+ scenes) — 20–40 min. You built iOS today at 09:16, so this may be warm and fast.
2. Confirm the scene list (`ShellScene` at index 0).
3. **Run in Xcode as:** default (Release).
4. **Build** (not *Build and Run*) → target `~/Documents/GolfinBuilds`.
5. Choose **Replace**. Append would carry the stale Info.plist forward.

---

## Phase 3 — Archive in Xcode (10–40 min)

1. Open `~/Documents/GolfinBuilds/Unity-iPhone.xcodeproj`.
2. **Unity-iPhone** target → **Signing & Capabilities**:
   - ✅ Automatically manage signing
   - **Team:** NEXT INNOVATION PTE. LTD. (`TCUV4A9VTJ`) — *not* either personal team
   - **Bundle Identifier:** `com.nextinnovation.golfingame`
   - If it errors "no profiles found", click **Try Again** — Xcode creates one when you're Admin. If it can't create an *Apple Distribution* cert, that's the ASC permissions gap flagged in Phase 0.
3. **Info.plist — ✅ automated, nothing to do.** `Assets/Editor/iOSPostProcess.cs` (`[PostProcessBuild(1000)]`) writes `ITSAppUsesNonExemptEncryption` = `false` into the generated `Info.plist` on every iOS build, so it survives Unity regenerating the project. Skips the export-compliance question on every future upload; valid for standard HTTPS/TLS, which is exempt. It was **absent** from the Aug 4 project — confirm the key is present in the rebuilt one before archiving.
4. Set the run destination to **Any iOS Device (arm64)**. **Archive stays greyed out until you do.**
5. **Product → Archive.**

---

## Phase 4 — Upload (10–20 min)

Organizer opens on completion (or **Window → Organizer**).

1. Archive → **Distribute App**
2. **App Store Connect** → **Upload**
3. Options:
   - ✅ Upload your app's symbols
   - ❌ **Manage Version and Build Number** — unchecked, so Xcode doesn't rewrite your `1.5.7 (2113)`
4. **Automatically manage signing** → Next → Upload

**Fallback:** Distribute App → **Export** → `.ipa` → drag into **Transporter** (Mac App Store). Better error messages than Xcode.

---

## Phase 5 — App Store Connect → TestFlight (10 min + processing)

**[TestFlight for Golfin Game](https://appstoreconnect.apple.com/apps/6741622475/testflight/ios)**. State of this record as of 2026-08-17:

- Build Uploads run to **`1.5.6 (5)`**, all *Complete*, last 2025-12-11
- Internal group **`In-House Testers`** already exists
- **No external groups yet** — Investors will be the first
- Test Information lives under *Additional*

1. **Wait for processing** — *Processing* for 5–30 min, email on completion. If the build vanishes, the rejection reason is in your email, not the web UI.
2. **Clear "Missing Compliance"** (skip if the Info.plist key from Phase 3 is in): build → **Provide Export Compliance Information** → for HTTPS-only, answer *Yes* to encryption then pick the **exempt** standard-encryption option.
3. **Internal testing — no review, instant:** assign the build to `In-House Testers`. New people must first exist under *Users and Access*. Max 100. **Internal builds skip Beta App Review** — smoke-test here first.
4. **External testing — Ken / investors, needs review:** **External Testing → +** → `Investors`. Fill **Test Information** (What to Test, feedback email, marketing URL, privacy policy URL, contact info — all mandatory) → add build → **Submit for Review**. 24–48h typical. Then enable the **Public Link** for a shareable URL, up to 10,000 testers, revocable.

---

## Phase 6 — Install

TestFlight app → emailed invite or public link → **Install**. Builds expire after **90 days**.

---

## Failure modes

| Error | Cause | Fix |
|---|---|---|
| "The bundle version must be higher than the previously uploaded version" | `0.1.0` vs uploaded `1.5.6` | Blocker #1 — set `1.5.7` |
| Xcode can't create an Apple Distribution cert | Signing as `cesar@clumsydwarf.com`, which is only a **Developer** on the team | Switch to `cesar.guarinoni@wonderwallgp.com` (**Admin**) in Xcode → Settings → Accounts |
| Archive menu greyed out | Destination is a simulator | Select **Any iOS Device (arm64)** |
| Xcode signs with the wrong team | Three Dev certs on three teams | Pick `TCUV4A9VTJ` / NEXT INNOVATION PTE. LTD. |
| Archived the wrong thing — version reads 0.1.0 | Archived the stale Aug 4 `GolfinBuilds` project | Rebuild from Unity with **Replace** after Phase 1 |
| Build stuck in Processing >1h, then disappears | Usually Info.plist / entitlements | Check the rejection email |
| "Redundant Binary Upload" | Build number reused within a version | Bump the Build field |
| `ITMS-90683` — missing usage description | Only once GPS code lands | Fill `locationUsageDescription` |

*Icon errors (`ITMS-90022` / `90023` / `90717`) are not expected — verified above.*

---

## Time budget from here

| | |
|---|---|
| Phase 0 | ✅ done |
| Phase 1 fixes | 10 min |
| Phase 2 rebuild (platform already warm) | 20–45 min |
| Phase 3 archive | 30 min |
| Phase 4 upload | 15 min |
| Phase 5 processing + assign to In-House Testers | 30 min |
| **Total to a build on your phone** | **~2 hours** |
| Investors group (Beta App Review) | +24–48h |

---

## Claude Code kickoff — Phase 1 blockers

```
Fix the three iOS upload blockers in ProjectSettings/ProjectSettings.asset so the
project can archive and upload to TestFlight.

Context:
- Golfin Game (com.nextinnovation.golfingame): the live App Store version is
  1.5.4, but TestFlight already has 1.5.6 (build 5) uploaded 2025-12-11, so
  bundleVersion must clear the highest UPLOADED train, not the live one.
  Set bundleVersion: 0.1.0 -> 1.5.7. Leave buildNumber iPhone: 2113 as is.
- appleEnableAutomaticSigning: 0 -> 1 and appleDeveloperTeamID: TCUV4A9VTJ
  (NEXT INNOVATION PTE. LTD. — confirmed from the provisioning profile on disk).
  The Aug 4 Xcode project at ~/Documents/GolfinBuilds already has
  DEVELOPMENT_TEAM = TCUV4A9VTJ set by hand; Unity overwrites it on Replace
  builds, which is why it has to live in ProjectSettings.
- Scripting define symbols: remove UNITY_MCP_READY from the iPhone entry only.
  Leave every other platform untouched.
- Do NOT touch m_BuildTargetPlatformIcons. The empty iPhone m_Textures arrays are
  correct — Unity falls back to the project default icon (Assets/Icons/
  Golfin-Icon2.png, 1024x1024) and generates the full alpha-free iOS icon set.
  Verified against Temp/StagingArea output.
- Add Assets/Editor/iOSPostProcess.cs: a [PostProcessBuild] callback that writes
  ITSAppUsesNonExemptEncryption = false into the generated Info.plist, so the
  export-compliance step is skipped on every upload. Use
  UnityEditor.iOS.Xcode.PlistDocument. Guard with #if UNITY_IOS.
- Minimal diff. One settings file plus one new Editor script.
- Out of scope: icons, build profiles, anything Android.

When done: list changed files with a 1-line summary each, confirm the YAML still
parses by opening the project, flag which need manual on-device verification,
update STATUS.md + IMPLEMENTER_REPORT.md if a spec folder exists, and update
Docs/AI_CONTEXT.md.
```

---

## Sources

- [Apple — SDK minimum requirements (Xcode 26 / iOS 26 SDK, effective 2026-04-28)](https://www.developer.apple.com/news/upcoming-requirements/)
- [Apple — Provide export compliance information for beta builds](https://developer.apple.com/help/app-store-connect/test-a-beta-version/provide-export-compliance-information-for-beta-builds)
- [Apple — Upload an app to App Store Connect](https://help.apple.com/xcode/mac/current/en.lproj/dev442d7f2ca.html)
- [Apple — Complying with encryption export regulations](https://developer.apple.com/documentation/security/complying_with_encryption_export_regulations)
- [App Store — Golfin Game (live version 1.5.4)](https://apps.apple.com/us/app/golfin-game/id6741622475)
- [Apple — TestFlight](https://developer.apple.com/testflight/)
- [Unity — iOS requirements and compatibility (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html)
- Local: `ProjectSettings/ProjectSettings.asset` · `Assets/Icons/Golfin-Icon2.png` · `Temp/StagingArea/` · `~/Documents/GolfinBuilds/Info.plist` + `project.pbxproj` · keychain + provisioning profile · [ASC TestFlight build history](https://appstoreconnect.apple.com/apps/6741622475/testflight/ios)
