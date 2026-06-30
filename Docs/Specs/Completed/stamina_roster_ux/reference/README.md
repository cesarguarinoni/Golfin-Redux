# Reference renders — `stamina_roster_ux`

Figma MCP is **live** — pull the canonical render at step 0 rather than trusting a stale PNG (Lesson AK).

- **File key:** `5gEAHjl6xAtW8iYY7NMvWd`
- **Frame:** `Roster Screen Shae` — node `4065:14999`
- **Parameters group (the bars/meter):** `4059:7070`
  - Strength `4059:7071` — ghost `4059:7080`, effective `4059:7082`
  - Club Control `4059:7090`
  - Recovery `4059:7109` (mockup ghost `4300:54910` → IGNORE)
  - Stamina meter `4059:7126` — low-state fills `4059:7135` / `4059:7137`

Pull:
```
get_screenshot(fileKey=5gEAHjl6xAtW8iYY7NMvWd, nodeId=4065:14999)
get_design_context(fileKey=5gEAHjl6xAtW8iYY7NMvWd, nodeId=4059:7070, clientFrameworks=unity, clientLanguages=csharp)
```

The mockup is NOT threshold-accurate (orange drawn at 33% where the live color is yellow). Behavior/thresholds = SPEC.md + `Docs/Design/stamina_economy.csv`; look (gradients, alpha, geometry) = the node. Asset URLs from `get_design_context` expire in 7 days.
