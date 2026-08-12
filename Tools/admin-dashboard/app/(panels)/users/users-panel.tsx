"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ProviderBadge } from "@/components/ProviderBadge";
import { fmtDate } from "@/lib/format";
import type {
  AdminUserRow,
  AuthProvider,
  GamePointAction,
  UsersResponse,
} from "@/lib/types";
import { UserDrawer } from "./user-drawer";

const PAGE_SIZE = 25;
const PROVIDERS: readonly AuthProvider[] = ["email", "google", "apple"];

function isBanned(u: AdminUserRow): boolean {
  return u.bannedUntil !== null && new Date(u.bannedUntil) > new Date();
}

function StatCard({
  label,
  value,
  sub,
}: {
  label: string;
  value: string;
  sub?: string;
}) {
  return (
    <div className="rounded-lg border border-surface-800 bg-surface-900 px-4 py-3">
      <div className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">
        {label}
      </div>
      <div className="mt-1 text-2xl font-semibold text-zinc-100">{value}</div>
      {sub && <div className="mt-0.5 text-xs text-zinc-500">{sub}</div>}
    </div>
  );
}

function CatalogCard({ catalog }: { catalog: GamePointAction[] }) {
  if (catalog.length === 0) return null;
  return (
    <section className="mt-8 rounded-lg border border-surface-800 bg-surface-900 p-4">
      <h2 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
        Economy catalog{" "}
        <span className="font-normal normal-case text-zinc-600">
          (game_point_actions, read-only)
        </span>
      </h2>
      <table className="mt-3 w-full text-left text-xs">
        <thead>
          <tr className="text-zinc-500">
            <th className="pb-2 pr-4 font-medium">action</th>
            <th className="pb-2 pr-4 font-medium">pts</th>
            <th className="pb-2 pr-4 font-medium">max / event</th>
            <th className="pb-2 pr-4 font-medium">daily cap</th>
            <th className="pb-2 font-medium">once per user</th>
          </tr>
        </thead>
        <tbody className="text-zinc-300">
          {catalog.map((c) => (
            <tr key={c.action} className="border-t border-surface-800">
              <td className="py-1.5 pr-4 font-mono">{c.action}</td>
              <td className="py-1.5 pr-4">{c.pts ?? "—"}</td>
              <td className="py-1.5 pr-4">{c.maxPerEvent ?? "—"}</td>
              <td className="py-1.5 pr-4">{c.dailyCap ?? "—"}</td>
              <td className="py-1.5">{c.oncePerUser ? "yes" : "no"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

export function UsersPanel() {
  const [data, setData] = useState<UsersResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [providerFilter, setProviderFilter] = useState<AuthProvider | "all">(
    "all"
  );
  const [unconfirmedOnly, setUnconfirmedOnly] = useState(false);
  const [bannedOnly, setBannedOnly] = useState(false);
  const [page, setPage] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/users");
      if (!res.ok) {
        const body = (await res.json().catch(() => null)) as {
          error?: string;
        } | null;
        throw new Error(body?.error ?? `Request failed (${res.status})`);
      }
      const json = (await res.json()) as UsersResponse;
      setData(json);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load users");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const users = useMemo(() => data?.users ?? [], [data]);
  // Drawer re-renders from the freshest row after mutations reload the list.
  const selected = useMemo(
    () => users.find((u) => u.id === selectedId) ?? null,
    [users, selectedId]
  );

  const stats = useMemo(() => {
    const total = users.length;
    const weekAgo = Date.now() - 7 * 24 * 3600 * 1000;
    const newLast7 = users.filter(
      (u) => new Date(u.createdAt).getTime() >= weekAgo
    ).length;
    const confirmed = users.filter((u) => u.emailConfirmedAt !== null).length;
    const byProvider = PROVIDERS.map((p) => ({
      provider: p,
      count: users.filter((u) => u.providers.includes(p)).length,
    }));
    return {
      total,
      newLast7,
      confirmedPct: total > 0 ? Math.round((confirmed / total) * 100) : 0,
      byProvider,
    };
  }, [users]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return users.filter((u) => {
      if (
        q &&
        !u.email.toLowerCase().includes(q) &&
        !(u.displayName ?? "").toLowerCase().includes(q)
      )
        return false;
      if (providerFilter !== "all" && !u.providers.includes(providerFilter))
        return false;
      if (unconfirmedOnly && u.emailConfirmedAt !== null) return false;
      if (bannedOnly && !isBanned(u)) return false;
      return true;
    });
  }, [users, search, providerFilter, unconfirmedOnly, bannedOnly]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, pageCount - 1);
  const pageRows = filtered.slice(
    safePage * PAGE_SIZE,
    (safePage + 1) * PAGE_SIZE
  );

  function resetPage() {
    setPage(0);
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        Failed to load users: {error}
      </div>
    );
  }

  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        Loading users…
      </div>
    );
  }

  return (
    <div>
      <div className="mb-5 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">Users</h1>
        <span className="text-xs text-zinc-500">
          read-only · RP = total_points
        </span>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Total users" value={String(stats.total)} />
        <StatCard label="New (last 7 days)" value={String(stats.newLast7)} />
        <StatCard label="Confirmed" value={`${stats.confirmedPct}%`} />
        <StatCard
          label="Providers"
          value={stats.byProvider
            .filter((p) => p.count > 0)
            .map((p) => `${p.count}`)
            .join(" / ")}
          sub={stats.byProvider
            .filter((p) => p.count > 0)
            .map((p) => p.provider)
            .join(" / ")}
        />
      </div>

      {/* Search + filters */}
      <div className="mt-6 flex flex-wrap items-center gap-3">
        <input
          type="search"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            resetPage();
          }}
          placeholder="Search email or name…"
          className="w-64 rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <div className="flex overflow-hidden rounded-md border border-surface-700 text-xs">
          {(["all", ...PROVIDERS] as const).map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => {
                setProviderFilter(p as AuthProvider | "all");
                resetPage();
              }}
              className={`px-3 py-1.5 font-medium capitalize transition ${
                providerFilter === p
                  ? "bg-accent-600 text-white"
                  : "bg-surface-900 text-zinc-400 hover:bg-surface-800"
              }`}
            >
              {p}
            </button>
          ))}
        </div>
        <label className="flex cursor-pointer items-center gap-1.5 text-xs text-zinc-400">
          <input
            type="checkbox"
            checked={unconfirmedOnly}
            onChange={(e) => {
              setUnconfirmedOnly(e.target.checked);
              resetPage();
            }}
            className="h-3.5 w-3.5 accent-emerald-500"
          />
          Unconfirmed only
        </label>
        <label className="flex cursor-pointer items-center gap-1.5 text-xs text-zinc-400">
          <input
            type="checkbox"
            checked={bannedOnly}
            onChange={(e) => {
              setBannedOnly(e.target.checked);
              resetPage();
            }}
            className="h-3.5 w-3.5 accent-emerald-500"
          />
          Banned only
        </label>
        <span className="ml-auto text-xs text-zinc-500">
          {filtered.length} of {users.length} users
        </span>
      </div>

      {/* Table */}
      <div className="mt-4 overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="px-4 py-2.5 font-medium">Email</th>
              <th className="px-4 py-2.5 font-medium">Username</th>
              <th className="px-4 py-2.5 font-medium">Provider</th>
              <th className="px-4 py-2.5 text-center font-medium">Confirmed</th>
              <th className="px-4 py-2.5 font-medium">Created</th>
              <th className="px-4 py-2.5 font-medium">Last sign-in</th>
              <th className="px-4 py-2.5 text-right font-medium">RP</th>
            </tr>
          </thead>
          <tbody>
            {pageRows.map((u) => (
              <tr
                key={u.id}
                onClick={() => setSelectedId(u.id)}
                className="cursor-pointer border-t border-surface-800 bg-surface-950 transition hover:bg-surface-850"
              >
                <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">
                  {u.email}
                  {isBanned(u) && (
                    <span className="ml-2 rounded bg-red-500/20 px-1.5 py-0.5 text-[10px] font-semibold text-red-300">
                      BANNED
                    </span>
                  )}
                </td>
                <td className="px-4 py-2.5 text-zinc-200">
                  {u.displayName ?? <span className="text-zinc-600">—</span>}
                </td>
                <td className="px-4 py-2.5">
                  <span className="flex gap-1">
                    {u.providers.map((p) => (
                      <ProviderBadge key={p} provider={p} />
                    ))}
                  </span>
                </td>
                <td className="px-4 py-2.5 text-center">
                  {u.emailConfirmedAt ? (
                    <span className="text-accent-400" title={u.emailConfirmedAt}>
                      ✓
                    </span>
                  ) : (
                    <span className="text-red-400">✗</span>
                  )}
                </td>
                <td className="px-4 py-2.5 text-xs text-zinc-400">
                  {fmtDate(u.createdAt)}
                </td>
                <td className="px-4 py-2.5 text-xs text-zinc-400">
                  {fmtDate(u.lastSignInAt)}
                </td>
                <td className="px-4 py-2.5 text-right font-semibold tabular-nums text-zinc-100">
                  {u.totalPoints.toLocaleString()}
                </td>
              </tr>
            ))}
            {pageRows.length === 0 && (
              <tr>
                <td
                  colSpan={7}
                  className="px-4 py-10 text-center text-sm text-zinc-600"
                >
                  No users match the current filters.
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
            ← Prev
          </button>
          <span>
            Page {safePage + 1} / {pageCount}
          </span>
          <button
            type="button"
            disabled={safePage >= pageCount - 1}
            onClick={() => setPage(safePage + 1)}
            className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
          >
            Next →
          </button>
        </div>
      )}

      <CatalogCard catalog={data.catalog} />

      {selected && data && (
        <UserDrawer
          user={selected}
          mock={data.mock}
          onClose={() => setSelectedId(null)}
          onMutated={load}
        />
      )}
    </div>
  );
}
