"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { catalogView, ID_COLUMN, type Facet } from "@/lib/contentView";
import type { ContentCatalogSummary, ContentRowsResponse, ContentStoredRow } from "@/lib/types";
import { DirtyBadge, DisabledBadge, RarityBadge } from "./badges";
import { fetchCatalogs, fetchRows } from "./client";
import { PublishDrawer } from "./publish-drawer";
import { RowEditor } from "./row-editor";

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
  /** Extra controls in the row editor (Shop's typeahead). */
  editorExtras?: (
    row: ContentStoredRow,
    draft: Record<string, string>,
    set: (column: string, value: string) => void
  ) => React.ReactNode;
  /** Replaces the default column list from CATALOG_VIEWS. */
  columns?: string[];
  /** Extra facet-like control rendered in the toolbar (Texts' prefix filter). */
  extraFilter?: (query: string, setQuery: (q: string) => void) => React.ReactNode;
  /** Hide the h1 — the Items panel supplies its own above the tabs. */
  hideTitle?: boolean;
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
}: CatalogPanelProps) {
  const translate = useT();
  const view = catalogView(catalog);
  const columns = columnsOverride ?? view.columns;
  const idColumn = ID_COLUMN[catalog] ?? "id";

  const [summary, setSummary] = useState<ContentCatalogSummary | null>(null);
  const [data, setData] = useState<ContentRowsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [facetValue, setFacetValue] = useState<Record<string, string>>({});

  const [editing, setEditing] = useState<ContentStoredRow | null>(null);
  const [publishing, setPublishing] = useState(false);

  /**
   * The one place a filter becomes a server query.
   *
   * `/api/content/:catalog/rows` accepts exactly one free-text `q`, which the
   * route matches against `row_id` OR the catalog's search column. A facet is
   * therefore only offered when its value provably lands in one of those two
   * (see `Facet` in lib/contentView.ts for the measured coverage), and picking
   * a facet REPLACES `q` rather than being AND-ed with it — the route cannot
   * express a conjunction, and pretending otherwise would show an operator a
   * narrower result than the filter claims.
   */
  const activeFacet = useMemo(() => {
    for (const facet of view.facets) {
      const value = facetValue[facet.column];
      if (value) return { facet, value };
    }
    return null;
  }, [view.facets, facetValue]);

  const serverQuery = activeFacet ? activeFacet.facet.toQuery(activeFacet.value) : search.trim();

  const loadSummary = useCallback(async () => {
    try {
      const res = await fetchCatalogs();
      setSummary(res.catalogs.find((c) => c.name === catalog) ?? null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, [catalog]);

  const loadRows = useCallback(async () => {
    try {
      setData(await fetchRows(catalog, { page, limit: view.limit, q: serverQuery }));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, [catalog, page, view.limit, serverQuery]);

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
  }, [serverQuery]);

  async function refresh(message: string) {
    setNotice(message);
    await Promise.all([loadSummary(), loadRows()]);
  }

  const rows = data?.rows ?? [];
  const pages = data ? Math.max(1, Math.ceil(data.total / data.limit)) : 1;

  /** Facet options come from the loaded page — the values, not the filtering. */
  const facetOptions = useMemo(() => {
    const out: Record<string, string[]> = {};
    for (const facet of view.facets) {
      const seen = new Set<string>();
      for (const row of rows) {
        const value = row.data[facet.column];
        if (value) seen.add(value);
      }
      const chosen = facetValue[facet.column];
      if (chosen) seen.add(chosen);
      out[facet.column] = [...seen].sort();
    }
    return out;
  }, [rows, view.facets, facetValue]);

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
          onChange={(e) => {
            setFacetValue({});
            setSearch(e.target.value);
          }}
          placeholder={translate(searchKey ?? "c.search")}
          className="w-64 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />

        {view.facets.map((facet) => (
          <FacetSelect
            key={facet.column}
            facet={facet}
            value={facetValue[facet.column] ?? ""}
            options={facetOptions[facet.column] ?? []}
            onChange={(value) => {
              setSearch("");
              setFacetValue(value ? { [facet.column]: value } : {});
            }}
          />
        ))}

        {extraFilter?.(search, (q) => {
          setFacetValue({});
          setSearch(q);
        })}

        {data && (
          <span className="text-xs text-zinc-500">
            {translate("c.rows.count", { shown: rows.length, total: data.total })}
          </span>
        )}

        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setPublishing(true);
          }}
          disabled={!summary}
          className="ml-auto rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:opacity-40"
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
          onClose={() => setEditing(null)}
          onSaved={async (message) => {
            setEditing(null);
            await refresh(message);
          }}
        >
          {editorExtras ? (draft, set) => editorExtras(editing, draft, set) : undefined}
        </RowEditor>
      )}

      {publishing && summary && (
        <PublishDrawer
          catalog={catalog}
          summary={summary}
          onClose={() => setPublishing(false)}
          onChanged={(message) => void refresh(message)}
        />
      )}
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
  const partial = facet.coverage
    ? translate("c.facet.partial", { hit: facet.coverage.hit, total: facet.coverage.total })
    : "";
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      title={`${translate("c.facet.serverNote")}${partial ? ` ${partial}` : ""}`}
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
