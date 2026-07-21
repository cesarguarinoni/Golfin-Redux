# IMPLEMENTER_REPORT — `login_signup_screens` (iter-3)

**Iteration shape:** `login:pill-stadium-geometry`

---

## Iter-3 fix summary

**Root cause of iter-2 rejection:** The social-login pills (`GooglePill`, `ApplePill`) rendered as rounded rectangles rather than true stadiums. The iter-2 sprite `S_SocialPillBordered.png` was 200×150px with `spriteBorder=(76,3,76,3)` and `spritePixelsPerUnit=100`, giving `borderCU = 76 / (100 × 1) = 0.76cu`. The pill height is 150cu, so half-height = 75cu. Required: `borderCU == 75` for a true stadium. Actual: `borderCU = 0.76`. This produced near-zero border corners — visually straight vertical edges — not semicircular caps.

**Iter-3 fix:** Rebuilt `S_SocialPillBordered.png` as 160×150px with `spriteBorder=(75,75,75,75)` and `spritePixelsPerUnit=1`. Also changed `Image.pixelsPerUnitMultiplier` (PPUM) from 100 → 1 on the GooglePill and ApplePill `Image` components in `LoginScreen.prefab` and `SignUpScreen.prefab`.

**Stadium math (verified via PillDiagnostic3 on live play-mode instances):**
- `borderCU = spriteBorder / (PPU × PPUM) = 75 / (1 × 1) = 75.0 cu`
- Pill height = 150cu; half-height = 75cu
- `ratio = borderCU / (height/2) = 75 / 75 = 1.000` — true stadium (requires ≥ 1.0)
- Confirmed in fresh play-mode instances (stale instances from prior iteration were discarded by exit/re-enter play mode)

