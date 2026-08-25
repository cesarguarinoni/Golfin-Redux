"use client";

import { useT } from "@/components/I18nProvider";
import { SHOP_CATEGORY_TO_CATALOG, shopOnSale, shopState } from "@/lib/contentView";
import type { ContentStoredRow } from "@/lib/types";
import { ShopStateBadge } from "../_content/badges";
import { CatalogPanel } from "../_content/catalog-panel";
import { RefPicker } from "./ref-picker";

/**
 * Shop — RP offers only (CONTENT_PIPELINE_PLAN.md §11: no IAP, no real money,
 * no store SKUs).
 *
 * The loud red notice at the top is not decoration and must not be quietly
 * dropped in a later redesign. §11.5: purchases still debit RP on the CLIENT
 * through `PointsSpendGate` and grant locally. Making the listing
 * admin-editable makes it very easy to assume the price is now authoritative —
 * it is not, and a panel that lets an operator believe otherwise is worse than
 * no panel.
 */

const CATEGORIES = Object.keys(SHOP_CATEGORY_TO_CATALOG);

export function ShopPanel() {
  const translate = useT();
  const now = Date.now();

  function renderCell(row: ContentStoredRow, column: string) {
    if (column === "category") {
      return (
        <span className="flex items-center gap-2">
          <code className="text-[11px] text-zinc-300">{row.data.category || "—"}</code>
          <ShopStateBadge state={shopState(row, now)} title={translate("sh.windowsMissing")} />
        </span>
      );
    }

    if (column === "rpCost") {
      const onSale = shopOnSale(row, now);
      return (
        <span className="flex items-center gap-1.5 tabular-nums">
          <span className={onSale ? "text-zinc-600 line-through" : "text-zinc-200"}>
            {row.data.rpCost || "—"}
          </span>
          {onSale && (
            <>
              <span className="font-semibold text-accent-300">{row.data.saleRpCost}</span>
              <span className="whitespace-nowrap rounded border border-accent-500/40 bg-accent-500/10 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                {translate("sh.sale")}
              </span>
            </>
          )}
          <span className="text-[10px] text-zinc-600">RP</span>
        </span>
      );
    }

    if (column === "popular" || column === "offer") {
      const on = (row.data[column] ?? "").trim().toLowerCase() === "true";
      return (
        <span className={on ? "text-accent-300" : "text-zinc-600"}>
          {on ? column.toUpperCase() : "—"}
        </span>
      );
    }

    return undefined;
  }

  return (
    <CatalogPanel
      catalog="shop_catalog"
      titleKey="sh.title"
      renderCell={renderCell}
      banner={
        <div className="mb-4 rounded-lg border border-red-500/50 bg-red-500/10 px-4 py-3">
          <p className="text-xs font-bold text-red-300">⚠ {translate("sh.notice.headline")}</p>
          <p className="mt-1 text-[11px] leading-relaxed text-red-200/85">
            {translate("sh.notice.body")}
          </p>
        </div>
      }
      editorExtras={(row, draft, set) => (
        <div className="space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
          <div>
            <span className="font-mono text-[11px] text-zinc-500">category</span>
            <select
              value={draft.category ?? ""}
              onChange={(e) => {
                // Changing category invalidates the reference: a club id is not
                // a ball id. Clearing it is what forces a deliberate re-pick
                // instead of leaving a refId that resolves in the wrong catalog.
                set("category", e.target.value);
                set("refId", "");
              }}
              className="mt-0.5 block w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="">—</option>
              {CATEGORIES.map((category) => (
                <option key={category} value={category}>
                  {category} → {SHOP_CATEGORY_TO_CATALOG[category]}
                </option>
              ))}
            </select>
          </div>

          <RefPicker
            category={draft.category ?? ""}
            refId={draft.refId ?? ""}
            onPick={(refId) => set("refId", refId)}
          />

          <p className="text-[10px] leading-relaxed text-zinc-600">
            {translate("sh.windowsMissing")}
          </p>
        </div>
      )}
    />
  );
}
