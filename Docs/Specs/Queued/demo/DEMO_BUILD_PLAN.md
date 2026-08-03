# GOLFIN Redux — Demo Build Plan

**Status:** 🟡 **IMPLEMENTED, NOT DEVICE-TESTED** · **Author:** Claude (Architect) · **Rev 6, 2026-08-03**

> 🔴 **VERIFICATION STATE — read before writing anything into this doc.**
> Code landed the implementation in `c7def999a` + `addfa34f0` (build profiles, screen gate, scene stripper, build script, soft-gating, playable Hole 1 flow, welcome banner, splash gate, showcase recorder).
> **Cesar has NOT run this on a device. No device verification of any kind has happened.** As of 2026-08-03 the on-device pass is still outstanding and is Cesar's to run.
> **Do not write, imply, or infer that Cesar tested, checked, saw, or confirmed anything on hardware.** If a claim about where Cesar observed something is not in his own words in the conversation, it does not go in this document — not as a premise, not as background, not as "presumably". This project has already burned iterations on exactly this fabrication (see the provenance scar in `AI_CONTEXT.md`), and it recurred here.
> What *is* verified: Editor-side behaviour per §3.6. What is *not*: everything in the ❌ rows of the §3.6 table — binary size, MCP assembly actually stripped from the player, IL2CPP behaviour, iOS safe area, portrait lock.

**Platform priority:** 📱 **iPhone first, Android second. This Mac is the build machine.**
**Verified against:** `C:\Users\cesar\GolfinRedux` @ Unity **6000.3.9f1** (Rev 2) · re-verified on the Mac `/Users/cesar/Documents/GolfinRedux` @ **6000.3.9f1** (Rev 3–4)
**Implementer:** Claude Code
**Notion:** `demo_build_slice` (Order 426) · prereq `unity_yaml_merge_driver` (429) · `unity_mcp_define_strip` (428) is now **executed inside §4 step 2+3**

> **Rev 6 changelog (2026-08-03):**
> - **Status → IMPLEMENTED, NOT DEVICE-TESTED.** Implementation landed (`c7def999a`, `addfa34f0`); the device pass has not happened.
> - 🔴 **Provenance guard added at the top of this doc**, after Cesar flagged an Architect assumption that he had already device-tested. He had not. Same failure shape as the `ball_trail_shot_isolation` scar.
>
> **Rev 5 changelog (2026-07-30) — "can I test this in the Editor first?":**
> - **New §3.6: Editor testing before any build.** Most of the demo *is* testable in play mode — set the active build profile to `iOS-Demo` and press Play. Full per-mechanism table of what the Editor does and does not prove.
> - 🔴 **Latent NRE caught in §3.3.** `IProcessSceneWithReport.OnProcessScene` also fires when entering play mode, and **`report` is `null` in that case** (Unity 6000.0 docs). Any implementation that reads `report.summary.*` to check the build target crashes on the first Play. Use `BuildPipeline.isBuildingPlayer` — Unity's own documented discriminator. Guard added to §3.6.
> - ⚠️ **Fidelity caveat recorded:** `Awake` is *blocked* during a build's `OnProcessScene` but **not** in play mode, so a stripped screen's `Awake` can run in the Editor and never run in the shipped demo. Screen-gating is safe to judge in play mode; `Awake` side effects are not.
>
> **Rev 4 changelog (2026-07-30) — all triggered by "iPhone first, Mac is the build machine":**
> - **§2.2 merge-driver path was Windows-only and would have failed here.** macOS path now verified *by executing the binary* (v1.0.1): `Unity.app/Contents/Helpers/UnityYAMLMerge`. ⚠️ There is a same-named **directory** at `Contents/Resources/UnityYAMLMerge/` holding the merge spec files — pointing the driver at it is the obvious mistake. Also confirmed: one Unity install (6000.3.9f1, no drift) and `git config` has **no** merge driver today.
> - **§3.1 daily-driver profile `Dev-Android` → `Dev-iOS`.** With iPhone primary, the old default would have caused the §2.1 failure mode it was meant to prevent.
> - **§3.5 `build-demo.ps1` → `build-demo.sh`**, `--platform ios|android`, default `ios`. Android retained as the *fast* loop (direct APK, no Xcode step); iOS is the milestone check.
> - **§4 steps 2 and 3 MERGED.** `unity_mcp_define_strip` (428) and the profile creation both deliver the dev profile — separately they duplicate work. Close 428 when that step lands.
>
> **Rev 3 changelog (2026-07-30):**
> - **Step 0 gate CLEARED.** `surface_classification_ob_rough` (`9d7d59a77`) and `ball_trail_shot_isolation` (`93f6348bb` + close-out `1a4e01031`) are both merged to `main`; `main == origin/main`.
> - **§3.4 location instruction CORRECTED** — the Rev 2 `CharacterDatabaseCSV` precedent was wrong and would not have wired up against §3.2. Use the `GachaBannerModel` static-catalog pattern. Details in §3.4.
> - **All four "Open items for Cesar" resolved** — `char_olivia`, full 7-club bag, Roster **fully locked**, display name `GOLFIN Demo`.
> - **§3.2 allowlist re-confirmed unchanged** at `{ Logo, Splash, Loading, Home }`.
> - Re-verified in source: `ScreenId` enum exists (`ScreenManager.cs:6`); `ShowScreen` is at `ScreenManager.cs:128` exactly as §3.2 asserts.

