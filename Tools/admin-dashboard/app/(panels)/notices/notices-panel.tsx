"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { deriveNoticeState, NOTICE_LIMITS } from "@/lib/notice";
import { fmtDate } from "@/lib/format";
import { useT } from "@/components/I18nProvider";
import type { NoticeRow, NoticeState, NoticesResponse } from "@/lib/types";
import { NoticeEditor } from "./notice-editor";

const STATE_STYLES: Record<NoticeState, string> = {
  LIVE: "border-accent-500/40 bg-accent-500/10 text-accent-300",
  SCHEDULED: "border-sky-500/40 bg-sky-500/10 text-sky-300",
  EXPIRED: "border-surface-700 bg-surface-850 text-zinc-500",
  OFF: "border-zinc-600 bg-surface-850 text-zinc-400",
};

function StateBadge({ state }: { state: NoticeState }) {
  return (
    <span
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase ${STATE_STYLES[state]}`}
    >
      {state}
    </span>
  );
}

export function NoticesPanel() {
  const t = useT();
  const [data, setData] = useState<NoticesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const [editing, setEditing] = useState<NoticeRow | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/notices");
      const body = (await res.json().catch(() => null)) as
        | (NoticesResponse & { error?: string })
        | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      if (body) {
        setData(body);
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t("notice.loadFailed"));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const now = Date.now();
  const rows = useMemo(() => data?.notices ?? [], [data]);
  const live = useMemo(
    () => rows.filter((n) => deriveNoticeState(n, now) === "LIVE"),
    [rows, now]
  );

  async function afterMutation(message: string) {
    setEditing(null);
    setCreating(false);
    setNotice(message);
    await load();
  }

  /**
   * The one-click switch. Switching a LIVE notice off needs the typed label, so
   * this asks for it here rather than sending a request it knows will 409.
   */
  async function toggleActive(n: NoticeRow) {
    const next = !n.isActive;
    let confirmLabel: string | undefined;
    if (!next && deriveNoticeState(n, Date.now()) === "LIVE") {
      const typed = window.prompt(t("notice.confirmDeactivate", { label: n.label }));
      if (typed === null) return;
      confirmLabel = typed;
    }

    setBusyId(n.id);
    setNotice(null);
    try {
      const res = await fetch(`/api/notices/${n.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ setActive: next, confirmLabel }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      await afterMutation(body?.message ?? t("notice.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("notice.switchFailed"));
    } finally {
      setBusyId(null);
    }
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        {t("notice.loadFailed")}: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        {t("notice.loading")}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">{t("notice.title")}</h1>
        <span className="whitespace-nowrap text-xs text-zinc-500">
          {t("notice.count", { live: live.length, total: rows.length })}
        </span>
      </div>

      <div className="mb-4 rounded-lg border border-accent-500/40 bg-accent-500/10 px-4 py-3 text-xs leading-relaxed text-accent-200">
        <strong className="font-semibold">{t("notice.howItWorksLead")}</strong>{" "}
        {t("notice.howItWorks", { max: NOTICE_LIMITS.maxLive })}
      </div>

      {live.length === 0 && (
        <p className="mb-4 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-xs text-zinc-400">
          {t("notice.noneLive")}
        </p>
      )}

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}

      <div className="mb-4 flex items-center gap-3">
        {data.mock && (
          <span className="rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-yellow-300 ring-1 ring-yellow-600/40">
            MOCK
          </span>
        )}
        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setCreating(true);
          }}
          className="ml-auto rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-accent-500"
        >
          {t("notice.newNotice")}
        </button>
      </div>

      <div className="overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[880px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.page")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.label")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.state")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.text")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.langs")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("notice.col.window")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">
                {t("notice.col.sort")}
              </th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium" />
            </tr>
          </thead>
          <tbody>
            {rows.map((n) => {
              const state = deriveNoticeState(n, now);
              // The page number a player would swipe to — live rows only, in the
              // order the endpoint serves them.
              const page = live.indexOf(n);
              return (
                <tr
                  key={n.id}
                  onClick={() => {
                    setNotice(null);
                    setEditing(n);
                  }}
                  className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                    state === "LIVE" ? "bg-surface-950" : "bg-surface-950/40 opacity-70"
                  }`}
                >
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs tabular-nums text-zinc-400">
                    {page >= 0 ? page + 1 : "—"}
                  </td>
                  <td className="px-4 py-2.5 text-sm font-medium text-zinc-200">{n.label}</td>
                  <td className="px-4 py-2.5">
                    <StateBadge state={state} />
                  </td>
                  <td className="max-w-[320px] px-4 py-2.5">
                    <div className="truncate text-xs font-semibold text-zinc-200">
                      {n.titleEn || <span className="font-normal text-zinc-600">—</span>}
                    </div>
                    <div className="truncate text-[11px] text-zinc-500">
                      {n.bodyEn.replace(/\s*\n\s*/g, " · ")}
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                    <span className={n.titleEn || n.bodyEn ? "text-accent-400" : "text-zinc-600"}>
                      EN
                    </span>{" "}
                    ·{" "}
                    <span className={n.titleJa || n.bodyJa ? "text-accent-400" : "text-zinc-600"}>
                      JA
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                    {n.startAt || n.endAt ? (
                      <>
                        {n.startAt ? fmtDate(n.startAt) : t("notice.always")} →{" "}
                        {n.endAt ? fmtDate(n.endAt) : t("notice.noExpiry")}
                      </>
                    ) : (
                      <span className="text-zinc-600">{t("notice.noWindow")}</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                    {n.sortOrder}
                  </td>
                  <td className="px-4 py-2.5">
                    <button
                      type="button"
                      disabled={busyId === n.id}
                      onClick={(e) => {
                        e.stopPropagation();
                        void toggleActive(n);
                      }}
                      className={`rounded-md border px-2.5 py-1 text-xs font-medium transition disabled:opacity-50 ${
                        n.isActive
                          ? "border-surface-700 text-zinc-400 hover:bg-surface-800"
                          : "border-accent-500/50 text-accent-300 hover:bg-accent-500/15"
                      }`}
                    >
                      {n.isActive ? t("notice.deactivate") : t("notice.activate")}
                    </button>
                  </td>
                </tr>
              );
            })}
            {rows.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-sm text-zinc-600">
                  {t("notice.empty")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {(editing || creating) && (
        <NoticeEditor
          notice={editing}
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
