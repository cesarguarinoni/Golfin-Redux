import "server-only";
import { createHash, randomUUID } from "node:crypto";
import { writeAudit } from "./audit";
import { findCourse, ART_SPEC } from "./courses";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { fetchTournaments } from "./tournamentData";
import {
  deriveState,
  isLive,
  validateHoleSet,
  validatePrizeBands,
  validateRestrictions,
  validateSlug,
} from "./tournament";
import type { MutationOutcome } from "./mutations";
import type { PrizeBand, TournamentInput, TournamentRow } from "./types";

/**
 * Write side of the Tournaments panel (SPEC §5).
 * Every path: server-only, called after checkAdmin(), audited with before/after,
 * with a mock branch so the UI is exercisable on fixtures.
 */

const ok = (message: string, data?: unknown): MutationOutcome & { data?: unknown } => ({
  ok: true,
  status: 200,
  message,
  data,
});
const fail = (status: number, message: string): MutationOutcome => ({
  ok: false,
  status,
  message,
});

/** Public-read Storage bucket holding per-tournament art (SPEC §5c.2). */
export const ART_BUCKET = "tournament-art";

// ---------------------------------------------------------------------------
// Validation — one gate both create and update go through.
// ---------------------------------------------------------------------------

export function validateInput(input: TournamentInput): string | null {
  const slugErr = validateSlug(input.slug);
  if (slugErr) return slugErr;

  const title = input.title.trim();
  if (title.length < 1 || title.length > 80) return "Title is required (1–80 characters).";

  const titleJa = input.titleJa?.trim() ?? "";
  if (titleJa.length > 80) return "Japanese title must be 80 characters or fewer.";

  if (!findCourse(input.courseId)) {
    return `Unknown course "${input.courseId}" — the game has no art or venue name for it.`;
  }

  const holeErr = validateHoleSet(input.holeSet);
  if (holeErr) return holeErr;

  const start = Date.parse(input.startAt);
  const end = Date.parse(input.endAt);
  if (Number.isNaN(start)) return "Start time is not a valid date.";
  if (Number.isNaN(end)) return "End time is not a valid date.";
  if (end <= start) return "End time must be after start time.";

  if (!Number.isInteger(input.entryFeePts) || input.entryFeePts < 0 || input.entryFeePts > 10_000) {
    return "Entry fee must be a whole number of RP between 0 and 10,000.";
  }
  if (
    !Number.isInteger(input.resolveDelayMinutes) ||
    input.resolveDelayMinutes < 0 ||
    input.resolveDelayMinutes > 1440
  ) {
    return "Resolve delay must be 0–1440 minutes.";
  }
  if (!input.botFieldId) return "A bot field is required.";

  // 600 chars: the Figma blurb is 268 and its box is FIXED at 360px tall, so a much
  // longer string overflows the info row on device rather than scrolling.
  const descEn = input.descriptionEn?.trim() ?? "";
  if (descEn.length > 600) return "English description must be 600 characters or fewer.";
  const descJa = input.descriptionJa?.trim() ?? "";
  if (descJa.length > 600) return "Japanese description must be 600 characters or fewer.";

  if (input.bannerUrl) {
    const hostErr = validateArtUrl(input.bannerUrl);
    if (hostErr) return hostErr;
  }

  const bandErrors = validatePrizeBands(input.bands);
  if (bandErrors.length > 0) return bandErrors.join(" ");

  const restrErr = validateRestrictions(input);
  if (restrErr) return restrErr;

  return null;
}

/**
 * 🔒 banner_url must point at this project's own Supabase Storage host.
 * The client enforces the same rule (SPEC §5c.2) — a free-text URL shipped to
 * every player is a way to serve arbitrary content and leak player IPs, and
 * `/tournaments/admin/create` can still write this table behind a static key.
 */
