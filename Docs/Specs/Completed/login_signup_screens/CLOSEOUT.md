# CLOSEOUT — login_signup_screens

**Status:** DONE — Cesar approved 2026-07-22.

After the implementer pipeline (iter-1..3), Cesar rejected several fidelity issues and directed the
architect (main Claude Code thread) to fix them directly against the Figma node
(`5gEAHjl6xAtW8iYY7NMvWd`, nodes 4065:5901 / 5902 / 6052 / 6053). Final changes, all verified in
real play-mode renders at 1170×2532 through `ScreenManager.ShowScreen`:

1. **Social pills → true stadium capsules.** Baked a full-capsule sprite
   `Assets/Art/UI/Account/S_SocialPillBordered.png` (radius = ½ height, 3px black border, Figma
   `rounded-90`) used as `Image.Type.Simple`; disabled the child `Inner` (`S_PillStadium`, sliced)
   fill that was painting a rounded-rectangle over it. (Login + Sign Up.)

2. **Section labels left-aligned.** `EMAIL` / `PASSWORD` / `USERNAME` given full row width
   (`LayoutElement.preferredWidth = 978`) + `TextAlignmentOptions.Left` + 16px left margin (Figma
   text x=16). Previously 134px content-width boxes centered by the VLG.

3. **48px bottom gap.** Card `Content` now fills the viewport (CSF vertical → Unconstrained, anchors
   stretched) with a flex-grow `ServiceSpacer` (`LayoutElement.flexibleHeight = 1`) before the last
   separator, so the footer/cancel sit ~48px off the card bottom (Figma flex-grow layout).

4. **Fabricated `TopBand` removed** from all four prefabs; the title now rides the shared
   `PersistentUI/TopBar` (real notched banner). New `PersistentUIManager.ShowAccountTitleBar(title)`
   shows banner + centered title only and strips reward-points/shop/ticket/settings chrome (user is
   pre-login); `ScreenManager` routes the four account screens to it with titles
   `GOLFIN ACCOUNT` (Login/CreateUsername) and `SIGN UP` (SignUp/EmailConfirmation).

5. Bold green link text (`Create an account` / `login here`); LOGIN button pinned to 388px centered.

**Cesar decision:** sparse cards (Create Username, Email Confirmation) keep the fill layout (48px
bottom gap) rather than hugging content.

**Canonical renders:** `screenshots/iter4_01_Login.png`, `iter4_03_SignUp.png`,
`iter4_02_CreateUsername.png`, `iter4_04_EmailConfirmation.png` (copied to
`Docs/Reports/Media/login_signup_screens_*.png`).

**Files touched (this close-out):** `Assets/Scripts/UI/ScreenManager.cs`,
`Assets/Scripts/UI/PersistentUIManager.cs`, `Assets/Prefabs/UI/Account/*.prefab`,
`Assets/Art/UI/Account/S_SocialPillBordered.png`.
