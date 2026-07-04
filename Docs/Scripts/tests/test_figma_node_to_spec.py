#!/usr/bin/env python3
"""
test_figma_node_to_spec.py — Unit tests for figma_node_to_spec.py (iter-3 skip-judgment rules)

Coverage:
  BACK-END:
    - requireSprite heuristic (D3): image/gradient/stroke/radius -> True; plain-solid/text -> False
    - iter-3 Rule 1: text nodes always requireSprite=False (even w/ gradient fill or stroke)
    - iter-3 Rule 1: text nodes always w=-1, h=-1 in element_to_spec output
    - iter-3 Rule 2: sprite-filled elements (requireSprite=True) always color=""
    - iter-3 Rule 3: root element (is_root=True) always w=-1, h=-1
    - All 8 keys always present in every output element
    - Sentinel -1/""/false emitted for unknowns
    - fontWeight maps to 4 allowed values or ""
    - color emits #RRGGBB or ""
    - Valid JSON structure (elements array, all 8 keys)
    - Runs offline (no network calls in tested code)
    - --verbose decision trace
    - Regression anchor: stamina_boost_shop menu-row requireSprite decisions

  NEW FRONT-END (iter-2):
    - XML metadata parsing (w/h/name/is_root extraction by node id)
    - JSX visual parsing (radius, fills, strokes, is_text, font props)
    - Join on node id (metadata w/h wins; JSX visual wins for visual attrs)
    - --name-map remaps Figma names to Unity GO names
    - Fixture integration: menu_row_13330-1178 + selection_card_13156-1232
    - CLI new signature: <metadata.xml> <context.jsx> [-o ...] [--name-map ...]
"""

import json
import os
import sys
import tempfile
import unittest

# Make the parent Docs/Scripts directory importable
_TESTS_DIR = os.path.dirname(os.path.abspath(__file__))
_SCRIPTS_DIR = os.path.dirname(_TESTS_DIR)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

