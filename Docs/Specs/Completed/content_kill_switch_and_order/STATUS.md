DONE

Approved by Cesar 2026-08-26. Per-catalog kill switch split from the global one; the -100 tie broken.
Previous state: READY_FOR_ARCHITECT_REVIEW.
Shipped on `main` — GolfinRedux `aa981b1b1` (Phases 1-2) and `6b689a8da` (kill switch + order), playlife `ee42f42` (endpoint + migration).
Folder moved Active/ -> Completed/ as part of that approval.

---

Prior contents, verbatim:

Implemented directly by Claude Code at Cesar's request (2026-08-26) — SPEC_KIND: backend, no
Figma node, no screenshot deliverable.

**Every acceptance item is now verified, including against prod.** Nothing outstanding.

* §1 per-catalog kill — `playlife/backend/routers/content.py` + the Unity client. 10 backend tests
  (tripwired: 8 of 10 fail against the pre-fix router), 10 new EditMode tests, and the original
  prod measurement re-run live and now correct.
* §2 execution order — `CharacterManager` −100 → −95 via `MonoImporter`, plus a runtime assert on
  the new `SaveDataHost.IsLoaded`. Verified over 5 consecutive play-mode boots.
* Migration applied by Cesar; `playlife-api` deployed (v50 → **v51**, image
  `deployment-01M0XVQSXZJVQQG2T71ZAR40DR`).

Live prod evidence (2026-08-26, `bags` disabled then restored):

| request | before the fix | now |
|---|---|---|
| `catalogs=bags,items` | `enabled` **False** | `enabled` **True**, `disabled ['bags']` |
| `catalogs=items` | `enabled` True | `enabled` **True**, `disabled ['bags']` |
| all seven | `enabled` **False** | `enabled` **True**, `disabled ['bags']` |

`bags` absent from `catalogs`, the other six served, registry restored to all-enabled.
Global kill proven to actually fire (`content_enabled=false` → `enabled False`, `disabled []`),
restored. `/health`, `/notices`, `/banners`, `/tournaments/golfin` all 200 after deploy.

Awaiting Cesar's sign-off only. The Phase-2 device pass is now unblocked.