#!/usr/bin/env python3
"""
figma_node_to_spec.py — Generate UIFidelityLinter spec.json from real Figma output.

Usage (iter-3 — skip-judgment rules + iter-2 Option B front-end):
    python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> -o out_spec.json
    python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> -o out.json --verbose
    python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> --name-map map.json -o out.json
    python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> --include MenuRow,Thumbnail
    python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> --min-size 20

INPUT (two files joined on data-node-id / XML id):
    <metadata.xml>  — get_metadata XML: authoritative geometry (node id, type, name, exact px w/h)
    <context.jsx>   — get_design_context JSX: visual attributes (fills, border, radius, font, color)

OUTPUT:
    { "elements": [
        { "name": "...", "w": float, "h": float, "radius": float,
          "requireSprite": bool, "color": "#RRGGBB"|"", "fontSize": float, "fontWeight": "" },
        ...
    ]}
    (Linter schema — JsonUtility.FromJson<UISpec> compatible; all 8 keys always present.)

NAME MAP (--name-map):
    JSON file: {"<figma-name>": "<UnityGOName>", ...}
    Maps Figma data-name → Unity prefab GO name (the linter matches on GO name).
    Without a map: emit Figma names + WARN per unmapped element.
    IMPORTANT: only map Figma names to GO names that EXIST in the target prefab.
    Omit Figma nodes that have no Unity counterpart; the linter will FAIL on "missing" otherwise.

Sentinel rules:
    w, h, radius, fontSize = -1  means "do not check"
    color = ""                   means "skip color check"
    fontWeight = ""              means "skip weight check"
    requireSprite = false        for pure text nodes and plain uniform-solid backgrounds

requireSprite heuristic (D3 — fail-safe: ambiguous → true):
    true  when: IMAGE fill, GRADIENT fill, stroke/border (weight>0), non-text with radius>0,
                metadata-only shape/graphic node (any non-layout-container type with no JSX visual)
    false when: pure text (regardless of fill type — text gradients are TMP vertex-gradients),
                plain SOLID+no stroke+no radius
    ambiguous (e.g. no fills, no strokes, no radius on non-text) → true (fail-safe)

    D6 — metadata-only shape/graphic node (iter-5 original; iter-6 generalized):
    When a mapped element has no matching JSX visual (no data-node-id in the JSX output) AND its
    XML type is NOT a pure layout container (frame, group, section, text, instance, component,
    component-set), it is treated as a graphic/shape primitive whose visual is rendered by a
    parent <img> in Figma. In this case _metadata_only_shape=True is set and requireSprite is
    forced True with a WARN, restoring the D3 ambiguous→True fail-safe contract.  This covers
    rounded-rectangle, vector, ellipse, line, polygon, star, rectangle, and any future shape
    type automatically (exclude-list, not enumerate-list). Never emit requireSprite=False for
    a named, mapped shape/graphic node that has no JSX visual signal.

iter-3 skip-judgment rules (encode what a hand-authored spec deliberately skips):
    Rule 1 — text nodes: always w=-1, h=-1 (Unity TMP rects are container-sized, not glyph-box)
             AND requireSprite=False ALWAYS, even with bg-clip-text gradient (TMP vertex-gradient,
             not an Image sprite).
    Rule 2 — sprite-filled elements (requireSprite=True): color="" (sprite carries the color;
             Image.color is white/tint — a Figma fill-color check would false-fail every sprite pill).
    Rule 3 — root/outermost element: w=-1, h=-1 (sized by parent/layout, not intrinsically).
             Identified as the first element at depth 0 in the metadata XML.
"""

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET


# ---------- constants ---------------------------------------------------------

ALLOWED_FONT_WEIGHTS = {"Bold", "SemiBold", "Medium", "Regular"}

# Anonymous / layout-only Figma names to skip (D4).
SKIP_NAME_PATTERNS = [
    re.compile(r"^Frame$"),           # plain unnamed Frame group
    re.compile(r"^Frame\s+\d+$"),     # "Frame 8", "Frame 9", "Frame 10", ...
    re.compile(r"^Group\s*\d*$"),     # "Group", "Group 2", ...
    re.compile(r"^Sheen$"),           # inner gloss overlays
    re.compile(r"^Ellipse\s*\d*$"),   # decoration blobs
    re.compile(r"^RP Icon$"),         # inner icon shapes with no Unity counterpart
    re.compile(r"^RP Icon Container$"),  # inner icon wrapper
]


# ---------- requireSprite heuristic -------------------------------------------

