"use client";

import { useT } from "@/components/I18nProvider";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Characters — 12 rows, so no facets and one page. Publishing this catalog also
 * mirrors into `golfin_characters` (the table tournament rarity restrictions
 * read); that happens server-side in lib/contentMutations.ts and is why a
 * failed mirror blocks the whole publish rather than half-applying it.
 *
 * The banner is content_two_way §6, and it is the same amber component the Shop
 * panel uses. It exists because `+ New row` makes creating a character feel
 * complete, and it is not: the row's DATA is created here, its ART ships with
 * the next build that bundles the sprites, and in between §4 withholds the
 * character everywhere instead of drawing a blank card. That withholding is the
 * correct behaviour and it is invisible — so it is stated here rather than
 * discovered.
 */
export function CharactersPanel() {
  const translate = useT();

  return (
    <CatalogPanel
      catalog="characters"
      titleKey="ch.title"
      banner={
        <div className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-4 py-3">
          <p className="text-xs font-bold text-amber-300">{translate("ch.notice.headline")}</p>
          <p className="mt-1 text-[11px] leading-relaxed text-amber-200/85">
            {translate("ch.notice.body")}
          </p>
        </div>
      }
    />
  );
}
