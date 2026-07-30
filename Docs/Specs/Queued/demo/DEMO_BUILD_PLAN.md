# GOLFIN Redux — Demo Build Plan

**Status:** SPEC_READY — all Cesar decisions locked, step 0 gate cleared · **Author:** Claude (Architect) · **Rev 3, 2026-07-30**
**Verified against:** `C:\Users\cesar\GolfinRedux` @ Unity **6000.3.9f1** (Rev 2) · re-verified on the Mac `/Users/cesar/Documents/GolfinRedux` @ **6000.3.9f1** (Rev 3)
**Implementer:** Claude Code
**Notion:** `demo_build_slice` (Order 426) · prereqs `unity_mcp_define_strip` (428), `unity_yaml_merge_driver` (429)

> **Rev 3 changelog (2026-07-30):**
> - **Step 0 gate CLEARED.** `surface_classification_ob_rough` (`9d7d59a77`) and `ball_trail_shot_isolation` (`93f6348bb` + close-out `1a4e01031`) are both merged to `main`; `main == origin/main`. Structural work may begin.
> - **§3.4 location instruction CORRECTED** — the Rev 2 `CharacterDatabaseCSV` precedent was wrong and would not have wired up against §3.2. Use the `GachaBannerModel` static-catalog pattern. Details in §3.4.
> - **All four "Open items for Cesar" resolved** — `char_olivia`, full 7-club bag, Roster **fully locked**, display name `GOLFIN Demo`.
> - **§3.2 allowlist re-confirmed unchanged** at `{ Logo, Splash, Loading, Home }`.
> - Re-verified in source: `ScreenId` enum exists (`ScreenManager.cs:6`); `ShowScreen` is at `ScreenManager.cs:128` exactly as §3.2 asserts.
> - One new open item (non-blocking): PowerShell vs shell for step 7 — see end of doc.

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
2. Create a **`Dev-Android`** build profile that adds `UNITY_MCP_READY` in *Build Data → Scripting Defines*. Use it as the day-to-day active profile — the Editor compiles with the active profile's defines, so MCP keeps working.
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
# .git/config on BOTH machines (.git/config is not tracked)
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<UnityInstall>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p --force --fallback none %O %A %B %P
```
…or, as a 60-second stopgap, mark them `-merge` so git refuses to auto-merge and forces a manual pick. Refusing is better than silently corrupting.

Also confirm the Mac is on **exactly 6000.3.9f1** — minor drift causes divergent reimports and asset-format churn.

### 2.3 🟡 There is no "Home scene" — the shell is one scene (architectural, shapes the whole plan)

Every UI screen — Logo, Splash, Loading, Home, Roster, Inventory, HoleSelection, ModeSelection, Leaderboard, 3× Tournament, 2× StaminaShop, GeneralShop, GachaHistory, GachaPrizes, Login/SignUp/CreateUsername/EmailConfirmation — is a **child GameObject inside `Assets/Scenes/ShellScene.unity`**, wired as `[SerializeField] GameObject` fields on `ScreenManager` (`Assets/Scripts/UI/ScreenManager.cs:54–81`).

Consequence: **a scene-list override cannot exclude a single screen.** It excludes hole geometry and lab scenes only — real, and the biggest size win available, but zero UI removal. This is why §3 uses a build-time scene stripper instead of assembly surgery.

Current build list (`EditorBuildSettings.asset`): `ShellScene` + `Physics/LabScaffold` + `Hole_01…18_Geo` + `Physics/PhysicsLab_TestGreen` = 21 scenes.

---

## 3. Build architecture

One project. One repo. Four build profiles plus a dev profile. Content removed at **build time**, never by mutating source.

### 3.1 Build profiles

`Assets/Settings/Build Profiles/` — profile assets are VCS-friendly and, as a bonus, **stop `EditorBuildSettings.asset` churning** every time the Mac dev adds a hole, removing a live conflict surface.

| Profile | Scene list override | Scripting defines | Player Settings override |
|---|---|---|---|
| `Dev-Android` | off (global) | `UNITY_MCP_READY` | none |
| `Android-Full` | off (global) | — | none |
| `iOS-Full` | off (global) | — | none |
| `Android-Demo` | **on**: `ShellScene` + `Hole_01_Geo` | `GOLFIN_DEMO` | bundle ID, product name, icons **only** |
| `iOS-Demo` | **on**: `ShellScene` + `Hole_01_Geo` | `GOLFIN_DEMO` | product name, icons **only** — **keep the Full bundle ID**, see §6 |

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

Skip CI. There is none today, iOS builds need macOS, and four profiles per push means multiple platform switches reimporting 1404 prefabs / 101 scenes / 333 `Resources` files. It would not get built, and every drift protection hanging off it would silently never run.

Instead: `Tools/build-demo.ps1` — batchmode Android-Demo build, dumping `BuildReport` summary + the top 50 packed assets by size + total size to a text file. ~2 hours to write, run at milestones. Eyeball the list for anything that shouldn't be there.

---

## 4. Work order — minimizing merge pain with the Mac dev

Their work (surface classification, OB, `HoleGeoImporter`, trails) lives in `Golfin.Physics.*` / `Golfin.Course.Runtime`, already behind asmdefs. Nothing below touches those. But their commits are landing **on `main`**, so ordering matters.

**Roadmap dependency:** this sits behind `phone_build_smoke_test` (Order 420) and shares every iOS upload gate with `testflight_distribution` (Order 424) — empty `m_BuildTargetPlatformIcons` for iPhone, empty build number, unset export-compliance flag, and `bundleVersion` needing to exceed the live App Store version. Those get fixed once, in 424, and the demo inherits them. Don't re-solve them here.

| # | Step | Days | Notes |
|---|---|---|---|
| 0 | **Merge the Mac dev's OB + trails work to `main` first** | — | Non-negotiable. No structural work over unmerged parallel work. |
| 1 | YAML merge driver on both machines (§2.2) + confirm 6000.3.9f1 | 0.25 | Do this before anything else touches a scene. |
| 2 | Strip `UNITY_MCP_READY` from iPhone/Android + `Dev-Android` profile (§2.1) | 0.25 | `ProjectSettings.asset`, two lines. Coordinate the 1-minute merge. |
| 3 | Five build profiles: IDs, names, icons, defines, scene lists (§3.1) | 1.0 | All new `.asset` files. Additive. |
| 4 | `DemoConfig.csv` + `DemoGate.cs` (§3.2, §3.4) | 1.5 | All new files. Fully concurrent-safe. |
| 5 | `ScreenManager` gate + Home button trim (§3.2) | 1.0 | One existing file, ~5 lines. |
| 6 | `DemoSceneProcessor.cs` (§3.3) | 1.0 | One new Editor file. Additive. |
| 7 | `Tools/build-demo.ps1` + manual QA pass + written checklist | 0.5 | |
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

### Still genuinely open — one item, raised 2026-07-30

**Step 7's `Tools/build-demo.ps1` is PowerShell, but the repo is now Mac-primary.** §3.5 scopes that script to an **Android** batchmode build, so PowerShell on the PC is not *wrong* — but iOS builds require macOS, and the day-to-day machine is the Mac (`/Users/cesar/Documents/GolfinRedux`, verified on **6000.3.9f1**). Options: (a) keep `.ps1`, Android-only, PC-only; (b) write `build-demo.sh` instead and run everything on the Mac; (c) both. **Cesar's call — does not block steps 3, 4 or 6.**

## Sources

- [Apple App Review Guidelines (2.1, 2.2, 2.3.1, 4.2, 4.3)](https://developer.apple.com/app-store/review/guidelines/)
- [Apple Forums 771167 — 4.3(a) rejection loop, 15 months unresolved](https://developer.apple.com/forums/thread/771167) · [819568 — unidentifiable "duplicate"](https://developer.apple.com/forums/thread/819568) · [698944 — DTS on shipping a lite version alongside](https://developer.apple.com/forums/thread/698944) · [117279 — Lite/Pro pair rejected under 4.3](https://developer.apple.com/forums/thread/117279)
- [TestFlight — 10,000 external testers, public links](https://developer.apple.com/testflight/)
- [Unity — Build Profiles window reference (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles-reference.html) · [Build profile scene list](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profile-scene-list.html) · [Custom scripting symbols (additive scopes)](https://docs.unity3d.com/6000.1/Documentation/Manual/custom-scripting-symbols.html) · [Assembly Definition properties (define constraints)](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html) · [Editor command line arguments](https://docs.unity3d.com/6000.2/Documentation/Manual/EditorCommandLineArguments.html)
- [Unity Discussions — `-activeBuildProfile` batchmode bug](https://discussions.unity.com/t/command-line-argument-to-build-using-a-build-profile-unity-6/951755) · [What you need to know about Build Profiles](https://discussions.unity.com/t/what-you-need-to-know-about-build-profiles-in-unity-6/1605803)
- [Google Play Spam / Repetitive Content policy](https://support.google.com/googleplay/android-developer/answer/9899034) · [Google Play Instant discontinued (Dec 2025)](https://developer.android.com/topic/google-play-instant)
