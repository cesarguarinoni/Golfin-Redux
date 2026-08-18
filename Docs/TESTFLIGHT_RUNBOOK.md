# GOLFIN Redux — iOS → TestFlight Runbook

**Target:** `iOS-Full` build → TestFlight internal testers
**Machine:** Cesar's MacBook Pro · repo `/Users/cesar/Documents/GolfinRedux` · Unity **6000.3.9f1**
**Record:** Golfin Game — `com.nextinnovation.golfingame`, NEXT INNOVATION PTE. LTD. (Cesar = Admin)
**Verified against the repo, the Mac, and live App Store Connect:** 2026-08-17

> ✅ **PIPELINE PROVEN 2026-08-17.** `1.5.7 (2192)` archived, uploaded 8:14 PM, processed clean,
> installed on device from TestFlight. Export compliance never asked (Info.plist key worked).
> Implementation commit `020ac3b43`. Roadmap Order 424 closed.
>
> **For subsequent uploads:** bump the Build number, then **Append** while iterating and
> **Replace** for anything you upload (Unity only documents that Append "overwrites certain
> values", never which — not a bet worth taking on a tester build). Replace is safe now:
> team comes from Player Settings, the compliance key from `Assets/Editor/iOSPostProcess.cs`,
> the archive post-action from `Assets/Editor/iOSArchivePostAction.cs`. Nothing lives only in
> the Xcode project.
>
> **Nothing to remember after an upload.** The build-number regression guard used to need a
> manual `GOLFIN → Build → Mark Current Commit As Uploaded` — and it was missed after this very
> upload, leaving `Docs/Versioning/last_uploaded_build.txt` at `0`. **Product → Archive** now
> runs `Tools/mark-uploaded.sh` for you (Phase 3 step 5). Commit the changed guard file with
> your next change; the menu item survives only as a manual escape hatch.

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

## One command — `fastlane ios testflight_build`

Everything below (Phases 2–4) automated into a single unattended run. **Same duration** — Unity
and IL2CPP dominate and are untouched — but you can walk away, and the four dialogs where a
human currently picks the wrong thing are gone.

```bash
./Tools/testflight.sh
```

That wrapper is one line of `exec fastlane ios testflight_build`, plus the two environment
facts the run cannot survive without: `LC_ALL`/`LANG` set to UTF-8, and Homebrew's `bin` on
`PATH`. `fastlane ios testflight_build` directly works too — **from a shell whose locale is
UTF-8.** From one without it (cron, CI, `bash -c`, any non-interactive shell) it dies about
three seconds into the archive with `invalid byte sequence in US-ASCII`, which looks exactly
like a build failure and is not one. See § "The locale trap" below.

What the lane does, in order (`fastlane/Fastfile`):

| Step | Action | Fails the lane when |
|---|---|---|
| 1 | `ensure_git_status_clean` | tree is dirty — the build number is `git rev-list --count HEAD` and would not describe the binary |
| 2 | `Tools/assert-unity-closed.sh` | the Editor holds `Temp/UnityLockfile` (batchmode can't take it) |
| 3 | `Tools/unity-build-ios.sh` → `Golfin.EditorTools.CIBuild.BuildIOS` | Unity's exit code is non-zero, or `Builds/iOS-Full/Unity-iPhone.xcodeproj` is missing |
| 4 | `build_app` (xcodebuild archive + export, `-allowProvisioningUpdates`) | signing/archive failure |
| 5 | `upload_to_testflight` (App Store Connect API key) | upload rejected |
| 6 | `Tools/mark-uploaded.sh` | never — always exits 0 by design |

After it finishes, **commit `Docs/Versioning/last_uploaded_build.txt`** with your next change.
It is the only file the run leaves dirty.

### One-time setup

**1. fastlane** (not yet installed on this Mac — Homebrew isn't either). System Ruby is
**2.6.10**, EOL and Apple-deprecated; never `gem install` against it. Homebrew's fastlane
vendors its own Ruby:

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

```bash
eval "$(/opt/homebrew/bin/brew shellenv)" && brew install fastlane
```

The first command asks for your admin password and prints two `eval` lines to add to
`~/.zprofile` — do that so `brew` and `fastlane` are on `PATH` in new shells.

**2. App Store Connect API key** — App Store Connect → Users and Access → **Integrations** →
**+**. Role **App Manager** or Admin. The `.p8` **downloads exactly once and can never be
re-downloaded**; if it is lost, revoke and mint a new one.

```bash
mkdir -p ~/.appstoreconnect && mv ~/Downloads/AuthKey_*.p8 ~/.appstoreconnect/ && chmod 600 ~/.appstoreconnect/*.p8
```

**3. Environment** — copy `fastlane/.env.example` → `fastlane/.env` (gitignored) and fill in
`ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_PATH`. Never commit either the `.env` or the `.p8`.

### The locale trap — read this before debugging an "archive failure"

Hit for real on the first end-to-end run, 2026-08-18. `build_app` died after 3 seconds with:

```
[!] invalid byte sequence in US-ASCII (ArgumentError)
    gym/lib/gym/error_handler.rb:15:in 'Regexp#==='
```

Nothing was wrong with the build. gym pipes **every line of xcodebuild's output** through
error-matching regexes; xcodebuild prints `➜` (U+279C) in its dependency graph on line ~18 of
any archive; and a regex match against a non-ASCII byte raises when Ruby's
`Encoding.default_external` is `US-ASCII` — which is what Ruby picks in any shell with no
`LANG`/`LC_ALL`. fastlane then died and took `xcodebuild` with it.

**`LC_ALL` in `fastlane/.env` does not fix this.** Ruby fixes its external encoding at process
start; dotenv reads `.env` afterwards. Those lines are still there (children inherit them, and
they silence fastlane's cosmetic warning) but they leave the encoding US-ASCII — a half-fix that
looks like a fix. The export must precede the fastlane process:

```bash
LC_ALL=en_US.UTF-8 LANG=en_US.UTF-8 fastlane ios testflight_build
```

Or just use `./Tools/testflight.sh`, which does exactly that. To stop meeting this in every
tool, put it in the shell profile once:

```bash
printf 'export LC_ALL=en_US.UTF-8\nexport LANG=en_US.UTF-8\neval "$(/opt/homebrew/bin/brew shellenv)"\n' >> ~/.zprofile
```

### When it fails

- **Unity build** — full batchmode log at `Builds/unity-build-ios.log`; the lane also echoes
  its last 120 lines. `[CIBuild] FAILED:` is the line that says why.
- **"REFUSING TO BUILD: computed build number N <= last-uploaded M"** — the upload guard.
  Commit something; the number is `git rev-list --count HEAD`.
- **Editor open** — quit Unity. If it crashed, the lock is stale and the script tells you
  which `rm` clears it.
- **Anything after the archive** — the Xcode project in `Builds/iOS-Full` is real and
  current; fall back to Phase 3/4 below by hand rather than re-running Unity.

The manual path below stays supported and is the fallback whenever the lane misbehaves.

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

### ⚠️ Blocker #3 does not stay fixed — and cannot be worked around by hand

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

**Output path: `Builds/iOS-Full` inside the repo.** That's where the 2026-08-17 build went and it's fine — `.gitignore:27` is `[Bb]uilds/`, so nothing leaks into git.

⚠️ **A stale Xcode project also exists at `~/Documents/GolfinBuilds` from Aug 4.** Do **not** archive that one — its `Info.plist` still reads `CFBundleShortVersionString 0.1.0` / `CFBundleVersion 2023`, predating every Phase 1 fix. Delete it or ignore it.

1. **File → Build Profiles** → **iOS** → **Switch Platform** if not already on iOS. A cold switch reimports everything (1400+ prefabs, 100+ scenes) — 20–40 min. You built iOS today at 09:16, so this may be warm and fast.
2. Confirm the scene list (`ShellScene` at index 0).
3. **Run in Xcode as:** default (Release).
4. **Build** (not *Build and Run*) → target `Builds/iOS-Full`.
5. Choose **Replace**. Append would carry the stale Info.plist forward.

---

## Phase 3 — Archive in Xcode (10–40 min)

1. Open `Builds/iOS-Full/Unity-iPhone.xcodeproj`.
2. **Unity-iPhone** target → **Signing & Capabilities**:
   - ✅ Automatically manage signing
   - **Team:** NEXT INNOVATION PTE. LTD. (`TCUV4A9VTJ`) — *not* either personal team
   - **Bundle Identifier:** `com.nextinnovation.golfingame`
   - If it errors "no profiles found", click **Try Again** — Xcode creates one when you're Admin. If it can't create an *Apple Distribution* cert, that's the ASC permissions gap flagged in Phase 0.
3. **Info.plist — ✅ automated, nothing to do.** `Assets/Editor/iOSPostProcess.cs` (`[PostProcessBuild(1000)]`) writes `ITSAppUsesNonExemptEncryption` = `false` into the generated `Info.plist` on every iOS build, so it survives Unity regenerating the project. Skips the export-compliance question on every future upload; valid for standard HTTPS/TLS, which is exempt. It was **absent** from the Aug 4 project — confirm the key is present in the rebuilt one before archiving.
4. Set the run destination to **Any iOS Device (arm64)**. **Archive stays greyed out until you do.**
5. **Product → Archive.** An Archive **post-action** — injected into the generated `.xcscheme` by
   `Assets/Editor/iOSArchivePostAction.cs` on every iOS build, so a **Replace** can't wipe it —
   runs `Tools/mark-uploaded.sh`, which advances `Docs/Versioning/last_uploaded_build.txt` to
   `git rev-list --count HEAD`. That's the guard `BuildStampGenerator` checks, so the next
   store build at the same commit is refused instead of burning an upload slot. It fires on
   **archive, not upload** — archiving and discarding still advances it, which is the safe
   direction. It never fails the archive; if the guard didn't move, the reason is in the
   gitignored `Docs/Versioning/.mark-uploaded.log`. **Commit the changed guard file** with your
   next change — the script deliberately doesn't commit for you.

---

## Phase 4 — Upload (10–20 min)

Organizer opens on completion (or **Window → Organizer**).

1. Archive → **Distribute App**
2. **App Store Connect** → **Upload**
3. Options:
   - ✅ Upload your app's symbols
   - ❌ **Manage Version and Build Number** — unchecked, so Xcode doesn't rewrite your version/build

**Note:** picking the **App Store Connect** tile with recommended settings skips the options screen entirely and the button reads **Distribute**, not Upload. The Upload/Export choice and the Manage-Version checkbox only appear under **Custom**. Recommended settings upload as-is, which is what you want.

**Fallback:** Distribute App → **Export** → `.ipa` → drag into **Transporter** (Mac App Store). Better error messages than Xcode.

---

## Phase 5 — App Store Connect → TestFlight (10 min + processing)

**[TestFlight for Golfin Game](https://appstoreconnect.apple.com/apps/6741622475/testflight/ios)**. State of this record as of 2026-08-17:

- Build Uploads run to **`1.5.7 (2192)`**, uploaded 2026-08-17 8:14 PM. Prior train was `1.5.6 (5)`, 2025-12-11
- Internal group **`In-House Testers`** — Cesar, 賢 小松, Ken (invite pending since Feb 2025), Gabriele Campagna
- External group **`Friends & Family`** exists but has never had a build — needs Test Information + Beta App Review first
- Test Information lives under *Additional*

1. **Wait for processing** — *Processing* for 5–30 min, email on completion. If the build vanishes, the rejection reason is in your email, not the web UI.
2. **Clear "Missing Compliance"** (skip if the Info.plist key from Phase 3 is in): build → **Provide Export Compliance Information** → for HTTPS-only, answer *Yes* to encryption then pick the **exempt** standard-encryption option.
3. **Internal testing — no review, instant:** assign the build to `In-House Testers`. New people must first exist under *Users and Access*. Max 100. **Internal builds skip Beta App Review** — smoke-test here first.
4. **External testing — Ken / investors, needs review:** **External Testing → +** → `Investors`. Fill **Test Information** (What to Test, feedback email, marketing URL, privacy policy URL, contact info — all mandatory) → add build → **Submit for Review**. 24–48h typical. Then enable the **Public Link** for a shareable URL, up to 10,000 testers, revocable.

---

## Phase 6 — Install

TestFlight app → emailed invite or public link → **Install**. Builds expire after **90 days**.

---

## Adding testers — the parts that aren't obvious

*Learned the hard way 2026-08-17 adding `zero.rsc@gmail.com` (Gabriele Campagna).*

### Internal vs external, in one line

**Internal** = the person needs a user account on your App Store Connect, capped at 100, no review, instant. **External** = email address only, no account access whatsoever, up to 10,000, but needs Test Information filled in and a Beta App Review pass (24–48h) on the first build. For anyone outside the company, external is genuinely less access — weigh that against the one-time review cost.

### ⚠️ Customer Support / Sales / Marketing CANNOT be internal testers

The obvious move is to grant the least-privileged role. It doesn't work. Per [Apple's role matrix](https://developer.apple.com/support/roles/), TestFlight access exists only for Account Holder, Admin, **App Manager** and **Developer**. Customer Support, Sales and Marketing have none — a user on those roles silently never appears in the eligible-testers list, with no error explaining why.

**Developer is the floor for internal testing.** To keep that tolerable:

- Leave **Access to Certificates, Identifiers & Profiles** unchecked (it's a separate checkbox under Additional Resources, independent of the role) — they then cannot touch signing
- Leave **Access to Reports** and **Create Apps** unchecked
- Set **Apps → Selected Apps** and pick only the one app. This matters more than the role: it hides GOLFIN GPS entirely
- Financials are never exposed at this level — those live in the Finance and Sales roles

### ⚠️ The group's own "Add Testers" dialog is unreliable

For a newly-invited user, the **+** next to `Testers (n)` inside a group keeps showing a stale list and will not offer the new person — through role changes, page reloads and cache-busting alike.

**Use this instead:** TestFlight → **Testers → All** → tick the person's row → **Add to Group** → pick the group → **Add.** New users show up here immediately with status *No Builds Available*, meaning they exist as a tester but belong to no group yet.

### Sequence that works

1. **Users and Access → +** — name, email, role **Developer**, all Additional Resources unchecked → **Next**
2. Untick every app except the one they need → **Invite** *(sends the ASC account email)*
3. **Wait for them to accept.** They cannot be added as a tester while the invitation is pending. Their real name replaces whatever placeholder you typed once they accept
4. **Testers → All** → tick → **Add to Group** → the internal group → **Add** *(sends the TestFlight email)*

Two separate emails, two separate acceptances. Budget for the round trip.

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
