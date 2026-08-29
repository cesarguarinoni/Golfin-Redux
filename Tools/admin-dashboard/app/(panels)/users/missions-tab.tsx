"use client";

import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { PlayerMissionsResponse } from "@/lib/dailyMissionData";

/**
 * The Missions tab of the Users drawer (missions_v1 §A6).
 *
 * UNLIKE THE INVENTORY TAB, EVERYTHING HERE IS SERVER TRUTH. The inventory tab
 * carries a red notice because its blob is client-asserted; `mission_progress`
 * and `daily_mission_claims` are written only by `golfin_mission_claim()` and
 * `golfin_daily_claim()`, so what is shown is what the server recorded. That is
 * a real difference and the absence of a warning here is deliberate, not an
 * omission.
 *
 * `clears: 0` WITH ATTEMPTS IS A REAL STATE and is shown as one — a mission
 * tried and failed is not the same as a mission never opened, and it is usually
 * the thing a support ticket is actually about.
 */
export function MissionsTab({
  data,
  onReset,
}: {
  data: PlayerMissionsResponse;
  onReset: (missionId: string) => void;
}) {
  const t = useT();

  if (data.missions.length === 0 && data.dailyClaims.length === 0) {
    return <p className="py-6 text-center text-xs text-zinc-600">{t("umis.none")}</p>;
  }

  return (
    <div className="space-y-3">
      {data.missions.length > 0 && (
        <ul className="space-y-2">
          {data.missions.map((m) => (
            <li
              key={m.missionId}
              className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="font-mono text-xs text-zinc-300">#{m.missionId}</span>
                <button
                  type="button"
                  onClick={() => onReset(m.missionId)}
                  className="rounded-md border border-surface-700 px-2 py-0.5 text-[10px] font-medium text-zinc-400 transition hover:border-red-500 hover:text-red-300"
                >
                  {t("umis.reset")}
                </button>
              </div>
              <div className="mt-1 flex flex-wrap gap-3 text-[11px] text-zinc-400">
                <span
                  className={m.clears > 0 ? "text-accent-400" : "text-zinc-500"}
                >
                  {m.clears} {t("umis.clears")}
                </span>
                <span>
                  {m.attempts} {t("umis.attempts")}
                </span>
                {m.bestStrokes !== null && (
                  <span>
                    {t("umis.best")} {m.bestStrokes}
                  </span>
                )}
              </div>
              {m.firstClearedAt && (
                <div className="mt-0.5 text-[10px] text-zinc-600">
                  {fmtDateTime(m.firstClearedAt)}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {data.dailyClaims.length > 0 && (
        <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
          <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
            {t("umis.dailyClaims")}
          </span>
          <ul className="mt-1.5 space-y-1">
            {data.dailyClaims.map((c) => (
              <li key={c.date} className="flex items-baseline justify-between text-[11px]">
                <code className="text-zinc-400">{c.date}</code>
                <span className="text-zinc-300">
                  +{c.rp}
                  <span className="ml-2 text-zinc-500">
                    {c.streak} {t("umis.streak")}
                  </span>
                  {c.strokes !== null && <span className="ml-2 text-zinc-600">{c.strokes}</span>}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
