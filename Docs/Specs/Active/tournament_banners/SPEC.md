# SPEC — `tournament_banners`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

**This is the feature the whole banner epic was for, and it is the one part that did not get
built.** Banners for the Home promo strip and the Rankings screen are live — created in the
dashboard's Banners panel, served by `GET /api/v1/banners`, rendered in the build. **Tournament
banners do not exist in the admin at all.**

A tournament banner is the cross-promotion strip at the top of the tournament sign-up modal
(Figma `13892:3435`, 970 × 252). Cesar's decision, 2026-08-17: the artwork is **managed in the
Banners panel** like every other banner, but **which** banner a tournament shows is chosen **per
tournament, in the Tournaments panel**. Upload once, assign many, switch off in one place.

## Provenance — why this is a separate task

This was `game_banners` **§9**, amended into that spec after the implementer had already started.
It was not built, and `game_banners` was moved to `Docs/Specs/Completed/` with §9 outstanding —
so the work became invisible. Refiled here, standalone. **`Docs/Specs/Completed/game_banners/SPEC.md`
§9 is the origin text; this spec supersedes it.** Nothing else from `game_banners` is reopened.

## What already exists (do NOT rebuild any of it)

Verified in the tree on 2026-08-17, not assumed:

| Layer | What shipped | Where |
|---|---|---|
| Schema | `public.game_banners` — `id, placement, label, image_url_en, image_url_ja, link_url, start_at, end_at, sort_order, is_active, created_at, updated_at` | `playlife/backend/migrations/2026_08_17_game_banners.sql` |
| Storage | public bucket `game-banners`, content-hashed object names, 500 KB cap | `lib/bannerMutations.ts` |
| Dashboard | the whole **Banners** panel — list, editor, upload, activate, delete, audited | `Tools/admin-dashboard/app/(panels)/banners/`, `lib/banner*.ts` |
| Backend | `GET /api/v1/banners`, one live row per placement, server-side window + `is_active` | `playlife/backend/routers/banners.py` |
| Client | `BannerPolicy`, `BannerService`, `BannerSlotBinder`, `RemoteBannerDtos`, `RemoteBannerSource` | `Assets/Scripts/BannersRuntime/` |
| Client | art download, host allowlist, disk cache | `TournamentArtService.Banners` + `BannerPolicy.IsArtAllowed` |
| **Modal** | **the entire consuming side** — `ApplyBanner`, `ApplyBannerState`, `_bannerRoot`/`_bannerImage`/`_bannerButton`, the 1411 ↔ 1167 padding switch, the link-open handler | `TournamentSignupModalController.cs` |

**The modal is already wired.** `TryResolveModalBanner` (`TournamentSignupModalController.cs:532`)
is a three-line stub that returns `false`, which is why every tournament renders the no-banner
state today. Landing this task is a change to **that one resolver** plus the data feeding it —
not to `ApplyBanner`, not to the prefab, not to the layout.

## The four gaps

1. `game_banners.placement` CHECK allows only `('home_promo', 'rankings')`.
2. There is no `tournaments.modal_banner_id`.
3. The tournament editor has no banner picker.
4. `GET /tournaments/golfin` does not join the banner, and the client has no DTO for it.

---

## 1. Schema — `playlife/backend/migrations/2026_08_17_tournament_banners.sql`

```sql
-- 1. Widen the placement CHECK.
alter table public.game_banners
  drop constraint if exists game_banners_placement_check;

alter table public.game_banners
  add constraint game_banners_placement_check
  check (placement in ('home_promo', 'rankings', 'tournament_modal'));

-- 2. The assignment.
alter table public.tournaments
  add column if not exists modal_banner_id uuid
    references public.game_banners(id) on delete set null;
```

`on delete set null`, never `cascade`: deleting a banner must not delete a tournament. The
tournament loses its strip and renders the no-banner state, which is a complete modal.

⚠️ Find the CHECK constraint's real name first — `pg_get_constraintdef` — rather than trusting the
one above. Postgres names it from the table and column, but the original migration may have
declared it inline under a different name.

Header, column comments and a VERIFICATION block in the house style of
`2026_08_17_tournaments_is_active.sql`. Copy the file into `Tools/admin-dashboard/migrations/`
so both repos carry it, as every other migration does.

**Migration first, verify over PostgREST, then deploy** (`ADMIN_DASHBOARD_OPS.md` §3.2).
Deploying a `.select()` naming a column that does not exist 500s the whole schedule endpoint —
that is the entire schedule, for every player, not just the banner.

## 2. Backend

### 2.1 `GET /api/v1/banners` must keep ignoring `tournament_modal`

`routers/banners.py` selects per placement. A `tournament_modal` row must **never** be auto-served
there — it only ever reaches a player attached to a tournament. If the existing query iterates a
placement list, add the exclusion explicitly rather than relying on the list happening not to
contain it.

### 2.2 `GET /tournaments/golfin` joins the banner

