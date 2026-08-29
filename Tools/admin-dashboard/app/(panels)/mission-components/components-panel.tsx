"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { holeBase, scoreGoal, tierForScore, type WeightRow } from "@/lib/missionScore";
import type { ContentStoredRow } from "@/lib/types";
import { CatalogPanel } from "../_content/catalog-panel";
import { fetchRows } from "../_content/client";

/**
 * Mission Components — the five catalogs a mission is composed FROM
 * (missions_v1 §A6), one tab each.
 *
 * One panel rather than five sidebar entries, for the reason the Items panel
 * gives: 263 rows across five tables that are only ever opened while thinking
 * about the same thing. None of them has a server mirror — they are pure client
 * and generator data — but "no mirror" is not "inert", and the Goal weights tab
 * is why.
 *
 * THE GOAL-WEIGHTS TAB SHOWS THE RE-SCORED CAMPAIGN BEFORE YOU PUBLISH.
 * `difficultyScore` is not stored truth, it is recomputed from these weights on
 * every `missions` publish — so raising AVOID Rough from 2 to 3 does not change
 * one number, it can push four missions up a tier and re-order the campaign
 * ladder. That consequence is invisible in a table of weights, which is exactly
 * why the AdminCatalogs sheet asked for the re-scored table to be shown here.
 * The preview reads the DRAFT weights and the DRAFT missions, because that is
 * the state the publish will act on.
 */

const TABS = [
  { catalog: "mission_start_areas", labelKey: "mc.tab.startAreas", noteKey: "mc.startAreas.note" },
  { catalog: "mission_wind_presets", labelKey: "mc.tab.wind", noteKey: null },
  { catalog: "mission_loadouts", labelKey: "mc.tab.loadouts", noteKey: "mc.loadouts.note" },
  { catalog: "mission_goal_weights", labelKey: "mc.tab.goalWeights", noteKey: "mc.goalWeights.note" },
  { catalog: "daily_mission_weights", labelKey: "mc.tab.dailyWeights", noteKey: null },
] as const;

type TabCatalog = (typeof TABS)[number]["catalog"];

const text = (v: unknown): string => (v === null || v === undefined ? "" : String(v).trim());
const num = (v: unknown): number => {
  const n = Number(text(v));
  return Number.isFinite(n) ? n : 0;
};

interface Rescored {
  rowId: string;
  name: string;
  stored: number;
  scored: number;
  storedTier: string;
  scoredTier: string | null;
}

