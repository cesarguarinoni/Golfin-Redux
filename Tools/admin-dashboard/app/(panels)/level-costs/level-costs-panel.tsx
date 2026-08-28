"use client";

import { useT } from "@/components/I18nProvider";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Level Costs — the `level_up_costs` catalog (progress_server_side §2).
 *
 * 240 rows of `level,cost_r,sp_reward`, shared by characters and clubs, and the
 * ONE catalog the server prices from directly: `golfin_level_up()` sums `cost_r`
 * over the published rows for the levels being bought. So an edit here is not a
 * display change — it is what every player pays for their next level, from their
 * next launch.
 *
 * The panel itself is deliberately the shared `CatalogPanel` with nothing added:
 * 240 rows is exactly the case its server-side pagination already exists for
 * (clubs is 799), and a second table implementation would be a second place for
 * the row editor, the publish drawer, the kill switch and the dirty count to
 * drift. The rules that ARE specific to this catalog live in the validator, not
 * here, because that is where a bad publish has to be stopped.
 */
export function LevelCostsPanel() {
  const translate = useT();

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("lu.title")}</h1>
        <span className="text-xs text-zinc-500">{translate("lu.note")}</span>
      </div>

      <CatalogPanel catalog="level_up_costs" titleKey="lu.title" hideTitle />
    </div>
  );
}
