"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import {
  GACHA_KIND_TO_CATALOG,
  GACHA_RARITY_BEARING_KINDS,
} from "@/lib/contentView";
import {
  effectiveOdds,
  simulate,
  totalOdds,
  RARITY_ORDER,
  rarityRank,
  type BannerRoll,
  type PoolEntry,
  type RateRow,
  type SimulateResult,
} from "@/lib/gachaOdds";
import type { DictKey } from "@/lib/i18n";
import type { ContentStoredRow } from "@/lib/types";
import { RarityBadge } from "../_content/badges";
import { CatalogPanel } from "../_content/catalog-panel";
import { fetchRows } from "../_content/client";
import type { RowIdContext } from "../_content/row-editor";
import { RefPicker } from "../shop/ref-picker";

/**
 * Gacha Pools — `gacha_pools` and `gacha_rates`, one tab each
 * (gacha_admin_catalogs §5.3).
 *
 * TWO CATALOGS, ONE PANEL, for the reason the Mission Components panel gives
 * about its five: they are only ever opened while thinking about the same
 * thing. A rate table is a statement about a pool — "Legendary is 2 %" means
 * nothing until you know what the Legendary prizes are — and the validator
 * treats them as a pair from both directions (rules 2-4, 9). Publishing is
 * still separate, and each tab carries its own version / dirty / kill-switch
 * badges, because each publishes on its own.
 *
 * WHAT THIS PANEL ADDS OVER A PLAIN TABLE is the two things a table cannot
 * show: the EFFECTIVE per-item odds (a number that exists nowhere in the data —
 * `rate(rarity) × weight / Σ weight`) and a SIMULATION of the roll that is
 * about to be published. Both come from `lib/gachaOdds.ts`, which is the same
 * function the server roll is checked against in `gacha_server_pull` — so what
 * this screen shows is not an approximation of what players will get, it is the
 * reference implementation of it.
 *
 * Everything here reads DRAFTS, deliberately: the state a publish is about to
 * make live is the state worth simulating.
 */

const TABS = [
  { catalog: "gacha_pools", labelKey: "gp.tab.pools" },
  { catalog: "gacha_rates", labelKey: "gp.tab.rates" },
] as const;

type TabCatalog = (typeof TABS)[number]["catalog"];

const KINDS = Object.keys(GACHA_KIND_TO_CATALOG);

/** Default simulation size and seed — §5.3 and the acceptance both name 10 000. */
const SIM_PULLS = 10000;
const SIM_SEED = 20260831;

const text = (v: unknown): string => (v === null || v === undefined ? "" : String(v).trim());
const num = (v: unknown): number => {
  const n = Number(text(v));
  return Number.isFinite(n) ? n : 0;
};
const pct = (p: number): string => `${(p * 100).toFixed(2)} %`;

const toEntry = (row: ContentStoredRow): PoolEntry => ({
  id: row.rowId,
  poolId: text(row.data.poolId),
  kind: text(row.data.kind),
  refId: text(row.data.refId),
  rarity: text(row.data.rarity),
  weight: num(row.data.weight),
  quantity: num(row.data.quantity),
  dupeRp: num(row.data.dupeRp),
  featured: text(row.data.featured).toLowerCase() === "true",
});

const toRate = (row: ContentStoredRow): RateRow => ({
  poolId: text(row.data.poolId),
  rarity: text(row.data.rarity),
  rateBp: num(row.data.rateBp),
});

/** Pool + rate + banner drafts, read once and shared by both tabs' extras. */
interface GachaDrafts {
  pools: ContentStoredRow[];
  rates: ContentStoredRow[];
  banners: ContentStoredRow[];
}