`routers/tournaments.py::list_golfin`. Add `modal_banner_id` to the `.select(...)` string — it
currently ends `"…, banner_url, bot_seed, description_en, description_ja, description_key"`.

Then **one** extra round trip for the whole payload — the same shape the prize-bands fetch already
uses, **not** one query per tournament:

```python
banner_ids = [t["modal_banner_id"] for t in tournaments if t.get("modal_banner_id")]
# one .in_() query against game_banners, filtered to is_active = true
```

Emit per tournament:

```json
"modal_banner": {
  "image_url_en": "https://…/game-banners/tournament_modal-en-abc123def456.jpg",
  "image_url_ja": null,
  "link_url": "https://golfin.io/campaign/august"
}
```

`null` when: the tournament has no `modal_banner_id`; the referenced row is missing; or that row
is `is_active = false`. **The `is_active` check happens server-side** — the client must never
learn that column exists.

⚠️ Do **not** put `modal_banner_id` itself in the payload. The client has no use for an internal
uuid and shipping one invites someone to fetch by it later.

⚠️ `start_at` / `end_at` / `sort_order` on a `tournament_modal` row are **not** consulted. The
tournament's own window governs when the strip is on screen.

## 3. Admin dashboard

### 3.1 Banners panel — placement-aware editor

`tournament_modal` joins the placement list. Because scheduling and ordering do not apply to it:

- **Hide** `start_at`, `end_at` and `sort_order` when the placement is `tournament_modal`, rather
  than showing controls that do nothing.
- `is_active` still applies and is still the kill switch — switching a banner off drops it from
  **every** tournament using it at once. That is the point of managing it in one place, and the
  UI should say so.
- Art spec for this placement: **970 × 252**, same MIME list and 500 KB cap as the rest.
- `link_url` still applies, through the same `BannerPolicy.IsLinkAllowed` gate.
- Each `tournament_modal` row in the list shows **"Assigned to N tournaments"**, read off
  `tournaments.modal_banner_id`, so the blast radius is visible without opening another panel.
- **Deleting an assigned banner** warns with the count and the tournament names and requires a
  typed confirmation — the same posture deactivating a LIVE banner already takes.

### 3.2 Tournaments panel — the picker

- `lib/types.ts`: `modalBannerId: string | null` on `TournamentRow` and `TournamentInput`.
- `lib/tournamentData.ts`: map `modal_banner_id` through the existing `str()` helper, so an
  un-migrated DB yields null instead of throwing — the way `isActive` already tolerates its column.
- `lib/tournamentMutations.ts`: persist on create and update. Validate that a non-null id names an
  existing `game_banners` row **whose placement is `tournament_modal`**. A dangling id or a
  `home_promo` id is a **400**, not a silent write.
- `tournament-editor.tsx`, **Artwork** tab: a dropdown of active `tournament_modal` banners by
  `label`, each with a thumbnail, plus a **None** entry. **No upload control here** — banner bytes
  are uploaded once, in the Banners panel; that is the whole point. Link out to `/banners` with one
  line saying where new ones come from.
- Audit rides in the existing `tournament_update` before/after snapshot. No new action name.

## 4. Client

### 4.1 Wire

- `RemoteTournamentDtos.cs`: a `RemoteModalBannerDto` with `image_url_en` / `image_url_ja` /
  `link_url`, and `[JsonProperty("modal_banner")] public RemoteModalBannerDto? ModalBanner;` on
  `RemoteTournamentDto`. Plain strings, no date handling.
- `TournamentDefinition.cs`: three `string?` properties —
  `ModalBannerImageUrlEn`, `ModalBannerImageUrlJa`, `ModalBannerLinkUrl` — **null-defaulted in the
  constructor** so every existing call site and test compiles untouched, exactly as `TitleJa` and
  the description trio were added.
- `TournamentScheduleMapper.cs`: pass them through. A `modal_banner` that is absent or null maps to
  three nulls; that is the no-banner state, not an error.

### 4.2 The resolver — the only behavioural change

`TournamentSignupModalController.TryResolveModalBanner` (currently `return false`):

```csharp
private static bool TryResolveModalBanner(
    TournamentDefinition def, out string? imageUrl, out string? linkUrl)
```

Resolution order — **the same ladder `BannerService` already uses for the other two placements;
read it and match it, do not invent a second one:**

1. `LocalizationManager.CurrentLanguage == Language.Japanese` → `ModalBannerImageUrlJa`, else `ModalBannerImageUrlEn`.
2. That one null/empty → the other.
3. Still nothing → **return false** (no banner; state B).
4. The chosen URL must pass `BannerPolicy.IsArtAllowed` → otherwise **return false**. Do not
   trust the server; this is the same defence-in-depth `BannerService` applies at ingest.

`linkUrl` is passed through raw — `ApplyBanner` already re-checks it with
`BannerPolicy.IsLinkAllowed` before enabling the button, and the click handler checks again.

### 4.3 What must NOT change