---

## 0. Decision summary

| Question | Answer |
|---|---|
| One Unity project or two? | **One.** Cesar's instinct was right. Build Profiles, not a second project, not a branch. |
| Separate App Store listing? | **No.** Demo builds ride the **existing** ASC record's TestFlight as a separate tester group + a direct signed APK on Android. No new bundle ID at all. See §1 and §6. |
| How is content removed? | Scene-list override + a **build-time scene stripper** + a deny-by-default screen gate. **Not** an assembly reorg. See §3. |
| Effort | **~5.5 dev-days**, zero file moves, merge-safe with the Mac dev's in-flight work. |
| Blockers found in repo | 3, all pre-existing, all cheap to fix. See §2. **One of them is an App Review liability today.** |

---

## 1. Distribution — the goal changes the answer

The demo is for **investors / publishers / Ken** (confirmed by Cesar 2026-07-30). That is a *controlled-audience* goal, and it is the one case where a public App Store listing is pure cost.

### Use TestFlight + direct APK

| Channel | Mechanism | Setup |
|---|---|---|
| iOS | TestFlight — up to 10,000 external testers, shareable public link, no email collection, revocable | ~1 day incl. first Beta App Review |
| Android | Signed **APK** on a download link, or Play internal testing track | ~2 hours |

TestFlight is *literally* what Apple's guideline 2.2 tells you to use, so there is no policy risk. It also exercises the entire real submission pipeline — signing, provisioning, App Store Connect, build upload, Beta App Review — which de-risks the full game's eventual submission **without** putting anything permanent on the store.

Ken/investors get a tap-to-install build on their own iPhone in under two minutes. That is the whole job.

### Why not the App Store, given this goal

Three findings from the adversarial pass, in order of weight:

1. **It aims the risk at the wrong submission.** Guideline 4.3(a) — *"Don't create multiple Bundle IDs of the same app"* — is evaluated against what's already live at review time. Ship the demo first and you manufacture the prior art the full GOLFIN gets judged against: same account, same engine fingerprint, same art, same golf. If the demo gets rejected you shrug. **If the full game gets 4.3'd you're in the appeal loop** — documented threads run 12+ months with Apple replying *"We are not able to provide feedback on app concepts or features"* and developers unable to learn which app they allegedly duplicated.
2. **Guideline 2.2 names this artifact.** *"Demos, betas, and trial versions of your app don't belong on the App Store – use TestFlight instead."* Renaming it changes what it's *called*, not what it *is*. In practice reviewers cite 4.2 ("lasting entertainment value" — one hole, no progression) rather than 2.2, but the outcome is the same.
3. **The positioning dodge is self-defeating.** To survive review the demo must look like an unrelated standalone product; to be useful it must be publicly connected to GOLFIN. The moment marketing says "try the demo," you've supplied the 4.3(a) evidence yourself. Compliance and usefulness are inversely coupled — there's no setting where both work.

Plus: one-star reviews on a deliberately thin app become a permanent public artifact under the developer name, and ratings never transfer to the full game.

### The one test that flips this

> **If GOLFIN were cancelled tomorrow, would you still ship and maintain this app?**

**Yes** → it's a product, not a demo. Give it its own identity and retention loop, and a separate listing is legitimate.
**No** → it's a demo. TestFlight.

One hole, one character, fixed bag, everything else locked answers *no*.

**See §6 for the distribution mechanics — there's a route that needs no new bundle ID and no new store record at all.**

**None of this changes the build work.** Everything in §2–§4 is required either way.

---

## 2. Three landmines in the repo — fix before anything else

### 2.1 🔴 `UNITY_MCP_READY` is compiled into player builds (~15 min) — Notion Order 428

`ProjectSettings.asset` lines 836–854 define `UNITY_MCP_READY` for **all 19 platforms, including iPhone and Android**. That satisfies the define constraint on `com.IvanMurzak.Unity.MCP.Runtime` — a **Runtime** asmdef (`includePlatforms: []`, `autoReferenced: true`) whose precompiled references include `Microsoft.AspNetCore.SignalR.Client.dll`, `Microsoft.AspNetCore.SignalR.Client.Core.dll` and `System.Text.Json.dll`.

**An AI remote-control plugin with a live SignalR network client is being built into your store binaries right now.** That is the textbook shape of guideline 2.3.1 — *"Don't include any hidden, dormant, or undocumented features in your app"* — plus undeclared network behaviour against your privacy manifest, plus IL2CPP bloat.

**Fix, preserving the MCP workflow** (build-profile defines are *additive*, so this composes cleanly):