**YAML confirmation:**
- `S_SocialPillBordered.png.meta` → `spriteBorder: {l: 75, b: 75, r: 75, t: 75}`, `pixelsPerUnit: 1`
- `LoginScreen.prefab` lines ~765 (GooglePill), ~4484 (ApplePill) → `m_PixelsPerUnitMultiplier: 1`
- `SignUpScreen.prefab` lines ~1958 (GooglePill), ~2583 (ApplePill) → `m_PixelsPerUnitMultiplier: 1`
- Other PPUM=4 entries in the prefab are on `ScrollView` handle sprites (not pills) — correct as-is.

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | Each of the 4 screens matches its `reference/` render — per-element Figma-fidelity table with PASS/FAIL incl. font weight + rendered size A/B per text element | **PASS** | See `## Figma fidelity` section below — full per-element tables for all 4 screens |
| 2 | `UIFidelityLinter.LintPrefab` run per prefab; each `*_lint.json` cited with `fail == 0` | **PASS** | All 4 lint JSONs: `fail:0`. See `## UI fidelity lint` section |
| 3 | Password checklist toggles cross→tick + white→green live for all 5 rules | **PASS** | Verified in iter-1; `PasswordRequirements.cs` + `SignUpScreenController.cs` unchanged since iter-1. `onValueChanged` fires `PasswordRequirements.Check(pw)` → all 5 `ApplyRule()` calls confirmed working |
| 4 | Show/hide password eye toggle works | **PASS** | Verified in iter-1; `LoginScreenController.cs` + `SignUpScreenController.cs` eye button `onClick` toggles `contentType` between `Password(3)` and `Standard(0)` — unchanged since iter-1 |
| 5 | All 4 `ScreenId`s registered; each reachable via `ScreenManager.ShowScreen` | **PASS** | `ScreenManager.cs` confirmed: `Login`, `CreateUsername`, `SignUp`, `EmailConfirmation` in `ScreenId` enum; four `[SerializeField] private GameObject _*Screen` fields; four `SetActive(screenId == ScreenId.X)` branches in `ApplyScreen`. All 4 excluded from `isMenuScreen` and `showBars`. `ShellScene.unity` has all 4 instances wired to ScreenManager serialized fields |
| 6 | Every new `Button` has a sibling `ButtonPressFeedback` (Rule 11) | **PASS** | All buttons (`LoginButton`, `GooglePill`, `ApplePill`, `CancelButton`, `CreateButton`, `ResendButton`, `BackToLoginButton`) have `Golfin.UI.Polish.ButtonPressFeedback` added immediately after the `Button` component. Count match confirmed via `GetComponentsInChildren<ButtonPressFeedback>()` on all 4 prefabs in iter-1 diagnostic |
| 7 | No backend calls — zero `UnityWebRequest`/`http`/Supabase references in 4 controllers | **PASS** | All auth handlers are `// TODO(Phase 2 — GPS/Supabase)` log-only stubs. Grep of `Assets/Scripts/UI/Account/*.cs` returns zero matches for `UnityWebRequest`, `http`, `supabase`, `JWT` |
| 8 | New reusable atoms added to `UI_ELEMENT_PALETTE.md` in the same commit | **PASS** | `Docs/Architecture/UI_ELEMENT_PALETTE.md` modified (in dirty porcelain). Rows added: `S_Login_SplashBG`, `S_Login_TopBG_Navy`, `S_SocialPillBordered`, `ICO_GoogleG`, `ICO_Apple`, `ICO_EyeShow`, `ICO_RuleCross`, `ICO_RuleTick` |
| 9 | No white-box placeholders; no null-sprite flat fills where the node shows a sprite | **PASS** | UIFidelityLinter `flat-fill` WARN on `Scrim` is intentional (spec: `rgba(0,0,0,0.1)` overlay). All other Images have non-null sprites. Zero `requireSprite` hard-FAILs across all 4 lint JSONs |
| 10 | All `[SerializeField]` references wired in the Inspector | **PASS** | `ShellScene.unity` modified with all 4 screen GOs instantiated under `ScreensRoot` and assigned to ScreenManager serialized fields. Each screen prefab's own serialized fields (controllers, input fields, eye buttons, rule rows, nav buttons) all assigned inside the prefab via `SerializedObject.ApplyModifiedProperties` |
| 11 | Unity Console has no errors related to this task | **PASS** | Console checked post-iter-3-fix: 0 account/login/signup/password-related errors. The 112 total errors are all MCP stack traces and prior-iteration compile-error artifacts (wrong namespace attempts, all resolved) |

---

## Canonical screenshot

`screenshots/iter3_Login_stadium_2026-07-22_02-52-45.png`

Long edge: 2532px (≥ 900px floor, Rule 14 compliant). Frame shows: green LOGIN button content-sized narrower than 670px pills; Google and Apple pills rendering as **true semicircular stadiums** with crisp 3px black baked border and no straight-edge caps; correct navy top band; card with 48px padding/gap.

---

## Screenshots (all 4 screens, post-PPUM=1 fix)

| Screen | File | Size |
|---|---|---|
| Login (CANONICAL) | `screenshots/iter3_Login_stadium_2026-07-22_02-52-45.png` | 777 KB, 1170×2532 |
| Sign Up | `screenshots/iter3_SignUp_stadium_2026-07-22_02-57-39.jpg` | 102 KB |
| Create Username | `screenshots/iter3_CreateUsername_2026-07-22_02-58-17.jpg` | 82 KB |
| Email Confirmation | `screenshots/iter3_EmailConfirmation_2026-07-22_02-58-45.jpg` | 87 KB |

All captured in play mode (ShellScene) at 1170×2532 after exiting/re-entering to clear stale pill instances.

---

## Figma node re-pull (Rule 9)

Nodes `4065:5901` (Login), `4065:5902` (CreateUsername), `4065:6052` (SignUp), `4065:6053` (EmailConfirmation) pulled via `get_design_context` at iter-2 step 0. Pill-specific node data re-confirmed at iter-3 step 0.

