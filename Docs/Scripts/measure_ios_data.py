#!/usr/bin/env python3
"""Per-file raw + deflate sizes for a built iOS Data folder (build_size_diet).

Raw bytes are the install contribution; the deflate size models the .ipa's per-entry
compressed size (the .ipa is a zip with per-file deflate, so compressing each file on its own
is the right model — a single tar.gz would cheat by compressing across files).

The non-Data part of the Payload (UnityFramework + Assets.car + plists + icons) does not change
with the asset diet, so it is added back as a measured constant, read off the shipped
Builds/ipa/Golfin.ipa on 2026-09-04:  111,741,942 B raw / 35,515,812 B compressed.
"""
import os, sys, zlib

NON_DATA_RAW = 111_741_942
NON_DATA_COMP = 35_515_812
MB = 1024 * 1024

def deflate_size(path):
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    n = 0
    with open(path, "rb") as f:
        while True:
            b = f.read(1 << 20)
            if not b:
                break
            n += len(c.compress(b))
    return n + len(c.flush())

def main():
    label, data = sys.argv[1], sys.argv[2]
    rows = []
    for dirpath, _dirs, files in os.walk(data):
        for fn in files:
            p = os.path.join(dirpath, fn)
            if os.path.islink(p):
                continue
            rows.append((os.path.getsize(p), deflate_size(p), os.path.relpath(p, data)))
    raw = sum(r[0] for r in rows)
    comp = sum(r[1] for r in rows)
    print(f"# reference/data_{label}.txt — build_size_diet")
    print(f"# Data folder: {data}   ({len(rows)} files)")
    print("# Columns: raw bytes (INSTALL contribution) | deflate bytes (Payload-compressed model) | path")
    print(f"# Non-Data Payload constant added below: {NON_DATA_RAW:,} B raw / {NON_DATA_COMP:,} B compressed")
    print()
    print(f"DATA raw           {raw:,} B = {raw/MB:8.1f} MiB")
    print(f"DATA compressed    {comp:,} B = {comp/MB:8.1f} MiB")
    print()
    print(f"INSTALL       = Data raw  + non-Data raw  = {(raw+NON_DATA_RAW)/MB:8.1f} MiB   (gate <= 1024 MiB)")
    print(f"PAYLOAD-COMP  = Data comp + non-Data comp = {(comp+NON_DATA_COMP)/MB:8.1f} MiB   (gate <=  350 MiB)")
    print()
    print("== Per-file, sorted by raw size ==")
    for r, c, n in sorted(rows, key=lambda r: -r[0]):
        print(f"{r:12d} {c:12d}  {n}")

main()