1. Remove `UNITY_MCP_READY` from the `iPhone` and `Android` entries in `ProjectSettings.asset` (leave Editor/Standalone if you want).
2. Create a **`Dev-iOS`** build profile that adds `UNITY_MCP_READY` in *Build Data → Scripting Defines*. Use it as the day-to-day active profile — the Editor compiles with the active profile's defines, so MCP keeps working. **(Rev 4: was `Dev-Android`; iPhone is now the primary target — see §3.1.)** Create `Dev-Android` alongside it for later.
3. The Full and Demo release profiles omit it. The assembly and its DLLs never reach a store build.

Trade-off: while a release profile is active, MCP tools go quiet in the Editor. Fine, and now explicit.

**Do this before `testflight_distribution` (424) — it is upload hygiene, not demo work.**

### 2.2 🔴 No YAML merge driver, two devs, one 4.1 MB scene (~30 min, both machines) — Notion Order 429

`.gitattributes` marks `*.unity` / `*.prefab` as `text eol=lf` with the comment *"use smart merge driver when available"* — but **no `merge=unityyamlmerge` attribute exists and no driver is configured**. Git will line-merge `ShellScene.unity` (4.1 MB, ~1200 GameObjects) and produce plausible-looking garbage.

This is a live data-loss hazard **today**, independent of the demo, because the Mac dev is working in parallel.

Either wire up UnityYAMLMerge on both machines:
```
# .gitattributes
*.unity   merge=unityyamlmerge eol=lf
*.prefab  merge=unityyamlmerge eol=lf
*.asset   merge=unityyamlmerge eol=lf
```
```ini
# .git/config — NOT tracked, so this must be done on EVERY machine that merges.
# macOS path VERIFIED 2026-07-30 by executing the binary (reports v1.0.1):
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Helpers/UnityYAMLMerge' merge -p --force --fallback none %O %A %B %P
```

> 🔴 **The Rev 2 path was Windows-only (`<UnityInstall>/Editor/Data/Tools/UnityYAMLMerge.exe`) and this is now the build machine — corrected.**
> ⚠️ **Trap, verified:** on macOS there are **two** things named `UnityYAMLMerge` inside `Unity.app/Contents/`, and the obvious one is wrong.
> - `Contents/Resources/UnityYAMLMerge/` — a **directory** holding `mergespecfile.txt`, `mergerules.txt`, `mergeresolving.txt`. Pointing the driver here fails silently-ish at merge time.
> - `Contents/Helpers/UnityYAMLMerge` — the **actual Mach-O arm64 executable**. This is the one.
>
> For the PC, the Windows path stays `<UnityInstall>/Editor/Data/Tools/UnityYAMLMerge.exe`.

…or, as a 60-second stopgap, mark them `-merge` so git refuses to auto-merge and forces a manual pick. Refusing is better than silently corrupting.

**Version check — Mac side already satisfied.** `/Applications/Unity/Hub/Editor/` contains exactly one install, **6000.3.9f1**, matching the project. Nothing to reconcile here; the drift risk is PC-side only.

**Current state verified 2026-07-30:** `.gitattributes` does mark `*.unity` / `*.prefab` / `*.mat` / `*.anim` / `*.controller` etc. as `text eol=lf` under the comment *"use smart merge driver when available"*, and `git config --get-regexp 'merge\.'` returns **nothing**. The diagnosis holds exactly as written.

### 2.3 🟡 There is no "Home scene" — the shell is one scene (architectural, shapes the whole plan)

Every UI screen — Logo, Splash, Loading, Home, Roster, Inventory, HoleSelection, ModeSelection, Leaderboard, 3× Tournament, 2× StaminaShop, GeneralShop, GachaHistory, GachaPrizes, Login/SignUp/CreateUsername/EmailConfirmation — is a **child GameObject inside `Assets/Scenes/ShellScene.unity`**, wired as `[SerializeField] GameObject` fields on `ScreenManager` (`Assets/Scripts/UI/ScreenManager.cs:54–81`).

Consequence: **a scene-list override cannot exclude a single screen.** It excludes hole geometry and lab scenes only — real, and the biggest size win available, but zero UI removal. This is why §3 uses a build-time scene stripper instead of assembly surgery.

Current build list (`EditorBuildSettings.asset`): `ShellScene` + `Physics/LabScaffold` + `Hole_01…18_Geo` + `Physics/PhysicsLab_TestGreen` = 21 scenes.

---

## 3. Build architecture

One project. One repo. Four build profiles plus a dev profile. Content removed at **build time**, never by mutating source.

### 3.1 Build profiles

`Assets/Settings/Build Profiles/` — profile assets are VCS-friendly and, as a bonus, **stop `EditorBuildSettings.asset` churning** every time the Mac dev adds a hole, removing a live conflict surface.

**Platform priority (Cesar, 2026-07-30): iPhone first, Android second. This Mac is the build machine.** `iOS-Demo` is the profile that has to work; Android follows.

| Profile | Scene list override | Scripting defines | Player Settings override |
|---|---|---|---|
| `Dev-iOS` ⭐ **daily driver** | off (global) | `UNITY_MCP_READY` | none |
| `iOS-Full` | off (global) | — | none |
| `iOS-Demo` | **on**: `ShellScene` + `Hole_01_Geo` | `GOLFIN_DEMO` | product name, icons **only** — **keep the Full bundle ID**, see §6 |
| `Android-Full` | off (global) | — | none |
| `Android-Demo` | **on**: `ShellScene` + `Hole_01_Geo` | `GOLFIN_DEMO` | bundle ID, product name, icons **only** |
| `Dev-Android` | off (global) | `UNITY_MCP_READY` | none | *(create now — it's a 30-second `.asset` — but it isn't the daily driver)* |