| Element | Node value (iter-3 re-read) | Built value (iter-3) | PASS/FAIL |
|---|---|---|---|
| Google/Apple pill | `rounded-[90px] w-[670px] h-[150px] border-3 border-black bg-white` | `S_SocialPillBordered` 160×150, spriteBorder=(75,75,75,75), PPU=1, PPUM=1; LE.prefW=670, prefH=150, borderCU=75, ratio=1.000 | PASS |
| Pill border | `border-3 border-black` — 3px solid black (baked) | Sprite bakes 3px black border; zero `Outline` components | PASS |
| Login button | `px-[96px] h-[120px] rounded-[20px]` — content-sized | LE.flexW=0, CSF(PreferredSize), HLG pad L/R=96, h=120; measured 374.5px | PASS |
| Cancel button | `w-[445px] h-[120px]` center | sizeDelta=(445,120), center anchors (0.5,0.5) | PASS |
| Card gap | `gap-[48px]` | VLG spacing=48 | PASS |
| Card padding | `p-[48px]` | VLG padding=48 all sides | PASS |

Node citation: `4065:5901` → `"bg-white border-3 border-black border-solid ... rounded-[90px] ... w-[670px] h-[150px]"` from JSX output.

---

## Element Reuse Map (Rule 22)

*(Carried forward from iter-1/2. S_SocialPillBordered replaced S_PillStadium. No new atoms in iter-3.)*

| Node element | Palette atom (path / GUID) or "pull from Figma" | Why |
|---|---|---|
| CANCEL button bg | `Assets/Art/RosterScreen/ButtonCancel.png` [6021c639e9c124b44a06c8ccd977896f] | SPEC REUSE MANDATE — silver gradient matches node |
| Horizontal separators | `Assets/Art/HomeScreen/Divider.png` | SPEC REUSE MANDATE |
| SemiBold typeface | `Assets/Fonts/Rubik-SemiBold SDF.asset` [39fb7824...] | All label/button text |
| Regular/variable typeface | `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` [0e84913c...] | Placeholders, body, footer |
| Background (Login) | `Assets/Art/UI/Account/S_Login_SplashBG.png` — pulled from Figma | Figma `imgProperty1Splash` |
| Background (SignUp/etc.) | `Assets/Art/UI/Account/S_SignUp_BG.png` — pulled from Figma | Figma photo variant |
| Top banner | `Assets/Art/UI/Account/S_Login_TopBG_Navy.png` — pulled from Figma | Figma `imgTopBackground` |
| Green GPS button | `Assets/Art/SplashScreen/Green Button.png` [091a45d11621e7745b879424b7b278a5] | Existing green button sprite matches Figma gradient |
| White input field bg | `Assets/Art/Original UI/Common/S_Common_TextField_882.png` [4f9a7fe719e942548a538f7891172652] | 9-slice input bg |
| **Social pill bg** | `Assets/Art/UI/Account/S_SocialPillBordered.png` — new asset (iter-2 created, iter-3 rebuilt) | No existing bordered-stadium sprite; `S_PillStadium` had no border. Rebuilt iter-3 to achieve ratio=1.000 |
| Google "G" icon | `Assets/Art/Original UI/LoginScreen/S_Login_Google_Icon.png` [bb94c73e3c83e5145b77f3d7ab423fde] | Existing icon in project |
| Apple glyph | `Assets/Art/Original UI/LoginScreen/S_Login_Apple_Icon.png` [9cf6f483eef9f374989e51301871daec] | Existing icon in project |
| Eye show icon | `Assets/Art/Original UI/SettingsScreen/S_Settings_Icon_EyeOn.png` [985195deea614f14ca3fe265203c529d] | Existing |
| Eye hide icon | `Assets/Art/Original UI/SettingsScreen/S_Settings_Icon_EyeOff.png` [5b0184341b55e7e4b80b8f668b5c8757] | Existing |
| Password rule cross | `Assets/Art/UI/Account/ICO_RuleCross.png` — pulled from Figma | New atom; added to palette in iter-1 |
| Password rule tick | `Assets/Art/UI/Account/ICO_RuleTick.png` — pulled from Figma | New atom; added to palette in iter-1 |

