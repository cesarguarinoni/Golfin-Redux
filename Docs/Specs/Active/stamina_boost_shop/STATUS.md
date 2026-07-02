# STATUS — stamina_boost_shop (Order 517)

**State:** `SPEC_READY`
**Tier:** 3 (two new ScreenManager screens · new CSV data model · new stamina top-up API · new nav entry)
**Pillar:** Shop (first shop — greenfield)
**Updated:** 2026-07-02

## Pipeline
- [x] DESIGN_BRIEF drafted (queued)
- [x] Design pass — D1–D9 resolved with Cesar 2026-07-02 (see SPEC "Locked design decisions")
- [x] `SPEC_READY` — Architect wrote SPEC.md, seed CSVs (`reference/`), and both node renders
- [ ] `IMPLEMENTER_WORKING`
- [ ] `READY_FOR_SELF_REVIEW`
- [ ] `SELF_REVIEW_PASS`
- [ ] `READY_FOR_ARCHITECT_REVIEW`
- [ ] `ARCHITECT_REVIEW_PASS`
- [ ] `DONE` — Cesar approved → move to `Docs/Specs/Completed/`

## Kickoff

```
Use the implementer subagent on "stamina_boost_shop"
```

## Notes
- Seed data + RP economy authored at spec time; implementer copies the two CSVs into `Assets/Resources/Data/`.
- Art (storefront/hero/menu sprites) is a Nishikawa dependency — wire against logical keys, don't block on it.