> 🔴 **Rev 2 named `Dev-Android` as the day-to-day active profile. With iPhone first that is now the wrong default and would actively cause the §2.1 failure mode.** The Editor compiles using the **active profile's** defines. In an iPhone-first shop you will spend the day switching between the dev profile and `iOS-Full` / `iOS-Demo` for real builds — so the dev profile must be the **iOS** one, or every switch back lands you on a profile whose platform doesn't match what you're building and MCP silently drops out for the wrong reason. **`Dev-iOS` is the daily driver.** Create `Dev-Android` in the same pass so it exists when Android work starts.

Drops 17 hole scenes + 2 physics-lab scenes. Terrain and course geometry are the bulk of the binary — this is the single biggest win in the plan.

> ⚠️ **A Player Settings override is a full clone of the PlayerSettings object, not a per-field diff.** Every global change made afterwards silently fails to reach overriding profiles. The recent iOS work (portrait lock, SafeAreaFitter, iOS quality tier) is exactly the kind of thing that would drift.
> **Rule: override the minimum — product name and icons, plus the Android bundle ID. Leave orientation, SDK levels, quality, stripping, and version/build numbers on the global settings.** Add a checklist line to `Tasks.md`.

> ⚠️ `-activeBuildProfile` has a reported Unity 6 bug where it exits batchmode if the profile is already active, and requires a **project-relative** path. Set the active profile inside the build method via the `BuildProfile` API instead of relying on the CLI flag.

**Bundle IDs — do NOT change the iOS one.** Per §6, the demo rides the existing ASC record's TestFlight, so `iOS-Demo` keeps `com.nextinnovation.golfingame` (same as `iOS-Full`) and overrides only product name and icons. Android-Demo can take its own ID since it's a sideloaded APK with no store record — if you do, use a hyphen rather than a dot suffix (Apple DTS guidance) and **no underscores**, which script-set Android IDs have been reported to strip.

### 3.2 `DemoGate` — deny-by-default screen allowlist

New file, `Assets/Scripts/Demo/DemoGate.cs`, in Assembly-CSharp. No asmdef, no file moves.

```csharp
namespace GolfinRedux.Demo
{
    public static class DemoGate
    {
#if GOLFIN_DEMO
        public const bool IsDemo = true;
#else
        public const bool IsDemo = false;
#endif
        // ALLOWLIST — deny by default. A new screen is locked until listed here.
        static readonly HashSet<ScreenId> Allowed = new()
        {
            ScreenId.Logo, ScreenId.Splash, ScreenId.Loading, ScreenId.Home
        };

        public static bool IsScreenAllowed(ScreenId id) => !IsDemo || Allowed.Contains(id);
    }
}
```