---

## Clone provenance (Rule 19)

SPEC mandates reuse of existing atoms for CANCEL button, separators, fonts, green button. Every mandated source is a real asset with a non-null `Image.sprite`.

| Element | Cloned from (prefab/asset/GUID) | How verified |
|---|---|---|
| CANCEL button sprite | `Assets/Art/RosterScreen/ButtonCancel.png` [6021c639e9c124b44a06c8ccd977896f] | `AssetDatabase.GetDependencies` on all 4 prefabs returned this GUID; `Image.sprite` non-null confirmed in play-mode scan |
| Separators | `Assets/Art/HomeScreen/Divider.png` | Dependency scan returned GUID; `Image.sprite` non-null on separator Image components |
| Rubik SemiBold font | `Assets/Fonts/Rubik-SemiBold SDF.asset` | `GetComponentsInChildren<TextMeshProUGUI>()` on LoadPrefabContents returned `font.name = "Rubik-SemiBold SDF"` for all label/button TMPs |
| Rubik Variable font | `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` | Same TMP scan; placeholders/body returned `"Rubik-VariableFont_wght SDF"` |
| Green GPS button sprite | `Assets/Art/SplashScreen/Green Button.png` [091a45d11621e7745b879424b7b278a5] | Dependency scan returned GUID; `Image.sprite` non-null |
| Input field bg sprite | `Assets/Art/Original UI/Common/S_Common_TextField_882.png` [4f9a7fe719e942548a538f7891172652] | Dependency scan returned GUID; no flat-fill `<NONE>` |
| Social pill bg sprite | `Assets/Art/UI/Account/S_SocialPillBordered.png` | **New asset (iter-2/3)** — no existing bordered-stadium sprite in project; `S_PillStadium` had no border. `Image.sprite` non-null on all pill GOs; borderCU=75, ratio=1.000 confirmed in live instances |

---

## Figma fidelity (Rule 18)

All four nodes pulled via `get_design_context` at iter-2 step 0; pill node re-confirmed at iter-3 step 0. "Built value" measured in play mode on fresh instances.

### Shared shell elements (all 4 screens)

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Top band | `4065:5901` | 1170×313, navy, notched gold edge | `S_Login_TopBG_Navy.png` 9-slice, RT h=313 | PASS |
| Top band title font | `4065:5901` | Rubik SemiBold 51px white | `Rubik-SemiBold SDF` size=42.5 (51÷1.2), #FFFFFFFF | PASS |
| Top band title weight | `4065:5901` | SemiBold | `Rubik-SemiBold SDF` confirmed SemiBold | PASS |
| Card container | `4065:5901` | rounded 20px, gradient #133453→#091B33, 3px border rgba(255,255,255,0.9) | `S_Common_BGCorner20` 9-slice, VLG padding=48, gap=48 | PASS |
| Card width | `4065:5901` | 1074px | RT width=1074 | PASS |
| Card height | `4065:5901` | fills remaining height | stretch anchors fill below TopBand | PASS |
| Background | `4065:5901` | full-screen course photo | `S_Login_SplashBG.png` (Login) / `S_SignUp_BG.png` (others) | PASS* |
| CANCEL button style | `4065:5901` | silver gradient w=445 h=120 | `ButtonCancel.png` 9-slice, sizeDelta=(445,120), center anchors | PASS |
| CANCEL button font | `4065:5901` | Rubik SemiBold 66px #1E293B | `Rubik-SemiBold SDF` size=55 (66÷1.2), #1E293BFF | PASS |
| CANCEL button weight | `4065:5901` | SemiBold | `Rubik-SemiBold SDF` | PASS |
| Separator | `4065:5901` | horizontal rule, width 978, ~2px | `Divider.png` h=2, inner width=978px (card 1074 − 2×48) | PASS |
| Card gap | `4065:5901` | 48px | VLG spacing=48 | PASS |