def _decide_require_sprite(element: dict, verbose: bool, name: str) -> bool:
    """
    D3 heuristic.  Returns require_sprite bool.
    fail-safe: ambiguous → True.
    """
    is_text = element.get("is_text", False)
    fills = element.get("fills", [])
    strokes = element.get("strokes", [])
    radius = element.get("radius", 0) or 0

    # D6 (iter-5): metadata-only shape node — no JSX visual was joined (rounded-rectangle / vector /
    # ellipse present in XML but absent from JSX data-node-id map).  The visual is rendered by a
    # parent <img> in Figma.  Emit requireSprite=True — restores the D3 ambiguous→True fail-safe.
    if element.get("_metadata_only_shape"):
        reason = "metadata-only shape node (no JSX visual — D6 fail-safe)"
        if verbose:
            print(f"  [{name}] requireSprite=True   — {reason}")
        return True

    # Rule 1 (iter-3): text nodes are NEVER requireSprite, regardless of fills.
    # A bg-clip-text gradient on a <p> is a TMP vertex-gradient, not a Unity Image sprite.
    if is_text:
        reason = "text node — requireSprite always False (TMP, not Image; Rule 1)"
        if verbose:
            print(f"  [{name}] requireSprite=False  — {reason}")
        return False

    # IMAGE fill → definitely needs a sprite
    has_image = any(f.get("type", "") == "IMAGE" for f in fills)
    if has_image:
        reason = "has IMAGE fill"
        if verbose:
            print(f"  [{name}] requireSprite=True   — {reason}")
        return True

    # GRADIENT fill → needs a sprite (gradients are baked into sprites in Unity)
    has_gradient = any(f.get("type", "").startswith("GRADIENT") for f in fills)
    if has_gradient:
        reason = "has GRADIENT fill"
        if verbose:
            print(f"  [{name}] requireSprite=True   — {reason}")
        return True

    # Any border/stroke → typically a bordered sprite
    has_stroke = any((s.get("weight", 0) or 0) > 0 for s in strokes)
    if has_stroke:
        reason = f"has stroke (weight={[s.get('weight') for s in strokes]})"
        if verbose:
            print(f"  [{name}] requireSprite=True   — {reason}")
        return True

    # Rounded corners on a non-text container → corner styling via sprite
    if not is_text and radius > 0:
        reason = f"non-text element with corner radius={radius}"
        if verbose:
            print(f"  [{name}] requireSprite=True   — {reason}")
        return True

    # Plain uniform-solid background, no stroke, no radius
    solid_fills = [f for f in fills if f.get("type", "") == "SOLID"]
    if solid_fills and not has_stroke and radius == 0:
        reason = "plain uniform-solid fill, no stroke, no radius (anonymous layout group)"
        if verbose:
            print(f"  [{name}] requireSprite=False  — {reason}")
        return False

    # No fills at all (pure layout spacer)
    if not fills and not strokes:
        reason = "no fills and no strokes (pure layout spacer)"
        if verbose:
            print(f"  [{name}] requireSprite=False  — {reason}")
        return False

    # Genuinely ambiguous → fail-safe: True
    reason = "ambiguous (fail-safe → True)"
    if verbose:
        print(f"  [{name}] requireSprite=True   — {reason}")
    return True


# ---------- color extraction --------------------------------------------------

def _dominant_color(element: dict) -> str:
    """
    D5: emit dominant rim/fill/text hex (#RRGGBB), or "" for multi-color/gradient.
    """
    is_text = element.get("is_text", False)

    if is_text:
        fc = element.get("font_color", None)
        if fc:
            return _normalise_hex(fc)
        return ""

    fills = element.get("fills", [])
    strokes = element.get("strokes", [])

    # Gradient or image → can't express as single hex
    for f in fills:
        ft = f.get("type", "")
        if ft.startswith("GRADIENT") or ft == "IMAGE":
            return ""

    # Single solid fill
    solid_fills = [f for f in fills if f.get("type", "") == "SOLID" and f.get("color")]
    if len(solid_fills) == 1:
        return _normalise_hex(solid_fills[0]["color"])

    # Multiple solid fills → ambiguous
    if len(solid_fills) > 1:
        return ""

    # No fill — check stroke color
    solid_strokes = [s for s in strokes if s.get("color") and (s.get("weight", 0) or 0) > 0]
    if len(solid_strokes) == 1:
        return _normalise_hex(solid_strokes[0]["color"])

    return ""


def _normalise_hex(color_str: str) -> str:
    """
    Convert a color string to #RRGGBB (6-digit upper-case hex), or "" if unparseable.
    Handles: "#rrggbb", "#rrggbbaa", "rgba(r,g,b,a)", "rgb(r,g,b)".
    """
    if not color_str:
        return ""
    color_str = color_str.strip()

    if color_str.lower() in ("white", "#fff", "#ffff"):
        return "#FFFFFF"
    if color_str.lower() in ("black", "#000"):
        return "#000000"
    if color_str.lower() == "transparent":
        return ""

    # Already a hex string
    if color_str.startswith("#"):
        h = color_str[1:]
        if len(h) == 3:
            h = "".join(c * 2 for c in h)
        if len(h) >= 6:
            return ("#" + h[:6]).upper()
        return ""

    # rgba(r, g, b, a) or rgb(r, g, b)
    m = re.match(r"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)", color_str, re.IGNORECASE)
    if m:
        r, g, b = int(m.group(1)), int(m.group(2)), int(m.group(3))
        return "#{:02X}{:02X}{:02X}".format(r, g, b)

    return ""


# ---------- font weight normalisation -----------------------------------------

