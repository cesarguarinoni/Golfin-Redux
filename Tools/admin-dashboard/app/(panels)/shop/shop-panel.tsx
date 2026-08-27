"use client";

import { useT } from "@/components/I18nProvider";
import { SHOP_CATEGORY_STRICT_BUILD, shopCategoryBuildPending } from "@/lib/buildGates";
import { SHOP_CATEGORY_TO_CATALOG, shopOnSale, shopState } from "@/lib/contentView";
import type { ContentStoredRow } from "@/lib/types";
import { ShopStateBadge } from "../_content/badges";
import { CatalogPanel } from "../_content/catalog-panel";
import { RefPicker } from "./ref-picker";

/**
 * Shop — RP offers only (CONTENT_PIPELINE_PLAN.md §11: no IAP, no real money,
 * no store SKUs).
 *
 * The notice at the top is not decoration and must not be quietly dropped in a
 * later redesign — but as of `shop_server_purchase` it is INFORMATION rather
 * than a warning, so it is AMBER, not red. The price IS now authoritative: a
 * purchase is one `POST /api/v1/shop/purchase` that reads the published row,
 * prices it off the SERVER clock and debits + queues the grant in one
 * transaction (SPEC §2).
 *
 * What still has to be said out loud is that enforcement is only as good as the
 * OLDEST build in the wild: an already-installed client keeps debiting locally
 * at its bundled price until the legacy `/points/spend` shop reason is closed
 * (SPEC §2.6, a separate deploy). That is what {build} in the copy is for.
 *
 * The build number itself is NOT declared here any more. It is
 * `SHOP_CATEGORY_STRICT_BUILD` in `lib/buildGates.ts`, because the publish
 * validator gates on the same number (rules G1/G2) and a constant that lives in
 * a panel is a constant the validator cannot see (shop_stocking §3).
 */

const CATEGORIES = Object.keys(SHOP_CATEGORY_TO_CATALOG);

export function ShopPanel() {
  const translate = useT();
  const now = Date.now();
  // Two states, one constant (lib/buildGates.ts). While it is 0 the client half
  // has not been uploaded, validator rule G1 refuses every character/item row,
  // and the banner has to say THAT rather than promise a build number that does
  // not exist yet.
  const buildPending = shopCategoryBuildPending();

  function renderCell(row: ContentStoredRow, column: string) {
    if (column === "category") {
      return (
        <span className="flex items-center gap-2">
          <code className="text-[11px] text-zinc-300">{row.data.category || "—"}</code>
          <ShopStateBadge
            state={shopState(row, now)}
            title={
              shopState(row, now) === "BROKEN"
                ? translate("sh.state.brokenHint")
                : translate("sh.state.hint")
            }
          />
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
      editorHiddenColumns={["category", "refId", "startAt", "endAt", "saleStartAt", "saleEndAt"]}
      banner={
        <div className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-4 py-3">
          <p className="text-xs font-bold text-amber-300">
            {buildPending
              ? translate("sh.notice.pendingHeadline")
              : translate("sh.notice.headline", { build: SHOP_CATEGORY_STRICT_BUILD })}
          </p>
          <p className="mt-1 text-[11px] leading-relaxed text-amber-200/85">
            {buildPending
              ? translate("sh.notice.pendingBody")
              : translate("sh.notice.body", { build: SHOP_CATEGORY_STRICT_BUILD })}
          </p>
        </div>
      }
      editorExtras={(row, draft, set, rowIdCtx) => (
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
            onPick={(refId) => {
              set("refId", refId);
              // Convenience only, and only on a row that has no id yet: a shop
              // entry is named after what it sells (`shop_char_olivia`), and
              // typing that a second time is how the two drift. Still editable
              // — this fills the field, it does not own it.
              if (rowIdCtx.isNew && !rowIdCtx.rowId.trim()) rowIdCtx.setRowId(`shop_${refId}`);
            }}
          />

          {/* The four §11.2 window fields are rendered EXPLICITLY rather than
              left to the generic field list below, so they are editable even on
              a row that predates the columns (a newly added draft has no
              `startAt` key at all, and an absent key would simply not appear). */}
          <div className="space-y-2 border-t border-surface-800 pt-3">
            <p className="text-[10px] font-medium text-zinc-500">{translate("sh.windows.title")}</p>
            <div className="grid grid-cols-2 gap-2">
              {(["startAt", "endAt", "saleStartAt", "saleEndAt"] as const).map((column) => {
                const raw = (draft[column] ?? "").trim();
                const unreadable = raw !== "" && Number.isNaN(Date.parse(raw.replace(" ", "T")));
                return (
                  <label key={column} className="block">
                    <span className="font-mono text-[10px] text-zinc-500">{column}</span>
                    <input
                      value={draft[column] ?? ""}
                      onChange={(e) => set(column, e.target.value)}
                      placeholder="2026-09-01T00:00:00Z"
                      className={`mt-0.5 w-full rounded-md border bg-surface-950 px-2 py-1 font-mono text-[11px] text-zinc-200 placeholder:text-zinc-700 focus:outline-none ${
                        unreadable
                          ? "border-red-500/60 focus:border-red-500"
                          : "border-surface-700 focus:border-accent-500"
                      }`}
                    />
                  </label>
                );
              })}
            </div>
            <p className="text-[10px] leading-relaxed text-zinc-600">{translate("sh.windows.help")}</p>
          </div>
        </div>
      )}
    />
  );
}
