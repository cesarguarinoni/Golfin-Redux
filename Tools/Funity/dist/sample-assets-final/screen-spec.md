# Sample Screen

## Screen

- Canvas: 390 x 844
- Source: figma-json

## Fonts

- Inter: weights 600, 700

## Assets

- No downloaded image assets

## Hierarchy

- Sample Screen [FRAME] size 390 x 844 at (0, 0)
  - Header [FRAME] size 342 x 96 at (24, 24)
    - Title [TEXT] size 180 x 32 at (20, 24)
      text "Welcome back" / Inter 28px / #FFFAF4FF
  - CTA Button [FRAME] size 200 x 56 at (24, 156)
    - CTA Label [TEXT] size 136 x 22 at (32, 17)
      text "Start Session" / Inter 18px / #FFFCF6FF

## Unity Mapping Notes

- Convert Figma top-left coordinates to Unity anchored positions.
- Map text nodes to TextMeshProUGUI or UI Toolkit Labels.
- Map fills to Image components or VisualElement backgrounds.
- Use the generated YAML as the deterministic scene contract.