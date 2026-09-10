# 3D Character Remake — Options & Costs

Date: 2026-09-03 · Author: Claude (Architect) · Status: DECISION NEEDED on the model source (§3/§4) — **the animation pipeline is DECIDED, see §7 (2026-09-07)**
Budget frame (Cesar): < $1k for tools/assets; 3D artist budgeted separately.

## 1. What the repo actually contains (GolfinRedux, checked 2026-09-03)

- **No character 3D assets ship today.** `Assets/Prefabs/Original/Characters/` has 5 prefabs
  (`PfYoungMale`, `PfYoungLady`, `PfOldMan`, `PfOldLady`, `PfFemale_Variant`, `PfMaleGolfer Variant`)
  but each is a *variant* of a source prefab (guid `9edef746…`) that does not exist anywhere in
  `Assets/`. No `.fbx` character, no `.anim`, no `.controller` outside vendor packs. `GameplayScene`
  has no golfer object. The only `Animator`/`SkinnedMeshRenderer` in the project is `IntroSystem.prefab`.
  ⇒ The "hideous" models and the animation pack live in the original Golfin project (Plastic /
  Addressables S3 bundles), not in Redux. Whatever we pick, the animation pack must be re-imported.
- **What the prefabs tell us about the runtime contract:** child transforms `ClubSlot`, `PutterSlot`,
  `GameplayIdleClubSlot`, `GameplayIdlePuttClubSlot`, `ClubStart`, `ClubEnd`, `UnplayableChecker`
  (two, with colliders + rigidbodies + script `41412eff…`). New models must expose the same sockets.
- **Roster:** 12 characters in `Assets/Data/Characters.csv` (James, Olivia, Richard, Elizabeth, Shae,
  Camila, Guillermo, Ean, Freda, Johan, Mike, Roshana). Starters = James + Olivia.
- **2D reference:** `Resources/Portraits/FullBody/BigRoster<Name>.png` (537×1483), one 3/4-front
  pose each, semi-realistic anime style (clean shading, stylised faces, realistic proportions,
  branded golf wear). No turnarounds exist — the old Confluence pipeline required
  front/side/back asset sheets; those were never made for this roster.
- **Old pipeline (Confluence "3D Character Creation Process"):** 17 steps, 4 roles (2D, 3D,
  tech artist, engineer), high-poly → low-poly → hair cards → rig. Meeting notes 2025-11-20: *all
  male models had arms too short for realistic animations* — the "hideous" problem was partly
  proportions vs. animation, not only surface quality.
- **Render target:** URP mobile, ASTC, quality tiers Low/Mid/High (`QUALITY_TIERS_9A_SPEC`).
  Budget per character: ≤ 15k tris, one 2048 atlas (1024 on Low), URP Lit or Simple Lit, no
  cloth sim, hair as mesh (no cards with alpha overdraw on Low).

## 2. Hard requirements for any option

| # | Requirement | Why |
|---|---|---|
| R1 | Unity **Humanoid** rig, clean T-pose, standard bone hierarchy (hips→spine→chest→neck→head; 3-joint arms/legs; 5-finger hands) | Lets one Animator Controller + the animation pack retarget to all 12 with zero per-character animation work. |
| R2 | Bottom-centre pivot, 1 unit = 1 m, ~1.75 m male / ~1.65 m female | Tee placement, camera rigs, club socket math. |
| R3 | Realistic limb proportions (arm span ≈ height) | The old models failed exactly here — swing clips clip through the body if arms are short. |
| R4 | ≤ 15k tris, 1 material, 2048 atlas, no transparency except eyelashes | Mobile Low tier. |
| R5 | Sockets: `ClubSlot`, `PutterSlot`, `GameplayIdleClubSlot`, `GameplayIdlePuttClubSlot`, `ClubStart`, `ClubEnd` under the right hand / grip bones | Existing prefab contract. |
| R6 | Naming `MESH_<Name>` / `T_<Name>_Albedo` / `M_<Name>` / `ANIM_*` | Confluence + CLAUDE.md conventions. |
| R7 | Face reads as the 2D portrait at HUD size (`Portraits/InGame`) | "Look like their 2D counterparts" — judged at phone size, not close-up. |

If the animation pack is Generic (baked to the old studio skeleton), R1 changes to "match that
skeleton's bone names" — confirm before ordering anything. **NOTE:** pack not found in Redux; assume
Humanoid until the FBX is inspected.

