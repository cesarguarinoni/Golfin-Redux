"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { RARITIES } from "@/lib/contentValidate";
import { GACHA_BANNER_ART, gachaBannerState } from "@/lib/contentView";
import type { ContentStoredRow } from "@/lib/types";
import { GachaStateBadge } from "../_content/badges";
import { CatalogPanel } from "../_content/catalog-panel";
import { fetchRows } from "../_content/client";

/**
 * Gacha Banners — the `gacha_banners` catalog (gacha_admin_catalogs §5.2).
 *
 * WHAT AN OPERATOR IS ACTUALLY DOING HERE. A banner row is four decisions:
 * WHEN it runs (startUtc / endUtc, on the server clock), WHAT it rolls (poolId),
 * WHAT IT COSTS (ticketType + costX1 / costX10), and WHAT IT SAYS (nameEn /
 * nameJa / taglineEn / taglineJa, plus one artwork). The extras below are those
 * four groups, in that order, and the raw field list underneath keeps only what
 * they do not render.
 *
 * THE TEXT IS NOT IN THE ARTWORK. Decision 7 (plan §9): every word a player
 * reads on a banner is UI-authored from the row and drawn over the image by the
 * card, exactly like the countdown already is. That is what makes a title
 * change a publish rather than a re-export of a PNG, and it is why there is ONE
 * `artUrl` and not one per locale. The hint says so at the point of upload,
 * because it is the one thing about this panel a designer can get wrong in a way
 * no validator can catch.
 */

/** Blank + the six tiers, for the two rarity selects. */
const RARITY_OPTIONS = ["", ...RARITIES] as const;

