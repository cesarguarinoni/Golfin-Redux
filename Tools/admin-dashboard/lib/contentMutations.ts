import "server-only";
import { writeAudit } from "./audit";
import {
  fetchAllRows,
  fetchDiff,
  fetchGlobalContentEnabled,
  fetchVersionSnapshot,
  GLOBAL_ENABLED_KEY,
  REFERENCED_CATALOGS,
} from "./contentData";
import {
  hasErrors,
  ID_COLUMN,
  rowIdPattern,
  ROW_ID_MAX,
  validateCatalog,
  type ContentProblem,
  type DraftRow,
} from "./contentValidate";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { ContentRowInput, ContentStoredRow } from "./types";

/**
 * Write side of the content catalogs (SPEC content_catalog §D).
 * Every function: server-only, called AFTER checkAdmin(), audited with
 * before/after, with a mock branch. Same shape as lib/noticeMutations.ts.
 */

export interface ContentOutcome {
  ok: boolean;
  status: number;
  message: string;
  /** Populated on a 400 from validation — the FULL list, never the first one. */
  problems?: ContentProblem[];
  version?: number;
}

const ok = (message: string, extra: Partial<ContentOutcome> = {}): ContentOutcome => ({
  ok: true,
  status: 200,
  message,
  ...extra,
});
const fail = (status: number, message: string, extra: Partial<ContentOutcome> = {}): ContentOutcome => ({
  ok: false,
  status,
  message,
  ...extra,
});

const toDraftRow = (r: ContentStoredRow): DraftRow => ({
  rowId: r.rowId,
  data: r.data,
  minBuild: r.minBuild,
  isActive: r.isActive,
});

// ---------------------------------------------------------------------------
// Draft edit
// ---------------------------------------------------------------------------

/** Upsert ONE draft row. Drafts are never served, so the row CONTENT needs no
 *  validation — publish is the gate (§D1), and blocking a half-typed row would
 *  make the editor unusable.
 *
 *  The row ID is the exception, and it is not content: it is the identity the
 *  upsert keys on. A malformed one produces a row nothing can resolve, and a
 *  colliding one silently overwrites something that already exists — which the
 *  editor's `+ New row` control makes reachable for the first time
 *  (shop_stocking §2). Both are checked HERE rather than only in the form: the
 *  route is reachable without the form. */
