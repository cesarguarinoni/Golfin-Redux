# catalog_join — gift_items and the authored half of venues

**Decision (Cesar, 2026-09-03):** both should join the content pipeline. Written after the gift
strip shipped Japanese-only names into an English build and no gate could say so — `gift_items`
is the one player-facing table with no repo mirror, so `export_content.py --check`, which diffs
every registered catalog by id AND by value, has never had it in scope.

## Why this is the real fix

`GiftItemName` (2026-09-03) localizes item names client-side through the `texts` catalog. That
stops the bleeding and it is worth keeping — it makes a NEW catalog row fixable by publishing one
text row. But it does not make `gift_items` governed: the table still has no bundled floor, no
drift check, no draft/publish gate, and no admin panel. The next column somebody adds there is
invisible to the repo for exactly the same reason `name` was.

## Part A — `gift_items` joins wholesale

21 rows, every one hand-authored. This is the textbook shape for a catalog.

| Step | |
|---|---|
| Schema | `name_en` / `name_ja`, `description_en` / `description_ja`; keep `name`/`description` until the client stops reading them, then drop. DDL → Cesar (`WORKFLOW_NOTES.md`) |
| Repo mirror | `Assets/Data/GiftItems.csv`; register in `Tools/content/catalogs.py` (`Catalog("gift_items", …, "id")`) |
| Seed | `seed_from_csv.py --apply` — note `reference_seeded_catalog_mirror_is_empty`: the seed stamps v1 but writes NO server mirror, so PUBLISH once and then count the rows |
| API | `/gifts/items` returns the language columns; `GiftItemDto` gains them |
| Client | `GiftItemName.Of` prefers the row's own language column and keeps the `texts` key as the fallback, so nothing regresses while the API rolls out |
| Admin | a Gift Items panel, same shape as the others |

**Retire the uuid-prefix keys only after** the language columns are live everywhere — not in the
same change. Two fallbacks for one release is the cheap way to avoid a blank strip.

## Part B — venues: an admin panel, NOT a catalog

**Corrected 2026-09-03 after Cesar asked the right question: "what if I want to add venues without
rebuilding the game?"** The first draft of this section said venues should join as a catalog. That
was wrong, and wrong in the direction that would have cost him the thing he asked for.

**Venues already need no rebuild.** They are read live — `/venue/list`, `/venue/nearby`,
`/venue/{id}`. Insert a row, it is there on the next launch. The 1,981 OSM rows have never been
shipped inside the app and should not start being.

Joining the bundled-catalog model moves them TOWARD a build dependency, not away from one. A
catalog's floor is a copy baked into the app; the server overlay does reach shipped builds, but it
is `min_build`-gated and that default is "the next build". Correct for a text string. Wrong for a
partner signed this afternoon.

Two mechanisms, routinely conflated — venues want the first, `gift_items` wants both:

| | gives you | needs a build |
|---|---|---|
| Admin panel on the live table | author the fields, visible immediately | no |
| Content catalog (floor + overlay + drafts + `--check`) | repo mirror, publish gate, drift detection | floor is baked; overlay is `min_build`-gated |

So Part B is: put the authored venue fields (`is_partner`, `partner_offer`, `subtitle`,
`chip_extra`, `price_label`, `image_url`) behind an admin panel writing `venues` directly. **This is
already `gps_checkin`'s Partners panel** — so Part B is not a separate task, it is a line item
confirming that panel is the right and complete answer. Nothing to build here beyond it.

The sizing that made the catalog look attractive still holds and still argues the same way: 1,998
rows, 1,981 `osm_import`, 11 with any authored field. A 2,000-line bundled floor to make 11 rows
editable was always the wrong trade; it just took the rebuild question to see WHY rather than only
that it was disproportionate. And the 1,961 Japanese names are proper nouns — 東京ゴルフ倶楽部 is that
course's name. Nothing there wants translating.

## Done when

- `export_content.py --check` covers both new catalogs and exits 0.
- An English build renders English gift item names with the client-side `texts` fallback REMOVED.
- A venue's partner offer can be authored in the admin and is live WITHOUT a build (gps_checkin's Partners panel).
- The catalog count in `catalogs.py` goes 20 → 21 (`gift_items` only — venues never becomes one).