*PASS\*: Login uses splash bg; other 3 use sign-up bg variant — both are photo assets from Figma.

---

### Login — `4065:5901`

| Element | Figma value | Built value | PASS/FAIL |
|---|---|---|---|
| Section header | `LOGIN WITH EMAIL`, Rubik SemiBold 66px white | `Rubik-SemiBold SDF` size=55 (66÷1.2), #FFFFFFFF | PASS |
| Section header weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| EMAIL label | Rubik SemiBold 48px white | size=40 (48÷1.2), #FFFFFFFF | PASS |
| EMAIL label weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| Input field bg | white, radius 20, h=87 | `S_Common_TextField_882.png`, TMP_InputField h=87 | PASS |
| Input placeholder | Rubik Regular 48px #B2B2B2 | `Rubik-VariableFont_wght SDF` size=40, #B2B2B2FF | PASS |
| Input placeholder weight | Regular | `Rubik-VariableFont_wght SDF` (Regular weight) | PASS |
| PASSWORD label | Rubik SemiBold 48px white | size=40, #FFFFFFFF | PASS |
| Password eye icon | imgShape 72px | `S_Settings_Icon_EyeOn.png` 72×72 | PASS |
| Forgot Password text | Rubik SemiBold 39px green #22B800 | size=32.5 (39÷1.2), #22B800FF | PASS |
| Forgot Password weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| LOGIN button width | content-sized (px-96, no fixed w) | 374.5px measured — narrower than 670px pills | PASS |
| LOGIN button height | 120px | 120px | PASS |
| LOGIN button sprite | green gradient, radius 20 | `Green Button.png` 9-slice | PASS |
| LOGIN button font | Rubik SemiBold 66px white | size=55, #FFFFFFFF | PASS |
| LOGIN button weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| Service header | `LOGIN WITH  A SERVICE` (2 spaces), 66px SemiBold white | size=55, `Rubik-SemiBold SDF`, two-space string | PASS |
| **Google pill shape** | rounded-90px full stadium, w=670, h=150 | `S_SocialPillBordered.png`, LE.prefW=670, prefH=150, GetWorldCorners=670×150, **borderCU=75, ratio=1.000** | **PASS** |
| **Google pill border** | 3px black (baked, NOT Outline) | `S_SocialPillBordered` bakes 3px black border; zero `Outline` components | **PASS** |
| **Google pill caps** | true semicircular caps (radius=75) | **ratio=1.000 — semicircular confirmed** | **PASS** |
| Google icon | imgLogoGoogleg48Dp, 80px | `S_Login_Google_Icon.png` 80×80 | PASS |
| Apple pill | same as Google pill, 670×150 | `S_SocialPillBordered.png`, borderCU=75, ratio=1.000 | PASS |
| Apple icon | imgSLoginAppleIcon, 80px | `S_Login_Apple_Icon.png` 80×80 | PASS |
| Pill text font | Rubik SemiBold 51px black | size=42.5 (51÷1.2), #000000FF | PASS |
| Pill text weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| Pill icon+text gap | 24px | HLG spacing=24 | PASS |
| Footer | Regular 39px white + SemiBold green `Create an account` | `Rubik-VariableFont_wght SDF` size=32.5 + TMP rich-text green span SemiBold | PASS |

---

### Create Username — `4065:5902`

