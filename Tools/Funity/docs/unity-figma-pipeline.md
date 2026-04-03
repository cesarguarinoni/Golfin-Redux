# Unity Figma Pipeline

## Goal

Recreate a Figma screen in Unity as closely as possible by generating deterministic artifacts from Figma source data.

## Inputs

- Figma REST API response JSON
- Node-specific JSON exports
- Future: direct API fetch by file key and node id

## Internal Model

The converter normalizes Figma into a scene graph with:

- Hierarchy
- Relative coordinates
- Width and height
- Typography
- Fill and stroke values
- Image/gradient hints
- Effects metadata

This intermediate model is intentionally renderer-agnostic so the same source can drive:

- Unity UGUI generation
- UI Toolkit generation
- YAML inspection
- Markdown spec docs

## Pixel-Perfect Constraints

To approach pixel-perfect parity in Unity, the final product should support:

- RectTransform anchoring translated from Figma coordinates
- Auto Layout mapped to Horizontal/Vertical/LayoutGroup rules
- TextMeshPro font asset mapping by family and weight
- Exact RGBA color preservation
- Nine-slice sprites or shader-backed rounded corners
- Asset export and sprite assignment
- Figma screenshot diffing for validation

## Recommended Generator Outputs

### 1. C# importer

Primary execution artifact inside Unity.

- Creates hierarchy
- Applies RectTransform positions
- Applies Image/TextMeshPro components
- Leaves explicit comments where custom Unity assets are required

### 2. YAML scene contract

Machine-readable checkpoint for debugging and deterministic re-generation.

### 3. Spec document

Human-readable fallback for engineers and designers reviewing parity.

## Biggest Gaps To Close Next

1. Figma API ingestion with personal access tokens
2. Asset downloading and sprite atlas strategy
3. Auto Layout translation
4. Font asset registry for TextMeshPro
5. Screenshot-based pixel-diff testing