## 3. Options

### A. AI generation + your cleanup (Tripo or Meshy) — **$20–40/mo**

| Tool | Plan | Credits | Per character | Rigging | Notes |
|---|---|---|---|---|---|
| Tripo | Pro $20/mo (yearly $240) | 3,000/mo | ~15–50 cr per attempt | Auto-rig, Humanoid/Mixamo-compatible, embeds clips, GLB/FBX | Fastest iteration; "Smart LowPoly" retopo; DCC Bridge to Unity/Blender on Pro+ |
| Meshy | Pro $20/mo | 1,000/mo | 25 cr image→3D, 10 cr texture | Remesh, rig, animation are **0 credits** | Reviewers rate it most production-ready for stylised characters; .fbx/.blend export |
| Rodin (Hyper3D) | Creator $30/mo | ~60 models | — | Weaker rigging, high-poly quads | Best geometric detail; more manual retopo |
| Hunyuan 3D | free/self-host | — | — | Needs manual retopo + rig | Experimentation only |

Workflow that actually works from a single 3/4 portrait:
1. **Make a turnaround first.** Generate front / side / back in **A-pose** from the portrait with an
   image model (Firefly/Nano-Banana/Midjourney, ~$10–30/mo or existing subs). Same outfit, flat
   lighting, neutral background. This is the single biggest quality lever — single-image → 3D
   guesses the back and hallucinates the face.
2. **Multi-image → 3D** (Tripo multiview / Meshy multi-image). 3–5 attempts per character.
3. **Retopo + texture** in-tool (Tripo Smart LowPoly / Meshy Remesh → 12–15k tris, 2048 atlas).
4. **Auto-rig** in-tool, export FBX, T-pose. Import → Humanoid → check avatar mapping.
5. **Human pass (Blender, free):** face/eyes/hair re-sculpt or replace hair with a hand-made mesh,
   fix hands, weight-paint armpits/shoulders (swing clips stress these), bake clean albedo.
   Realistic effort: **6–10 h per character** after the first two.

Total tools: ~$60–120 for a 3-month run. Cost for 12 characters ≈ **$100–150 + 80–120 h of your time**.
Risk: faces. AI output converges on a generic "AI face"; hair and eyes need the human pass to read
as James/Olivia. Mitigation: pilot the two starters before buying anything beyond one month.

### B. VRoid Studio (pixiv) — **$0**

Free anime-style character maker, Humanoid VRM out of the box, commercial use allowed (pixiv FAQ),
Unity import via UniVRM (free), built-in polygon reduction. 12 characters in a **consistent** style
in days, not weeks; hair is a strength; outfits are painted textures (golf polo/skirt/cap are easy).
Downside: the result looks *VRoid-anime* — younger and flatter than our semi-realistic portraits;
faces cannot be sculpted freely (slider-based). Best fit if Ken accepts a slight style shift, or as
the **fallback body pipeline** (use VRoid for body/rig/outfit, replace head via Option A/D).
Mobile: 20–40k tris raw → reduce to ~15k; MToon shader needs URP variant (UniVRM ships it).

### C. Reallusion Character Creator 5 — **~$300–500 one-off**

CC5 perpetual $299 (Deluxe bundle $499). Semi-realistic humans, closest to our portrait style,
game-ready Humanoid skeleton, LOD/decimation, FBX to Unity. Headshot 3 (photo → head) is a paid
add-on — price not verified, budget ~$200. Outfit packs are extra ($20–60 each) and are
Western-realistic, so golf wear needs texture work. Poly counts are high (30k+ per outfit) — needs
the LOD/merge-material export every time. Total tooling ≈ $500–700, inside the < $1k frame;
learning curve ~1 week. Quality ceiling is higher and more consistent than Option A; faces still
need Headshot + tweaks to match the 2D art.

### D. Freelance 3D artist (the "separate budget")

Market rates found (2026):

| Tier | Per character (model + texture + rig) | ×12 | Notes |
|---|---|---|---|
| Fiverr / Upwork entry | $80–350 | $1k–4.2k | High variance; expect 1–2 redo rounds; insist on R1–R5 in the brief |
| Mid freelancer ($30–80/h, ~40 h) | $1.2k–3.2k | $14k–38k | Good stylised game characters |
| Senior / small studio | $2k–5k+ | $24k–60k+ | Studio pipeline from Confluence |
| **Hybrid: AI base (A/C) + freelancer finish** | $150–400 | **$1.8k–4.8k** | Artist gets a rigged base + turnaround; does face/hair/hands/weights/UV clean, 4–8 h each |

