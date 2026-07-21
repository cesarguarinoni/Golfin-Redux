# SPEC — `login_signup_screens`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

Build the four GOLFIN account screens — **Login**, **Create Username**, **Sign Up**, **Email Confirmation** — as standalone, editable Unity UI (prefabs under `ScreensRoot`, registered in `ScreenManager`), matching the Figma design at 1:1 shell-canvas geometry. **Phase 1 = UI only, tied to nothing.** No backend, no Supabase, no OAuth, no email — every auth action is a clearly-marked placeholder stub. The screens exist as a clickable shell so Cesar can correct the code, and so Phase 2 can wire them to the GPS/PLAYLIFE (Supabase) backend at clean seams. The only live logic in Phase 1 is the **client-side password-rule checklist** (advisory) and the **show/hide password** toggle.

**Backend context (for Phase 2 only — do NOT implement now):** GPS auth is Supabase (`GPS_INTEGRATION_REFERENCE.md`). Signup = `auth.signUp({email,password})`; the "username" is the profile `display_name` set from the Create Username screen; email verification is the Supabase confirm-email flow; Google/Apple are Supabase OAuth. Field cross-check is **done and passing** — email+password is exactly what the backend needs. This spec captures that mapping only as `// TODO(Phase 2)` seams.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` (Golfin Game Redux), page "Login Screen", parent node `4062:4971`.
- **Four frames (each 1170×2532):**
  | Screen | Figma node | Top-band title | Token source |
  |---|---|---|---|
  | Login | `4065:5901` | `GOLFIN ACCOUNT` | **token-exact** (design-context pulled in spec) |
  | Create Username | `4065:5902` | `GOLFIN ACCOUNT` | copy-exact; **re-pull `get_design_context` for text tokens** |
  | Sign Up | `4065:6052` | `SIGN UP` | **token-exact** |
  | Email Confirmation | `4065:6053` | `SIGN UP` | copy-exact; **re-pull `get_design_context` for text tokens** |
- **Node renders in `reference/`:** `01_Login.png`, `02_CreateUsername.png`, `03_SignUp.png`, `04_EmailConfirmation.png` — A/B every element against these.
- **Rule 9 (mandatory):** at step 0 the implementer AND each reviewer run `get_design_context` on **all four** node ids and diff live px/font/gap/sprite against the node — this SPEC's token tables are a reconcile-against-node convenience, never the source of truth. Tokens for `5902` and `6053` are copy-only here; pull them.
- **Placeholder content:** all field values are placeholders (`Email`, `Password`, `Username` in grey `#B2B2B2`). No mock data to reproduce.

## Shared shell (identical across all four frames)

Build once as a reusable structure, vary the card body per screen.

| Element | Property → value |
|---|---|
| Root canvas | shell canvas, 1:1 Figma px (frame 1170×2532); place screens under `ScreensRoot` |
| Background | full-screen "Splash" course/sky photo (`Backgrounds` cmp `2032:459`, img `imgProperty1Splash`). **Reuse the existing Splash-screen background asset if it matches; else export.** Do NOT touch `M_Splash*.mat` (standing ban) |
| Scrim/blur | over the whole screen: `backdrop-blur 10px` + `rgba(0,0,0,0.1)` fill. (If a real-time blur is impractical, a pre-blurred bg variant + 10% black scrim is acceptable — A/B the result.) |
| Top UI band | 1170×313, top background img `imgTopBackground` (notched gold-edged banner). Reuse the existing shell top-band if one exists; else export. Centered title text (per-screen) — Rubik SemiBold **51px**, white, tracking −1.29, centered |
| Cards Container | rounded **20px**, vertical gradient **`#133453`→`#091B33`**, border **3px `rgba(255,255,255,0.9)`**, shadow `0 4 4 rgba(0,0,0,.25)`, width **1074**, padding **48**, gap **48**, centered; fills remaining height. Scrollable (ScrollRect) — a 20%-indicator scrollbar + up/down chevrons sit at the right edge; implement scroll only if content overflows the card |
| CANCEL button | bottom of card on Login / Create Username / Sign Up — silver (see button atoms) |

## Figma Fidelity (Rule 18 — reviewers reproduce with PASS/FAIL vs `reference/`)