def _normalise_font_weight(raw: str) -> str:
    """
    Map font weight strings to the 4 allowed UISpec values:
    Bold | SemiBold | Medium | Regular  — or "" if unrecognised.

    Handles Tailwind patterns from JSX like:
      font-['Rubik:Bold']      → "Bold"
      font-['Rubik:SemiBold']  → "SemiBold"
      font-bold                → "Bold"
      font-semibold            → "SemiBold"
      font-normal              → "Regular"
    """
    if not raw:
        return ""
    raw = raw.strip()
    lower = raw.lower()

    mapping = {
        "bold": "Bold",
        "semibold": "SemiBold",
        "semi bold": "SemiBold",
        "semi-bold": "SemiBold",
        "medium": "Medium",
        "regular": "Regular",
        "normal": "Regular",
        "light": "Regular",
        "thin": "Regular",
        "extralight": "Regular",
        "extra light": "Regular",
        "black": "Bold",
        "extrabold": "Bold",
        "extra bold": "Bold",
        "heavy": "Bold",
    }
    if lower in mapping:
        return mapping[lower]

    # Partial containment
    if "semibold" in lower or "semi bold" in lower or "semi-bold" in lower:
        return "SemiBold"
    if "bold" in lower:
        return "Bold"
    if "medium" in lower:
        return "Medium"
    if "regular" in lower or "normal" in lower:
        return "Regular"

    # Numeric weight
    try:
        w = int(re.sub(r"\D", "", raw))
        if w >= 700:
            return "Bold"
        if w >= 600:
            return "SemiBold"
        if w >= 500:
            return "Medium"
        return "Regular"
    except (ValueError, TypeError):
        pass

    return ""


# ---------- element filtering (D4) --------------------------------------------

def _should_skip(name: str, w: float, h: float, min_size: float) -> bool:
    """Return True for anonymous wrapper/layout nodes that have no Unity counterpart."""
    for pat in SKIP_NAME_PATTERNS:
        if pat.match(name):
            return True
    # Skip hairline elements (divider lines < 3px)
    if w > 0 and h > 0:
        if min(w, h) < 3:
            return True
    # Skip if both dimensions below min_size
    if min_size > 0:
        if w > 0 and h > 0 and max(w, h) < min_size:
            return True
    return False


# ---------- element → UISpecElement ------------------------------------------

def element_to_spec(element: dict, verbose: bool) -> dict:
    """
    Convert one element descriptor to a UISpecElement dict (all 8 keys always present).

    iter-3 skip-judgment rules applied here:
      Rule 1 — text nodes: w=-1, h=-1 (TMP container-sized, not glyph-box-sized)
      Rule 2 — sprite elements (requireSprite=True): color="" (sprite carries colour)
      Rule 3 — root element (is_root=True): w=-1, h=-1 (layout-driven, not intrinsic)
    """
    name = element.get("name", "Unknown")
    if verbose:
        print(f"Processing: {name}")

    w = float(element.get("w", -1) or -1)
    h = float(element.get("h", -1) or -1)
    radius = float(element.get("radius", -1) or -1)

    is_text = element.get("is_text", False)
    is_root = element.get("is_root", False)

    # Rule 1 (iter-3): text nodes — Unity TMP rects are container-sized, not glyph-box-sized.
    # Override whatever geometry was read from metadata.
    if is_text:
        w = -1.0
        h = -1.0
        if verbose:
            print(f"  [{name}] w/h → -1 (text node, Rule 1)")

    # Rule 3 (iter-3): root element — sized by parent layout, not intrinsically.
    if is_root:
        w = -1.0
        h = -1.0
        if verbose:
            print(f"  [{name}] w/h → -1 (root element, Rule 3)")

    # fontSize: only meaningful for text nodes; sentinel -1 otherwise
    font_size = -1.0
    if is_text:
        fs = element.get("font_size", None)
        if fs is not None and fs > 0:
            font_size = round(float(fs) / 1.2, 1)  # auto-divide by 1.2 (shell TMP convention)

    # fontWeight: only for text nodes
    font_weight = ""
    if is_text:
        fw = element.get("font_weight", None)
        if fw:
            font_weight = _normalise_font_weight(fw)

    # requireSprite heuristic (D3) — Rule 1 already forces False for text nodes inside here
    require_sprite = _decide_require_sprite(element, verbose, name)

    # color (D5)
    color = _dominant_color(element)

    # Rule 2 (iter-3): sprite-filled elements — the colour is carried by the sprite.
    # Unity Image.color is always white for correctly imported sprites, so a Figma fill-color
    # check would false-fail every reused pill/button.  Blank it out.
    if require_sprite:
        color = ""
        if verbose:
            print(f"  [{name}] color → '' (sprite element, Rule 2)")

    spec_el = {
        "name": name,
        "w": w,
        "h": h,
        "radius": radius,
        "requireSprite": require_sprite,
        "color": color,
        "fontSize": font_size,
        "fontWeight": font_weight,
    }
    return spec_el


# ---------- main entry (generate_spec) ----------------------------------------

