#!/usr/bin/env python3
"""
GOLFIN Visual QA Pipeline
Compares Unity screenshots against Figma reference exports.

Usage:
  python3 visual_qa.py --export-figma          # Export Figma screens as PNGs
  python3 visual_qa.py --compare               # Compare Unity vs Figma screenshots
  python3 visual_qa.py --compare --screen Loading  # Compare specific screen
  python3 visual_qa.py --full                   # Export + Compare all

Requires:
  - FIGMA_TOKEN env var or --token flag
  - Unity screenshots in QA/Screenshots/Unity/
  - pip install requests Pillow
"""

import os, sys, json, argparse, time, hashlib
from pathlib import Path
from datetime import datetime

try:
    import requests
except ImportError:
    print("pip install requests"); sys.exit(1)

# ═══ CONFIG ═══
FIGMA_FILE_KEY = "5gEAHjl6xAtW8iYY7NMvWd"
FIGMA_API = "https://api.figma.com/v1"

# Screen name → Figma node ID mapping
SCREEN_MAP = {
    "LogoScreen":    {"page": "Logo",           "node_id": "2622:843"},
    "SplashScreen":  {"page": "Splash Screen",  "node_id": "2032:327"},
    "LoadingScreen": {"page": "Loading",        "node_id": "4096:1181"},
}

QA_DIR = Path(__file__).parent
FIGMA_DIR = QA_DIR / "Screenshots" / "Figma"
UNITY_DIR = QA_DIR / "Screenshots" / "Unity"
REPORTS_DIR = QA_DIR / "Reports"
CACHE_DIR = QA_DIR / ".cache"


def get_token(args):
    token = getattr(args, 'token', None) or os.environ.get("FIGMA_TOKEN")
    if not token:
        token_file = QA_DIR / ".figma_token"
        if token_file.exists():
            token = token_file.read_text().strip()
    if not token:
        print("ERROR: No Figma token. Set FIGMA_TOKEN env var, use --token, or create QA/.figma_token")
        sys.exit(1)
    return token


# ═══════════════════════════════════════════════════════════════
# FIGMA EXPORT
# ═══════════════════════════════════════════════════════════════

def export_figma_screens(token, screens=None, scale=2):
    """Export Figma screens as PNGs using the Image API."""
    FIGMA_DIR.mkdir(parents=True, exist_ok=True)
    headers = {"X-Figma-Token": token}

    if screens:
        targets = {k: v for k, v in SCREEN_MAP.items() if k in screens}
    else:
        targets = SCREEN_MAP

    if not targets:
        print(f"No matching screens. Available: {list(SCREEN_MAP.keys())}")
        return {}

    # First, get component node IDs for each page
    # We need the actual component nodes, not just the page
    print(f"Fetching file structure...")
    r = requests.get(f"{FIGMA_API}/files/{FIGMA_FILE_KEY}?depth=2", headers=headers)
    if r.status_code == 429:
        wait = int(r.headers.get('Retry-After', 30))
        print(f"Rate limited. Waiting {wait}s...")
        time.sleep(wait)
        r = requests.get(f"{FIGMA_API}/files/{FIGMA_FILE_KEY}?depth=2", headers=headers)
    r.raise_for_status()
    file_data = r.json()

    # Find first component in each target page
    node_ids = {}
    for page in file_data["document"]["children"]:
        for screen_name, info in targets.items():
            if page["name"] == info["page"]:
                # Use first component child if available, otherwise the page itself
                children = page.get("children", [])
                if children:
                    # Pick first COMPONENT or FRAME
                    node_ids[screen_name] = children[0]["id"]
                else:
                    node_ids[screen_name] = page["id"]

    if not node_ids:
        print("Could not find target pages in Figma file")
        return {}

    # Request image exports
    ids_str = ",".join(node_ids.values())
    print(f"Exporting {len(node_ids)} screens at {scale}x...")

    r = requests.get(
        f"{FIGMA_API}/images/{FIGMA_FILE_KEY}",
        headers=headers,
        params={"ids": ids_str, "scale": scale, "format": "png"}
    )
    if r.status_code == 429:
        wait = int(r.headers.get('Retry-After', 30))
        print(f"Rate limited. Waiting {wait}s...")
        time.sleep(wait)
        r = requests.get(
            f"{FIGMA_API}/images/{FIGMA_FILE_KEY}",
            headers=headers,
            params={"ids": ids_str, "scale": scale, "format": "png"}
        )
    r.raise_for_status()
    images = r.json().get("images", {})

    exported = {}
    for screen_name, node_id in node_ids.items():
        url = images.get(node_id)
        if not url:
            print(f"  ⚠️  No image for {screen_name} (node {node_id})")
            continue

        # Download the image
        img_r = requests.get(url)
        img_r.raise_for_status()
        out_path = FIGMA_DIR / f"{screen_name}.png"
        out_path.write_bytes(img_r.content)
        size_kb = len(img_r.content) / 1024
        print(f"  ✅ {screen_name}.png ({size_kb:.0f} KB)")
        exported[screen_name] = out_path

    # Save metadata
    meta = {
        "exported_at": datetime.now().isoformat(),
        "file_key": FIGMA_FILE_KEY,
        "scale": scale,
        "screens": {k: str(v) for k, v in exported.items()}
    }
    (FIGMA_DIR / "export_meta.json").write_text(json.dumps(meta, indent=2))

    return exported


