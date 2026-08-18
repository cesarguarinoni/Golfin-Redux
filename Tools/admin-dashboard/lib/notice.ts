/**
 * Home-notice rules shared by the server routes and the client panel.
 * Client-safe — do not import server-only modules here.
 *
 * SPEC: Docs/Specs/Active/home_notices/SPEC.md §3.
 */

import type { NoticeInput, NoticeRow, NoticeState } from "./types";

/**
 * Length caps. These are not database constraints — they are what the Home
 * panel can actually draw on a narrow phone before TextMeshPro truncates
 * rather than wraps. The editor shows a live counter and refuses past these,
 * because the failure mode is invisible from the dashboard: the text saves
 * fine and is clipped on the device.
 */
export const NOTICE_LIMITS = {
  label: 80,
  title: 48,
  /** ~4 short lines. The bundled maintenance body is 108 characters. */
  body: 240,
  /** Beyond this the endpoint truncates — the panel is three dots wide. */
  maxLive: 5,
} as const;

/**
 * The same rule the endpoint applies (`backend/routers/notices.py::_is_live`):
 * active AND started AND not ended. LIVE is the only state a player can see.
 *
 * A bound that is PRESENT but unparseable returns "OFF" rather than being
 * ignored — that mirrors the endpoint failing closed, so the panel never claims
 * LIVE for a row the server will refuse to serve.
 */
export function deriveNoticeState(
  n: Pick<NoticeRow, "isActive" | "startAt" | "endAt">,
  nowMs: number
): NoticeState {
  if (!n.isActive) return "OFF";

  if (n.startAt !== null) {
    const start = Date.parse(n.startAt);
    if (Number.isNaN(start)) return "OFF";
    if (nowMs < start) return "SCHEDULED";
  }
  if (n.endAt !== null) {
    const end = Date.parse(n.endAt);
    if (Number.isNaN(end)) return "OFF";
    // end_at is EXCLUSIVE, matching the endpoint.
    if (nowMs >= end) return "EXPIRED";
  }
  return "LIVE";
}

/**
 * @returns an error message, or null when the input is acceptable.
 *
 * The asymmetry between EN and JA is deliberate and matches the banners: EN is
 * the base locale and the fallback for everything, so an active notice must
 * have it. Japanese is optional — a JP player then reads the English, which is
 * strictly better than an empty panel.
 */
export function validateNoticeInput(input: NoticeInput): string | null {
  const label = (input.label ?? "").trim();
  if (label.length < 1 || label.length > NOTICE_LIMITS.label) {
    return `Label is required (1–${NOTICE_LIMITS.label} characters). It is admin-only — players never see it.`;
  }

  const fields = [
    ["English title", input.titleEn, NOTICE_LIMITS.title],
    ["Japanese title", input.titleJa, NOTICE_LIMITS.title],
    ["English body", input.bodyEn, NOTICE_LIMITS.body],
    ["Japanese body", input.bodyJa, NOTICE_LIMITS.body],
  ] as const;
  for (const [what, value, cap] of fields) {
    if ((value ?? "").length > cap) {
      return `${what} is ${(value ?? "").length} characters — the panel fits ${cap}.`;
    }
  }

  if (input.isActive) {
    if (!(input.titleEn ?? "").trim() && !(input.bodyEn ?? "").trim()) {
      return "An active notice needs an English title or body — that is what a player sees when no Japanese is written.";
    }
  }

  if (input.startAt && Number.isNaN(Date.parse(input.startAt))) {
    return "Start time is not a valid date.";
  }
  if (input.endAt && Number.isNaN(Date.parse(input.endAt))) {
    return "End time is not a valid date.";
  }
  if (input.startAt && input.endAt && Date.parse(input.endAt) <= Date.parse(input.startAt)) {
    return "End time must be after start time.";
  }

  if (!Number.isInteger(input.sortOrder) || Math.abs(input.sortOrder) > 999) {
    return "Sort order must be a whole number between −999 and 999.";
  }

  return null;
}

/**
 * What a player in each language actually sees, applying the same fallback the
 * client applies. Rendered as the editor's preview so the operator can see that
 * leaving Japanese blank means "show the English", not "show nothing".
 */
export function resolveForLocale(
  n: Pick<NoticeRow, "titleEn" | "titleJa" | "bodyEn" | "bodyJa">,
  japanese: boolean
): { title: string; body: string } {
  const pick = (preferred: string | null, fallback: string) =>
    preferred && preferred.trim().length > 0 ? preferred : fallback;
  return japanese
    ? { title: pick(n.titleJa, n.titleEn), body: pick(n.bodyJa, n.bodyEn) }
    : { title: n.titleEn, body: n.bodyEn };
}