export async function upsertDraftRow(
  adminEmail: string,
  catalog: string,
  input: ContentRowInput
): Promise<ContentOutcome> {
  const rowId = (input.rowId ?? "").trim();
  if (!rowId) return fail(400, "rowId is required.");
  if (!input.data || typeof input.data !== "object") return fail(400, "data must be an object.");

  const minBuild = Number.isFinite(input.minBuild) ? Number(input.minBuild) : 0;
  const isActive = input.isActive !== false;
  const data: Record<string, string> = {};
  for (const [k, v] of Object.entries(input.data)) {
    data[k] = v === null || v === undefined ? "" : String(v);
  }

  const before = (await fetchAllRows("content_drafts", catalog)).find((r) => r.rowId === rowId) ?? null;

  // ---- row id rules, on creation only ------------------------------------
  //
  // `expectNew` is the CALLER'S INTENT, and it is what makes the draft
  // collision detectable at all: without it, "create a row that happens to
  // exist" and "edit that row" are the same request, and the create silently
  // wins. With it, the editor's new-row drawer can be told no.
  const creating = input.expectNew === true || before === null;

  if (creating) {
    const pattern = rowIdPattern(catalog);
    if (rowId.length > ROW_ID_MAX) {
      return fail(400, `Row id "${rowId}" is ${rowId.length} characters; the maximum is ${ROW_ID_MAX}.`);
    }
    if (!pattern.test(rowId)) {
      return fail(
        400,
        `Row id "${rowId}" is not a valid id for ${catalog}. Allowed: ${pattern.source} ` +
          `(${catalog === "texts" ? "letters, digits and underscores" : "lower-case letters, digits and underscores"}).`
      );
    }

    if (input.expectNew === true && before) {
      return fail(409, `Row id "${rowId}" already exists as a DRAFT row in ${catalog}. Edit that row instead.`);
    }

    // Unique against PUBLISHED rows too. A draft created under a published
    // row's id is not a new row at all: the next publish would overwrite the
    // published one (`on conflict (catalog, row_id) do update`), silently.
    const publishedClash = (await fetchAllRows("content_rows", catalog)).some((r) => r.rowId === rowId);
    if (publishedClash) {
      return fail(
        409,
        `Row id "${rowId}" already exists as a PUBLISHED row in ${catalog}. Publishing a new row ` +
          "under that id would overwrite it. Pick a different id, or edit the existing row."
      );
    }
  }

  // The id column inside `data` is WRITTEN FROM the row id, never typed. The
  // two are the same fact, the exporter writes `data` into the CSV, and the
  // validator already errors when they disagree — so the way to make them
  // agree is to stop having two places to say it (shop_stocking §2).
  const idColumn = ID_COLUMN[catalog];
  if (idColumn) data[idColumn] = rowId;

  if (isMockMode()) {
    const store = mockDb().contentDrafts;
    const at = store.findIndex((r) => r.catalog === catalog && r.rowId === rowId);
    const next: ContentStoredRow = { catalog, rowId, data, minBuild, isActive };
    if (at >= 0) store[at] = next;
    else store.push(next);
  } else {
    const res = await getSupabaseAdmin()
      .from("content_drafts")
      .upsert(
        {
          catalog,
          row_id: rowId,
          data,
          min_build: minBuild,
          is_active: isActive,
          updated_by: adminEmail,
          updated_at: new Date().toISOString(),
        },
        { onConflict: "catalog,row_id" }
      );
    if (res.error) return fail(500, `Draft upsert failed: ${res.error.message}`);
  }

  // targetUser is a UUID column and this target is not a user, so it stays null
  // exactly like the Tournaments / Notices panels; WHAT was edited lives in the
  // action string and in the before/after payloads.
  await writeAudit(
    adminEmail,
    before ? `content.draft.update:${catalog}` : `content.draft.create:${catalog}`,
    null,
    "content_drafts",
    before,
    { catalog, rowId, data, minBuild, isActive }
  );
  return ok(`Draft ${catalog}/${rowId} saved.`);
}

// ---------------------------------------------------------------------------
// Publish
// ---------------------------------------------------------------------------

/**
 * The golfin_characters mirror (SPEC §A4).
 *
 * `golfin_characters(id, display_name, rarity)` is a hand-maintained server-side
 * copy of Characters.csv that `routers/tournaments_golfin.py` reads to enforce
 * `char_rarity_min/max` at tournament entry. It has ALREADY drifted once — it
 * said char_olivia was Uncommon for three days after the CSV said Common, which
 * wrongly rejected her from Common-only events (fixed by
 * 2026_08_24_golfin_characters_rarity_fix.sql).
 *
 * This task is the moment that drift stops being a one-off and becomes routine:
 * an admin editing rarity in a panel has no idea the mirror exists. So the
 * mirror write is part of the publish REQUEST, not a follow-up job.
 *
 * Ordering: the mirror is written from the DRAFTS, BEFORE content_publish, so a
 * mirror failure means the publish never happened (§A4: "fail the publish if
 * that write fails"). The residual window is mirror-ahead-of-catalog if the RPC
 * then fails — the safer direction of the two, since the mirror would hold
 * exactly the rarities the admin was trying to publish, and a retry converges.
 */
async function mirrorCharacters(drafts: ContentStoredRow[]): Promise<string | null> {
  if (isMockMode()) return null;

  const rows = drafts.map((r) => ({
    id: r.rowId,
    display_name: [r.data.name, r.data.lastName].filter(Boolean).join(" ").trim() || r.rowId,
    rarity: r.data.rarity ?? "",
  }));
  if (rows.length === 0) return null;

  const res = await getSupabaseAdmin().from("golfin_characters").upsert(rows, { onConflict: "id" });
  return res.error ? res.error.message : null;
}