# ═══════════════════════════════════════════════════════════════
# PIXEL COMPARISON
# ═══════════════════════════════════════════════════════════════

def pixel_diff(img1_path, img2_path):
    """Basic pixel-level comparison between two images. Returns diff stats."""
    try:
        from PIL import Image
        import numpy as np
    except ImportError:
        return {"error": "pip install Pillow numpy for pixel comparison"}

    img1 = Image.open(img1_path).convert("RGBA")
    img2 = Image.open(img2_path).convert("RGBA")

    # Resize to match if different dimensions
    if img1.size != img2.size:
        # Scale smaller to match larger
        target = max(img1.size, img2.size)
        img1 = img1.resize(target, Image.LANCZOS)
        img2 = img2.resize(target, Image.LANCZOS)

    arr1 = np.array(img1, dtype=np.float32)
    arr2 = np.array(img2, dtype=np.float32)

    # Per-pixel difference
    diff = np.abs(arr1 - arr2)
    mean_diff = diff.mean()
    max_diff = diff.max()

    # Structural similarity (simple version)
    # Count pixels with >threshold difference
    threshold = 30  # out of 255
    diff_mask = diff.mean(axis=2) > threshold
    diff_percent = (diff_mask.sum() / diff_mask.size) * 100

    # Generate diff image
    diff_img = Image.fromarray(np.uint8(np.clip(diff * 3, 0, 255)))
    diff_path = REPORTS_DIR / f"diff_{Path(img1_path).stem}.png"
    diff_img.save(diff_path)

    # Find bounding boxes of diff regions
    regions = []
    if diff_percent > 0.5:
        # Simple region detection: find rows/cols with differences
        row_diff = diff_mask.any(axis=1)
        col_diff = diff_mask.any(axis=0)
        if row_diff.any():
            y_start = int(np.argmax(row_diff))
            y_end = int(len(row_diff) - np.argmax(row_diff[::-1]))
            x_start = int(np.argmax(col_diff))
            x_end = int(len(col_diff) - np.argmax(col_diff[::-1]))
            regions.append({
                "y_start": y_start, "y_end": y_end,
                "x_start": x_start, "x_end": x_end,
                "area_percent": round(diff_percent, 2)
            })

    return {
        "mean_diff": round(float(mean_diff), 2),
        "max_diff": round(float(max_diff), 2),
        "diff_percent": round(diff_percent, 2),
        "diff_image": str(diff_path),
        "resolution": list(img1.size),
        "regions": regions,
        "match_score": round(100 - diff_percent, 2)
    }


# ═══════════════════════════════════════════════════════════════
# COMPARISON REPORT
# ═══════════════════════════════════════════════════════════════

def compare_screens(screens=None):
    """Compare Unity screenshots against Figma exports."""
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)

    if screens:
        targets = screens
    else:
        targets = list(SCREEN_MAP.keys())

    results = {}
    for screen_name in targets:
        unity_path = UNITY_DIR / f"{screen_name}.png"
        figma_path = FIGMA_DIR / f"{screen_name}.png"

        if not unity_path.exists():
            print(f"  ⚠️  {screen_name}: No Unity screenshot (run Tools → QA → Capture All Screens)")
            results[screen_name] = {"status": "missing_unity"}
            continue
        if not figma_path.exists():
            print(f"  ⚠️  {screen_name}: No Figma export (run --export-figma first)")
            results[screen_name] = {"status": "missing_figma"}
            continue

        print(f"  Comparing {screen_name}...")

        # Pixel diff
        diff = pixel_diff(unity_path, figma_path)
        diff["unity_path"] = str(unity_path)
        diff["figma_path"] = str(figma_path)

        # Hash for change detection
        diff["unity_hash"] = hashlib.md5(unity_path.read_bytes()).hexdigest()[:12]
        diff["figma_hash"] = hashlib.md5(figma_path.read_bytes()).hexdigest()[:12]

        results[screen_name] = diff

        # Print summary
        score = diff.get("match_score", 0)
        emoji = "✅" if score > 95 else "🟡" if score > 80 else "🔴"
        print(f"  {emoji} {screen_name}: {score}% match ({diff.get('diff_percent',0)}% pixels differ)")

    # Save report
    report = {
        "timestamp": datetime.now().isoformat(),
        "screens": results,
        "summary": {
            "total": len(results),
            "passing": sum(1 for r in results.values() if r.get("match_score", 0) > 95),
            "warning": sum(1 for r in results.values() if 80 < r.get("match_score", 0) <= 95),
            "failing": sum(1 for r in results.values() if r.get("match_score", 0) <= 80),
            "missing": sum(1 for r in results.values() if "status" in r),
        }
    }

    report_path = REPORTS_DIR / f"qa_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    report_path.write_text(json.dumps(report, indent=2))
    print(f"\n📊 Report saved: {report_path}")

    # Also save as latest
    latest_path = REPORTS_DIR / "qa_report_latest.json"
    latest_path.write_text(json.dumps(report, indent=2))

    # Generate markdown summary
    md = generate_markdown_report(report)
    md_path = REPORTS_DIR / "qa_report_latest.md"
    md_path.write_text(md)
    print(f"📝 Markdown report: {md_path}")

    return report


