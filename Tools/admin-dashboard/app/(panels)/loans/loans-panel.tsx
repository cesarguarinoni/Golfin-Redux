"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import {
  LOAN_STATUSES,
  type LoanAdminAction,
  type LoanAdminRow,
  type LoanRulesResponse,
  type LoansResponse,
} from "@/lib/types";
import { LoanCard, NoteDialog, statusLabel } from "./loan-rows";

/**
 * Loans ops (loans_ops §3.3).
 *
 * A LIVE panel, cloned from the Gacha ops panel's shape: filters, a paged
 * log, CSV of the filtered set, a row that expands to detail. No draft, no
 * publish, no version — it reads what the server did with the loans and
 * carries the three support actions.
 *
 * THREE THINGS, in the order an operator needs them:
 *
 *   1. THE LOG, because "what happened to this player's loan" is the support
 *      question. Filterable by status / kind / name-or-id / date; a row
 *      expands to its timeline and its action bar.
 *   2. THE ACTIONS, on the row, never in bulk: force-return an active loan,
 *      cancel an offer, clear a pair's cooldown. Every one needs a note.
 *   3. THE RULES CARD, read-only, fetched from the API's own constants so it
 *      cannot drift from routers/loans.py.
 *
 * NO RED WARNING BANNER, for the reason the Gacha panel gives: everything
 * here is server truth, written by the router or by a security-definer
 * function in one transaction. The one caveat is in the subtitle — lazy
 * expiry — because that is what the table will show.
 */

interface Filters {
  status: string;
  kind: string;
  q: string;
  from: string;
  to: string;
}

const EMPTY: Filters = { status: "", kind: "", q: "", from: "", to: "" };

function queryOf(filters: Filters, extra: Record<string, string> = {}): string {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.kind) params.set("kind", filters.kind);
  if (filters.q.trim()) params.set("q", filters.q.trim());
  // Widened to whole UTC days so "to" INCLUDES the day the operator typed.
  if (filters.from) params.set("from", `${filters.from}T00:00:00Z`);
  if (filters.to) params.set("to", `${filters.to}T23:59:59Z`);
  for (const [k, v] of Object.entries(extra)) params.set(k, v);
  return params.toString();
}

type PendingAction = { loan: LoanAdminRow; action: LoanAdminAction } | null;

function RulesCard({ rules }: { rules: LoanRulesResponse | null }) {
  const t = useT();
  const r = rules?.rules ?? null;
  const cell = (label: string, value: string) => (
    <div className="rounded-lg border border-surface-800 bg-surface-950 px-3 py-2">
      <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{label}</div>
      <div className="mt-0.5 text-sm font-semibold tabular-nums text-zinc-100">{value}</div>
    </div>
  );
  return (
    <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold text-zinc-200">{t("loans.rules.title")}</h2>
        {rules && (
          <code className="text-[10px] text-zinc-600">{t("loans.rules.source", { url: rules.source })}</code>
        )}
      </div>
      <p className="mt-1.5 text-[11px] leading-relaxed text-zinc-500">{t("loans.rules.body")}</p>
      {rules?.unavailable && (
        <p className="mt-2 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-[11px] text-amber-200">
          {t("loans.rules.unavailable", { reason: rules.unavailable })}
        </p>
      )}
      {!rules && <p className="py-2 text-[11px] text-zinc-600">{t("common.loading")}</p>}
      {r && (
        <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
          {cell(t("loans.rules.share"), `${(r.lenderShareBp / 100).toFixed(r.lenderShareBp % 100 === 0 ? 0 : 1)}%`)}
          {cell(t("loans.rules.days"), r.allowedDays.map((d) => t("loans.rules.daysFmt", { n: d })).join(" / "))}
          {cell(t("loans.rules.out"), String(r.maxLoansOut))}
          {cell(t("loans.rules.in"), String(r.maxLoansIn))}
          {cell(t("loans.rules.pending"), String(r.maxPendingIn))}
          {cell(t("loans.rules.ttl"), t("loans.rules.hours", { n: r.offerTtlHours }))}
          {cell(t("loans.rules.cooldown"), t("loans.rules.hours", { n: r.reofferCooldownHours }))}
          {cell(t("loans.rules.endedWindow"), t("loans.rules.daysFmt", { n: r.endedWindowDays }))}
        </div>
      )}
    </section>
  );
}