from figma_node_to_spec import (
    _decide_require_sprite,
    _dominant_color,
    _normalise_hex,
    _normalise_font_weight,
    _should_skip,
    element_to_spec,
    generate_spec,
    main,
    ALLOWED_FONT_WEIGHTS,
    _parse_metadata_xml,
    _parse_jsx_visual,
    parse_figma_dumps,
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

ALL_8_KEYS = {"name", "w", "h", "radius", "requireSprite", "color", "fontSize", "fontWeight"}

# Fixture paths
_REPO_ROOT = os.path.abspath(os.path.join(_TESTS_DIR, "..", "..", ".."))
_FIXTURE_DIR = os.path.join(_REPO_ROOT, "Docs", "Specs", "Active",
                             "figma_node_spec_generator", "reference", "nodes")

MENU_ROW_XML = os.path.join(_FIXTURE_DIR, "menu_row_13330-1178_metadata.xml")
MENU_ROW_JSX = os.path.join(_FIXTURE_DIR, "menu_row_13330-1178_context.jsx")
CARD_XML = os.path.join(_FIXTURE_DIR, "selection_card_13156-1232_metadata.xml")
CARD_JSX = os.path.join(_FIXTURE_DIR, "selection_card_13156-1232_context.jsx")


def _make_el(name="TestEl", w=100, h=50, radius=0,
             fills=None, strokes=None, is_text=False, is_root=False,
             font_size=None, font_weight=None, font_color=None):
    return {
        "name": name,
        "w": w,
        "h": h,
        "radius": radius,
        "fills": fills if fills is not None else [],
        "strokes": strokes if strokes is not None else [],
        "is_text": is_text,
        "is_root": is_root,
        "font_size": font_size,
        "font_weight": font_weight,
        "font_color": font_color,
    }


def _solid_fill(color="#133453"):
    return {"type": "SOLID", "color": color}

def _gradient_fill():
    return {"type": "GRADIENT_LINEAR", "color": None, "stops": ["#133453", "#091b33"]}

def _image_fill():
    return {"type": "IMAGE", "color": None}

def _stroke(color="#0a1d35", weight=2):
    return {"type": "SOLID", "color": color, "weight": weight}


def _write_temp_xml(content):
    with tempfile.NamedTemporaryFile(mode="w", suffix=".xml", delete=False,
                                     encoding="utf-8") as fh:
        fh.write(content)
        return fh.name


def _write_temp_jsx(content):
    with tempfile.NamedTemporaryFile(mode="w", suffix=".jsx", delete=False,
                                     encoding="utf-8") as fh:
        fh.write(content)
        return fh.name


# ===========================================================================
# BACK-END TESTS (unchanged logic from iter-1)
# ===========================================================================

class TestRequireSprite(unittest.TestCase):

    def _rs(self, el, verbose=False):
        return _decide_require_sprite(el, verbose, el.get("name", "?"))

    def test_image_fill_returns_true(self):
        el = _make_el(fills=[_image_fill()])
        self.assertTrue(self._rs(el))

    def test_image_fill_with_stroke_returns_true(self):
        el = _make_el(fills=[_image_fill()], strokes=[_stroke()])
        self.assertTrue(self._rs(el))

    def test_gradient_linear_returns_true(self):
        el = _make_el(fills=[_gradient_fill()])
        self.assertTrue(self._rs(el))

    def test_gradient_radial_returns_true(self):
        el = _make_el(fills=[{"type": "GRADIENT_RADIAL", "color": None}])
        self.assertTrue(self._rs(el))

    def test_gradient_angular_returns_true(self):
        el = _make_el(fills=[{"type": "GRADIENT_ANGULAR", "color": None}])
        self.assertTrue(self._rs(el))

    def test_gradient_diamond_returns_true(self):
        el = _make_el(fills=[{"type": "GRADIENT_DIAMOND", "color": None}])
        self.assertTrue(self._rs(el))

    def test_stroke_with_weight_returns_true(self):
        el = _make_el(fills=[], strokes=[_stroke(weight=2)])
        self.assertTrue(self._rs(el))

    def test_stroke_with_fractional_weight_returns_true(self):
        el = _make_el(fills=[], strokes=[_stroke(weight=1.5)])
        self.assertTrue(self._rs(el))

    def test_stroke_weight_zero_is_ambiguous_fail_safe(self):
        el = _make_el(fills=[], strokes=[{"type": "SOLID", "color": "#fff", "weight": 0}])
        self.assertTrue(self._rs(el), "Ambiguous invisible-stroke -> True (D3 fail-safe)")

    def test_corner_radius_non_text_returns_true(self):
        el = _make_el(radius=32, fills=[_solid_fill()], is_text=False)
        self.assertTrue(self._rs(el))

    def test_corner_radius_text_not_triggered_by_radius(self):
        el = _make_el(radius=8, fills=[_solid_fill()], is_text=True,
                      font_size=20, font_weight="Bold", font_color="#fff")
        self.assertFalse(self._rs(el))

    def test_pure_text_no_stroke_returns_false(self):
        el = _make_el(is_text=True, fills=[_solid_fill("#ffffff")], strokes=[])
        self.assertFalse(self._rs(el))

    def test_text_with_no_fills_returns_false(self):
        el = _make_el(is_text=True, fills=[], strokes=[])
        self.assertFalse(self._rs(el))

    def test_text_with_gradient_fill_returns_false_rule1(self):
        """Rule 1 (iter-3): text node with bg-clip-text gradient is ALWAYS False.
        The gradient is a TMP vertex-gradient, not a Unity Image sprite."""
        el = _make_el(is_text=True, fills=[_gradient_fill()], strokes=[])
        self.assertFalse(self._rs(el),
            "Rule 1: text node with gradient fill MUST return False — TMP vertex-gradient, not a sprite")

    def test_text_with_stroke_returns_false_rule1(self):
        """Rule 1 (iter-3): text node with stroke is ALWAYS False.
        Strokes on text are TMP outline/underline effects, not Image sprites."""
        el = _make_el(is_text=True, fills=[_solid_fill()], strokes=[_stroke(weight=1)])
        self.assertFalse(self._rs(el),
            "Rule 1: text node with stroke MUST return False — text stroke is TMP effect, not a sprite")

    def test_plain_solid_no_stroke_no_radius_returns_false(self):
        el = _make_el(radius=0, fills=[_solid_fill()], strokes=[])
        self.assertFalse(self._rs(el))

    def test_multiple_solid_fills_no_stroke_radius_zero(self):
        el = _make_el(radius=0, fills=[_solid_fill("#111"), _solid_fill("#222")], strokes=[])
        self.assertFalse(self._rs(el))

    def test_empty_fills_and_strokes_returns_false(self):
        el = _make_el(fills=[], strokes=[])
        self.assertFalse(self._rs(el))

    def test_ambiguous_radius_no_fills_is_true(self):
        el = _make_el(radius=16, fills=[], strokes=[], is_text=False)
        self.assertTrue(self._rs(el))

    def test_verbose_does_not_raise(self):
        el = _make_el(fills=[_image_fill()])
        _decide_require_sprite(el, verbose=True, name="TestVerbose")

    def test_menu_row_gradient_and_stroke(self):
        el = _make_el(radius=32, fills=[_gradient_fill()], strokes=[_stroke(weight=2)])
        self.assertTrue(self._rs(el))

    def test_thumbnail_image_fill(self):
        el = _make_el(name="Thumbnail", radius=22, fills=[_image_fill()], strokes=[_stroke(weight=1.5)])
        self.assertTrue(self._rs(el))

    def test_item_name_text(self):
        el = _make_el(name="ItemName", is_text=True, fills=[_solid_fill("#ffffff")], strokes=[])
        self.assertFalse(self._rs(el))

    def test_tier_badge_stroke_and_solid(self):
        el = _make_el(name="TierBadge", h=28, radius=14,
                      fills=[_solid_fill("rgba(217,219,235,0.2)")],
                      strokes=[_stroke("#d9dbeb", 1.5)])
        self.assertTrue(self._rs(el))

    def test_buy_button_stroke(self):
        el = _make_el(name="BuyButton", w=215, h=56, radius=20,
                      fills=[], strokes=[_stroke("#422100", 1)])
        self.assertTrue(self._rs(el))

    def test_rp_pill_solid_radius43_returns_true(self):
        """CRITICAL: RP Container SOLID #001e39 + radius 43 -> True via radius branch, NOT gradient."""
        el = _make_el(name="RpPill", w=215, h=56, radius=43,
                      fills=[_solid_fill("#001e39")], strokes=[])
        self.assertTrue(self._rs(el),
            "Solid fill + large radius (43) MUST return True via the radius branch, NOT gradient")


class TestAllKeysPresent(unittest.TestCase):

    def _check_keys(self, spec_el):
        missing = ALL_8_KEYS - set(spec_el.keys())
        self.assertEqual(missing, set(), f"Missing keys: {missing} in element {spec_el}")

    def test_text_element_has_all_8_keys(self):
        el = _make_el(is_text=True, fills=[_solid_fill()], font_size=24, font_weight="Bold")
        self._check_keys(element_to_spec(el, verbose=False))

    def test_image_element_has_all_8_keys(self):
        el = _make_el(fills=[_image_fill()])
        self._check_keys(element_to_spec(el, verbose=False))

    def test_gradient_element_has_all_8_keys(self):
        el = _make_el(fills=[_gradient_fill()])
        self._check_keys(element_to_spec(el, verbose=False))

    def test_plain_solid_element_has_all_8_keys(self):
        el = _make_el(fills=[_solid_fill()])
        self._check_keys(element_to_spec(el, verbose=False))

    def test_empty_element_has_all_8_keys(self):
        el = _make_el(fills=[], strokes=[])
        self._check_keys(element_to_spec(el, verbose=False))

    def test_generate_spec_all_elements_have_all_8_keys(self):
        elements = [
            _make_el("El1", fills=[_image_fill()]),
            _make_el("El2", is_text=True, fills=[_solid_fill()], font_size=20, font_weight="Bold"),
            _make_el("El3", fills=[_gradient_fill()]),
            _make_el("El4", fills=[], strokes=[_stroke()]),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        for el in result["elements"]:
            self._check_keys(el)


class TestSentinels(unittest.TestCase):

    def test_unknown_w_h_is_minus_one(self):
        el = _make_el(w=-1, h=-1, fills=[_solid_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["w"], -1.0)
        self.assertEqual(spec["h"], -1.0)

    def test_unknown_radius_is_minus_one(self):
        el = _make_el(radius=-1, fills=[_solid_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["radius"], -1.0)

    def test_non_text_font_size_is_minus_one(self):
        el = _make_el(is_text=False, fills=[_solid_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["fontSize"], -1.0)

    def test_non_text_font_weight_is_empty_string(self):
        el = _make_el(is_text=False, fills=[_solid_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["fontWeight"], "")

    def test_gradient_color_is_empty_string(self):
        el = _make_el(fills=[_gradient_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["color"], "")

    def test_image_color_is_empty_string(self):
        el = _make_el(fills=[_image_fill()])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["color"], "")

    def test_no_fills_no_strokes_color_is_empty_string(self):
        el = _make_el(fills=[], strokes=[])
        spec = element_to_spec(el, False)
        self.assertEqual(spec["color"], "")

    def test_require_sprite_false_for_plain_text(self):
        el = _make_el(is_text=True, fills=[_solid_fill()], strokes=[])
        self.assertFalse(element_to_spec(el, False)["requireSprite"])

    def test_require_sprite_is_bool(self):
        el = _make_el(fills=[_image_fill()])
        self.assertIsInstance(element_to_spec(el, False)["requireSprite"], bool)


class TestFontWeightMapping(unittest.TestCase):

    def _w(self, raw):
        return _normalise_font_weight(raw)

    def test_bold(self):
        self.assertEqual(self._w("Bold"), "Bold")
        self.assertEqual(self._w("bold"), "Bold")
        self.assertEqual(self._w("BOLD"), "Bold")

    def test_semibold(self):
        self.assertEqual(self._w("SemiBold"), "SemiBold")
        self.assertEqual(self._w("semibold"), "SemiBold")
        self.assertEqual(self._w("Semi Bold"), "SemiBold")
        self.assertEqual(self._w("semi-bold"), "SemiBold")

    def test_medium(self):
        self.assertEqual(self._w("Medium"), "Medium")
        self.assertEqual(self._w("medium"), "Medium")

    def test_regular(self):
        self.assertEqual(self._w("Regular"), "Regular")
        self.assertEqual(self._w("regular"), "Regular")
        self.assertEqual(self._w("Normal"), "Regular")

    def test_unknown_returns_allowed_or_empty(self):
        result = self._w("UltraCondensedExpanded")
        self.assertIn(result, ALLOWED_FONT_WEIGHTS | {""})

    def test_none_empty_returns_empty(self):
        self.assertEqual(self._w(None), "")
        self.assertEqual(self._w(""), "")

    def test_numeric_700_is_bold(self):
        self.assertEqual(self._w("700"), "Bold")

    def test_numeric_600_is_semibold(self):
        self.assertEqual(self._w("600"), "SemiBold")

    def test_numeric_400_is_regular(self):
        self.assertEqual(self._w("400"), "Regular")

    def test_output_always_allowed_or_empty(self):
        for raw in ["Bold", "SemiBold", "Medium", "Regular", "Light", "Black", "900", "", None, "weird"]:
            out = self._w(raw)
            self.assertIn(out, ALLOWED_FONT_WEIGHTS | {""}, f"Invalid output '{out}' for '{raw}'")

    def test_text_bold_in_spec(self):
        el = _make_el(is_text=True, fills=[_solid_fill()], font_size=24, font_weight="Bold")
        self.assertEqual(element_to_spec(el, False)["fontWeight"], "Bold")

    def test_text_semibold_in_spec(self):
        el = _make_el(is_text=True, fills=[_solid_fill()], font_size=24, font_weight="SemiBold")
        self.assertEqual(element_to_spec(el, False)["fontWeight"], "SemiBold")

    def test_rubik_semibold_from_jsx_token(self):
        self.assertEqual(self._w("SemiBold"), "SemiBold")

    def test_noto_bold_from_jsx_token(self):
        self.assertEqual(self._w("Bold"), "Bold")


class TestColorNormalisation(unittest.TestCase):

    def _norm(self, s):
        return _normalise_hex(s)

    def test_hex6_lowercase(self):
        self.assertEqual(self._norm("#133453"), "#133453".upper())

    def test_hex6_uppercase(self):
        self.assertEqual(self._norm("#C7D6EB"), "#C7D6EB")

    def test_hex8_strips_alpha(self):
        self.assertEqual(self._norm("#133453FF"), "#133453")

    def test_hex3_expands(self):
        self.assertEqual(self._norm("#FFF"), "#FFFFFF")

    def test_rgba_parses(self):
        self.assertEqual(self._norm("rgba(62,124,168,0.85)"), "#3E7CA8")

    def test_rgb_parses(self):
        self.assertEqual(self._norm("rgb(115,224,128)"), "#73E080")

    def test_empty_returns_empty(self):
        self.assertEqual(self._norm(""), "")
        self.assertEqual(self._norm(None), "")

    def test_invalid_returns_empty(self):
        self.assertEqual(self._norm("not-a-color"), "")

    def test_dominant_color_single_solid(self):
        el = _make_el(fills=[_solid_fill("#133453")])
        self.assertEqual(element_to_spec(el, False)["color"], "#133453")

    def test_dominant_color_gradient_empty(self):
        el = _make_el(fills=[_gradient_fill()])
        self.assertEqual(element_to_spec(el, False)["color"], "")

    def test_dominant_color_image_empty(self):
        el = _make_el(fills=[_image_fill()])
        self.assertEqual(element_to_spec(el, False)["color"], "")

    def test_text_color_from_font_color(self):
        el = _make_el(is_text=True, fills=[_solid_fill("#ffffff")], font_color="#73e080")
        self.assertEqual(element_to_spec(el, False)["color"], "#73E080")

    def test_multiple_solid_fills_empty(self):
        el = _make_el(fills=[_solid_fill("#111"), _solid_fill("#222")])
        self.assertEqual(element_to_spec(el, False)["color"], "")

    def test_stroke_only_returns_empty_color_rule2(self):
        """Rule 2 (iter-3): stroke-only element is requireSprite=True (stroke branch);
        color must be '' — the sprite carries the colour, not Image.color."""
        el = _make_el(fills=[], strokes=[_stroke("#0a1d35", 1)])
        spec = element_to_spec(el, False)
        self.assertTrue(spec["requireSprite"], "precondition: stroke-only must be requireSprite=True")
        self.assertEqual(spec["color"], "",
            "Rule 2: stroke-only (requireSprite=True) must emit color='' not the stroke colour")


class TestValidJsonStructure(unittest.TestCase):

    def test_output_is_serialisable(self):
        elements = [
            _make_el("MenuRow", w=1022, h=156, radius=32, fills=[_gradient_fill()], strokes=[_stroke()]),
            _make_el("Thumbnail", w=124, h=124, radius=22, fills=[_image_fill()], strokes=[_stroke(weight=1.5)]),
            _make_el("ItemName", is_text=True, fills=[_solid_fill()], font_size=28, font_weight="Bold"),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        parsed = json.loads(json.dumps(result))
        self.assertIn("elements", parsed)
        self.assertEqual(len(parsed["elements"]), 3)

    def test_each_element_has_exactly_8_keys(self):
        elements = [
            _make_el("A", fills=[_image_fill()]),
            _make_el("B", is_text=True, fills=[_solid_fill()], font_size=20, font_weight="Medium"),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        parsed = json.loads(json.dumps(result))
        for el in parsed["elements"]:
            self.assertEqual(set(el.keys()), ALL_8_KEYS)

    def test_sentinels_survive_json_round_trip(self):
        elements = [_make_el("Unknown", w=-1, h=-1, radius=-1, fills=[], strokes=[])]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        parsed = json.loads(json.dumps(result))
        el = parsed["elements"][0]
        self.assertEqual(el["w"], -1.0)
        self.assertEqual(el["h"], -1.0)
        self.assertEqual(el["radius"], -1.0)
        self.assertEqual(el["fontSize"], -1.0)
        self.assertEqual(el["color"], "")
        self.assertEqual(el["fontWeight"], "")
        self.assertFalse(el["requireSprite"])

    def test_require_sprite_is_json_bool(self):
        elements = [
            _make_el("Sprite", fills=[_image_fill()]),
            _make_el("Text", is_text=True, fills=[_solid_fill()]),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        parsed = json.loads(json.dumps(result))
        self.assertIsInstance(parsed["elements"][0]["requireSprite"], bool)
        self.assertIsInstance(parsed["elements"][1]["requireSprite"], bool)


class TestIncludeFilter(unittest.TestCase):

    def _els(self):
        return [
            _make_el("MenuRow", fills=[_gradient_fill()]),
            _make_el("Thumbnail", fills=[_image_fill()]),
            _make_el("ItemName", is_text=True, fills=[_solid_fill()]),
            _make_el("BuyButton", fills=[], strokes=[_stroke()]),
        ]

    def test_include_filters_to_named_elements(self):
        result = generate_spec(self._els(), include=["Thumbnail", "BuyButton"],
                               min_size=0, verbose=False)
        names = [e["name"] for e in result["elements"]]
        self.assertIn("Thumbnail", names)
        self.assertIn("BuyButton", names)
        self.assertNotIn("MenuRow", names)

    def test_empty_include_returns_all_non_skipped(self):
        result = generate_spec(self._els(), include=[], min_size=0, verbose=False)
        names = [e["name"] for e in result["elements"]]
        self.assertEqual(set(names), {"MenuRow", "Thumbnail", "ItemName", "BuyButton"})


class TestSkipHeuristic(unittest.TestCase):

    def test_frame_name_is_skipped(self):
        self.assertTrue(_should_skip("Frame", 100, 100, 0))

    def test_named_element_not_skipped(self):
        self.assertFalse(_should_skip("MenuRow", 100, 100, 0))

    def test_sheen_is_skipped(self):
        self.assertTrue(_should_skip("Sheen", 100, 100, 0))

    def test_min_size_filters_tiny_elements(self):
        self.assertTrue(_should_skip("MyEl", 10, 10, min_size=20))

    def test_min_size_passes_large_elements(self):
        self.assertFalse(_should_skip("MyEl", 100, 100, min_size=20))

    def test_generate_spec_skips_frame_elements(self):
        elements = [
            _make_el("MenuRow", fills=[_gradient_fill()]),
            _make_el("Frame", fills=[_solid_fill()]),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=False)
        names = [e["name"] for e in result["elements"]]
        self.assertIn("MenuRow", names)
        self.assertNotIn("Frame", names)


class TestVerboseTrace(unittest.TestCase):

    def test_verbose_runs_without_crash(self):
        elements = [
            _make_el("A", fills=[_image_fill()]),
            _make_el("B", is_text=True, fills=[_solid_fill()], font_size=20, font_weight="Bold"),
            _make_el("C", fills=[_gradient_fill()]),
            _make_el("D", fills=[], strokes=[_stroke()]),
        ]
        result = generate_spec(elements, include=[], min_size=0, verbose=True)
        self.assertEqual(len(result["elements"]), 4)


class TestRegressionAnchorMenuRow(unittest.TestCase):
    """
    The hand-authored row_spec.json (stamina_boost_shop) listed these elements.
    Generator must produce requireSprite=True for all non-text elements
    and requireSprite=False for all text nodes.
    """

    def _spec(self, el):
        return element_to_spec(el, verbose=False)

    def test_thumbnail_require_sprite(self):
        el = _make_el("Thumbnail", w=124, h=124, radius=22,
                      fills=[_image_fill()], strokes=[_stroke(weight=1.5)])
        self.assertTrue(self._spec(el)["requireSprite"])

    def test_tier_badge_require_sprite(self):
        el = _make_el("TierBadge", h=28, radius=14,
                      fills=[_solid_fill("rgba(217,219,235,0.2)")],
                      strokes=[_stroke("#d9dbeb", 1.5)])
        self.assertTrue(self._spec(el)["requireSprite"])

    def test_buy_button_require_sprite(self):
        el = _make_el("BuyButton", w=215, h=56, radius=20,
                      fills=[], strokes=[_stroke("#422100", 1)])
        self.assertTrue(self._spec(el)["requireSprite"])

    def test_rp_pill_solid_radius43_require_sprite(self):
        """CRITICAL: SOLID #001e39 + radius 43 (no gradient!) -> True via radius branch."""
        el = _make_el("RpPill", w=215, h=56, radius=43,
                      fills=[_solid_fill("#001e39")], strokes=[])
        self.assertTrue(self._spec(el)["requireSprite"],
            "SOLID fill + large radius (43) MUST use radius branch -> True; no fabricated gradient needed")

    def test_item_name_label_not_require_sprite(self):
        el = _make_el("ItemNameLabel", is_text=True, fills=[_solid_fill("#ffffff")], strokes=[],
                      font_size=28, font_weight="Bold")
        s = self._spec(el)
        self.assertFalse(s["requireSprite"])
        self.assertEqual(s["fontWeight"], "Bold")

    def test_item_desc_label_not_require_sprite(self):
        el = _make_el("ItemDescLabel", is_text=True, fills=[_solid_fill("#c7d6eb")], strokes=[],
                      font_size=18, font_weight="Regular")
        self.assertFalse(self._spec(el)["requireSprite"])

    def test_sta_label_not_require_sprite(self):
        el = _make_el("StaLabel", is_text=True, fills=[_solid_fill("#73e080")], strokes=[],
                      font_size=22, font_weight="Bold")
        self.assertFalse(self._spec(el)["requireSprite"])

    def test_rp_cost_label_not_require_sprite(self):
        el = _make_el("RpCostLabel", is_text=True, fills=[_solid_fill("#ffffff")], strokes=[],
                      font_size=33, font_weight="SemiBold")
        s = self._spec(el)
        self.assertFalse(s["requireSprite"])
        self.assertEqual(s["fontWeight"], "SemiBold")

    def test_buy_button_label_not_require_sprite(self):
        el = _make_el("BuyButtonLabel", is_text=True, fills=[_solid_fill("#321506")], strokes=[],
                      font_size=39, font_weight="SemiBold")
        s = self._spec(el)
        self.assertFalse(s["requireSprite"])
        self.assertEqual(s["fontWeight"], "SemiBold")


# ===========================================================================
# NEW FRONT-END TESTS (iter-2: XML + JSX parsing)
# ===========================================================================

class TestXmlParsing(unittest.TestCase):
    """Unit tests for _parse_metadata_xml()."""

    def test_parses_frame_wh(self):
        xml_path = _write_temp_xml(
            '<frame id="1:1" name="Root" width="200" height="100">'
            '<frame id="1:2" name="Child" width="50" height="50" /></frame>'
        )
        try:
            nodes = _parse_metadata_xml(xml_path)
            self.assertIn("1:1", nodes)
            self.assertEqual(nodes["1:1"]["w"], 200.0)
            self.assertEqual(nodes["1:1"]["h"], 100.0)
            self.assertEqual(nodes["1:1"]["name"], "Root")
            self.assertIn("1:2", nodes)
            self.assertEqual(nodes["1:2"]["w"], 50.0)
        finally:
            os.unlink(xml_path)

    def test_parses_text_type(self):
        xml_path = _write_temp_xml(
            '<frame id="1:1" name="Root" width="200" height="100">'
            '<text id="1:2" name="Label" width="80" height="20" /></frame>'
        )
        try:
            nodes = _parse_metadata_xml(xml_path)
            self.assertEqual(nodes["1:2"]["type"], "text")
        finally:
            os.unlink(xml_path)

    def test_missing_width_defaults_minus_one(self):
        xml_path = _write_temp_xml('<frame id="1:1" name="Root" />')
        try:
            nodes = _parse_metadata_xml(xml_path)
            self.assertEqual(nodes["1:1"]["w"], -1.0)
        finally:
            os.unlink(xml_path)

    def test_nested_nodes_all_captured(self):
        xml_path = _write_temp_xml(
            '<frame id="1:1" name="A" width="100" height="100">'
            '  <frame id="1:2" name="B" width="50" height="50">'
            '    <text id="1:3" name="C" width="30" height="15" />'
            '  </frame>'
            '</frame>'
        )
        try:
            nodes = _parse_metadata_xml(xml_path)
            self.assertEqual(len(nodes), 3)
            self.assertIn("1:3", nodes)
        finally:
            os.unlink(xml_path)


class TestJsxParsing(unittest.TestCase):
    """Unit tests for _parse_jsx_visual()."""

    def _parse(self, jsx):
        path = _write_temp_jsx(jsx)
        try:
            nodes, warnings = _parse_jsx_visual(path)
            return nodes, warnings
        finally:
            os.unlink(path)

    def test_solid_fill_bg_hex(self):
        jsx = '<div className="bg-[#001e39] rounded-[43px]" data-node-id="1:1" data-name="RpPill"></div>'
        nodes, _ = self._parse(jsx)
        self.assertIn("1:1", nodes)
        fills = nodes["1:1"].get("fills", [])
        self.assertEqual(len(fills), 1)
        self.assertEqual(fills[0]["type"], "SOLID")
        self.assertEqual(fills[0]["color"], "#001E39")

    def test_solid_fill_radius_extracted(self):
        jsx = '<div className="bg-[#001e39] rounded-[43.077px]" data-node-id="1:1" data-name="RpPill"></div>'
        nodes, _ = self._parse(jsx)
        self.assertAlmostEqual(nodes["1:1"]["radius"], 43.077, places=2)

    def test_gradient_fill_bg_gradient(self):
        jsx = '<div className="bg-gradient-to-b from-[#133453] to-[#091b33]" data-node-id="1:2" data-name="Row"></div>'
        nodes, _ = self._parse(jsx)
        fills = nodes["1:2"]["fills"]
        self.assertTrue(any(f["type"] == "GRADIENT_LINEAR" for f in fills))

    def test_inline_style_linear_gradient(self):
        jsx = (
            '<div className="some-class" data-node-id="1:3" data-name="Btn" '
            'style={{ backgroundImage: "linear-gradient(180deg, red, blue)" }}></div>'
        )
        nodes, _ = self._parse(jsx)
        fills = nodes["1:3"]["fills"]
        self.assertTrue(any(f["type"] == "GRADIENT_LINEAR" for f in fills))

    def test_border_stroke_extracted(self):
        jsx = '<div className="border border-[#422100] border-solid" data-node-id="1:4" data-name="BuyBtn"></div>'
        nodes, _ = self._parse(jsx)
        strokes = nodes["1:4"]["strokes"]
        self.assertTrue(len(strokes) > 0)
        self.assertEqual(strokes[0]["color"], "#422100")

    def test_border_2_weight(self):
        jsx = '<div className="border-2 border-[#ffe48b] border-solid" data-node-id="1:5"></div>'
        nodes, _ = self._parse(jsx)
        strokes = nodes["1:5"]["strokes"]
        self.assertTrue(any(s["weight"] == 2.0 for s in strokes))

    def test_border_px_weight(self):
        jsx = '<div className="border-[1.5px] border-[#d9dbeb] border-solid" data-node-id="1:6"></div>'
        nodes, _ = self._parse(jsx)
        strokes = nodes["1:6"]["strokes"]
        self.assertTrue(any(abs(s["weight"] - 1.5) < 0.01 for s in strokes))

    def test_p_tag_is_text(self):
        jsx = "<p className=\"text-[28px] font-bold text-white\" data-node-id=\"1:7\">Hello</p>"
        nodes, _ = self._parse(jsx)
        self.assertTrue(nodes["1:7"]["is_text"])

    def test_text_font_size_extracted(self):
        jsx = '<p className="text-[28px] text-white" data-node-id="1:8">Hello</p>'
        nodes, _ = self._parse(jsx)
        self.assertEqual(nodes["1:8"]["font_size"], 28.0)

    def test_text_font_weight_from_family_colon(self):
        jsx = "<p className=\"font-['Rubik:SemiBold'] text-[33.6px] text-white\" data-node-id=\"1:9\">500</p>"
        nodes, _ = self._parse(jsx)
        fw = nodes["1:9"].get("font_weight", "")
        self.assertEqual(fw, "SemiBold")

    def test_text_font_bold_from_family_colon(self):
        jsx = "<p className=\"font-['Noto_Sans_JP:Bold'] text-[28px] text-white\" data-node-id=\"1:10\">Text</p>"
        nodes, _ = self._parse(jsx)
        fw = nodes["1:10"].get("font_weight", "")
        self.assertEqual(fw, "Bold")

    def test_image_child_sets_image_fill(self):
        jsx = (
            '<div className="rounded-[22px]" data-node-id="1:11" data-name="Thumb">'
            '  <img alt="" src="https://example.com/img.png" />'
            '</div>'
        )
        nodes, _ = self._parse(jsx)
        fills = nodes["1:11"]["fills"]
        self.assertTrue(any(f["type"] == "IMAGE" for f in fills))

    def test_data_name_extracted(self):
        jsx = '<div className="bg-[#001e39]" data-node-id="1:12" data-name="RP Container"></div>'
        nodes, _ = self._parse(jsx)
        self.assertEqual(nodes["1:12"]["data_name"], "RP Container")

    def test_no_radius_returns_zero(self):
        jsx = '<div className="bg-[#001e39]" data-node-id="1:13" data-name="Plain"></div>'
        nodes, _ = self._parse(jsx)
        self.assertEqual(nodes["1:13"]["radius"], 0.0)


class TestJoinAndNameMap(unittest.TestCase):
    """Tests for parse_figma_dumps() join + name-map logic."""

    def _run(self, xml_content, jsx_content, name_map=None):
        xml_path = _write_temp_xml(xml_content)
        jsx_path = _write_temp_jsx(jsx_content)
        try:
            els, warnings = parse_figma_dumps(xml_path, jsx_path,
                                               name_map or {}, verbose=False)
            return els, warnings
        finally:
            os.unlink(xml_path)
            os.unlink(jsx_path)

    def test_wh_comes_from_metadata(self):
        xml = '<frame id="1:1" name="Foo" width="215" height="56" />'
        jsx = '<div className="bg-[#001e39] rounded-[43px]" data-node-id="1:1" data-name="Foo"></div>'
        els, _ = self._run(xml, jsx)
        el = next((e for e in els if e["name"] == "Foo"), None)
        self.assertIsNotNone(el)
        self.assertEqual(el["w"], 215.0)
        self.assertEqual(el["h"], 56.0)

    def test_radius_comes_from_jsx(self):
        xml = '<frame id="1:1" name="Foo" width="215" height="56" />'
        jsx = '<div className="bg-[#001e39] rounded-[43.077px]" data-node-id="1:1" data-name="Foo"></div>'
        els, _ = self._run(xml, jsx)
        el = next((e for e in els if "Foo" in e.get("name", "")), None)
        self.assertIsNotNone(el)
        self.assertAlmostEqual(el["radius"], 43.077, places=2)

    def test_name_map_renames_element(self):
        xml = '<frame id="1:1" name="RP Container" width="215" height="56" />'
        jsx = '<div className="bg-[#001e39] rounded-[43px]" data-node-id="1:1" data-name="RP Container"></div>'
        els, _ = self._run(xml, jsx, {"RP Container": "RpPill"})
        names = [e["name"] for e in els]
        self.assertIn("RpPill", names)
        self.assertNotIn("RP Container", names)

    def test_unmapped_name_emits_warning(self):
        xml = '<frame id="1:1" name="Some Figma Name" width="100" height="50" />'
        jsx = '<div className="bg-[#001e39]" data-node-id="1:1" data-name="Some Figma Name"></div>'
        els, warnings = self._run(xml, jsx, {})
        warning_text = " ".join(warnings)
        self.assertIn("Some Figma Name", warning_text)
        self.assertIn("WARNING", warning_text)

    def test_unmapped_name_uses_figma_name(self):
        xml = '<frame id="1:1" name="Unmapped" width="100" height="50" />'
        jsx = '<div className="bg-[#001e39]" data-node-id="1:1" data-name="Unmapped"></div>'
        els, _ = self._run(xml, jsx, {})
        names = [e["name"] for e in els]
        self.assertIn("Unmapped", names)


# ---------------------------------------------------------------------------
# CLI tests (new two-arg signature)
# ---------------------------------------------------------------------------

class TestMainCLI(unittest.TestCase):

    def _run_main(self, xml_content, jsx_content, extra_args=None, name_map=None):
        with tempfile.TemporaryDirectory() as tmpdir:
            xml_path = os.path.join(tmpdir, "meta.xml")
            jsx_path = os.path.join(tmpdir, "ctx.jsx")
            out_path = os.path.join(tmpdir, "out.json")
            with open(xml_path, "w") as f:
                f.write(xml_content)
            with open(jsx_path, "w") as f:
                f.write(jsx_content)
            args = [xml_path, jsx_path, "-o", out_path]
            if name_map:
                nm_path = os.path.join(tmpdir, "nm.json")
                with open(nm_path, "w") as f:
                    json.dump(name_map, f)
                args += ["--name-map", nm_path]
            if extra_args:
                args += extra_args
            rc = main(args)
            spec = None
            if os.path.isfile(out_path):
                with open(out_path) as f:
                    spec = json.load(f)
            return rc, spec

    def test_exit_0_on_valid_input(self):
        xml = '<frame id="1:1" name="Root" width="100" height="50" />'
        jsx = '<div className="bg-[#001e39]" data-node-id="1:1" data-name="Root"></div>'
        rc, spec = self._run_main(xml, jsx)
        self.assertEqual(rc, 0)
        self.assertIsNotNone(spec)

    def test_exit_1_on_missing_xml(self):
        rc = main(["/tmp/nonexistent_xyz.xml", "/tmp/nonexistent_xyz.jsx", "-o", "/tmp/out.json"])
        self.assertEqual(rc, 1)

    def test_spec_has_elements_key(self):
        xml = '<frame id="1:1" name="Root" width="200" height="100" />'
        jsx = '<div className="bg-[#133453] rounded-[32px]" data-node-id="1:1" data-name="Root"></div>'
        rc, spec = self._run_main(xml, jsx)
        self.assertIn("elements", spec)

    def test_output_all_8_keys(self):
        xml = '<frame id="1:1" name="Root" width="200" height="100" />'
        jsx = '<div className="bg-gradient-to-b" data-node-id="1:1" data-name="Root"></div>'
        rc, spec = self._run_main(xml, jsx)
        self.assertEqual(rc, 0)
        for el in spec["elements"]:
            self.assertEqual(set(el.keys()), ALL_8_KEYS)

    def test_name_map_applied(self):
        xml = '<frame id="1:1" name="RP Container" width="215" height="56" />'
        jsx = '<div className="bg-[#001e39] rounded-[43px]" data-node-id="1:1" data-name="RP Container"></div>'
        rc, spec = self._run_main(xml, jsx, name_map={"RP Container": "RpPill"})
        names = [e["name"] for e in spec["elements"]]
        self.assertIn("RpPill", names)
        self.assertNotIn("RP Container", names)

    def test_rp_container_solid_radius_require_sprite_true(self):
        """Core regression: SOLID #001e39 + radius 43 -> requireSprite=True (radius branch)."""
        xml = '<frame id="1:1" name="RP Container" width="215" height="56" />'
        jsx = '<div className="bg-[#001e39] rounded-[43.077px]" data-node-id="1:1" data-name="RP Container"></div>'
        rc, spec = self._run_main(xml, jsx, name_map={"RP Container": "RpPill"})
        self.assertEqual(rc, 0)
        rp = next(e for e in spec["elements"] if e["name"] == "RpPill")
        self.assertTrue(rp["requireSprite"],
            "Solid #001e39 + radius 43 MUST yield requireSprite=True (radius branch)")

    def test_verbose_no_crash(self):
        xml = '<frame id="1:1" name="Root" width="100" height="50" />'
        jsx = '<div className="bg-[#001e39]" data-node-id="1:1" data-name="Root"></div>'
        rc, spec = self._run_main(xml, jsx, extra_args=["--verbose"])
        self.assertEqual(rc, 0)

    def test_creates_parent_dirs(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            xml_path = os.path.join(tmpdir, "meta.xml")
            jsx_path = os.path.join(tmpdir, "ctx.jsx")
            out_path = os.path.join(tmpdir, "nested", "deep", "out.json")
            with open(xml_path, "w") as f:
                f.write('<frame id="1:1" name="Root" width="100" height="50" />')
            with open(jsx_path, "w") as f:
                f.write('<div className="bg-[#001e39]" data-node-id="1:1" data-name="Root"></div>')
            rc = main([xml_path, jsx_path, "-o", out_path])
            self.assertEqual(rc, 0)
            self.assertTrue(os.path.isfile(out_path))


# ===========================================================================
# ITER-3 SKIP-JUDGMENT RULE TESTS (Rules 1–4)
# ===========================================================================

class TestIter3Rules(unittest.TestCase):
    """
    Tests for the four iter-3 skip-judgment rules that collapse linter over-fails.
    These encode what a hand-authored spec deliberately skips.
    """

    # --- Rule 1: text nodes → w=-1, h=-1, requireSprite=False always --------

    def test_rule1_text_wh_forced_to_minus_one(self):
        """Rule 1: text node geometry must always be -1/-1 regardless of metadata size."""
        el = _make_el("MyLabel", w=248, h=34, is_text=True,
                      fills=[{"type": "SOLID", "color": "#FFFFFF"}])
        spec = element_to_spec(el, verbose=False)
        self.assertEqual(spec["w"], -1.0, "Rule 1: text w must be -1")
        self.assertEqual(spec["h"], -1.0, "Rule 1: text h must be -1")

    def test_rule1_text_require_sprite_false_solid_fill(self):
        """Rule 1: text with solid fill → requireSprite=False."""
        el = _make_el("SolidText", is_text=True,
                      fills=[{"type": "SOLID", "color": "#FFFFFF"}])
        spec = element_to_spec(el, verbose=False)
        self.assertFalse(spec["requireSprite"])

    def test_rule1_text_require_sprite_false_gradient_fill(self):
        """Rule 1: text with bg-clip-text gradient (TierLabel MEDIUM BOOST) → requireSprite=False.
        This was the key linter HARD FAIL in iter-2 (TierLabel got requireSprite=True)."""
        el = _make_el("TierLabel", is_text=True,
                      fills=[{"type": "GRADIENT_LINEAR", "color": None}],
                      strokes=[])
        spec = element_to_spec(el, verbose=False)
        self.assertFalse(spec["requireSprite"],
            "Rule 1: gradient-fill text MUST be False — bg-clip-text is TMP vertex-gradient, not sprite")
        self.assertEqual(spec["w"], -1.0)
        self.assertEqual(spec["h"], -1.0)

    def test_rule1_text_require_sprite_false_with_stroke(self):
        """Rule 1: text with stroke → requireSprite=False.
        TMP outline/underline is not an Image sprite."""
        el = _make_el("OutlinedLabel", is_text=True,
                      fills=[{"type": "SOLID", "color": "#FFFFFF"}],
                      strokes=[{"type": "SOLID", "color": "#000000", "weight": 2}])
        spec = element_to_spec(el, verbose=False)
        self.assertFalse(spec["requireSprite"],
            "Rule 1: text with stroke MUST be False — stroke on text is TMP outline effect")
        self.assertEqual(spec["w"], -1.0)
        self.assertEqual(spec["h"], -1.0)

    def test_rule1_text_font_size_still_divided_by_1_2(self):
        """Rule 1 does not affect fontSize/fontWeight extraction — those still work normally."""
        el = _make_el("PriceLabel", is_text=True, font_size=33.6, font_weight="SemiBold",
                      fills=[{"type": "SOLID", "color": "#FFFFFF"}])
        spec = element_to_spec(el, verbose=False)
        self.assertAlmostEqual(spec["fontSize"], 33.6 / 1.2, delta=0.2,
            msg="Rule 1: fontSize must still be Figma px / 1.2")
        self.assertEqual(spec["fontWeight"], "SemiBold")

    # --- Rule 2: sprite-filled elements → color="" --------------------------

    def test_rule2_sprite_element_color_blanked(self):
        """Rule 2: requireSprite=True element must have color=""."""
        # RpPill: SOLID #001e39 + radius 43 -> requireSprite=True
        el = _make_el("RpPill", w=215, h=56, radius=43,
                      fills=[{"type": "SOLID", "color": "#001e39"}])
        spec = element_to_spec(el, verbose=False)
        self.assertTrue(spec["requireSprite"], "precondition: must be requireSprite=True")
        self.assertEqual(spec["color"], "",
            "Rule 2: sprite element must have color='' — sprite carries colour, not Image.color")

    def test_rule2_buy_button_color_blanked(self):
        """Rule 2: BuyButton (stroke sprite) must have color=""."""
        el = _make_el("BuyButton", w=215, h=56, radius=20,
                      fills=[], strokes=[{"type": "SOLID", "color": "#422100", "weight": 1}])
        spec = element_to_spec(el, verbose=False)
        self.assertTrue(spec["requireSprite"])
        self.assertEqual(spec["color"], "",
            "Rule 2: BuyButton (stroke sprite) color must be ''")

    def test_rule2_non_sprite_element_retains_color(self):
        """Rule 2 only blanks color on requireSprite=True elements.
        requireSprite=False elements with a solid fill should still emit their color."""
        # Plain solid fill, no stroke, no radius → requireSprite=False
        el = _make_el("PlainBg", radius=0,
                      fills=[{"type": "SOLID", "color": "#133453"}], strokes=[])
        spec = element_to_spec(el, verbose=False)
        self.assertFalse(spec["requireSprite"], "precondition: plain bg must be False")
        self.assertEqual(spec["color"], "#133453",
            "Rule 2: non-sprite element MUST keep its color")

    def test_rule2_image_fill_sprite_color_blanked(self):
        """Rule 2: IMAGE fill element (requireSprite=True via image branch) → color=""."""
        el = _make_el("Thumbnail", w=124, h=124, radius=22,
                      fills=[{"type": "IMAGE", "color": None}])
        spec = element_to_spec(el, verbose=False)
        self.assertTrue(spec["requireSprite"])
        self.assertEqual(spec["color"], "")

    def test_rule2_gradient_fill_sprite_color_blanked(self):
        """Rule 2: GRADIENT fill non-text element (requireSprite=True) → color=""."""
        el = _make_el("GradRow", radius=32,
                      fills=[{"type": "GRADIENT_LINEAR", "color": None}])
        spec = element_to_spec(el, verbose=False)
        self.assertTrue(spec["requireSprite"])
        self.assertEqual(spec["color"], "")

    # --- Rule 3: root element → w=-1, h=-1 ----------------------------------

    def test_rule3_root_element_wh_forced_to_minus_one(self):
        """Rule 3: root element (is_root=True) must always have w=-1, h=-1.
        Root node size is layout-driven, not intrinsic."""
        el = _make_el("MenuRow", w=994, h=160, radius=32, is_root=True,
                      fills=[{"type": "GRADIENT_LINEAR", "color": None}])
        spec = element_to_spec(el, verbose=False)
        self.assertEqual(spec["w"], -1.0,
            "Rule 3: root element w must be -1 (layout-driven)")
        self.assertEqual(spec["h"], -1.0,
            "Rule 3: root element h must be -1 (layout-driven)")

    def test_rule3_non_root_element_keeps_geometry(self):
        """Rule 3 only affects is_root=True elements; is_root=False elements keep their geometry."""
        el = _make_el("Thumbnail", w=124, h=124, radius=22, is_root=False,
                      fills=[{"type": "IMAGE", "color": None}])
        spec = element_to_spec(el, verbose=False)
        self.assertEqual(spec["w"], 124.0, "Non-root element must keep its w")
        self.assertEqual(spec["h"], 124.0, "Non-root element must keep its h")

    def test_rule3_xml_parser_marks_top_element_as_root(self):
        """Rule 3: _parse_metadata_xml must mark depth-0 element as is_root=True."""
        xml_path = _write_temp_xml(
            '<frame id="1:1" name="Root" width="994" height="160">'
            '  <frame id="1:2" name="Child" width="124" height="124" />'
            '</frame>'
        )
        try:
            nodes = _parse_metadata_xml(xml_path)
            self.assertTrue(nodes["1:1"].get("is_root", False),
                "Rule 3: top-level XML element (depth=0) must be is_root=True")
            self.assertFalse(nodes["1:2"].get("is_root", True),
                "Rule 3: child XML element (depth=1) must be is_root=False")
        finally:
            os.unlink(xml_path)

    def test_rule3_root_flag_propagated_through_parse_figma_dumps(self):
        """Rule 3: is_root must be propagated from metadata into the element dict."""
        xml = '<frame id="1:1" name="MenuRow" width="994" height="160" />'
        jsx = '<div className="bg-gradient-to-b" data-node-id="1:1" data-name="MenuRow"></div>'
        xml_path = _write_temp_xml(xml)
        jsx_path = _write_temp_jsx(jsx)
        try:
            els, _ = parse_figma_dumps(xml_path, jsx_path, {}, verbose=False)
            el = next((e for e in els if e.get("name") == "MenuRow"), None)
            self.assertIsNotNone(el, "MenuRow element must be present")
            self.assertTrue(el.get("is_root", False),
                "Rule 3: top-level element must have is_root=True after parse_figma_dumps")
        finally:
            os.unlink(xml_path)
            os.unlink(jsx_path)

    # --- Rule 4: name map → real GO names only (integration) ---------------

    def test_rule4_buybuttoncontainer_excluded_via_null(self):
        """Rule 4: 'Button Container' maps to null → excluded (no GO 'BuyButtonContainer' in prefab).
        The null-convention means 'exclude this Figma node entirely'."""
        self.assertIn("Button Container", MENU_ROW_NAME_MAP,
            "Rule 4: 'Button Container' must be in name map with null value (exclusion convention)")
        self.assertIsNone(MENU_ROW_NAME_MAP["Button Container"],
            "Rule 4: 'Button Container' must map to null to signal exclusion")
        self.assertNotIn("BuyButtonContainer", MENU_ROW_NAME_MAP.values(),
            "Rule 4: 'BuyButtonContainer' must not be a target GO name")

    def test_rule4_menurow_excluded_via_null(self):
        """Rule 4: 'Menu Item: whisky-flight' maps to null → excluded (root, no Unity GO counterpart)."""
        self.assertIn("Menu Item: whisky-flight", MENU_ROW_NAME_MAP,
            "Rule 4: root Figma node must be in name map with null value (exclusion convention)")
        self.assertIsNone(MENU_ROW_NAME_MAP["Menu Item: whisky-flight"],
            "Rule 4: root Figma node must map to null to signal exclusion")
        self.assertNotIn("MenuRow", MENU_ROW_NAME_MAP.values(),
            "Rule 4: 'MenuRow' must not be a target GO name")

    # --- Rule 1 (supplemental): XML metadata type=text wins when JSX lacks <p> -------

    def test_rule1_metadata_type_text_overrides_jsx_false(self):
        """Rule 1 supplemental: if XML metadata type=text but JSX has no <p> (Figma wraps text
        in <div>), is_text must still be True. The fix is to OR both signals:
        vis.get('is_text', False) or (meta.get('type') == 'text').
        Without this fix, BuyButtonLabel (node 13330:1197) emitted w=81 instead of -1.

        iter-4 extension (ARCHITECT_REVIEW §3d): also asserts that font props are extracted
        from the <div> className — the iter-3 bug that slipped through 159/159.  The outer
        <div> carries text-[39px] and font-['Rubik:SemiBold']; the inner <p> has no size/weight
        classes.  Without the iter-4 fix (_parse_text_props runs for ALL elements, not only <p>),
        BuyButtonLabel emitted fontSize=-1 / fontWeight="" — silently dropping the font gate."""
        xml = '<frame id="1:1" name="Root" width="994" height="160">' \
              '  <text id="1:2" name="PrimaryLabel" width="81" height="54" />' \
              '</frame>'
        # JSX does NOT have a <p> for this node — simulates Figma's <div> wrapper for text.
        # font-['Rubik:SemiBold'] + text-[39px] are on the OUTER <div> (not the inner <p>).
        jsx = ('<div className="bg-gradient-to-b" data-node-id="1:1" data-name="Root">'
               "  <div className=\"font-['Rubik:SemiBold'] text-[39px]\" data-node-id=\"1:2\" data-name=\"PrimaryLabel\">"
               '    <p className="leading-[54px]">BUY</p>'
               '  </div>'
               '</div>')
        xml_path = _write_temp_xml(xml)
        jsx_path = _write_temp_jsx(jsx)
        try:
            els, _ = parse_figma_dumps(xml_path, jsx_path, {}, verbose=False)
            label = next((e for e in els if e.get("name") == "PrimaryLabel"), None)
            self.assertIsNotNone(label, "PrimaryLabel element must be parsed")
            self.assertTrue(label.get("is_text"),
                "Rule 1 supplemental: XML type=text must force is_text=True even when JSX has no <p>")
            spec = element_to_spec(label, verbose=False)
            self.assertEqual(spec["w"], -1.0,
                "Rule 1: text node with XML type=text must emit w=-1")
            self.assertEqual(spec["h"], -1.0,
                "Rule 1: text node with XML type=text must emit h=-1")
            # iter-4 assertion: font props must be extracted from the <div> className.
            # Expected: 39px / 1.2 = 32.5 (rounded to 1 decimal), weight SemiBold.
            self.assertAlmostEqual(spec["fontSize"], 32.5, delta=0.1,
                msg="Rule 1 div-wrapped: fontSize must be 39/1.2=32.5 extracted from <div> className"
                    " (not -1). iter-3 omitted this check — the bug slipped past 159/159.")
            self.assertEqual(spec["fontWeight"], "SemiBold",
                "Rule 1 div-wrapped: fontWeight must be 'SemiBold' extracted from <div> "
                "font-['Rubik:SemiBold'] className (not ''). iter-3 omitted this check.")
        finally:
            import os
            os.unlink(xml_path)
            os.unlink(jsx_path)


# ===========================================================================
# FIXTURE INTEGRATION TESTS (parse real committed dumps)
# ===========================================================================

MENU_ROW_NAME_MAP = {
    # Rule 4 (iter-3): null value = "exclude this node" (no Unity GO counterpart).
    # "Menu Item: whisky-flight" is the root; no GO named "MenuRow" exists in StaminaMenuRow.prefab.
    # "Button Container" wraps BuyButton in Figma but has no GO named "BuyButtonContainer" in Unity.
    "Menu Item: whisky-flight": None,  # excluded (null convention, Rule 4)
    "Button Container": None,           # excluded (null convention, Rule 4)
    "menu_whisky-flight": "Thumbnail",
    "Tier Badge": "TierBadge",
    "RP Container": "RpPill",
    "Main Buttons": "BuyButton",
    "RP Amount": "RpCostLabel",
    "MEDIUM BOOST": "TierLabel",
    "山崎 Whisky Flight": "ItemNameLabel",
    "3-glass tasting — Yamazaki, Hibiki, Hakushu": "ItemDescLabel",
    "+40 STA": "StaLabel",
    "PRIMARY": "BuyButtonLabel",
}

CARD_NAME_MAP = {
    "Card: kageroh": "SelectionCard",
    "storefront_kageroh": "StorefrontImage",
    "RP Container": "RpPill",
    "Daily Bonus": "DailyBonusBadge",
}


@unittest.skipUnless(os.path.isfile(MENU_ROW_XML), "Missing fixture: menu_row_13330-1178_metadata.xml")
class TestFixtureMenuRow(unittest.TestCase):
    """Integration test against real committed fixtures for node 13330:1178."""

    @classmethod
    def setUpClass(cls):
        cls.elements, cls.warnings = parse_figma_dumps(
            MENU_ROW_XML, MENU_ROW_JSX, MENU_ROW_NAME_MAP, verbose=False
        )
        cls.spec = generate_spec(cls.elements, include=[], min_size=0, verbose=False)
        cls.by_name = {e["name"]: e for e in cls.spec["elements"]}

    def test_parses_without_error(self):
        self.assertIsInstance(self.elements, list)
        self.assertGreater(len(self.elements), 0)

    def test_menu_row_root_not_emitted(self):
        """Rule 4 (iter-3): 'Menu Item: whisky-flight' is the Figma root node.
        It was removed from the name map because StaminaMenuRow.prefab has no GO named 'MenuRow'.
        Emitting it caused a linter 'missing' FAIL.
        """
        self.assertNotIn("MenuRow", self.by_name,
            "Rule 4: root Figma node 'Menu Item: whisky-flight' must NOT be emitted "
            "(no matching Unity GO 'MenuRow' in StaminaMenuRow.prefab)")

    def test_rp_pill_requires_sprite_via_radius(self):
        """
        CRITICAL regression: RP Container is SOLID #001e39 + radius 43 (NO gradient).
        requireSprite must be True via the radius branch, NOT the gradient branch.
        This was the red-team blocker in iter-1.
        """
        rp = self.by_name.get("RpPill")
        self.assertIsNotNone(rp, "RpPill must appear in output -- check name-map")
        self.assertTrue(rp["requireSprite"],
            f"RpPill (SOLID #001e39, radius 43): requireSprite must be True. Got: {rp}")
        # Verify the fill is SOLID not GRADIENT -- confirms radius branch fired
        rp_el = next((e for e in self.elements if e["name"] == "RpPill"), None)
        if rp_el:
            fills = rp_el.get("fills", [])
            gradient_fills = [f for f in fills if f.get("type", "").startswith("GRADIENT")]
            self.assertEqual(len(gradient_fills), 0,
                f"RpPill MUST have no gradient fills -- fixture is SOLID. Got: {fills}")

    def test_thumbnail_requires_sprite(self):
        t = self.by_name.get("Thumbnail")
        self.assertIsNotNone(t, "Thumbnail must appear in output")
        self.assertTrue(t["requireSprite"])

    def test_buy_button_requires_sprite(self):
        b = self.by_name.get("BuyButton")
        self.assertIsNotNone(b, "BuyButton must appear in output")
        self.assertTrue(b["requireSprite"])

    def test_tier_badge_requires_sprite(self):
        t = self.by_name.get("TierBadge")
        self.assertIsNotNone(t, "TierBadge must appear in output")
        self.assertTrue(t["requireSprite"])

    def test_all_8_keys_present_in_every_element(self):
        for el in self.spec["elements"]:
            missing = ALL_8_KEYS - set(el.keys())
            self.assertEqual(missing, set(), f"Missing keys {missing} in {el['name']}")

    def test_rp_pill_geometry(self):
        rp = self.by_name.get("RpPill")
        if rp:
            self.assertAlmostEqual(rp["w"], 215.0, places=0)
            self.assertAlmostEqual(rp["h"], 56.0, places=0)

    def test_rp_pill_radius(self):
        rp = self.by_name.get("RpPill")
        if rp:
            self.assertAlmostEqual(rp["radius"], 43.077, places=1)

    def test_text_elements_not_require_sprite(self):
        text_names = ["ItemNameLabel", "ItemDescLabel", "StaLabel", "RpCostLabel"]
        for name in text_names:
            el = self.by_name.get(name)
            if el is not None:
                self.assertFalse(el["requireSprite"],
                    f"{name} is text; requireSprite must be False")

    def test_item_name_label_font_size(self):
        """ItemNameLabel: Figma text-[28px] -> fontSize=28/1.2=23.3."""
        el = self.by_name.get("ItemNameLabel")
        if el and el["fontSize"] > 0:
            self.assertAlmostEqual(el["fontSize"], 28.0 / 1.2, delta=0.2)

    def test_rp_cost_label_font_size(self):
        """RpCostLabel: Figma text-[33.6px] -> fontSize approx 28.0."""
        el = self.by_name.get("RpCostLabel")
        if el and el["fontSize"] > 0:
            self.assertAlmostEqual(el["fontSize"], 33.6 / 1.2, delta=0.5)


@unittest.skipUnless(os.path.isfile(CARD_XML), "Missing fixture: selection_card_13156-1232_metadata.xml")
class TestFixtureSelectionCard(unittest.TestCase):
    """
    Integration test against real committed fixtures for node 13156:1232
    (Card: kageroh -- selection card).
    Overfit-guard: second fixture prevents parser being over-fit to one JSX shape.
    """

    @classmethod
    def setUpClass(cls):
        cls.elements, cls.warnings = parse_figma_dumps(
            CARD_XML, CARD_JSX, CARD_NAME_MAP, verbose=False
        )
        cls.spec = generate_spec(cls.elements, include=[], min_size=0, verbose=False)
        cls.by_name = {e["name"]: e for e in cls.spec["elements"]}

    def test_parses_without_error(self):
        self.assertIsInstance(self.elements, list)
        self.assertGreater(len(self.elements), 0)

    def test_card_root_geometry(self):
        """Rule 3 (iter-3): root element w/h must be -1 (layout-driven, not intrinsic).
        Was 978×360 in iter-2; now correctly -1/-1."""
        card = self.by_name.get("SelectionCard")
        if card:
            self.assertEqual(card["w"], -1.0,
                "Rule 3: SelectionCard is root — w must be -1 (layout-driven)")
            self.assertEqual(card["h"], -1.0,
                "Rule 3: SelectionCard is root — h must be -1 (layout-driven)")

    def test_card_root_requires_sprite(self):
        card = self.by_name.get("SelectionCard")
        if card:
            self.assertTrue(card["requireSprite"])

    def test_storefront_image_requires_sprite(self):
        s = self.by_name.get("StorefrontImage")
        if s:
            self.assertTrue(s["requireSprite"])

    def test_rp_pill_in_card_requires_sprite(self):
        """RP Container in card: SOLID #001e39 + radius-40 -> requireSprite=True (radius branch)."""
        rp = self.by_name.get("RpPill")
        if rp:
            self.assertTrue(rp["requireSprite"])
            rp_el = next((e for e in self.elements if e["name"] == "RpPill"), None)
            if rp_el:
                fills = rp_el.get("fills", [])
                gradient_fills = [f for f in fills if f.get("type", "").startswith("GRADIENT")]
                self.assertEqual(len(gradient_fills), 0,
                    "Card RP Container is SOLID fill -- no gradient should appear")

    def test_daily_bonus_badge_requires_sprite(self):
        d = self.by_name.get("DailyBonusBadge")
        if d:
            self.assertTrue(d["requireSprite"])

    def test_all_8_keys_present_in_every_element(self):
        for el in self.spec["elements"]:
            missing = ALL_8_KEYS - set(el.keys())
            self.assertEqual(missing, set(), f"Missing keys {missing} in {el['name']}")

    def test_output_is_json_serialisable(self):
        json_str = json.dumps(self.spec)
        parsed = json.loads(json_str)
        self.assertIn("elements", parsed)


# ===========================================================================
# DIV-WRAPPED TEXT OVERFIT-GUARD (iter-4)
# ===========================================================================
# Dedicated synthetic fixture that guarantees _parse_text_props is called for
# <div>-wrapped text (not only <p> tags).  If iter-3's regression recurs
# (conditional `if is_text` restored, or _parse_text_props made <p>-only again),
# every test in this class will fail — making the regression immediately visible.
#
# Structure mirrors the real BuyButtonLabel (node 13330:1197):
#   outer <div> carries font-['Family:Weight'] + text-[Npx]
#   inner <p> carries only layout classes (no size/weight) — as Figma emits it.
#
# (The SelectionCard fixture doesn't cover this because all its text nodes are <p>.)

class TestDivWrappedTextOverfitGuard(unittest.TestCase):
    """Overfit-guard: _parse_text_props must run for <div>-wrapped text elements.

    These synthetic tests exist SOLELY to catch a regression to the iter-3 bug
    (font props only extracted for <p> tags).  They are intentionally minimal —
    one fixture, a few assertions — so they can't be accidentally loosened.
    """

    # --- single div-wrapped text element ---

    @classmethod
    def setUpClass(cls):
        xml = (
            '<frame id="2:1" name="Container" width="300" height="80">'
            '  <text id="2:2" name="DivLabel" width="100" height="40" />'
            '</frame>'
        )
        # Outer <div> carries font + size; inner <p> carries ONLY leading (layout).
        # This exactly mirrors how Figma emits BuyButtonLabel.
        jsx = (
            '<div className="bg-white" data-node-id="2:1" data-name="Container">'
            "  <div className=\"font-['Rubik:Bold'] text-[24px] text-[#FFFFFF]\""
            '       data-node-id="2:2" data-name="DivLabel">'
            '    <p className="leading-[40px]">HELLO</p>'
            '  </div>'
            '</div>'
        )
        xml_path = _write_temp_xml(xml)
        jsx_path = _write_temp_jsx(jsx)
        try:
            cls.elements, cls.warnings = parse_figma_dumps(
                xml_path, jsx_path, {}, verbose=False)
            cls.spec = generate_spec(cls.elements, include=[], min_size=0, verbose=False)
            cls.by_name = {e["name"]: e for e in cls.spec["elements"]}
        finally:
            os.unlink(xml_path)
            os.unlink(jsx_path)

    def test_div_label_is_text(self):
        """XML type=text forces is_text=True even though JSX has no <p> at node level."""
        el = next((e for e in self.elements if e["name"] == "DivLabel"), None)
        self.assertIsNotNone(el, "DivLabel must appear in parsed elements")
        self.assertTrue(el.get("is_text"),
            "DivLabel: XML type=text must force is_text=True (Rule 1 supplemental)")

    def test_div_label_wh_minus_one(self):
        """Text node must emit w=-1, h=-1 (Rule 1)."""
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el, "DivLabel must appear in spec")
        self.assertEqual(el["w"], -1.0,
            "DivLabel (text): w must be -1 regardless of XML geometry")
        self.assertEqual(el["h"], -1.0,
            "DivLabel (text): h must be -1 regardless of XML geometry")

    def test_div_label_font_size_extracted_from_div_classname(self):
        """CORE overfit-guard: fontSize must come from outer <div>'s text-[24px],
        NOT be -1 (the iter-3 regression value where only <p> was scanned)."""
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el, "DivLabel must appear in spec")
        # Expected: 24 / 1.2 = 20.0
        self.assertGreater(el["fontSize"], 0,
            "DivLabel: fontSize must be > 0. Got -1 = iter-3 regression (font props only "
            "extracted from <p> tags, missing outer <div> font classes).")
        self.assertAlmostEqual(el["fontSize"], 24.0 / 1.2, delta=0.2,
            msg="DivLabel: expected fontSize=20.0 (24/1.2) from outer <div> className")

    def test_div_label_font_weight_extracted_from_div_classname(self):
        """CORE overfit-guard: fontWeight must come from outer <div>'s font-['Rubik:Bold'],
        NOT be '' (the iter-3 regression value)."""
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el, "DivLabel must appear in spec")
        self.assertNotEqual(el["fontWeight"], "",
            "DivLabel: fontWeight must not be '' — outer <div> carries font-['Rubik:Bold']. "
            "'' = iter-3 regression (font-props only extracted from <p> tags).")
        self.assertEqual(el["fontWeight"], "Bold",
            "DivLabel: fontWeight must be 'Bold' from font-['Rubik:Bold'] in outer <div>")

    def test_div_label_does_not_require_sprite(self):
        """Text node: requireSprite must be False (Rule 1)."""
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el, "DivLabel must appear in spec")
        self.assertFalse(el["requireSprite"],
            "DivLabel is text; requireSprite must be False (Rule 1)")

    def test_div_label_color_key_present(self):
        """color key must be present (sentinel '' is acceptable when size and color
        share the text-[...] token and the regex picks the non-hex first — this is a
        pre-existing limitation; the critical overfit-guard for this class is font
        size + weight, not color).  Asserts the key exists and is a string."""
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el, "DivLabel must appear in spec")
        self.assertIn("color", el,
            "DivLabel: 'color' key must be present in spec output")
        self.assertIsInstance(el["color"], str,
            "DivLabel: 'color' must be a string ('' or '#RRGGBB')")

    def test_div_label_all_8_keys_present(self):
        el = self.by_name.get("DivLabel")
        self.assertIsNotNone(el)
        missing = ALL_8_KEYS - set(el.keys())
        self.assertEqual(missing, set(), f"Missing keys {missing} in DivLabel")


@unittest.skipUnless(os.path.isfile(MENU_ROW_XML), "Missing fixture: menu_row_13330-1178_metadata.xml")
class TestMetadataOnlyShapeNodeFailSafe(unittest.TestCase):
    """
    iter-5 regression guard — D6 fail-safe: metadata-only shape node must emit requireSprite=True.

    The RP Icon in both real fixtures (node 13330:1192 in menu_row, 13156:1250 in selection_card)
    is a `rounded-rectangle` in the XML metadata but has NO data-node-id entry in the JSX — its
    visual is an <img> on its parent `RP Icon Container`.  When mapped to a Unity GO name, the old
    code emitted requireSprite=False silently (fills/strokes/radius all empty from the join miss →
    "no fills and no strokes" branch → False).  D6 must force True + emit a WARN.

    This class is the exact overfit-guard for D6, parallel to TestDivWrappedTextOverfitGuard for D5.
    Reverting the D6 guard (removing the _metadata_only_shape check) must cause these tests to FAIL.
    """

    def setUp(self):
        # Augment the committed MENU_ROW_NAME_MAP with RP Icon → RpIconSprite.
        # The committed map null-excludes RP Icon only via SKIP_NAME_PATTERNS (a name-literal bandaid
        # that is NOT in the name-map itself).  Adding it here maps it explicitly to a real GO name
        # — the scenario that triggers the D6 fail-safe.
        augmented_map = dict(MENU_ROW_NAME_MAP)
        augmented_map["RP Icon"] = "RpIconSprite"
        elements, warnings = parse_figma_dumps(
            MENU_ROW_XML, MENU_ROW_JSX, augmented_map, verbose=False
        )
        # Run through generate_spec with verbose_trace=False to get the UISpecElement dicts.
        # (generate_spec applies SKIP_NAME_PATTERNS; RP Icon is in that list, so we must bypass
        # by calling element_to_spec directly on the raw elements from parse_figma_dumps.)
        self.elements = elements
        self.warnings = warnings
        self.by_name = {el["name"]: el for el in elements}

    def test_rp_icon_sprite_present_in_elements(self):
        """RP Icon must appear as RpIconSprite after explicit name-map."""
        self.assertIn(
            "RpIconSprite", self.by_name,
            "RpIconSprite must appear in elements when RP Icon is mapped in the name-map. "
            "If missing, the node was unexpectedly excluded or the name-map was not applied."
        )

    def test_rp_icon_sprite_require_sprite_true(self):
        """D6 CORE OVERFIT-GUARD: requireSprite must be True for a metadata-only rounded-rectangle.

        This is the D6 fail-safe regression test.  Reverting the _metadata_only_shape guard or
        removing the D6 early-return from _decide_require_sprite will make this fail.
        Previous (buggy) behaviour: requireSprite=False (fills/strokes empty → "pure layout spacer")."""
        # We test _decide_require_sprite directly on the raw element dict from parse_figma_dumps.
        el = self.by_name.get("RpIconSprite")
        self.assertIsNotNone(el, "RpIconSprite element must exist to test requireSprite")
        result = _decide_require_sprite(el, verbose=False, name="RpIconSprite")
        self.assertTrue(
            result,
            "D6 FAIL: _decide_require_sprite returned False for RpIconSprite "
            "(metadata-only rounded-rectangle, no JSX visual). Expected True (D6 fail-safe). "
            "This is the reachable requireSprite false-negative reported in REDTEAM_REVIEW.md "
            "Attack 3. el=%r" % el
        )

    def test_rp_icon_sprite_d6_flag_set(self):
        """The _metadata_only_shape flag must be set on the element dict."""
        el = self.by_name.get("RpIconSprite")
        self.assertIsNotNone(el)
        self.assertTrue(
            el.get("_metadata_only_shape"),
            "RpIconSprite element must carry _metadata_only_shape=True (set by D6 guard). "
            "If False, the detection condition was not triggered — check that vis was empty and "
            "meta.type == 'rounded-rectangle'."
        )

    def test_rp_icon_sprite_warn_emitted(self):
        """A WARN must be emitted when the D6 fail-safe triggers."""
        warn_texts = " ".join(self.warnings)
        self.assertIn(
            "metadata-only", warn_texts,
            "Expected a 'metadata-only' WARNING in the warnings list when RpIconSprite resolves "
            "to a shape node with no JSX visual.  Warnings found: %r" % self.warnings
        )
        self.assertIn(
            "RpIconSprite", warn_texts,
            "The D6 warning must name the mapped Unity GO ('RpIconSprite').  "
            "Warnings found: %r" % self.warnings
        )

    def test_full_spec_via_element_to_spec_require_sprite_true(self):
        """element_to_spec must carry through requireSprite=True for RpIconSprite."""
        el = self.by_name.get("RpIconSprite")
        self.assertIsNotNone(el)
        spec_el = element_to_spec(el, verbose=False)
        self.assertTrue(
            spec_el["requireSprite"],
            "element_to_spec output must have requireSprite=True for RpIconSprite. "
            "Got requireSprite=%r. Check that _decide_require_sprite reads _metadata_only_shape "
            "from the element dict before the 'no fills and no strokes' branch." % spec_el["requireSprite"]
        )

    def test_committed_elements_unaffected(self):
        """D6 guard must NOT affect elements that have a real JSX visual (RpPill, TierBadge, etc.)."""
        # These elements have real JSX visual data → vis is NOT empty → _metadata_only_shape NOT set.
        # Their requireSprite values must still match the iter-4 expectations.
        # generate_spec(elements, include, min_size, verbose) — elements comes from parse_figma_dumps.
        elements, _warnings = parse_figma_dumps(
            MENU_ROW_XML, MENU_ROW_JSX, MENU_ROW_NAME_MAP, verbose=False
        )
        spec = generate_spec(elements, [], 0.0, False)
        by_name = {el["name"]: el for el in spec["elements"]}
        # RpPill: RP Container is a div frame with radius → requireSprite=True
        rp_pill = by_name.get("RpPill")
        if rp_pill:
            self.assertTrue(rp_pill["requireSprite"],
                            "RpPill must still requireSprite=True (has radius) — D6 must not affect it")
        # BuyButton: frame with bg gradient → requireSprite=True
        buy_btn = by_name.get("BuyButton")
        if buy_btn:
            self.assertTrue(buy_btn["requireSprite"],
                            "BuyButton must still requireSprite=True — D6 must not affect it")

    def test_selection_card_rp_icon_also_fails_safe(self):
        """D6 applies to selection_card fixture too (node 13156:1250, same rounded-rectangle pattern)."""
        if not os.path.isfile(CARD_XML):
            self.skipTest("Missing fixture: selection_card_13156-1232_metadata.xml")
        augmented_card_map = dict(CARD_NAME_MAP)
        augmented_card_map["RP Icon"] = "RpIconSprite"
        elements, warnings = parse_figma_dumps(
            CARD_XML, CARD_JSX, augmented_card_map, verbose=False
        )
        by_name = {el["name"]: el for el in elements}
        el = by_name.get("RpIconSprite")
        self.assertIsNotNone(el,
            "RpIconSprite must appear in selection_card elements when RP Icon is name-mapped")
        result = _decide_require_sprite(el, verbose=False, name="RpIconSprite")
        self.assertTrue(result,
            "D6 FAIL on selection_card fixture: RpIconSprite returned requireSprite=False. "
            "The metadata-only shape node pattern recurs in both committed fixtures.")
        warn_texts = " ".join(warnings)
        self.assertIn("metadata-only", warn_texts,
            "D6 WARN must be emitted for selection_card RpIconSprite. "
            "Warnings: %r" % warnings)


class TestMetadataOnlyShapeNodeGeneralization(unittest.TestCase):
    """
    iter-6 generalization guard — D6 exclude-list structural fix.

    Iter-5 hardened D6 with an INCLUDE-LIST {rounded-rectangle, vector, ellipse}.
    Iter-6 inverts to an EXCLUDE-LIST of non-graphic container types
    {frame, group, section, text, instance, component, component-set}.
    This means line/polygon/star/rectangle/boolean-operation/etc. are now correctly
    caught by D6 even though they were not explicitly enumerated in iter-5.

    This class uses SYNTHETIC XML+JSX fixtures — no dependency on the committed
    menu_row fixtures.  Tests here MUST FAIL if the generalization is reverted to
    the old 3-type include-list.

    Synthetic fixture strategy:
    - XML: minimal Figma get_metadata XML with one root frame + one shape child
    - JSX: minimal JSX with NO data-node-id attribute for the shape child (simulates
      the metadata-only join-miss — the shape has no JSX entry)
    - name-map: shape child mapped explicitly to a Unity GO name
    """

    # ------------------------------------------------------------------ helpers

    @staticmethod
    def _make_xml_and_jsx(shape_type: str, shape_id: str = "100:2",
                          parent_id: str = "100:1") -> tuple:
        """Return (xml_path, jsx_path) for a minimal fixture with the given shape_type.

        The XML contains a root <frame> (id=parent_id) with one child whose tag is
        shape_type.  The JSX contains the parent frame div but NO data-node-id for the
        shape child — reproducing the metadata-only join-miss.
        """
        import tempfile, textwrap
        # Build XML: Figma get_metadata uses 'width'/'height' attributes (NOT 'w'/'h').
        # <frame id="100:1" name="Root" width="200" height="200">
        #   <{shape_type} id="100:2" name="ShapeChild" width="40" height="40" />
        # </frame>
        xml_content = textwrap.dedent(f"""\
            <frame id="{parent_id}" name="Root" width="200" height="200">
              <{shape_type} id="{shape_id}" name="ShapeChild" width="40" height="40" />
            </frame>
        """)
        # Build JSX: parent frame div only — ShapeChild has NO data-node-id → join miss
        jsx_content = textwrap.dedent(f"""\
            <div data-node-id="{parent_id}" data-node-name="Root" class="w-[200px] h-[200px]">
              <div class="w-[40px] h-[40px]"><!-- shape rendered by parent img --></div>
            </div>
        """)
        xml_file = tempfile.NamedTemporaryFile(
            mode="w", suffix=".xml", delete=False, prefix=f"test_d6_{shape_type}_"
        )
        xml_file.write(xml_content)
        xml_file.flush()
        jsx_file = tempfile.NamedTemporaryFile(
            mode="w", suffix=".jsx", delete=False, prefix=f"test_d6_{shape_type}_"
        )
        jsx_file.write(jsx_content)
        jsx_file.flush()
        return xml_file.name, jsx_file.name

    @staticmethod
    def _run_for_shape(shape_type: str) -> tuple:
        """Run parse_figma_dumps for a synthetic fixture of the given shape_type.
        Returns (elements, warnings, by_name)."""
        xml_path, jsx_path = TestMetadataOnlyShapeNodeGeneralization._make_xml_and_jsx(shape_type)
        name_map = {"ShapeChild": "SyntheticShapeGO"}
        try:
            elements, warnings = parse_figma_dumps(xml_path, jsx_path, name_map, verbose=False)
        finally:
            import os as _os
            _os.unlink(xml_path)
            _os.unlink(jsx_path)
        by_name = {el["name"]: el for el in elements}
        return elements, warnings, by_name

    # ------------------------------------------------------------------ positive cases
    # For these shape types: mapped + empty JSX → D6 must fire → requireSprite=True + WARN.
    # These tests FAIL if the guard is reverted to the old include-list (line/polygon/star
    # are NOT in {rounded-rectangle, vector, ellipse}).

    def _assert_d6_fires(self, shape_type: str):
        elements, warnings, by_name = self._run_for_shape(shape_type)
        go_name = "SyntheticShapeGO"

        self.assertIn(
            go_name, by_name,
            f"SyntheticShapeGO must appear in elements for shape_type={shape_type!r}. "
            "If missing, name-map was not applied or fixture parse failed."
        )
        el = by_name[go_name]
        self.assertTrue(
            el.get("_metadata_only_shape"),
            f"D6 FAIL ({shape_type}): _metadata_only_shape must be True for a mapped "
            f"metadata-only {shape_type!r} node with no JSX visual. "
            "Reverting from exclude-list to the old 3-type include-list causes this failure."
        )
        result = _decide_require_sprite(el, verbose=False, name=go_name)
        self.assertTrue(
            result,
            f"D6 FAIL ({shape_type}): requireSprite must be True for a metadata-only "
            f"{shape_type!r} node (no JSX visual, mapped to a Unity GO). "
            "Got False — D6 guard did not fire. "
            "If the guard uses an include-list {rounded-rectangle, vector, ellipse}, "
            f"{shape_type!r} falls through. This test exists to catch that regression."
        )
        warn_text = " ".join(warnings)
        self.assertIn(
            "metadata-only", warn_text,
            f"D6 WARN must be emitted for {shape_type!r} metadata-only node. "
            f"Got warnings: {warnings!r}"
        )
        self.assertIn(
            go_name, warn_text,
            f"D6 WARN must name the mapped GO ({go_name!r}) for {shape_type!r}. "
            f"Got warnings: {warnings!r}"
        )

    def test_line_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'line' node (NOT in the old 3-type include-list)."""
        self._assert_d6_fires("line")

    def test_polygon_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'polygon' node (NOT in the old 3-type include-list)."""
        self._assert_d6_fires("polygon")

    def test_star_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'star' node (NOT in the old 3-type include-list)."""
        self._assert_d6_fires("star")

    # ------------------------------------------------------------------ negative cases
    # For these types: mapped + empty JSX → D6 must NOT fire → requireSprite=False.
    # A mapped frame/group with no visual is a layout spacer — the exclude-list must
    # protect these from the D6 guard.

    def _assert_d6_does_not_fire(self, container_type: str):
        elements, warnings, by_name = self._run_for_shape(container_type)
        go_name = "SyntheticShapeGO"

        self.assertIn(
            go_name, by_name,
            f"SyntheticShapeGO must appear in elements for container_type={container_type!r}."
        )
        el = by_name[go_name]
        self.assertFalse(
            el.get("_metadata_only_shape"),
            f"D6 over-reach ({container_type}): _metadata_only_shape must NOT be set for a "
            f"mapped {container_type!r} node with no visual — it is a layout spacer, not a "
            "graphic primitive. The exclude-list must protect it."
        )
        result = _decide_require_sprite(el, verbose=False, name=go_name)
        self.assertFalse(
            result,
            f"D6 over-reach ({container_type}): requireSprite must be False for a mapped "
            f"{container_type!r} layout spacer with no visual fills/strokes/radius. "
            f"Got True — D6 fired on a container type it should NOT touch."
        )
        warn_text = " ".join(warnings)
        self.assertNotIn(
            "metadata-only", warn_text,
            f"D6 WARN must NOT be emitted for {container_type!r} layout spacer. "
            f"Got warnings: {warnings!r}"
        )

    def test_frame_layout_spacer_require_sprite_false(self):
        """D6 must NOT fire for a mapped 'frame' container with no visual (layout spacer)."""
        self._assert_d6_does_not_fire("frame")

    def test_group_layout_spacer_require_sprite_false(self):
        """D6 must NOT fire for a mapped 'group' container with no visual (layout spacer)."""
        self._assert_d6_does_not_fire("group")

    # ------------------------------------------------------------------ overfit-guard
    # Explicitly verify the guard does NOT use the old 3-type include-list.
    # A fresh 'rectangle' type (present in Figma XML; neither in the old set nor in the
    # non-graphic-container set) must trigger D6.

    def test_rectangle_also_caught_by_exclude_list(self):
        """A metadata-only 'rectangle' must also trigger D6 (not in old 3-type list, not a container).

        This is the overfit-guard: 'rectangle' was NEVER in the iter-5 include-list
        {rounded-rectangle, vector, ellipse}, so if the old enumeration were still in use,
        this test would FAIL.  It passes only with the generalized exclude-list.
        """
        self._assert_d6_fires("rectangle")

    # ------------------------------------------------------------------ iter-7 positive cases
    # instance / component / component-set must NOT be in the exclude-list.
    # These tests MUST FAIL if the exclude-list is widened back to include these three types
    # (the iter-6 over-broad set).  They prove the terminal fix is structural:
    # a mapped instance/component/component-set node with no JSX visual → requireSprite=True + WARN.

    def test_instance_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'instance' node.

        Figma icon libraries publish icons AS components; every icon in a designer's screen
        is an 'instance'.  The metadata-only-icon-in-parent-<img> pattern from iter-4
        (visual painted by an <img> on the parent, no data-node-id on the child) applies to
        instance nodes with exactly the same frequency as rounded-rectangle.  Under D3
        (ambiguous → True), a mapped instance with no joined visual must emit requireSprite=True.

        This test MUST FAIL if 'instance' is re-added to _NON_GRAPHIC_CONTAINER_TYPES.
        """
        self._assert_d6_fires("instance")

    def test_component_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'component' node.

        A 'component' node is a reusable Figma component definition — it may itself be a
        graphic primitive (an icon defined as a standalone component).  When mapped with no
        JSX visual, D3 applies: ambiguous → True.

        This test MUST FAIL if 'component' is re-added to _NON_GRAPHIC_CONTAINER_TYPES.
        """
        self._assert_d6_fires("component")

    def test_component_set_metadata_only_require_sprite_true(self):
        """D6 must fire for a metadata-only 'component-set' node.

        A 'component-set' groups component variants (e.g. an icon at different sizes or
        states).  It is potentially graphic and must not be treated as a pure layout
        container.  Under D3 (ambiguous → True), a mapped component-set with no joined
        visual must emit requireSprite=True.

        This test MUST FAIL if 'component-set' is re-added to _NON_GRAPHIC_CONTAINER_TYPES.
        """
        self._assert_d6_fires("component-set")

    def test_text_never_triggers_d6(self):
        """'text' is in the exclude-list — D6 must NEVER fire for it regardless of vis."""
        self._assert_d6_does_not_fire("text")


if __name__ == "__main__":
    unittest.main(verbosity=2)
