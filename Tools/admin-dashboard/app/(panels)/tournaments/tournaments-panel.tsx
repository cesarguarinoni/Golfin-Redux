"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { courseName } from "@/lib/courses";
import { fmtDate } from "@/lib/format";
import { artLayer, deriveState, expandHoleSet, prizePoolSummary } from "@/lib/tournament";
import type { DictKey } from "@/lib/i18n";
import type { TournamentKind, TournamentRow, TournamentState, TournamentsResponse } from "@/lib/types";
import { ArtBadge, KindBadge, StateBadge } from "./badges";
import { TournamentEditor } from "./tournament-editor";

const STATES: TournamentState[] = ["Upcoming", "Open", "Ending", "Ended"];

export function TournamentsPanel() {
  const t = useT();
  const [data, setData] = useState<TournamentsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [kindFilter, setKindFilter] = useState<TournamentKind | "all">("golfin");
  const [activeFilter, setActiveFilter] = useState<"all" | "active" | "inactive">("all");
  const [stateFilter, setStateFilter] = useState<TournamentState | "all">("all");
  const [query, setQuery] = useState("");

  const [editing, setEditing] = useState<TournamentRow | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/tournaments");
      const body = (await res.json().catch(() => null)) as
        | (TournamentsResponse & { error?: string })
        | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      if (body) {
        setData(body);
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t("tourn.loadFailed"));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const now = Date.now();
  const rows = useMemo(() => data?.tournaments ?? [], [data]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rows.filter((t) => {
      if (kindFilter !== "all" && t.kind !== kindFilter) return false;
      if (activeFilter === "active" && !t.isActive) return false;
      if (activeFilter === "inactive" && t.isActive) return false;
      if (stateFilter !== "all" && deriveState(t.startAt, t.endAt, now) !== stateFilter) {
        return false;
      }
      if (q) {
        const hay = `${t.slug ?? ""} ${t.title} ${t.courseId ?? ""} ${t.sponsorName ?? ""}`;
        if (!hay.toLowerCase().includes(q)) return false;
      }
      return true;
    });
  }, [rows, kindFilter, activeFilter, stateFilter, query, now]);

  const counts = useMemo(() => {
    const out: Record<string, number> = {};
    for (const t of rows) {
      const s = deriveState(t.startAt, t.endAt, now);
      out[s] = (out[s] ?? 0) + 1;
    }
    return out;
  }, [rows, now]);

  async function afterMutation(message: string) {
    setEditing(null);
    setCreating(false);
    setNotice(message);
    await load();
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        {t("tourn.loadFailed")}: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        {t("tourn.loading")}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">{t("tourn.title")}</h1>
        <span className="text-xs text-zinc-500">
          {rows.filter((r) => !r.isActive).length} {t("tourn.count.inactive")} ·{" "}
          {counts.Open ?? 0} {t("tourn.count.open")} · {counts.Upcoming ?? 0}{" "}
          {t("tourn.count.upcoming")} · {counts.Ended ?? 0} {t("tourn.count.ended")}
        </span>
      </div>

      {/* Phase 3 shipped 2026-08-14: the client now fetches this schedule. The CSV
          export stays because the shipped file is still the OFFLINE fallback. */}
      <div className="mb-4 rounded-lg border border-accent-500/40 bg-accent-500/10 px-4 py-3 text-xs text-accent-200">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <strong className="font-semibold">{t("tourn.live.headline")}</strong>
          <span className="text-accent-200/80">
            {t("tourn.live.body")}
          </span>
          <span className="ml-auto flex gap-2">
            <a
              href="/api/tournaments/export?file=tournaments"
              className="rounded-md border border-accent-500/50 px-2.5 py-1 font-medium text-accent-100 hover:bg-accent-500/15"
            >
              {t("tourn.export.tournaments")}
            </a>
            <a
              href="/api/tournaments/export?file=prizes"
              className="rounded-md border border-accent-500/50 px-2.5 py-1 font-medium text-accent-100 hover:bg-accent-500/15"
            >
              {t("tourn.export.prizes")}
            </a>
          </span>
        </div>
      </div>

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex overflow-hidden rounded-md border border-surface-700 text-xs">
          {(["golfin", "gps", "all"] as const).map((k) => (
            <button
              key={k}
              type="button"
              onClick={() => setKindFilter(k)}
              className={`px-3 py-1.5 font-medium transition ${
                kindFilter === k
                  ? "bg-accent-600 text-white"
                  : "bg-surface-900 text-zinc-400 hover:bg-surface-800"
              }`}
            >
              {k}
            </button>
          ))}
        </div>
        <select
          value={activeFilter}
          onChange={(e) => setActiveFilter(e.target.value as "all" | "active" | "inactive")}
          className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
        >
          <option value="all">{t("tourn.filter.activeAll")}</option>
          <option value="active">{t("tourn.filter.activeOnly")}</option>
          <option value="inactive">{t("tourn.filter.inactiveOnly")}</option>
        </select>
        <select
          value={stateFilter}
          onChange={(e) => setStateFilter(e.target.value as TournamentState | "all")}
          className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
        >
          <option value="all">{t("tourn.filter.allStates")}</option>
          {STATES.map((s) => (
            <option key={s} value={s}>
              {t(`tstate.${s}` as DictKey)}
            </option>
          ))}
        </select>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t("tourn.filter.search")}
          className="w-60 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <span className="text-xs text-zinc-500">
          {filtered.length} {t("common.of")} {rows.length}
        </span>
        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setCreating(true);
          }}
          className="ml-auto rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-accent-500"
        >
          {t("tourn.new")}
        </button>
      </div>

      {/* Table */}
      <div className="mt-4 overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[1000px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.tournament")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.state")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.course")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tourn.col.holes")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tourn.col.fee")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.prizes")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.window")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tourn.col.entries")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tourn.col.art")}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((row) => {
              const state = deriveState(row.startAt, row.endAt, now);
              const pool = prizePoolSummary(row.bands);
              return (
                <tr
                  key={row.id}
                  onClick={() => {
                    setNotice(null);
                    setEditing(row);
                  }}
                  className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                    row.isActive ? "bg-surface-950" : "bg-surface-950/40 opacity-60"
                  }`}
                >
                  <td className="px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-zinc-200">{row.title}</span>
                      <KindBadge kind={row.kind} />
                      {!row.isActive && (
                        <span
                          className="whitespace-nowrap rounded border border-zinc-600 px-1.5 py-0.5 text-[10px] font-bold uppercase text-zinc-400"
                          title={t("tourn.inactiveHint")}
                        >
                          {t("tourn.inactiveBadge")}
                        </span>
                      )}
                    </div>
                    <code className="text-[11px] text-zinc-600">{row.slug ?? "—"}</code>
                  </td>
                  <td className="px-4 py-2.5">
                    <StateBadge state={state} />
                  </td>
                  <td className="px-4 py-2.5 text-xs text-zinc-300">
                    {courseName(row.courseId)}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-400">
                    {expandHoleSet(row.holeSet).length || "—"}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                    {row.entryFeePts > 0 ? `${row.entryFeePts} RP` : t("common.free")}
                  </td>
                  <td className="px-4 py-2.5 text-xs text-zinc-400">
                    {pool.places > 0 ? (
                      <>
                        <span className="font-semibold text-accent-400">
                          {pool.top.toLocaleString()}
                        </span>{" "}
                        {t("tourn.topPlaces")} {pool.places} {t("tourn.places")}
                      </>
                    ) : (
                      <span className="text-amber-400">{t("tourn.noBands")}</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                    {fmtDate(row.startAt)} → {fmtDate(row.endAt)}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                    {row.entryCount}
                    {row.entryCount > 0 && (
                      <span className="ml-1 text-[10px] text-zinc-600">
                        ({row.humanEntryCount} {t("tourn.human")})
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <ArtBadge layer={artLayer(row)} />
                  </td>
                </tr>
              );
            })}
            {filtered.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-10 text-center text-sm text-zinc-600">
                  {t("tourn.none")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {(editing || creating) && (
        <TournamentEditor
          tournament={editing}
          mock={data.mock}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSaved={afterMutation}
        />
      )}
    </div>
  );
}
