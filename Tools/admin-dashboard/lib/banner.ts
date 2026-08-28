/**
 * Banner rules shared by the server routes and the client panel.
 * Client-safe — do not import server-only modules here.
 *
 * SPEC: Docs/Specs/Active/game_banners/SPEC.md §3.3–§3.5, §5.
 */

import type { BannerInput, BannerPlacement, BannerRow, BannerState } from "./types";

/** Public-read Storage bucket holding banner artwork (SPEC §3.3). */
export const BANNER_BUCKET = "game-banners";

/**
 * The four slots that exist. All are hard-coded in the build — a placement
 * cannot be added from here, and the DB CHECK constraint agrees with this list.
 * List order is DISPLAY order in the editor dropdown and the grouped list.
 *
 * `tournament_modal` is different in kind from the other three: it is never
 * auto-served by `GET /api/v1/banners`. It reaches a player only by being
 * ASSIGNED to a tournament in the Tournaments panel, and shows inside that
 * tournament's sign-up modal. See PLACEMENT_IS_ASSIGNED.
 */
export const BANNER_PLACEMENTS = ["home_promo", "rankings", "store", "tournament_modal"] as const;

/**
 * Placements chosen per-tournament rather than auto-served.
 *
 * For these, `start_at` / `end_at` / `sort_order` do NOT apply — the
 * tournament's own window governs when the banner is on screen, and there is no
 * "pick the top one" among assigned banners. The editor hides those three
 * fields rather than showing controls that do nothing.
 *
 * `is_active` still applies, and is still the kill switch: switching one off
 * drops it from EVERY tournament using it, at once.
 */
export const PLACEMENT_IS_ASSIGNED: Record<BannerPlacement, boolean> = {
  home_promo: false,
  rankings: false,
  store: false,
  tournament_modal: true,
};

export function isAssignedPlacement(placement: BannerPlacement): boolean {
  return PLACEMENT_IS_ASSIGNED[placement] === true;
}

export function isBannerPlacement(value: unknown): value is BannerPlacement {
  return (BANNER_PLACEMENTS as readonly string[]).includes(value as string);
}

/**
 * Upload spec. `maxBytes` is the same 500 KB ceiling as the tournament card
 * ART_SPEC, for the same reason: every mobile player downloads this once.
 *
 * The per-placement pixel targets are the REAL dimensions of the sprites that
 * ship in the build, measured 2026-08-17 with `sips -g pixelWidth`. They are
 * guidance shown in the editor, not a gate — drift warns amber, never blocks.
 */
export const BANNER_ART_SPEC = {
  mimeTypes: ["image/jpeg", "image/png", "image/webp"] as const,
  maxBytes: 500 * 1024,
  /** Warn (not block) outside ±12% of the slot's aspect — same as ART_SPEC. */
  aspectTolerance: 0.12,
  placements: {
    home_promo: {
      screen: "Home",
      where: "Canvas/ScreensRoot/HomeScreen/PromoBanner",
      sprite: "Assets/Art/HomeScreen/GPS Banner.png",
      // The SLOT, not the old bundled sprite (which was 1010×292). Resized 2026-08-17 to sit
      // 24px clear of the Tee nav button and 24px clear of the mode cards; those two gaps are
      // what fix the height. The Image is Simple with preserveAspect off, so art authored to a
      // different ratio is stretched — which is exactly what this warning is for.
      width: 970,
      height: 214,
      aspect: 970 / 214,
    },
    rankings: {
      screen: "Rankings",
      where: "RankingsScreen/ContentArea/Banner",
      sprite: "Assets/Art/RankingsScreen/Banner.png",
      width: 970,
      height: 252,
      aspect: 970 / 252,
    },
    store: {
      screen: "Store",
      where:
        "GeneralShopScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/" +
        "ScrollArea/Viewport/GridContent/WinterSaleBanner",
      // The bundled Winter Sale art is an AUTHORING placeholder, never a
      // runtime fallback: with no live store row the slot is hidden and the
      // card list closes up. Measured 2026-08-28 off the prefab's RectTransform
      // (978x252), which is also the sprite's own size. The Image is Simple
      // with preserveAspect off, so art authored to another ratio is stretched
      // — which is what the drift warning is for.
      sprite: "Assets/Art/Shop/Banner - Winter Sale.png",
      width: 978,
      height: 252,
      aspect: 978 / 252,
    },
    tournament_modal: {
      screen: "Tournament sign-up modal",
      where: "TournamentSignupModal/Panel/Content/BannerRoot",
      // No bundled sprite: unlike the other two slots the strip does not ship
      // with fallback art. With no banner assigned the modal renders its
      // no-banner state (1167 tall instead of 1411), which is a complete design.
      sprite: "—  (no bundled fallback; the strip is simply absent)",
      width: 970,
      height: 252,
      aspect: 970 / 252,
    },
  },
} as const;

