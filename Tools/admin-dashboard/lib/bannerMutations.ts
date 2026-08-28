import "server-only";
import { createHash, randomUUID } from "node:crypto";
import {
  BANNER_ART_SPEC,
  BANNER_BUCKET,
  deriveBannerState,
  isBannerPlacement,
  validateBannerArtUrl,
  validateBannerInput,
} from "./banner";
import { fetchBanners } from "./bannerData";
import { writeAudit } from "./audit";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { MutationOutcome } from "./mutations";
import type { BannerInput, BannerPlacement, BannerRow } from "./types";

/**
 * Write side of the Banners panel (SPEC game_banners §3).
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

export type BannerLocale = "en" | "ja";

// ---------------------------------------------------------------------------
// Guardrails
// ---------------------------------------------------------------------------

/**
 * Switching a LIVE banner off is player-facing and instant — the next client
 * fetch drops it and the slot disappears, with the surrounding UI closing up.
 * Same shape as
 * the tournament editor's confirmSlug: re-type the label to mean it.
 *
 * Only *deactivation* is guarded. Turning one ON, or editing a draft, is
 * reversible in the same click.
 */
function liveOffGuard(
  existing: BannerRow,
  nextActive: boolean,
  confirmLabel: string | undefined
): string | null {
  if (nextActive) return null;
  if (deriveBannerState(existing, Date.now()) !== "LIVE") return null;
  if ((confirmLabel ?? "").trim() !== existing.label) {
    return `"${existing.label}" is LIVE — players are seeing it right now. Re-type the label to confirm switching it off.`;
  }
  return null;
}

function snapshot(b: BannerRow): Record<string, unknown> {
  return {
    placement: b.placement,
    label: b.label,
    image_url_en: b.imageUrlEn,
    image_url_ja: b.imageUrlJa,
    link_url: b.linkUrl,
    start_at: b.startAt,
    end_at: b.endAt,
    sort_order: b.sortOrder,
    is_active: b.isActive,
  };
}

function toDbRow(input: BannerInput): Record<string, unknown> {
  return {
    placement: input.placement,
    label: input.label.trim(),
    image_url_en: input.imageUrlEn || null,
    image_url_ja: input.imageUrlJa || null,
    link_url: input.linkUrl || null,
    start_at: input.startAt ? new Date(input.startAt).toISOString() : null,
    end_at: input.endAt ? new Date(input.endAt).toISOString() : null,
    sort_order: input.sortOrder,
    is_active: input.isActive,
    updated_at: new Date().toISOString(),
  };
}

async function loadOne(id: string): Promise<BannerRow | undefined> {
  const { banners } = await fetchBanners();
  return banners.find((b) => b.id === id);
}

// ---------------------------------------------------------------------------
// Create
// ---------------------------------------------------------------------------

export async function createBanner(
  adminEmail: string,
  input: BannerInput
): Promise<MutationOutcome> {
  const err = validateBannerInput(input);
  if (err) return fail(400, err);

  if (isMockMode()) {
    const now = new Date().toISOString();
    const row: BannerRow = {
      id: randomUUID(),
      placement: input.placement,
      label: input.label.trim(),
      imageUrlEn: input.imageUrlEn || null,
      imageUrlJa: input.imageUrlJa || null,
      linkUrl: input.linkUrl || null,
      startAt: input.startAt ? new Date(input.startAt).toISOString() : null,
      endAt: input.endAt ? new Date(input.endAt).toISOString() : null,
      sortOrder: input.sortOrder,
      isActive: input.isActive,
      createdAt: now,
      updatedAt: now,
    };
    mockDb().banners.unshift(row);
    await writeAudit(adminEmail, "banner_create", null, "game_banners", null, snapshot(row));
    return ok(`Created "${row.label}".`);
  }

  const admin = getSupabaseAdmin();
  const res = await admin.from("game_banners").insert(toDbRow(input)).select("id").single();
  if (res.error) return fail(500, `Insert failed: ${res.error.message}`);

  const newId = String((res.data as { id: string }).id);
  const created = await loadOne(newId);
  await writeAudit(
    adminEmail,
    "banner_create",
    null,
    "game_banners",
    null,
    created ? snapshot(created) : { id: newId, label: input.label }
  );
  return ok(`Created "${input.label.trim()}".`);
}

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------

