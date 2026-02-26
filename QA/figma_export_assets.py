#!/usr/bin/env python3
"""GOLFIN Figma Asset Exporter
Exports individual UI elements as PNGs directly from Figma API.

Usage:
  python3 QA/figma_export_assets.py                    # Export all components
  python3 QA/figma_export_assets.py --page "Home"      # Export from specific page
  python3 QA/figma_export_assets.py --node-id "123:456" # Export specific node
  python3 QA/figma_export_assets.py --list              # List all exportable nodes
"""

import json, os, sys, requests, time, re
from pathlib import Path

FIGMA_TOKEN = os.environ.get("FIGMA_TOKEN", "")
if not FIGMA_TOKEN:
    # Try reading from local config
    token_path = os.path.expanduser("~/.config/figma/token")
    if os.path.exists(token_path):
        FIGMA_TOKEN = open(token_path).read().strip()
    else:
        print("Set FIGMA_TOKEN env var or create ~/.config/figma/token")
        sys.exit(1)
FILE_KEY = "5gEAHjl6xAtW8iYY7NMvWd"
API = "https://api.figma.com/v1"
HEADERS = {"X-Figma-Token": FIGMA_TOKEN}
OUTPUT_DIR = Path("Assets/Art/UI/Figma_Exports")
CACHE_FILE = Path("/tmp/figma_full.json")

def fetch_file():
    """Fetch full Figma file (uses cache if < 1hr old)"""
    if CACHE_FILE.exists():
        age = time.time() - CACHE_FILE.stat().st_mtime
        if age < 3600:
            print(f"Using cached Figma file ({int(age)}s old)")
            return json.loads(CACHE_FILE.read_text())
    
    print("Fetching Figma file...")
    r = requests.get(f"{API}/files/{FILE_KEY}", headers=HEADERS)
    if r.status_code == 429:
        print("⚠️  Rate limited. Using cache if available.")
        if CACHE_FILE.exists():
            return json.loads(CACHE_FILE.read_text())
        sys.exit(1)
    r.raise_for_status()
    data = r.json()
    CACHE_FILE.write_text(json.dumps(data))
    return data

def collect_nodes(node, page_name="", depth=0):
    """Walk the Figma tree and collect exportable nodes"""
    results = []
    t = node.get("type", "")
    name = node.get("name", "")
    node_id = node.get("id", "")
    
    # Track page name
    if t == "PAGE":
        page_name = name
    
    # Collect frames, components, instances, vectors, images
    if t in ("FRAME", "COMPONENT", "INSTANCE", "VECTOR", "RECTANGLE", "GROUP", "BOOLEAN_OPERATION"):
        bbox = node.get("absoluteBoundingBox", {})
        w = bbox.get("width", 0)
        h = bbox.get("height", 0)
        if w > 0 and h > 0:
            results.append({
                "id": node_id,
                "name": name,
                "type": t,
                "page": page_name,
                "width": round(w),
                "height": round(h),
                "depth": depth,
                "has_export": len(node.get("exportSettings", [])) > 0,
            })
    
    for child in node.get("children", []):
        results.extend(collect_nodes(child, page_name, depth + 1))
    
    return results

def export_images(node_ids, scale=2, format="png"):
    """Call Figma image export API for given node IDs"""
    if not node_ids:
        return {}
    
    # Figma API limits to 50 IDs per request
    all_urls = {}
    batch_size = 50
    for i in range(0, len(node_ids), batch_size):
        batch = node_ids[i:i+batch_size]
        ids_param = ",".join(batch)
        
        print(f"  Requesting export for {len(batch)} nodes...")
        r = requests.get(
            f"{API}/images/{FILE_KEY}",
            headers=HEADERS,
            params={"ids": ids_param, "scale": scale, "format": format}
        )
        
        if r.status_code == 429:
            print("⚠️  Rate limited. Waiting 30s...")
            time.sleep(30)
            r = requests.get(
                f"{API}/images/{FILE_KEY}",
                headers=HEADERS,
                params={"ids": ids_param, "scale": scale, "format": format}
            )
        
        r.raise_for_status()
        data = r.json()
        
        if data.get("err"):
            print(f"  ⚠️  API error: {data['err']}")
        
        urls = data.get("images", {})
        all_urls.update(urls)
        
        if i + batch_size < len(node_ids):
            time.sleep(1)  # Rate limit courtesy
    
    return all_urls

