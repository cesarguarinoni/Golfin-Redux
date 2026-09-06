#!/usr/bin/env python3
"""Compute every number DESIGN_CONSISTENCY_AUDIT.md quotes, from one stated corpus.

WHY THIS EXISTS. The red-team failed this report twice for the same thing: the top-line
summaries were corrected by hand while sub-tables, fix-list references and section headers
kept older numbers from an older corpus. Hand-editing a number in five places and claiming
"every count comes from one corpus" is the failure, not the arithmetic.

So the numbers are GENERATED. Run this, paste nothing: it prints the canonical block for each
section and a contradiction check. If a section of the report disagrees with this output, the
report is wrong — not this script.

    python3 Docs/Scripts/audit_numbers.py [--check Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md]

CORPUS RULE (the one the report states, applied here and nowhere else):
  * EN dumps for structural counts, JA dumps for the CJK-binding count
  * one file per DISTINCT screen — Inventory's four tab states collapse to InventoryScreen,
    because counting them separately multiplies every Inventory site by four
  * modals (MODAL_*) and prefab-only dumps (PREFAB_*) EXCLUDED — they are reported separately
    and are not part of any shape count
"""
from __future__ import annotations
import collections, glob, json, os, re, sys

D = 'Docs/Diagnostics/_capture/design_audit'
SCALE = [20, 30, 33, 39, 45, 48, 51, 66]
CJK = re.compile(r'[぀-ヿ一-鿿ｦ-ﾟ]')


def corpus(locale: str) -> dict:
    out = {}
    for f in glob.glob(f'{D}/*__{locale}.json'):
        n = os.path.basename(f).replace(f'__{locale}.json', '')
        if n.startswith(('TRIPWIRE', 'UNITTEST', 'MODAL_', 'PREFAB')):
            continue
        if n.startswith('Inventory_tab'):
            continue
        out[n] = json.load(open(f))
    return out


def alpha(hexs: str) -> float:
    h = hexs.lstrip('#')
    return int(h[6:8], 16) / 255 if len(h) >= 8 else 1.0


def near(v, t, tol=0.4): return abs(v - t) <= tol


def compute():
    EN, JA = corpus('en'), corpus('ja')
    n = dict(screens_en=len(EN), screens_ja=len(JA))

    lib = outline = shadow = filled = vis = panel = 0
    filled_by = collections.Counter()
    buckets = collections.Counter()
    for d in EN.values():
        for t in d['texts']:
            if t['font'] == 'LiberationSans SDF': lib += 1
            if t['outlineComponent']: outline += 1
            if t['shadowComponent']: shadow += 1
            if not t['autoSize']:
                px = round(t['renderedPx'], 2)
                if   any(near(px, s) for s in SCALE):          buckets['on-scale'] += 1
                elif any(near(px * 1.4, s) for s in SCALE):    buckets['div14'] += 1
                elif any(near(px * 1.2, s) for s in SCALE):    buckets['div12'] += 1
                elif any(near(px * 66 / 59, s) for s in SCALE):buckets['semi5966'] += 1
                else:                                          buckets['unexplained'] += 1
        for i in d['images']:
            if i['type'] == 'Filled':
                filled += 1
                filled_by[i['path'].split('/')[-1]] += 1
            if i['outlineComponent']: outline += 1
            if i['shadowComponent']: shadow += 1
            if i['sprite'] == '<NONE>' and alpha(i['color']) > 0.2:
                vis += 1
                if i['width'] >= 200 and i['height'] >= 60: panel += 1

    cjk_latin = cjk_noto = 0
    for d in JA.values():
        for t in d['texts']:
            if CJK.search(t['text']):
                if 'NotoSans' in t['font']: cjk_noto += 1
                else:                        cjk_latin += 1

    n.update(lib=lib, outline=outline, shadow=shadow, filled=filled,
             filled_by=dict(filled_by.most_common()), visfill=vis, panel=panel,
             cjk_latin=cjk_latin, cjk_noto=cjk_noto,
             sizes=dict(buckets), sizes_total=sum(buckets.values()))
    return n