export async function updateBanner(
  adminEmail: string,
  id: string,
  input: BannerInput
): Promise<MutationOutcome> {
  const err = validateBannerInput(input);
  if (err) return fail(400, err);

  const existing = await loadOne(id);
  if (!existing) return fail(404, "Banner not found.");

  const guard = liveOffGuard(existing, input.isActive, input.confirmLabel);
  if (guard) return fail(409, guard);

  const before = snapshot(existing);

  if (isMockMode()) {
    const row = mockDb().banners.find((b) => b.id === id);
    if (!row) return fail(404, "Banner not found.");
    Object.assign(row, {
      placement: input.placement,
      label: input.label.trim(),
      imageUrlEn: input.imageUrlEn || null,
      imageUrlJa: input.imageUrlJa || null,
      linkUrl: input.linkUrl || null,
      startAt: input.startAt ? new Date(input.startAt).toISOString() : null,
      endAt: input.endAt ? new Date(input.endAt).toISOString() : null,
      sortOrder: input.sortOrder,
      isActive: input.isActive,
      updatedAt: new Date().toISOString(),
    });
    await writeAudit(adminEmail, "banner_update", null, "game_banners", before, snapshot(row));
    return ok(`Saved "${row.label}".`);
  }

  const upd = await getSupabaseAdmin().from("game_banners").update(toDbRow(input)).eq("id", id);
  if (upd.error) return fail(500, `Update failed: ${upd.error.message}`);

  const after = await loadOne(id);
  await writeAudit(
    adminEmail,
    "banner_update",
    null,
    "game_banners",
    before,
    after ? snapshot(after) : null
  );
  return ok(`Saved "${input.label.trim()}".`);
}

// ---------------------------------------------------------------------------
// Activate / deactivate — the one-click switch on the list row
// ---------------------------------------------------------------------------

export async function setBannerActive(
  adminEmail: string,
  id: string,
  active: boolean,
  confirmLabel?: string
): Promise<MutationOutcome> {
  const existing = await loadOne(id);
  if (!existing) return fail(404, "Banner not found.");

  const guard = liveOffGuard(existing, active, confirmLabel);
  if (guard) return fail(409, guard);

  // Activating a banner with no art publishes a slot that shows nothing new —
  // the same rule validateBannerInput applies on save, enforced on this path too.
  if (active && !existing.imageUrlEn && !existing.imageUrlJa) {
    return fail(400, `"${existing.label}" has no artwork — upload EN or JA art before activating.`);
  }

  const before = snapshot(existing);
  const action = active ? "banner_activate" : "banner_deactivate";

  if (isMockMode()) {
    const row = mockDb().banners.find((b) => b.id === id);
    if (!row) return fail(404, "Banner not found.");
    row.isActive = active;
    row.updatedAt = new Date().toISOString();
    await writeAudit(adminEmail, action, null, "game_banners", before, snapshot(row));
  } else {
    const upd = await getSupabaseAdmin()
      .from("game_banners")
      .update({ is_active: active, updated_at: new Date().toISOString() })
      .eq("id", id);
    if (upd.error) return fail(500, `Update failed: ${upd.error.message}`);

    const after = await loadOne(id);
    await writeAudit(
      adminEmail,
      action,
      null,
      "game_banners",
      before,
      after ? snapshot(after) : null
    );
  }

  return ok(
    active
      ? `"${existing.label}" is on — players see it on their next launch.`
      : `"${existing.label}" is off — the slot is hidden on the next fetch.`
  );
}

// ---------------------------------------------------------------------------
// Delete
// ---------------------------------------------------------------------------

export async function deleteBanner(
  adminEmail: string,
  id: string,
  confirmLabel: string
): Promise<MutationOutcome> {
  const existing = await loadOne(id);
  if (!existing) return fail(404, "Banner not found.");
  if (confirmLabel.trim() !== existing.label) {
    return fail(400, "Confirmation label does not match.");
  }

  const before = snapshot(existing);

  if (isMockMode()) {
    const db = mockDb();
    db.banners = db.banners.filter((b) => b.id !== id);
    await writeAudit(adminEmail, "banner_delete", null, "game_banners", before, null);
    return ok(`Deleted "${existing.label}".`);
  }

  const { error } = await getSupabaseAdmin().from("game_banners").delete().eq("id", id);
  if (error) return fail(500, `Delete failed: ${error.message}`);

  await writeAudit(adminEmail, "banner_delete", null, "game_banners", before, null);
  return ok(
    `Deleted "${existing.label}". The uploaded artwork stays in Storage — it is content-hashed and harmless.`
  );
}