def download_images(url_map, nodes_by_id, output_dir):
    """Download exported images to local files"""
    output_dir.mkdir(parents=True, exist_ok=True)
    downloaded = 0
    
    for node_id, url in url_map.items():
        if not url:
            continue
        
        node_info = nodes_by_id.get(node_id, {})
        name = node_info.get("name", node_id)
        page = node_info.get("page", "unknown")
        
        # Clean filename
        safe_name = re.sub(r'[^\w\-.]', '_', name)
        safe_page = re.sub(r'[^\w\-.]', '_', page)
        
        # Create page subdirectory
        page_dir = output_dir / safe_page
        page_dir.mkdir(parents=True, exist_ok=True)
        
        filepath = page_dir / f"{safe_name}.png"
        
        try:
            img_data = requests.get(url, timeout=30).content
            filepath.write_bytes(img_data)
            w = node_info.get("width", "?")
            h = node_info.get("height", "?")
            print(f"  ✅ {safe_page}/{safe_name}.png ({w}×{h})")
            downloaded += 1
        except Exception as e:
            print(f"  ❌ {safe_name}: {e}")
    
    return downloaded

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Export Figma assets as PNGs")
    parser.add_argument("--page", help="Filter by page name (substring match)")
    parser.add_argument("--node-id", help="Export specific node ID")
    parser.add_argument("--list", action="store_true", help="List exportable nodes without downloading")
    parser.add_argument("--scale", type=float, default=2, help="Export scale (default 2x)")
    parser.add_argument("--min-size", type=int, default=20, help="Min width/height to export (default 20)")
    parser.add_argument("--max-depth", type=int, default=4, help="Max tree depth to export (default 4)")
    parser.add_argument("--output", default=str(OUTPUT_DIR), help="Output directory")
    args = parser.parse_args()
    
    data = fetch_file()
    
    if args.node_id:
        # Export specific node
        print(f"Exporting node {args.node_id}...")
        urls = export_images([args.node_id], scale=args.scale)
        nodes_by_id = {args.node_id: {"name": args.node_id, "page": "manual"}}
        downloaded = download_images(urls, nodes_by_id, Path(args.output))
        print(f"\n✅ Exported {downloaded} image(s)")
        return
    
    # Collect all nodes
    all_nodes = []
    for page in data.get("document", {}).get("children", []):
        all_nodes.extend(collect_nodes(page))
    
    # Filter
    filtered = all_nodes
    if args.page:
        filtered = [n for n in filtered if args.page.lower() in n["page"].lower()]
    
    filtered = [n for n in filtered 
                if n["width"] >= args.min_size 
                and n["height"] >= args.min_size
                and n["depth"] <= args.max_depth]
    
    if args.list:
        print(f"\n{'Page':<20} {'Name':<40} {'Type':<15} {'Size':<12} {'ID'}")
        print("-" * 110)
        for n in sorted(filtered, key=lambda x: (x["page"], x["depth"], x["name"])):
            size = f"{n['width']}×{n['height']}"
            exp = " 📦" if n["has_export"] else ""
            print(f"{n['page']:<20} {'  '*n['depth']}{n['name']:<40} {n['type']:<15} {size:<12} {n['id']}{exp}")
        print(f"\n{len(filtered)} exportable nodes found")
        return
    
    # Export
    print(f"\nExporting {len(filtered)} nodes at {args.scale}x scale...")
    nodes_by_id = {n["id"]: n for n in filtered}
    node_ids = list(nodes_by_id.keys())
    
    urls = export_images(node_ids, scale=args.scale)
    downloaded = download_images(urls, nodes_by_id, Path(args.output))
    
    print(f"\n✅ Exported {downloaded} image(s) to {args.output}")

if __name__ == "__main__":
    main()