def generate_spec(elements: list, include: list, min_size: float, verbose: bool) -> dict:
    """
    Convert a flat list of element descriptors to a UISpec dict.

    Args:
        elements: List of element dicts (from parse_figma_dumps or direct construction).
        include:  Whitelist of element names (empty = include all non-skipped).
        min_size: Skip elements whose max(w,h) is below this (0 = no filter).
        verbose:  Print per-element decision trace.
    Returns:
        {"elements": [ {8-key dict}, ... ]}
    """
    include_set = set(n.strip() for n in include) if include else set()

    spec_elements = []
    for el in elements:
        name = el.get("name", "")
        w = float(el.get("w", -1) or -1)
        h = float(el.get("h", -1) or -1)

        # D4 filtering
        if include_set and name not in include_set:
            if verbose:
                print(f"  [{name}] SKIP (not in --include list)")
            continue

        if not include_set and _should_skip(name, w, h, min_size):
            if verbose:
                print(f"  [{name}] SKIP (anonymous/layout/hairline)")
            continue

        spec_el = element_to_spec(el, verbose)
        spec_elements.append(spec_el)

    return {"elements": spec_elements}


# ═══════════════════════════════════════════════════════════════════════════════
# NEW FRONT-END: parse real Figma output (metadata.xml + context.jsx)
# ═══════════════════════════════════════════════════════════════════════════════

def _parse_metadata_xml(xml_path: str) -> dict:
    """
    Parse get_metadata XML into a dict keyed by node-id.
    Returns: { "13330:1178": { "id": ..., "name": ..., "w": float, "h": float,
                                "type": "frame"|"text"|"rounded-rectangle"|"vector"|"line",
                                "is_root": bool } }
    The top-level element (depth=0) is marked is_root=True so element_to_spec can apply Rule 3.
    """
    tree = ET.parse(xml_path)
    root = tree.getroot()

    nodes = {}

    def _walk(el, depth: int = 0):
        node_id = el.get("id")
        if not node_id:
            return
        tag = el.tag.lower()
        name = el.get("name", "")
        try:
            w = float(el.get("width", -1))
        except (TypeError, ValueError):
            w = -1.0
        try:
            h = float(el.get("height", -1))
        except (TypeError, ValueError):
            h = -1.0
        nodes[node_id] = {
            "id": node_id,
            "name": name,
            "w": w,
            "h": h,
            "type": tag,       # "frame", "text", "rounded-rectangle", "vector", "line"
            "is_root": (depth == 0),   # Rule 3: outermost element is layout-driven
        }
        for child in el:
            _walk(child, depth + 1)

    _walk(root)
    return nodes


