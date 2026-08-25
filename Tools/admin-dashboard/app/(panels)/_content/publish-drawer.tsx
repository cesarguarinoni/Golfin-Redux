"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { ContentProblem } from "@/lib/contentValidate";
import type { ContentCatalogSummary, ContentDiffResponse } from "@/lib/types";
import { DiffKindBadge } from "./badges";
import {
  fetchDiff,
  fetchVersionHistory,
  HISTORY_CAP,
  publishCatalog,
  rollbackCatalog,
  setCatalogEnabled,
  type VersionEntry,
} from "./client";

/**
 * ONE publish drawer, shared by all five panels.
 *
 * Three tabs: the diff, the version history, the kill switch.
 *
 * THE DIFF IS THE POINT (CONTENT_PIPELINE_PLAN.md §7.2 calls it the
 * highest-value guard in the system), so the publish button is not merely
 * *next to* the diff — it is unreachable until the diff has loaded AND the
 * operator has ticked "I have read the changes above". `canPublish` below is
 * the whole of that rule, in one place, so it cannot rot into a decoration.
 *
 * `z-40`: the language switcher is `z-30` and must be covered (§3.4).
 */

const DIFF_ROW_CAP = 200;

type Tab = "diff" | "history" | "switch";

export function PublishDrawer({
  catalog,
  summary,
  onClose,
  onChanged,
}: {
  catalog: string;
  summary: ContentCatalogSummary;
  onClose: () => void;
  /** Called after any mutation so the panel can refetch. */
  onChanged: (message: string) => void;
}) {
  const translate = useT();
  const [tab, setTab] = useState<Tab>("diff");

  const [diff, setDiff] = useState<ContentDiffResponse | null>(null);
  const [diffError, setDiffError] = useState<string | null>(null);

  const [acknowledged, setAcknowledged] = useState(false);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [problems, setProblems] = useState<ContentProblem[]>([]);
  const [warnings, setWarnings] = useState<ContentProblem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const [history, setHistory] = useState<VersionEntry[] | null>(null);

  const loadDiff = useCallback(async () => {
    setDiff(null);
    setDiffError(null);
    try {
      setDiff(await fetchDiff(catalog));
    } catch (err) {
      setDiffError(err instanceof Error ? err.message : String(err));
    }
  }, [catalog]);

  useEffect(() => {
    void loadDiff();
  }, [loadDiff]);

  useEffect(() => {
    if (tab !== "history" || history) return;
    void fetchVersionHistory(catalog, summary.publishedVersion).then(setHistory);
  }, [tab, history, catalog, summary.publishedVersion]);

  const changeCount = diff
    ? diff.counts.added + diff.counts.changed + diff.counts.deactivated + diff.counts.reactivated
    : 0;

  // The gate, in one expression. A diff that has not loaded, an empty diff, an
  // unticked box, or an in-flight request all mean "not yet".
  const canPublish = Boolean(diff) && changeCount > 0 && acknowledged && !busy;

  async function doPublish() {
    setBusy(true);
    setError(null);
    setProblems([]);
    setWarnings([]);
    try {
      const res = await publishCatalog(catalog, note);
      setWarnings(res.warnings ?? []);
      setDone(translate("cp.published", { catalog, version: res.version }));
      setAcknowledged(false);
      setNote("");
      await loadDiff();
      setHistory(null);
      onChanged(res.message);
    } catch (err) {
      const e = err as Error & { problems?: ContentProblem[] };
      setError(e.message);
      setProblems(e.problems ?? []);
    } finally {
      setBusy(false);
    }
  }

  async function doRollback(version: number) {
    const next = summary.publishedVersion + 1;
    if (!window.confirm(translate("cp.history.confirm", { version, next }))) return;
    setBusy(true);
    setError(null);
    try {
      const res = await rollbackCatalog(catalog, version);
      setDone(translate("cp.history.done", { from: version, version: res.version }));
      setHistory(null);
      await loadDiff();
      onChanged(res.message);
    } catch (err) {
      setError(`${translate("cp.history.failed")}: ${err instanceof Error ? err.message : err}`);
    } finally {
      setBusy(false);
    }
  }

  async function doToggleEnabled() {
    setBusy(true);
    setError(null);
    try {
      const res = await setCatalogEnabled(catalog, !summary.isEnabled);
      setDone(res.message);
      onChanged(res.message);
    } catch (err) {
      setError(`${translate("cp.enabled.failed")}: ${err instanceof Error ? err.message : err}`);
    } finally {
      setBusy(false);
    }
  }

  const shown = diff ? diff.entries.slice(0, DIFF_ROW_CAP) : [];

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      <button
        type="button"
        aria-label={translate("common.close")}
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      <div className="absolute right-0 top-0 flex h-full w-full max-w-3xl flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        {/* `pt-10`, not `py-4`: the mode banner is `sticky top-0 z-50` and this
            drawer is `z-40`, so the banner paints OVER the drawer's first 29px.
            Measured 2026-08-25: with `py-4` the <h2> top lands at y=16 and is
            clipped by 13px. The inherited Tournaments/Banners/Notices/Users
            editors all have the same overlap — reported rather than changed
            here, since fixing four other panels is outside this task. */}
        <header className="border-b border-surface-800 px-5 pb-4 pt-10">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h2 className="truncate text-base font-semibold text-zinc-100">
                {translate("cp.title", { catalog })}
              </h2>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-zinc-500">
                <code>{catalog}</code>
                <span>·</span>
                <span>{translate("c.version", { n: summary.publishedVersion })}</span>
                {diff?.mock && (
                  <span className="rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold text-yellow-300 ring-1 ring-yellow-600/40">
                    MOCK
                  </span>
                )}
              </div>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="shrink-0 rounded-md border border-surface-700 px-2.5 py-1 text-xs text-zinc-400 hover:bg-surface-800"
            >
              {translate("common.close")}
            </button>
          </div>

          <nav className="mt-3 flex gap-1">
            {(["diff", "history", "switch"] as const).map((id) => (
              <button
                key={id}
                type="button"
                onClick={() => setTab(id)}
                className={`whitespace-nowrap rounded-md px-3 py-1.5 text-xs font-medium transition ${
                  tab === id
                    ? "bg-surface-700 text-zinc-100"
                    : "text-zinc-500 hover:bg-surface-800 hover:text-zinc-300"
                }`}
              >
                {translate(`cp.tab.${id}`)}
              </button>
            ))}
          </nav>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {done && (
            <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
              {done}
            </p>
          )}
          {error && (
            <div className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
              {error}
            </div>
          )}

          {tab === "diff" && (
            <>
              {problems.length > 0 && (
                <div className="mb-4 rounded-lg border border-red-500/50 bg-red-500/10 p-3">
                  <h3 className="text-xs font-bold text-red-300">
                    {translate("cp.problems.title", { n: problems.length })}
                  </h3>
                  <p className="mt-1 text-[11px] text-red-200/80">{translate("cp.problems.body")}</p>
                  <ul className="mt-2 space-y-1">
                    {problems.map((problem, i) => (
                      <li key={`${problem.rowId}-${problem.column}-${i}`} className="text-[11px] text-red-200">
                        <code className="text-red-300">
                          {problem.rowId ?? "—"}
                          {problem.column ? `/${problem.column}` : ""}
                        </code>{" "}
                        {problem.message}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {warnings.length > 0 && (
                <div className="mb-4 rounded-lg border border-amber-500/40 bg-amber-500/10 p-3">
                  <h3 className="text-xs font-bold text-amber-300">
                    {translate("cp.warnings.title", { n: warnings.length })}
                  </h3>
                  <p className="mt-1 text-[11px] text-amber-200/80">{translate("cp.warnings.body")}</p>
                  <ul className="mt-2 space-y-1">
                    {warnings.map((problem, i) => (
                      <li key={`${problem.rowId}-${problem.column}-${i}`} className="text-[11px] text-amber-200">
                        <code className="text-amber-300">
                          {problem.rowId ?? "—"}
                          {problem.column ? `/${problem.column}` : ""}
                        </code>{" "}
                        {problem.message}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {diffError && (
                <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                  {translate("cp.diff.failed")}: {diffError}
                </div>
              )}
              {!diff && !diffError && (
                <p className="py-8 text-center text-sm text-zinc-500">{translate("cp.diff.loading")}</p>
              )}

              {diff && (
                <>
                  <div className="mb-3 flex flex-wrap items-center gap-3 text-xs">
                    {(["added", "changed", "deactivated", "reactivated"] as const).map((kind) => (
                      <span key={kind} className="flex items-center gap-1.5">
                        <span className="tabular-nums font-semibold text-zinc-200">
                          {diff.counts[kind]}
                        </span>
                        <DiffKindBadge kind={kind} />
                      </span>
                    ))}
                  </div>

                  {diff.counts.deactivated > 0 && (
                    <p className="mb-3 rounded-md border border-red-500/30 bg-red-500/5 px-3 py-2 text-[11px] text-red-200/90">
                      {translate("cp.diff.deactivatedNote")}
                    </p>
                  )}

                  {changeCount === 0 ? (
                    <p className="rounded-md border border-surface-700 bg-surface-850 px-3 py-6 text-center text-sm text-zinc-500">
                      {translate("cp.diff.none")}
                    </p>
                  ) : (
                    <>
                      {diff.entries.length > DIFF_ROW_CAP && (
                        <p className="mb-2 text-[11px] text-amber-300">
                          {translate("cp.diff.truncated", {
                            shown: DIFF_ROW_CAP,
                            total: diff.entries.length,
                          })}
                        </p>
                      )}
                      <div className="space-y-2">
                        {shown.map((entry) => (
                          <div
                            key={entry.rowId}
                            className="overflow-hidden rounded-lg border border-surface-800 bg-surface-950"
                          >
                            <div className="flex items-center gap-2 border-b border-surface-800 bg-surface-900 px-3 py-1.5">
                              <DiffKindBadge kind={entry.kind} />
                              <code className="truncate text-[11px] text-zinc-300">{entry.rowId}</code>
                            </div>
                            <table className="w-full text-left text-[11px]">
                              <thead className="text-zinc-600">
                                <tr>
                                  <th className="whitespace-nowrap px-3 py-1 font-medium">
                                    {translate("cp.diff.col.field")}
                                  </th>
                                  <th className="whitespace-nowrap px-3 py-1 font-medium">
                                    {translate("cp.diff.col.before")}
                                  </th>
                                  <th className="whitespace-nowrap px-3 py-1 font-medium">
                                    {translate("cp.diff.col.after")}
                                  </th>
                                </tr>
                              </thead>
                              <tbody>
                                {entry.fields.map((field) => (
                                  <tr key={field.column} className="border-t border-surface-800/60">
                                    <td className="whitespace-nowrap px-3 py-1 font-mono text-zinc-400">
                                      {field.column}
                                    </td>
                                    <td className="px-3 py-1 text-red-300/80">
                                      <span className="break-all line-through decoration-red-500/40">
                                        {field.before ?? "—"}
                                      </span>
                                    </td>
                                    <td className="px-3 py-1 break-all text-emerald-300">
                                      {field.after ?? "—"}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        ))}
                      </div>
                    </>
                  )}
                </>
              )}
            </>
          )}

          {tab === "history" && (
            <>
              <p className="mb-3 rounded-md border border-sky-500/40 bg-sky-500/10 px-3 py-2 text-[11px] text-sky-200">
                {translate("cp.history.forward", {
                  example: Math.max(1, summary.publishedVersion - 1),
                })}
              </p>
              <p className="mb-3 text-[11px] text-zinc-500">{translate("cp.history.source")}</p>
              {summary.publishedVersion > HISTORY_CAP && (
                <p className="mb-3 text-[11px] text-amber-300">
                  {translate("cp.history.capped", {
                    cap: HISTORY_CAP,
                    oldest: summary.publishedVersion - HISTORY_CAP + 1,
                  })}
                </p>
              )}

              {!history && <p className="py-6 text-center text-sm text-zinc-500">{translate("common.loading")}</p>}

              {history && (
                <table className="w-full text-left text-xs">
                  <thead className="text-zinc-600">
                    <tr>
                      <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                        {translate("cp.history.col.version")}
                      </th>
                      <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                        {translate("cp.history.col.when")}
                      </th>
                      <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                        {translate("cp.history.col.who")}
                      </th>
                      <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                        {translate("cp.history.col.what")}
                      </th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {history.map((entry) => {
                      const current = entry.version === summary.publishedVersion;
                      return (
                        <tr key={entry.version} className="border-t border-surface-800">
                          <td className="whitespace-nowrap px-2 py-2 font-semibold tabular-nums text-zinc-200">
                            v{entry.version}
                            {current && (
                              <span className="ml-1.5 whitespace-nowrap rounded border border-accent-500/40 bg-accent-500/10 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                                {translate("cp.history.current")}
                              </span>
                            )}
                          </td>
                          <td className="whitespace-nowrap px-2 py-2 text-zinc-400">
                            {entry.detailed ? fmtDateTime(entry.at) : "—"}
                          </td>
                          <td className="px-2 py-2 text-zinc-400">
                            <span className="block max-w-[14rem] truncate">{entry.by ?? "—"}</span>
                          </td>
                          <td className="px-2 py-2 text-zinc-400">
                            {!entry.detailed && (
                              <span className="text-zinc-600">{translate("cp.history.noDetail")}</span>
                            )}
                            {entry.detailed && entry.restoredFrom !== null && (
                              <span className="text-sky-300">
                                {translate("cp.history.rollbackOf", { from: entry.restoredFrom })}
                              </span>
                            )}
                            {entry.detailed && entry.restoredFrom === null && entry.counts && (
                              <span className="tabular-nums">
                                +{entry.counts.added} ~{entry.counts.changed} −{entry.counts.deactivated}
                              </span>
                            )}
                            {entry.note && (
                              <span className="ml-1.5 italic text-zinc-500">“{entry.note}”</span>
                            )}
                          </td>
                          <td className="px-2 py-2 text-right">
                            {!current && (
                              <button
                                type="button"
                                disabled={busy}
                                onClick={() => void doRollback(entry.version)}
                                className="whitespace-nowrap rounded-md border border-surface-700 px-2 py-1 text-[11px] text-zinc-300 transition hover:bg-surface-800 disabled:opacity-40"
                              >
                                {translate("cp.history.restore")}
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </>
          )}

          {tab === "switch" && (
            <div className="rounded-lg border border-surface-800 bg-surface-950 p-4">
              <h3 className="text-sm font-semibold text-zinc-200">{translate("cp.enabled.title")}</h3>
              <p
                className={`mt-2 text-xs ${summary.isEnabled ? "text-accent-300" : "text-red-300"}`}
              >
                {translate(summary.isEnabled ? "cp.enabled.on" : "cp.enabled.off", { catalog })}
              </p>
              <button
                type="button"
                disabled={busy}
                onClick={() => void doToggleEnabled()}
                className={`mt-4 rounded-md px-3 py-1.5 text-xs font-semibold text-white transition disabled:opacity-40 ${
                  summary.isEnabled ? "bg-red-600 hover:bg-red-500" : "bg-accent-600 hover:bg-accent-500"
                }`}
              >
                {translate(summary.isEnabled ? "cp.enabled.disable" : "cp.enabled.enable")}
              </button>
            </div>
          )}
        </div>

        {tab === "diff" && (
          <footer className="border-t border-surface-800 bg-surface-900 px-5 py-4">
            <div className="rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2">
              <p className="text-xs font-semibold text-amber-200">{translate("cp.confirm.headline")}</p>
              <p className="mt-1 text-[11px] text-amber-200/80">
                {translate("cp.confirm.body", { catalog, from: summary.publishedVersion })}
              </p>
              <label className="mt-2 flex items-center gap-2 text-xs text-amber-100">
                <input
                  type="checkbox"
                  checked={acknowledged}
                  disabled={!diff || changeCount === 0}
                  onChange={(e) => setAcknowledged(e.target.checked)}
                  className="h-3.5 w-3.5 accent-amber-500 disabled:opacity-40"
                />
                {translate("cp.confirm.check")}
              </label>
            </div>

            <div className="mt-3 flex items-end gap-3">
              <label className="flex-1 text-[11px] text-zinc-500">
                {translate("cp.note.label")}
                <input
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder={translate("cp.note.placeholder")}
                  className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
                />
              </label>
              <button
                type="button"
                disabled={!canPublish}
                onClick={() => void doPublish()}
                className="shrink-0 rounded-md bg-accent-600 px-4 py-2 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:cursor-not-allowed disabled:bg-surface-700 disabled:text-zinc-500"
              >
                {busy ? translate("cp.publishing") : translate("cp.publish")}
              </button>
            </div>
          </footer>
        )}
      </div>
    </div>
  );
}
