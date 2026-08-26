import "server-only";
import { writeAudit } from "./audit";
import {
  fetchAllRows,
  fetchDiff,
  fetchGlobalContentEnabled,
  GLOBAL_ENABLED_KEY,
  REFERENCED_CATALOGS,
} from "./contentData";
import { hasErrors, validateCatalog, type ContentProblem, type DraftRow } from "./contentValidate";
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

/** Upsert ONE draft row. Drafts are never served, so this needs no validation —
 *  publish is the gate (§D1), and blocking a half-typed row would make the
 *  editor unusable. */
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
  if (catalog === "shop_catalog") {
    for (const other of REFERENCED_CATALOGS) {
      const rows = await fetchAllRows("content_drafts", other);
      otherCatalogs.set(other, new Map(rows.map((r) => [r.rowId, toDraftRow(r)])));
    }
  }

  const problems = validateCatalog(catalog, drafts.map(toDraftRow), {
    publishedMinBuild: new Map(published.map((r) => [r.rowId, r.minBuild])),
    otherCatalogs,
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

  if (catalog === "characters") {
    const mirrorError = await mirrorCharacters(drafts);
    if (mirrorError) {
      return fail(
        502,
        `golfin_characters mirror write failed, so nothing was published: ${mirrorError}. ` +
          "The mirror is what tournament rarity restrictions read (SPEC §A4); publishing " +
          "characters without it would reintroduce the char_olivia drift."
      );
    }
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
    { catalog, version, note: note ?? null, mirroredToGolfinCharacters: catalog === "characters" }
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
    { catalog, restoredFrom: toVersion, version }
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
