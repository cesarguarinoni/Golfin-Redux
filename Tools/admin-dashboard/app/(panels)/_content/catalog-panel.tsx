"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { catalogView, ID_COLUMN, type Facet } from "@/lib/contentView";
import type { ContentCatalogSummary, ContentRowsResponse, ContentStoredRow } from "@/lib/types";
import { DirtyBadge, DisabledBadge, RarityBadge } from "./badges";
import { fetchCatalogs, fetchRows } from "./client";
import { PublishDrawer } from "./publish-drawer";
import { RowEditor, type RowIdContext } from "./row-editor";

/**
 * The shared catalog panel: toolbar → server-paged table → row editor →
 * publish drawer. Clubs, Characters, Items/Bags/Balls and Texts are all this
 * component plus a `CatalogView` descriptor; Shop composes its own row cells on
 * top of the same machinery.
 *
 * NOTHING IS EVER FETCHED WHOLE. Every list read goes through
 * `/api/content/:catalog/rows?page=&limit=&q=`, so the 799-row clubs catalog
 * arrives 50 rows at a time and the browser never holds more than one page.
 * `q` and the facets are both part of the QUERY — see `serverQuery` below —
 * not a `.filter()` over what happens to be loaded.
 *
 * NOTE ON NAMING: the translator is `translate`, not `t`, and row callbacks are
 * `(row) =>`. `rows.map((t) => …)` has shadowed the translator twice in this
 * codebase (ADMIN_DASHBOARD_OPS.md §3.4) — including, still, in
 * tournaments-panel.tsx and tournament-editor.tsx.
 */

export interface CatalogPanelProps {
  catalog: string;
  titleKey: DictKey;
  /** Optional element rendered between the header and the toolbar. */
  banner?: React.ReactNode;
  /** Override the search-box placeholder. */
  searchKey?: DictKey;
  /** Custom cell renderer; falls back to the raw string. */
  renderCell?: (row: ContentStoredRow, column: string) => React.ReactNode;
  /** Extra controls in the row editor (Shop's typeahead). `rowIdCtx` is how a
   *  panel prefills the id of a NEW row — Shop derives it from the picked ref. */
  editorExtras?: (
    row: ContentStoredRow,
    draft: Record<string, string>,
    set: (column: string, value: string) => void,
    rowIdCtx: RowIdContext
  ) => React.ReactNode;
  /** Replaces the default column list from CATALOG_VIEWS. */
  columns?: string[];
  /** Extra facet-like control rendered in the toolbar (Texts' prefix filter). */
  extraFilter?: (query: string, setQuery: (q: string) => void) => React.ReactNode;
  /** Hide the h1 — the Items panel supplies its own above the tabs. */
  hideTitle?: boolean;
  /** Columns `editorExtras` already renders, so the raw field list skips them. */
  editorHiddenColumns?: string[];
}

