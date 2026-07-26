# iPhone Build — Your Setup Steps (at-the-machine cheatsheet)

> Everything **you** do by hand for the tethered iPhone smoke test. Claude Code handles the code side (orientation lock, safe-area, quality tier) separately — this doc is only your part.
> Unity **6000.3.9f1** · Mac + Xcode · **paid Apple Team** (signing unblocked 2026-07-27).
> TestFlight/OTA-to-Ken is a SEPARATE task — see `Docs/Specs/Queued/testflight_distribution/SPEC.md`. Do this tethered smoke FIRST.

---

## Before you start (have these ready)
- [ ] Mac with Xcode installed
- [ ] iPhone + charging cable
- [ ] Your paid Apple Developer account signed in (you have this now)
- [ ] **Unity Hub → Installs → ⚙ on 6000.3.9f1 → Add Modules → iOS Build Support ticked** (large download — do first if missing)

---

## STEP 1 — Set the bundle ID (Unity, ~1 min)
1. **Edit → Project Settings → Player**
2. Click the **iOS tab** (Apple icon in the platform row)
3. **Other Settings → Identification**
4. **Bundle Identifier**: replace the template junk with your final reverse-DNS id, e.g. `com.golfin.redux`
   - lowercase, letters/numbers/dots, no spaces
   - **pick the FINAL one** — it becomes the app's permanent identity once TestFlight/App Store Connect knows it
5. Confirm **Target minimum iOS Version = 15.0** and **Target SDK = Device SDK** (both already set)
6. Leave **Automatically Sign** unticked here — you'll sign in Xcode
7. **File → Save Project**

---

## STEP 2 — Add your Apple ID to Xcode (one-time)
1. **Xcode → Settings → Accounts** (⌘,)
2. **+ → Apple ID**, sign in
3. Your **paid Team** now appears in the list — that's your signing identity

---

## STEP 3 — Prep the iPhone (one-time, easy to forget)
1. Plug in the phone, unlock it, tap **Trust This Computer** on the phone
2. On the phone: **Settings → Privacy & Security → Developer Mode → On** → restart when prompted
   - *(iOS 16+ needs this — without it the app won't launch)*

---

## STEP 4 — Build from Unity
1. **File → Build Settings** (Build Profiles in Unity 6)
2. Platform = **iOS**; if not active, select it → **Switch Platform**
3. **Build** → choose an empty folder (e.g. `~/GolfinBuilds/ios`) → wait
   - Unity makes an Xcode project; it does NOT install to the phone

---

## STEP 5 — Sign & run (Xcode)
1. In the build folder, open **`Unity-iPhone.xcodeproj`**
2. Left sidebar: select **Unity-iPhone** project → **Unity-iPhone** target → **Signing & Capabilities** tab
3. Tick **Automatically manage signing**
4. **Team** → pick your **paid Team** *(NOT Personal — paid = no 7-day expiry, no 3-app limit)*
5. Toolbar run destination → **your iPhone**
6. Press **▶ Run**

---

## STEP 6 — Trust the cert (first install only)
- First launch shows "Untrusted Developer" → on the phone: **Settings → General → VPN & Device Management → [your account] → Trust** → tap the app again.

---

## Now run the smoke checklist
The app should open into Logo → Splash → Loading → Home. Keep the **Xcode console** visible — that's where any crash/exception shows.

- [ ] Cold boot → Logo → Splash → Loading → Home completes
- [ ] Portrait lock holds — rotate the phone, UI must NOT rotate
- [ ] Note anything clipped under the Dynamic Island / home indicator (expected — see below)
- [ ] Roster: characters load, portraits + art render (no pink/magenta = missing shader)
- [ ] JP + EN both render (no tofu boxes □□□)
- [ ] Enter Lomond Hole 1 → drive from tee → ball simulates + settles
- [ ] Map view opens and closes cleanly
- [ ] **Hole Selection cards + Hole Complete modal show REAL hole art, not a Missing placeholder** (this validates the recent HoleImages course-namespacing on hardware)
- [ ] Gacha, Shop, Inventory/Bag each open without crash
- [ ] Audio plays (SFX on shot)
- [ ] Xcode console clean of exceptions through all the above

---

## Looks "wrong" but is EXPECTED (not bugs)
- **Content under the Dynamic Island / home indicator** — no safe-area handling yet. Claude Code's `SafeAreaFitter` (Phase A) addresses it; for the smoke test just note what's clipped, don't treat it as a regression.
- Nothing else should look off. Anything that does → capture a screenshot + the Xcode console line and it becomes a follow-up.

---

## Next: get a build to Ken (TestFlight)
Once this tethered smoke is GREEN, the over-the-air path to Ken is its own task:
**`Docs/Specs/Queued/testflight_distribution/SPEC.md`** (Notion Order 424). Don't upload an unsmoked build.
