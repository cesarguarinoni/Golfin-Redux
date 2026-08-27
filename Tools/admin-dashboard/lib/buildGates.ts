/**
 * Client build numbers the admin has to know about.
 *
 * PURE and CLIENT-SAFE — no `server-only`, no I/O. Both the validator
 * (`lib/contentValidate.ts`, which must stay testable without a database) and
 * the Shop panel's banner read from here, so the number lives in exactly one
 * place. A constant that is duplicated is a constant that is half-updated.
 */

/**
 * The first uploaded build whose client parses shop categories STRICTLY and
 * prices a purchase on the server (`shop_server_purchase`, the client half).
 *
 * Older builds map ANY non-`ball` category onto `club`
 * (`GeneralShopCatalog.ParseCategory` before the strict fix), so a `character`
 * or `item` row that reaches one of them renders as a club card that can only
 * fail. The server-side `min_build` filter is the only thing that keeps such a
 * row away from those builds, and `min_build` is IMMUTABLE once published — so
 * it has to be right the first time it is published, not fixed afterwards.
 * That is what rule G1 in `contentValidate.ts` enforces.
 *
 * ⚠️ HOW TO SET IT — read it, never infer it.
 *
 *   0  = the build carrying the client half has NOT been uploaded yet. G1 turns
 *        into a hard error on every non-club/ball row, and the Shop banner says
 *        so. This is the correct value until the archive exists.
 *
 *   N  = the number in `Docs/Versioning/last_uploaded_build.txt` AFTER the
 *        archive that carries the client half. That file is written by
 *        `Tools/mark-uploaded.sh` from `git rev-list --count HEAD` at archive
 *        time, so the build number is the COMMIT COUNT — "last upload + 1" is
 *        wrong by construction. (It was: the panel shipped 2334 as a guess
 *        while HEAD was already past 2338.)
 *
 * Setting it is a one-line commit plus a dashboard redeploy — remember the
 * dashboard is its own deploy surface (`npm --prefix Tools/admin-dashboard run
 * deploy`); the API deploy does not ship it.
 *
 * ── SET 2026-08-27 ──────────────────────────────────────────────────────────
 * Build **2350** (1.5.7) was archived and uploaded to TestFlight that day and is
 * the first build carrying the `shop_server_purchase` client half. The number was
 * READ from `Docs/Versioning/last_uploaded_build.txt` after the archive, which is
 * what `Tools/mark-uploaded.sh` writes from `git rev-list --count HEAD`.
 *
 * Note how far off a guess would have been: the panel previously hard-coded 2334
 * as "last upload (2333) + 1". The real number is 2350 — sixteen commits of
 * daylight. That is the whole reason this is read and not inferred.
 */
/**
 * ⚠️ THE `: number` ANNOTATION IS LOAD-BEARING. Do not remove it.
 *
 * Without it TypeScript infers the LITERAL type (`2350`), and every
 * `SHOP_CATEGORY_STRICT_BUILD === 0` check — the pending-state test below and
 * validator rule G1 — becomes a comparison between two non-overlapping literal
 * types, i.e. a compile error (TS2367). This is a constant whose whole purpose
 * is to be edited, and it flipped 0 -> 2350 on 2026-08-27; the annotation is
 * what lets both states compile.
 */
export const SHOP_CATEGORY_STRICT_BUILD: number = 2350;

/** True while the build carrying the strict client half is still unpublished. */
export const shopCategoryBuildPending = (): boolean => SHOP_CATEGORY_STRICT_BUILD === 0;
