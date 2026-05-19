#!/usr/bin/env python3
"""Compare pre-tune (20260518_122845) vs post-tune (20260519_061402) for Phase B Stage 2."""
import csv, statistics
from collections import defaultdict

PRE = '/Users/cesar/Documents/GolfinRedux/Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv'
POST = '/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/captures/20260519_061402/sweep.csv'

def load(path):
    with open(path, encoding='utf-8-sig') as f:
        return list(csv.DictReader(f))

pre = load(PRE)
post = load(POST)

ROLL_SURFACES = {'Fairway', 'Green', 'Sand'}
ROLL_HOLES = {'1', '9'}

def filter_roll(rows):
    return [r for r in rows if r['mode']=='roll' and r['surface_target'] in ROLL_SURFACES and r['end_surface']==r['surface_target'] and r['source_hole'] in ROLL_HOLES]

def by_bucket(rows):
    b = defaultdict(list)
    for r in rows:
        b[(r['surface_target'], float(r['target_v_horizontal_mps']))].append(r)
    return b

pre_r = filter_roll(pre)
post_r = filter_roll(post)
print(f'pre filtered: {len(pre_r)}, post filtered: {len(post_r)}')

# Sand: H9 only per Cesar caveat
def by_bucket_h9(rows):
    b = defaultdict(list)
    for r in rows:
        if r['surface_target']=='Sand' and r['source_hole']=='9':
            b[float(r['target_v_horizontal_mps'])].append(r)
        elif r['surface_target']!='Sand':
            b[(r['surface_target'], float(r['target_v_horizontal_mps']))].append(r)
    return b

pre_b = by_bucket(pre_r)
post_b = by_bucket(post_r)

# Sand H9-only
pre_sand_h9 = defaultdict(list)
post_sand_h9 = defaultdict(list)
for r in pre_r:
    if r['surface_target']=='Sand' and r['source_hole']=='9':
        pre_sand_h9[float(r['target_v_horizontal_mps'])].append(r)
for r in post_r:
    if r['surface_target']=='Sand' and r['source_hole']=='9':
        post_sand_h9[float(r['target_v_horizontal_mps'])].append(r)

print()
print('=== PRE vs POST roll medians at v=20 and v=25 ===')
print(f'{"surf":<10} {"v":>4} {"pre_med":>9} {"post_med":>9} {"delta":>8} {"target":>8} {"pass?":<8}')

# Targets from SPEC
ACCEPT = {
    ('Fairway', 25): (17, 23),
    ('Green', 25): (2.5, 4.0),
    ('Sand', 25): (1.0, 1.5),
    # not in SPEC but useful sanity at v=20:
    ('Fairway', 20): (10, 18),
    ('Green', 20): (1.5, 3.0),
    ('Sand', 20): (0.6, 1.2),
}

for surf in ['Fairway', 'Green', 'Sand']:
    for v in [20.0, 25.0]:
        if surf == 'Sand':
            pre_samples = pre_sand_h9.get(v, [])
            post_samples = post_sand_h9.get(v, [])
        else:
            pre_samples = pre_b.get((surf, v), [])
            post_samples = post_b.get((surf, v), [])
        if not pre_samples or not post_samples:
            print(f'{surf:<10} {v:>4.0f} (no data)')
            continue
        pre_d = statistics.median([float(s['roll_distance_m']) for s in pre_samples])
        post_d = statistics.median([float(s['roll_distance_m']) for s in post_samples])
        delta_pct = (post_d - pre_d) / pre_d * 100
        target_lo, target_hi = ACCEPT.get((surf, int(v)), (None, None))
        if target_lo is None:
            band_str = 'no-target'
            passed = '—'
        else:
            passed = 'PASS' if target_lo <= post_d <= target_hi else 'FAIL'
            band_str = f'{target_lo}-{target_hi}'
        print(f'{surf:<10} {v:>4.0f} {pre_d:>9.3f} {post_d:>9.3f} {delta_pct:>+7.1f}% {band_str:>8} {passed:<8}')

# Stimpmeter check
print()
print('=== Stimpmeter (Green putt v=1.80) ===')
pre_putt = [float(r['roll_distance_m']) for r in pre if r['mode']=='putt' and r['surface_target']=='Green' and abs(float(r['target_v_horizontal_mps'])-1.80)<0.01]
post_putt = [float(r['roll_distance_m']) for r in post if r['mode']=='putt' and r['surface_target']=='Green' and abs(float(r['target_v_horizontal_mps'])-1.80)<0.01]
print(f'  pre: median={statistics.median(pre_putt):.4f}, n={len(pre_putt)}')
print(f'  post: median={statistics.median(post_putt):.4f}, n={len(post_putt)}')

# Full pre vs post for all surfaces at all speeds (whole picture)
print()
print('=== FULL PRE/POST CURVE (filtered rows only) ===')
print(f'{"surf":<10} {"v":>4} {"pre":>8} {"post":>8} {"delta%":>9}')
for surf in ['Fairway', 'Green', 'Sand']:
    for v in [3.0, 6.0, 9.0, 12.0, 15.0, 20.0, 25.0]:
        if surf=='Sand':
            ps = pre_sand_h9.get(v, [])
            qs = post_sand_h9.get(v, [])
        else:
            ps = pre_b.get((surf,v), [])
            qs = post_b.get((surf,v), [])
        if not ps or not qs:
            continue
        p = statistics.median([float(s['roll_distance_m']) for s in ps])
        q = statistics.median([float(s['roll_distance_m']) for s in qs])
        print(f'{surf:<10} {v:>4.0f} {p:>8.3f} {q:>8.3f} {(q-p)/p*100:>+8.1f}%')