/**
 * The golfin_mode_fees mirror (game_modes_admin SPEC §4).
 *
 * `golfin_mode_fees(mode_id, entry_fee, is_locked)` is what POST /points/spend
 * prices a `mode_entry_fee:<id>` debit against. It exists for the same reason
 * `golfin_characters` does — a typed server-side copy of a catalog the spend
 * path cannot afford to re-derive out of jsonb on every request — and it is
 * written here, in the publish REQUEST, for the reason golfin_characters
 * TAUGHT us: a mirror maintained by hand drifts, silently, and the person
 * editing the panel has no idea it exists.
 *
 * THIS ONE IS STRICTER THAN THE CHARACTERS MIRROR, because the stakes differ.
 * A stale rarity wrongly excluded char_olivia from Common-only tournaments for
 * three days. A stale FEE means every player is charged the old price — or,
 * worse, refused at the new one while the card still shows the old — for as
 * long as it lasts. So the fee is upserted BEFORE `content_publish` and a
 * failure aborts the publish entirely.
 *
 * The residual window is mirror-ahead-of-catalog (the mirror lands, the rpc then
 * fails). That is the safer of the two directions and it is chosen deliberately:
 * a mirror holding the fee the admin was TRYING to publish refuses stale clients
 * at the new price, which is a `fee_changed` and a second tap. The other order
 * would publish a card saying 15 while the server still charges 10 — a player
 * charged a number they were never shown.
 *
 * NON-NUMERIC / NEGATIVE FEES CANNOT REACH HERE: the validator has already
 * refused them (contentValidate rule 10), and `golfin_mode_fees` carries its own
 * `entry_fee >= 0` check as the backstop. `Number(...) || 0` is the third layer
 * and exists only so a blank cell mirrors as free rather than as NaN.
 */
async function mirrorModeFees(drafts: ContentStoredRow[]): Promise<string | null> {
  if (isMockMode()) return null;

  const rows = drafts
    // A DEACTIVATED mode is not mirrored as free — it is not mirrored as
    // ANYTHING new, and the row it already has stays put. Deactivation (I6) is
    // how a mode is withdrawn from the client; the server should keep refusing
    // its old price rather than start accepting 0 for a mode nobody can see.
    .filter((r) => r.isActive)
    .map((r) => ({
      mode_id: r.rowId,
      entry_fee: Math.max(0, Math.trunc(Number(r.data.entryFee) || 0)),
      is_locked: String(r.data.locked ?? "").trim().toLowerCase() === "true"
        || String(r.data.locked ?? "").trim() === "1",
      updated_at: new Date().toISOString(),
    }));
  if (rows.length === 0) return null;

  const res = await getSupabaseAdmin()
    .from("golfin_mode_fees")
    .upsert(rows, { onConflict: "mode_id" });
  return res.error ? res.error.message : null;
}

/**
 * THE MIRROR DISPATCHER — the ONE place that knows which catalogs have a
 * server-side mirror and how to write it.
 *
 * ⚠️ WHY THIS EXISTS AS A FUNCTION rather than two `if (catalog === …)` blocks:
 * because two call sites ARE the bug this was added to fix. `mirrorModeFees`
 * was called from `publishCatalog` and nowhere else, so `rollbackCatalog` — a
 * first-class, UI-exposed operator control — produced a NEW client-visible
 * catalog version while leaving the mirror at whatever the last publish wrote.
 * Undoing a bad fee publish is the single most likely reason to roll `modes`
 * back, and it was the one path that silently did not undo the fee.
 *
 * Found by the red-team gate (REDTEAM_REVIEW.md §2), not by the two gates
 * before it, and not by me. Every future path that changes what a catalog
 * SERVES must call this; routing them all through one function is what makes
 * "did you remember the mirror?" a question with one answer instead of N.
 *
 * `rows` is the row set that is ABOUT TO BECOME PUBLISHED — drafts on a
 * publish, the rolled-to snapshot on a rollback. Returns an error string, or
 * null when there was nothing to do or it succeeded.
 */
/**
 * The catalogs that have a SERVER-SIDE MIRROR — a typed copy the game's own
 * request path reads, which must move whenever what the catalog SERVES moves.
 *
 * Exported and named so "does this catalog have a mirror?" is one fact rather
 * than a pattern of `if (catalog === …)` scattered across the mutation paths.
 * `mirrorForCatalog` below is the only thing that writes them; anything that
 * changes what a catalog serves must go through it.
 */
