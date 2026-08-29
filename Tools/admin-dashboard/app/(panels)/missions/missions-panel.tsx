"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { ContentStoredRow } from "@/lib/types";
import { CatalogPanel } from "../_content/catalog-panel";
import { fetchRows } from "../_content/client";
import { MissionRowExtras, type ComponentOptions } from "./mission-row-extras";

/**
 * Missions — the `missions` catalog (missions_v1 §A6), the eleventh.
 *
 * 40 ROWS, AND EVERY ONE OF THEM IS A PRICE THE SERVER ENFORCES. Publishing
 * this catalog mirrors each row's tier and RP into `golfin_mission_rewards` in
 * the same request, and `golfin_mission_claim()` credits from THAT — so an edit
 * here is not "the card will say 25 next launch", it is "25 is what lands in the
 * player's ledger". The mirror write is part of the publish and a publish that
 * cannot write it FAILS; see `mirrorMissionRewards` in lib/contentMutations.ts.
 * Same shape as `modes`, one room along, and for the same reason.
 *
 * THE ROW EDITOR IS THE ONE THING THIS PANEL ADDS TO `CatalogPanel`, and it is
 * the reason the panel exists rather than being a fourth entry in Items' tabs. A
 * mission is COMPOSED — hole x start area x wind x loadout x goals — and every
 * one of those is a row id in another catalog. Typed by hand, `TEE_BAK` is a
 * mission nobody can start and the operator finds out at publish, or worse
 * doesn't. Fed from the component catalogs as dropdowns, it cannot be typed
 * wrong at all. (The validator still checks it: the route is reachable without
 * the form, and that is where a bad publish is actually stopped.)
 *
 * WHAT THE PANEL DELIBERATELY DOES NOT DO is show a difficulty number of its
 * own. `difficultyScore` on a row is DISPLAY — the publish recomputes it from
 * `mission_goal_weights` and the component weights, and the validator warns when
 * the stored value has drifted. Rendering a second, panel-computed number would
 * make three sources for one fact.
 */
export function MissionsPanel() {
  const translate = useT();
  const [options, setOptions] = useState<ComponentOptions | null>(null);

  // The component catalogs, once, on mount. Small — 162 + 9 + 13 + 4 rows —
  // and every one of them is needed to render a single row editor, so paging
  // them would buy nothing and cost a spinner inside a drawer.
  const load = useCallback(async () => {
    try {
      const [areas, winds, loadouts, tiers] = await Promise.all([
        fetchRows("mission_start_areas", { limit: 500 }),
        fetchRows("mission_wind_presets", { limit: 100 }),
        fetchRows("mission_loadouts", { limit: 100 }),
        fetchRows("mission_tiers", { limit: 100 }),
      ]);
      setOptions({
        startAreas: areas.rows,
        winds: winds.rows,
        loadouts: loadouts.rows,
        tiers: tiers.rows,
      });
    } catch {
      // A dropdown that could not load falls back to a free-text field rather
      // than blocking the editor: an operator who can still type is better off
      // than one who cannot open the row at all, and the validator is the gate.
      setOptions(null);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("ms.title")}</h1>
        <span className="text-xs text-zinc-500">{translate("ms.note")}</span>
      </div>

      <CatalogPanel
        catalog="missions"
        titleKey="ms.title"
        hideTitle
        banner={
          <p className="mb-4 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-[11px] leading-relaxed text-zinc-400">
            {translate("ms.recomputed")}
          </p>
        }
        editorExtras={(row: ContentStoredRow, draft, set) => (
          <MissionRowExtras options={options} draft={draft} set={set} />
        )}
        editorHiddenColumns={[
          "tier", "holeId", "startAreaId", "windPresetId", "loadoutId",
          "goal1Type", "goal1Param", "goal2Type", "goal2Param", "goal3Type", "goal3Param",
        ]}
      />

      <div className="mt-8">
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
          <h2 className="text-sm font-semibold text-zinc-200">{translate("ms.tiers.title")}</h2>
          <span className="text-xs text-zinc-500">{translate("ms.tiers.note")}</span>
        </div>
        {/* The tier strip is part of THIS panel, per the AdminCatalogs sheet:
            four rows that define the bands the missions above are sorted into.
            Its own catalog, its own publish (it mirrors separately, into
            golfin_mission_tier_bonus) — but nobody opens a tier table except
            while looking at the campaign it tiers. */}
        <CatalogPanel catalog="mission_tiers" titleKey="ms.tiers.title" hideTitle />
      </div>
    </div>
  );
}