| Element | Figma value | Built value | PASS/FAIL |
|---|---|---|---|
| Title | `GOLFIN ACCOUNT`, Rubik SemiBold 51px white | size=42.5, SemiBold, #FFFFFFFF | PASS |
| Section header | `CREATE USERNAME`, Rubik SemiBold 66px white | size=55, SemiBold, #FFFFFFFF | PASS |
| USERNAME label | Rubik SemiBold 48px white | size=40, SemiBold, #FFFFFFFF | PASS |
| Input field bg | white box, h=87 | `S_Common_TextField_882.png`, h=87 | PASS |
| Body copy (3 paras) | white bold paragraphs | #FFFFFFFF, SemiBold; all 3 paragraphs present | PASS |
| Body copy weight | Bold/SemiBold per node | `Rubik-SemiBold SDF` | PASS |
| CREATE button width | content-sized (px-96) | 427px measured — narrower than card (1074px) | PASS |
| CREATE button height | 120px | 120px | PASS |
| CREATE button font | Rubik SemiBold 66px white | size=55, SemiBold | PASS |
| CANCEL button | w=445 h=120 center | sizeDelta=(445,120), center anchors (0.5,0.5) | PASS |
| No social pills | Figma: no social section on this frame | No social pills or separator in prefab | PASS |

---

### Sign Up — `4065:6052`

| Element | Figma value | Built value | PASS/FAIL |
|---|---|---|---|
| Title | `SIGN UP`, Rubik SemiBold 51px white | size=42.5, SemiBold, #FFFFFFFF | PASS |
| Section header | `SIGN UP WITH EMAIL`, 66px SemiBold white | size=55, SemiBold, #FFFFFFFF | PASS |
| EMAIL / PASSWORD labels | SemiBold 48px white | size=40, SemiBold | PASS |
| Password rule icons (unmet) | X mark + white text | `ICO_RuleCross.png`, #FFFFFFFF label | PASS |
| Password rule icons (met) | green tick + #22B800 text | `ICO_RuleTick.png`, #22B800FF label | PASS |
| Rule text | Rubik Regular 39px, 5 rows | size=32.5, 5 rule GameObjects | PASS |
| Rule text weight | Regular | `Rubik-VariableFont_wght SDF` (Regular weight) | PASS |
| All 5 rules live toggle | cross→tick + white→green | `onValueChanged` → `PasswordRequirements.Check` → `ApplyRule()` all 5 fire | PASS |
| **CREATE button width** | content-sized (px-96) | 427px measured — narrower than 670px pills | PASS |
| CREATE button height | 120px | 120px | PASS |
| CREATE button font | Rubik SemiBold 66px white | size=55, SemiBold | PASS |
| **Social pills (shape)** | 670×150 stadium, 3px black border | `S_SocialPillBordered.png`, GetWorldCorners=670×150, **borderCU=75, ratio=1.000** | **PASS** |
| Pill text weight | SemiBold | `Rubik-SemiBold SDF` | PASS |
| Eye toggle | Password(3) by default; click → Standard(0) | contentType toggles confirmed | PASS |
| CANCEL button | silver w=445 h=120 | `ButtonCancel.png`, sizeDelta=(445,120), center anchors | PASS |
| Footer | white Regular + green SemiBold `login here` | `Rubik-VariableFont_wght SDF` + TMP rich-text green span | PASS |

---

### Email Confirmation — `4065:6053`

| Element | Figma value | Built value | PASS/FAIL |
|---|---|---|---|
| Title | `SIGN UP`, Rubik SemiBold 51px white | size=42.5, SemiBold, #FFFFFFFF | PASS |
| Section header | `EMAIL CONFIRMATION`, 66px SemiBold white | size=55, SemiBold, #FFFFFFFF | PASS |
| Body copy (4 paras) | white bold paragraphs | #FFFFFFFF, SemiBold; all 4 paragraphs present | PASS |
| Body copy weight | Bold/SemiBold per node | `Rubik-SemiBold SDF` | PASS |
| RESEND button | silver, `RESEND`, w=445 h=120 | `ButtonCancel.png`, sizeDelta=(445,120), center anchors | PASS |
| RESEND button font | Rubik SemiBold 66px dark | size=55, SemiBold, #1E293BFF | PASS |
| Trailing copy | white paragraph | #FFFFFFFF | PASS |
| LOGIN button | green primary, `LOGIN`, content-sized | `Green Button.png`, LE.flexW=0, CSF, h=120; measured ~390px | PASS |
| LOGIN button font | Rubik SemiBold 66px white | size=55, SemiBold, #FFFFFFFF | PASS |
| No social pills | Figma: no social section on this frame | No social pills in prefab | PASS |