export function LoansPanel() {
  const t = useT();

  const [data, setData] = useState<LoansResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const [draft, setDraft] = useState<Filters>(EMPTY);
  const [applied, setApplied] = useState<Filters>(EMPTY);
  const [page, setPage] = useState(0);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const [pending, setPending] = useState<PendingAction>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);

  const [rules, setRules] = useState<LoanRulesResponse | null>(null);

  const load = useCallback(async (filters: Filters, pageNo: number) => {
    try {
      const res = await fetch(`/api/loans?${queryOf(filters, { page: String(pageNo) })}`, {
        cache: "no-store",
      });
      const body = (await res.json()) as LoansResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setData(body);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, []);

  useEffect(() => {
    void load(applied, page);
  }, [load, applied, page]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/api/loans/rules", { cache: "no-store" });
        const body = (await res.json()) as LoanRulesResponse & { error?: string };
        if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
        if (!cancelled) setRules(body);
      } catch (err) {
        if (!cancelled)
          setRules({ rules: null, source: "", unavailable: err instanceof Error ? err.message : String(err) });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function runAction(note: string) {
    if (!pending) return;
    setBusy(true);
    setDialogError(null);
    try {
      const res = await fetch(`/api/loans/${encodeURIComponent(pending.loan.id)}/actions`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action: pending.action, note }),
      });
      const body = (await res.json().catch(() => null)) as { message?: string; error?: string } | null;
      if (!res.ok) throw new Error(body?.error ?? `HTTP ${res.status}`);
      setNotice({ ok: true, text: body?.message ?? t("common.done") });
      setPending(null);
      setRefreshKey((k) => k + 1);
      await load(applied, page);
    } catch (err) {
      // A refusal (409 not_active / not_offered …) stays IN the dialog so the
      // operator reads it against the row they were acting on; it also goes to
      // the panel's notice so it is not lost when the dialog closes.
      const text = err instanceof Error ? err.message : String(err);
      setDialogError(text);
      setNotice({ ok: false, text });
    } finally {
      setBusy(false);
    }
  }

  const loans = data?.loans ?? [];

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{t("loans.title")}</h1>
        <code className="text-xs text-zinc-600">golfin_loans · golfin_loan_events</code>
      </div>

      <p className="mb-4 text-[11px] leading-relaxed text-zinc-500">{t("loans.subtitle")}</p>

      {error && (
        <p className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {error}
        </p>
      )}
      {notice && (
        <p
          className={`mb-4 rounded-md border px-3 py-2 text-xs ${
            notice.ok
              ? "border-accent-500/40 bg-accent-600/10 text-accent-400"
              : "border-red-500/40 bg-red-500/10 text-red-300"
          }`}
        >
          {notice.text}
        </p>
      )}

      {/* ── 1. The log ──────────────────────────────────────────────────── */}
      <section className="mb-4 rounded-lg border border-surface-800 bg-surface-950 p-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-sm font-semibold text-zinc-200">{t("loans.log.title")}</h2>
          <a
            href={`/api/loans/export?${queryOf(applied)}`}
            title={t("loans.exportHint")}
            className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] font-medium text-zinc-300 transition hover:border-accent-500 hover:text-accent-300"
          >
            {t("loans.export")}
          </a>
        </div>

        {/* Status chips — the whole set, All first. */}
        <div className="mt-3 flex flex-wrap gap-1">
          {(["", ...LOAN_STATUSES] as const).map((s) => {
            const active = draft.status === s;
            return (
              <button
                key={s || "all"}
                type="button"
                onClick={() => {
                  const next = { ...draft, status: s };
                  setDraft(next);
                  setApplied(next);
                  setPage(0);
                }}
                className={`whitespace-nowrap rounded-full border px-2.5 py-0.5 text-[10px] font-medium transition ${
                  active
                    ? "border-accent-500 bg-accent-600/20 text-accent-300"
                    : "border-surface-700 text-zinc-400 hover:border-accent-500/60 hover:text-zinc-200"
                }`}
              >
                {s ? statusLabel(s, t) : t("loans.filter.all")}
              </button>
            );
          })}
        </div>

        <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-5">
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("loans.filter.kind")}</span>
            <select
              value={draft.kind}
              onChange={(e) => setDraft({ ...draft, kind: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="">{t("loans.filter.all")}</option>
              <option value="character">{t("loans.kind.character")}</option>
              <option value="club">{t("loans.kind.club")}</option>
            </select>
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("loans.filter.q")}</span>
            <input
              value={draft.q}
              onChange={(e) => setDraft({ ...draft, q: e.target.value })}
              placeholder={t("loans.filter.qPlaceholder")}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("loans.filter.from")}</span>
            <input
              type="date"
              value={draft.from}
              onChange={(e) => setDraft({ ...draft, from: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("loans.filter.to")}</span>
            <input
              type="date"
              value={draft.to}
              onChange={(e) => setDraft({ ...draft, to: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <div className="flex items-end gap-2">
            <button
              type="button"
              onClick={() => {
                setApplied(draft);
                setPage(0);
              }}
              className="rounded-md bg-accent-600 px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-accent-500"
            >
              {t("loans.filter.apply")}
            </button>
            <button
              type="button"
              onClick={() => {
                setDraft(EMPTY);
                setApplied(EMPTY);
                setPage(0);
              }}
              className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] text-zinc-400 hover:bg-surface-800"
            >
              {t("loans.filter.clear")}
            </button>
          </div>
        </div>

        {data && loans.length === 0 ? (
          <p className="py-6 text-center text-xs text-zinc-600">{t("loans.empty")}</p>
        ) : (
          <ul className="mt-3 space-y-1.5">
            {loans.map((loan) => (
              <LoanCard
                key={loan.id}
                loan={loan}
                expanded={expanded === loan.id}
                onToggle={() => setExpanded(expanded === loan.id ? null : loan.id)}
                onAction={(l, action) => {
                  setNotice(null);
                  setDialogError(null);
                  setPending({ loan: l, action });
                }}
                refreshKey={refreshKey}
              />
            ))}
          </ul>
        )}

        {data && (page > 0 || data.hasMore) && (
          <div className="mt-3 flex items-center justify-end gap-2 text-[11px] text-zinc-400">
            <button
              type="button"
              disabled={page === 0}
              onClick={() => setPage(page - 1)}
              className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
            >
              {t("loans.prev")}
            </button>
            <span>{t("loans.page", { n: page + 1 })}</span>
            <button
              type="button"
              disabled={!data.hasMore}
              onClick={() => setPage(page + 1)}
              className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
            >
              {t("loans.next")}
            </button>
          </div>
        )}
      </section>

      {/* ── 2. The rules card ───────────────────────────────────────────── */}
      <RulesCard rules={rules} />

      {/* ── The action confirmation ─────────────────────────────────────── */}
      {pending && (
        <NoteDialog
          title={t(`loans.dialog.${pending.action}.title` as DictKey)}
          body={t(`loans.dialog.${pending.action}.body` as DictKey)}
          confirmLabel={t(`loans.action.${pending.action}` as DictKey)}
          destructive={pending.action !== "clear_cooldown"}
          busy={busy}
          error={dialogError}
          onCancel={() => setPending(null)}
          onConfirm={(note) => void runAction(note)}
        />
      )}
    </div>
  );
}