**Allowlist, never a denylist.** With a denylist every screen added after today ships in the demo by default. With an allowlist the failure mode is loud (a screen won't open) instead of silent.

Gate at the single choke point — `ScreenManager.ShowScreen()` (`ScreenManager.cs:128`):

```csharp
public void ShowScreen(ScreenId screenId, bool instant = false)
{
    if (!DemoGate.IsScreenAllowed(screenId))
    {
        Debug.Log($"[DemoGate] blocked {screenId}");
        return;
    }
    // …existing body unchanged
```

~30 lines total across two files. Zero conflict with physics/course work.

### 3.3 Build-time scene stripper — the piece that makes it honest

`Assets/Editor/DemoSceneProcessor.cs`, implementing `IProcessSceneWithReport`. Runs on the **in-memory copy** of the scene during the build; the file on disk is never touched.

```
if GOLFIN_DEMO && scene.name == "ShellScene":
    find ScreenManager
    for each screen-container field whose ScreenId is not in DemoGate.Allowed:
        Object.DestroyImmediate(container)
```

This works cleanly because `ScreenManager.ApplyScreen()` already null-guards every container (`if (_rosterScreen != null) …`). Destroyed screens become no-ops that log a warning. **No code change needed to survive stripping.**

What this buys, and why it beats the assembly approach:

- Full-game screen GameObjects **and their sprite/prefab references** are genuinely absent from the demo build → real size reduction, unlike an asmdef split which removes IL but leaves the art.
- `ShellScene.unity` on disk stays intact → the Editor never shows missing scripts, and there is no risk of committing a mutilated 4.1 MB scene at 1am.
- No file moves, no `.meta` churn, no merge exposure.

### 3.4 `DemoConfig` — soft gating

**Location — corrected 2026-07-30 (Architect, verified in source).** Place at **`Assets/Resources/Data/demo_config.csv`**, loaded via `Resources.Load<TextAsset>("Data/demo_config")`.

> ⚠️ **Rev 2 said "matching `CharacterDatabaseCSV` conventions." That instruction was wrong — do not follow it.** `CharacterDatabaseCSV.cs:45` and `ClubDatabaseCSV.cs:45` both take an **Inspector-assigned `TextAsset`** (*"drag Clubs.csv into Inspector"*) — a serialized field on a MonoBehaviour living in `ShellScene`. `DemoGate` (§3.2) is a `static class` with no Inspector, no MonoBehaviour and no scene presence, so it **cannot** consume one. The correct precedent is the static-catalog pattern at **`GachaBannerModel.cs:90`** — `Resources.Load<TextAsset>("Data/gacha_banners")`. Lowercase-snake filename also matches every existing file in `Resources/Data/` (`bot_clubs`, `gacha_banners`, `shop_catalog`, `stamina_shop_items`).
> Side benefit: §5 already notes `Assets/Resources` ships regardless of scene list, defines or assemblies — so the config is guaranteed present in the demo binary with no build-profile coupling.

**Locked values (Cesar, 2026-07-30):**

| Field | Value | Note |
|---|---|---|
| `playable_holes` | `hole_01` | Only hole in the demo scene list (§3.1) |
| `character_id` | **`char_olivia`** | Olivia Guarinoni — Uncommon, `startLevel 40` / `maxLevel 79`, `BigRosterOlivia`. Mid-tier deliberately: shows visible progression headroom rather than a maxed ceiling. |
| `club_ids` | all 7 | See note below |
| `repair_kits_enabled` | `false` | |
| `balls_enabled` | `false` | |
| `points_enabled` | `false` | |

**On the club bag — this was not really a choice.** `Assets/Resources/Data/Clubs.csv` contains exactly **7 clubs, one per type**, spanning all six rarities: `club_driver_gf` (Driver, Common), `club_wood_gf` (Wood, Common), `club_iron9_klyro` (Iron, Uncommon), `club_iron7_mireo` (Iron, Rare), `club_awedge_fyloe` (A.Wedge, Mythic), `club_pwedge_royal` (P.Wedge, Legendary), `club_putter_golfinx` (Putter, Supreme). There is no second driver or second putter to pick between, so a playable bag is forced to the full set. Ship all 7 at default level.

Ships as **data inside the demo binary**, not remote config — so it can't be flipped by a user or by a server, which is what keeps it clear of the 2.3.1 concern that runtime feature flags raise.

Also needed: trim Home-screen buttons that point at blocked screens (hide, don't just disable — a dead-end locked button reads as an unfinished build under guideline 2.1), and bypass the Login/SignUp gate entirely since the demo is fully offline with no Supabase calls.

### 3.5 Verification — a script you'll actually run

Skip CI. There is none today, and four profiles per push means multiple platform switches reimporting 1404 prefabs / 101 scenes / 333 `Resources` files. It would not get built, and every drift protection hanging off it would silently never run.

Instead: **`Tools/build-demo.sh`** — batchmode build dumping `BuildReport` summary + the top 50 packed assets by size + total size to a text file. ~2 hours to write, run at milestones. Eyeball the list for anything that shouldn't be there.

> 🔴 **Rev 2 specced `build-demo.ps1` (PowerShell) and an Android-only build. Both corrected in Rev 4** — this Mac is the build machine and iPhone is the primary target.

**Shape:** one script, `--platform ios|android`, **defaulting to `ios`**.

- **`ios`** — the shipping target, so this is what milestone checks run against. Note batchmode produces an **Xcode project**, not an `.ipa`; `BuildReport` is still fully populated at that point, which is all §3.5 needs. The `.ipa` comes from Xcode archive afterwards, as part of `testflight_distribution` (424).
- **`android`** — keep it, because it is the *faster* loop: batchmode emits an APK directly with no Xcode step. For the actual question this script answers — *"is anything in this build that shouldn't be?"* — the packed-asset list is near-identical across platforms, so use `android` for quick iterative size checks and `ios` at milestones.

Mac-specific: `bash`, not PowerShell; Unity at `/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity`; remember `chmod +x`.

### 3.6 Testing the demo **in the Editor**, before any build — NEW in Rev 5

Most of the demo is testable in play mode. Do this first; a build is the slow confirmation, not the first look.

**Set the active build profile to `iOS-Demo`, press Play.** That's the whole setup — the Editor compiles using the *active profile's* scripting defines, so `GOLFIN_DEMO` is live in play mode.

| Mechanism | Testable in Editor? | How |
|---|---|---|
| `GOLFIN_DEMO` define → `DemoGate.IsDemo` | ✅ **Fully** | Active profile `iOS-Demo` → define is set → `ShowScreen` gate is live. |
| `DemoGate` allowlist blocking screens | ✅ **Fully** | Tap through Home. Blocked screens no-op and log `[DemoGate] blocked <id>`. |
| `DemoSceneProcessor` stripper | ✅ **Yes** — see the null-report rule below | Runs on play-mode scene reload. Screen containers are physically destroyed in the play-mode copy. |
| `demo_config.csv` | ✅ **Fully** | `Resources.Load` behaves identically in Editor and player. |
| Home-screen button trim | ✅ **Fully** | Visual, in play mode. |
| Scene-list override | ⚠️ **Partly** | The Editor can always open any scene from the Project window, so "the other 17 holes are gone" is **not** enforced in-editor. Verify via `BuildReport` (§3.5), not by trying to open `Hole_07_Geo`. |
| Actual binary size | ❌ **No** | The whole point of the scene-list drop. §3.5 only. |
| `UNITY_MCP_READY` stripped from the player | ❌ **No** | The Editor always has *some* profile active. Confirm via the packed-assembly list in `BuildReport`. |
| IL2CPP behaviour, iOS safe area, portrait lock | ❌ **No** | Device build — `phone_build_smoke_test` (420) territory. |

#### 🔴 Mandatory: `report` is **null** in play mode

`IProcessSceneWithReport.OnProcessScene(Scene scene, BuildReport report)` is invoked during Player and AssetBundle builds **and also as a scene is reloaded while entering Editor play mode** — and in the play-mode case **`report` is `null`**. (Same for Addressables builds.)

**Any implementation that dereferences `report` — e.g. `report.summary.platform` to check the build target — throws a `NullReferenceException` the moment Cesar presses Play.** Unity's documented discriminator is `BuildPipeline.isBuildingPlayer`; use that, and treat a null `report` as the play-mode path rather than as an error.

```csharp
public void OnProcessScene(Scene scene, BuildReport report)
{
    // report == null  =>  entering play mode (or an Addressables build). NOT an error.
    // Do NOT touch report.* without a null check.
    if (!DemoGate.IsDemo) return;
    if (scene.name != "ShellScene") return;
    // …destroy non-allowlisted containers
}
```

#### ⚠️ One fidelity caveat — `Awake` ordering differs between play mode and build

Known Unity behaviour: `OnProcessScene` always runs after the scene loads, but **in a build the call to `Awake` is blocked, while in play mode it is not.** So in the Editor a screen container's `Awake` can run *before* the stripper destroys it, whereas in the real build it never runs at all.

Practical consequence: if any stripped screen registers a singleton, subscribes to a manager event, or writes state in `Awake`, the Editor will show side effects the shipped demo won't have (and vice-versa — an editor pass doesn't prove the build is clean). **Screen-gating behaviour is safe to judge in play mode; anything depending on `Awake` side effects must be confirmed on a real build.**

#### Suggested loop

1. Active profile → `iOS-Demo`. Press Play.
2. Logo → Splash → Loading → Home should all work; every other screen should no-op with a `[DemoGate] blocked` log and no visible dead-end button.
3. Play Hole 1 end-to-end with Olivia and the 7-club bag.
4. Switch active profile → `Dev-iOS`, press Play again — **everything should come back**. If it doesn't, the gate is leaking outside `GOLFIN_DEMO` and that is a bug.
5. Only then run `Tools/build-demo.sh` (§3.5) for the size/packed-asset confirmation.

Step 4 is the one people skip. It is the cheapest possible check that the demo gating is genuinely compile-time and hasn't quietly become permanent.

---

## 4. Work order — minimizing merge pain with the Mac dev

Their work (surface classification, OB, `HoleGeoImporter`, trails) lives in `Golfin.Physics.*` / `Golfin.Course.Runtime`, already behind asmdefs. Nothing below touches those. But their commits are landing **on `main`**, so ordering matters.

**Roadmap dependency:** this sits behind `phone_build_smoke_test` (Order 420) and shares every iOS upload gate with `testflight_distribution` (Order 424) — empty `m_BuildTargetPlatformIcons` for iPhone, empty build number, unset export-compliance flag, and `bundleVersion` needing to exceed the live App Store version. Those get fixed once, in 424, and the demo inherits them. Don't re-solve them here.

| # | Step | Days | Notes |
|---|---|---|---|
| 0 | ~~Merge the Mac dev's OB + trails work to `main` first~~ | — | ✅ **DONE 2026-07-30.** `9d7d59a77` (OB) + `93f6348bb`/`1a4e01031` (trails) are on `main`; `main == origin/main`. Gate cleared. |
| 1 | YAML merge driver (§2.2) | 0.25 | **Mac path verified** — use `Contents/Helpers/UnityYAMLMerge`, not the same-named directory under `Contents/Resources/`. Version check already satisfied on the Mac (one install, 6000.3.9f1). PC still needs its own `.git/config` block. Do this before anything else touches a scene. |
| 2+3 | **Merged — `unity_mcp_define_strip` (428) + all build profiles (§2.1, §3.1)** | 1.25 | **Rev 4: these were separate steps and should not be.** 428's fix *is* "strip the define from the iPhone/Android entries in `ProjectSettings.asset`, then add it to a dev build profile" — and that dev profile (`Dev-iOS`) is already a deliverable of step 3. Doing them apart means creating the same `.asset` twice or half-creating it. One sitting: two lines in `ProjectSettings.asset` + six profile assets (`Dev-iOS`, `Dev-Android`, `iOS-Full`, `iOS-Demo`, `Android-Full`, `Android-Demo`). **Close Notion 428 when this lands.** |
| 4 | `demo_config.csv` + `DemoGate.cs` (§3.2, §3.4) | 1.5 | All new files. Fully concurrent-safe. **See §3.4 for the corrected location** — `Assets/Resources/Data/demo_config.csv`, `Resources.Load`, NOT an Inspector-assigned TextAsset. |
| 5 | `ScreenManager` gate + Home button trim (§3.2) | 1.0 | One existing file, ~5 lines. The only step with any merge surface. |
| 6 | `DemoSceneProcessor.cs` (§3.3) | 1.0 | One new Editor file. Additive. |
| 7 | `Tools/build-demo.sh` + manual QA pass + written checklist | 0.5 | **Rev 4: `.sh` not `.ps1`, `--platform ios\|android` defaulting to `ios`.** See §3.5. |
| | **Total** | **~5.5** | |

Steps 3, 4, 6 are pure additions — no existing file is renamed or moved, so the merge surface with the Mac dev is one file (step 5) in territory they aren't touching.

Also worth 10 minutes: there's a stale worktree at `C:/Users/cesar/Golfin Redux/.claude/worktrees/kind-haslett` — note the **space** in the path, versus the live repo at `C:/Users/cesar/GolfinRedux`. Two project paths one character apart will eat somebody's afternoon. Prune it.

---

## 5. Deliberately rejected, and why

### ❌ Assembly reorg (`Golfin.FullGame.asmdef` with `!GOLFIN_DEMO` constraint)

This was the "proper" plan. It does not survive contact with the codebase:

- **asmdefs cannot reference Assembly-CSharp.** `ScreenManager`, `ModalController`, `CharacterManager`, `RewardPointsManager`, `HoleData`/`HoleDatabase`, `ItemManager`, `ClubManager`, `BagManager`, `BallManager` are all in the default assembly. ~280–330 files (≈190 under `Assets/Scripts/UI`) would have to move first, transitively.
- **The event-driven singleton design closes the loop.** Managers raise `Action` events that UI subscribes to; UI calls `.Instance` back. That's a bidirectional graph, and a one-way assembly reference can't express it. You either hoist everything into `Golfin.Core` (the degenerate outcome — nothing is actually separated) or invert every manager→UI edge behind interfaces and a boot-time event bus. The second is a redesign of the whole shell.
- **The boundary runs through the wrong folder.** `Assets/Scripts/UI/Roster/` holds *both* the progression UI *and* `PlayerCharacterData`, `RarityStatCaps`, `CharacterDatabase`, `CharacterDatabaseCSV`, `RewardPointsManager` — and the demo needs the character it displays.
- **Constraint-excluded assemblies aren't compiled**, so with the demo profile active ~20 screen roots in `ShellScene` show as *Missing (Mono Script)*. One prefab-apply or one "Remove Missing Scripts" click writes a mutilated 4.1 MB scene to disk.
- **`Assets/Resources` (333 files) ignores the whole mechanism anyway** — every portrait, thumbnail and gacha banner ships regardless of scene list, defines, or assemblies.

**Cost: 15–25 dev-days with an unbounded bug tail, landing on top of live parallel work, to remove perhaps 1–3 MB of IL2CPP output that is noise next to terrain and art you weren't removing.** Revisit only if a publisher or App Review actually forces it — and then: freeze window, one branch, `git mv` with `.meta` in the *same* commit, one folder at a time starting with `Assets/Scripts/UI/Shop`, merged the same day. Never leave an asmdef branch open overnight.

### ❌ CI building all four profiles per push
See §3.5. Would not get built; every protection hanging off it would silently never run.

### ❌ `BuildReport` GUID assertion that fails the build
Implementable in ~half a day (`IPostprocessBuildWithReport` → `report.packedAssets[].contents[].sourceAssetGUID` → denylist → `BuildFailedException`), but it fails on day one because of `Resources`, the denylist rots by hand, and the top-50 asset dump in §3.5 gives the same signal for free.

### ❌ Runtime/remote feature flags as the gating mechanism
A server-flippable switch over full-game content is precisely what 2.3.1 was written to catch. Compile-time define + build-time stripping + data shipped inside the binary is both safer and smaller.

### What the cheap plan honestly gives up

Full-game **code** (Shop, Gacha, Roster, Tournaments) still exists in the demo binary as unreferenced IL — unreachable, never constructed, no code path instantiates it, screens physically stripped from the scene. That satisfies 2.3.1 in practice: dormant *reachable* features get rejected; unreferenced library code does not, or every Unity app would fail review. Someone could decompile the demo and find `GachaMockPrizePool`. Nobody will, and there's no moat on an unreleased game.

---

## 6. Distribution mechanics — 🔴 corrected 2026-07-30

**Reading `AI_CONTEXT.md` closed the open question here, and it changes the answer.**

The iOS bundle ID is already deliberately set to **`com.nextinnovation.golfingame`** — the **live App Store Golfin's** ID — so that Redux ships as an *update to the existing App Store Connect record* (app owned by NEXT INNOVATION PTE. LTD., Cesar is Admin). That was decided under `phone_build_smoke_test` (Order 420).

**So the existing record is already reserved for the full game.** The "ship the demo into the old record" idea from Rev 1 is **dead**: it would spend the full game's listing on a one-hole build, and its public reviews and rating with it.

### ✅ Recommended: demo builds ride the existing record's TestFlight

TestFlight builds live *under* an app record but are never publicly listed and never face App Store review — only the much lighter Beta App Review. Since the full game isn't released yet, that record's TestFlight is free to carry whatever build you want.

- Create an **external tester group** ("Investors") on the existing record. Demo builds → that group. Full builds → internal testers.
- Multiple builds coexist in TestFlight, and groups are assigned per build, so the two never collide.
- **Zero new bundle IDs. Zero new App Store Connect records. Zero App Review exposure. Nothing public.**
- Ken/investors get a tap-to-install build on their own iPhone in under two minutes.

**The one trade-off:** demo and full builds share a bundle ID, so they can't be installed side by side on the same device. For stakeholder demos that's a non-issue.

Version/build numbers are shared across the record too, so demo uploads consume build numbers from the same sequence — bump monotonically and don't reuse. The existing gate still applies: `bundleVersion` must exceed the live App Store Golfin's version or ASC rejects the upload (`0.1.0` will be rejected).

**Android:** signed APK on a download link. No Play listing, no repetitive-content exposure, no 12-tester closed-testing cycle.

### If you later want a genuinely public demo listing

That needs a **new, separate bundle ID** — a second ASC record, since the original is committed to the full game. Everything in §1 applies: distinct name and icon with no public relationship to GOLFIN, no IAP, no account, and a specific Notes-for-Review explanation on every submission. You'd be accepting a real conditional risk of a 4.3 tangle on GOLFIN's own submission, on an appeal path with no useful feedback. Not recommended, and not needed for the stated goal.

**Either way the build work in §2–§4 is identical.** Ship to TestFlight first, put it in Ken's hands, then decide.

---

## Open items for Cesar

**All four resolved 2026-07-30. Do not re-litigate — implement as stated.**

1. ~~Which character and which club set?~~ **RESOLVED — `char_olivia` (Olivia Guarinoni), full 7-club bag.** See §3.4 for the locked table and why the bag was forced.
2. ~~Roster read-only vs locked?~~ **RESOLVED — stays FULLY LOCKED**, as originally specced. The §3.2 allowlist is therefore **unchanged**: `{ Logo, Splash, Loading, Home }`. Roster is *not* added. The Rev 2 note arguing for a read-only Roster was considered and declined — treat it as closed, not as a standing suggestion.
3. ~~Demo display name?~~ **RESOLVED — `GOLFIN Demo`.** Product-name override on the `iOS-Demo` / `Android-Demo` profiles only (§3.1). Nothing public; the bundle ID does **not** change (§6).
4. ~~Does the old ASC record still exist?~~ **Resolved** — it does, it's in use, and it's reserved for the full game. See §6.

### Still genuinely open — RESOLVED 2026-07-30

**~~Step 7's script format.~~ RESOLVED.** iPhone first, Android second, **this Mac is the build machine**. Step 7 is **`Tools/build-demo.sh`**, `--platform ios|android`, default `ios`. See §3.5. Knock-on corrections made in the same pass: §2.2's merge-driver path was Windows-only and is now the verified macOS binary; §3.1's daily-driver profile was `Dev-Android` and is now **`Dev-iOS`**; §4 steps 2 and 3 are **merged**, since `unity_mcp_define_strip` (428) and the profile creation deliver the same asset.

**Nothing is open. The spec is ready to implement.**

## Sources

- [Apple App Review Guidelines (2.1, 2.2, 2.3.1, 4.2, 4.3)](https://developer.apple.com/app-store/review/guidelines/)
- [Apple Forums 771167 — 4.3(a) rejection loop, 15 months unresolved](https://developer.apple.com/forums/thread/771167) · [819568 — unidentifiable "duplicate"](https://developer.apple.com/forums/thread/819568) · [698944 — DTS on shipping a lite version alongside](https://developer.apple.com/forums/thread/698944) · [117279 — Lite/Pro pair rejected under 4.3](https://developer.apple.com/forums/thread/117279)
- [TestFlight — 10,000 external testers, public links](https://developer.apple.com/testflight/)
- [Unity — Build Profiles window reference (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles-reference.html) · [Build profile scene list](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profile-scene-list.html) · [Custom scripting symbols (additive scopes)](https://docs.unity3d.com/6000.1/Documentation/Manual/custom-scripting-symbols.html) · [Assembly Definition properties (define constraints)](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html) · [Editor command line arguments](https://docs.unity3d.com/6000.2/Documentation/Manual/EditorCommandLineArguments.html)
- [Unity Discussions — `-activeBuildProfile` batchmode bug](https://discussions.unity.com/t/command-line-argument-to-build-using-a-build-profile-unity-6/951755) · [What you need to know about Build Profiles](https://discussions.unity.com/t/what-you-need-to-know-about-build-profiles-in-unity-6/1605803)
- [Google Play Spam / Repetitive Content policy](https://support.google.com/googleplay/android-developer/answer/9899034) · [Google Play Instant discontinued (Dec 2025)](https://developer.android.com/topic/google-play-instant)
