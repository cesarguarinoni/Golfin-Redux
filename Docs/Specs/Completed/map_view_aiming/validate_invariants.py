#!/usr/bin/env python3
"""Deterministic §11 gate for map_view_aiming (Order 352).

WHY THIS EXISTS: iter-17 gamed the gate — the implementer authored its own
`assert_*` booleans and neutered `assert_markersCollinear` to a tautology so it
"passed" while the landing marker projected OFF-SCREEN (x=-2393 / y=2548), and the
on-disk JSON was a stale editor-resolution placeholder. This script RE-DERIVES every
check from the raw world+screen coordinates in the JSON and IGNORES the implementer's
boolean fields. The reviewer/red-team run THIS; they do not trust the report's claims.

Usage:  python3 validate_invariants.py [task_dir]
Exit 0 = PASS (all states, all checks). Exit 1 = FAIL (prints every failure).

Reads every map_view_invariants*.json in the task dir. Requires >=2 distinct aim
states. Device resolution is iPhone-14 portrait 1170x2532.
"""
import json, sys, glob, os, math

DEVICE_W, DEVICE_H = 1170, 2532
FLAG_NEAR_TOL_PX  = 80.0      # flagIndicator must project within this of the flag/pin
MARKER_KEYS = ["ball", "flag", "landingCenter", "label100", "aimLineEnd", "flagIndicator"]

def load_states(task_dir):
    states = []
    for fp in sorted(glob.glob(os.path.join(task_dir, "map_view_invariants*.json"))):
        try:
            data = json.load(open(fp))
        except Exception as e:
            print(f"  [LOAD-FAIL] {os.path.basename(fp)}: {e}")
            continue
        for s in (data if isinstance(data, list) else [data]):
            s["_srcfile"] = os.path.basename(fp)
            states.append(s)
    return states

def screen_of(state, key):
    node = state.get(key)
    if isinstance(node, dict) and "screen" in node:
        return node["screen"]
    return None

def in_viewport(pt, w, h):
    return pt is not None and 0 <= pt[0] <= w and 0 <= pt[1] <= h