export function GachaBannersPanel({ now }: { now: number }) {
  const translate = useT();

  // Distinct poolIds and the ticket types, for the two pickers. Read once on
  // mount from the DRAFT rows of the other catalogs — the same rows the
  // validator will resolve against at publish, so what the picker offers and
  // what publish accepts cannot disagree.
  const [poolIds, setPoolIds] = useState<string[]>([]);
  const [tickets, setTickets] = useState<ContentStoredRow[]>([]);

  const loadReferences = useCallback(async () => {
    try {
      const [pools, ticketRows] = await Promise.all([
        fetchRows("gacha_pools", { limit: 200 }),
        fetchRows("ticket_types", { limit: 200 }),
      ]);
      const ids = new Set<string>();
      for (const row of pools.rows) {
        const poolId = (row.data.poolId ?? "").trim();
        if (poolId) ids.add(poolId);
      }
      setPoolIds(Array.from(ids).sort());
      setTickets(ticketRows.rows);
    } catch {
      // A reference read that fails leaves the pickers empty; the fields are
      // still editable as text below, and publish is the gate either way.
      setPoolIds([]);
      setTickets([]);
    }
  }, []);

  useEffect(() => {
    void loadReferences();
  }, [loadReferences]);

  function renderCell(row: ContentStoredRow, column: string) {
    if (column === "state") {
      const state = gachaBannerState(row, now);
      return <GachaStateBadge state={state} title={translate("gb.state.hint")} />;
    }
    if (column === "costX1" || column === "costX10") {
      return (
        <span className="flex items-center gap-1 tabular-nums text-zinc-200">
          {row.data[column] || "—"}
          <span className="text-[10px] text-zinc-600">
            {ticketLabel(tickets, row.data.ticketType ?? "")}
          </span>
        </span>
      );
    }
    if (column === "pityThreshold") {
      const threshold = Number((row.data.pityThreshold ?? "").trim());
      // Blank and 0 are the SAME state — no pity (decision 2). Rendering "0"
      // as a number would read as "a pity of zero", which is not a thing.
      if (!Number.isFinite(threshold) || threshold <= 0) {
        return <span className="text-zinc-600">{translate("common.none")}</span>;
      }
      return (
        <span className="tabular-nums text-zinc-200">
          {threshold} → {row.data.pityMinRarity || "?"}
        </span>
      );
    }
    if (column === "active") {
      const on = (row.data.active ?? "").trim().toLowerCase() === "true";
      return <span className={on ? "text-accent-300" : "text-zinc-600"}>{on ? "true" : "false"}</span>;
    }
    return undefined;
  }

  return (
    <CatalogPanel
      catalog="gacha_banners"
      titleKey="gb.title"
      renderCell={renderCell}
      editorHiddenColumns={[
        "poolId",
        "ticketType",
        "pityThreshold",
        "pityMinRarity",
        "guaranteeMinRarityX10",
        "nameEn",
        "nameJa",
        "taglineEn",
        "taglineJa",
        "featuredRefIds",
      ]}
      banner={
        <div className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-4 py-3">
          <p className="text-xs font-bold text-amber-300">{translate("gb.notice.headline")}</p>
          <p className="mt-1 text-[11px] leading-relaxed text-amber-200/85">
            {translate("gb.notice.body")}
          </p>
        </div>
      }
      editorExtras={(row, draft, set) => (
        <div className="space-y-4">
          {/* ---- what it rolls, what it costs ---------------------------- */}
          <div className="space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
            <label className="block">
              <span className="font-mono text-[11px] text-zinc-500">poolId</span>
              <select
                value={draft.poolId ?? ""}
                onChange={(e) => set("poolId", e.target.value)}
                className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
              >
                <option value="">—</option>
                {/* A poolId already on the row that is no longer in the pools
                    catalog still has to be selectable, or opening the drawer
                    would silently clear it on save. */}
                {unionWith(poolIds, draft.poolId).map((poolId) => (
                  <option key={poolId} value={poolId}>
                    {poolId}
                  </option>
                ))}
              </select>
              <span className="mt-1 block text-[10px] leading-relaxed text-zinc-600">
                {translate("gb.pool.hint")}
              </span>
            </label>

            <label className="block">
              <span className="font-mono text-[11px] text-zinc-500">ticketType</span>
              <select
                value={draft.ticketType ?? ""}
                onChange={(e) => set("ticketType", e.target.value)}
                className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
              >
                <option value="">—</option>
                {unionWith(
                  tickets.map((t) => t.rowId),
                  draft.ticketType
                ).map((id) => (
                  <option key={id} value={id}>
                    {ticketOptionLabel(tickets, id)}
                  </option>
                ))}
              </select>
              <span className="mt-1 block text-[10px] leading-relaxed text-zinc-600">
                {translate("gb.ticket.hint")}
              </span>
            </label>
          </div>

          {/* ---- pity and the x10 guarantee ------------------------------ */}
          <div className="space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
            <p className="text-[10px] font-medium text-zinc-500">{translate("gb.pity")}</p>
            <div className="grid grid-cols-2 gap-2">
              <label className="block">
                <span className="font-mono text-[10px] text-zinc-500">pityThreshold</span>
                <input
                  value={draft.pityThreshold ?? ""}
                  onChange={(e) => set("pityThreshold", e.target.value.replace(/[^0-9]/g, ""))}
                  placeholder="0"
                  className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 font-mono text-[11px] text-zinc-200 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
                />
              </label>
              <RaritySelect
                column="pityMinRarity"
                value={draft.pityMinRarity ?? ""}
                onChange={(v) => set("pityMinRarity", v)}
              />
            </div>
            <RaritySelect
              column="guaranteeMinRarityX10"
              value={draft.guaranteeMinRarityX10 ?? ""}
              onChange={(v) => set("guaranteeMinRarityX10", v)}
            />
            <p className="text-[10px] leading-relaxed text-zinc-600">{translate("gb.pity.hint")}</p>
          </div>

          {/* ---- the card's text, per locale ----------------------------- */}
          <div className="space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
            <p className="text-[10px] font-medium text-zinc-500">{translate("gb.text")}</p>
            <div className="grid grid-cols-2 gap-2">
              {(["nameEn", "nameJa", "taglineEn", "taglineJa"] as const).map((column) => (
                <label key={column} className="block">
                  <span className="font-mono text-[10px] text-zinc-500">{column}</span>
                  <input
                    value={draft[column] ?? ""}
                    onChange={(e) => set(column, e.target.value)}
                    className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
                  />
                </label>
              ))}
            </div>
            {/* ⚠️ THE ONE THING NO VALIDATOR CAN CATCH. Amber, not grey. */}
            <p className="rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-1.5 text-[10px] leading-relaxed text-amber-200/90">
              {translate("gb.text.hint")}
            </p>
            <label className="block">
              <span className="font-mono text-[10px] text-zinc-500">featuredRefIds</span>
              <input
                value={draft.featuredRefIds ?? ""}
                onChange={(e) => set("featuredRefIds", e.target.value)}
                placeholder="club_pwedge_royal;club_putter_golfinx"
                className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 font-mono text-[11px] text-zinc-200 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
              />
              <span className="mt-1 block text-[10px] leading-relaxed text-zinc-600">
                {translate("gb.featured.hint")}
              </span>
            </label>
          </div>

          {/* ---- artwork ------------------------------------------------- */}
          <ArtPreview url={draft.artUrl ?? ""} rowId={row.rowId} />
        </div>
      )}
    />
  );
}