export function bannerSpec(placement: BannerPlacement) {
  return BANNER_ART_SPEC.placements[placement];
}

export const PLACEMENT_LABEL: Record<BannerPlacement, string> = {
  home_promo: "Home — promo strip",
  rankings: "Rankings — banner",
  store: "Store — banner",
  tournament_modal: "Tournament — sign-up modal strip",
};

// ---------------------------------------------------------------------------
// Derived state
// ---------------------------------------------------------------------------

/**
 * The same rule the endpoint applies (`backend/routers/banners.py::_is_live`):
 * active AND started AND not ended. LIVE is the only state a player can see;
 * every other state means the slot shows its bundled sprite.
 *
 * A bound that is PRESENT but unparseable returns "OFF" rather than being
 * ignored — that mirrors the endpoint failing closed, so the panel never claims
 * LIVE for a row the server will refuse to serve.
 */
export function deriveBannerState(
  b: Pick<BannerRow, "isActive" | "startAt" | "endAt">,
  nowMs: number
): BannerState {
  if (!b.isActive) return "OFF";

  if (b.startAt !== null) {
    const start = Date.parse(b.startAt);
    if (Number.isNaN(start)) return "OFF";
    if (nowMs < start) return "SCHEDULED";
  }
  if (b.endAt !== null) {
    const end = Date.parse(b.endAt);
    if (Number.isNaN(end)) return "OFF";
    // end_at is EXCLUSIVE, matching the endpoint.
    if (nowMs >= end) return "EXPIRED";
  }
  return "LIVE";
}

// ---------------------------------------------------------------------------
// URL validation
// ---------------------------------------------------------------------------

/**
 * 🔒 Canonical URL validator for Supabase Storage art buckets.
 *
 * Both art callers use this single implementation — do not fork it into a
 * second copy. Mirrors `TournamentArtPolicy.IsAllowedUnder` on the Unity
 * client, so every URL this accepts is one the client will serve.
 *
 * Callers:
 *   - `validateBannerArtUrl`  → bucket `game-banners`
 *   - `uploadCatalogArt`      → bucket `catalog-art`
 *
 * `image_url_*` / catalog art URL columns are free-text stored in the DB and
 * fetched unattended at boot: an arbitrary URL is a content channel into every
 * player's device. Parse first, compare NORMALIZED parts — never a raw
 * `startsWith`. A prefix check passes
 * `…/public/catalog-art/../../../rest/v1/rpc/x`.
 *
 * @returns an error message, or null when the URL is acceptable.
 */
export function validateArtUrlUnderBucket(url: string, bucket: string): string | null {
  const base = process.env.SUPABASE_URL ?? process.env.NEXT_PUBLIC_SUPABASE_URL;

  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return "Artwork URL is not a valid URL.";
  }

  if (parsed.protocol !== "https:") return "Artwork URL must be https.";
  if (parsed.username || parsed.password) return "Artwork URL must not carry userinfo.";
  if (parsed.port) return "Artwork URL must use the default https port.";

  // Mock mode has no project host to compare against; the bucket-path rules
  // below still apply, so a wrong bucket is caught either way.
  if (base) {
    let allowedHost: string;
    try {
      allowedHost = new URL(base).host;
    } catch {
      return "SUPABASE_URL is not a valid URL.";
    }
    if (parsed.host !== allowedHost) {
      return `Artwork must live in this project's Supabase Storage (${allowedHost}), not ${parsed.host}.`;
    }
  }

  const bucketRoot = `/storage/v1/object/public/${bucket}/`;
  const path = parsed.pathname; // dot segments already collapsed by URL
  if (!path.startsWith(bucketRoot)) {
    return `Artwork must be inside the "${bucket}" bucket.`;
  }
  if (path.length <= bucketRoot.length) {
    return "Artwork URL names the bucket root, not an object.";
  }
  // Belt and braces: refuse an escaped dot segment that survived normalization.
  if (path.includes("..") || /%2e/i.test(path)) {
    return "Artwork URL contains a path traversal.";
  }
  return null;
}