export function GachaPoolsPanel() {
  const translate = useT();
  const [tab, setTab] = useState<TabCatalog>("gacha_pools");
  const active = useMemo(() => TABS.find((t) => t.catalog === tab) ?? TABS[0], [tab]);

  const [drafts, setDrafts] = useState<GachaDrafts | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [pools, rates, banners] = await Promise.all([
        fetchRows("gacha_pools", { limit: 200 }),
        fetchRows("gacha_rates", { limit: 200 }),
        fetchRows("gacha_banners", { limit: 200 }),
      ]);
      setDrafts({ pools: pools.rows, rates: rates.rows, banners: banners.rows });
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("gp.title")}</h1>
      </div>
      <p className="mb-4 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-[11px] leading-relaxed text-zinc-400">
        {translate("gp.note")}
      </p>

      <div className="mb-4 flex gap-1 border-b border-surface-800">
        {TABS.map((entry) => (
          <button
            key={entry.catalog}
            type="button"
            onClick={() => setTab(entry.catalog)}
            className={`rounded-t-md px-3 py-2 text-xs font-medium transition ${
              tab === entry.catalog
                ? "border-b-2 border-accent-500 text-zinc-100"
                : "text-zinc-500 hover:text-zinc-300"
            }`}
          >
            {translate(entry.labelKey as DictKey)}
          </button>
        ))}
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-[11px] text-red-300">
          {error}
        </p>
      )}

      {/* The odds / simulation block sits ABOVE the table on the Pools tab and
          the sum indicator above the Rates tab: both are about the pool as a
          WHOLE, and a per-pool fact rendered under a paged table would be a
          fact about whichever rows happened to be on screen. */}
      {tab === "gacha_pools" && drafts && <PoolSummaries drafts={drafts} />}
      {tab === "gacha_rates" && drafts && <RateSums drafts={drafts} />}

      <CatalogPanel
        key={active.catalog}
        catalog={active.catalog}
        titleKey="gp.title"
        hideTitle
        renderCell={active.catalog === "gacha_pools" ? renderPoolCell : undefined}
        editorHiddenColumns={
          active.catalog === "gacha_pools" ? ["kind", "refId", "rarity", "featured"] : undefined
        }
        editorExtras={
          active.catalog === "gacha_pools"
            ? (row, draft, set, rowIdCtx) => (
                <PoolEntryEditor draft={draft} set={set} rowIdCtx={rowIdCtx} />
              )
            : undefined
        }
      />
    </div>
  );
}

function renderPoolCell(row: ContentStoredRow, column: string) {
  if (column === "featured") {
    const on = text(row.data.featured).toLowerCase() === "true";
    return <span className={on ? "text-accent-300" : "text-zinc-600"}>{on ? "FEATURED" : "—"}</span>;
  }
  return undefined;
}

/**
 * `kind` select → `RefPicker` → rarity, which is auto-filled and LOCKED for the
 * kinds whose catalog row carries one (§5.3, validator rule 6).
 *
 * The lock is the point. A club's rarity is a fact about the club; typing a
 * different one here does not make the prize rarer, it moves the entry into a
 * different rarity BUCKET and rolls it at that bucket's odds while the game
 * still shows the club's real rarity. Publish refuses it (rule 6) — this makes
 * it unreachable instead of merely refused. Balls and tickets have no rarity of
 * their own, so there the field is the operator's to choose.
 */