export const MIRRORED_CATALOGS = ["characters", "modes"];
async function mirrorForCatalog(
  catalog: string,
  rows: ContentStoredRow[]
): Promise<{ error: string; detail: string } | null> {
  if (catalog === "characters") {
    const error = await mirrorCharacters(rows);
    return error
      ? {
          error,
          detail:
            "The mirror is what tournament rarity restrictions read (SPEC §A4); " +
            "publishing characters without it would reintroduce the char_olivia drift.",
        }
      : null;
  }

  if (catalog === "modes") {
    const error = await mirrorModeFees(rows);
    return error
      ? {
          error,
          detail:
            "The mirror is what /points/spend prices a mode entry against " +
            "(game_modes_admin §4); serving modes without it would show players " +
            "one fee and charge them another.",
        }
      : null;
  }

  return null;
}

export async function publishCatalog(
  adminEmail: string,
  catalog: string,
  note?: string
): Promise<ContentOutcome> {
  const drafts = await fetchAllRows("content_drafts", catalog);
  const published = await fetchAllRows("content_rows", catalog);

  // §D1.6 needs the OTHER catalogs' drafts to resolve shop refIds. Only load
  // them for the catalog that actually references them.
  const otherCatalogs = new Map<string, Map<string, DraftRow>>();
  const needs =
    catalog === "shop_catalog"
      ? REFERENCED_CATALOGS
      : // progress_server_side §2 — the level-cost table's contiguity ceiling is
        // the highest `maxLevel` any character or club can reach, so the rule
        // cannot be checked without them. Drafts, not published rows, for the
        // same reason as the shop's refIds: raising a maxLevel and extending the
        // cost table are normally published together.
        catalog === "level_up_costs"
        ? ["characters", "clubs"]
        : [];
  for (const other of needs) {
    const rows = await fetchAllRows("content_drafts", other);
    otherCatalogs.set(other, new Map(rows.map((r) => [r.rowId, toDraftRow(r)])));
  }

  // The ONE cross-surface number a modes publish is checked against: what
  // `versus_win` actually pays. Read here rather than in the validator because
  // the validator is pure — and read ONLY for `modes`, because this is one
  // warning about one pair, not a mapping table (see contentValidate rule 10).
  let versusWinPts: number | null | undefined;
  if (catalog === "modes" && !isMockMode()) {
    const res = await getSupabaseAdmin()
      .from("game_point_actions")
      .select("pts")
      .eq("action", "versus_win")
      .maybeSingle();
    // A read failure is NOT a publish failure: this feeds a WARNING. Blocking a
    // fee change because one advisory lookup blipped would be the tail wagging
    // the dog. Left undefined ⇒ the warning simply does not run.
    if (!res.error && res.data) {
      const pts = (res.data as { pts: number | null }).pts;
      versusWinPts = pts === null ? null : Number(pts);
    }
  }

  const problems = validateCatalog(catalog, drafts.map(toDraftRow), {
    publishedMinBuild: new Map(published.map((r) => [r.rowId, r.minBuild])),
    otherCatalogs,
    versusWinPts,
  });

  if (hasErrors(problems)) {
    // NOTHING is published. Not the valid rows, not a subset — §D1.
    return fail(
      400,
      `${problems.filter((p) => p.severity === "error").length} validation error(s); nothing was published.`,
      { problems }
    );
  }

  // The diff is the audit's before/after payload, and it has to be read BEFORE
  // the publish — afterwards drafts and published are identical by definition.
  const diff = await fetchDiff(catalog);

  const mirrorProblem = await mirrorForCatalog(catalog, drafts);
  if (mirrorProblem) {
    return fail(
      502,
      `Mirror write failed, so nothing was published: ${mirrorProblem.error}. ${mirrorProblem.detail}`
    );
  }

  let version: number;
  if (isMockMode()) {
    const store = mockDb();
    store.contentPublished = [
      ...store.contentPublished.filter((r) => r.catalog !== catalog),
      ...drafts.map((r) => ({ ...r, catalog })),
    ];
    const meta = store.contentCatalogs.find((c) => c.name === catalog);
    version = (meta?.publishedVersion ?? 0) + 1;
    if (meta) {
      meta.publishedVersion = version;
      meta.publishedCount = drafts.length;
      meta.draftCount = drafts.length;
      meta.dirtyCount = 0;
    }
    // Record the snapshot the way `content_publish` does live, so mock mode's
    // version history is a real list rather than one that only ever shows the
    // fixture (content_panels_gaps §2).
    store.contentVersions.unshift({
      catalog,
      version,
      publishedBy: adminEmail,
      publishedAt: new Date().toISOString(),
      note: note ?? null,
      rowCount: drafts.length,
    });
  } else {
    const res = await getSupabaseAdmin().rpc("content_publish", {
      p_catalog: catalog,
      p_by: adminEmail,
      p_note: note ?? null,
    });
    if (res.error) return fail(500, `content_publish failed: ${res.error.message}`);
    version = Number(res.data);
  }

  await writeAudit(
    adminEmail,
    `content.publish:${catalog}`,
    null,
    "content_rows",
    { catalog, version: diff.publishedVersion, counts: diff.counts, entries: diff.entries },
    {
      catalog,
      version,
      note: note ?? null,
      mirroredToGolfinCharacters: catalog === "characters",
      mirroredToGolfinModeFees: catalog === "modes",
    }
  );

  const warnings = problems.filter((p) => p.severity === "warning");
  return ok(
    `Published ${catalog} v${version} — ${diff.counts.added} added, ${diff.counts.changed} changed, ` +
      `${diff.counts.deactivated} deactivated` +
      (warnings.length ? `; ${warnings.length} warning(s).` : "."),
    { version, problems: warnings.length ? warnings : undefined }
  );
}

