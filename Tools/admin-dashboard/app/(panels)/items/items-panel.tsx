"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Items and Bags — TWO catalogs behind two tabs in ONE panel.
 *
 * They are 3 + 10 = 13 rows between them; two sidebar entries for that would
 * cost more attention than it returns. They stay two separate catalogs
 * underneath — each has its own version, its own dirty count, its own kill
 * switch and its own publish, and the drawer is scoped to whichever tab is
 * open. The tab is a navigation convenience, not a merge.
 *
 * Balls was the third tab until 2026-08-31 (ball_data_wiring), when the catalog
 * went from 2 rows to 20 and gained a rarity facet; it moved to its own sidebar
 * entry at `/balls`. Nothing else changed — the users drawer and the gacha and
 * shop panels reach the `balls` catalog by NAME, not through this panel.
 */

const TABS = [
  { catalog: "items", labelKey: "it.tab.items" },
  { catalog: "bags", labelKey: "it.tab.bags" },
] as const;

export function ItemsPanel() {
  const translate = useT();
  const [active, setActive] = useState<(typeof TABS)[number]["catalog"]>("items");

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("it.title")}</h1>
        <span className="text-xs text-zinc-500">{translate("it.oneCatalogNote")}</span>
      </div>

      <nav className="mb-4 flex gap-1">
        {TABS.map((tab) => (
          <button
            key={tab.catalog}
            type="button"
            onClick={() => setActive(tab.catalog)}
            className={`whitespace-nowrap rounded-md px-3 py-1.5 text-xs font-medium transition ${
              active === tab.catalog
                ? "bg-surface-700 text-zinc-100"
                : "text-zinc-500 hover:bg-surface-800 hover:text-zinc-300"
            }`}
          >
            {translate(tab.labelKey as DictKey)}
          </button>
        ))}
      </nav>

      {/* `key` remounts on tab change so page/search/facet state cannot leak
          from one catalog into another — a stale `page=3` against a 2-row
          catalog renders as "no rows match", which reads as a bug. */}
      <CatalogPanel
        key={active}
        catalog={active}
        titleKey={`it.tab.${active === "items" ? "items" : "bags"}` as DictKey}
        hideTitle
      />
    </div>
  );
}
