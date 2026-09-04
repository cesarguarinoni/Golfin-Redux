#!/usr/bin/env python3
"""build_size_diet Phase 2 — INDEPENDENT verification that zones.bytes carries the same data.

Reads the committed zones.json out of git and the converted zones.bytes off disk, parses BOTH
as JSON, and compares the parsed structures. Comparing parsed JSON rather than text is the
point: it proves the minifier changed only whitespace, in a second implementation, without
trusting the C# ZoneDataDiff that gated the conversion.

  python3 Docs/Scripts/verify_zones_gzip.py [git-ref]
"""
import glob, gzip, json, subprocess, sys, os

def main():
    ref = sys.argv[1] if len(sys.argv) > 1 else "5d8bd6f83"
    paths = sorted(glob.glob("Assets/Resources/HoleData/*/*/zones.bytes"))
    print(f"# Independent zones.json (at {ref}) vs zones.bytes (working tree) verification")
    print(f"# Decoder: Docs/Scripts/verify_zones_gzip.py — no Unity, no C#.")
    print()
    print(f"{'hole':<34} {'json B':>10} {'gzip B':>9} {'x':>5}  parsed-JSON equality")
    ok = True
    tb = ta = 0
    for p in paths:
        rel = p.replace("Assets/Resources/HoleData/", "")
        oldp = p[:-len(".bytes")] + ".json"
        old = subprocess.run(["git", "show", f"{ref}:{oldp}"], capture_output=True)
        if old.returncode != 0:
            print(f"{rel:<34} {'-':>10} {'-':>9} {'-':>5}  *** no {oldp} at {ref}")
            ok = False
            continue
        a = json.loads(old.stdout)
        b = json.loads(gzip.decompress(open(p, "rb").read()))
        same = a == b
        ok &= same
        tb += len(old.stdout); ta += os.path.getsize(p)
        print(f"{rel:<34} {len(old.stdout):>10} {os.path.getsize(p):>9} "
              f"{len(old.stdout)/os.path.getsize(p):>5.1f}  {'IDENTICAL' if same else '*** DIFFERS ***'}")
    mib = 1024 * 1024
    print(f"{'TOTAL':<34} {tb:>10} {ta:>9} {tb/ta:>5.1f}  ({tb/mib:.1f} MiB -> {ta/mib:.1f} MiB)")
    print()
    print("RESULT:", "every hole's parsed zone data is identical." if ok else "*** MISMATCH ***")
    return 0 if ok else 1

sys.exit(main())
