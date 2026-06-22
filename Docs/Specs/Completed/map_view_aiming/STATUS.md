DONE

# STATUS — `map_view_aiming` (Order 352) — DONE (Cesar approved 2026-06-22)

Cesar: "Mark as done." Feature approved and working in real play: tap the hole-map thumbnail → hero-angle overlay; touch-follow aim (free placement, red guide >120% club carry); terrain-conforming red→green landing zone (ZTest-Always shader, renders over terrain/trees); SHOOT button closes (no aim move, no Club Selection); camera repositions to the chosen aim on close; club selection shows a full bag with real distances.

## Close-out
- Moved Active → Completed.
- Stopgap flagged for architect: `Docs/Specs/Queued/club_bag_population_concern/SPEC.md` (default bag + SelectedDistance; "we have save states — why not used?").
- Polish backlog: `Docs/Specs/Queued/map_view_polish/SPEC.md` (landing zone over trees, zoom-out distance, bring back distance bands, open-hiccup).
- Architecture note: LabScaffold is the gameplay host by design (GameplaySceneLoader); rename/restructure is a separate decision (not tracked yet).
