#!/usr/bin/env python3
"""build_size_diet Phase 2 — INDEPENDENT verification that GHM2 is lossless.

Deliberately a second implementation, in another language, reading the ORIGINAL GHM1 bytes
straight out of git rather than off disk (the converter overwrote the working tree). If this
Python decoder and the C# one in HeightmapLoader agree on every one of 8.4 million samples per
hole, the agreement is evidence; a tool checking its own round trip is not.

  python3 Docs/Scripts/verify_ghm2.py [git-ref]     (default: the commit before the conversion)
"""
import array, hashlib, struct, subprocess, sys, zlib, glob, os

def parse(buf):
    magic = buf[:4]
    ver, res = struct.unpack_from('<ii', buf, 4)
    sx, sz, px, py, pz = struct.unpack_from('<5f', buf, 12)
    fmt, = struct.unpack_from('<i', buf, 32)
    n = res * res
    if magic == b'GHM1':
        assert (ver, fmt) == (1, 1), f"GHM1 header says version={ver} format={fmt}"
        h = array.array('i'); h.frombytes(buf[36:36 + n * 4])
    elif magic == b'GHM2':
        assert (ver, fmt) == (2, 2), f"GHM2 header says version={ver} format={fmt}"
        raw = zlib.decompressobj(-15).decompress(buf[36:])
        assert len(raw) == n * 4, f"GHM2 payload is {len(raw)} B, expected {n*4}"
        d = array.array('i'); d.frombytes(raw)
        h = d
        for y in range(res):
            b = y * res
            for x in range(1, res):
                h[b + x] += h[b + x - 1]
    else:
        raise AssertionError(f"bad magic {magic!r}")
    return dict(res=res, sx=sx, sz=sz, px=px, py=py, pz=pz, h=h)

def sha(h):
    return hashlib.sha256(h.tobytes()).hexdigest()

def main():
    ref = sys.argv[1] if len(sys.argv) > 1 else "5d8bd6f83"
    paths = sorted(glob.glob("Assets/Resources/HoleData/*/*/heightmap.bytes"))
    print(f"# Independent GHM1 (at {ref}) vs GHM2 (working tree) verification")
    print(f"# Decoder: Docs/Scripts/verify_ghm2.py — no Unity, no C#.")
    print()
    print(f"{'hole':<34} {'GHM1 B':>11} {'GHM2 B':>10} {'x':>5}  {'sha256(decoded int32[]) GHM1 == GHM2':<70}")
    ok = True
    tb = ta = 0
    for p in paths:
        rel = p.replace("Assets/Resources/HoleData/", "")
        old = subprocess.run(["git", "show", f"{ref}:{p}"], capture_output=True).stdout
        new = open(p, "rb").read()
        a, b = parse(old), parse(new)
        same = (a["h"] == b["h"]
                and a["res"] == b["res"]
                and (a["sx"], a["sz"], a["px"], a["py"], a["pz"])
                    == (b["sx"], b["sz"], b["px"], b["py"], b["pz"]))
        sa, sb = sha(a["h"]), sha(b["h"])
        tb += len(old); ta += len(new)
        ok &= same and sa == sb
        print(f"{rel:<34} {len(old):>11} {len(new):>10} {len(old)/len(new):>5.1f}  {sa}  {'MATCH' if same and sa==sb else '*** DIFFERS ***'}")
    mib = 1024 * 1024
    print(f"{'TOTAL':<34} {tb:>11} {ta:>10} {tb/ta:>5.1f}  ({tb/mib:.1f} MiB -> {ta/mib:.1f} MiB)")
    print()
    print("RESULT:", "every hole decodes to bit-identical Q16.16 samples." if ok else "*** MISMATCH ***")
    return 0 if ok else 1

sys.exit(main())
