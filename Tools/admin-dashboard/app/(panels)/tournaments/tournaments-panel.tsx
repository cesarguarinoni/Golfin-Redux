"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { courseName } from "@/lib/courses";
import { fmtDate } from "@/lib/format";
import { artLayer, deriveState, expandHoleSet, prizePoolSummary } from "@/lib/tournament";
import type { TournamentKind, TournamentRow, TournamentState, TournamentsResponse } from "@/lib/types";
import { ArtBadge, KindBadge, StateBadge } from "./badges";
import { TournamentEditor } from "./tournament-editor";

const STATES: TournamentState[] = ["Upcoming", "Open", "Ending", "Ended"];

export function TournamentsPanel() {
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
      setError(err instanceof Error ? err.message : "Failed to load tournaments");
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
        Failed to load tournaments: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        Loading tournaments…
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">Tournaments</h1>
        <span className="text-xs text-zinc-500">
          {rows.filter((t) => !t.isActive).length} inactive · {counts.Open ?? 0} open ·{" "}
          {counts.Upcoming ?? 0} upcoming · {counts.Ended ?? 0} ended
        </span>
      </div>

      {/* Phase 3 shipped 2026-08-14: the client now fetches this schedule. The CSV
          export stays because the shipped file is still the OFFLINE fallback. */}
      <div className="mb-4 rounded-lg border border-accent-500/40 bg-accent-500/10 px-4 py-3 text-xs text-accent-200">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <strong className="font-semibold">Edits here reach players on their next launch.</strong>
          <span className="text-accent-200/80">
            The game fetches this schedule at boot and falls back to the shipped{" "}
            <code>tournaments.csv</code> only when it cannot reach the server. Re-export at each
            release so that offline fallback is not a schedule from three builds ago.
          </span>
          <span className="ml-auto flex gap-2">
            <a
              href="/api/tournaments/export?file=tournaments"
              className="rounded-md border border-accent-500/50 px-2.5 py-1 font-medium text-accent-100 hover:bg-accent-500/15"
            >
              Export tournaments.csv
            </a>
            <a
              href="/api/tournaments/export?file=prizes"
              className="rounded-md border border-accent-500/50 px-2.5 py-1 font-medium text-accent-100 hover:bg-accent-500/15"
            >
              Export tournament_prizes.csv
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
          <option value="all">Active + inactive</option>
          <option value="active">Active only</option>
          <option value="inactive">Inactive only</option>
        </select>
        <select
          value={stateFilter}
          onChange={(e) => setStateFilter(e.target.value as TournamentState | "all")}
          className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
        >
          <option value="all">All states</option>
          {STATES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Filter by slug, title, course…"
          className="w-60 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <span className="text-xs text-zinc-500">
          {filtered.length} of {rows.length}
        </span>
        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setCreating(true);
          }}
          className="ml-auto rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-accent-500"
        >
          + New tournament
        </button>
      </div>

      {/* Table */}
      <div className="mt-4 overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[1000px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="px-4 py-2.5 font-medium">Tournament</th>
              <th className="px-4 py-2.5 font-medium">State</th>
              <th className="px-4 py-2.5 font-medium">Course</th>
              <th className="px-4 py-2.5 text-right font-medium">Holes</th>
              <th className="px-4 py-2.5 text-right font-medium">Fee</th>
              <th className="px-4 py-2.5 font-medium">Prizes</th>
              <th className="px-4 py-2.5 font-medium">Window (UTC)</th>
              <th className="px-4 py-2.5 text-right font-medium">Entries</th>
              <th className="px-4 py-2.5 font-medium">Art</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((t) => {
              const state = deriveState(t.startAt, t.endAt, now);
              const pool = prizePoolSummary(t.bands);
              return (
                <tr
                  key={t.id}
                  onClick={() => {
                    setNotice(null);
                    setEditing(t);
                  }}
                  className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                    t.isActive ? "bg-surface-950" : "bg-surface-950/40 opacity-60"
                  }`}
                >
                  <td className="px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-zinc-200">{t.title}</span>
                      <KindBadge kind={t.kind} />
                      {!t.isActive && (
                        <span
                          className="rounded border border-zinc-600 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-zinc-400"
                          title="Hidden from the game — the schedule endpoint does not return it"
                        >
                          inactive
                        </span>
                      )}
                    </div>
                    <code className="text-[11px] text-zinc-600">{t.slug ?? "—"}</code>
                  </td>
                  <td className="px-4 py-2.5">
                    <StateBadge state={state} />
                  </td>
                  <td className="px-4 py-2.5 text-xs text-zinc-300">
                    {courseName(t.courseId)}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-400">
                    {expandHoleSet(t.holeSet).length || "—"}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                    {t.entryFeePts > 0 ? `${t.entryFeePts} RP` : "free"}
                  </td>
                  <td className="px-4 py-2.5 text-xs text-zinc-400">
                    {pool.places > 0 ? (
                      <>
                        <span className="font-semibold text-accent-400">
                          {pool.top.toLocaleString()}
                        </span>{" "}
                        top · {pool.places} places
                      </>
                    ) : (
                      <span className="text-amber-400">no bands</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                    {fmtDate(t.startAt)} → {fmtDate(t.endAt)}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                    {t.entryCount}
                    {t.entryCount > 0 && (
                      <span className="ml-1 text-[10px] text-zinc-600">
                        ({t.humanEntryCount} human)
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <ArtBadge layer={artLayer(t)} />
                  </td>
                </tr>
              );
            })}
            {filtered.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-10 text-center text-sm text-zinc-600">
                  No tournaments match the current filters.
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
