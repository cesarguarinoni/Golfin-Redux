# SPEC — `auth_golf_profile`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. (Standard pipeline states — SPEC_READY → IMPLEMENTER_WORKING → … → DONE.)

## The nine build rules from `gps_profile_pack` apply verbatim

Bake gradients from tokens via scripts, never tint or hand-edit PNGs; translucency through `GpsUiColor.A()`/`ADark()` against the real backdrop, never over-painted; icon rings are the navy-disc(#112D4F)-in-gold-ring atom; Main Buttons labels size 59 (≤18 chars); geometry-JSON + invariants + UI-fidelity lint gates with the numbers quoted; SemiBold white for interactive text; **every new text key PUBLISHED, not just CSV'd**; Editor builder scripts are the prefab source of truth; reuse existing atoms (`S_HUB_*`, `S_GpsIconRing_*`, GPS Icons, `Next Hole Panel.png` 9-slice inset rule). See `Docs/Specs/Completed/gps_profile_pack/SPEC.md` §Build rules for the full text.

## Goal

The post-signup **Golf Profile** capture and **Welcome tutorial** — the last two Auth-extras frames. After first arrival at Home (GPS builds only), the player is offered once: pick an avatar colour, confirm nickname, pick golf experience, optionally enter handicap → SAVE (or Skip) → a one-page Welcome tutorial → GET STARTED lands in the GPS hub. Data persists to the PLAYLIFE `profiles` row, which requires a **small additive backend change** (below) — the first of this build; Cesar has pre-approved the shape by choosing this spec.

## Reference

- **Figma frames:** `Auth - Golf Profile (post-signup)` `14029:33628` and `Auth - Welcome Tutorial` `14029:33929`, page GPS / PLAYLIFE, file `5gEAHjl6xAtW8iYY7NMvWd`.
- **Node renders in `reference/`:** `golf_profile_14029-33628.png`, `welcome_14029-33929.png` (1170×2532, pulled 2026-09-02). Ground truth for A/B.
- Both frames: `Backgrounds` variant **Splash**; `GPS Nav Bar Container` **hidden**; game `Top UI` present.
- Placeholder content: "Misaki" is a nickname sample; "e.g. 18.4" is hint text; the swatch initials are empty TEXT nodes (initial renders only after a nickname exists — first letter, uppercase).

## Figma Fidelity (values pulled fresh from the nodes 2026-09-02)

| Element | Figma node | Property → value |
|---|---|---|
| Golf Profile Panel | `14029:33885` | 958×731 at (10,0) in Content Container; fill GRADIENT #133453→#091b33 (vertical); stroke 3 GRADIENT #ffffff→#d1d5db(0.4)→#818ea1; r50; = the standard panel atom (`S_HUB_Panel` family) — reuse it |
| Intro Title | `14029:33887` | "SET UP YOUR GOLF PROFILE", Rubik SemiBold 36, #eedc9a, centred, y=30 |
| Intro Sub | `14029:33888` | "You can change all of this later in Settings", Rubik Medium 24, #b7c3d3, centred |
| Colour swatches row | `14029:33890` | 492 wide, centred; four discs: unselected **100×100** (y+10), selected **120×120** (y0) |
| Swatch PINK | `14029:33892` | vertical GRADIENT **#e57a97→#b84e6b**, stroke #f3ecc2 w4 |
| Swatch GREEN (selected state shown) | `14029:33895` | vertical GRADIENT **#4fa36b→#2d6f45**, stroke **#eedc9a w8** (selected = 120px + gold w8; unselected = 100px + #f3ecc2 w4) |
| Swatch BLUE | `14029:33898` | vertical GRADIENT **#4f86d6→#2c5aa0**, stroke #f3ecc2 w4 |
| Swatch GOLD | `14029:33901` | vertical GRADIENT **#c7a04a→#8a6a22**, stroke #f3ecc2 w4 |
| Swatch initial | `14029:33893` etc. | first letter of nickname, Rubik SemiBold 42 (50 on the selected 120px disc), #ffffff, centred |
| Colour Label | `14029:33903` | "Pick your avatar colour", Rubik Medium 24, #b7c3d3, centred under the row |
| Field label (both fields) | `14029:33905`/`33918` | "NICKNAME" / "HANDICAP (OPTIONAL)", Rubik Medium 24, #b7c3d3, left |
| Input box (both) | `14029:33906`/`33919` | 878×80; fill **#000000@0.35** (→ `ADark(black,0.35)` per Build rule 2); stroke #818ea1 w2; **r24**; text Rubik Medium 30 — value #ffffff, hint #7d8a99 ("e.g. 18.4") |
| Experience chips | `14029:33910` | three chips 284.67×60, gap 12, full-row; **unselected**: fill ADark(black,0.35), stroke #818ea1 w2, r100, label Rubik Medium 28ish → match node (#ffffff… see render) | 
| Chip selected | `14029:33913` | fill GRADIENT **#f3ecc2→#c9a94f** (the gold Main-Button gradient), stroke **#422100 w1**, r100; label dark (#422100 family — sample the render) |
| Chip labels | `14029:33912/14/16` | "BEGINNER" / "INTERMEDIATE" / "ADVANCED" |
| SAVE PROFILE button | `14029:33922` | Main Buttons Gold instance, 958 wide, label "SAVE PROFILE" size 59 |
| Skip link (Golf Profile) | `14029:33928` | "Skip for now", Rubik Medium 26, #b7c3d3, centred, below the button |
| Welcome Panel | `14029:34188` | 958×385; same panel atom; ring 150×150 (navy-disc gradient #204b76→#0b203d, gold-ring stroke #f3ecc2→#98855b w8.33) with 74px Rounds icon; title "WELCOME TO GOLFIN GPS" Rubik SemiBold 40 #eedc9a; sub Rubik Medium 28 **#ffffff** 860 wide centred; dots: 40×14 pill #eedc9a active + three 14×14 #ffffff@0.35 (static, decorative) |
| Feature tiles ×4 | `14029:34205/15/24/32` | 470×228, panel atom fill/stroke r50, two rows gap 18/24; ring 96×96 (icon 48, ring stroke w5.33); name Rubik SemiBold 30 #ffffff; desc Rubik Medium 22 #b7c3d3. SCREENSHOT "AI reads your scorecard" · CHECK IN "Prove it with GPS" · VOTE "Predict and win points" · GIFT "Support your favourite golfers". Icons: Screenshot / Pin / Heart / Gift from the GPS Icons set (already imported as `ICO_Gps*.png`) |
| Welcome Skip row | `14029:34187` | "Skip", Rubik Medium 26, #b7c3d3, right-aligned above the panel |
| GET STARTED button | `14029:34246` | Main Buttons Gold instance, 958 wide, label "GET STARTED" size 59 |

## Backend change (playlife repo — small, additive, deploy required)

The `profiles` columns and the update endpoint don't cover this screen yet. Verified 2026-09-02: `handicap NUMERIC(5,1)` exists (`20260410000000_privacy_and_stats.sql`) but **nothing writes it** — even the Flutter signup screen collects handicap + experience and never submits them (`golfin_signup_screen.dart:47-67`); there is no `golf_experience` or `avatar_color` column at all; `PUT /user/update` (`backend/routers/user.py:89`) accepts only `display_name`/`bio`/`avatar_url`.

1. **Migration** `2026_09_02_golf_profile.sql` (Architect pastes SQL in chat for Cesar; file goes in `supabase/migrations/`):
   `ALTER TABLE public.profiles ADD COLUMN IF NOT EXISTS golf_experience TEXT, ADD COLUMN IF NOT EXISTS avatar_color TEXT;` plus CHECK constraints (`golf_experience IN ('beginner','intermediate','advanced')`, `avatar_color IN ('pink','green','blue','gold')`), both nullable.
2. **`UpdateProfileRequest`** (`user.py:19`): add `handicap: Optional[float] = None`, `golf_experience: Optional[str] = None`, `avatar_color: Optional[str] = None`; validate the enums (422 on bad values); include each in `update_data` only when not None. `display_name` stays required — the Unity client always sends the nickname field's value. The existing 409 unique-username translation and tournament-rename propagation stay untouched and apply.
3. **Deploy** to Fly (`playlife-api`) from the Mac; verify `PUT /user/update` round-trips the three new fields via `GET /user/detail`.

Flutter is unaffected (fields optional). No other endpoint changes. `/user/detail` is `select("*")` so the new columns flow into `UserDetailDto` automatically — add the three optional properties to the DTO (`Golfin.Social/UserDetailDto`).

## Architecture context

- **Asmdef boundaries:** UI + controllers in Assembly-CSharp (`Assets/Scripts/UI/Gps/`, namespace `Golfin.Gps.UI`); DTO additions in `Golfin.Social`. No new asmdefs.
- **Existing code referenced:** `UserService` (`Golfin.Social`) — add an `Update(...)` call wrapping `PUT /user/update` (Endpoints entry exists? check `Endpoints.cs`; add `UserUpdate` if absent); `GpsGate` (from `punch_it_gps_variants`) — both new ScreenIds join its GPS-screen list; `ScreenManager` ShowTopBarOnly group + `PersistentUIManager.NavTitleKeyFor`; `SignUpScreenController.cs:180-188` shows the post-auth landing chain (StartingCharacterSelection / CreateUsername / Home); `GpsHubScreenController` OnEnable pattern (paint cache → subscribe → `client.Run`) for prefill.
- **Builder:** new `GpsAuthExtrasBuilder.cs` in `Assets/Scripts/UI/Gps/Editor/`, following `GpsProfilePackBuilder` (geometry JSON, lint spec.json for BOTH screens including inputs/chips/swatches, panel atoms, baked sprites only where a gradient demands it — the swatch discs are 4 tiny bakes via `Docs/Scripts/make_gps_auth_swatches.py` OR ellipse sprites + gradient bake; prefer a bake script per Build rule 1).

## Implementation

1. **ScreenIds** `GpsGolfProfile`, `GpsWelcome` — registered in `ScreenManager` (SetActive block + ShowTopBarOnly), titles via `NavTitleKeyFor` (`GPS_GOLFPROF_TITLE` / `GPS_WELCOME_TITLE`); **added to `GpsGate`'s screen list** (they are GPS surface; in a "Punch it" build neither shows and the trigger below is a no-op).
2. **Trigger** — data-light, covers every auth path: on Home entry (`HomeScreenController`), if `GpsGate.Enabled && AuthService.Instance.IsSignedIn && !PlayerPrefs.HasKey("gps_profile_prompted")` → `ShowScreen(GpsGolfProfile)`. SAVE and Skip both set `gps_profile_prompted=1`. Per-device once; existing accounts get one offer too (accepted; Cesar runs GPS). NOTE: verify the exact `AuthService` signed-in property name — flag if it differs.
3. **Golf Profile screen** (`GpsGolfProfileScreenController`): prefill nickname from `UserService.LastDetail?.display_name` (and fire the hub-pattern fetch); swatch initial = first letter of the live nickname field, uppercase; selection states per fidelity table (selected disc 120 + gold w8; selected chip gold-gradient). SAVE → `UserService.Update(displayName, handicap?, golfExperience, avatarColor)`; on 409 show the existing duplicate-name treatment (reuse whatever `CreateUsername` shows — reference its controller, don't invent); on success → `GpsWelcome`. Skip → `GpsWelcome` without a write. Handicap parsed as decimal, blank = null; reject non-numeric with the input shake/red used elsewhere if one exists, else just refuse SAVE (NOTE which).
4. **Welcome screen** (`GpsWelcomeScreenController`): static content; GET STARTED → `ShowScreen(GpsHub)`; Skip (top-right) → `ShowScreen(ScreenId.Home)`. Dots static.
5. **Profile-pack payoff:** `GpsProfileScreenController` hero avatar disc gradient picks the `avatar_color` pair from the table above (fallback: gold) — one switch on the detail DTO, no layout change.
6. **Localization** — Build rule 7, the full importer path. ~23 new keys EN+JA in `Assets/Localization/LocalizationText.csv` (`GPS_GOLFPROF_TITLE`, `_SUB`, `_COLOUR_LABEL`, `_NICKNAME`, `_EXPERIENCE`, `_EXP_BEGINNER`, `_EXP_INTERMEDIATE`, `_EXP_ADVANCED`, `_HANDICAP`, `_HANDICAP_HINT`, `_SAVE`, `_SKIP`, `_NAME_TAKEN`; `GPS_WELCOME_TITLE`, `_SUB`, `_FEAT_SS`, `_FEAT_SS_DESC`, `_FEAT_CHECKIN`, `_FEAT_CHECKIN_DESC`, `_FEAT_VOTE`, `_FEAT_VOTE_DESC`, `_FEAT_GIFT`, `_FEAT_GIFT_DESC`, `_GET_STARTED`) → importer PLAN (read verdict line) → APPLY → **publish `texts`** → `export_content.py --check` clean. Stop on CONFLICTS, no `--overwrite-dirty`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Per-element A/B crops vs both reference renders for every fidelity-table row; ΔRGB table for panel fills, input fills, chip states, swatch gradients (Build rule 5).
- [ ] Geometry JSON + invariants audit + lint: `N sites 0 FAIL`, `lint fail=0`, both screens, spec.json covering inputs/chips/swatches/rings.
- [ ] Fresh Editor pass: signed in, Home entry with the PlayerPrefs flag cleared → Golf Profile appears once; SAVE writes and `GET /user/detail` echoes handicap/golf_experience/avatar_color (quote the log); second Home entry shows nothing.
- [ ] 409 path: setting nickname to an existing display_name shows the duplicate treatment, no crash, no flag set... (flag IS set only on completed SAVE/Skip).
- [ ] Skip path writes nothing (quote the absence — no PUT in the log) and still reaches Welcome.
- [ ] GET STARTED lands on GpsHub; Welcome Skip lands on Home; BACK behaviour consistent with the nav-back memory rules.
- [ ] `GpsGate` list includes both new ids; with the gate forced off, the Home trigger is a no-op (EditMode-testable via the two-arg overload).
- [ ] Profile hero disc reflects avatar_color (screenshot with a non-default colour).
- [ ] Importer: PLAN verdict quoted, APPLY, publish, `--check` clean for `texts`; zero hardcoded `.text` literals (grep quoted).
- [ ] Backend: migration applied (Cesar), endpoint deployed, round-trip verified — quote the curl/log.
- [ ] EditMode suite green (full sweep per assembly).
- [ ] Spec deviations flagged with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Gps/GpsGolfProfileScreenController.cs`, `GpsWelcomeScreenController.cs` — NEW.
- `Assets/Scripts/UI/Gps/Editor/GpsAuthExtrasBuilder.cs` — NEW builder (+ `Docs/Scripts/make_gps_auth_swatches.py` if baking).
- `Assets/Prefabs/UI/Gps/GpsGolfProfileScreen.prefab`, `GpsWelcomeScreen.prefab` — NEW (builder output).
- `Assets/Scripts/UI/ScreenManager.cs`, `PersistentUIManager.cs`, `Assets/Scripts/UI/Gps/GpsGate.cs`, `HomeScreenController.cs` — wiring.
- `Golfin.Social` `UserDetailDto` + `UserService.Update`; `Golfin.Net/Endpoints.cs` if the update route is missing.
- `Assets/Scripts/UI/Gps/GpsProfileScreenController.cs` — hero disc colour switch.
- `Assets/Localization/LocalizationText.csv` + importer/publish.
- playlife: `backend/routers/user.py`, `supabase/migrations/2026_09_02_golf_profile.sql`, Fly deploy.

## Smoke evidence

Editor play-mode run of the full funnel (flag cleared → Golf Profile → SAVE → Welcome → GET STARTED → hub) with service logs quoted; screenshots of both screens into `screenshots/`; the A/B crops + ΔRGB table in the report.

## Out of scope (do NOT do these)

- No Settings screen for editing these later ("change later in Settings" copy stands; the screen itself is a future task).
- No changes to the four existing auth screens or the email-confirmation flow.
- No avatar photo upload (`/user/avatar` exists; unused here).
- No Flutter changes.
- No gifts/votes work (next spec).
