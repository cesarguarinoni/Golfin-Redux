#!/usr/bin/env python3
"""Stage 2 proposed k-values with empirical scaling — use v=20 and v=25 as anchors."""
import csv, statistics, math
from collections import defaultdict

CSV_PATH = '/Users/cesar/Documents/GolfinRedux/Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv'
with open(CSV_PATH, encoding='utf-8-sig') as f:
    rows = list(csv.DictReader(f))

ROLL_SURFACES = {'Fairway', 'Green', 'Sand'}
ROLL_HOLES = {'1', '9'}

roll_rows = [r for r in rows if r['mode']=='roll' and r['surface_target'] in ROLL_SURFACES and r['end_surface']==r['surface_target'] and r['source_hole'] in ROLL_HOLES]
putt_rows = [r for r in rows if r['mode']=='putt']

# Stimpmeter — fix lookup, v=1.80 not 1.83
green_stimp = [float(r['roll_distance_m']) for r in putt_rows if r['surface_target']=='Green' and abs(float(r['target_v_horizontal_mps']) - 1.80) < 0.01]
print(f'Green Stimp v=1.80: n={len(green_stimp)}, median={statistics.median(green_stimp):.4f}')
v0 = 1.80
print(f'  d=v0/k (k=0.50): {v0/0.50:.4f}  -> obs/pred: {statistics.median(green_stimp)/(v0/0.50):.4f} ({((statistics.median(green_stimp)/(v0/0.50))-1)*100:+.2f}%)')

# But Cesar cited 1.83 m/s as the canonical USGA Stimpmeter release.  Putt.csv comment says 1.829.
# The CSV has v_tgt=1.80 (rounded?), v_contact=1.830 (actual at ball center).
# Use v=1.83 for predict.
print(f'  d=1.83/0.50 = {1.83/0.50:.4f}  -> obs/pred: {statistics.median(green_stimp)/(1.83/0.50):.4f} ({((statistics.median(green_stimp)/(1.83/0.50))-1)*100:+.2f}%)')

# ---- ROLL PATH proposal with v=20 and v=25 anchors (both high-end, real driver landings) ----
print('\n=== ROLL-PATH PROPOSAL: anchored at v=20 (contact ~24) and v=25 (contact ~29.5) ===')
TARGETS = {
    # v=25 represents tour driver landing (contact ~29.5 m/s ≈ 66 mph)
    # Sources:
    #   Trackman Annual Golf Report 2024 → PGA driver carry 275 yd + roll 15-30 yd on tour fairway = 13.7-27.4 m roll
    #   Field observation: approach to green checks within 2-4 m for well-struck mid-iron (PGA Tour)
    #   Penner 2002 + Cochran 1968 → ball plugs in sand within ~1 m after high-speed impact
    'Fairway': {'v20_target': 14.0, 'v25_target': 20.0},
    'Green':   {'v20_target':  2.0, 'v25_target':  3.0},
    'Sand':    {'v20_target':  1.0, 'v25_target':  1.2},
}
CURRENT_K = {'Fairway': 0.18, 'Green': 0.12, 'Sand': 0.70}

buckets = defaultdict(list)
for r in roll_rows:
    buckets[(r['surface_target'], float(r['target_v_horizontal_mps']))].append(r)

# For Sand, use H9 only (Cesar caveat — H1 sand zone is over-damped)
sand_h9_buckets = defaultdict(list)
for r in roll_rows:
    if r['surface_target']=='Sand' and r['source_hole']=='9':
        sand_h9_buckets[float(r['target_v_horizontal_mps'])].append(r)

print(f'\n{"surf":<10} {"k_cur":>6} {"d_obs_v20":>10} {"d_obs_v25":>10} {"target_v20":>11} {"target_v25":>11} {"k_v20":>7} {"k_v25":>7} {"k_avg":>7}')
proposed = {}
for surf in ['Fairway', 'Green', 'Sand']:
    if surf == 'Sand':
        d20 = statistics.median([float(s['roll_distance_m']) for s in sand_h9_buckets[20.0]])
        d25 = statistics.median([float(s['roll_distance_m']) for s in sand_h9_buckets[25.0]])
        note = ' (H9 only — Cesar caveat)'
    else:
        d20 = statistics.median([float(s['roll_distance_m']) for s in buckets[(surf, 20.0)]])
        d25 = statistics.median([float(s['roll_distance_m']) for s in buckets[(surf, 25.0)]])
        note = ''
    k_cur = CURRENT_K[surf]
    t20 = TARGETS[surf]['v20_target']
    t25 = TARGETS[surf]['v25_target']
    # Linear scaling: k_new = k_cur * (d_obs / d_target).  Higher k → less roll.
    k20 = k_cur * (d20 / t20)
    k25 = k_cur * (d25 / t25)
    k_avg = (k20 + k25) / 2
    proposed[surf] = round(k_avg, 2)
    print(f'{surf:<10} {k_cur:>6.2f} {d20:>10.3f} {d25:>10.3f} {t20:>11.1f} {t25:>11.1f} {k20:>7.3f} {k25:>7.3f} {k_avg:>7.3f}{note}')

print(f'\nFinal proposal (rounded to 2 dp): {proposed}')

# ---- For Rough/Tee we have no clean CSV data because Cesar's filter excluded them ----
# But we know surfaces.csv lists their current k values.  Cesar's original Phase B targets
# called for Fairway 0.18→~0.30 and Rough 0.45→~0.65 (+44% bumps).  Without clean data,
# the implementer should NOT touch Rough/Tee in this stage — flag as Phase B follow-up.

# ---- Putt path — defer.  Stimpmeter drift is -1.4%, within noise.  Tighter calibration is Stage 3 if needed. ----
