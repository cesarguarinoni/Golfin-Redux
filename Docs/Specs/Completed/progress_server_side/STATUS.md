DONE

Cesar approved 2026-08-28 ("Done then"). Folder moved to Docs/Specs/Completed/.

Every §6 step ran, including the two that are usually skipped:
  • §21 live E2E on prod, both halves — a grandfathered level-up and a
    cost_changed re-price, six server rows verified by SQL, the cost change
    published from the live admin UI.
  • §23 three deployment proofs, all quoted in IMPLEMENTER_REPORT.md.

§5 (legacy closure) was folded in on Cesar's call rather than deferred:
/points/spend refuses character_level_up and club_level_up, verified
authenticated against prod. The reset he offered was measured first and turned
out to be a no-op, so it was not run.

Two defects were found and fixed inside the task rather than shipped:
  • the new panel's sidebar rendered the raw key `nav.level-costs` — fixed, and
    closed mechanically (PanelDef.id is derived from the dictionary, so a panel
    with no label is now a compile error);
  • the grandfather cross-check read `level` when the blob key is `lv` — fixed,
    and then made to FIRE on prod, both branches.

One process defect is disclosed and NOT fixed, deliberately: commit 978a71e1e
swept three unrelated paths into a docs commit via `git add Docs/`. History was
already pushed; rewriting it would be the worse trade. See tasks/lessons.md
Lesson AH.
