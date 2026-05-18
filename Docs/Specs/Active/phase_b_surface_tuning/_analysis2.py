#!/usr/bin/env python3
"""Stage 2 deep analysis — split by hole, compute predict/observed ratios, propose k targets."""
import csv, statistics, math
from collections import defaultdict

CSV_PATH = '/Users/cesar/Documents/GolfinRedux/Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv'
with open(CSV_PATH, encoding='utf-8-sig') as f:
    rows = list(csv.DictReader(f))

ROLL_SURFACES = {'Fairway', 'Green', 'Sand'}
ROLL_HOLES = {'1', '9'}

roll_rows = [r for r in rows if r['mode'] == 'roll' and r['surface_target'] in ROLL_SURFACES and r['end_surface'] == r['surface_target'] and r['source_hole'] in ROLL_HOLES]
putt_rows = [r for r in rows if r['mode'] == 'putt']

# ---- 1. PUTT JITTER CHECK (verify n=1 effective due to deterministic sim) ----
print('=== PUTT JITTER CHECK: stdev within (surface, target_v) buckets ===')
b = defaultdict(list)
for r in putt_rows:
    b[(r['surface_target'], float(r['target_v_horizontal_mps']))].append(float(r['roll_distance_m']))
for k, v in sorted(b.items()):
    if len(v) > 1:
        sd = statistics.stdev(v)
        print(f'  {k[0]:<10} v={k[1]:>5.2f}  n={len(v)}  mean={statistics.mean(v):.4f}  stdev={sd:.6f}')

# ---- 2. SAND H1 vs H9 (median-filter caveat) ----
print('\n=== SAND H1 vs H9 (median + spread) ===')
for vt in sorted(set(float(r['target_v_horizontal_mps']) for r in roll_rows if r['surface_target']=='Sand')):
    for hole in ['1', '9']:
        s = [float(r['roll_distance_m']) for r in roll_rows if r['surface_target']=='Sand' and r['source_hole']==hole and float(r['target_v_horizontal_mps'])==vt]
        if s:
            print(f'  Sand H{hole} v={vt:>5.1f}  n={len(s)}  vals={[round(x,3) for x in s]}  med={statistics.median(s):.3f}')

# ---- 3. KEY OBSERVATION TABLE: median roll vs actual_v_contact for each surface ----
print('\n=== ROLL PATH OBSERVED: (surface, target_v) -> median roll, median v_contact ===')
print(f'{"surf":<10} {"v_tgt":>6} {"v_contact":>10} {"roll_med":>10} {"d/v_contact":>12} {"k_eff(d=v²/2gk)":>18}')
buckets = defaultdict(list)
for r in roll_rows:
    buckets[(r['surface_target'], float(r['target_v_horizontal_mps']))].append(r)

for key in sorted(buckets.keys()):
    surf, vt = key
    samples = buckets[key]
    vc = statistics.median([float(s['actual_v_at_contact_mps']) for s in samples])
    d = statistics.median([float(s['roll_distance_m']) for s in samples])
    # k_eff back-solved from d = v²/(2gk) — toy model, just for shape sense
    k_eff = (vc*vc) / (2 * 9.81 * d) if d > 0.001 else float('nan')
    print(f'{surf:<10} {vt:>6.1f} {vc:>10.3f} {d:>10.3f} {d/vc:>12.4f} {k_eff:>18.3f}')

# ---- 4. STIMPMETER GREEN PUTT: confirm Cesar's 3.5333 vs 3.58 predict ----
print('\n=== STIMPMETER GREEN PUTT (canonical test, v=1.83 m/s) ===')
green_stimp = [float(r['roll_distance_m']) for r in putt_rows if r['surface_target']=='Green' and abs(float(r['target_v_horizontal_mps'])-1.83) < 0.01]
print(f'  n={len(green_stimp)}  vals={green_stimp[:3]}{"..." if len(green_stimp)>3 else ""}  median={statistics.median(green_stimp):.4f}')
# Putt.csv has Green k=0.50.  Simple model: d = v/k → 1.83/0.50 = 3.66 m.  Other model: d = v²/(2gk) → 1.83²/(2*9.81*0.50) = 0.341 m (doesn't match).
# The 3.58 m Cesar cites must be from a "d = v/k" linear model in code.
d_pred_linear = 1.83 / 0.50
d_pred_squared = 1.83**2 / (2*9.81*0.50)
print(f'  d_pred LINEAR (v/k, k=0.50): {d_pred_linear:.4f}')
print(f'  d_pred QUADRATIC (v²/2gk):  {d_pred_squared:.4f}')
print(f'  Observed/LINEAR: {statistics.median(green_stimp)/d_pred_linear:.4f}  ({((statistics.median(green_stimp)/d_pred_linear)-1)*100:+.2f}% drift)')

# ---- 5. PROPOSE NEW k VALUES (rough first-pass) ----
# Method: empirically scale current k so that v=25 (high end, closest to real driver landing speed) hits target roll.
print('\n=== NEW k PROPOSAL (driver-landing v=25 m/s target, contact ~29.5 m/s) ===')
# Targets per real-golf benchmarks:
# Fairway: driver roll-out 15-30 yd (Trackman 2024) = 13.7-27.4 m. Mid: 20 m
# Green: well-struck approach checks in 2-4 m. Mid: 3 m
# Sand: balls plug/check in <2 m. Mid: 1 m (already close)
TARGETS_AT_V25 = {'Fairway': 20.0, 'Green': 3.0, 'Sand': 1.0}
CURRENT_K_ROLL = {'Fairway': 0.18, 'Green': 0.12, 'Sand': 0.70}
for surf in ['Fairway', 'Green', 'Sand']:
    samples = buckets[(surf, 25.0)]
    d_obs = statistics.median([float(s['roll_distance_m']) for s in samples])
    target = TARGETS_AT_V25[surf]
    k_cur = CURRENT_K_ROLL[surf]
    # Inverse linear scaling: k_new = k_cur * (d_obs / d_target)
    k_new = k_cur * (d_obs / target)
    print(f'  {surf:<10} current_k={k_cur:.2f}  d_obs(v25)={d_obs:>6.3f} m  target={target:.1f} m  -> proposed_k={k_new:.3f}')