def generate_markdown_report(report):
    """Generate a human-readable markdown report."""
    lines = [
        "# GOLFIN Visual QA Report",
        f"**Generated:** {report['timestamp']}",
        f"**Screens:** {report['summary']['total']} total | "
        f"✅ {report['summary']['passing']} passing | "
        f"🟡 {report['summary']['warning']} warning | "
        f"🔴 {report['summary']['failing']} failing",
        "",
        "## Results",
        "",
        "| Screen | Match | Diff % | Status |",
        "|--------|-------|--------|--------|",
    ]

    for name, data in report["screens"].items():
        if "status" in data:
            lines.append(f"| {name} | — | — | ⚠️ {data['status']} |")
        else:
            score = data.get("match_score", 0)
            emoji = "✅" if score > 95 else "🟡" if score > 80 else "🔴"
            lines.append(f"| {name} | {score}% | {data.get('diff_percent',0)}% | {emoji} |")

    lines.extend([
        "",
        "## How to Fix",
        "",
        "1. Open diff images in `QA/Reports/diff_*.png` to see highlighted differences",
        "2. For AI-powered analysis, upload Unity + Figma screenshots to the chat",
        "3. Run `Tools → Create GOLFIN UI Scene` in Unity after code fixes",
        "4. Recapture with `Tools → QA → Capture All Screens`",
        "5. Re-run `python3 visual_qa.py --compare` to verify",
    ])

    return "\n".join(lines)


# ═══════════════════════════════════════════════════════════════
# FIGMA DESIGN TOKEN EXTRACTION (bonus)
# ═══════════════════════════════════════════════════════════════

def extract_figma_tokens(token):
    """Pull design tokens from Figma for automated value checking."""
    headers = {"X-Figma-Token": token}
    print("Extracting design tokens...")

    r = requests.get(f"{FIGMA_API}/files/{FIGMA_FILE_KEY}", headers=headers)
    if r.status_code == 429:
        wait = int(r.headers.get('Retry-After', 30))
        print(f"Rate limited. Waiting {wait}s...")
        time.sleep(wait)
        r = requests.get(f"{FIGMA_API}/files/{FIGMA_FILE_KEY}", headers=headers)
    r.raise_for_status()
    data = r.json()

    colors = set()
    fonts = set()

    def walk(node):
        for fill in node.get("fills", []):
            if fill.get("visible", True) is False: continue
            if fill["type"] == "SOLID" and "color" in fill:
                c = fill["color"]
                r, g, b = int(c["r"]*255), int(c["g"]*255), int(c["b"]*255)
                colors.add(f"#{r:02x}{g:02x}{b:02x}")

        style = node.get("style", {})
        if style.get("fontFamily"):
            fonts.add((style["fontFamily"], style.get("fontWeight", 400), style.get("fontSize", 0)))

        for child in node.get("children", []):
            walk(child)

    walk(data["document"])

    tokens = {
        "colors": sorted(colors),
        "fonts": [{"family": f, "weight": w, "size": s} for f, w, s in sorted(fonts)],
        "extracted_at": datetime.now().isoformat()
    }

    tokens_path = QA_DIR / "design_tokens.json"
    tokens_path.write_text(json.dumps(tokens, indent=2))
    print(f"✅ Saved {len(tokens['colors'])} colors, {len(tokens['fonts'])} font styles → {tokens_path}")
    return tokens


# ═══════════════════════════════════════════════════════════════
# CLI
# ═══════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(description="GOLFIN Visual QA Pipeline")
    parser.add_argument("--export-figma", action="store_true", help="Export Figma screens as PNGs")
    parser.add_argument("--compare", action="store_true", help="Compare Unity vs Figma screenshots")
    parser.add_argument("--full", action="store_true", help="Export + Compare all")
    parser.add_argument("--tokens", action="store_true", help="Extract Figma design tokens")
    parser.add_argument("--screen", type=str, help="Specific screen name (e.g. LoadingScreen)")
    parser.add_argument("--token", type=str, help="Figma personal access token")
    parser.add_argument("--scale", type=int, default=2, help="Figma export scale (default: 2)")

    args = parser.parse_args()
    screens = [args.screen] if args.screen else None

    if not any([args.export_figma, args.compare, args.full, args.tokens]):
        parser.print_help()
        return

    if args.export_figma or args.full:
        token = get_token(args)
        export_figma_screens(token, screens, args.scale)

    if args.compare or args.full:
        compare_screens(screens)

    if args.tokens:
        token = get_token(args)
        extract_figma_tokens(token)


if __name__ == "__main__":
    main()