The hybrid is the sweet spot: the artist's hours go where AI fails (face likeness, hair, deformation),
not on blocking out a body for the 12th time.

### E. Rebuild the old studio pipeline — $25k+ · not recommended
Four roles, 17 review steps, and it already produced the models we are replacing.

## 4. Recommendation

**Pilot → then commit.** Two weeks, ~$60 in tools:

1. Recover the animation pack from the old project; inspect one FBX → Humanoid or Generic (decides R1).
2. Subscribe to **Meshy Pro or Tripo Pro for one month** ($20). Make A-pose turnarounds for
   **James and Olivia** from the BigRoster art. Generate, retopo, rig, import.
3. Wire one **`AnimatorController_Golfer`** + the pack; build `PfGolfer_James` with the R5 sockets
   from `PfYoungMale.prefab`'s hierarchy. Screen-record a full swing on device (Low tier).
4. Judge at phone size against `Portraits/InGame/James.png`. If the face/hair fail → hand the two
   FBX + turnarounds to a freelancer for a **$150–400 finish pass** (Option D-hybrid) and lock that
   as the per-character pipeline: **≈ $2–4k artist total for 12**, tools < $150.
5. If Ken wants a step up in fidelity instead, Option C (CC5, ~$500–700) replaces step 2 without
   changing anything downstream — same Humanoid rig, same prefab, same controller.

Do **not** start with VRoid unless the style shift is explicitly accepted; do not commission all 12
from a freelancer before the pilot proves the rig/animation contract on device.

## 5. Unity implementation (unchanged whichever option wins) — spec follows after pilot

- `Assets/Art/3D/Characters/<Name>/MESH_<Name>.fbx` + `T_<Name>_Albedo.png` + `M_<Name>.mat` (URP Simple Lit, Low tier; Lit on High).
- One `Assets/Animations/Golfer/AnimatorController_Golfer.controller` shared by all; per-model Humanoid avatar. **Clips are NOT retargeted by Unity (§7):** every model is auto-rigged in Mixamo and its clips are downloaded *on that model* (With Skin), imported as Humanoid with Avatar → *Copy From Other Avatar* = the model's own. Root settings per `golfer_3d_test` SPEC §5.1.
- `Prefabs/Characters/PfGolfer_<Name>.prefab` = mesh + Animator + the six socket transforms + two `UnplayableChecker` volumes copied from `PfYoungMale.prefab` (same script guid `41412eff…`).
- `Characters.csv` gains `modelPrefab` column (default `PfGolfer_<Name>`); loader resolves via `Resources.Load` like sprites (CSV-first, no ScriptableObjects). Missing prefab → fall back to starter model, log once (same "renderable" pattern as `CONTENT_TWO_WAY_SPEC` §4).
- Addressables **not** needed: 12 × ~1.5 MB (mesh + ASTC atlas) ≈ 18 MB in-build; revisit only if roster > 30.

## 6. Open items
- [ ] Locate old FBX + animation pack (original repo / S3). Confirm Humanoid vs Generic.
- [ ] Ken: accept semi-realistic (A/C/D) vs anime (B)? Show the pilot renders side-by-side with the 2D art.
- [ ] Headshot 3 price (not verified) if Option C is chosen.
- [ ] Who is the "separate" 3D artist — existing contact or new hire? Brief = §2 table verbatim.

## 7. Pipeline decision — animation is Mixamo-native, never Unity-retargeted (2026-09-07, from `golfer_3d_test` §9.8)