// ---------------------------------------------------------------------------
// Rollback
// ---------------------------------------------------------------------------

/**
 * Restore a previous snapshot. It comes back as a NEW, HIGHER version — never a
 * decrement. Clients cache by version and ask `since=N`; rewinding the counter
 * would leave a client that already holds v12 permanently unaware of the
 * rollback, still serving the bad content (SPEC §A1.2).
 *
 * ⚠️ AND IT RE-MIRRORS, which it did not until 2026-08-28 (REDTEAM_REVIEW §2).
 *
 * A rollback produces a new, client-visible catalog version — it is a publish
 * that happens to carry old content. So every consequence of publishing applies,
 * including the server-side mirrors. It used not to: `mirrorModeFees` was
 * reachable only from `publishCatalog`, so an operator who fat-fingered
 * `practice.entryFee = 150`, published, and then hit ROLLBACK got a card reading
 * 10 again while `golfin_mode_fees` sat at 150 — every player answered
 * `fee_changed: 150`, and anyone under 150 RP locked out of the free-tier mode
 * the operator had just "fixed". Undoing a bad fee publish is the most likely
 * reason to roll `modes` back at all, so this was the one path that could not
 * afford to be the one path without a mirror.
 *
 * The rows mirrored are the ROLLED-TO SNAPSHOT — what the catalog is about to
 * serve — read from `content_versions` before the rpc, mirrored before the rpc,
 * and a mirror failure aborts the rollback. Identical posture to publish,
 * deliberately: same ordering, same abort, same residual window
 * (mirror-ahead-of-catalog, which is the safer of the two directions).
 */
