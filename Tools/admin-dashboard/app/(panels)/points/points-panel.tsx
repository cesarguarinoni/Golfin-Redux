"use client";

import { useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { DictKey } from "@/lib/i18n";
import type { PointsCurrency, PointsResponse } from "@/lib/types";

const PAGE_SIZE = 25;

export function PointsPanel() {
  const t = useT();
  const [data, setData] = useState<PointsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [currency, setCurrency] = useState<PointsCurrency | "all">("all");
  const [typeFilter, setTypeFilter] = useState<string>("all");
  const [emailQuery, setEmailQuery] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/api/points");
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as PointsResponse;
        if (!cancelled) setData(json);
      } catch (err) {
        if (!cancelled)
          setError(err instanceof Error ? err.message : t("points.loadFailed"));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const entries = useMemo(() => data?.entries ?? [], [data]);

  const types = useMemo(
    () => [...new Set(entries.map((e) => e.type))].sort(),
    [entries]
  );

  const filtered = useMemo(() => {
    const q = emailQuery.trim().toLowerCase();
    const from = dateFrom ? new Date(`${dateFrom}T00:00:00Z`).getTime() : null;
    const to = dateTo ? new Date(`${dateTo}T23:59:59.999Z`).getTime() : null;
    return entries.filter((e) => {
      if (currency !== "all" && e.currency !== currency) return false;
      if (typeFilter !== "all" && e.type !== typeFilter) return false;
      if (q && !e.userEmail.toLowerCase().includes(q)) return false;
      const t = new Date(e.createdAt).getTime();
      if (from !== null && t < from) return false;
      if (to !== null && t > to) return false;
      return true;
    });
  }, [entries, currency, typeFilter, emailQuery, dateFrom, dateTo]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, pageCount - 1);
  const pageRows = filtered.slice(
    safePage * PAGE_SIZE,
    (safePage + 1) * PAGE_SIZE
  );

  const resetPage = () => setPage(0);

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        {t("points.loadFailed")}: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        {t("points.loading")}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-5 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">{t("points.title")}</h1>
        <span className="text-xs text-zinc-500">
          {t("points.subtitle")}
        </span>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex overflow-hidden rounded-md border border-surface-700 text-xs">
          {(["all", "activity", "gift"] as const).map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => {
                setCurrency(c);
                resetPage();
              }}
              className={`px-3 py-1.5 font-medium transition ${
                currency === c
                  ? "bg-accent-600 text-white"
                  : "bg-surface-900 text-zinc-400 hover:bg-surface-800"
              }`}
            >
              {t(`points.currency.${c}` as DictKey)}
            </button>
          ))}
        </div>
        <select
          value={typeFilter}
          onChange={(e) => {
            setTypeFilter(e.target.value);
            resetPage();
          }}
          className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
        >
          <option value="all">{t("points.allTypes")}</option>
          {types.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <input
          type="search"
          value={emailQuery}
          onChange={(e) => {
            setEmailQuery(e.target.value);
            resetPage();
          }}
          placeholder={t("points.filterEmail")}
          className="w-56 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <label className="flex items-center gap-1.5 text-xs text-zinc-500">
          {t("points.from")}
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => {
              setDateFrom(e.target.value);
              resetPage();
            }}
            className="rounded-md border border-surface-700 bg-surface-900 px-2 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
          />
        </label>
        <label className="flex items-center gap-1.5 text-xs text-zinc-500">
          {t("points.to")}
          <input
            type="date"
            value={dateTo}
            onChange={(e) => {
              setDateTo(e.target.value);
              resetPage();
            }}
            className="rounded-md border border-surface-700 bg-surface-900 px-2 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
          />
        </label>
        <span className="ml-auto text-xs text-zinc-500">
          {filtered.length} {t("common.of")} {entries.length} {t("common.rows")}
        </span>
      </div>

      {/* Table */}
      <div className="mt-4 overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[820px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("points.col.when")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("points.col.user")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("points.col.type")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("points.col.amount")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("points.col.description")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("points.col.key")}</th>
            </tr>
          </thead>
          <tbody>
            {pageRows.map((e) => (
              <tr
                key={e.id}
                className="border-t border-surface-800 bg-surface-950"
              >
                <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                  {fmtDateTime(e.createdAt)}
                </td>
                <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">
                  {e.userEmail}
                </td>
                <td className="px-4 py-2.5 font-mono text-xs text-zinc-400">
                  {e.type}
                </td>
                <td
                  className={`px-4 py-2.5 text-right font-semibold tabular-nums ${
                    e.amount < 0 ? "text-red-400" : "text-accent-400"
                  }`}
                >
                  {e.amount > 0 ? "+" : ""}
                  {e.amount.toLocaleString()}
                  <span className="ml-1 text-[10px] font-medium uppercase text-zinc-500">
                    {e.currency}
                  </span>
                </td>
                <td className="max-w-[16rem] truncate px-4 py-2.5 text-xs text-zinc-300">
                  {e.description ?? <span className="text-zinc-600">—</span>}
                </td>
                <td className="px-4 py-2.5 font-mono text-[10px] text-zinc-600">
                  {e.idempotencyKey ? (
                    <span title={e.idempotencyKey}>
                      {e.idempotencyKey.slice(0, 8)}…
                    </span>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            ))}
            {pageRows.length === 0 && (
              <tr>
                <td
                  colSpan={6}
                  className="px-4 py-10 text-center text-sm text-zinc-600"
                >
                  {t("points.none")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {pageCount > 1 && (
        <div className="mt-3 flex items-center justify-end gap-2 text-xs text-zinc-400">
          <button
            type="button"
            disabled={safePage === 0}
            onClick={() => setPage(safePage - 1)}
            className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
          >
            {t("common.prev")}
          </button>
          <span>
            {t("common.page")} {safePage + 1} / {pageCount}
          </span>
          <button
            type="button"
            disabled={safePage >= pageCount - 1}
            onClick={() => setPage(safePage + 1)}
            className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
          >
            {t("common.next")}
          </button>
        </div>
      )}
    </div>
  );
}
