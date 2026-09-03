READY_FOR_ARCHITECT_REVIEW

# STATUS — `gps_standalone_shell`

**Current:** `READY_FOR_ARCHITECT_REVIEW` — Claude Code, 2026-09-04, **round 2 complete**.

Round 1 shipped as build 2635 (1.0.0, 427 MB). Round 2 is the `KICKOFF_ADDENDUM.md` list:

| | | |
|---|---|---|
| **R1** real icon | **PASS** | Cesar's `AppIcon_GolfinGps_1024_opaque.png`; the generated placeholder AND its baker deleted. |
| **R2** the size | **PASS** | Total User Assets **555 MB → 98.3 MB**. Golf-only `Resources` subfolders move out for the build and back after, driven by a call-site enumeration — which is what caught `Characters/Homescreen`, loaded by the GPS Avatar screen. |
| **R3** once per account | **PASS** | `gps_profile_prompt_server_flag` landed first (deployed + verified). Both first-run cases re-proven — and the proof found a round-1 defect: the shell's boot resolved the offer itself and jumped over the account-flag wait. |
| **R4** strip refused screens | **PASS** | 15 destroyed, 18 kept; every GPS screen still opens on the stripped scene with zero MissingReference. |
| **R5** uncompressed textures | **PASS** | The four the shell ships → ASTC 6x6 (7.85 MB → ~0.85 MB raw). Game-only ones left to `build_size_diet`. |

**Cesar authorized the upload** ("punch standalone") after round 2's work was in. The Unity half was
verified on its own first — `unity-build-ios.sh standalone` SUCCEEDED at 98.3 MB user assets — then
the full lane ran.

EditMode 2398 / 2395 pass / 0 fail. `ProjectSettings.asset`, `ShellScene.unity` and
`Assets/Resources/**` are all byte-identical to HEAD after every build and proof.

Remaining: device pass §7 (7.1–7.10) and §1b on the new build — including row 6's real
`client_platform == "ios-playlife"`, which only a device can show.

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | Build profile, StandaloneGate + Home rewrite, hub-first boot, chrome, identity, third fastlane lane. |
| 2026-09-03 | `READY_FOR_ARCHITECT_REVIEW` | Round 1 built and self-verified. |
| 2026-09-03 | reviewed PASS → build 2635 | Cesar ran "punch it standalone"; 427 MB, placeholder icon → three asks. |
| 2026-09-04 | `READY_FOR_ARCHITECT_REVIEW` | Round 2: R1–R5 done, 98.3 MB, round-1 boot defect fixed, uploaded. |
