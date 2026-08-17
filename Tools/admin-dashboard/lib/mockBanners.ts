import "server-only";
import { BANNER_BUCKET } from "./banner";
import type { BannerRow } from "./types";

/**
 * Mock-mode banner fixtures — one row per placement, so the whole panel is
 * exercisable with `MOCK_MODE=1` and no secrets.
 *
 * The URLs point at a fake host on purpose. `validateBannerArtUrl` compares
 * against SUPABASE_URL, which is absent in mock mode, so the bucket-path half
 * of the rule is what these exercise; nothing ever fetches them.
 */

const MOCK_STORAGE = `https://mock.supabase.local/storage/v1/object/public/${BANNER_BUCKET}`;

export const MOCK_BANNERS: BannerRow[] = [
  {
    id: "b1000000-0000-4000-8000-000000000001",
    placement: "home_promo",
    label: "August GPS campaign",
    imageUrlEn: `${MOCK_STORAGE}/home_promo-en-a1b2c3d4e5f6.jpg`,
    imageUrlJa: `${MOCK_STORAGE}/home_promo-ja-0f1e2d3c4b5a.jpg`,
    linkUrl: "https://golfin.io/campaign/august",
    startAt: null,
    endAt: null,
    sortOrder: 10,
    isActive: true,
    createdAt: "2026-08-17T09:00:00.000Z",
    updatedAt: "2026-08-17T09:00:00.000Z",
  },
  {
    id: "b1000000-0000-4000-8000-000000000002",
    placement: "rankings",
    label: "Rankings — season 3 teaser",
    imageUrlEn: `${MOCK_STORAGE}/rankings-en-9a8b7c6d5e4f.png`,
    imageUrlJa: null,
    linkUrl: null,
    startAt: null,
    endAt: null,
    sortOrder: 0,
    isActive: true,
    createdAt: "2026-08-16T09:00:00.000Z",
    updatedAt: "2026-08-16T09:00:00.000Z",
  },
  {
    // A draft: no art, switched off. Exercises the OFF badge and the
    // "active needs at least one image" validation when toggled on.
    id: "b1000000-0000-4000-8000-000000000003",
    placement: "home_promo",
    label: "September draft (no art yet)",
    imageUrlEn: null,
    imageUrlJa: null,
    linkUrl: null,
    startAt: "2026-09-01T00:00:00.000Z",
    endAt: "2026-09-30T00:00:00.000Z",
    sortOrder: 20,
    isActive: false,
    createdAt: "2026-08-17T10:00:00.000Z",
    updatedAt: "2026-08-17T10:00:00.000Z",
  },
];
