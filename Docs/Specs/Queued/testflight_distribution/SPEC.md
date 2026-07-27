# testflight_distribution — SPEC

> **Status:** QUEUED. **Notion Order 424, P2.** Phase: Loop v2.
> **Depends on:** `phone_build_smoke_test` (Order 420) passing tethered smoke FIRST. Do not upload an unsmoked build — a TestFlight cycle costs processing time + Ken's time.
> **Goal:** get a build to Ken (and any tester) over-the-air via TestFlight, instead of tethering to one phone.
> **Split from** `phone_build_smoke_test/SPEC.md` §7 on 2026-07-27 (paid account + TestFlight now available). That §7 is the summary; this is the full task.
> **Bundle ID (locked 2026-07-27):** `com.nextinnovation.golfingame` — the **LIVE App Store Golfin's** id, reused so Redux ships as an *update* to the existing listing. **Signing team:** NEXT INNOVATION PTE. LTD. (Cesar = Admin → signing authorized). Consequences: this is **NOT a new app** (see §2) and the version is **no longer free** (see §1d).

---

## 0. Why separate from the smoke test
The tethered smoke test proves the build runs on hardware — no App Store Connect involvement. TestFlight is a distribution pipeline with its own record-keeping, asset requirements, and tester onboarding. Bundling them would gate "does it run?" behind "is the store record set up?", which is backwards. Smoke first, distribute second.

---

## 1. Upload gates — MUST clear before any upload (verified 2026-07-27)

These do NOT block the tethered smoke, but App Store Connect rejects an upload without them.

### 1a. App icon — needs ART FROM CESAR
`m_BuildTargetPlatformIcons` is empty; no 1024×1024 marketing icon on disk. App Store Connect rejects uploads without a valid **1024×1024 icon, PNG, no alpha channel**. A placeholder logo is acceptable for beta but it must be valid.
- **Cesar:** supply the 1024² PNG (+ ideally the full iOS icon set, or let Unity downscale from the 1024).
- **Code (once PNG exists):** wire it into Player Settings > iOS > Icon slots; verify the generated asset catalog carries the 1024 marketing slot.

**STATUS 2026-07-27:** ✅ RESOLVED. `Assets/Icons/Golfin-Icon.png` is now **1024×1024, RGB, no alpha** (md5 `ccdcc11f0fad882e988f0ab28e9e5f22`, 495 KB) — Cesar's own re-export, verified compliant, tracked in git. **Remaining:** Code wires it into Player Settings iOS icon slots when 424 goes active.

### 1b. Build number
`buildNumber:` is blank; `bundleVersion: 0.1.0`. App Store Connect requires a build number, **unique and incrementing per upload**. Start at `1`, bump every upload.
- **Code:** set `buildNumber` = 1 in Player Settings.
- ⚠️ The version STRING is **no longer a free choice** (see §1d) — reusing the live bundle id means `0.1.0` will be rejected. Set `bundleVersion` per §1d before the first upload.

### 1c. Export compliance
No `ITSAppUsesNonExemptEncryption` flag → Xcode prompts "does your app use encryption?" on every upload. A golf game using only standard HTTPS is **exempt**.
- **Code:** set `ITSAppUsesNonExemptEncryption` = `NO`/`false` (Info.plist via Player Settings) to skip the per-upload prompt.

### 1d. Version must EXCEED the live app  ⚠️ NEW — direct consequence of bundle-id reuse
Because Redux uses `com.nextinnovation.golfingame` (the live App Store Golfin's id), every upload is validated against that app's existing version history. `bundleVersion: 0.1.0` is **below** the live app's published version, so App Store Connect **will reject** it.
- **Cesar:** look up the live Golfin's **current App Store version** (App Store Connect → Golfin → App Store tab, or the public store listing).
- **Code:** set `bundleVersion` **strictly higher** than that (e.g. live `2.3.1` → use `2.4.0` or `3.0.0`), plus `buildNumber` = 1 (per §1b).
- ⚠️ Confirm the target version WITH Cesar before the first upload — it's a permanent, user-visible version jump on the live listing, not a throwaway beta number.

---

## 2. App Store Connect setup (Cesar) — record ALREADY EXISTS
⚠️ Because Redux reuses the live bundle id, you do **NOT** create a New App. The `com.nextinnovation.golfingame` record already exists under NEXT INNOVATION PTE. LTD., and TestFlight builds attach to it automatically once uploaded (§3).
1. Confirm you can see the **Golfin** app under App Store Connect → **My Apps**, and that your account (Admin) has TestFlight access on it.
2. Do **NOT** register a new bundle id or create a new listing — that forks Redux off the live app and defeats the whole reuse.
3. (If Redux were ever a *standalone* beta instead, you'd use a throwaway id like `com.nextinnovation.golfingame.reduxbeta` and *then* create a New App — but that is explicitly not the current plan.)

---

## 3. Archive & upload (Cesar, per build)
1. In Xcode with the build project open, run destination = **Any iOS Device (arm64)** (not the simulator, not a specific phone)
2. **Product → Archive** (a release build, not Run)
3. Organizer window opens → select the archive → **Distribute App → App Store Connect → Upload**
4. Accept automatic signing / distribution cert prompts
5. Answer the export-compliance question (exempt — see 1c; if the flag is set it won't even ask)
6. Upload → **wait for processing** (~5–15 min; you'll get an email or see it appear in the TestFlight tab)

---

## 4. Tester onboarding (Cesar)
- **Internal testing (fastest):** add Ken as a user in App Store Connect (Users and Access) with a role that allows TestFlight, then add him to the Internal Testing group. Internal builds are available immediately, no review.
- **External testing:** needs a one-time light **Beta App Review** per build train, plus a test-info/what-to-test blurb. Use this if Ken isn't an App Store Connect user.
- Ken installs the **TestFlight** app from the App Store, accepts the email invite (or redemption link), installs the build, plays OTA.

---

## 5. Suggested first-beta polish (optional, not gates)
- App display name (Player Settings > Product Name) — currently may still read a template name.
- Launch screen (currently default) — fine for beta, nicer later.
- Version bump `0.1.0` → `1.0.0` if you want the beta to read as a real 1.0 line.

---

## 6. Out of scope
- Public App Store submission / full App Review (this is TestFlight beta only).
- Android/Play Console internal testing (mirror task if wanted, separate).
- Automated CI upload (Fastlane/Xcode Cloud) — manual is fine at this stage.

---

## 7. Done when
Ken has installed a smoke-passed build from TestFlight on his own device and can launch into the Home screen. Icon shows (not the default). No crash on cold boot.