def _parse_jsx_visual(jsx_path: str) -> dict:
    """
    Parse get_design_context JSX into a dict keyed by node-id.
    For each data-node-id element, extract:
      - radius   (from rounded-[Npx]; per-corner → -1 + WARN list)
      - fills    ([{"type": "SOLID"|"GRADIENT_LINEAR"|"IMAGE", "color": hex|None}])
      - strokes  ([{"color": hex, "weight": float}])
      - is_text  (True for <p> elements)
      - font_size  (from text-[Npx] inside <p>)
      - font_weight (from font-['Family:Weight'] or font-bold/semibold/etc.)
      - font_color  (from text-[#hex] / text-white / text-black inside <p>)
      - has_img_child (True if this element or a direct child has <img>)

    Returns: { "13330:1178": { visual-dict }, ... }
    Warnings about per-corner radius are returned in the "_warnings" key of each entry.
    """
    with open(jsx_path, "r", encoding="utf-8") as fh:
        jsx = fh.read()

    nodes = {}

    # We use a two-phase approach:
    # Phase 1: Find all elements with data-node-id (both <div> and <p>)
    # Phase 2: For each element, extract its opening tag class attributes

    # Regex: find all opening tags that have data-node-id
    # We match the opening tag content (everything up to >) - works because JSX
    # serializes one element per line with data-node-id on the outermost element.
    #
    # Strategy: split on data-node-id occurrences, then parse backwards for the
    # preceding className and element type.

    # Pattern: capture the element type + className + data-node-id + data-name
    # JSX output format (observed): <div className="..." data-node-id="..." data-name="...">
    # or: <p className="..." data-node-id="...">
    TAG_PATTERN = re.compile(
        r'<(div|p)\b[^>]*?data-node-id="([^"]+)"([^>]*?)>',
        re.DOTALL
    )

    # Also capture self-closing elements <div ... data-node-id="..."/>
    SELF_CLOSE_PATTERN = re.compile(
        r'<(div|p)\b[^>]*?data-node-id="([^"]+)"([^>]*?)/>',
        re.DOTALL
    )

    def _extract_class(tag_attrs: str) -> str:
        m = re.search(r'className="([^"]*)"', tag_attrs, re.DOTALL)
        if m:
            # JSX multiline classNames may have newlines — collapse
            return " ".join(m.group(1).split())
        return ""

    def _extract_data_name(tag_attrs: str) -> str:
        m = re.search(r'data-name="([^"]*)"', tag_attrs)
        return m.group(1) if m else ""

    def _parse_radius(cls: str, node_id: str) -> tuple:
        """Returns (radius_float, warnings_list)."""
        warnings = []

        # Per-corner patterns: rounded-tl-[..], rounded-tr-[..], rounded-bl-[..], rounded-br-[..]
        per_corner = re.findall(r'rounded-(?:tl|tr|bl|br|t|b|l|r)-\[([^\]]+)\]', cls)
        if per_corner:
            # Extract rounded-tl/tr/bl/br values
            corners = re.findall(r'rounded-(?:tl|tr|bl|br)-\[([^\]]+)\]', cls)
            if corners:
                try:
                    vals = [float(v.rstrip("px")) for v in corners]
                    if len(set(vals)) == 1:
                        return (vals[0], warnings)
                    else:
                        warnings.append(f"node {node_id}: per-corner radius {corners} differ → emitting -1")
                        return (-1.0, warnings)
                except ValueError:
                    warnings.append(f"node {node_id}: could not parse per-corner radius {corners}")
                    return (-1.0, warnings)

        # Uniform rounded-[Npx]
        m = re.search(r'(?<!\w)rounded-\[([^\]]+)\]', cls)
        if m:
            val = m.group(1).rstrip("px")
            try:
                return (float(val), warnings)
            except ValueError:
                warnings.append(f"node {node_id}: could not parse radius '{m.group(1)}'")
                return (-1.0, warnings)

        # Tailwind shorthand: rounded-full (pill), rounded-lg, rounded, etc.
        # These map to semantic radii — emit -1 (can't be exact)
        shorthand = re.search(r'(?<!\w)rounded(?:-(?:sm|md|lg|xl|2xl|3xl|full))?\b', cls)
        if shorthand:
            token = shorthand.group(0)
            if token == "rounded-full":
                # pill shape — radius is half the height, unknown without size → -1
                warnings.append(f"node {node_id}: rounded-full (pill) → emitting -1 (unknown height)")
                return (-1.0, warnings)
            if token in ("rounded-sm", "rounded", "rounded-md"):
                return (4.0, warnings)
            if token == "rounded-lg":
                return (8.0, warnings)
            if token == "rounded-xl":
                return (12.0, warnings)
            if token == "rounded-2xl":
                return (16.0, warnings)
            if token == "rounded-3xl":
                return (24.0, warnings)

        return (0.0, warnings)

    def _parse_fills(cls: str, tag_attrs: str, node_id: str) -> list:
        """Extract fill descriptors from className + inline style."""
        fills = []

        # Gradient via bg-gradient-to-* (any direction token)
        if re.search(r'\bbg-gradient-to-\w+\b', cls):
            fills.append({"type": "GRADIENT_LINEAR", "color": None})
            return fills   # gradient dominates; don't add bg-[hex] as well

        # Gradient via bg-gradient-to-b ... from/to (also caught above, redundant safety)
        if re.search(r'\bbg-clip-text\b.*\bbg-gradient', cls, re.DOTALL) or \
           re.search(r'\bbg-gradient', cls):
            fills.append({"type": "GRADIENT_LINEAR", "color": None})
            return fills

        # Inline style backgroundImage: linear-gradient(...)
        if 'backgroundImage' in tag_attrs and 'linear-gradient' in tag_attrs:
            fills.append({"type": "GRADIENT_LINEAR", "color": None})
            return fills

        # Solid bg-[#hex] or bg-[rgba(...)]
        m = re.search(r'\bbg-\[([^\]]+)\]', cls)
        if m:
            raw_color = m.group(1)
            hex_color = _normalise_hex(raw_color)
            fills.append({"type": "SOLID", "color": hex_color or raw_color})
            return fills

        # bg-white / bg-black / bg-transparent
        if re.search(r'\bbg-white\b', cls):
            fills.append({"type": "SOLID", "color": "#FFFFFF"})
            return fills
        if re.search(r'\bbg-black\b', cls):
            fills.append({"type": "SOLID", "color": "#000000"})
            return fills
        if re.search(r'\bbg-transparent\b', cls):
            return []

        return fills

    def _has_img_child_in_block(block_text: str) -> bool:
        """True if <img ... /> appears in the block BEFORE the next data-node-id element.

        This ensures we only detect <img> that is a DIRECT child fill of the current element,
        not an <img> inside a nested named child element. If another data-node-id appears before
        the <img>, that <img> belongs to a nested element and is NOT this element's fill.
        """
        # Find position of the first nested named element (which owns its own fills)
        next_named = re.search(r'data-node-id="', block_text)
        # Find the first <img> tag
        img_match = re.search(r'<img\b', block_text)
        if not img_match:
            return False
        if next_named and next_named.start() < img_match.start():
            # <img> comes AFTER a nested named element — belongs to the nested element
            return False
        return True

    def _parse_strokes(cls: str, node_id: str) -> list:
        """Extract stroke descriptors from className border-* tokens."""
        strokes = []

        # Check for border-solid or border-dashed (confirm a border exists)
        has_border_style = bool(re.search(r'\bborder-(?:solid|dashed|dotted)\b', cls))
        # Also: just `border` alone is a shorthand for border-1
        has_border_keyword = bool(re.search(r'(?<!\w)border(?!\s*-\[|\s*-\d|\s*-solid|\s*-dashed|\s*-dotted|\s*-none|\s*-t\b|\s*-b\b|\s*-l\b|\s*-r\b|\s*-x\b|\s*-y\b|\s*-opacity|\s*-collapse|\s*-separate|\s*-spacing)\b', cls))

        # border color: border-[#hex] or border-[rgba(...)]
        border_color_match = re.search(r'\bborder-\[([^\]]+)\]', cls)
        border_color = None
        if border_color_match:
            raw = border_color_match.group(1)
            # Skip if it looks like a pixel value (e.g. border-[1.5px])
            if not re.match(r'^[\d.]+px$', raw.strip()):
                border_color = _normalise_hex(raw) or raw

        # border weight: border-2, border-3, border-[Npx], border-[1.5px]
        weight = 0.0
        # Explicit px weight: border-[1.5px], border-[2px]
        px_match = re.search(r'\bborder-\[(\d+(?:\.\d+)?)px\]', cls)
        if px_match:
            weight = float(px_match.group(1))
        else:
            # Named weights: border (=1), border-2, border-3, border-4...
            named_match = re.search(r'(?<!\w)border-(\d+)(?!\w)', cls)
            if named_match:
                weight = float(named_match.group(1))
            elif has_border_keyword or has_border_style:
                # `border` alone = 1px default
                weight = 1.0

        if weight > 0 and border_color:
            strokes.append({"type": "SOLID", "color": border_color, "weight": weight})
        elif weight > 0:
            # border with no explicit color → still a stroke (color unknown)
            strokes.append({"type": "SOLID", "color": "", "weight": weight})

        return strokes

    def _parse_text_props(cls: str) -> dict:
        """Extract font_size, font_weight, font_color from any element's className.

        Tag-agnostic: called for ALL elements (not only <p>).  This handles Figma's
        common pattern of wrapping a <p> inside a <div> that carries text-[Npx] /
        font-['Family:Weight'] — the <p> child has no size/weight classes, so only
        scanning <p> elements drops those props (the iter-3 bug, ARCHITECT_REVIEW §3b).
        Non-text elements will harmlessly receive font_size=None / font_weight="" /
        font_color="" which is the same sentinel already used for sprite elements."""
        # font_size: text-[Npx]
        font_size = None
        m = re.search(r'\btext-\[(\d+(?:\.\d+)?)px\]', cls)
        if m:
            try:
                font_size = float(m.group(1))
            except ValueError:
                pass

        # font_weight:
        # Priority 1: font-['Family:Weight'] → extract Weight part
        fw = ""
        family_weight = re.search(r"font-\['[^']*:([^']+)'\]", cls)
        if family_weight:
            fw = family_weight.group(1)
        else:
            # Priority 2: font-bold / font-semibold / font-normal / font-medium
            tw_fw = re.search(r'\bfont-(bold|semibold|medium|normal|light)\b', cls)
            if tw_fw:
                fw = tw_fw.group(1)

        font_weight = _normalise_font_weight(fw) if fw else ""

        # font_color: text-[#hex] / text-white / text-black / text-[rgba(...)]
        # Skip: text-[transparent] (gradient text)
        font_color = ""
        tc = re.search(r'\btext-\[([^\]]+)\]', cls)
        if tc:
            raw = tc.group(1)
            if raw.lower() != "transparent" and not raw.startswith("color:"):
                font_color = _normalise_hex(raw)
        if not font_color:
            if re.search(r'\btext-white\b', cls):
                font_color = "#FFFFFF"
            elif re.search(r'\btext-black\b', cls):
                font_color = "#000000"

        return {
            "font_size": font_size,
            "font_weight": font_weight,
            "font_color": font_color,
        }

    # ---- find all blocks in the JSX scoped to a data-node-id ----
    # We need both the opening-tag attributes AND the block content (for img child detection).
    # Strategy: find all positions of `data-node-id="..."` and extract the surrounding tag.

    all_warnings = []

    # Find all matches of opening tags with data-node-id
    for pattern in (TAG_PATTERN, SELF_CLOSE_PATTERN):
        for m in pattern.finditer(jsx):
            tag_type = m.group(1)  # "div" or "p"
            node_id = m.group(2)
            rest_attrs = m.group(3)  # everything after data-node-id="..." up to >

            # Combine className from before and after data-node-id in the full tag
            full_tag_text = m.group(0)
            cls = _extract_class(full_tag_text)
            data_name = _extract_data_name(full_tag_text)
            is_text = (tag_type == "p")

            # For div elements: look ahead a bit for <img src=...> (IMAGE fill signal)
            # We grab a window of ~400 chars after the opening tag
            window_start = m.end()
            window_end = min(window_start + 600, len(jsx))
            block_window = jsx[window_start:window_end]
            has_img = _has_img_child_in_block(block_window) if not is_text else False

            radius, warns = _parse_radius(cls, node_id)
            all_warnings.extend(warns)

            if has_img:
                fills = [{"type": "IMAGE", "color": None}]
            else:
                fills = _parse_fills(cls, full_tag_text, node_id)

            strokes = _parse_strokes(cls, node_id)

            # Always run _parse_text_props — it is tag-agnostic.  Figma often wraps
            # text content in a <div> (not a <p>), and font/size classes live on that
            # outer div.  If we only ran this for <p> tags (JSX is_text=True), a
            # div-wrapped text element like BuyButtonLabel would lose its font gate.
            # Non-text elements get font_size=None / font_weight="" — the same
            # sentinels element_to_spec uses when is_text=False; no leakage.
            text_props = _parse_text_props(cls)

            # For non-text elements that have an <img>, add IMAGE fill if not already present
            if has_img and not any(f["type"] == "IMAGE" for f in fills):
                fills = [{"type": "IMAGE", "color": None}]

            entry = {
                "node_id": node_id,
                "data_name": data_name,
                "is_text": is_text,
                "fills": fills,
                "strokes": strokes,
                "radius": radius,
                "_warnings": warns,
            }
            # Always merge text_props so font_{size,weight,color} are available in vis
            # for the XML→JSX join in parse_figma_dumps (where is_text is re-evaluated
            # as vis.is_text OR meta.type=="text", catching div-wrapped text cases).
            entry.update(text_props)

            # Only overwrite if we have a data-name (named elements take priority)
            if node_id not in nodes:
                nodes[node_id] = entry
            elif data_name and not nodes[node_id].get("data_name"):
                nodes[node_id] = entry

    return nodes, all_warnings


