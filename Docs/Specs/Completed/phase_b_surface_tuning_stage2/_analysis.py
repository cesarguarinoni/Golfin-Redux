#!/usr/bin/env python3
"""Phase B Stage 2 — analyze sweep.csv against Cesar's filter."""
import csv, statistics, math
from collections import defaultdict

CSV_PATH = '/Users/cesar/Documents/GolfinRedux/Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv'

with open(CSV_PATH, encoding='utf-8-sig') as f:
    rows = list(csv.DictReader(f))

print(f'TOTAL rows: {len(rows)}')
print(f'cols: {list(rows[0].keys())}')
print(f'modes: {sorted(set(r["mode"] for r in rows))}')
print(f'source_holes: {sorted(set(r["source_hole"] for r in rows))}')
print(f'surface_targets: {sorted(set(r["surface_target"] for r in rows))}')
print(f'end_surfaces: {sorted(set(r["end_surface"] for r in rows))}')
print()

ROLL_SURFACES = {'Fairway', 'Green', 'Sand'}
ROLL_HOLES = {'1', '9'}

roll_rows = [
    r for r in rows
    if r['mode'] == 'roll'
    and r['surface_target'] in ROLL_SURFACES
    and r['end_surface'] == r['surface_target']
    and r['source_hole'] in ROLL_HOLES
]
putt_rows = [r for r in rows if r['mode'] == 'putt']

print(f'roll rows (filtered): {len(roll_rows)}')
print(f'putt rows (all): {len(putt_rows)}')
print()

print('=== ROLL PATH: filtered rows by (surface, target_v, spin) ===')
print(f'{"surf":<10} {"v_tgt":>6} {"spin":>5} {"n":>3} {"v_contact_med":>14} {"roll_dist_med":>14} {"bounces_med":>11} {"holes":<8}')
buckets_roll = defaultdict(list)
for r in roll_rows:
    key = (r['surface_target'], float(r['target_v_horizontal_mps']), int(r['target_spin_rpm']))
    buckets_roll[key].append(r)

for key in sorted(buckets_roll.keys()):
    surf, vt, spin = key
    samples = buckets_roll[key]
    v_contact = [float(s['actual_v_at_contact_mps']) for s in samples]
    roll = [float(s['roll_distance_m']) for s in samples]
    bounces = [int(s['bounce_count']) for s in samples]
    holes = sorted(set(s['source_hole'] for s in samples))
    print(f'{surf:<10} {vt:>6.1f} {spin:>5} {len(samples):>3} {statistics.median(v_contact):>14.3f} {statistics.median(roll):>14.3f} {statistics.median(bounces):>11.1f} {",".join(holes):<8}')

print()
print('=== putt.csv current ===')
PUTT_CFG = '/Users/cesar/Documents/GolfinRedux/Assets/Resources/Physics/putt.csv'
with open(PUTT_CFG, encoding='utf-8-sig') as f:
    print(f.read())

print('=== surfaces.csv current ===')
SURF_CFG = '/Users/cesar/Documents/GolfinRedux/Assets/Resources/Physics/surfaces.csv'
with open(SURF_CFG, encoding='utf-8-sig') as f:
    print(f.read())

print('=== PUTT PATH: all rows by (surface, target_v) ===')
buckets_putt = defaultdict(list)
for r in putt_rows:
    key = (r['surface_target'], float(r['target_v_horizontal_mps']))
    buckets_putt[key].append(r)

print(f'Putt buckets: {len(buckets_putt)}')
print(f'{"surf":<14} {"v_tgt":>6} {"n":>3} {"v_contact":>10} {"roll_dist":>10}')
for key in sorted(buckets_putt.keys()):
    surf, vt = key
    samples = buckets_putt[key]
    v_contact = [float(s['actual_v_at_contact_mps']) for s in samples]
    roll = [float(s['roll_distance_m']) for s in samples]
    print(f'{surf:<14} {vt:>6.2f} {len(samples):>3} {statistics.median(v_contact):>10.3f} {statistics.median(roll):>10.4f}')