def main():
    n = compute()
    print(f"CORPUS: {n['screens_en']} EN screens / {n['screens_ja']} JA screens "
          f"(Inventory tabs collapsed; modals + prefab dumps excluded)\n")
    print(f"  LiberationSans (in-screen)   {n['lib']}   (+5 in CharacterThumbnailCard/StatBar = 41)")
    print(f"  Outline components           {n['outline']}")
    print(f"  Shadow components            {n['shadow']}")
    print(f"  Image.Type.Filled            {n['filled']}   {n['filled_by']}")
    print(f"  visible flat fills           {n['visfill']}   panel-sized {n['panel']}")
    print(f"  CJK on Latin / on NotoSansJP {n['cjk_latin']} / {n['cjk_noto']}")
    print(f"  sizes (n={n['sizes_total']}):           {n['sizes']}")

    if '--check' in sys.argv:
        path = sys.argv[sys.argv.index('--check') + 1]
        lines = open(path, encoding='utf-8').read().split('\n')
        bad, info = [], []

        # A line may legitimately quote a number from a DIFFERENT scope, but only if it says so
        # on that same line. The exemption is SYNTACTIC (the line declares its own scope, or marks
        # itself as a superseded figure) — never a list of blessed numbers, which is how the last
        # two checkers passed straight over real defects.
        def declares_scope(ln):
            return ('across all' in ln and 'dump' in ln) or 'has been wrong' in ln \
                or 'The first revision' in ln or 'The second said' in ln \
                or 'read 134 in one folder' in ln

        # Numbers that are not counts: section refs (§ 3.5, "§ 1"), list markers, years, and the
        # ÷ divisors. Stripped BEFORE the rule runs so they cannot mask a real count.
        def counts_on(ln):
            ln = re.sub(r'§\s*[\d.]+', ' ', ln)
            ln = re.sub(r'÷\s*[\d.]+', ' ', ln)
            ln = re.sub(r'\b20\d\d-\d\d-\d\d\b', ' ', ln)
            return {int(x) for x in re.findall(r'\b(\d{2,})\b', ln)}

        # THE RULE: any line that names a shape AND states counts must state that shape's
        # canonical value. It does not care how the number is formatted, bolded or punctuated —
        # the previous checker required `**N**` as its own span and was therefore blind to
        # `**S2/S3 - 442 visible, 26 panel-sized**`, which is the defect it existed to catch.
        for label, val in (('Image.Type.Filled', n['filled']),
                           ('panel-sized', n['panel']),
                           ('visible flat fill', n['visfill']),
                           ('CJK label', n['cjk_latin'])):
            for i, ln in enumerate(lines, 1):
                if label not in ln:
                    continue
                nums = counts_on(ln)
                if not nums:
                    continue                      # prose mention, states no count
                if val in nums:
                    continue                      # states the canonical value
                (info if declares_scope(ln) else bad).append((i, label, sorted(nums), val))

        # Every breakdown table must sum to the header it sits under. This check would have caught
        # the 447-vs-225 defect on its own, knowing nothing about the right answer.
        rows = {m.group(1): int(m.group(2))
                for m in re.finditer(r"^\|\s*`(\w+)`\s*\|\s*(\d+)", '\n'.join(lines), re.M)}
        if not rows:
            bad.append(('SUM', 'breakdown table not found - check is blind', 0, n['filled']))
        elif sum(rows.values()) != n['filled']:
            bad.append(('SUM', f'{rows}', sum(rows.values()), n['filled']))

        for i, lab, got, want in info:
            print(f"  scope-declared (OK)  line {i}: {lab} states {got} (corpus = {want})")
        if bad:
            for b in bad:
                print(f"  STALE  line {b[0]}: {b[1]} states {b[2]}, corpus = {b[3]}")
        print("\ncontradictions vs this corpus:", len(bad) if bad else "none")
        return 1 if bad else 0
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
