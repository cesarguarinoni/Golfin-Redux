# testflight_distribution — SPEC

> **Status:** QUEUED. **Notion Order 424, P2.** Phase: Loop v2.
> **Depends on:** `phone_build_smoke_test` (Order 420) passing tethered smoke FIRST. Do not upload an unsmoked build — a TestFlight cycle costs processing time + Ken's time.
> **Goal:** get a build to Ken (and any tester) over-the-air via TestFlight, instead of tethering to one phone.
> **Split from** `phone_build_smoke_test/SPEC.md` §7 on 2026-07-27 (paid account + TestFlight now available). That §7 is the summary; this is the full task.

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

### 1b. Build number
`buildNumber:` is blank; `bundleVersion: 0.1.0`. App Store Connect requires a build number, **unique and incrementing per upload**. Start at `1`, bump every upload. (Version string `0.1.0` is fine, or bump to `1.0.0` for the first real beta — Cesar's call.)
- **Code:** set `buildNumber` = 1 in Player Settings.

### 1c. Export compliance
No `ITSAppUsesNonExemptEncryption` flag → Xcode prompts "does your app use encryption?" on every upload. A golf game using only standard HTTPS is **exempt**.
- **Code:** set `ITSAppUsesNonExemptEncryption` = `NO`/`false` (Info.plist via Player Settings) to skip the per-upload prompt.

---

## 2. App Store Connect setup (Cesar, one-time)
1. appstoreconnect.apple.com → **My Apps → +** → **New App**
2. Platform iOS; Name; primary language; **Bundle ID = the exact id set in Player Settings** (must already be registered — automatic signing registers it, or register manually in the Developer portal → Identifiers)
3. SKU (any internal string, e.g. `golfin-redux`)
4. Save. This creates the record TestFlight builds attach to.

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
