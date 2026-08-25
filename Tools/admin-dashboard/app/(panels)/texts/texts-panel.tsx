"use client";

import { useT } from "@/components/I18nProvider";
import type { ContentStoredRow } from "@/lib/types";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Texts — 501 keys, EN and JA side by side.
 *
 * Two things this panel does that a generic table would not:
 *
 *   - the prefix filter is a real server query. Keys are namespaced
 *     (`ROSTER_*`, `HOME_*`, `AUTH_*`), and the rows route matches `q` against
 *     `row_id ILIKE *q*`, so typing a prefix genuinely narrows the SQL rather
 *     than the loaded page. That is a property of the key convention, not a
 *     coincidence — see the CatalogPanel comment on `serverQuery`.
 *   - a missing Japanese value is called out. It is not an error (the game
 *     falls back to English) but it is invisible in a plain table, and
 *     "invisible until a Japanese player sees English" is the failure this
 *     catalog exists to prevent.
 */

/** Prefixes offered as one-click filters. Each is sent as `q`. */
const PREFIXES = [
  "AUTH_",
  "CLUB_",
  "GACHA_",
  "HOME_",
  "INV_",
  "MODAL_",
  "RANK_",
  "ROSTER_",
  "SHOP_",
  "TOURN_",
];

export function TextsPanel() {
  const translate = useT();

  function renderCell(row: ContentStoredRow, column: string) {
    if (column !== "Japanese") return undefined;
    const value = row.data.Japanese ?? "";
    if (value.trim()) {
      return <span className="block max-w-[22rem] truncate">{value}</span>;
    }
    return (
      <span
        title={translate("tx.missingJaHint")}
        className="whitespace-nowrap rounded border border-amber-500/50 bg-amber-500/10 px-1.5 py-0.5 text-[10px] font-bold text-amber-300"
      >
        {translate("tx.missingJa")}
      </span>
    );
  }

  return (
    <CatalogPanel
      catalog="texts"
      titleKey="tx.title"
      searchKey="c.searchTexts"
      renderCell={renderCell}
      extraFilter={(query, setQuery) => (
        <select
          value={PREFIXES.includes(query) ? query : ""}
          onChange={(e) => setQuery(e.target.value)}
          title={translate("c.facet.serverNote")}
          className={`rounded-md border bg-surface-900 px-2.5 py-1.5 text-xs focus:outline-none ${
            PREFIXES.includes(query)
              ? "border-accent-500/60 text-accent-300"
              : "border-surface-700 text-zinc-300 focus:border-accent-500"
          }`}
        >
          <option value="">{translate("tx.prefix.any")}</option>
          {PREFIXES.map((prefix) => (
            <option key={prefix} value={prefix}>
              {prefix}
            </option>
          ))}
        </select>
      )}
    />
  );
}