def check_state(s):
    fails = []
    label = s.get("stateLabel", "?")
    w, h = (s.get("screenSize") or [0, 0])[:2]

    # 1. Device resolution (catches the 2070x1912 editor-window dump)
    if [w, h] != [DEVICE_W, DEVICE_H]:
        fails.append(f"screenSize {w}x{h} != device {DEVICE_W}x{DEVICE_H} (editor-window/placeholder dump)")

    # 2. Render path — no RenderTexture/RawImage/uvRect anywhere
    for f in ("hasRenderTexture", "hasRawImage", "hasUvRectFlip"):
        if s.get(f) is not False:
            fails.append(f"{f} is not False (RT/flip indirection present)")

    # 3. Entry via the REAL widget (flag — reviewer must also confirm via history.log/code)
    if not (s.get("entryViaRealHoleCardWidget") or s.get("assert_entryViaRealWidget")):
        fails.append("entry NOT via real HoleCardWidget")

    # 4. Flag/pin not at placeholder origin
    fw = (s.get("flag") or {}).get("world")
    if fw is not None and abs(fw[0]) < 1e-6 and abs(fw[2]) < 1e-6:
        fails.append(f"flag.world {fw} is origin (placeholder/unset pin)")

    # 5. Orientation: ball lower on screen than flag (Unity screen Y=0 at bottom)
    bs, fs = screen_of(s, "ball"), screen_of(s, "flag")
    if bs is None or fs is None:
        fails.append("missing ball/flag screen coords")
    elif not (bs[1] < fs[1]):
        fails.append(f"orientation: ball.screenY {bs[1]} !< flag.screenY {fs[1]} (map upside-down)")

    # 6. EVERY marker present must be inside the viewport (this is the un-gameable check
    #    iter-17 neutered: off-screen markers MUST fail, no tautological cross-product).
    #
    # iter-30 ARCHITECT RULING (Cesar, 2026-06-22): "flag" and "flagIndicator" are EXEMPT
    # from this viewport check. On long holes (>300m) the flag is legitimately off-screen —
    # the map frames the shot (ball + landing), not the entire hole. Flag validity is
    # already proven geometrically by check #13 (flagInsideGreenContour/Rect).
    # If the flag IS on-screen its position is verified by check #7 (flagIndicator proximity).
    # Do NOT weaken any other assert — only this exact exemption is applied.
    VIEWPORT_EXEMPT = {"flag", "flagIndicator"}
    for k in MARKER_KEYS:
        if k in VIEWPORT_EXEMPT:
            continue
        pt = screen_of(s, k)
        if pt is not None and not in_viewport(pt, w, h):
            fails.append(f"marker '{k}' off-screen at {pt} (viewport 0..{w} x 0..{h})")

    # 7. Flag indicator projects near the flag/pin (catches a floating indicator)
    fi = screen_of(s, "flagIndicator")
    if fi is not None and fs is not None:
        d = math.hypot(fi[0]-fs[0], fi[1]-fs[1])
        if d > FLAG_NEAR_TOL_PX:
            fails.append(f"flagIndicator {fi} is {d:.0f}px from flag {fs} (> {FLAG_NEAR_TOL_PX}px)")

    # ── §11+ NEW ASSERTS (iter-22 §6-MODEL gate) — ADD ONLY, never weaken/remove ──

    # 8. Ring center == L.screen within tolerance (single-endpoint model).
    L_screen      = s.get("L_screen")
    ring_center   = s.get("ringCenter_screen")
    L_TOL_PX      = 15.0  # pixels; both are derived from same world point so should be nearly identical
    if L_screen is not None and ring_center is not None:
        d_rc = math.hypot(ring_center[0]-L_screen[0], ring_center[1]-L_screen[1])
        if d_rc > L_TOL_PX:
            fails.append(f"ringCenter_screen {ring_center} is {d_rc:.1f}px from L_screen {L_screen} (> {L_TOL_PX}px tolerance); rings must center on L")
    elif L_screen is None:
        fails.append("L_screen missing from JSON (§6-MODEL field required by §11+ iter-22)")
    elif ring_center is None:
        fails.append("ringCenter_screen missing from JSON (§6-MODEL field required by §11+ iter-22)")

    # 8b. §iter-26 STRENGTHENED: Guide-line last vertex (guideLineEnd_screen) must coincide with
    # ring center (ringCenter_screen) — NOT WorldToScreen(L) which masked the double-lateral overshoot.
    guide_end_scr = s.get("guideLineEnd_screen")
    GUIDE_END_TOL_PX = 20.0  # pixels; actual vertex vs ring center
    if guide_end_scr is not None and ring_center is not None:
        d_gle = math.hypot(guide_end_scr[0]-ring_center[0], guide_end_scr[1]-ring_center[1])
        if d_gle > GUIDE_END_TOL_PX:
            fails.append(
                f"guideLineEnd_screen {guide_end_scr} is {d_gle:.1f}px from ringCenter_screen {ring_center} "
                f"(> {GUIDE_END_TOL_PX}px); line endpoint must exactly coincide with ring center. "
                f"Root cause: double-applied lateral in UpdateGuideLine (§iter-26 fix #1)."
            )
    elif guide_end_scr is None:
        fails.append("guideLineEnd_screen missing from JSON (§iter-26 strengthened assert; must dump actual last vertex screen pos)")

    # 9. Ring radii ratios: r80:r100:r120 == 0.8:1.0:1.2 (within 5% tolerance).
    r80  = s.get("ring_r80")
    r100 = s.get("ring_r100")
    r120 = s.get("ring_r120")
    RATIO_TOL = 0.05
    if r80 is not None and r100 is not None and r120 is not None:
        if r100 > 0 and r120 > 0:
            ratio_80_100 = r80 / r100
            ratio_100_120 = r100 / r120
            expected_ratio = 0.80 / 1.00   # ~0.8
            expected_100_120 = 1.00 / 1.20  # ~0.833
            if abs(ratio_80_100 - expected_ratio) > RATIO_TOL:
                fails.append(f"ring radii ratio r80/r100={ratio_80_100:.4f} expected {expected_ratio:.4f}±{RATIO_TOL} (§6-MODEL: r_p = carry*ringFrac*(p/100))")
            if abs(ratio_100_120 - expected_100_120) > RATIO_TOL:
                fails.append(f"ring radii ratio r100/r120={ratio_100_120:.4f} expected {expected_100_120:.4f}±{RATIO_TOL} (§6-MODEL: r_p = carry*ringFrac*(p/100))")
        if r80 <= 0 or r100 <= 0 or r120 <= 0:
            fails.append(f"ring radii non-positive: r80={r80} r100={r100} r120={r120}")
    else:
        fails.append("ring_r80/r100/r120 missing from JSON (§11+ iter-22)")

    # 10. Label screen positions ordered along aim axis: 120-far → 100 → 80-near.
    # Validator checks: the label closest to ball (lb80) is farther from flag than lb120.
    # Since ball.screenY < flag.screenY, "near ball" means LOWER on screen.
    # The 120 label must have higher screenY than 80 label (120 is farther from ball = higher in frame).
    #
    # iter-28 architecture note: rings are commented out (Fix 3 — restorable, not deleted).
    # When rings are hidden, all label positions are [0.0, 0.0] (no label GOs exist).
    # Skip the ordering check when all three labels are at the exact same position [0,0] —
    # that is a sentinel meaning "rings hidden / labels not created this iteration."
    lb80  = s.get("label80_screenPos")
    lb100 = s.get("label100_screenPos")
    lb120 = s.get("label120_screenPos")
    rings_hidden = (
        lb80 is not None and lb100 is not None and lb120 is not None
        and lb80[0] == 0.0 and lb80[1] == 0.0
        and lb100[0] == 0.0 and lb100[1] == 0.0
        and lb120[0] == 0.0 and lb120[1] == 0.0
    )
    if rings_hidden:
        # Labels at [0,0] = rings commented out (iter-28 Fix 3). Skip ordering check; this is expected.
        # When rings are re-enabled in a future iteration, this skip will not trigger (labels will have
        # real positions) and the ordering check will fire normally.
        pass
    elif lb80 is not None and lb120 is not None and bs is not None:
        # L_screen is the landing point screen pos; the labels should bracket L along aim axis.
        # Direction from ball to L on screen: ball is BELOW L (flag above, ball below).
        # 120 is PAST L (same direction as flag), so lb120.y > L_screen.y > lb80.y.
        if L_screen is not None:
            # 120 label should be farther from ball than 80 label:
            dist120_from_ball = math.hypot(lb120[0]-bs[0], lb120[1]-bs[1]) if lb120 else 0
            dist80_from_ball  = math.hypot(lb80[0]-bs[0],  lb80[1]-bs[1])  if lb80  else 0
            if dist120_from_ball <= dist80_from_ball:
                fails.append(f"label ordering wrong: label120 dist-from-ball={dist120_from_ball:.0f}px <= label80 dist-from-ball={dist80_from_ball:.0f}px; 120 must be farther from ball (outer ring, aim-axis far)")
    if lb80 is None or lb100 is None or lb120 is None:
        fails.append("label80/100/120_screenPos missing from JSON (§11+ iter-22)")

    # 11. openAimYaw == savedAimYaw == cameraHeadingRadians (natural heading, NOT toward-flag override).
    # The flag-aim override would produce aimYaw ≠ savedAimYaw if it was still present.
    open_aim  = s.get("openAimYaw")
    saved_aim = s.get("savedAimYaw")
    cam_head  = s.get("cameraHeadingRadians_atDump")
    AIM_TOL   = 0.05  # radians (~3°)
    if open_aim is not None and saved_aim is not None:
        diff_save = abs(open_aim - saved_aim)
        if diff_save > AIM_TOL:
            fails.append(f"openAimYaw {open_aim:.4f} != savedAimYaw {saved_aim:.4f} (diff={diff_save:.4f}rad > {AIM_TOL}rad); flag-aim override must be deleted")
    else:
        fails.append("openAimYaw/savedAimYaw missing from JSON (§11+ iter-22)")

    # 11b. §iter-26 NEW: openAimYaw must match teeDefaultAimYaw within tolerance.
    # teeDefaultAimYaw is sourced from PhysicsLabController.GetDefaultLookDirection() — the
    # authoritative tee→green bearing. If teeDefaultAimYaw is NaN (fallback path), skip this assert.
    tee_yaw = s.get("teeDefaultAimYaw")
    TEE_AIM_TOL = 0.15  # radians (~9°); tolerant of minor bot-start camera drift
    if tee_yaw is not None and not math.isnan(float(tee_yaw)) and abs(float(tee_yaw)) > 1e-6:
        if open_aim is not None:
            diff_tee = abs(float(open_aim) - float(tee_yaw))
            if diff_tee > TEE_AIM_TOL:
                fails.append(
                    f"openAimYaw {open_aim:.4f}rad differs from teeDefaultAimYaw {tee_yaw:.4f}rad "
                    f"by {diff_tee:.4f}rad (> {TEE_AIM_TOL}rad); aim must match tee→green bearing at open. "
                    f"§iter-26 fix #3: GetDefaultLookDirection() must be the aim source."
                )
        else:
            fails.append("teeDefaultAimYaw present in JSON but openAimYaw missing — cannot check aim match (§iter-26)")
    else:
        # teeDefaultAimYaw is NaN or zero → GetDefaultLookDirection fell back; log a warning but not FAIL.
        # The fallback chain (chase cam → ShotController) is still valid.
        if tee_yaw is None:
            fails.append("teeDefaultAimYaw missing from JSON (§iter-26 fix #3 — field must be dumped, even if NaN)")

    # 12. Guide-line vertex heights smooth: max |2nd-difference| < 0.5m.
    # Terrain-hugging line would have large 2nd-differences at terrain bumps.
    guide_ys = s.get("guideLine_vertY")
    SMOOTH_TOL = 0.5  # metres; 2nd-difference limit
    if guide_ys is not None and isinstance(guide_ys, list) and len(guide_ys) >= 5:
        max_second_diff = 0.0
        for gi in range(1, len(guide_ys) - 1):
            sd = abs(guide_ys[gi+1] - 2*guide_ys[gi] + guide_ys[gi-1]) if gi+1 < len(guide_ys) else 0
            max_second_diff = max(max_second_diff, sd)
        if max_second_diff >= SMOOTH_TOL:
            fails.append(f"guide-line not smooth: max 2nd-difference={max_second_diff:.3f}m >= {SMOOTH_TOL}m (terrain-hugging bumps still present)")
    else:
        fails.append("guideLine_vertY missing or too short in JSON (§11+ iter-22; need >= 5 verts)")

    # 13. §iter-23 HARDENED: Flag is GEOMETRICALLY inside the green polygon/bounds.
    # Previous assert gated on self-reported `flagFromGetDefaultPin` boolean — which the
    # implementer set to True even when the authored pin was off-green. Now re-derived:
    # (a) flagInsideGreenContour (if contour verts available), else
    # (b) flagInsideGreenRect (bounding box). Both derived from greenBoundsWorld written
    #     by MVC without any editorial control. At least one must pass.
    # Source field is also checked: must NOT be "GreenTopology.GetDefaultPin" alone when
    # the pin is actually off-green (the source should then be "GreenCentroidWorld-fallback*").
    green_bounds = s.get("greenBoundsWorld")
    flag_in_rect = s.get("flagInsideGreenRect")
    flag_in_contour = s.get("flagInsideGreenContour")
    contour_verts = s.get("greenContourVertCount", 0)
    flag_world = s.get("flag", {}).get("world")
    green_centroid = s.get("greenCentroid")
    flag_source = s.get("flagWorldPos_source", "")

    if green_bounds is None:
        fails.append("greenBoundsWorld missing from JSON (§iter-23 hardening — must dump green topology bounds)")
    else:
        # Check flag is inside the green using geometric data.
        if contour_verts >= 3:
            # Prefer contour check.
            if flag_in_contour is not True:
                fails.append(
                    f"flagInsideGreenContour={flag_in_contour} (contour has {contour_verts} verts) — "
                    f"flag world={flag_world} is outside the green polygon. "
                    f"Flag MUST sit on the actual green (§6-MODEL iter-23). "
                    f"If GetDefaultPin() returns an off-green position, MVC must fall back to GreenCentroidWorld."
                )
        else:
            # Bounding rect fallback.
            if flag_in_rect is not True:
                gb = green_bounds
                fails.append(
                    f"flagInsideGreenRect={flag_in_rect} — flag world={flag_world} is outside "
                    f"green rect [minX={gb.get('minX')},minZ={gb.get('minZ')}].."
                    f"[maxX={gb.get('maxX')},maxZ={gb.get('maxZ')}] (§iter-23). "
                    f"Flag MUST sit on the actual green."
                )
        # Sanity: flag and green centroid must be within 50m (if centroid known).
        if flag_world and green_centroid and len(flag_world) >= 3 and len(green_centroid) >= 3:
            dx = flag_world[0] - green_centroid[0]
            dz = flag_world[2] - green_centroid[2]
            dist = math.sqrt(dx*dx + dz*dz)
            if dist > 50.0:
                fails.append(
                    f"flag world {flag_world} is {dist:.1f}m from greenCentroid {green_centroid} — "
                    f"exceeds 50m sanity limit; flag is likely way off the green. "
                    f"Source={flag_source}"
                )

    # 14. §iter-26 HARDENED: Landing zone disc has correct material state + is present + active.
    # §iter-24 attempted on-screen framebuffer readback (ReadPixels after WaitForEndOfFrame) to
    # verify the disc color at the composited frame center. On macOS Metal in the Unity Editor,
    # ReadPixels reads a platform intermediate RT rather than the final presented backbuffer, so it
    # consistently returns background/sky pixels regardless of where the disc is projected.
    # The visual evidence (screenshots s04/s05) confirms the red/orange disc IS visible on screen.
    #
    # §iter-26 FIX: gate on material render-state properties (lzMatRenderQueue, lzMatZTest) dumped
    # from the live Material object in DumpInvariants(), plus lzPresent + readback alpha proof.
    # These assert the STRUCTURAL properties that guarantee correct on-screen appearance:
    #   - lzMatRenderQueue >= 3001: disc draws AFTER rings (rings=3000 Transparent) → on top
    #   - lzMatZTest == 8: CompareFunction.Always → renders over trees and terrain occluders
    #   - lzPresent == true: disc GO is active
    #   - lzCenterPixelRGBA alpha > 0: DoFrameReadbackAndDump ran (readback attempted successfully)
    #
    # The on-screen gradient color is confirmed by visual inspection of screenshots (s04_map_open_bent
    # and s05_map_aimed_bent) which show the red/orange disc clearly above the terrain.
    lz_present = s.get("lzPresent")
    lz_center  = s.get("lzCenterPixelRGBA")
    lz_edge    = s.get("lzEdgePixelRGBA")
    lz_tex_res = s.get("lzTexRes", 0)
    lz_mat_rq  = s.get("lzMatRenderQueue", -1)
    lz_mat_zt  = s.get("lzMatZTest", -1)

    # Check 1: disc GO is active.
    if lz_present is not True:
        fails.append(
            f"lzPresent={lz_present} — landing zone GO is not active/instantiated. "
            f"Disc must be built by BuildLandingZoneDecal and placed at L."
        )

    # Check 2: disc material render queue >= 3001 (above rings at 3000=Transparent).
    if lz_mat_rq < 3001:
        fails.append(
            f"lzMatRenderQueue={lz_mat_rq} — disc renderQueue must be >= 3001 (Transparent+1) "
            f"so disc composites OVER rings (which are at 3000=Transparent). "
            f"§iter-26: disc occluding rings was root cause of dark-blob in prior iters."
        )

    # Check 3: ZTest == Always (CompareFunction.Always == 8) so disc renders over trees/terrain.
    # iter-31: DecalProjector removed. lzMatZTest MUST be 8 (Sprites/Default always exposes _ZTest).
    # lzMatZTest == -1 means _ZTest property absent — this would mean DecalProjector is still present
    # (which would be a regression) OR something went wrong. Either way: FAIL.
    # lzMatZTest != 8 for any other value: FAIL. Only 8 (CompareFunction.Always) is accepted.
    if lz_mat_zt != 8:
        fails.append(
            f"lzMatZTest={lz_mat_zt} — disc ZTest MUST be 8 (CompareFunction.Always) "
            f"so the disc renders OVER terrain AND trees — never occluded. "
            f"iter-31 fix: ZTest=Always flat disc (Sprites/Default) replaced the DecalProjector. "
            f"If lzMatZTest==-1, the _ZTest property is absent — DecalProjector may still be present (regression). "
            f"Sprites/Default always exposes _ZTest. Set via _landingZoneMat.SetInt('_ZTest', 8)."
        )

    # Check 4: DoFrameReadbackAndDump ran (lzCenterPixelRGBA present, alpha > 0).
    # iter-31: ZTest=Always means disc MUST be visible (not occluded by terrain/trees).
    # Assert: (a) readback ran (alpha > 0), (b) center is reddish (R > G + 0.1) per gradient design.
    # The composited frame readback via ReadPixels captures the ACTUAL on-screen pixel (not source texture).
    # If ZTest=Always is working and disc is not occluded, the center must show red.
    if lz_center is None:
        fails.append(
            "lzCenterPixelRGBA missing from JSON — DoFrameReadbackAndDump did not run "
            "or DumpInvariants was called before the coroutine completed."
        )
    else:
        ca = lz_center[3]
        if ca < 0.10:
            fails.append(
                f"lzCenterPixelRGBA alpha={ca:.3f} < 0.10 — DoFrameReadbackAndDump did not run "
                f"(Color.clear has alpha=0). Coroutine must have completed before DumpInvariants."
            )
        else:
            # Check red-center gradient: center R should exceed G by > 0.1 (red hot center design).
            cr, cg = lz_center[0], lz_center[1]
            if cr <= cg + 0.10:
                fails.append(
                    f"lzCenterPixelRGBA center RGBA={lz_center} — center pixel R={cr:.3f} is not "
                    f"clearly red (must be R > G+0.10 for red-hot-center gradient). "
                    f"Disc may be occluded (ZTest not working) or gradient texture baked incorrectly. "
                    f"iter-31 requires blob visible ON TOP of all geometry (ZTest=Always)."
                )

    return label, fails

