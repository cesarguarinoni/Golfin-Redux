import "server-only";
import type { NoticeRow } from "./types";

/**
 * Mock-mode notice fixtures. Three rows so the panel's ordering, the JA
 * fallback (row 2 has no Japanese) and the scheduled state are all visible
 * with `MOCK_MODE=1` and no secrets.
 */
export const MOCK_NOTICES: NoticeRow[] = [
  {
    id: "n1000000-0000-4000-8000-000000000001",
    label: "August maintenance window",
    titleEn: "MAINTENANCE NOTICE",
    titleJa: "メンテナンス情報",
    bodyEn:
      "Scheduled server maintenance: 2026/08/28\nThe game will not be available for a short time\nduring maintenance.",
    bodyJa:
      "定期サーバーメンテナンス: 2026/08/28\nメンテナンス中はゲームをご利用いただけません。",
    startAt: null,
    endAt: "2026-08-29T00:00:00.000Z",
    sortOrder: 20,
    isActive: true,
    createdAt: "2026-08-18T09:00:00.000Z",
    updatedAt: "2026-08-18T09:00:00.000Z",
  },
  {
    id: "n1000000-0000-4000-8000-000000000002",
    label: "Season 3 teaser",
    titleEn: "SEASON 3 IS COMING",
    titleJa: null,
    bodyEn: "New courses, new rivals.\nStarts September 1.",
    bodyJa: null,
    startAt: null,
    endAt: null,
    sortOrder: 10,
    isActive: true,
    createdAt: "2026-08-16T09:00:00.000Z",
    updatedAt: "2026-08-16T09:00:00.000Z",
  },
  {
    id: "n1000000-0000-4000-8000-000000000003",
    label: "September draft (not written yet)",
    titleEn: "",
    titleJa: null,
    bodyEn: "",
    bodyJa: null,
    startAt: "2026-09-01T00:00:00.000Z",
    endAt: null,
    sortOrder: 0,
    isActive: false,
    createdAt: "2026-08-18T10:00:00.000Z",
    updatedAt: "2026-08-18T10:00:00.000Z",
  },
];