### 1. Login — `4065:5901`
| Element | Property → value |
|---|---|
| Title | `GOLFIN ACCOUNT` — Rubik SemiBold 51px white, centered |
| Section header | `LOGIN WITH EMAIL` — Rubik SemiBold 66px white, centered (leading 84) |
| `EMAIL` label | Rubik SemiBold 48px white (leading 63); right chevron (decorative/static) |
| Email input | white box, radius 20, h **87**, inset shadow `inset 0 4 10 rgba(0,0,0,.5)`, pad-left 24; placeholder `Email` Rubik Regular 48px `#B2B2B2` |
| `PASSWORD` label | same as EMAIL label |
| Password input | same box as Email; placeholder `Password`; **eye/show icon** right (img `imgShape`, 72px) |
| Forgot Password | `Forgot Password` — Rubik SemiBold 39px **green `#22B800`**, centered |
| LOGIN button | **green primary** — gradient `#22B800→#20A80C→#179005`, outer border `#137704` + inner 2px `#B2FFA1`, radius 20, h **120**, px-96, sheen + ellipse highlight; text `LOGIN` Rubik SemiBold 66px white |
| Separator | horizontal divider img `imgSeparator`, width 978, ~2px |
| Service header | `LOGIN WITH  A SERVICE` (two spaces before A) — Rubik SemiBold 66px white |
| Google pill | white bg, **border 3px black**, radius 90, h **150**, w 670, gap 24, px-48; Google "G" logo (`imgLogoGoogleg48Dp`, 80px) + `Login with Google` Rubik SemiBold 51px black |
| Apple pill | same pill; Apple glyph (`S_Login_Apple_Icon`, 80px) + `Login with Apple` |
| Separator | as above |
| CANCEL button | **silver** — gradient `#FFFFFF→#D1D5DB→#818EA1`, outer border `#334155` + inner 2px `#F7F8F9`, radius 20, h 120, w **445**, sheen; text `CANCEL` Rubik SemiBold 66px `#1E293B` |
| Footer | `No account? ` (Rubik Regular 39px white) + `Create an account` (Rubik SemiBold 39px green `#22B800`), centered |

### 2. Create Username — `4065:5902`  *(pull design-context for exact text tokens)*
| Element | Property → value |
|---|---|
| Title | `GOLFIN ACCOUNT` (top band) |
| Section header | `CREATE USERNAME` — Rubik SemiBold 66px white, centered |
| `USERNAME` label | Rubik SemiBold 48px white |
| Username input | white box (as Email input); placeholder `Username` `#B2B2B2` |
| Body copy | three white bold paragraphs (verify weight/size vs render): `Please choose a username.` · `Your username is public and used across all Golfin services to interact with the community.` · `Your username can only contain letters and numbers.` |
| CREATE button | green primary (as LOGIN) |
| CANCEL button | silver (as Login CANCEL) |
| *(no social buttons, no footer link on this frame)* | |

### 3. Sign Up — `4065:6052`
| Element | Property → value |
|---|---|
| Title | `SIGN UP` (top band) |
| Section header | `SIGN UP WITH EMAIL` — Rubik SemiBold 66px white |
| `EMAIL` label + input | as Login (placeholder `Email`) |
| `PASSWORD` label + input | as Login (placeholder `Password`, eye icon) |
| Password rules (5) | vertical list, gap 6; each row = icon 48px + text Rubik Regular 39px white (leading 54): `Must contain 8 characters` · `Must contain one lowercase letter` · `Must contain one uppercase letter` · `Must contain one number` · `Must contain one special character` |
| Rule icon states | **unmet** = X mark (img `imgIcon` + strokes) + white text; **met** = green tick + green `#22B800` text (client-side, live — see §Logic) |
| CREATE button | green primary |
| Separator | `imgSeparator` |
| Service header | `SIGN UP WITH  A SERVICE` (two spaces) |
| Google pill | white pill + Google logo + `Sign up with Google` |
| Apple pill | white pill + Apple glyph + `Sign up with Apple` |
| Separator | `imgSeparator` |
| CANCEL button | silver |
| Footer | `If you already have an account, ` (white) + `login here` (green `#22B800` SemiBold) |

### 4. Email Confirmation — `4065:6053`  *(pull design-context for exact text tokens)*
| Element | Property → value |
|---|---|
| Title | `SIGN UP` (top band) |
| Section header | `EMAIL CONFIRMATION` — Rubik SemiBold 66px white |
| Body copy | white bold paragraphs, left-aligned (verify weight/size): `A confirmation email has been sent to your inbox.` · `Please check your email and click the confirmation link to verify your account.` · `Remember to check your spam folder.` · `If you didn't receive the email, please click the button below.` |
| RESEND button | **silver** (centered, mid-panel) — text `RESEND` |
| Trailing copy | `Once you have confirmed your email, please go back to the login screen.` |
| LOGIN button | green primary (bottom) — text `LOGIN` |

## Element Reuse Map (Rule 22 — reuse real atoms, don't fabricate flat fills)