**Decision:** every roster model is uploaded to Mixamo, auto-rigged there, and the golf clips are
downloaded **on that model** ("With Skin"). Unity does no Humanoid retargeting at all: clip avatar
= character avatar. Club-in-hand mocap (CMU 64 / Motion Cast #05) is **off the table** — the clips
were never the problem.

**Evidence** (`Docs/Specs/Active/golfer_3d_test/IMPLEMENTER_REPORT.md` §13 + F1, one harness run
each, Hole 06, no tuning on either side):

| measure | Quaternius stand-in, Y-Bot clips **retargeted by Unity** | Mixamo Remy, same clips **downloaded on the model** |
|---|---|---|
| foot slide during the swing, worst foot | **0.4770 m** | **0.0915 m** (5.2× less) |
| foot slide, other foot | 0.4552 m | 0.0528 m |
| grip / finger / forearm correction | 7 rounds of solver code | none (`forceGripPose=false`) |
| mid-swing frame (t = 0.6 s) | legs splayed, feet dragged, club barely off the ball | recognisable backswing, feet planted |

Same clips by origin, same controller states, same bootstrap, same camera, same assertions; the
only variable is whether Unity retargeted. The *less* corrected prefab holds its stance 5× better,
so the bend-at-the-waist / sliding legs / open hands seen on the stand-in were a retargeting artefact
(mocap proportions ≠ body proportions, no foot pinning), not a presenter bug and not a clip quality
issue. Seven grip-tuning rounds on the Quaternius rig were spent on the wrong cause.

**What this changes in the options above**
- Any model source (A/B/C/D) must survive a Mixamo auto-rig: single mesh (or few), clean A/T-pose,
  symmetric, no separate hair/cloth rigs — check this before paying for a finish pass. VRoid (B)
  exports need the Mixamo round-trip too; treat it as a hard step in the pilot (§4 step 2).
- R1 still stands (Unity Humanoid), but its *purpose* is now the shared controller + sockets, not
  cross-model retargeting. R3 (limb proportions) matters less for animation and more for the look.
- The pilot (§4) gets one more gate: the Mixamo-native prefab's foot slide ≤ 0.10 m in the
  `golfer_3d_test` harness before the character is accepted.

**Import rule that will bite every Mixamo FBX (F2):** the character arrives ~2.33× oversized
(foot→head 3.089 m vs the 1.328 m stand-in) because the FBX carries a 0.01 file scale. Do **not**
turn *Use File Scale* off — that multiplies by 100 (measured 132.8 m). Keep `useFileScale = true`
and put the correction in `globalScale` (Remy: `1.328 / 3.089 = 0.42992`, applied to the character
**and every clip**). For roster models target R2 (≈1.75 m male / 1.65 m female) instead of 1.328 m.

**Club grip — settled 2026-09-11 (`golfer_club_grip`, nine iterations):** the club mount that transfers to every roster model is `ClubRoot → GripTarget` driven by an Animation Rigging `MultiParentConstraint` averaging **both** hand bones (a single hand bone inherits one wrist's roll and points the shaft at the camera), with one authored `ClubSlot` / `PutterSlot` local pose per model solved for head-on-ball and face-square. **The hand pose does not transfer and is not procedural**: nine rounds of IK, landmark and contact-wrap code on the stand-in produced hands that were wrong at close range and invisible at game range. At the gameplay camera the clip's own hands read fine. So the roster brief (§2 / §4) gains one deliverable: **each model ships with an authored two-hand golf grip pose** (fingers closed on the actual grip, overlap grip, per `Docs/Specs/Active/golfer_club_grip/reference/GOLF_GRIP_GEOMETRY.html`) as a one-frame clip or bone-rotation asset, applied on a fingers-only layer — the artist does this in minutes with the club in hand; it is not Code's job.

**Still open after the test:** the club's world orientation follows the clip's wrist roll, so the
shaft reads wrong at address on the Mixamo-native rig (`sbs_address.jpg`). That is fixed by
parenting the club to a grip target and pulling both hands onto the shaft with Animation Rigging
two-bone IK — spec `golfer_club_grip`. Fingers stay open until an authored grip hand pose exists
(backlog). Remy itself is 36,510 tris and is a pipeline proof, not a shippable character.

## Sources
- Tripo pricing: https://www.tripo3d.ai/pricing · Tripo Unity rig export: https://www.tripo3d.ai/blog/export-ai-character-rig-animation-unity
- Meshy pricing/credits: https://www.meshy.ai/pricing · https://docs.meshy.ai/en/webapp/pricing
- Hyper3D Rodin pricing: https://hyper3d.ai/pricing
- Tool comparison (Jul 2026): https://marcellinusprevailer.com/meshy-vs-tripo-rodin-hunyuan-3d-generator-2077dd4d4533
- CC5 pricing/features: https://www.cgchannel.com/2025/08/reallusion-releases-character-creator-5/
- VRoid commercial use: https://vroid.pixiv.help/hc/en-us/articles/4405813333657
- Freelance rates: https://rocketbrush.com/blog/3d-character-art-prices-guide · https://www.fiverr.com/resources/guides/costs/3d-artist