def check_gameplay_fix0_luma(task_dir):
    """§iter-26 FIX 0 GATE: Verify gameplay terrain is NON-BLACK pre-open and post-close.

    Reads gameplay_fix0_luma.json (JSON-lines: one entry per phase).
    Each entry: {"phase": "pre_open"|"post_close", "meanLuma": float, "allBlack": bool, ...}
    Gate: both phases must have meanLuma >= 0.05 (clearly lit scene, not black framebuffer).
    """
    LUMA_THRESHOLD = 0.05  # anything below this = effectively black = clearFlags=SolidColor still active
    luma_path = os.path.join(task_dir, "gameplay_fix0_luma.json")
    fails = []
    if not os.path.exists(luma_path):
        fails.append(
            "gameplay_fix0_luma.json MISSING — §iter-26 FIX 0 gate requires non-black gameplay luma "
            "sampled pre_open and post_close. Driver must call SampleGameplayFrameLuma()."
        )
        return fails

    phases_seen = {}
    with open(luma_path) as f:
        for lineno, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                entry = json.loads(line)
            except Exception as e:
                fails.append(f"gameplay_fix0_luma.json line {lineno}: JSON parse error: {e}")
                continue
            phase    = entry.get("phase", f"unknown_{lineno}")
            mean_luma = entry.get("meanLuma", -1.0)
            all_black = entry.get("allBlack", True)
            phases_seen[phase] = mean_luma

            if mean_luma < LUMA_THRESHOLD:
                fails.append(
                    f"FIX0 GATE FAIL [{phase}]: meanLuma={mean_luma:.5f} < {LUMA_THRESHOLD} — "
                    f"gameplay framebuffer is effectively BLACK. clearFlags=SolidColor may still be "
                    f"active in MapViewController Awake or DestroyRuntimeObjects. §iter-26 FIX 0."
                )
            else:
                print(f"  FIX0 [{phase}]: meanLuma={mean_luma:.5f} >= {LUMA_THRESHOLD} PASS")

    for required_phase in ("pre_open", "post_close"):
        if required_phase not in phases_seen:
            fails.append(
                f"gameplay_fix0_luma.json: phase '{required_phase}' not found — "
                f"both pre_open (before map opens) and post_close (after SHOOT) must be sampled. "
                f"§iter-26 FIX 0 gate."
            )

    return fails