function PoolEntryEditor({
  draft,
  set,
  rowIdCtx,
}: {
  draft: Record<string, string>;
  set: (column: string, value: string) => void;
  rowIdCtx: RowIdContext;
}) {
  const translate = useT();
  const kind = draft.kind ?? "";
  const rarityLocked = GACHA_RARITY_BEARING_KINDS.includes(kind);

  return (
    <div className="space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div>
        <span className="font-mono text-[11px] text-zinc-500">kind</span>
        <select
          value={kind}
          onChange={(e) => {
            // Changing the kind invalidates BOTH the reference and the rarity
            // it was copied from — a club id is not a ball id, and a Legendary
            // club's rarity is not a fact about whatever replaces it.
            set("kind", e.target.value);
            set("refId", "");
            set("rarity", "");
          }}
          className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
        >
          <option value="">—</option>
          {KINDS.map((k) => (
            <option key={k} value={k}>
              {k} → {GACHA_KIND_TO_CATALOG[k]}
            </option>
          ))}
        </select>
        <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{translate("gp.kind.hint")}</p>
      </div>

      <RefPicker
        category={kind}
        refId={draft.refId ?? ""}
        catalogFor={GACHA_KIND_TO_CATALOG}
        onPick={(refId, resolved) => {
          set("refId", refId);
          // Rule 6, applied at the point of picking: the entry's rarity IS the
          // ref's rarity for club / character / item.
          if (GACHA_RARITY_BEARING_KINDS.includes(kind)) set("rarity", resolved?.rarity ?? "");
          // Convenience only, and only on a row with no id yet — the same rule
          // the shop applies: a pool entry is named after its pool and what it
          // pays out, and typing that twice is how the two drift.
          if (rowIdCtx.isNew && !rowIdCtx.rowId.trim()) {
            const poolId = (draft.poolId ?? "").trim();
            rowIdCtx.setRowId(poolId ? `${poolId}_${refId}` : refId);
          }
        }}
      />

      <label className="block">
        <span className="font-mono text-[11px] text-zinc-500">rarity</span>
        {rarityLocked ? (
          <>
            <input
              value={draft.rarity ?? ""}
              readOnly
              className="mt-0.5 w-full cursor-not-allowed rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-400 focus:outline-none"
            />
            <span className="mt-1 block text-[10px] leading-relaxed text-zinc-600">
              {translate("gp.rarity.locked", { kind })}
            </span>
          </>
        ) : (
          <>
            <select
              value={draft.rarity ?? ""}
              onChange={(e) => set("rarity", e.target.value)}
              className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="">—</option>
              {RARITY_ORDER.map((rarity) => (
                <option key={rarity} value={rarity}>
                  {rarity}
                </option>
              ))}
            </select>
            <span className="mt-1 block text-[10px] leading-relaxed text-zinc-600">
              {translate("gp.rarity.free")}
            </span>
          </>
        )}
      </label>

      <label className="flex items-start gap-2 text-xs text-zinc-300">
        <input
          type="checkbox"
          checked={text(draft.featured).toLowerCase() === "true"}
          onChange={(e) => set("featured", e.target.checked ? "true" : "false")}
          className="mt-0.5 h-3.5 w-3.5 accent-accent-500"
        />
        <span className="font-medium">{translate("gp.featured")}</span>
      </label>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Effective odds + simulation, per pool
// ---------------------------------------------------------------------------

function PoolSummaries({ drafts }: { drafts: GachaDrafts }) {
  const poolIds = useMemo(() => {
    const ids = new Set<string>();
    for (const row of drafts.pools) if (row.isActive && text(row.data.poolId)) ids.add(text(row.data.poolId));
    for (const row of drafts.rates) if (row.isActive && text(row.data.poolId)) ids.add(text(row.data.poolId));
    return Array.from(ids).sort();
  }, [drafts]);

  return (
    <div className="mb-4 space-y-4">
      {poolIds.map((poolId) => (
        <PoolSummary key={poolId} poolId={poolId} drafts={drafts} />
      ))}
    </div>
  );
}

function PoolSummary({ poolId, drafts }: { poolId: string; drafts: GachaDrafts }) {
  const translate = useT();

  const entries = useMemo(
    () =>
      drafts.pools
        .filter((r) => r.isActive && text(r.data.poolId) === poolId)
        .map(toEntry)
        // Sort by rarity then weight desc — the order the odds read best in,
        // and the order §5.3 asks the table to group by.
        .sort((a, b) => rarityRank(a.rarity) - rarityRank(b.rarity) || b.weight - a.weight),
    [drafts.pools, poolId]
  );
  const rates = useMemo(
    () => drafts.rates.filter((r) => r.isActive && text(r.data.poolId) === poolId).map(toRate),
    [drafts.rates, poolId]
  );

  const odds = useMemo(() => effectiveOdds(rates, entries), [rates, entries]);
  const total = totalOdds(odds);

  // The banner whose pity / guarantee the simulation runs with. First active
  // banner on this pool — a simulation with nobody's pity is a simulation of a
  // banner that does not exist.
  const banner = useMemo(() => {
    const hit = drafts.banners.find(
      (b) => b.isActive && text(b.data.poolId) === poolId && text(b.data.active).toLowerCase() === "true"
    );
    if (!hit) return null;
    const roll: BannerRoll & { rowId: string } = {
      rowId: hit.rowId,
      poolId,
      pityThreshold: num(hit.data.pityThreshold),
      pityMinRarity: text(hit.data.pityMinRarity),
      guaranteeMinRarityX10: text(hit.data.guaranteeMinRarityX10),
    };
    return roll;
  }, [drafts.banners, poolId]);

  const [sim, setSim] = useState<SimulateResult | null>(null);
  // Any edit to the pool invalidates a simulation of the old one.
  useEffect(() => setSim(null), [entries, rates, banner]);

  return (
    <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
          <code className="text-zinc-300">{poolId}</code>
          <span className="ml-2 font-normal normal-case tracking-normal text-zinc-600">
            {translate("gp.odds.title")}
          </span>
        </h2>
        <span className={`text-[11px] tabular-nums ${nearlyOne(total) ? "text-zinc-500" : "text-amber-400"}`}>
          {translate("gp.odds.total", { pct: pct(total) })}
        </span>
      </div>

      {rates.length === 0 ? (
        <p className="mt-2 text-[11px] text-amber-300">{translate("gp.odds.noRates", { pool: poolId })}</p>
      ) : (
        <div className="mt-2 overflow-x-auto">
          <table className="w-full min-w-[520px] text-left text-[11px]">
            <tbody>
              {odds.map(({ entry, p }) => (
                <tr key={entry.id} className="border-t border-surface-800/70">
                  <td className="py-1 pr-2">
                    <RarityBadge rarity={entry.rarity} />
                  </td>
                  <td className="py-1 pr-2">
                    <code className="text-zinc-400">{entry.refId}</code>
                    <span className="ml-1.5 text-zinc-600">{entry.kind}</span>
                    {entry.quantity > 1 && <span className="ml-1 text-zinc-500">×{entry.quantity}</span>}
                    {entry.featured && <span className="ml-1.5 text-accent-400">★</span>}
                  </td>
                  <td className="py-1 pr-2 text-right tabular-nums text-zinc-500">w {entry.weight}</td>
                  <td className="py-1 text-right tabular-nums text-zinc-200">{pct(p)}</td>
                  <td className="py-1 pl-2 text-[10px] text-amber-400">
                    {p === 0 ? translate("gp.odds.unreachable") : ""}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="mt-1.5 text-[10px] text-zinc-600">{translate("gp.odds.formula")}</p>

      {/* ---- simulation ---------------------------------------------------- */}
      <div className="mt-3 border-t border-surface-800 pt-3">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            disabled={rates.length === 0 || entries.length === 0}
            onClick={() =>
              setSim(
                simulate(
                  rates,
                  entries,
                  banner ?? { poolId, pityThreshold: 0, pityMinRarity: "", guaranteeMinRarityX10: "" },
                  SIM_PULLS,
                  SIM_SEED
                )
              )
            }
            className="rounded-md border border-accent-500/50 px-3 py-1.5 text-xs font-semibold text-accent-300 transition hover:bg-accent-500/10 disabled:opacity-40"
          >
            {translate("gp.sim.run")}
          </button>
          {banner && (
            <span className="text-[10px] text-zinc-500">
              {translate("gp.sim.banner")}: <code className="text-zinc-400">{banner.rowId}</code>
            </span>
          )}
        </div>

        {sim && (
          <div className="mt-2 overflow-x-auto">
            <table className="w-full min-w-[420px] text-left text-[11px]">
              <thead className="text-[10px] text-zinc-500">
                <tr>
                  <th className="py-1 font-medium">{translate("gp.sim.col.rarity")}</th>
                  <th className="py-1 text-right font-medium">{translate("gp.sim.col.published")}</th>
                  <th className="py-1 text-right font-medium">{translate("gp.sim.col.observed")}</th>
                  <th className="py-1 text-right font-medium">Δ</th>
                </tr>
              </thead>
              <tbody>
                {RARITY_ORDER.filter((rarity) => sim.published[rarity] !== undefined).map((rarity) => {
                  const published = sim.published[rarity] ?? 0;
                  const observed = (sim.observed[rarity] ?? 0) / sim.pulls;
                  const delta = observed - published;
                  return (
                    <tr key={rarity} className="border-t border-surface-800/70">
                      <td className="py-1">
                        <RarityBadge rarity={rarity} />
                      </td>
                      <td className="py-1 text-right tabular-nums text-zinc-500">{pct(published)}</td>
                      <td className="py-1 text-right tabular-nums text-zinc-200">{pct(observed)}</td>
                      {/* Amber past 1.5 points — the acceptance's own threshold.
                          Pity and the x10 guarantee move the distribution ON
                          PURPOSE, so a delta is information, never an error. */}
                      <td
                        className={`py-1 text-right tabular-nums ${
                          Math.abs(delta) > 0.015 ? "text-amber-400" : "text-zinc-500"
                        }`}
                      >
                        {delta >= 0 ? "+" : ""}
                        {(delta * 100).toFixed(2)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <p className="mt-1.5 flex flex-wrap gap-3 text-[10px] text-zinc-500">
              <span>{translate("gp.sim.pulls", { n: sim.pulls.toLocaleString() })}</span>
              <span>{translate("gp.sim.pityHits", { n: sim.pityHits })}</span>
              <span>{translate("gp.sim.guaranteeHits", { n: sim.guaranteeHits })}</span>
            </p>
            <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{translate("gp.sim.note")}</p>
          </div>
        )}
      </div>
    </section>
  );
}

/** Σ rateBp per pool, live from the drafts — §5.3's Rates-tab indicator. */
function RateSums({ drafts }: { drafts: GachaDrafts }) {
  const translate = useT();
  const sums = useMemo(() => {
    const out = new Map<string, number>();
    for (const row of drafts.rates) {
      if (!row.isActive) continue;
      const poolId = text(row.data.poolId);
      if (!poolId) continue;
      out.set(poolId, (out.get(poolId) ?? 0) + num(row.data.rateBp));
    }
    return Array.from(out.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [drafts.rates]);

  if (sums.length === 0) return null;

  return (
    <section className="mb-4 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <ul className="flex flex-wrap gap-x-6 gap-y-1">
        {sums.map(([poolId, sum]) => (
          <li key={poolId} className="text-[11px]">
            <code className="text-zinc-400">{poolId}</code>{" "}
            <span className={sum === 10000 ? "tabular-nums text-accent-300" : "tabular-nums text-red-300"}>
              {sum === 10000
                ? translate("gp.rates.sum.ok", { sum })
                : translate("gp.rates.sum.bad", { sum })}
            </span>
          </li>
        ))}
      </ul>
      <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">{translate("gp.rates.sum.hint")}</p>
    </section>
  );
}

/** 1 within float noise — the odds are a product of divisions. */
const nearlyOne = (total: number): boolean => Math.abs(total - 1) < 1e-9;
