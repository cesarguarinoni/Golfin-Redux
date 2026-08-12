"use client";

import { useEffect, useState } from "react";
import { ProviderBadge } from "@/components/ProviderBadge";
import type { AdminUserRow, UserDetailResponse } from "@/lib/types";
import { fmtDateTime } from "./users-panel";

type Tab = "transactions" | "activities";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {label}
      </div>
      <div className="mt-0.5 text-sm text-zinc-200">{children}</div>
    </div>
  );
}

export function UserDrawer({
  user,
  onClose,
}: {
  user: AdminUserRow;
  onClose: () => void;
}) {
  const [detail, setDetail] = useState<UserDetailResponse | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("transactions");

  useEffect(() => {
    let cancelled = false;
    setDetail(null);
    setDetailError(null);
    (async () => {
      try {
        const res = await fetch(`/api/users/${user.id}`);
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as UserDetailResponse;
        if (!cancelled) setDetail(json);
      } catch (err) {
        if (!cancelled)
          setDetailError(
            err instanceof Error ? err.message : "Failed to load detail"
          );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user.id]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      {/* Overlay */}
      <button
        type="button"
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      {/* Slide-over */}
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        <header className="flex items-start justify-between border-b border-surface-800 px-5 py-4">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-semibold text-zinc-100">
                {user.displayName ?? user.email}
              </h2>
              {user.providers.map((p) => (
                <ProviderBadge key={p} provider={p} />
              ))}
            </div>
            <div className="mt-0.5 font-mono text-xs text-zinc-500">
              {user.email}
            </div>
            <div className="mt-0.5 font-mono text-[10px] text-zinc-600">
              {user.id}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 text-zinc-500 transition hover:bg-surface-800 hover:text-zinc-200"
            aria-label="Close drawer"
          >
            <svg
              viewBox="0 0 24 24"
              className="h-5 w-5"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              aria-hidden
            >
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {/* RP summary */}
          <section className="rounded-lg border border-surface-800 bg-surface-950 p-4">
            <div className="flex items-end justify-between">
              <div>
                <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                  Reward Points (total_points)
                </div>
                <div className="mt-1 text-3xl font-bold tabular-nums text-accent-400">
                  {user.totalPoints.toLocaleString()}
                  <span className="ml-1.5 text-sm font-semibold text-zinc-500">
                    RP
                  </span>
                </div>
              </div>
              <div className="text-right text-xs text-zinc-400">
                <div>
                  activity{" "}
                  <span className="font-semibold tabular-nums text-zinc-200">
                    {user.activityPts.toLocaleString()}
                  </span>
                </div>
                <div className="mt-0.5">
                  gift{" "}
                  <span className="font-semibold tabular-nums text-zinc-200">
                    {user.giftPts.toLocaleString()}
                  </span>
                </div>
              </div>
            </div>
          </section>

          {/* Profile fields */}
          <section className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-3">
            <Field label="Avatar level">{user.avatarLevel}</Field>
            <Field label="Avatar XP">{user.avatarXp.toLocaleString()}</Field>
            <Field label="Trust level">{user.trustLevel ?? "—"}</Field>
            <Field label="Followers">{user.followersCount}</Field>
            <Field label="Following">{user.followingCount}</Field>
            <Field label="Badges">{user.badgesCount}</Field>
          </section>

          {/* Auth identity + timestamps */}
          <section className="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-2">
            <Field label="Providers">{user.providers.join(", ")}</Field>
            <Field label="Email confirmed">
              {user.emailConfirmedAt ? (
                <span className="text-accent-400">
                  ✓ {fmtDateTime(user.emailConfirmedAt)}
                </span>
              ) : (
                <span className="text-red-400">✗ unconfirmed</span>
              )}
            </Field>
            <Field label="Created">{fmtDateTime(user.createdAt)}</Field>
            <Field label="Last sign-in">{fmtDateTime(user.lastSignInAt)}</Field>
            <Field label="Banned until">
              {user.bannedUntil ? (
                <span className="text-red-400">
                  {fmtDateTime(user.bannedUntil)}
                </span>
              ) : (
                "—"
              )}
            </Field>
          </section>

          {/* Tabs */}
          <div className="mt-5 flex gap-1 border-b border-surface-800">
            {(
              [
                ["transactions", "Points ledger"],
                ["activities", "Activities"],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                onClick={() => setTab(key)}
                className={`rounded-t-md px-3 py-2 text-xs font-medium transition ${
                  tab === key
                    ? "border-b-2 border-accent-500 text-zinc-100"
                    : "text-zinc-500 hover:text-zinc-300"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <div className="py-3">
            {detailError && (
              <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                {detailError}
              </p>
            )}
            {!detail && !detailError && (
              <p className="py-6 text-center text-xs text-zinc-600">Loading…</p>
            )}
            {detail && tab === "transactions" && (
              <ul className="space-y-2">
                {detail.transactions.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    No points transactions.
                  </li>
                )}
                {detail.transactions.map((t) => (
                  <li
                    key={t.id}
                    className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2"
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-xs text-zinc-400">
                        {t.type}
                      </span>
                      <span
                        className={`text-sm font-semibold tabular-nums ${
                          t.amount < 0 ? "text-red-400" : "text-accent-400"
                        }`}
                      >
                        {t.amount > 0 ? "+" : ""}
                        {t.amount.toLocaleString()}
                        <span className="ml-1 text-[10px] font-medium uppercase text-zinc-500">
                          {t.currency}
                        </span>
                      </span>
                    </div>
                    {t.description && (
                      <div className="mt-0.5 text-xs text-zinc-300">
                        {t.description}
                      </div>
                    )}
                    <div className="mt-0.5 text-[10px] text-zinc-600">
                      {fmtDateTime(t.createdAt)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
            {detail && tab === "activities" && (
              <ul className="space-y-2">
                {detail.activities.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    No recorded activities.
                  </li>
                )}
                {detail.activities.map((a) => (
                  <li
                    key={a.id}
                    className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2"
                  >
                    <div className="text-xs text-zinc-300">{a.label}</div>
                    <div className="mt-0.5 text-[10px] text-zinc-600">
                      {fmtDateTime(a.createdAt)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <footer className="border-t border-surface-800 px-5 py-2.5 text-center text-[10px] uppercase tracking-widest text-zinc-600">
          Read-only — v1
        </footer>
      </div>
    </div>
  );
}