def parse_figma_dumps(xml_path: str, jsx_path: str, name_map: dict,
                      verbose: bool) -> tuple:
    """
    Join metadata XML (geometry) + context JSX (visual) on node id.

    Returns:
        (elements: list[dict], warnings: list[str])
    Each element dict has all fields needed by element_to_spec():
      name, w, h, radius, fills, strokes, is_text,
      font_size (raw Figma px — NOT yet ÷1.2, element_to_spec does that),
      font_weight, font_color
    """
    metadata = _parse_metadata_xml(xml_path)
    visual_by_id, parse_warnings = _parse_jsx_visual(jsx_path)

    all_warnings = list(parse_warnings)
    elements = []

    for node_id, meta in metadata.items():
        vis = visual_by_id.get(node_id, {})
        figma_name = vis.get("data_name") or meta.get("name", "")

        # Apply name map: Figma name → Unity GO name (or null to exclude)
        # Convention: map to null  → exclude this Figma node entirely (no Unity GO counterpart)
        #             map to string → rename to Unity GO name
        #             absent        → emit with Figma name + warning
        _nm_key = figma_name if figma_name in name_map else node_id
        _in_map = _nm_key in name_map
        unity_name = name_map.get(figma_name) if figma_name in name_map else name_map.get(node_id)

        if _in_map and unity_name is None:
            # Explicit null in name map → exclude this node (Rule 4: no Unity GO counterpart)
            continue

        if unity_name:
            display_name = unity_name
        else:
            display_name = figma_name
            if figma_name and figma_name not in ("", "Unknown"):
                # Only warn if the element has a real name and visual data
                # (skip unnamed or no-visual elements)
                if vis:
                    all_warnings.append(
                        f"WARNING: no name-map entry for Figma name '{figma_name}' (node {node_id}) "
                        f"— emitting Figma name. Add to --name-map to use Unity GO name."
                    )

        # Use metadata for w/h (authoritative); JSX for everything visual
        el = {
            "name": display_name,
            "w": meta.get("w", -1),
            "h": meta.get("h", -1),
            "radius": vis.get("radius", 0.0),
            "fills": vis.get("fills", []),
            "strokes": vis.get("strokes", []),
            # Rule 1: text if EITHER the JSX <p> heuristic OR XML metadata type=text says so.
            # vis.get("is_text") can be False even when type=text (Figma wraps text in <div>),
            # so we must OR both signals rather than using vis as a fallback.
            "is_text": vis.get("is_text", False) or (meta.get("type") == "text"),
            "is_root": meta.get("is_root", False),  # Rule 3: propagate root flag
            # font properties (raw px — element_to_spec will apply ÷1.2)
            "font_size": vis.get("font_size", None),
            "font_weight": vis.get("font_weight", ""),
            "font_color": vis.get("font_color", ""),
        }

        # D6 (iter-6 generalized) — metadata-only shape/graphic node fail-safe.
        # When a MAPPED (non-null) element resolves to an empty vis (no JSX data-node-id match),
        # the join produced no fills, strokes, or radius.  Without this guard, _decide_require_sprite
        # falls through to the "no fills and no strokes (pure layout spacer)" branch →
        # requireSprite=False, a silent false-negative.
        #
        # Iter-5 used an INCLUDE-LIST {rounded-rectangle, vector, ellipse} — a bandaid that still
        # let line/polygon/star/rectangle/boolean-operation/etc. silently emit requireSprite=False.
        # Iter-6 inverts to an EXCLUDE-LIST; iter-7 narrows it to the MINIMAL correct set.
        #
        # Exclude-list intent: ONLY node types that are definitively NOT a graphic themselves.
        #   frame / group / section — pure layout wrappers; no visual contribution of their own.
        #   text — always requireSprite=False per Rule 1 (own path handles it).
        #
        # NOT excluded (D3 ambiguous → True fail-safe):
        #   instance / component / component-set — these are *references* to component definitions
        #   that may themselves be graphic.  Figma icon libraries (Material Icons, Phosphor,
        #   Iconify, in-house catalogs) publish icons AS components; every icon the designer drops
        #   is an `instance` node.  The metadata-only-icon-in-parent-<img> pattern (no JSX visual
        #   join, no fills/strokes/radius on the child) applies to `instance` nodes just as to
        #   `rounded-rectangle`.  Under D3 (ambiguous → True), a mapped instance with no visual
        #   signal must emit requireSprite=True + WARN — never silently False.
        #
        # Any type not in this set (shape primitive, instance, component, component-set, or any
        # future/unknown type) with no JSX visual → requireSprite=True + WARN.  Over-triggering
        # toward True for an exotic type is the D3-sanctioned safe direction.
        _NON_GRAPHIC_CONTAINER_TYPES = {"frame", "group", "section", "text"}
        if (
            not vis                                              # no JSX visual joined
            and meta.get("type", "")                             # has a type (not empty/unknown)
            and meta.get("type", "") not in _NON_GRAPHIC_CONTAINER_TYPES  # NOT a layout container
            and _in_map and unity_name is not None              # explicitly mapped to a Unity GO
        ):
            el["_metadata_only_shape"] = True
            all_warnings.append(
                f"WARNING: node {node_id} '{figma_name}' mapped to '{unity_name}' resolved to "
                f"metadata-only (type={meta.get('type')!r} with no JSX visual) — emitting "
                f"requireSprite=True (D6 fail-safe; any non-layout-container type with no vis); "
                f"verify the built sprite exists in the prefab."
            )

        elements.append(el)

    return elements, all_warnings


