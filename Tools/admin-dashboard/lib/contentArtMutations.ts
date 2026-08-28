import "server-only";
import { createHash } from "node:crypto";
import { writeAudit } from "./audit";
import { validateArtUrlUnderBucket } from "./banner";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { MutationOutcome } from "./mutations";

/**
 * Artwork upload for admin-created catalog rows (SPEC content_art_urls §3).
 *
 * Mirrors the uploadBannerArt shape (bannerMutations.ts) exactly:
 *   - Server-only (import "server-only")
 *   - Immutable naming: {catalog}-{rowId}-{column}-{sha256[:12]}.{ext}
 *     so the URL IS the cache key — replacing an image produces a NEW URL,
 *     no invalidation needed on the client.
 *   - Re-uploading the same bytes → same URL (upsert: true → no-op rewrite)
 *   - Size cap 500 KB (SPEC §3 — same limit as banners)
 *   - MIME: JPG, PNG only — NO WebP (SPEC §5.1, Cesar 2026-08-27; see CATALOG_ART_SPEC)
 *   - Writes an audit row ("content_art_upload") — same as "banner_art_upload"
 *   - Creates the bucket on first use (no manual Supabase step needed)
 */

const CATALOG_ART_BUCKET = "catalog-art";

/**
 * Same SIZE spec as banners, but NOT the same MIME list — SPEC §5.1, Cesar 2026-08-27.
 *
 * NO WebP. A banner is only ever fetched at runtime, so WebP is free there. Catalog art has a
 * SECOND life: `content_art_bundling` pulls it into `Resources/` so the next build can bundle
 * it, and Unity does not import WebP natively. Accepting a format the bundling step cannot use
 * would let an operator upload art that works right up until the build meant to absorb it —
 * i.e. it would break much later, somewhere else, for reasons nobody would connect back here.
 */
const CATALOG_ART_SPEC = {
  mimeTypes: ["image/jpeg", "image/png"] as const,
  maxBytes: 500 * 1024,
} as const;

/** Catalogs the client can receive art for. */
const ALLOWED_CATALOGS = [
  "characters",
  "clubs",
  "items",
  "balls",
] as const;
export type AllowedCatalog = (typeof ALLOWED_CATALOGS)[number];

function isAllowedCatalog(s: string): s is AllowedCatalog {
  return (ALLOWED_CATALOGS as readonly string[]).includes(s);
}

/** URL columns the client reads from catalog rows. */
const ALLOWED_COLUMNS = [
  "portraitUrl",
  "fullUrl",
  "thumbnailUrl",
  "controlUrl",
] as const;
export type AllowedColumn = (typeof ALLOWED_COLUMNS)[number];

function isAllowedColumn(s: string): s is AllowedColumn {
  return (ALLOWED_COLUMNS as readonly string[]).includes(s);
}

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

export interface CatalogArtUploadResult extends MutationOutcome {
  url?: string;
}

/**
 * Validates then uploads to the public `catalog-art` bucket.
 *
 * @param adminEmail   Audit attribution.
 * @param catalog      One of the allowed catalog names (characters / clubs / items / balls).
 * @param rowId        The row's id column value (e.g. "char_james").
 * @param column       The URL column being set (e.g. "portraitUrl").
 * @param file         The artwork File from FormData.
 */
export async function uploadCatalogArt(
  adminEmail: string,
  catalog: AllowedCatalog,
  rowId: string,
  column: AllowedColumn,
  file: File
): Promise<CatalogArtUploadResult> {
  // --- parameter validation -----------------------------------------------
  if (!isAllowedCatalog(catalog)) {
    return fail(400, `Unknown catalog "${catalog}". Allowed: ${ALLOWED_CATALOGS.join(", ")}.`);
  }
  if (!rowId || !/^[\w\-]+$/.test(rowId)) {
    return fail(400, `rowId must be a non-empty alphanumeric/hyphen/underscore string.`);
  }
  if (!isAllowedColumn(column)) {
    return fail(400, `Unknown column "${column}". Allowed: ${ALLOWED_COLUMNS.join(", ")}.`);
  }

  // --- file validation -----------------------------------------------------
  if (!(CATALOG_ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
    return fail(
      400,
      `Unsupported type "${file.type || "unknown"}". Use JPG or PNG — NOT WebP: ` +
        `Unity cannot import it, so content_art_bundling could never pull it into a build.`
    );
  }
  if (file.size > CATALOG_ART_SPEC.maxBytes) {
    return fail(
      400,
      `Image is ${(file.size / 1024).toFixed(0)} KB — the cap is ${CATALOG_ART_SPEC.maxBytes / 1024} KB. ` +
        `Every mobile player downloads this art over the network.`
    );
  }
  if (file.size === 0) return fail(400, "File is empty.");

  // --- immutable name ------------------------------------------------------
  const bytes = Buffer.from(await file.arrayBuffer());
  const hash = createHash("sha256").update(bytes).digest("hex").slice(0, 12);
  const ext = file.type === "image/png" ? "png" : "jpg";   // no webp — see CATALOG_ART_SPEC
  // e.g. "characters-char_james-portraitUrl-a1b2c3d4e5f6.jpg"
  const path = `${catalog}-${rowId}-${column}-${hash}.${ext}`;

  // --- mock branch ---------------------------------------------------------
  if (isMockMode()) {
    const url = `https://mock.supabase.local/storage/v1/object/public/${CATALOG_ART_BUCKET}/${path}`;
    await writeAudit(adminEmail, "content_art_upload", null, "storage", null, {
      path,
      bytes: file.size,
      catalog,
      rowId,
      column,
      mock: true,
    });
    return { ...ok(`Uploaded ${path} (mock).`), url };
  }

  // --- real upload ---------------------------------------------------------
  const admin = getSupabaseAdmin();

  // Create the bucket on first use (one-time, idempotent after that).
  const buckets = await admin.storage.listBuckets();
  if (!buckets.error && !buckets.data.some((b) => b.name === CATALOG_ART_BUCKET)) {
    const created = await admin.storage.createBucket(CATALOG_ART_BUCKET, {
      public: true,
      fileSizeLimit: CATALOG_ART_SPEC.maxBytes,
      allowedMimeTypes: [...CATALOG_ART_SPEC.mimeTypes],
    });
    if (created.error) {
      return fail(500, `Could not create bucket: ${created.error.message}`);
    }
  }

  const up = await admin.storage.from(CATALOG_ART_BUCKET).upload(path, bytes, {
    contentType: file.type,
    cacheControl: "31536000",
    upsert: true, // same bytes → same path → harmless rewrite
  });
  if (up.error) return fail(500, `Upload failed: ${up.error.message}`);

  const { data } = admin.storage.from(CATALOG_ART_BUCKET).getPublicUrl(path);
  const url = data.publicUrl;

  // Re-validate the URL Storage hands back against the same rules the client's
  // allowlist enforces (TournamentArtPolicy.IsAllowedUnder). A URL that passes
  // here but fails the client check would save cleanly in the admin and never
  // show on a device. validateArtUrlUnderBucket is the canonical implementation
  // shared with validateBannerArtUrl — not a copy, not a weaker subset.
  const urlErr = validateArtUrlUnderBucket(url, CATALOG_ART_BUCKET);
  if (urlErr) {
    return fail(500, `Storage returned an unexpected URL: ${urlErr}`);
  }

  await writeAudit(adminEmail, "content_art_upload", null, "storage", null, {
    path,
    bytes: file.size,
    catalog,
    rowId,
    column,
    url,
  });
  return { ...ok(`Uploaded ${path}.`), url };
}