function RescorePreview() {
  const translate = useT();
  const [rows, setRows] = useState<Rescored[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [missions, weights, areas, winds, loadouts, tiers] = await Promise.all([
        fetchRows("missions", { limit: 200 }),
        fetchRows("mission_goal_weights", { limit: 200 }),
        fetchRows("mission_start_areas", { limit: 500 }),
        fetchRows("mission_wind_presets", { limit: 100 }),
        fetchRows("mission_loadouts", { limit: 100 }),
        fetchRows("mission_tiers", { limit: 100 }),
      ]);

      const weightRows: WeightRow[] = weights.rows.map((r) => ({
        goal: text(r.data.goal),
        match: text(r.data.match),
        scope: text(r.data.scope),
        param: text(r.data.param),
        weight: text(r.data.weight),
      }));
      const areaBy = new Map<string, ContentStoredRow>();
      for (const r of areas.rows) {
        areaBy.set(`${text(r.data.holeId)}:${text(r.data.areaId)}`, r);
      }
      const windBy = new Map(winds.rows.map((r) => [r.rowId, r]));
      const loadoutBy = new Map(loadouts.rows.map((r) => [r.rowId, r]));
      const tierBands = tiers.rows.map((r) => ({
        tier: r.rowId,
        scoreMin: num(r.data.scoreMin),
        scoreMaxExcl: num(r.data.scoreMaxExcl),
      }));

      const out: Rescored[] = [];
      for (const m of missions.rows) {
        const area = areaBy.get(`${text(m.data.holeId)}:${text(m.data.startAreaId)}`);
        const wind = windBy.get(text(m.data.windPresetId));
        const loadout = loadoutBy.get(text(m.data.loadoutId));
        if (!area || !wind || !loadout) continue;
        const par = num(m.data.par);

        let score = holeBase(weightRows, par);
        score += num(area.data.weight) + num(wind.data.weight) + num(loadout.data.weight);
        for (const slot of [1, 2, 3]) {
          const goalType = text(m.data[`goal${slot}Type`]);
          if (!goalType) continue;
          score += scoreGoal(weightRows, goalType, text(m.data[`goal${slot}Param`]), text(area.data.kind), par);
        }

        const storedTier = text(m.data.tier);
        const scoredTier = tierForScore(tierBands, score);
        // Only the rows that MOVED. A table of 40 unchanged numbers hides the
        // two that changed, which is the opposite of the point.
        if (score !== num(m.data.difficultyScore) || scoredTier !== storedTier) {
          out.push({
            rowId: m.rowId,
            name: text(m.data.name_en) || m.rowId,
            stored: num(m.data.difficultyScore),
            scored: score,
            storedTier,
            scoredTier,
          });
        }
      }
      out.sort((a, b) => Number(a.rowId) - Number(b.rowId));
      setRows(out);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <section className="mt-6 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <h2 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
        {translate("mc.rescore.title")}
      </h2>
      {error && <p className="mt-2 text-[11px] text-red-300">{error}</p>}
      {!rows && !error && <p className="mt-2 text-[11px] text-zinc-600">{translate("common.loading")}</p>}
      {rows && rows.length === 0 && (
        <p className="mt-2 text-[11px] text-zinc-500">{translate("mc.rescore.none")}</p>
      )}
      {rows && rows.length > 0 && (
        <ul className="mt-2 space-y-1">
          {rows.map((r) => {
            const movedTier = r.scoredTier !== r.storedTier;
            return (
              <li
                key={r.rowId}
                className="flex items-baseline justify-between rounded-md border border-surface-800 px-2 py-1.5 text-[11px]"
              >
                <span className="text-zinc-300">
                  <code className="text-zinc-500">#{r.rowId}</code> {r.name}
                </span>
                <span className={`tabular-nums ${movedTier ? "text-amber-400" : "text-zinc-400"}`}>
                  {r.stored} → {r.scored}
                  {movedTier && (
                    <span className="ml-2 font-semibold">
                      {r.storedTier} → {r.scoredTier ?? "—"}
                    </span>
                  )}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

export function MissionComponentsPanel() {
  const translate = useT();
  const [tab, setTab] = useState<TabCatalog>("mission_start_areas");
  const active = useMemo(() => TABS.find((t) => t.catalog === tab) ?? TABS[0], [tab]);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("mc.title")}</h1>
        <span className="text-xs text-zinc-500">{translate("mc.note")}</span>
      </div>

      <div className="mb-4 flex gap-1 border-b border-surface-800">
        {TABS.map((entry) => (
          <button
            key={entry.catalog}
            type="button"
            onClick={() => setTab(entry.catalog)}
            className={`rounded-t-md px-3 py-2 text-xs font-medium transition ${
              tab === entry.catalog
                ? "border-b-2 border-accent-500 text-zinc-100"
                : "text-zinc-500 hover:text-zinc-300"
            }`}
          >
            {translate(entry.labelKey as DictKey)}
          </button>
        ))}
      </div>

      <CatalogPanel
        key={active.catalog}
        catalog={active.catalog}
        titleKey="mc.title"
        hideTitle
        banner={
          active.noteKey ? (
            <p className="mb-4 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-[11px] leading-relaxed text-zinc-400">
              {translate(active.noteKey as DictKey)}
            </p>
          ) : undefined
        }
      />

      {tab === "mission_goal_weights" && <RescorePreview />}
    </div>
  );
}