export async function rollbackCatalog(
  adminEmail: string,
  catalog: string,
  toVersion: number
): Promise<ContentOutcome> {
  if (!Number.isFinite(toVersion) || toVersion < 1) {
    return fail(400, "toVersion must be a positive version number.");
  }

  if (isMockMode()) {
    const store = mockDb();
    const meta = store.contentCatalogs.find((c) => c.name === catalog);
    if (!meta) return fail(404, `Unknown catalog "${catalog}".`);
    const restored = store.contentVersions.find(
      (v) => v.catalog === catalog && v.version === toVersion
    );
    if (!restored) return fail(404, `${catalog} has no version ${toVersion}.`);
    const version = meta.publishedVersion + 1;
    meta.publishedVersion = version;
    // Rollback publishes FORWARD, so it creates a version too.
    store.contentVersions.unshift({
      catalog,
      version,
      publishedBy: adminEmail,
      publishedAt: new Date().toISOString(),
      note: `rollback of v${toVersion}`,
      rowCount: restored.rowCount,
    });
    await writeAudit(adminEmail, `content.rollback:${catalog}`, null, "content_rows",
      { catalog, toVersion }, { catalog, version, mock: true });
    return ok(`Rolled ${catalog} back to v${toVersion}, published as v${version}.`, { version });
  }

  const before = await fetchDiff(catalog);

  // The rows this rollback is about to make live. Read from the snapshot rather
  // than from `content_rows` after the fact, so the mirror can be written BEFORE
  // the rpc — the same ordering publish uses, for the same reason: a mirror
  // failure must mean the rollback never happened, not that it half happened.
  const snapshot = await fetchVersionSnapshot(catalog, toVersion);
  if (snapshot === null) {
    return fail(404, `${catalog} has no version ${toVersion} to roll back to.`);
  }

  const mirrorProblem = await mirrorForCatalog(catalog, snapshot);
  if (mirrorProblem) {
    return fail(
      502,
      `Mirror write failed, so nothing was rolled back: ${mirrorProblem.error}. ` +
        `${mirrorProblem.detail} A rollback is a publish carrying old content, so it ` +
        "has to move the mirror too — otherwise the catalog goes back and the price does not."
    );
  }

  const res = await getSupabaseAdmin().rpc("content_rollback", {
    p_catalog: catalog,
    p_to_version: toVersion,
    p_by: adminEmail,
  });
  if (res.error) return fail(400, `content_rollback failed: ${res.error.message}`);
  const version = Number(res.data);

  await writeAudit(
    adminEmail,
    `content.rollback:${catalog}`,
    null,
    "content_rows",
    { catalog, version: before.publishedVersion },
    { catalog, restoredFrom: toVersion, version, mirrored: MIRRORED_CATALOGS.includes(catalog) }
  );
  return ok(`Rolled ${catalog} back to v${toVersion}, published forward as v${version}.`, { version });
}

// ---------------------------------------------------------------------------
// Kill switch
// ---------------------------------------------------------------------------

/**
 * §7.4, the PER-CATALOG half. `is_enabled = false` makes ONE catalog vanish from
 * /api/v1/content and names it in the response's top-level `disabled` list —
 * never an empty catalog, which a client could reasonably apply as "everything
 * was deleted". That catalog reverts to its bundled CSV; no other is touched.
 *
 * ⚠️ IT DELIBERATELY DOES NOT TOUCH THE SERVER-SIDE MIRRORS, and that is a
 * decision rather than an oversight (REDTEAM_REVIEW §2, secondary finding).
 *
 * Killing `modes` reverts clients to their bundled modes.csv, while
 * `golfin_mode_fees` keeps whatever the last publish wrote. If a fee had been
 * published but not yet exported into a shipped build, the two disagree. All
 * three available behaviours were considered:
 *
 *   1. DELETE the mirror rows on kill. Then /points/spend answers `unknown_mode`
 *      for every mode and NOBODY can enter ANY mode. Strictly worse than the
 *      disagreement it fixes.
 *   2. Have /spend skip fee validation while the catalog is disabled. That turns
 *      this button into "switch off fee enforcement", i.e. it hands back the
 *      client-asserted price the whole of game_modes_admin exists to take away.
 *      A kill switch must never be an authorisation bypass.
 *   3. LEAVE IT (what happens today). The client shows the bundled fee, the
 *      server prices from the published one, and the mismatch surfaces as
 *      `fee_changed` — the card re-prices to the server's number and the second
 *      tap pays it. Nobody is locked out and nobody is charged a number they
 *      were not shown first.
 *
 * 3 is the only one that is safe in every direction, so the residual
 * disagreement is accepted and bounded by the `fee_changed` UX rather than
 * engineered away. ROLLBACK is a different case and IS fixed — a rollback
 * changes what the catalog SERVES, so the mirror must follow it; a kill switch
 * stops serving the catalog at all, so there is nothing for the mirror to follow.
 *
 * ⚠️ IT DOES NOT DROP THE TOP-LEVEL `enabled` FLAG, and the wording that said it
 * did was the bug. Until 2026-08-26 the endpoint ANDed this column across the
 * REQUESTED catalogs into top-level `enabled`, and the client drops EVERY cache
 * on `enabled:false` — so disabling one catalog reverted all seven on every
 * client (content_kill_switch_and_order). The global switch is
 * `setGlobalContentEnabled` below, and it is a different row in a different
 * table on purpose.
 */
