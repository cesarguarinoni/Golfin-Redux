"use client";

import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Characters — 12 rows, so no facets and one page. Publishing this catalog also
 * mirrors into `golfin_characters` (the table tournament rarity restrictions
 * read); that happens server-side in lib/contentMutations.ts and is why a
 * failed mirror blocks the whole publish rather than half-applying it.
 */
export function CharactersPanel() {
  return <CatalogPanel catalog="characters" titleKey="ch.title" />;
}
