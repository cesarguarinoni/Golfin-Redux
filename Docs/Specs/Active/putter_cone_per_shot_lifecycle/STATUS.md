# STATUS — `putter_cone_per_shot_lifecycle`

**Current:** `SPEC_READY`

**History:**
- 2026-05-14 10:00 JST — Architect amended SPEC. Piece 2 added: central ball sprite size parity (both `_normalSize` and `_puttModeSize` = 150f, fields kept separate). Surfaced from Cesar Lesson O.
- 2026-05-14 09:30 JST — Architect locked. Approach A (reuse `_coneGraphic` with putt-mode styling) confirmed. Approach B rejected to avoid coupling to `PuttPathPredictor` (slated for deletion under Order 110).
- 2026-05-14 — Drafted by architect chain during §2f Lesson O closeout.
