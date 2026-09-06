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

# The TRACKED copy is the corpus. `Docs/Diagnostics/_capture/` is gitignored, so while the dumps
# lived only there, every citation in the report pointed at a file no other machine could open —
# and the audit's whole evidence base was per-machine. This path is committed.
D = 'Docs/Reports/DesignAudit'
SCALE = [20, 30, 33, 39, 45, 48, 51, 66]
TIER2 = {'LoginScreen', 'SignUpScreen', 'SplashScreen', 'CreateUsernameScreen',
         'EmailConfirmationScreen', 'ResetPasswordScreen'}
CJK = re.compile(r'[぀-ヿ一-鿿ｦ-ﾟ]')


def corpus(locale: str) -> dict:
    out = {}
    for f in glob.glob(f'{D}/*__{locale}.json'):
        n = os.path.basename(f).replace(f'__{locale}.json', '')
        if n.startswith(('TRIPWIRE', 'UNITTEST', 'MODAL_', 'PREFAB')):
            continue
        if n.startswith('Inventory_tab'):
            continue
        # Tier-2 AUTH screens are dumped as evidence (the spec asks for them) but are NOT part of
        # the 17-screen corpus the shape counts are computed on. They carry no `MODAL_` prefix, so
        # when the modal pass was re-run they walked silently into the corpus and moved the size
        # buckets (÷1.2 139 -> 194, unexplained 274 -> 279) without touching any other number.
        # Regenerating evidence must not be able to change the audit's findings; this is the guard.
        if n in TIER2:
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

        # A line may legitimately quote a number from a DIFFERENT scope, but only if it says so on
        # that same line. The exemption is SYNTACTIC (the line declares its own scope, or marks
        # itself as a superseded figure) — never a list of blessed numbers, which is how three
        # successive checkers passed straight over real defects.
        def declares_scope(ln):
            return ('across all' in ln and 'dump' in ln) or 'has been wrong' in ln \
                or 'The first revision' in ln or 'The second said' in ln \
                or 'read 134 in one folder' in ln

        def counts_on(ln):
            ln = re.sub(r'§\s*[\d.]+', ' ', ln)
            ln = re.sub(r'÷\s*[\d.]+', ' ', ln)
            ln = re.sub(r'\b20\d\d-\d\d-\d\d\b', ' ', ln)
            return {int(x) for x in re.findall(r'\b(\d{2,})\b', ln)}

        # RULE 1 — any line naming a shape AND stating counts must state that shape's canonical
        # value. Formatting-independent: the previous version required `**N**` as its own span and
        # was blind to `**S2/S3 - 442 visible, 26 panel-sized**`.
        for label, val in (('Image.Type.Filled', n['filled']),
                           ('panel-sized', n['panel']),
                           ('visible flat fill', n['visfill']),
                           ('CJK label', n['cjk_latin'])):
            for i, ln in enumerate(lines, 1):
                if label not in ln:
                    continue
                nums = counts_on(ln)
                if not nums or val in nums:
                    continue
                (info if declares_scope(ln) else bad).append((i, label, sorted(nums), val))

        # RULE 2 — the SIZE BUCKETS. Red-team round 3 found three stale bucket citations (Q7b 144,
        # Q8 275, prose 275) that this gate could not see because it only ever checked four shape
        # labels. Each bucket is now checked wherever it is named, including inside a `§ 3.8 (N)`
        # citation, which is how Q7b and Q8 quote theirs.
        # Each bucket is matched by SEVERAL surface forms, because prose does not always use the
        # bucket's name: §3.8 says "plus 274 labels no conversion explains", which is the
        # unexplained bucket spelled out. A line-based, single-alias rule missed exactly that on
        # round 3 even while catching the same number in the Q8 table row two sections later.
        BUCKETS = (('÷1.4', n['sizes'].get('div14', 0), ('÷1.4',)),
                   ('÷1.2', n['sizes'].get('div12', 0), ('÷1.2',)),
                   ('59/66', n['sizes'].get('semi5966', 0), ('59/66',)),
                   ('unexplained', n['sizes'].get('unexplained', 0),
                    ('nexplained', 'conversion explains', 'convention explains')))
        # Matched on PARAGRAPHS, whitespace-normalised, so a claim that wraps across a line break
        # is still one claim. `units` is (first line number, text).
        units, buf, first = [], [], 1
        for i, ln in enumerate(lines, 1):
            if ln.strip() == '':
                if buf: units.append((first, ' '.join(buf))); buf = []
                first = i + 1
            else:
                if not buf: first = i
                buf.append(ln.strip())
        if buf: units.append((first, ' '.join(buf)))
        # table rows stay individually addressable so a stale cell reports its own line
        units += [(i, ln) for i, ln in enumerate(lines, 1) if ln.startswith('|')]

        for label, val, aliases in BUCKETS:
            for i, ln in units:
                if not any(a in ln for a in aliases):
                    continue
                stated = set()
                for a in aliases:
                    stated |= {int(x) for x in re.findall(rf"(\d+)\s*(?:labels?\s+)?(?:no\s+)?{re.escape(a)}", ln)}
                    stated |= {int(x) for x in re.findall(rf"{re.escape(a)}[^|]*\|\s*\**(\d+)", ln)}
                # A `§ 3.8 (N)` citation belongs to the bucket the ROW IS ABOUT — its action cell —
                # not to any bucket merely mentioned in passing. Q7 is the ÷1.4 row and its prose
                # says "not to the ÷1.2 target"; binding the citation to that mention flagged a
                # correct row.
                if ln.startswith('|'):
                    cells = [c.strip() for c in ln.strip('|').split('|')]
                    subject = cells[1] if len(cells) > 1 else ''
                else:
                    subject = ln
                if any(a in subject for a in aliases):
                    stated |= {int(x) for x in re.findall(r"§\s*3\.8\s*\((\d+)\)", ln)}
                stated.discard(0)
                for got in stated:
                    if got != val:
                        (info if declares_scope(ln) else bad).append((i, f'bucket {label}', got, val))

        # RULE 3 — FABRICATION. Every object named in the §3.5 breakdown must actually occur in the
        # corpus with exactly that count. Round 3 shipped a `GhostBar / Fill | 3` row matching ZERO
        # images in any of the 21 dumps; a sum check alone could never see that, and the malformed
        # first column also dodged the sum regex, so the gate reported "none" over both.
        truth = n['filled_by']
        rows, in35 = {}, False
        for ln in lines:
            if ln.startswith('### 3.5'): in35 = True; continue
            if in35 and ln.startswith('###'):        break
            if not in35 or not ln.startswith('|'):   continue
            cells = [c.strip() for c in ln.strip('|').split('|')]
            if len(cells) < 2 or not cells[1].strip('*').isdigit():
                continue
            name = cells[0].replace('`', '').replace('*', '').strip()
            if not name or name.lower() in ('object', 'count'):
                continue
            rows[name] = int(cells[1].strip('*'))
        if not rows:
            bad.append(('FAB', '§3.5 breakdown table not parsed - check is blind', 0, n['filled']))
        else:
            for name, cnt in rows.items():
                if name not in truth:
                    bad.append(('FAB', f'§3.5 row `{name}` matches NO image in the corpus', cnt, 0))
                elif truth[name] != cnt:
                    bad.append(('FAB', f'§3.5 row `{name}`', cnt, truth[name]))
            if sum(rows.values()) != n['filled']:
                bad.append(('SUM', f'§3.5 rows {rows}', sum(rows.values()), n['filled']))

        for i, lab, got, want in info:
            print(f"  scope-declared (OK)  line {i}: {lab} states {got} (corpus = {want})")
        for b in bad:
            print(f"  STALE  line {b[0]}: {b[1]} states {b[2]}, corpus = {b[3]}")
        print("\ncontradictions vs this corpus:", len(bad) if bad else "none")
        return 1 if bad else 0
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