`ApplyBanner`, `ApplyBannerState`, the 0 ↔ 32 padding switch, `_bannerRoot` / `_bannerImage` /
`_bannerButton`, the prefab, and the link-open handler are **already correct and already tested at
1411 / 1167**. This task feeds them; it does not touch them.

⚠️ `BannerService.BannerPlacement` and `TryParsePlacement` cover `home_promo` / `rankings` for the
`/api/v1/banners` path. **Do not add `TournamentModal` to that enum.** A tournament banner never
comes through that endpoint, and adding it there would create a second, unreachable code path that
looks like it works. `BannerService` already logs and skips unknown placements, which is correct
behaviour if a `tournament_modal` row somehow appeared in that payload.

## 5. Acceptance checklist

Each item `PASS` or `FAIL` with a one-sentence justification citing what was measured.

**Schema**

- [ ] The real CHECK constraint name was read from `pg_get_constraintdef` before dropping it.
- [ ] Inserting a `tournament_modal` row succeeds; inserting a `nonsense` placement still fails.
- [ ] `tournaments.modal_banner_id` verified over PostgREST by name before any deploy.
- [ ] Deleting an assigned banner leaves the tournament alive with `modal_banner_id = null`.

**Backend**

- [ ] `GET /api/v1/banners` does **not** return `tournament_modal` rows, proven with one live.
- [ ] `GET /tournaments/golfin` returns `modal_banner` for an assigned tournament and `null` for an unassigned one.
- [ ] Switching the banner row `is_active = false` makes it `null` on the wire **without** touching the tournament.
- [ ] The join is ONE extra query for the whole payload — confirmed by reading the code, not guessed.
- [ ] `modal_banner_id` does **not** appear in the payload.
- [ ] The schedule endpoint still returns all 6 tournaments and 19 base fields — no regression from the `.select()` edit.

**Dashboard**

- [ ] `start_at` / `end_at` / `sort_order` are hidden for `tournament_modal`, visible for the other two.
- [ ] The picker lists only active `tournament_modal` banners, plus None.
- [ ] A `modal_banner_id` naming a `home_promo` row is rejected with 400.
- [ ] Assignment round-trips: save, reload the panel, still selected.
- [ ] "Assigned to N tournaments" is correct after assigning to two.
- [ ] Deleting an assigned banner requires the typed confirm and names the tournaments.
- [ ] The assignment appears in the Audit Log inside a `tournament_update` before/after.

**Client**

- [ ] Assigned + active → the strip renders at 970 × 252 and the modal measures **1411**.
- [ ] Unassigned → no strip, modal measures **1167**, no gap. (The state B regression — it works today and must keep working.)
- [ ] JP player with `image_url_ja` set gets the JA art; with it null, falls back to EN.
- [ ] An `image_url_*` outside the `game-banners` prefix is refused by `BannerPolicy.IsArtAllowed` and renders state B rather than downloading it.
- [ ] The strip's link opens for an allowlisted host and the button is non-interactable with no link.
- [ ] Home promo and Rankings banners are **unaffected** — both still render. This task must not regress the shipped placements.
- [ ] EditMode suite still green, unmodified.

## 6. Smoke evidence

- Screenshots at 1170 × 2532: the sign-up modal **with** a tournament banner and **without**, plus
  the Home and Rankings slots still working.
- The dashboard flow end to end: upload a `tournament_modal` banner, assign it, see it in game
  with **no client rebuild** — that is the whole promise of this feature and it is the thing to
  demonstrate.
- One `[BannerArt] Cache HIT` line on a second launch.

## 7. Files this task touches

**New**

- `playlife/backend/migrations/2026_08_17_tournament_banners.sql` (+ dashboard copy)

**Modified**

- `playlife/backend/routers/tournaments.py` — `modal_banner_id` in the select, one join, `modal_banner` in the payload
- `playlife/backend/routers/banners.py` — explicit `tournament_modal` exclusion
- `Tools/admin-dashboard/lib/{types,tournamentData,tournamentMutations,banner,bannerData,bannerMutations}.ts`
- `Tools/admin-dashboard/app/(panels)/tournaments/tournament-editor.tsx` — the picker
- `Tools/admin-dashboard/app/(panels)/banners/{banners-panel,banner-editor}.tsx` — placement-aware fields, assigned count, delete warning
- `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs`, `TournamentScheduleMapper.cs`
- `Assets/Scripts/Tournaments/TournamentDefinition.cs` — three null-defaulted properties
- `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` — **`TryResolveModalBanner` only**
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## 8. Out of scope (do NOT do these)

- Any change to `ApplyBanner`, `ApplyBannerState`, the padding switch, or
  `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab`. The consuming side is done.
- Adding `TournamentModal` to `BannerService.BannerPlacement` (§4.3).
- The result / CLAIM modal `13894:3628`, which shares the strip. Separate task, once this works.
- Scheduling, rotation or multiple banners per tournament. One assignment, governed by the
  tournament's own window.
- Touching `tournaments.banner_url` or the `tournament-art` bucket — that is the 260×360 card art
  and thumbnail, a different image with a different bucket.
- Impression or click analytics.