export function CatalogPanel({
  catalog,
  titleKey,
  banner,
  searchKey,
  renderCell,
  editorExtras,
  columns: columnsOverride,
  extraFilter,
  hideTitle,
  editorHiddenColumns,
}: CatalogPanelProps) {
  const translate = useT();
  const view = catalogView(catalog);
  const columns = columnsOverride ?? view.columns;
  const idColumn = ID_COLUMN[catalog] ?? "id";

  const [summary, setSummary] = useState<ContentCatalogSummary | null>(null);
  /** The GLOBAL kill switch (PLAN §7.4) — see GlobalKillBanner below. */
  const [globalEnabled, setGlobalEnabled] = useState(true);
  const [data, setData] = useState<ContentRowsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [facetValue, setFacetValue] = useState<Record<string, string>>({});
  /** Distinct values per field, from the SERVER — see fetchFacetValues. */
  const [facetValues, setFacetValues] = useState<Record<string, string[]> | null>(null);

  const [editing, setEditing] = useState<ContentStoredRow | null>(null);
  /** True while `editing` is the blank row from `+ New row` (shop_stocking §2). */
  const [creating, setCreating] = useState(false);
  const [publishing, setPublishing] = useState(false);

  /**
   * Filters are structured and AND-ed, server-side (content_panels_gaps §1).
   *
   * Each facet is its own query parameter matching `data->>'<field>'` exactly,
   * so brand=BogeyB AND rarity=Common is one query and `total` is the count of
   * the FILTERED set. The previous version had to squeeze a facet through the
   * single free-text `q`, which meant one facet at a time and — for rarity —
   * matching the row id instead of the field. Both limits are gone.
   */
  const activeFilters = useMemo(() => {
    const out: Record<string, string> = {};
    for (const facet of view.facets) {
      const value = facetValue[facet.column];
      if (value) out[facet.column] = value;
    }
    return out;
  }, [view.facets, facetValue]);

  // Stable identity for the effect dependency — an object literal would refetch
  // on every render.
  const filterKey = JSON.stringify(activeFilters);
  const searchQuery = search.trim();

  const loadSummary = useCallback(async () => {
    try {
      const res = await fetchCatalogs();
      setSummary(res.catalogs.find((c) => c.name === catalog) ?? null);
      setGlobalEnabled(res.globalEnabled);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, [catalog]);

  const loadRows = useCallback(async () => {
    try {
      const res = await fetchRows(catalog, {
        page,
        limit: view.limit,
        q: searchQuery,
        filters: JSON.parse(filterKey) as Record<string, string>,
        // Ask for the catalog-wide distinct values once, on the first load.
        withFacets: view.facets.length > 0 && !facetValues,
      });
      setData(res);
      if (res.facetValues) setFacetValues(res.facetValues);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
    // `facetValues` is intentionally not a dependency: including it would
    // refetch the moment the values arrive.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [catalog, page, view.limit, view.facets.length, searchQuery, filterKey]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);
  useEffect(() => {
    void loadRows();
  }, [loadRows]);

  // Any query change resets to page 1 — page 7 of a narrower result is a blank
  // table that looks like "no rows match".
  useEffect(() => {
    setPage(1);
  }, [searchQuery, filterKey]);

  async function refresh(message: string) {
    setNotice(message);
    await Promise.all([loadSummary(), loadRows()]);
  }

  const rows = data?.rows ?? [];
  const pages = data ? Math.max(1, Math.ceil(data.total / data.limit)) : 1;

  /**
   * Options come from the SERVER's distinct values over the whole catalog, not
   * from the rows on screen. That is the difference between "BogeyB is
   * selectable" and "BogeyB is selectable once you happen to page onto it".
   */
  const facetOptions = facetValues ?? {};

  return (
    <div>
      {/* `hideTitle` drops the <h1> only — the Items panel supplies its own
          above the tabs. The version / dirty / kill-switch badges ALWAYS
          render: each tab is a separate catalog that publishes independently,
          so hiding its state along with its heading would be the one thing an
          operator most needs to see before pressing publish. */}
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        {hideTitle ? (
          <span />
        ) : (
          <h1 className="text-lg font-semibold text-zinc-100">{translate(titleKey)}</h1>
        )}
        {summary && (
          <div className="flex flex-wrap items-center gap-2 text-xs text-zinc-500">
            <code className="text-zinc-600">{catalog}</code>
            <span>{translate("c.version", { n: summary.publishedVersion })}</span>
            <DirtyBadge count={summary.dirtyCount} />
            {!summary.isEnabled && <DisabledBadge />}
          </div>
        )}
      </div>

      {/* ⚠️ ABOVE the panel's own banner and above everything else, because it OUTRANKS
          everything else on this screen: while the global switch is off, no publish on any
          catalog reaches a single player. An operator publishing into a global kill and seeing
          "Published v12" with no other signal is the failure this exists to prevent. */}
      {!globalEnabled && <GlobalKillBanner />}

      {banner}

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}
      {error && (
        <div className="mb-4 rounded-lg border border-red-500/40 bg-red-500/10 p-3 text-sm text-red-300">
          {translate("c.loadFailed")}: {error}
        </div>
      )}

      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={translate(searchKey ?? "c.search")}
          className="w-64 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />

        {view.facets.map((facet) => (
          <FacetSelect
            key={facet.column}
            facet={facet}
            value={facetValue[facet.column] ?? ""}
            options={facetOptions[facet.column] ?? []}
            onChange={(value) =>
              setFacetValue((prev) => {
                const next = { ...prev };
                if (value) next[facet.column] = value;
                else delete next[facet.column];
                return next;
              })
            }
          />
        ))}

        {extraFilter?.(search, setSearch)}

        {data && (
          <span className="text-xs text-zinc-500">
            {translate("c.rows.count", { shown: rows.length, total: data.total })}
          </span>
        )}

        {/* CREATE. Until this existed, every row in every catalog came from the
            seed migration: the panel could edit and publish, but not add. The
            backend was always able — PUT upserts by rowId and content_publish
            is `on conflict do update` — so this is the missing control, not a
            new capability. Registered HERE rather than in the Shop panel so all
            seven catalogs get it (shop_stocking §2). */}
        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setCreating(true);
            setEditing({ catalog, rowId: "", data: {}, minBuild: 0, isActive: true });
          }}
          className="ml-auto rounded-md border border-accent-500/50 px-3 py-1.5 text-xs font-semibold text-accent-300 transition hover:bg-accent-500/10"
        >
          {translate("c.newRow")}
        </button>

        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setPublishing(true);
          }}
          disabled={!summary}
          className="rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:opacity-40"
        >
          {translate("c.publishOpen")}
          {summary && summary.dirtyCount > 0 && (
            <span className="ml-1.5 rounded bg-white/20 px-1 py-0.5 text-[10px] tabular-nums">
              {summary.dirtyCount}
            </span>
          )}
        </button>
      </div>

      <p className="mt-2 text-[11px] text-zinc-600">{translate("c.serverPaged")}</p>

      {/* Table */}
      <div className="mt-3 overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[900px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("c.col.rowId")}</th>
              {columns.map((column) => (
                <th key={column} className="whitespace-nowrap px-4 py-2.5 font-mono font-medium">
                  {column}
                </th>
              ))}
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("c.col.state")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.rowId}
                onClick={() => {
                  setNotice(null);
                  setCreating(false);
                  setEditing(row);
                }}
                className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                  row.isActive ? "bg-surface-950" : "bg-surface-950/40 opacity-60"
                }`}
              >
                <td className="px-4 py-2.5">
                  <code className="text-[11px] text-zinc-400">{row.data[idColumn] ?? row.rowId}</code>
                </td>
                {columns.map((column) => (
                  <td key={column} className="px-4 py-2.5 text-xs text-zinc-300">
                    {renderCell?.(row, column) ??
                      (column === "rarity" ? (
                        <RarityBadge rarity={row.data.rarity ?? ""} />
                      ) : (
                        <span className="block max-w-[22rem] truncate">{row.data[column] || "—"}</span>
                      ))}
                  </td>
                ))}
                <td className="px-4 py-2.5">
                  {row.isActive ? (
                    <span className="text-[11px] text-zinc-600">—</span>
                  ) : (
                    <span className="whitespace-nowrap rounded border border-zinc-600 px-1.5 py-0.5 text-[10px] font-bold text-zinc-400">
                      OFF
                    </span>
                  )}
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={columns.length + 2} className="px-4 py-10 text-center text-sm text-zinc-600">
                  {data ? translate("c.rows.none") : translate("c.loading")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Server pagination */}
      {data && pages > 1 && (
        <div className="mt-3 flex items-center justify-center gap-3 text-xs text-zinc-500">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            className="rounded-md border border-surface-700 px-2.5 py-1 text-zinc-300 hover:bg-surface-800 disabled:opacity-30"
          >
            {translate("common.prev")}
          </button>
          <span className="tabular-nums">{translate("c.page", { page, pages })}</span>
          <button
            type="button"
            disabled={page >= pages}
            onClick={() => setPage((p) => Math.min(pages, p + 1))}
            className="rounded-md border border-surface-700 px-2.5 py-1 text-zinc-300 hover:bg-surface-800 disabled:opacity-30"
          >
            {translate("common.next")}
          </button>
        </div>
      )}

      {editing && (
        <RowEditor
          catalog={catalog}
          row={editing}
          columns={columns}
          published={editing.version !== undefined}
          isNew={creating}
          hiddenColumns={editorHiddenColumns}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSaved={async (message) => {
            setEditing(null);
            setCreating(false);
            await refresh(message);
          }}
        >
          {editorExtras
            ? (draft, set, rowIdCtx) => editorExtras(editing, draft, set, rowIdCtx)
            : undefined}
        </RowEditor>
      )}

      {publishing && summary && (
        <PublishDrawer
          catalog={catalog}
          summary={summary}
          globalEnabled={globalEnabled}
          onClose={() => setPublishing(false)}
          onChanged={(message) => void refresh(message)}
        />
      )}
    </div>
  );
}

/**
 * Remote content is off for EVERY player — `content_settings.content_enabled` is false.
 *
 * Deliberately loud and deliberately not a badge: the per-catalog OFF state is a badge next to
 * the version, and these two must never look alike at a glance. One catalog reverting to its
 * bundled CSV and the whole pipeline being dark are different emergencies.
 */
function GlobalKillBanner() {
  const translate = useT();
  return (
    <div className="mb-4 rounded-lg border border-red-500/50 bg-red-500/10 px-3 py-2.5">
      <p className="text-xs font-bold text-red-300">⚠ {translate("c.globalKill.headline")}</p>
      <p className="mt-1 text-[11px] leading-relaxed text-red-200/85">
        {translate("c.globalKill.body")}
      </p>
    </div>
  );
}

function FacetSelect({
  facet,
  value,
  options,
  onChange,
}: {
  facet: Facet;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}) {
  const translate = useT();
  const label = translate(facet.labelKey);
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      title={translate("c.facet.serverNote")}
      className={`rounded-md border bg-surface-900 px-2.5 py-1.5 text-xs focus:outline-none ${
        value
          ? "border-accent-500/60 text-accent-300"
          : "border-surface-700 text-zinc-300 focus:border-accent-500"
      }`}
    >
      <option value="">{translate("c.facet.any", { label })}</option>
      {options.map((option) => (
        <option key={option} value={option}>
          {option}
        </option>
      ))}
    </select>
  );
}
