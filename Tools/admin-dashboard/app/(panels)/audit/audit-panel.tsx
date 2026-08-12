"use client";

import { useEffect, useMemo, useState } from "react";
import { fmtDateTime } from "@/lib/format";
import type { AuditResponse } from "@/lib/types";

const PAGE_SIZE = 25;

function JsonCell({ value }: { value: unknown }) {
  if (value === null || value === undefined) {
    return <span className="text-zinc-700">—</span>;
  }
  const full = JSON.stringify(value);
  const short = full.length > 60 ? `${full.slice(0, 60)}…` : full;
  return (
    <span className="font-mono text-[10px] text-zinc-400" title={full}>
      {short}
    </span>
  );
}

export function AuditPanel() {
  const [data, setData] = useState<AuditResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/api/audit");
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as AuditResponse;
        if (!cancelled) setData(json);
      } catch (err) {
        if (!cancelled)
          setError(
            err instanceof Error ? err.message : "Failed to load audit log"
          );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const entries = useMemo(() => data?.entries ?? [], [data]);
  const pageCount = Math.max(1, Math.ceil(entries.length / PAGE_SIZE));
  const safePage = Math.min(page, pageCount - 1);
  const pageRows = entries.slice(
    safePage * PAGE_SIZE,
    (safePage + 1) * PAGE_SIZE
  );

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        Failed to load audit log: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        Loading audit log…
      </div>
    );
  }

  return (
    <div>
      <div className="mb-5 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">Audit Log</h1>
        <span className="text-xs text-zinc-500">
          admin_audit_log · read-only viewer
        </span>
      </div>

      {entries.length === 0 ? (
        <div className="rounded-lg border border-surface-800 bg-surface-900 px-6 py-12 text-center">
          <p className="text-sm text-zinc-400">No audit entries yet.</p>
          <p className="mx-auto mt-2 max-w-md text-xs leading-relaxed text-zinc-600">
            Every admin mutation (username edits, RP adjustments, bans, email
            confirmations, deletions…) writes one row to{" "}
            <span className="font-mono">public.admin_audit_log</span>. This
            panel fills up as soon as mutations run against the live database —
            in mock mode, entries appear here after you perform mock mutations
            in the Users panel.
          </p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-surface-800">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="bg-surface-900 text-xs text-zinc-500">
                <tr>
                  <th className="px-4 py-2.5 font-medium">When</th>
                  <th className="px-4 py-2.5 font-medium">Admin</th>
                  <th className="px-4 py-2.5 font-medium">Action</th>
                  <th className="px-4 py-2.5 font-medium">Target user</th>
                  <th className="px-4 py-2.5 font-medium">Table</th>
                  <th className="px-4 py-2.5 font-medium">Before</th>
                  <th className="px-4 py-2.5 font-medium">After</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((e) => (
                  <tr
                    key={e.id}
                    className="border-t border-surface-800 bg-surface-950 align-top"
                  >
                    <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                      {fmtDateTime(e.at)}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">
                      {e.adminEmail}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="rounded bg-surface-800 px-1.5 py-0.5 font-mono text-[10px] text-accent-400">
                        {e.action}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[10px] text-zinc-500">
                      {e.targetUser ? (
                        <span title={e.targetUser}>
                          {e.targetUser.slice(0, 8)}…
                        </span>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-xs text-zinc-400">
                      {e.tableName ?? "—"}
                    </td>
                    <td className="max-w-[14rem] truncate px-4 py-2.5">
                      <JsonCell value={e.before} />
                    </td>
                    <td className="max-w-[14rem] truncate px-4 py-2.5">
                      <JsonCell value={e.after} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

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
        </>
      )}
    </div>
  );
}