Auth screens are largely **greenfield atoms** — most do not exist in `UI_ELEMENT_PALETTE.md`. Reuse where a real atom exists; for genuinely-absent atoms, **export the Figma sprite** (the node provides download URLs for every one below) and import under the naming convention. Do **not** ship a null-sprite `Image` where the node shows a sprite/gradient/border (Rule 21 hard-fails it). If any listed source truly can't be produced, **surface it — do not hand-roll silently.**

| Figma element | Source | Action |
|---|---|---|
| CANCEL button | **REUSE** Silver button `Assets/Art/RosterScreen/ButtonCancel.png` (GUID `6021c639e9c124b44a06c8ccd977896f`) if it A/Bs clean to the node's silver gradient; else build gradient+border+sheen | reuse-first |
| Separators | **REUSE** Divider (horizontal) `Assets/Art/HomeScreen/Divider.png` (`36b5ccd887…`) if it matches `imgSeparator`; else export | reuse-first |
| Fonts (SemiBold / Regular) | **REUSE** `Rubik-SemiBold SDF.asset` (`39fb7824…`) and `Rubik-VariableFont_wght SDF.asset` (`0e84913c…`) | reuse |
| Splash background | **REUSE** existing Splash-screen bg if it matches `imgProperty1Splash`; else export | reuse-first |
| Top banner band | **REUSE** existing shell top-band if present; else export `imgTopBackground` | reuse-first |
| **Green GPS primary button** | NEW `S_Btn_GreenGPS` — export the Figma button container or build gradient `#22B800→#20A80C→#179005` + inner `#B2FFA1` border + sheen/ellipse overlays | new atom |
| **White input field** | NEW `S_InputField_BG` — white rounded-20 sprite + inset-shadow overlay | new atom |
| **Social login pill** | NEW `S_Btn_SocialPill` — white rounded-90, 3px black border | new atom |
| Google "G" icon | NEW `ICO_GoogleG` — export `imgLogoGoogleg48Dp` | new atom |
| Apple glyph | NEW `ICO_Apple` — export `S_Login_Apple_Icon` vector | new atom |
| Eye / show-password icon | NEW `ICO_EyeShow` — export `imgShape` | new atom |
| Password-rule X / tick | NEW `ICO_RuleCross` / `ICO_RuleTick` — export `imgIcon`+strokes; tick from a green check | new atom |

**Every new reusable atom above is added to `UI_ELEMENT_PALETTE.md` in the same commit** (palette maintenance rule).

## Architecture context

- **Scene:** `Assets/Scenes/ShellScene.unity` — build the four screens under `Canvas > ScreensRoot`, inactive by default.
- **Prefabs (editable, NOT variants):** `Assets/Prefabs/UI/Account/{LoginScreen,CreateUsernameScreen,SignUpScreen,EmailConfirmationScreen}.prefab`. Instance each under `ScreensRoot` and wire to `ScreenManager`.
- **`ScreenManager`** (`Assets/Scripts/UI/ScreenManager.cs`, namespace `GolfinRedux.UI`):
  - Add to `ScreenId` enum: `Login, CreateUsername, SignUp, EmailConfirmation`.
  - Add four `[SerializeField] private GameObject _{login,createUsername,signUp,emailConfirmation}Screen;` fields.
  - Add four `SetActive(screenId == ScreenId.X)` branches in `ApplyScreen`.
  - **Exclude** all four from `isMenuScreen` and `showBars` (pre-game gates, like Logo/Splash/Loading — no persistent bars, no menu music).
- **Controllers:** one thin MonoBehaviour per screen, following the existing shell-screen controller convention (match where `HomeScreenController` / `RosterScreenController` live — same namespace/asmdef). Suggested namespace `Golfin.UI.Account`.
  - `LoginScreenController`, `CreateUsernameScreenController`, `SignUpScreenController`, `EmailConfirmationScreenController`.
- **`PasswordRequirements`** — pure C# helper (no Unity deps): `Check(string) → (len8, lower, upper, digit, special)`. Drives the Sign Up checklist. Client-side, advisory only.
- **Inputs:** `TMP_InputField` for email/password/username. Password field `contentType = Password`; eye toggle flips to `Standard` and back (`ForceLabelUpdate`). Use `UnityEngine.InputSystem` if any raw input is needed (never legacy `Input`).
- **Every new `Button` gets `Golfin.UI.Polish.ButtonPressFeedback`** immediately after the `Button` component (Rule 11, hook-enforced).
- **Localization:** route all static strings through `LocalizationManager.Get("AUTH_*")` keys (e.g. `AUTH_LOGIN_WITH_EMAIL`, `AUTH_EMAIL`, `AUTH_FORGOT_PASSWORD`, `AUTH_RULE_8CHARS`, …). Add **EN** values now; JP values may follow in a localization pass (add the keys so nothing is hardcoded).

## Logic (Phase 1 — client-only)