/**
 * 🔒 Validates that banner artwork lives in THIS project's Supabase Storage,
 * inside the `game-banners` bucket.
 *
 * Thin wrapper over `validateArtUrlUnderBucket` — all checks live there.
 * Behaviour is identical to the pre-refactor implementation; the mock-mode
 * branch (no SUPABASE_URL → host check skipped) is preserved unchanged.
 *
 * @returns an error message, or null when the URL is acceptable.
 */
export function validateBannerArtUrl(url: string): string | null {
  return validateArtUrlUnderBucket(url, BANNER_BUCKET);
}

/**
 * 🔒 Host allowlist for the tap-through URL, kept in step with
 * `BannerPolicy.AllowedLinkHosts` in the Unity client.
 *
 * Exact, ordinal host matches — NO suffix matching and no wildcard. A
 * `*.golfin.io` rule is precisely what would let `evil-golfin.io` and
 * `golfin.io.attacker.net` through.
 *
 * ⚠️ An admin cannot add a host from the dashboard, BY DESIGN: the client's
 * copy of this list ships in the build, so a new campaign host needs a store
 * release. Adding one here alone produces a link the operator can save and the
 * device silently refuses.
 */
export const ALLOWED_LINK_HOSTS = [
  "golfin.io",
  "www.golfin.io",
  "golfin.world",
  "www.golfin.world",
] as const;

/** @returns an error message, or null when the link URL is acceptable. */
export function validateBannerLinkUrl(url: string): string | null {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return "Link URL is not a valid URL.";
  }
  if (parsed.protocol !== "https:") return "Link URL must be https.";
  if (parsed.username || parsed.password) return "Link URL must not carry userinfo.";
  if (parsed.port) return "Link URL must use the default https port.";
  if (!(ALLOWED_LINK_HOSTS as readonly string[]).includes(parsed.hostname)) {
    return `"${parsed.hostname}" is not an allowlisted link host. The client refuses anything outside ${ALLOWED_LINK_HOSTS.join(", ")} — adding a host needs a client release.`;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Input validation — one gate both create and update go through (SPEC §3.4)
// ---------------------------------------------------------------------------

export function validateBannerInput(input: BannerInput): string | null {
  const label = (input.label ?? "").trim();
  if (label.length < 1 || label.length > 80) {
    return "Label is required (1–80 characters). It is admin-only — players never see it.";
  }

  if (!isBannerPlacement(input.placement)) {
    return `Unknown placement "${input.placement}". The build has exactly three: ${BANNER_PLACEMENTS.join(", ")}.`;
  }

  // A DRAFT may have no art. A LIVE banner with no art is a slot that silently
  // does nothing — the client resolves nothing and leaves the bundled sprite.
  if (input.isActive && !input.imageUrlEn && !input.imageUrlJa) {
    return "An active banner needs at least one image (EN or JA) — otherwise it publishes a slot that shows nothing new.";
  }

  for (const [what, url] of [
    ["EN image", input.imageUrlEn],
    ["JA image", input.imageUrlJa],
  ] as const) {
    if (!url) continue;
    const err = validateBannerArtUrl(url);
    if (err) return `${what}: ${err}`;
  }

  // Null is valid: a banner with no link is informational, and the client
  // leaves the button non-interactable.
  if (input.linkUrl) {
    const err = validateBannerLinkUrl(input.linkUrl);
    if (err) return err;
  }

  if (input.startAt) {
    if (Number.isNaN(Date.parse(input.startAt))) return "Start time is not a valid date.";
  }
  if (input.endAt) {
    if (Number.isNaN(Date.parse(input.endAt))) return "End time is not a valid date.";
  }
  if (input.startAt && input.endAt && Date.parse(input.endAt) <= Date.parse(input.startAt)) {
    return "End time must be after start time.";
  }

  if (!Number.isInteger(input.sortOrder) || input.sortOrder < -999 || input.sortOrder > 999) {
    return "Sort order must be a whole number between −999 and 999.";
  }

  return null;
}