export async function setCatalogEnabled(
  adminEmail: string,
  catalog: string,
  enabled: boolean
): Promise<ContentOutcome> {
  if (isMockMode()) {
    const meta = mockDb().contentCatalogs.find((c) => c.name === catalog);
    if (!meta) return fail(404, `Unknown catalog "${catalog}".`);
    const before = meta.isEnabled;
    meta.isEnabled = enabled;
    await writeAudit(adminEmail, `content.enabled:${catalog}`, null, "content_catalogs",
      { catalog, is_enabled: before }, { catalog, is_enabled: enabled });
    return ok(`${catalog} is now ${enabled ? "ENABLED" : "DISABLED"}.`);
  }

  const supabase = getSupabaseAdmin();
  const current = await supabase
    .from("content_catalogs")
    .select("is_enabled")
    .eq("name", catalog)
    .maybeSingle();
  if (current.error) return fail(500, `content_catalogs read failed: ${current.error.message}`);
  if (!current.data) return fail(404, `Unknown catalog "${catalog}".`);

  const res = await supabase
    .from("content_catalogs")
    .update({ is_enabled: enabled, updated_at: new Date().toISOString() })
    .eq("name", catalog);
  if (res.error) return fail(500, `content_catalogs update failed: ${res.error.message}`);

  await writeAudit(
    adminEmail,
    `content.enabled:${catalog}`,
    null,
    "content_catalogs",
    { catalog, is_enabled: (current.data as { is_enabled: boolean }).is_enabled },
    { catalog, is_enabled: enabled }
  );
  return ok(`${catalog} is now ${enabled ? "ENABLED" : "DISABLED"} for the game.`);
}

/**
 * §7.4, the GLOBAL half — `content_settings.content_enabled`.
 *
 * ⚠️ THIS IS THE BIG ONE. `false` means every client ignores the whole content response and drops
 * EVERY catalog's cache, reverting the game to its bundled CSVs until it is flipped back. The
 * per-catalog switch above takes one catalog back to bundled; this takes all of them, for every
 * player.
 *
 * WHY IT EXISTS AS A BUTTON. §7.4 promises "one flag, no deploy" for both switches, and until now
 * only the per-catalog one had a control — the global flag needed a hand-written SQL `update`,
 * which is not "no deploy" in any sense an operator at 2am would recognise. The flag was always a
 * DB row precisely so it could be flipped from here; this is the missing half of that decision.
 *
 * NOT INSTANT, and the UI says so: a 60 s response cache plus apply-at-next-launch (I5) means up
 * to a minute to reach a client, landing at its next launch. Re-enabling costs another launch,
 * because a killed client has already dropped its caches and has to refetch.
 *
 * UPSERT, not update: the row is seeded by the migration, but a project where the migration has
 * not run reads as ENABLED (the endpoint fails open), and there the operator's flip has to CREATE
 * the row or it would silently do nothing.
 */
export async function setGlobalContentEnabled(
  adminEmail: string,
  enabled: boolean
): Promise<ContentOutcome> {
  const before = await fetchGlobalContentEnabled();

  if (isMockMode()) {
    mockDb().contentGlobalEnabled = enabled;
  } else {
    const res = await getSupabaseAdmin()
      .from("content_settings")
      .upsert(
        { key: GLOBAL_ENABLED_KEY, value: enabled, updated_at: new Date().toISOString() },
        { onConflict: "key" }
      );
    if (res.error) return fail(500, `content_settings update failed: ${res.error.message}`);
  }

  // targetUser stays null — the target is the whole content pipeline, not a user. The action
  // string is deliberately NOT `content.enabled:*`, which is the per-catalog action: an audit
  // reader must be able to tell "someone killed the bags catalog" from "someone killed remote
  // content for every player" without opening the payload.
  await writeAudit(
    adminEmail,
    "content.global_enabled",
    null,
    "content_settings",
    { key: GLOBAL_ENABLED_KEY, value: before },
    { key: GLOBAL_ENABLED_KEY, value: enabled }
  );

  return ok(
    enabled
      ? "Remote content is ENABLED globally. Clients pick it up within ~60s, applied at their next launch."
      : "Remote content is KILLED globally — every client reverts to its bundled CSVs at its next launch."
  );
}