# ---------- CLI ---------------------------------------------------------------

def main(argv=None):
    ap = argparse.ArgumentParser(
        description=(
            "Generate UIFidelityLinter spec.json from real Figma output.\n"
            "Input: a get_metadata XML file + a get_design_context JSX file, "
            "joined on data-node-id / XML element id."
        )
    )
    ap.add_argument("metadata_xml",
                    help="Path to saved get_metadata XML file.")
    ap.add_argument("context_jsx",
                    help="Path to saved get_design_context JSX file.")
    ap.add_argument("-o", "--output", default=None,
                    help="Output spec.json path (default: <context_jsx stem>_spec.json).")
    ap.add_argument("--name-map", default=None,
                    help='JSON file mapping Figma names → Unity GO names: '
                         '{"<figma-name>": "<UnityGOName>"}. '
                         'Without this, Figma names are emitted with WARNINGs.')
    ap.add_argument("--include", default="",
                    help="Comma-separated list of element names to include (default: all non-skipped).")
    ap.add_argument("--min-size", type=float, default=0.0,
                    help="Skip elements whose max(w,h) < this value (default: 0 = no filter).")
    ap.add_argument("--verbose", action="store_true",
                    help="Print per-element decision trace (why requireSprite true/false).")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.metadata_xml):
        print(f"ERROR: metadata XML not found: {args.metadata_xml}", file=sys.stderr)
        return 1
    if not os.path.isfile(args.context_jsx):
        print(f"ERROR: context JSX not found: {args.context_jsx}", file=sys.stderr)
        return 1

    # Load optional name map
    name_map = {}
    if args.name_map:
        if not os.path.isfile(args.name_map):
            print(f"ERROR: name-map file not found: {args.name_map}", file=sys.stderr)
            return 1
        with open(args.name_map, "r", encoding="utf-8") as fh:
            try:
                name_map = json.load(fh)
            except json.JSONDecodeError as exc:
                print(f"ERROR: invalid JSON in name-map {args.name_map}: {exc}", file=sys.stderr)
                return 1
        if args.verbose:
            print(f"Name map: {len(name_map)} entries from {args.name_map}")

    if args.verbose:
        print(f"Metadata: {args.metadata_xml}")
        print(f"Context:  {args.context_jsx}")
        print()

    # Parse real Figma output
    try:
        elements, warnings = parse_figma_dumps(
            args.metadata_xml, args.context_jsx, name_map, args.verbose
        )
    except ET.ParseError as exc:
        print(f"ERROR: XML parse error in {args.metadata_xml}: {exc}", file=sys.stderr)
        return 1
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    # Print warnings
    for w in warnings:
        print(w, file=sys.stderr)

    include = [n for n in args.include.split(",") if n.strip()] if args.include else []

    if args.verbose:
        print(f"Filter: include={include or 'all'}, min-size={args.min_size}")
        print()

    spec = generate_spec(elements, include, args.min_size, args.verbose)

    # Determine output path
    if args.output:
        out_path = args.output
    else:
        stem = os.path.splitext(args.context_jsx)[0]
        out_path = stem + "_spec.json"

    out_dir = os.path.dirname(out_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    out_json = json.dumps(spec, indent=2, ensure_ascii=False)
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(out_json)

    if args.verbose:
        print()
    print(f"Wrote {len(spec['elements'])} elements → {out_path}")
    if args.verbose:
        print(out_json)

    return 0


if __name__ == "__main__":
    sys.exit(main())
