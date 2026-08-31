"use client";

import { useT } from "@/components/I18nProvider";
import {
  RARITY_STYLE,
  monogram,
  monogramHue,
  type GachaBannerState,
  type ShopState,
} from "@/lib/contentView";
import type { ContentDiffKind } from "@/lib/types";

/**
 * Badges for the content panels.
 *
 * State badges (LIVE / SCHEDULED / ENDED / OFF) and rarity names are
 * UNTRANSLATED by design — ADMIN_DASHBOARD_OPS.md §3.4 lists them alongside DB
 * column names and slugs. `whitespace-nowrap` everywhere and no
 * `tracking-wider`, because JA sits in the same badge and letter-spacing on
 * kana reads as broken rather than as emphasis.
 */

const SHOP_STATE_STYLE: Record<ShopState, string> = {
  LIVE: "border-accent-500/40 bg-accent-500/10 text-accent-300",
  SCHEDULED: "border-sky-500/40 bg-sky-500/10 text-sky-300",
  ENDED: "border-surface-700 bg-surface-850 text-zinc-500",
  OFF: "border-zinc-600 bg-surface-850 text-zinc-500",
  // Red, and never mistakable for LIVE: the row has a schedule window that
  // could not be parsed, so it fails closed (content_panels_gaps §3).
  BROKEN: "border-red-500/50 bg-red-500/15 text-red-300",
};

export function ShopStateBadge({ state, title }: { state: ShopState; title?: string }) {
  return (
    <span
      title={title}
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-bold ${SHOP_STATE_STYLE[state]}`}
    >
      {state}
    </span>
  );
}

/**
 * The gacha banner state badge (gacha_admin_catalogs §5.2).
 *
 * The same five states and the same palette as the shop's, on purpose: an
 * operator reading LIVE / SCHEDULED / ENDED / OFF / BROKEN in two panels is
 * reading the same fact about a scheduling window, and giving the gacha its own
 * colours would suggest otherwise. `GachaBannerState` is a separate type only
 * because the two are derived from different columns.
 */
export function GachaStateBadge({ state, title }: { state: GachaBannerState; title?: string }) {
  return (
    <span
      title={title}
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-bold ${SHOP_STATE_STYLE[state]}`}
    >
      {state}
    </span>
  );
}

/** Rarity name, untranslated — it is the value stored in the row. */
export function RarityBadge({ rarity }: { rarity: string }) {
  if (!rarity) return <span className="text-zinc-600">—</span>;
  return (
    <span
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-semibold ${
        RARITY_STYLE[rarity] ?? "border-surface-700 bg-surface-850 text-zinc-400"
      }`}
    >
      {rarity}
    </span>
  );
}

const DIFF_STYLE: Record<ContentDiffKind, string> = {
  added: "border-emerald-500/40 bg-emerald-500/10 text-emerald-300",
  changed: "border-amber-500/40 bg-amber-500/10 text-amber-300",
  deactivated: "border-red-500/40 bg-red-500/10 text-red-300",
  reactivated: "border-sky-500/40 bg-sky-500/10 text-sky-300",
};

export function DiffKindBadge({ kind }: { kind: ContentDiffKind }) {
  const translate = useT();
  return (
    <span
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-bold ${DIFF_STYLE[kind]}`}
    >
      {translate(`cp.diff.${kind}`)}
    </span>
  );
}

/** Unpublished-draft count. Zero is shown too — "clean" is information. */
export function DirtyBadge({ count }: { count: number }) {
  const translate = useT();
  if (count <= 0) {
    return (
      <span className="whitespace-nowrap rounded border border-surface-700 bg-surface-850 px-1.5 py-0.5 text-[10px] font-medium text-zinc-500">
        {translate("c.badge.clean")}
      </span>
    );
  }
  return (
    <span
      title={translate("c.badge.dirtyHint")}
      className="whitespace-nowrap rounded border border-amber-500/50 bg-amber-500/15 px-1.5 py-0.5 text-[10px] font-bold text-amber-300"
    >
      {translate("c.badge.dirty", { n: count })}
    </span>
  );
}

export function DisabledBadge() {
  const translate = useT();
  return (
    <span
      title={translate("c.badge.disabledHint")}
      className="whitespace-nowrap rounded border border-red-500/50 bg-red-500/15 px-1.5 py-0.5 text-[10px] font-bold text-red-300"
    >
      {translate("c.badge.disabled")}
    </span>
  );
}

/**
 * `URL-only · not bundled` — content_art_bundling §9.2.
 *
 * The row has art (a URL an installed build renders from) but no bundled sprite
 * name, so no build carries the file yet. That is a legitimate, temporary state,
 * not an error — it ends when someone runs `GOLFIN/Content/Fetch URL Art`, which
 * pulls the art into `Resources/` and fills the name column in. Sky, not amber:
 * it is pipeline state to be aware of, never something failing.
 *
 * BOTH the label and the hover explanation are TRANSLATED, unlike LIVE /
 * SCHEDULED / rarity names. §3.4 leaves those alone because they are the value
 * stored in the row; this one is descriptive prose about pipeline state, so it
 * reads as broken in a JA session if it stays English. Verified rendering as
 * `URL のみ・未同梱` on 2026-08-28.
 */
export function UrlOnlyBadge({ columns }: { columns: string[] }) {
  const translate = useT();
  if (columns.length === 0) return null;
  return (
    <span
      title={translate("c.badge.urlOnlyHint", { columns: columns.join(", ") })}
      className="whitespace-nowrap rounded border border-sky-500/40 bg-sky-500/10 px-1.5 py-0.5 text-[10px] font-semibold text-sky-300"
    >
      {translate("c.badge.urlOnly")}
    </span>
  );
}

/**
 * The "art thumbnail" of §11.3, as far as the data allows.
 *
 * The catalogs store a Unity SPRITE NAME (`portraitSprite: "Driver-G&F"`), not a
 * URL, and there is no bucket holding catalog art — so there is no image to
 * fetch. Rather than render a broken `<img>` or nothing at all, this is a
 * deterministic monogram tile (same entity ⇒ same colour, always) shown next to
 * the exact sprite name the game will pass to `Resources.Load`.
 */
export function ArtTile({ name, seed, size = 40 }: { name: string; seed: string; size?: number }) {
  const hue = monogramHue(seed);
  return (
    <span
      aria-hidden
      style={{
        width: size,
        height: size,
        background: `hsl(${hue} 45% 22%)`,
        color: `hsl(${hue} 70% 72%)`,
        borderColor: `hsl(${hue} 45% 34%)`,
      }}
      className="flex shrink-0 items-center justify-center rounded-md border text-xs font-bold"
    >
      {monogram(name)}
    </span>
  );
}