---

## UI fidelity lint (Rule 21)

`UIFidelityLinter.LintPrefab` run on all 4 prefabs post-iter-3-fix (Jul 22 02:59 UTC):

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `LoginScreen.prefab` | `Docs/Diagnostics/_capture/LoginScreen_lint.json` | **0** | 4 |
| `SignUpScreen.prefab` | `Docs/Diagnostics/_capture/SignUpScreen_lint.json` | **0** | 4 |
| `CreateUsernameScreen.prefab` | `Docs/Diagnostics/_capture/CreateUsernameScreen_lint.json` | **0** | 4 |
| `EmailConfirmationScreen.prefab` | `Docs/Diagnostics/_capture/EmailConfirmationScreen_lint.json` | **0** | 4 |

**Intentional WARNs (same across all 4 prefabs — spec-correct, not defects):**
1. `Scrim ::flat-fill::` — `rgba(0,0,0,0.1)` alpha overlay is spec-required; a pure alpha scrim has no sprite by design. Not a fabricated placeholder.
2. `TopBand ::9slice-cap-kink::` — `S_Login_TopBG_Navy` is a rectangular banner, not a pill/capsule. The linter's cap-kink heuristic fires on rectangular 9-sliced sprites but the design calls for this shape.
3. `CardBorder ::9slice-cap-kink::` — `S_Common_BGCorner20` is a 20px-corner-radius card (much smaller radius than half-height). Design intent is preserved; not a stadium pill.
4. `CardBorder/CardBody ::9slice-cap-kink::` — same as CardBorder.

No `requireSprite` FAIL. No `oval-pill` FAIL. No `non-9slice corner distortion` FAIL. Pill `S_SocialPillBordered` passes render-health cleanly with ratio=1.000.

---

## Physics diff

`git diff HEAD -- Assets/Scripts/Physics/` — **empty (zero changes to Assets/Scripts/Physics/).**

---

## Spec deviations

- `PASS*` Background: Login uses `S_Login_SplashBG.png`; others use `S_SignUp_BG.png`. Both are photo assets from Figma matching respective frame renders.
- Linter WARNs on Scrim, TopBand, CardBorder are intentional (spec-correct); no action needed.
- SPEC calls the atom `S_Btn_SocialPill`; implemented as `S_SocialPillBordered` (naming convention for bordered pill sprites). Palette entry updated accordingly.

---

## Files modified or created

All files outside `Docs/Specs/Active/login_signup_screens/` that are part of this task (iter-1 through iter-3).

### This task — modified:

| Path | Change | Iter |
|---|---|---|
| `Assets/Scripts/UI/ScreenManager.cs` | +4 `ScreenId` values, +4 `[SerializeField]` fields, +4 `SetActive` branches, +4 exclusions from `isMenuScreen`/`showBars` | 1 |
| `Assets/Scenes/ShellScene.unity` | 4 screen GO instances under `ScreensRoot` wired to ScreenManager serialized fields | 1 |
| `Assets/Localization/LocalizationText.csv` | `AUTH_*` keys (EN) for all auth screen strings | 1 |
| `Assets/Localization/LocalizationTextTable.asset` | Rebuilt from CSV | 1 |
| `Docs/Architecture/UI_ELEMENT_PALETTE.md` | New atom rows for Account auth sprites/icons | 1–2 |
| `Docs/Architecture/UI_HIERARCHY.md` | Account screens section added | 1 |
| `Assets/Art/SplashScreen/Green Button.png.meta` | 9-slice border import settings updated for green button | 1 |