// ---------------------------------------------------------------------------
// Artwork upload
// ---------------------------------------------------------------------------

export interface BannerArtUploadResult extends MutationOutcome {
  url?: string;
}

/**
 * Validates then uploads to the public `game-banners` bucket.
 *
 * Immutable naming — `{placement}-{locale}-{content-hash}.{ext}` — so the URL
 * IS the cache key: replacing an image produces a NEW URL, which is what lets
 * the client key its disk cache on the URL with no invalidation story at all.
 * Re-uploading the same bytes therefore yields the same URL, and `upsert: true`
 * makes that a no-op rewrite rather than a conflict.
 */
export async function uploadBannerArt(
  adminEmail: string,
  placement: BannerPlacement,
  locale: BannerLocale,
  file: File
): Promise<BannerArtUploadResult> {
  if (!isBannerPlacement(placement)) return fail(400, `Unknown placement "${placement}".`);
  if (locale !== "en" && locale !== "ja") return fail(400, `Unknown locale "${locale}".`);

  if (!(BANNER_ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
    return fail(400, `Unsupported type "${file.type || "unknown"}". Use JPG, PNG or WebP.`);
  }
  if (file.size > BANNER_ART_SPEC.maxBytes) {
    return fail(
      400,
      `Image is ${(file.size / 1024).toFixed(0)} KB — the cap is ${BANNER_ART_SPEC.maxBytes / 1024} KB. Every mobile player downloads this.`
    );
  }
  if (file.size === 0) return fail(400, "File is empty.");

  const bytes = Buffer.from(await file.arrayBuffer());
  const hash = createHash("sha256").update(bytes).digest("hex").slice(0, 12);
  const ext = file.type === "image/png" ? "png" : file.type === "image/webp" ? "webp" : "jpg";
  const path = `${placement}-${locale}-${hash}.${ext}`;

  if (isMockMode()) {
    // No Storage in mock mode — hand back a stable fake so the editor flow works.
    const url = `https://mock.supabase.local/storage/v1/object/public/${BANNER_BUCKET}/${path}`;
    await writeAudit(adminEmail, "banner_art_upload", null, "storage", null, {
      path,
      bytes: file.size,
      mock: true,
    });
    return { ...ok(`Uploaded ${path} (mock).`), url };
  }

  const admin = getSupabaseAdmin();

  // Create the bucket on first use so this needs no manual Supabase step.
  const buckets = await admin.storage.listBuckets();
  if (!buckets.error && !buckets.data.some((b) => b.name === BANNER_BUCKET)) {
    const created = await admin.storage.createBucket(BANNER_BUCKET, {
      public: true,
      fileSizeLimit: BANNER_ART_SPEC.maxBytes,
      allowedMimeTypes: [...BANNER_ART_SPEC.mimeTypes],
    });
    if (created.error) return fail(500, `Could not create bucket: ${created.error.message}`);
  }

  const up = await admin.storage.from(BANNER_BUCKET).upload(path, bytes, {
    contentType: file.type,
    cacheControl: "31536000",
    upsert: true, // same bytes → same path, so this is a no-op re-write
  });
  if (up.error) return fail(500, `Upload failed: ${up.error.message}`);

  const { data } = admin.storage.from(BANNER_BUCKET).getPublicUrl(path);
  const url = data.publicUrl;

  // The client refuses anything off-host or outside the bucket, so a surprise
  // here would be a banner that saves cleanly and never appears on a device.
  const hostErr = validateBannerArtUrl(url);
  if (hostErr) return fail(500, `Storage returned an unexpected URL: ${hostErr}`);

  await writeAudit(adminEmail, "banner_art_upload", null, "storage", null, {
    path,
    bytes: file.size,
    placement,
    locale,
    url,
  });
  return { ...ok(`Uploaded ${path}.`), url };
}
