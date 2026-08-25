"use client";

import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Clubs — 799 rows, which is why this panel exists as a panel rather than a
 * table. Every read is one server page (50 rows) and both the search box and
 * the brand / type / rarity facets are part of the QUERY, never a filter over
 * the loaded array. See `CatalogPanel` for how a facet becomes `q`, and
 * `lib/contentView.ts` for the measured coverage of each one.
 */
export function ClubsPanel() {
  return <CatalogPanel catalog="clubs" titleKey="cl.title" />;
}