- **Password checklist (Sign Up):** on the password field's `onValueChanged`, run `PasswordRequirements.Check` and toggle each of the 5 rows cross→tick + white→green. Advisory only; **server is the source of truth** (Phase 2), so do NOT block CREATE on it in a way that would diverge from the server — for Phase 1, CREATE is a stub regardless.
- **Show/hide password:** eye icon toggles the password `TMP_InputField` content type.
- **Navigation stubs (local screen swaps via `ScreenManager.ShowScreen`, no backend):**
  - Login: `Create an account` → `SignUp`; `CANCEL` → previous/Splash; `LOGIN`, `Forgot Password`, Google, Apple → `// TODO(Phase 2)` log-only stubs (LOGIN may advance to `CreateUsername` behind a clearly-commented placeholder to demo the first-login step).
  - Sign Up: `CREATE` → `EmailConfirmation` (placeholder advance); `login here` → `Login`; `CANCEL` → `Login`; Google, Apple → stub log.
  - Create Username: `CREATE` → stub log (placeholder → `Home`); `CANCEL` → back.
  - Email Confirmation: `RESEND` → stub log; `LOGIN` → `Login`.
- All real-auth handlers are `// TODO(Phase 2 — GPS/Supabase)` one-liners that log and do nothing else. No `UnityWebRequest`, no HTTP, no Supabase.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] Each of the 4 screens matches its `reference/` render — per-element Figma-fidelity table reproduced with PASS/FAIL (Rule 18), incl. **font weight + rendered size** A/B per text element
- [ ] `UIFidelityLinter.LintPrefab` run per prefab; each `*_lint.json` cited with **`fail == 0`** (Rule 21)
- [ ] Password checklist toggles cross→tick + white→green live for all 5 rules
- [ ] Show/hide password eye toggle works
- [ ] All 4 `ScreenId`s registered; each reachable via `ScreenManager.ShowScreen(...)` and renders the correct frame (drive as a real user — the app boots behind a title/PLAY gate; C8)
- [ ] Every new `Button` has a sibling `ButtonPressFeedback` (Rule 11)
- [ ] **No backend calls** — grep of the 4 controllers + helper shows zero `UnityWebRequest`/`http`/Supabase references
- [ ] New reusable atoms added to `UI_ELEMENT_PALETTE.md` in the same commit
- [ ] No white-box placeholders; no null-sprite flat fills where the node shows a sprite
- [ ] All `[SerializeField]` references wired in the Inspector
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) flagged at the bottom of the report

## Files / hierarchy this task touches

- `Assets/Scripts/UI/ScreenManager.cs` — +4 `ScreenId`, +4 serialized fields, +4 `SetActive` branches
- `Assets/Scripts/UI/Account/*.cs` — 4 screen controllers + `PasswordRequirements` helper (new folder/asmdef per shell convention)
- `Assets/Prefabs/UI/Account/*.prefab` — 4 editable screen prefabs
- `Assets/Scenes/ShellScene.unity` — 4 screen instances under `ScreensRoot`, wired to `ScreenManager`
- `Assets/Art/UI/Account/…` (or convention path) — new exported sprites (`S_Btn_GreenGPS`, `S_InputField_BG`, `S_Btn_SocialPill`, `ICO_GoogleG`, `ICO_Apple`, `ICO_EyeShow`, `ICO_RuleCross`, `ICO_RuleTick`)
- `Assets/Localization/*` — `AUTH_*` keys (EN)
- `Docs/Architecture/UI_ELEMENT_PALETTE.md` — new atom rows
- `Docs/Architecture/UI_HIERARCHY.md` — new Account-screens section

## Smoke evidence

Drive `ScreenManager.ShowScreen` to each of the 4 screens in play mode (as a real user, past the boot gate), capture via the sanctioned `screenshot-game-view` path, and A/B each canonical PNG against its `reference/` render. Password checklist + eye toggle verified by typing into the Sign Up password field and capturing the cross→tick transition. Canonical screenshot per screen (long edge ≥900px, Rule 14).

## Out of scope (do NOT do these)

- Any backend / Supabase / FastAPI / OAuth / email-send / JWT work — **all of Phase 2**
- Real login, real signup, real password validation against the server, Forgot-Password flow
- **Username uniqueness / availability check** — deferred to v2/v3 (Cesar); Create Username accepts input with format-only feel, no "taken?" check
- **Terms / privacy line** — handled in Settings, NOT on these screens (Cesar)
- Wiring the screens into the actual app boot / deciding when the login gate triggers (Phase 2)
- Full JP localization (add EN + keys now; JP is a later pass)
- Editing `Assets/Scripts/Physics/`, `M_Splash*.mat`, or any shipped prefab in place (standing bans)

---