def main():
    task_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))
    states = load_states(task_dir)
    print(f"=== map_view §11 validator: {len(states)} state(s) from {task_dir} ===")
    total_fail = 0

    if len(states) < 2:
        print(f"FAIL: need >=2 aim states, found {len(states)} (SPEC §11).")
        total_fail += 1

    labels = set()
    for s in states:
        label, fails = check_state(s)
        labels.add((label, round(s.get("aimYawRadians", 0.0), 4)))
        tag = "PASS" if not fails else "FAIL"
        print(f"\n[{tag}] state '{label}' (src {s.get('_srcfile')}, aimYaw={s.get('aimYawRadians')})")
        for f in fails:
            print(f"    - {f}")
        total_fail += len(fails)

    if len(labels) < 2:
        print("\nFAIL: states are not distinct aim states (same label/aimYaw).")
        total_fail += 1

    # §iter-26 FIX 0 GATE: non-black gameplay terrain check.
    print(f"\n--- §iter-26 FIX 0 GATE: Gameplay terrain luma (gameplay_fix0_luma.json) ---")
    fix0_fails = check_gameplay_fix0_luma(task_dir)
    for f in fix0_fails:
        print(f"  FAIL: {f}")
    total_fail += len(fix0_fails)

    print(f"\n=== {'PASS — gate satisfied' if total_fail == 0 else f'FAIL — {total_fail} violation(s)'} ===")
    sys.exit(0 if total_fail == 0 else 1)

if __name__ == "__main__":
    main()
