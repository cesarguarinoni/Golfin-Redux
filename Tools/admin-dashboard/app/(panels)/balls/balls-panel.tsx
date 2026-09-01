"use client";

import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Balls — its own panel since 2026-08-31 (ball_data_wiring), when the catalog went
 * from 2 rows to 20 and gained a `rarity` column. It used to be the third tab inside
 * Items, on the reasoning that items + bags + balls were 15 rows between them and did
 * not justify three sidebar entries; at 20 rows with brand AND rarity facets it is the
 * same shape of thing Clubs is, and it faceted like one long before it moved.
 *
 * `CatalogPanel` already knew the `balls` catalog — view config, art folders and art
 * columns were all in place. This panel adds nothing but the route.
 */
export function BallsPanel() {
  return <CatalogPanel catalog="balls" titleKey="bl.title" />;
}
