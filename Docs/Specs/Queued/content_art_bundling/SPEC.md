# SPEC — `content_art_bundling`

> **Draft, filed 2026-08-27 for review with the Architect.** Queued deliberately: this is not
> `SPEC_READY` and must not be picked up by the implementer chain until Cesar and the Architect
> have been through it.
>
> Depends on `content_art_urls` (in flight) for the URL columns and the resolution ladder.
> Plan: `Docs/CONTENT_PIPELINE_PLAN.md` §10.2.

## Status

See `STATUS.md`. `DRAFT` — awaiting architect review.

## Goal

Make *"the admin informs the next build"* mechanical.

`content_art_urls` closes the gap between a row existing and its art shipping: the row renders
by URL until a build bundles the asset, and its §2.2 ladder then hands the row back to the
bundled sprite automatically. What it does not do is get the art INTO the build — today that is
still a human downloading a file, naming it, dropping it in `Resources/` and setting a column.

This task is that step, done once, correctly, and reviewably.

**It does not change what players see.** Every behaviour is already specified by
`content_art_urls` §2.2; this only makes rule 2 start applying to a row sooner, by putting the
asset where rule 2 can find it.

---

## 1. Decisions of record (Cesar, 2026-08-27)

1. **No WebP.** Catalog-art uploads are PNG/JPG only, enforced at upload in `content_art_urls`
   §5.1. Unity does not import WebP natively, and accepting a format this step cannot bundle
   would let an operator upload art that works until the build meant to absorb it.
2. **Import settings are checked, not defaulted.** A raw PNG dropped into `Resources/` ships
   uncompressed at full source size unless its `TextureImporter` is configured. That is worse
   than not bundling it — it is the §10.2 size problem, made worse, silently.
3. **Deterministic names.** The bundled sprite name is derived from the row, not chosen by a
   human and not taken from the bucket's content-hashed filename.

## 2. Where this runs — NOT in the build lane

`Docs/TESTFLIGHT_RUNBOOK.md` already settles this for the exporter, and the reasoning transfers
verbatim: *"An export inside the lane would bake CSV changes into a build whose COMMIT does not
contain them, and the build number IS the commit count."* Downloading art into `Assets/` at
build time has exactly that defect, and additionally dirties the tree that fastlane's
`ensure_git_status_clean` guards.

So: a **human-run step that produces a reviewable git diff**, shaped like `export_content.py` —
you run it, you look at what it did, you commit it, and the build that follows carries it.

**It runs in the Unity Editor, not as a Python tool**, because rule 2 of §1 requires
`TextureImporter` work that only Unity can do correctly. Hand-writing `.meta` files to avoid
opening the Editor is the obvious shortcut and it is forbidden here: import settings are the
part most likely to be silently wrong, and a hand-rolled `.meta` is how they get that way.

Entry point: `GOLFIN/Content/Fetch URL Art` (menu item), plus a `[MenuItem]`-free static entry
so it can be driven from `script-execute` in review.

**No Supabase credentials.** It reads the repo CSVs, which the exporter has already filled in,
and fetches over plain HTTPS from the public bucket. The service key is not needed and must not
be required.

## 3. What it does, per row

For every row of `characters` / `clubs` / `items` / `balls` that has a URL column set and the
corresponding sprite-NAME column empty:

1. **Check the URL against the client's own allowlist** — `CatalogArtPolicy.IsArtAllowed`,
   reused, not re-implemented. A URL the client would refuse must never become a bundled asset;
   that would ship art nobody can trace back to a legitimate upload.
2. **Refuse WebP** on the response's content type as well as the extension. Belt and braces: the
   upload path blocks it, but this step must not depend on that having held.
3. **Download** to a temp path, respecting the same 1 MB ceiling the client enforces.
4. **Derive the deterministic name** (§4).
5. **Refuse on collision** (§4). Never overwrite an existing asset.
6. **Write** into the catalog's `Resources` folder — the same folder constant the loader uses
   (`Portraits/Thumbnails`, `Items/Full`, …), cited to the loader.
7. **Apply import settings from a sibling** (§5).
8. **Set the sprite-name column** in the repo CSV.
9. **Report** the added in-build cost (§6).

Then stop. It writes no catalog rows and talks to no admin API — the CSV now carries a name the
catalog does not, which is precisely the drift `import_content.py` exists to resolve, so the
closing instruction it prints is: *run `import_content.py --apply`, publish, then export.* That
reuses the loop `content_two_way` built and tested rather than inventing a second way in.

## 4. Naming — deterministic, convention-shaped, collision-refusing

The bucket filename is `{catalog}-{rowId}-{column}-{sha256[:12]}.{ext}`, which is right for a
cache key and wrong for a repo asset. Derive instead from the row id, following
`Docs/Game Design/ASSET_NAMING_CONVENTION.md`:

