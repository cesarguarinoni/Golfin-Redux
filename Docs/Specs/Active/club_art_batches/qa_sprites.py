"""
Sprite QA for the club art pipeline. RUN THIS ON EVERY SPRITE BEFORE DELIVERY.

    python3 qa_sprites.py <dir-or-files...>          # e.g. python3 qa_sprites.py Controls Portraits Full

Catches the failure that shipped TWICE on 2026-08-20: a club with TWO shafts.
A correct club sprite has AT MOST ONE narrow limb crossing the frame edge. The head itself may
touch an edge, but that reads as a WIDE crossing and is ignored.

Also checks exact target size, corner alpha, and duplicate files.
Requires only PIL + numpy (no scipy).
"""
import sys, os, glob, hashlib
from PIL import Image
import numpy as np

TARGETS = {'S_Menu': (264, 411), 'S_Controls': (1156, 649)}
FULL = (537, 900)
NARROW_FRAC = 0.18          # limb narrower than this fraction of the edge = a shaft

def target_size(name):
    for pre, sz in TARGETS.items():
        if name.startswith(pre):
            return sz
    return FULL

def runs(line, min_len=4):
    """(length, start) of contiguous True runs in a 1-D boolean array."""
    out, n = [], 0
    for i, v in enumerate(line):
        if v:
            n += 1
        elif n:
            out.append((n, i - n)); n = 0
    if n:
        out.append((n, len(line) - n))
    return [(r, s) for r, s in out if r >= min_len]

def narrow_edge_crossings(op):
    h, w = op.shape
    found = []
    for edge, line, span in (('top', op[0, :], w), ('bottom', op[-1, :], w),
                             ('left', op[:, 0], h), ('right', op[:, -1], h)):
        for L, s in runs(line):
            if L / span < NARROW_FRAC:
                found.append((edge, L / span, s == 0, s + L == span))
    # A single limb crossing AT a corner cuts two edges; count it once.
    # (top-start+left-start = TL, top-end+right-start = TR,
    #  bottom-start+left-end = BL, bottom-end+right-end = BR)
    def drop_pair(e1, at_end1, e2, at_end2):
        a = next((f for f in found if f[0] == e1 and f[3 if at_end1 else 2]), None)
        b = next((f for f in found if f[0] == e2 and f[3 if at_end2 else 2]), None)
        if a and b:
            found.remove(b)
    drop_pair('top', False, 'left', False)
    drop_pair('top', True, 'right', False)
    drop_pair('bottom', False, 'left', True)
    drop_pair('bottom', True, 'right', True)
    return [(e, f) for e, f, _, _ in found]

def detached_fragments(op, min_rel=0.02):
    """Connected-component labelling with a simple union-find (no scipy).
    Returns sizes (relative to the largest blob) of any extra blobs above min_rel."""
    h, w = op.shape
    parent = {}
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb: parent[rb] = ra
    lbl = np.zeros((h, w), dtype=np.int32)
    nxt = 1
    for y in range(h):
        row = op[y]
        for x in np.flatnonzero(row):
            up = lbl[y-1, x] if y else 0
            le = lbl[y, x-1] if x else 0
            if up and le:
                lbl[y, x] = min(up, le); union(min(up, le), max(up, le))
            elif up or le:
                lbl[y, x] = up or le
            else:
                lbl[y, x] = nxt; parent[nxt] = nxt; nxt += 1
    if nxt <= 1: return []
    roots = {}
    for y in range(h):
        for x in np.flatnonzero(lbl[y]):
            r = find(int(lbl[y, x])); roots[r] = roots.get(r, 0) + 1
    sizes = sorted(roots.values(), reverse=True)
    if len(sizes) < 2: return []
    return [s / sizes[0] for s in sizes[1:] if s / sizes[0] > min_rel]

def check(path):
    name = os.path.basename(path)
    im = Image.open(path).convert('RGBA')
    a = np.array(im)
    op = a[..., 3] > 128
    problems, notes = [], []

    tgt = target_size(name)
    if im.size != tgt:
        # legacy hand-made source art lives at 2148x3600; that is Cesar's, not pipeline output
        if im.size == (2148, 3600):
            notes.append("legacy source-art size %s (not pipeline output)" % (im.size,))
        else:
            problems.append("size %s != %s" % (im.size, tgt))
    if name.startswith(('S_Menu', 'S_Controls')) and int(a[0, 0, 3]) != 0:
        # a narrow shaft crossing exactly at the top-left corner legitimately occupies it
        tl_shaft = op[0, 0] and any(r[1] == 0 for r in runs(op[0, :])) and any(r[1] == 0 for r in runs(op[:, 0]))
        if not tl_shaft:
            problems.append("corner alpha %d != 0 (background not cut out)" % int(a[0, 0, 3]))

    # detached fragments: flood-fill label without scipy
    frag = detached_fragments(op)
    if frag:
        problems.append("DETACHED FRAGMENT(S): %d extra blob(s), largest %d%% of body - severed head?"
                        % (len(frag), int(100*frag[0])))

    shafts = narrow_edge_crossings(op)
    if len(shafts) > 1:
        where = ', '.join("%s:%.2f" % (e, f) for e, f in shafts)
        problems.append("%d SHAFTS crossing the frame [%s] - does this club have two shafts?"
                        % (len(shafts), where))
    return problems, notes

def main(args):
    paths = []
    for a in args:
        paths += sorted(glob.glob(os.path.join(a, '*.png'))) if os.path.isdir(a) else [a]
    paths = [p for p in paths if not p.endswith('.meta')]
    seen, bad = {}, 0
    for p in paths:
        probs, notes = check(p)
        d = hashlib.md5(open(p, 'rb').read()).hexdigest()
        if d in seen:
            probs.append("DUPLICATE of %s" % os.path.basename(seen[d]))
        seen[d] = p
        if probs:
            bad += 1
            print("FAIL %s" % os.path.basename(p))
            for x in probs:
                print("       - %s" % x)
        elif notes:
            print("note %s" % os.path.basename(p))
            for x in notes:
                print("       - %s" % x)
    print("\n%d/%d clean" % (len(paths) - bad, len(paths)))
    return 1 if bad else 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:] or ['.']))