/** Blank + the six tiers. Blank is a real value: "no pity", "no guarantee". */
function RaritySelect({
  column,
  value,
  onChange,
}: {
  column: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-[10px] text-zinc-500">{column}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
      >
        {RARITY_OPTIONS.map((rarity) => (
          <option key={rarity || "none"} value={rarity}>
            {rarity || "—"}
          </option>
        ))}
      </select>
    </label>
  );
}

/**
 * The uploaded banner, at the card's aspect ratio, with the target dimensions
 * named and a drift note when the file does not match them.
 *
 * The UPLOAD BUTTON is not here: it belongs to the `artUrl` field the RowEditor
 * already renders for every registered art-URL column (contentView
 * ART_URL_COLUMNS), and adding a second one would be two controls writing the
 * same field. This is the preview beside it — the thing the editor could not
 * show, because it does not know a banner is 882 × 1448.
 *
 * Drift is AMBER, never a block: an off-size banner scales, it does not break.
 */
function ArtPreview({ url, rowId }: { url: string; rowId: string }) {
  const translate = useT();
  const [natural, setNatural] = useState<{ w: number; h: number } | null>(null);
  const trimmed = url.trim();

  useEffect(() => {
    setNatural(null);
  }, [trimmed]);

  const drift =
    natural !== null &&
    (natural.w !== GACHA_BANNER_ART.width || natural.h !== GACHA_BANNER_ART.height);

  return (
    <div className="space-y-2 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <p className="text-[10px] font-medium text-zinc-500">{translate("gb.art")}</p>
      {trimmed ? (
        <div className="flex items-start gap-3">
          {/* eslint-disable-next-line @next/next/no-img-element -- a Supabase
              Storage URL on an internal tool; next/image would add a loader
              config for one preview. */}
          <img
            src={trimmed}
            alt={rowId}
            onLoad={(e) =>
              setNatural({
                w: e.currentTarget.naturalWidth,
                h: e.currentTarget.naturalHeight,
              })
            }
            className="w-24 rounded-md border border-surface-700 bg-surface-900 object-cover"
            style={{ aspectRatio: `${GACHA_BANNER_ART.width} / ${GACHA_BANNER_ART.height}` }}
          />
          <div className="min-w-0 flex-1 space-y-1">
            <code className="block break-all text-[10px] text-zinc-500">{trimmed}</code>
            {natural && (
              <p className={`text-[10px] ${drift ? "text-amber-300" : "text-zinc-500"}`}>
                {drift
                  ? translate("gb.art.sizeDrift", {
                      w: natural.w,
                      h: natural.h,
                      tw: GACHA_BANNER_ART.width,
                      th: GACHA_BANNER_ART.height,
                    })
                  : `${natural.w}×${natural.h}`}
              </p>
            )}
          </div>
        </div>
      ) : (
        <p className="text-[11px] text-zinc-600">{translate("common.none")}</p>
      )}
      <p className="text-[10px] leading-relaxed text-zinc-600">
        {translate("gb.art.hint", { w: GACHA_BANNER_ART.width, h: GACHA_BANNER_ART.height })}
      </p>
    </div>
  );
}

/** Options plus the row's current value, so a stale reference stays selectable. */
function unionWith(options: string[], current: string | undefined): string[] {
  const value = (current ?? "").trim();
  if (!value || options.includes(value)) return options;
  return [...options, value].sort();
}

function ticketOptionLabel(tickets: ContentStoredRow[], id: string): string {
  const hit = tickets.find((t) => t.rowId === id);
  return hit ? `${hit.data.nameEn || hit.data.key || "?"} (${id})` : id;
}

/** The short ticket name for the cost cells — "×" plus a name reads as a price. */
function ticketLabel(tickets: ContentStoredRow[], id: string): string {
  const hit = tickets.find((t) => t.rowId === (id ?? "").trim());
  return hit ? String(hit.data.key || hit.data.nameEn || id) : id || "";
}