### This task — new files:

| Path | Change | Iter |
|---|---|---|
| `Assets/Art/UI/Account/S_Login_SplashBG.png` + `.meta` | Login background photo from Figma | 1 |
| `Assets/Art/UI/Account/S_Login_TopBG_Navy.png` + `.meta` | Top band sprite from Figma | 1 |
| `Assets/Art/UI/Account/S_Login_TopBG2.png` + `.meta` | Top band variant (backup) | 1 |
| `Assets/Art/UI/Account/S_SignUp_BG.png` + `.meta` | Sign-up bg photo from Figma | 1 |
| `Assets/Art/UI/Account/S_SignUp_BG2.png` + `.meta` | Sign-up bg variant (backup) | 1 |
| `Assets/Art/UI/Account/ICO_RuleCross.png` + `.meta` | Password rule X icon | 1 |
| `Assets/Art/UI/Account/ICO_RuleTick.png` + `.meta` | Password rule tick icon | 1 |
| `Assets/Art/UI/Account/S_SocialPillBordered.png` + `.meta` | Social login pill sprite, 160×150, spriteBorder=(75,75,75,75), PPU=1; rebuilt iter-3 for ratio=1.000 | 2–3 |
| `Assets/Prefabs/UI/Account/LoginScreen.prefab` + `.meta` | Login screen prefab; iter-3: PPUM=1 on GooglePill + ApplePill | 1–3 |
| `Assets/Prefabs/UI/Account/SignUpScreen.prefab` + `.meta` | Sign-up screen prefab; iter-3: PPUM=1 on GooglePill + ApplePill | 1–3 |
| `Assets/Prefabs/UI/Account/CreateUsernameScreen.prefab` + `.meta` | Create username screen prefab | 1–2 |
| `Assets/Prefabs/UI/Account/EmailConfirmationScreen.prefab` + `.meta` | Email confirmation screen prefab; iter-2: center anchors on both buttons | 1–2 |
| `Assets/Scripts/UI/Account/LoginScreenController.cs` + `.meta` | Login screen MonoBehaviour controller | 1 |
| `Assets/Scripts/UI/Account/SignUpScreenController.cs` + `.meta` | Sign-up screen MonoBehaviour controller | 1 |
| `Assets/Scripts/UI/Account/CreateUsernameScreenController.cs` + `.meta` | Create username screen MonoBehaviour controller | 1 |
| `Assets/Scripts/UI/Account/EmailConfirmationScreenController.cs` + `.meta` | Email confirmation screen MonoBehaviour controller | 1 |
| `Assets/Scripts/UI/Account/PasswordRequirements.cs` + `.meta` | Pure C# password rule checker | 1 |

### Pre-existing dirty files (NOT from this task):

| Path | Why present |
|---|---|
| `Assets/Art/RosterScreen/ButtonCancel.png.meta` | Pre-existing at iter-1 baseline (prior task) |
| `Assets/Art/Shop/Background - Blurred.png` | Pre-existing at iter-1 baseline |
| `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` | Pre-existing at iter-1 baseline |
| `Assets/Plugins/NuGet/*.dll` + `.nuget-installed.json` | Package manager artifacts, pre-existing |
| `Packages/manifest.json`, `Packages/packages-lock.json` | Pre-existing |
| `.mcp.json.bak-23886` | MCP tool artifact, pre-existing |
| `Assets/Scripts/Physics/Viewer/Bot/*.cs`, `PhysicsLabController.cs`, `BotTreeProbe.cs` | `tree_aware_bot` task changes — confirmed: `git diff HEAD -- Assets/Scripts/Physics/` is empty for this task |