export function validateArtUrl(url: string): string | null {
  const base = process.env.SUPABASE_URL ?? process.env.NEXT_PUBLIC_SUPABASE_URL;
  if (!base) return null; // mock mode: nothing to compare against
  let allowedHost: string;
  let host: string;
  try {
    allowedHost = new URL(base).host;
    const parsed = new URL(url);
    host = parsed.host;
    if (parsed.protocol !== "https:") return "Artwork URL must be https.";
  } catch {
    return "Artwork URL is not a valid URL.";
  }
  if (host !== allowedHost) {
    return `Artwork must live in this project's Supabase Storage (${allowedHost}), not ${host}.`;
  }
  return null;
}

/** Guardrail: touching a tournament players may be mid-entry in (SPEC §5). */
function liveGuard(t: TournamentRow, confirmSlug: string | undefined): string | null {
  const state = deriveState(t.startAt, t.endAt, Date.now());
  if (!isLive(state)) return null;
  if ((confirmSlug ?? "").trim() !== t.slug) {
    return `"${t.slug}" is ${state} — players may be mid-entry. Re-type the slug to confirm.`;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Snapshots for the audit trail
// ---------------------------------------------------------------------------

function snapshot(t: TournamentRow): Record<string, unknown> {
  return {
    slug: t.slug,
    title: t.title,
    title_ja: t.titleJa,
    name_key: t.nameKey,
    course_id: t.courseId,
    hole_set: t.holeSet,
    start_at: t.startAt,
    end_at: t.endAt,
    resolve_delay_minutes: t.resolveDelayMinutes,
    entry_fee_pts: t.entryFeePts,
    bot_field_id: t.botFieldId,
    sponsor_name: t.sponsorName,
    league_key: t.leagueKey,
    banner_url: t.bannerUrl,
    modal_banner_id: t.modalBannerId,
    description_en: t.descriptionEn,
    description_ja: t.descriptionJa,
    description_key: t.descriptionKey,
    is_active: t.isActive,
    category: t.category,
    max_players: t.maxPlayers,
    players_per_division: t.playersPerDivision,
    division_type: t.divisionType,
    char_rarity_min: t.charRarityMin,
    char_rarity_max: t.charRarityMax,
    char_level_min: t.charLevelMin,
    char_level_max: t.charLevelMax,
    gear_rule: t.gearRule,
    club_rarity_max: t.clubRarityMax,
    bands: t.bands.map((b) => ({
      rank_from: b.rankFrom,
      rank_to: b.rankTo,
      rp_reward: b.rpReward,
      item_reward_id: b.itemRewardId,
    })),
  };
}

function toDbRow(input: TournamentInput): Record<string, unknown> {
  return {
    kind: "golfin",
    slug: input.slug,
    title: input.title.trim(),
    title_ja: input.titleJa?.trim() || null,
    name_key: input.nameKey?.trim() || null,
    course_id: input.courseId,
    hole_set: input.holeSet.trim(),
    start_at: new Date(input.startAt).toISOString(),
    end_at: new Date(input.endAt).toISOString(),
    resolve_delay_minutes: input.resolveDelayMinutes,
    entry_fee_pts: input.entryFeePts,
    bot_field_id: input.botFieldId,
    sponsor_name: input.sponsorName?.trim() || null,
    league_key: input.leagueKey || null,
    banner_url: input.bannerUrl || null,
    modal_banner_id: input.modalBannerId || null,
    description_en: input.descriptionEn?.trim() || null,
    description_ja: input.descriptionJa?.trim() || null,
    description_key: input.descriptionKey?.trim() || null,
    is_active: input.isActive,
    // Restrictions (tournament_restrictions): null = unrestricted. gear_rule
    // "supplied" is refused by validateRestrictions until standard-spec lands.
    category: input.category || null,
    max_players: input.maxPlayers,
    players_per_division: input.playersPerDivision,
    division_type: input.divisionType || null,
    char_rarity_min: input.charRarityMin || null,
    char_rarity_max: input.charRarityMax || null,
    char_level_min: input.charLevelMin,
    char_level_max: input.charLevelMax,
    gear_rule: input.gearRule || null,
    club_rarity_max: input.clubRarityMax || null,
    // tier/status are GPS columns; 'open'/'upcoming' satisfy their CHECK
    // constraints. State for golfin rows is DERIVED from the dates (SPEC §4.1).
    tier: "open",
    status: "upcoming",
  };
}

async function loadOne(id: string): Promise<TournamentRow | undefined> {
  const { tournaments } = await fetchTournaments();
  return tournaments.find((t) => t.id === id);
}

function bandRows(tournamentId: string, bands: PrizeBand[]): Record<string, unknown>[] {
  return bands.map((b) => ({
    tournament_id: tournamentId,
    rank_from: b.rankFrom,
    rank_to: b.rankTo,
    rp_reward: b.rpReward,
    item_reward_id: b.itemRewardId?.trim() || null,
  }));
}


/**
 * A non-null `modalBannerId` must name an EXISTING `game_banners` row whose
 * placement is `tournament_modal`. A dangling id, or one pointing at a
 * `home_promo` / `rankings` row, is a 400 — never a silent write.
 *
 * Why it is not in `validateInput`: that function is synchronous and pure, and
 * this needs the database. Both create and update call it right after.
 *
 * Returns an error string, or null when the id is absent or valid.
 */
export async function validateModalBannerId(
  modalBannerId: string | null | undefined
): Promise<string | null> {
  const id = modalBannerId?.trim();
  if (!id) return null; // "None" is always valid — the modal renders without a strip.

  if (isMockMode()) {
    const b = mockDb().banners?.find((x) => x.id === id);
    if (!b) return `Banner "${id}" does not exist.`;
    if (b.placement !== "tournament_modal") {
      return `Banner "${b.label}" is a ${b.placement} banner. Only tournament_modal banners can be assigned to a tournament.`;
    }
    return null;
  }

  const admin = getSupabaseAdmin();
  const { data, error } = await admin
    .from("game_banners")
    .select("id, label, placement")
    .eq("id", id)
    .maybeSingle();

  // A query failure is not a validation pass. Refuse rather than write an id
  // we could not check.
  if (error) return `Could not verify the banner: ${error.message}`;
  if (!data) return `Banner "${id}" does not exist.`;
  if (data.placement !== "tournament_modal") {
    return `Banner "${data.label}" is a ${data.placement} banner. Only tournament_modal banners can be assigned to a tournament.`;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Create
// ---------------------------------------------------------------------------

export async function createTournament(
  adminEmail: string,
  input: TournamentInput
): Promise<MutationOutcome> {
  const err = validateInput(input);
  if (err) return fail(400, err);

  const bannerErr = await validateModalBannerId(input.modalBannerId);
  if (bannerErr) return fail(400, bannerErr);

  const { tournaments } = await fetchTournaments();
  if (tournaments.some((t) => t.slug === input.slug)) {
    return fail(409, `Slug "${input.slug}" is already taken.`);
  }

  // Fixed per tournament so every player sees the same bot field.
  const botSeed = Math.floor(Math.random() * 2_000_000_000) + 1;

  if (isMockMode()) {
    const db = mockDb();
    const id = randomUUID();
    const row: TournamentRow = {
      id,
      kind: "golfin",
      slug: input.slug,
      title: input.title.trim(),
      titleJa: input.titleJa?.trim() || null,
      nameKey: input.nameKey?.trim() || null,
      courseId: input.courseId,
      holeSet: input.holeSet.trim(),
      startAt: new Date(input.startAt).toISOString(),
      endAt: new Date(input.endAt).toISOString(),
      resolveDelayMinutes: input.resolveDelayMinutes,
      entryFeePts: input.entryFeePts,
      botFieldId: input.botFieldId,
      sponsorName: input.sponsorName?.trim() || null,
      leagueKey: input.leagueKey || null,
      bannerUrl: input.bannerUrl || null,
      modalBannerId: input.modalBannerId || null,
      descriptionEn: input.descriptionEn?.trim() || null,
      descriptionJa: input.descriptionJa?.trim() || null,
      descriptionKey: input.descriptionKey?.trim() || null,
      isActive: input.isActive,
      category: input.category || null,
      maxPlayers: input.maxPlayers,
      playersPerDivision: input.playersPerDivision,
      divisionType: input.divisionType || null,
      charRarityMin: input.charRarityMin || null,
      charRarityMax: input.charRarityMax || null,
      charLevelMin: input.charLevelMin,
      charLevelMax: input.charLevelMax,
      gearRule: input.gearRule || null,
      clubRarityMax: input.clubRarityMax || null,
      botSeed,
      status: "upcoming",
      tier: "open",
      createdAt: new Date().toISOString(),
      bands: input.bands.map((b) => ({ ...b, id: randomUUID() })),
      entryCount: 0,
      humanEntryCount: 0,
    };
    db.tournaments.unshift(row);
    db.tournamentEntries[id] = [];
    await writeAudit(adminEmail, "tournament_create", null, "tournaments", null, snapshot(row));
    return ok(`Created "${input.slug}".`);
  }

  const admin = getSupabaseAdmin();
  const insertRes = await admin
    .from("tournaments")
    .insert({ ...toDbRow(input), bot_seed: botSeed })
    .select("id")
    .single();
  if (insertRes.error) return fail(500, `Insert failed: ${insertRes.error.message}`);

  const newId = String((insertRes.data as { id: string }).id);
  const bandsRes = await admin
    .from("tournament_prize_bands")
    .insert(bandRows(newId, input.bands));
  if (bandsRes.error) {
    // Never leave a tournament with no prize ladder behind.
    await admin.from("tournaments").delete().eq("id", newId);
    return fail(500, `Prize bands failed (tournament rolled back): ${bandsRes.error.message}`);
  }

  const created = await loadOne(newId);
  await writeAudit(
    adminEmail,
    "tournament_create",
    null,
    "tournaments",
    null,
    created ? snapshot(created) : { id: newId, slug: input.slug }
  );
  return ok(`Created "${input.slug}".`);
}

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------

export async function updateTournament(
  adminEmail: string,
  id: string,
  input: TournamentInput
): Promise<MutationOutcome> {
  const err = validateInput(input);
  if (err) return fail(400, err);

  const bannerErr = await validateModalBannerId(input.modalBannerId);
  if (bannerErr) return fail(400, bannerErr);

  const existing = await loadOne(id);
  if (!existing) return fail(404, "Tournament not found.");
  if (existing.kind !== "golfin") {
    return fail(400, "This panel only edits kind='golfin' rows. GPS events are managed elsewhere.");
  }

  const guard = liveGuard(existing, input.confirmSlug);
  if (guard) return fail(409, guard);

  const { tournaments } = await fetchTournaments();
  if (tournaments.some((t) => t.slug === input.slug && t.id !== id)) {
    return fail(409, `Slug "${input.slug}" is already taken by another tournament.`);
  }

  const before = snapshot(existing);

  if (isMockMode()) {
    const db = mockDb();
    const row = db.tournaments.find((t) => t.id === id);
    if (!row) return fail(404, "Tournament not found.");
    Object.assign(row, {
      slug: input.slug,
      title: input.title.trim(),
      titleJa: input.titleJa?.trim() || null,
      nameKey: input.nameKey?.trim() || null,
      courseId: input.courseId,
      holeSet: input.holeSet.trim(),
      startAt: new Date(input.startAt).toISOString(),
      endAt: new Date(input.endAt).toISOString(),
      resolveDelayMinutes: input.resolveDelayMinutes,
      entryFeePts: input.entryFeePts,
      botFieldId: input.botFieldId,
      sponsorName: input.sponsorName?.trim() || null,
      leagueKey: input.leagueKey || null,
      bannerUrl: input.bannerUrl || null,
      modalBannerId: input.modalBannerId || null,
      descriptionEn: input.descriptionEn?.trim() || null,
      descriptionJa: input.descriptionJa?.trim() || null,
      descriptionKey: input.descriptionKey?.trim() || null,
      isActive: input.isActive,
      category: input.category || null,
      maxPlayers: input.maxPlayers,
      playersPerDivision: input.playersPerDivision,
      divisionType: input.divisionType || null,
      charRarityMin: input.charRarityMin || null,
      charRarityMax: input.charRarityMax || null,
      charLevelMin: input.charLevelMin,
      charLevelMax: input.charLevelMax,
      gearRule: input.gearRule || null,
      clubRarityMax: input.clubRarityMax || null,
      bands: input.bands.map((b) => ({ ...b, id: b.id || randomUUID() })),
    });
    await writeAudit(adminEmail, "tournament_update", null, "tournaments", before, snapshot(row));
    return ok(`Saved "${input.slug}".`);
  }

  const admin = getSupabaseAdmin();
  const upd = await admin.from("tournaments").update(toDbRow(input)).eq("id", id);
  if (upd.error) return fail(500, `Update failed: ${upd.error.message}`);

  // Bands are replaced wholesale — the editor always sends the full ladder.
  const del = await admin.from("tournament_prize_bands").delete().eq("tournament_id", id);
  if (del.error) return fail(500, `Clearing prize bands failed: ${del.error.message}`);
  const ins = await admin.from("tournament_prize_bands").insert(bandRows(id, input.bands));
  if (ins.error) {
    return fail(
      500,
      `Prize bands failed to save — THIS TOURNAMENT NOW HAS NO PRIZE LADDER, re-save it: ${ins.error.message}`
    );
  }

  const after = await loadOne(id);
  await writeAudit(
    adminEmail,
    "tournament_update",
    null,
    "tournaments",
    before,
    after ? snapshot(after) : null
  );
  return ok(`Saved "${input.slug}".`);
}

// ---------------------------------------------------------------------------
// Duplicate — the fastest way to build next month's schedule.
// ---------------------------------------------------------------------------

export async function duplicateTournament(
  adminEmail: string,
  id: string,
  newSlug: string
): Promise<MutationOutcome> {
  const source = await loadOne(id);
  if (!source) return fail(404, "Tournament not found.");

  const slug = newSlug.trim();
  const slugErr = validateSlug(slug);
  if (slugErr) return fail(400, slugErr);

  // Same length, shifted so the copy opens a week after the original closed —
  // a sane default the editor can then adjust.
  const start = source.startAt ? Date.parse(source.startAt) : Date.now();
  const end = source.endAt ? Date.parse(source.endAt) : start + 7 * 86_400_000;
  const shift = end - start + 7 * 86_400_000;

  const input: TournamentInput = {
    slug,
    title: `${source.title} (copy)`,
    titleJa: source.titleJa ? `${source.titleJa}（コピー）` : null,
    nameKey: source.nameKey,
    modalBannerId: source.modalBannerId,
    descriptionEn: source.descriptionEn,
    descriptionJa: source.descriptionJa,
    descriptionKey: source.descriptionKey,
    courseId: source.courseId ?? "",
    holeSet: source.holeSet ?? "1-18",
    startAt: new Date(start + shift).toISOString(),
    endAt: new Date(end + shift).toISOString(),
    resolveDelayMinutes: source.resolveDelayMinutes ?? 30,
    entryFeePts: source.entryFeePts,
    botFieldId: source.botFieldId ?? "field_small",
    sponsorName: source.sponsorName,
    leagueKey: source.leagueKey,
    // Art is deliberately NOT copied: the same file under a new tournament
    // would be right by accident. The editor prompts for new art.
    bannerUrl: null,
    // A copy starts switched OFF — you almost never want a duplicate live
    // the instant it is created, before its dates and art are right.
    isActive: false,
    // Restrictions copy verbatim — a duplicated bracket keeps its rules.
    category: source.category,
    maxPlayers: source.maxPlayers,
    playersPerDivision: source.playersPerDivision,
    divisionType: source.divisionType,
    charRarityMin: source.charRarityMin,
    charRarityMax: source.charRarityMax,
    charLevelMin: source.charLevelMin,
    charLevelMax: source.charLevelMax,
    gearRule: source.gearRule,
    clubRarityMax: source.clubRarityMax,
    bands: source.bands.map((b) => ({ ...b, id: "" })),
  };

  const outcome = await createTournament(adminEmail, input);
  if (!outcome.ok) return outcome;
  return ok(`Duplicated "${source.slug}" → "${slug}". Dates shifted; artwork not copied.`);
}

// ---------------------------------------------------------------------------
// Delete — double-confirm, states the cascade.
// ---------------------------------------------------------------------------

export async function deleteTournament(
  adminEmail: string,
  id: string,
  confirmSlug: string
): Promise<MutationOutcome> {
  const existing = await loadOne(id);
  if (!existing) return fail(404, "Tournament not found.");
  if (confirmSlug.trim() !== existing.slug) {
    return fail(400, "Confirmation slug does not match.");
  }

  const before = { ...snapshot(existing), entry_count: existing.entryCount };

  if (isMockMode()) {
    const db = mockDb();
    db.tournaments = db.tournaments.filter((t) => t.id !== id);
    delete db.tournamentEntries[id];
    await writeAudit(adminEmail, "tournament_delete", null, "tournaments", before, null);
    return ok(`Deleted "${existing.slug}" and ${existing.entryCount} entries.`);
  }

  const admin = getSupabaseAdmin();
  const { error } = await admin.from("tournaments").delete().eq("id", id);
  if (error) return fail(500, `Delete failed: ${error.message}`);

  await writeAudit(adminEmail, "tournament_delete", null, "tournaments", before, null);
  return ok(
    `Deleted "${existing.slug}" — ${existing.entryCount} entries and ${existing.bands.length} prize bands cascaded.`
  );
}

// ---------------------------------------------------------------------------
// Artwork upload (SPEC §5c.4)
// ---------------------------------------------------------------------------

export interface ArtUploadResult extends MutationOutcome {
  url?: string;
}

/**
 * Validates then uploads to the public `tournament-art` bucket.
 * Immutable naming ({slug}-{content-hash}.{ext}) so the URL IS the cache key —
 * a changed image is a new URL and there is no invalidation problem to solve.
 */
export async function uploadTournamentArt(
  adminEmail: string,
  slug: string,
  file: File
): Promise<ArtUploadResult> {
  const slugErr = validateSlug(slug);
  if (slugErr) return fail(400, slugErr);

  if (!(ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
    return fail(400, `Unsupported type "${file.type || "unknown"}". Use JPG, PNG or WebP.`);
  }
  if (file.size > ART_SPEC.maxBytes) {
    return fail(
      400,
      `Image is ${(file.size / 1024).toFixed(0)} KB — the cap is ${ART_SPEC.maxBytes / 1024} KB. Every mobile player downloads this.`
    );
  }
  if (file.size === 0) return fail(400, "File is empty.");

  const bytes = Buffer.from(await file.arrayBuffer());
  const hash = createHash("sha256").update(bytes).digest("hex").slice(0, 12);
  const ext = file.type === "image/png" ? "png" : file.type === "image/webp" ? "webp" : "jpg";
  const path = `${slug}-${hash}.${ext}`;

  if (isMockMode()) {
    // No Storage in mock mode — hand back a stable fake so the editor flow works.
    const url = `https://mock.supabase.local/storage/v1/object/public/${ART_BUCKET}/${path}`;
    await writeAudit(adminEmail, "tournament_art_upload", null, "storage", null, {
      path,
      bytes: file.size,
      mock: true,
    });
    return { ...ok(`Uploaded ${path} (mock).`), url };
  }

  const admin = getSupabaseAdmin();

  // Create the bucket on first use so this needs no manual Supabase step.
  const buckets = await admin.storage.listBuckets();
  if (!buckets.error && !buckets.data.some((b) => b.name === ART_BUCKET)) {
    const created = await admin.storage.createBucket(ART_BUCKET, {
      public: true,
      fileSizeLimit: ART_SPEC.maxBytes,
      allowedMimeTypes: [...ART_SPEC.mimeTypes],
    });
    if (created.error) return fail(500, `Could not create bucket: ${created.error.message}`);
  }

  const up = await admin.storage.from(ART_BUCKET).upload(path, bytes, {
    contentType: file.type,
    cacheControl: "31536000",
    upsert: true, // same bytes → same path, so this is a no-op re-write
  });
  if (up.error) return fail(500, `Upload failed: ${up.error.message}`);

  const { data } = admin.storage.from(ART_BUCKET).getPublicUrl(path);
  const url = data.publicUrl;

  const hostErr = validateArtUrl(url);
  if (hostErr) return fail(500, `Storage returned an unexpected URL: ${hostErr}`);

  await writeAudit(adminEmail, "tournament_art_upload", null, "storage", null, {
    path,
    bytes: file.size,
    url,
  });
  return { ...ok(`Uploaded ${path}.`), url };
}

// ---------------------------------------------------------------------------
// CSV export (SPEC §5) — the panel is useful as an authoring tool TODAY:
// edit here → export → commit → build, until Phase 3 makes it live.
// ---------------------------------------------------------------------------

function csvCell(value: string | number | null): string {
  const s = value === null ? "" : String(value);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export async function exportTournamentsCsv(): Promise<{
  tournaments: string;
  prizes: string;
}> {
  const { tournaments } = await fetchTournaments();
  const golfin = tournaments
    .filter((t) => t.kind === "golfin")
    .sort((a, b) => (a.startAt ?? "").localeCompare(b.startAt ?? ""));

  // Bands are per-tournament in the DB but the CSV keys them by a shared table
  // id, so identical ladders are de-duplicated back into one table.
  const tableIdByFingerprint = new Map<string, string>();
  const prizeRows: string[] = [];

  const tHeader =
    "id,nameKey,courseId,holeSet,startUtc,endUtc,resolveDelayMinutes,entryFeeRP,botFieldId,prizeTableId,sponsorKey,leagueKey";
  const tRows: string[] = [];

  for (const t of golfin) {
    const sorted = [...t.bands].sort((a, b) => a.rankFrom - b.rankFrom);
    const fingerprint = JSON.stringify(
      sorted.map((b) => [b.rankFrom, b.rankTo, b.rpReward, b.itemRewardId ?? ""])
    );
    let tableId = tableIdByFingerprint.get(fingerprint);
    if (!tableId) {
      tableId = `prize_${t.slug}`;
      tableIdByFingerprint.set(fingerprint, tableId);
      for (const b of sorted) {
        prizeRows.push(
          [tableId, b.rankFrom, b.rankTo, b.rpReward, b.itemRewardId ?? ""].map(csvCell).join(",")
        );
      }
    }
    tRows.push(
      [
        t.slug,
        // Missing key → emit the title. The client localizer falls back to the
        // raw string, so the card renders the title rather than nothing.
        t.nameKey ?? t.title,
        t.courseId,
        t.holeSet,
        t.startAt ? new Date(t.startAt).toISOString().replace(".000", "") : "",
        t.endAt ? new Date(t.endAt).toISOString().replace(".000", "") : "",
        t.resolveDelayMinutes ?? 30,
        t.entryFeePts,
        t.botFieldId,
        tableId,
        t.sponsorName,
        t.leagueKey,
      ]
        .map(csvCell)
        .join(",")
    );
  }

  const banner =
    "# Exported from the GOLFIN admin dashboard. Row order = render order on the T7 screen.\n" +
    "# startUtc/endUtc are ABSOLUTE UTC on purpose — this file ships.\n";

  return {
    tournaments: `${banner}${tHeader}\n${tRows.join("\n")}\n`,
    prizes:
      "# Rank-band prize tables. rpReward = RP; itemRewardId blank = RP only.\n" +
      "# Exported from the admin dashboard: bands are per-tournament server-side and\n" +
      "# identical ladders are de-duplicated back into one table id here.\n" +
      "prizeTableId,rankFrom,rankTo,rpReward,itemRewardId\n" +
      `${prizeRows.join("\n")}\n`,
  };
}