| Catalog / column | Rule | `char_zoe` → |
|---|---|---|
| characters `portraitUrl` | `S_Char_{Pascal(id minus "char_")}` | `S_Char_Zoe` |
| characters `fullUrl` | `S_CharFull_{Pascal(...)}` | `S_CharFull_Zoe` |
| items / balls thumbnail, full | `S_{Item\|Ball}_{Pascal(...)}` / `S_{...}Full_{...}` | |
| clubs portrait / full / control | follow the existing `S_Club_{Type}-{Brand}` shape, derived from the row's `type` and `brand` columns rather than the id | |

**Deterministic means re-running produces the same name**, which is what makes step 5 a safe
no-op on a second run.

**Collision is a REFUSAL, never an overwrite.** If the derived name already exists in that
folder, stop for that row, name it, and continue with the others — an artist's hand-made asset
must never be replaced by a downloaded one. Report the refusals at the end; the operator either
renames or accepts that the row already has art.

## 5. Import settings — copied from a sibling, verified, reported

The catalog's `Resources` folder already contains correctly-configured art. Read the
`TextureImporter` of a sibling in the SAME folder and apply its settings to the new asset:
texture type, sprite mode, `maxTextureSize`, compression + format, `alphaIsTransparency`,
`mipmapEnabled`, `pixelsPerUnit`.

- **If the folder is empty**, do not guess: refuse the row, say which folder had no reference
  asset, and let the operator place one deliberately.
- **Verify after import** by re-reading the importer, and print the resulting format and
  `maxTextureSize` per asset. A setting that failed to apply must not pass silently — this is
  decision 2 of §1 and the whole reason this runs in the Editor.

## 6. Size reporting — the number §10.2 cares about

Print, per run and per catalog: assets added, source bytes, and the **estimated in-build bytes**
after compression. `CONTENT_PIPELINE_PLAN.md` §10.2 measures `Assets/Resources/Clubs` at 122 MB
of source PNG (~50 MB in-build) covering about a third of the roster, trending toward ~150 MB at
full coverage. A tool whose whole job is adding to that folder must say what it added, every
time, or the growth is invisible until a build report.

Append the same summary to `Docs/Reports/content_art.txt` — the file `content_two_way` §5
already writes — rather than starting a second report nobody reads.

## 7. Acceptance

- [ ] A row with a URL and an empty name: run the tool → the PNG lands in the right `Resources`
      folder under the derived name, the CSV gains the name, and `git status` shows exactly those
      two changes plus the `.meta`.
- [ ] **Import settings match the sibling**, verified by reading the importer back — assert
      compression format and `maxTextureSize` equal the reference asset's, and that they are NOT
      the Unity defaults.
- [ ] **Re-running is a no-op.** Same command twice, second run reports 0 fetched and produces no
      diff.
- [ ] **Collision refuses.** Point a row at a name that already exists → refused and named, the
      existing asset byte-identical afterwards.
- [ ] **A WebP URL is refused**, by content type and by extension, with a message that says why.
- [ ] **A URL outside the allowlist is refused** — `CatalogArtPolicy.IsArtAllowed` is what says
      no, not a local re-implementation.
- [ ] **An empty `Resources` folder refuses** rather than guessing settings.
- [ ] **The ladder hands over.** After fetching + importing + publishing + exporting, load the
      game and confirm the row now resolves via `content_art_urls` §2.2 **rule 2** (bundled), not
      rule 1 or 3 — log the sprite identity, do not infer it. This is the whole point of the task.
- [ ] Size report printed and appended to `Docs/Reports/content_art.txt`.
- [ ] Full unfiltered EditMode sweep green.

## 8. Out of scope

- **Retiring bundled art.** Explicitly not the direction (Cesar, 2026-08-27) — see
  `content_art_urls` §8. This task moves art INTO the build; nothing moves out.
- **Running inside the build lane.** §2.
- **Choosing art.** This bundles what an operator already uploaded; it makes no decision about
  which rows deserve art.
- **3D / hole content.** `CONTENT_PIPELINE_PLAN.md` §10.3, triggered by the second course.
- **Clearing the URL after bundling.** `content_art_urls` §2.2 is explicit that the URL stays —
  players on older builds still depend on it.

## 9. Open questions for the Architect

1. **Club naming.** §4 derives club names from `type` + `brand` rather than the row id, because
   that is what the existing 799 rows look like — but club art is SHARED across rarities
   (`club_driver_fairloft_common` and `…_supreme` reference the same sprite). So a per-row fetch
   would download the same asset up to six times and collide with itself on runs 2–6. Proposal:
   de-duplicate by derived name within a run and fetch once. Worth confirming that shared art is
   still the intent for admin-created clubs.
2. **Who sets the URL column to begin with for an admin-created row** — the operator uploads via
   the row editor, which sets the URL. But the sprite NAME column stays empty until this tool
   runs. Should the admin's row editor show that a row is "URL-only, not yet bundled"? A one-line
   badge in the panel would make the pipeline state visible where the operator already is.
3. **Frequency.** Is this a release-prep step (run once before cutting a build, alongside
   `export_content.py`) or something run whenever art is uploaded? The runbook currently has one
   content step before a build; adding a second is fine, but they should be adjacent and
   documented together.
