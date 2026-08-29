"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { hasServerMirror } from "@/lib/contentView";
import { fmtDateTime } from "@/lib/format";
import type { ContentProblem } from "@/lib/contentValidate";
import type {
  ContentCatalogSummary,
  ContentDiffResponse,
  ContentVersionsResponse,
} from "@/lib/types";
import { DiffKindBadge } from "./badges";
import {
  fetchDiff,
  fetchVersions,
  publishCatalog,
  rollbackCatalog,
  setCatalogEnabled,
  setGlobalContentEnabled,
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
/** Versions per page. The list is PAGED, not capped — v1 is always reachable
 *  by paging to the end, which is the whole point of §2. */
const HISTORY_PAGE = 25;

type Tab = "diff" | "history" | "switch";

export function PublishDrawer({
  catalog,
  summary,
  globalEnabled,
  onClose,
  onChanged,
}: {
  catalog: string;
  summary: ContentCatalogSummary;
  /**
   * `content_settings.content_enabled` — the GLOBAL kill switch (PLAN §7.4).
   *
   * It lives in every catalog's drawer on purpose, next to that catalog's own switch. The two
   * were once the same switch, and disabling one catalog silently reverted all seven on every
   * client (content_kill_switch_and_order). Showing them side by side, each saying plainly what
   * it reaches, is the cheapest guard against that confusion coming back through the UI.
   */
  globalEnabled: boolean;
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

  const [history, setHistory] = useState<ContentVersionsResponse | null>(null);
  const [historyPage, setHistoryPage] = useState(1);
  const [historyError, setHistoryError] = useState<string | null>(null);

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

  const loadHistory = useCallback(async () => {
    setHistoryError(null);
    try {
      setHistory(await fetchVersions(catalog, { page: historyPage, limit: HISTORY_PAGE }));
    } catch (err) {
      setHistory(null);
      setHistoryError(err instanceof Error ? err.message : String(err));
    }
  }, [catalog, historyPage]);

  useEffect(() => {
    if (tab !== "history") return;
    void loadHistory();
  }, [tab, loadHistory]);

  const changeCount = diff
    ? diff.counts.added + diff.counts.changed + diff.counts.deactivated + diff.counts.reactivated
    : 0;

  /**
   * A MIRRORED catalog may be published with NOTHING CHANGED, and that is the
   * only way to fill a server mirror that is empty.
   *
   * The case that forced this: `seed_from_csv.py --apply` writes `content_rows`
   * and stamps `published_version = 1`, but it does NOT write mirrors — only a
   * publish does. So a freshly-seeded `mission_tiers` reads v1, has an empty
   * diff, and its `golfin_mission_tier_bonus` mirror has zero rows. The claim
   * path then finds no tier row and silently pays no completion bonus, and the
   * operator has no button that would fix it. A no-op publish is a re-sync.
   */
  const mirrored = hasServerMirror(catalog);

  // The gate, in one expression. A diff that has not loaded, an unticked box, or
  // an in-flight request all mean "not yet"; an EMPTY diff means "not yet" too,
  // unless publishing would still do something — which, for a mirrored catalog,
  // it would.
  const canPublish = Boolean(diff) && (changeCount > 0 || mirrored) && acknowledged && !busy;

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
      setHistoryPage(1);
      await loadHistory();
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
      setHistoryPage(1);
      await loadHistory();
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

  /**
   * The GLOBAL kill. CONFIRMED on the way OFF, one click on the way back ON.
   *
   * Asymmetric on purpose: killing reverts every catalog for every player and costs each of them
   * two launches to undo, while restoring only ever moves toward the state the pipeline is
   * supposed to be in. The per-catalog switch above needs no confirm because its blast radius is
   * one catalog — which is exactly the distinction that got lost the first time.
   */
  async function doToggleGlobal() {
    if (globalEnabled && !window.confirm(translate("cp.global.confirm"))) return;

    setBusy(true);
    setError(null);
    try {
      const res = await setGlobalContentEnabled(!globalEnabled);
      setDone(res.message);
      onChanged(res.message);
    } catch (err) {
      setError(`${translate("cp.global.failed")}: ${err instanceof Error ? err.message : err}`);
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
                    <div className="rounded-md border border-surface-700 bg-surface-850 px-3 py-6 text-center">
                      <p className="text-sm text-zinc-500">{translate("cp.diff.none")}</p>
                      {mirrored && (
                        <p className="mx-auto mt-2 max-w-md text-[11px] leading-relaxed text-amber-300/90">
                          {translate("cp.diff.mirrorResync")}
                        </p>
                      )}
                    </div>
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

              {historyError && (
                <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                  {historyError}
                </div>
              )}
              {!history && !historyError && (
                <p className="py-6 text-center text-sm text-zinc-500">{translate("common.loading")}</p>
              )}

              {history && (
                <>
                  {/* `table-fixed` + a scroll container, because the RESTORE
                      BUTTON MUST STAY REACHABLE. Measured 2026-08-25: with a
                      free-flowing table and a `note` column of its own, the
                      buttons landed at x=1625 inside a panel ending at x=1440 —
                      185px outside, in BOTH languages. The rollback control is
                      the entire point of this tab, so the note moved under the
                      timestamp instead of competing for width. */}
                  <div className="overflow-x-auto">
                    <table className="w-full table-fixed text-left text-xs">
                      <colgroup>
                        <col className="w-[7.5rem]" />
                        <col />
                        <col className="w-[4rem]" />
                        <col className="w-[6.5rem]" />
                      </colgroup>
                      <thead className="text-zinc-600">
                        <tr>
                          <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                            {translate("cp.history.col.version")}
                          </th>
                          <th className="whitespace-nowrap px-2 py-1.5 font-medium">
                            {translate("cp.history.col.when")}
                          </th>
                          <th className="whitespace-nowrap px-2 py-1.5 text-right font-medium">
                            {translate("cp.history.col.rows")}
                          </th>
                          <th />
                        </tr>
                      </thead>
                      <tbody>
                        {history.versions.map((entry) => {
                          const current = entry.version === summary.publishedVersion;
                          return (
                            <tr key={entry.version} className="border-t border-surface-800 align-top">
                              <td className="px-2 py-2 font-semibold tabular-nums text-zinc-200">
                                <span className="whitespace-nowrap">v{entry.version}</span>
                                {current && (
                                  <span className="ml-1.5 whitespace-nowrap rounded border border-accent-500/40 bg-accent-500/10 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                                    {translate("cp.history.current")}
                                  </span>
                                )}
                                {entry.version === 1 && (
                                  <span
                                    title={translate("cp.history.seedHint")}
                                    className="ml-1.5 whitespace-nowrap rounded border border-sky-500/40 bg-sky-500/10 px-1 py-0.5 text-[9px] font-bold text-sky-300"
                                  >
                                    {translate("cp.history.seed")}
                                  </span>
                                )}
                              </td>
                              <td className="px-2 py-2 text-zinc-400">
                                <span className="block whitespace-nowrap">
                                  {fmtDateTime(entry.publishedAt)}
                                </span>
                                <span className="block truncate text-[11px] text-zinc-500">
                                  {entry.publishedBy ?? translate("cp.history.bySeed")}
                                </span>
                                {entry.note && (
                                  <span
                                    title={entry.note}
                                    className="mt-0.5 block truncate text-[11px] italic text-zinc-600"
                                  >
                                    “{entry.note}”
                                  </span>
                                )}
                              </td>
                              <td className="whitespace-nowrap px-2 py-2 text-right tabular-nums text-zinc-400">
                                {entry.rowCount}
                              </td>
                              <td className="px-2 py-2 text-right">
                                {!current && (
                                  <button
                                    type="button"
                                    disabled={busy}
                                    title={translate("cp.history.restoreHint")}
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
                        {history.versions.length === 0 && (
                          <tr>
                            <td colSpan={4} className="px-2 py-8 text-center text-zinc-600">
                              {translate("cp.history.none")}
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>

                  {history.total > history.limit && (
                    <div className="mt-3 flex items-center justify-center gap-3 text-xs text-zinc-500">
                      <button
                        type="button"
                        disabled={historyPage <= 1}
                        onClick={() => setHistoryPage((n) => Math.max(1, n - 1))}
                        className="rounded-md border border-surface-700 px-2.5 py-1 text-zinc-300 hover:bg-surface-800 disabled:opacity-30"
                      >
                        {translate("common.prev")}
                      </button>
                      <span className="tabular-nums">
                        {translate("c.page", {
                          page: history.page,
                          pages: Math.max(1, Math.ceil(history.total / history.limit)),
                        })}
                      </span>
                      <button
                        type="button"
                        disabled={historyPage >= Math.ceil(history.total / history.limit)}
                        onClick={() => setHistoryPage((n) => n + 1)}
                        className="rounded-md border border-surface-700 px-2.5 py-1 text-zinc-300 hover:bg-surface-800 disabled:opacity-30"
                      >
                        {translate("common.next")}
                      </button>
                      <span className="text-zinc-600">
                        {translate("cp.history.total", { n: history.total })}
                      </span>
                    </div>
                  )}
                </>
              )}
            </>
          )}

          {tab === "switch" && (
            <>
              {/* TWO SWITCHES, SHOWN TOGETHER, NEVER MERGED. Each card states its own blast
                  radius in its own body text — one catalog vs every catalog for every player —
                  because "kill switch" on its own is exactly the phrase that let the per-catalog
                  column quietly do the global job (content_kill_switch_and_order). */}
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

              <div
                className={`mt-4 rounded-lg border p-4 ${
                  globalEnabled
                    ? "border-surface-800 bg-surface-950"
                    : "border-red-500/50 bg-red-500/10"
                }`}
              >
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-sm font-semibold text-zinc-200">
                    {translate("cp.global.title")}
                  </h3>
                  <span className="whitespace-nowrap rounded border border-red-500/50 bg-red-500/15 px-1.5 py-0.5 text-[10px] font-bold text-red-300">
                    {translate("cp.global.tag")}
                  </span>
                </div>

                <p className={`mt-2 text-xs ${globalEnabled ? "text-accent-300" : "text-red-300"}`}>
                  {translate(globalEnabled ? "cp.global.on" : "cp.global.off")}
                </p>

                <p className="mt-2 text-[11px] leading-relaxed text-zinc-500">
                  {translate("cp.global.timing")}
                </p>

                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void doToggleGlobal()}
                  className={`mt-4 rounded-md px-3 py-1.5 text-xs font-semibold text-white transition disabled:opacity-40 ${
                    globalEnabled ? "bg-red-600 hover:bg-red-500" : "bg-accent-600 hover:bg-accent-500"
                  }`}
                >
                  {translate(globalEnabled ? "cp.global.disable" : "cp.global.enable")}
                </button>

                <p className="mt-3 text-[11px] leading-relaxed text-zinc-600">
                  {translate("cp.global.row")}
                </p>
              </div>
            </>
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
                  disabled={!diff || (changeCount === 0 && !mirrored)}
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
