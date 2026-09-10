"use client";

import { useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { fmtDateTime } from "@/lib/format";
import { actionsFor, clockLabel } from "@/lib/loanStatus";
import type { LoanAdminAction, LoanAdminRow, LoanDetailResponse } from "@/lib/types";

/**
 * The loan ROW, its expanded timeline + action bar, and the note dialog —
 * shared by the Loans panel and the Users drawer's Loans tab (loans_ops §3.3,
 * §3.4), so the two surfaces cannot drift: an operator who has used one has
 * used both.
 *
 * The card fetches its OWN detail (`GET /api/loans/:id`) when expanded, and
 * again whenever `refreshKey` changes — the parent bumps it after an action so
 * the timeline shows the admin row it just wrote. The MUTATION is the parent's:
 * the card only reports "the operator wants action X on loan Y", and the parent
 * owns the dialog, the POST, the toast and the list refetch. That is the same
 * split the Users drawer makes with its tabs.
 */

type T = ReturnType<typeof useT>;

/** "in 2d" / "3h ago", from an ISO stamp against the real clock. */
export function relativeOf(iso: string | null, t: T, nowMs = Date.now()): string {
  if (!iso) return "—";
  const at = Date.parse(iso);
  if (!Number.isFinite(at)) return "—";
  const diff = at - nowMs;
  const abs = Math.abs(diff);
  let n: number;
  let unit: DictKey;
  if (abs < 3_600_000) {
    n = Math.max(1, Math.round(abs / 60_000));
    unit = "loans.unit.m";
  } else if (abs < 48 * 3_600_000) {
    n = Math.round(abs / 3_600_000);
    unit = "loans.unit.h";
  } else {
    n = Math.round(abs / 86_400_000);
    unit = "loans.unit.d";
  }
  return t(diff >= 0 ? "loans.rel.in" : "loans.rel.ago", { n, unit: t(unit) });
}

const STATUS_TONE: Record<string, string> = {
  offered: "border-amber-500/40 bg-amber-500/10 text-amber-300",
  active: "border-accent-500/40 bg-accent-600/15 text-accent-300",
  returned: "border-surface-700 bg-surface-800 text-zinc-300",
  expired: "border-surface-700 bg-surface-800 text-zinc-400",
  declined: "border-red-500/30 bg-red-500/10 text-red-300",
  rescinded: "border-red-500/30 bg-red-500/10 text-red-300",
  offer_expired: "border-surface-700 bg-surface-800 text-zinc-500",
};

export function statusLabel(status: string, t: T): string {
  const key = `loans.status.${status}` as DictKey;
  const label = t(key);
  // `translate` returns the key for an unknown status; show the raw column
  // value instead so a new status is visible rather than a literal key.
  return label === key ? status.toUpperCase() : label;
}

export function StatusPill({ status }: { status: string }) {
  const t = useT();
  return (
    <span
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[9px] font-bold ${
        STATUS_TONE[status] ?? "border-surface-700 bg-surface-800 text-zinc-300"
      }`}
    >
      {statusLabel(status, t)}
    </span>
  );
}

/**
 * Format the clock line the shared module chose.
 *
 * The CHOICE — which string, which timestamp — lives in `lib/loanStatus.ts`
 * because it is a judgement about status and clocks, and this component used to
 * make its own with a `switch` whose `default` labelled a lender's rescind and
 * a 48 h lapse as "answered". This function now only renders.
 */
function clockLine(loan: LoanAdminRow, t: T): string | null {
  const now = Date.now();
  const chosen = clockLabel(loan, now);
  return chosen ? t(chosen.key, { rel: relativeOf(chosen.iso, t, now) }) : null;
}

function PartyLink({ id, name }: { id: string; name: string | null }) {
  const t = useT();
  return (
    <a
      href={`/users?open=${encodeURIComponent(id)}`}
      title={t("loans.openUser")}
      className="truncate text-zinc-200 underline-offset-2 hover:text-accent-300 hover:underline"
    >
      {name ?? `${id.slice(0, 8)}…`}
    </a>
  );
}

export function LoanCard({
  loan,
  expanded,
  onToggle,
  onAction,
  refreshKey,
}: {
  loan: LoanAdminRow;
  expanded: boolean;
  onToggle: () => void;
  /** The operator pressed an action button. The parent opens the note dialog. */
  onAction: (loan: LoanAdminRow, action: LoanAdminAction) => void;
  /** Bumped by the parent after a mutation so the timeline refetches. */
  refreshKey: number;
}) {
  const t = useT();
  const [detail, setDetail] = useState<LoanDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!expanded) return;
    let cancelled = false;
    setError(null);
    (async () => {
      try {
        const res = await fetch(`/api/loans/${encodeURIComponent(loan.id)}`, { cache: "no-store" });
        const body = (await res.json()) as LoanDetailResponse & { error?: string };
        if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
        if (!cancelled) setDetail(body);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [expanded, loan.id, refreshKey]);

  const kindKey = `loans.kind.${loan.kind}` as DictKey;
  const kindLabel = t(kindKey) === kindKey ? loan.kind : t(kindKey);
  const clock = clockLine(loan, t);
  const actions = actionsFor(loan, Date.now());

  return (
    <li className="rounded-md border border-surface-800/70 bg-surface-900/60 px-2.5 py-2">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px]">
        <span className="whitespace-nowrap text-zinc-500">
          {fmtDateTime(loan.offeredAt ?? loan.startsAt ?? loan.createdAt)}
        </span>
        <StatusPill status={loan.status} />
        <span className="whitespace-nowrap">
          <span className="rounded bg-surface-800 px-1 py-0.5 text-[9px] uppercase text-zinc-500">
            {kindLabel}
          </span>{" "}
          <code className="text-zinc-400">{loan.refId}</code>
        </span>
        <span className="flex min-w-0 items-center gap-1">
          <PartyLink id={loan.lenderId} name={loan.lenderName} />
          <span className="text-zinc-600">→</span>
          <PartyLink id={loan.borrowerId} name={loan.borrowerName} />
        </span>
        <span className="whitespace-nowrap font-semibold text-zinc-200">
          {t("loans.col.days", { n: loan.days })}
        </span>
        <span className="whitespace-nowrap tabular-nums text-zinc-400">
          {t("loans.col.level", { from: loan.levelAtStart, to: loan.levelAtEnd ?? "…" })}
        </span>
        <span className="whitespace-nowrap tabular-nums text-zinc-400" title={t("loans.col.rpHint")}>
          {t("loans.col.rp", { lender: loan.rpToLender, borrower: loan.rpToBorrower })}
        </span>
        {clock && <span className="whitespace-nowrap text-zinc-500">{clock}</span>}
        <button
          type="button"
          onClick={onToggle}
          className="ml-auto whitespace-nowrap text-[10px] text-zinc-500 underline-offset-2 hover:text-accent-400 hover:underline"
        >
          {expanded ? t("loans.collapse") : t("loans.expand")}
        </button>
      </div>

      {expanded && (
        <div className="mt-2 border-l border-surface-800 pl-3">
          {error && (
            <p className="rounded-md border border-red-500/40 bg-red-500/10 px-2 py-1 text-[11px] text-red-300">
              {error}
            </p>
          )}
          {!detail && !error && (
            <p className="py-1 text-[11px] text-zinc-600">{t("common.loading")}</p>
          )}
          {detail && (
            <>
              <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                {t("loans.timeline.title")}
              </div>
              {detail.notMigrated && (
                <p className="mt-1 rounded-md border border-amber-500/50 bg-amber-500/10 px-2 py-1 text-[11px] text-amber-200">
                  {t("loans.timeline.notMigrated", { file: detail.notMigrated })}
                </p>
              )}
              {!detail.notMigrated && detail.events.length === 0 && (
                <p className="py-1 text-[11px] text-zinc-600">{t("loans.timeline.empty")}</p>
              )}
              {detail.events.length > 0 && (
                <ul className="mt-1 space-y-1">
                  {detail.events.map((ev) => (
                    <li key={ev.id} className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[11px]">
                      <span className="whitespace-nowrap text-zinc-600">{fmtDateTime(ev.at)}</span>
                      <span
                        className={`whitespace-nowrap rounded px-1 py-0.5 text-[9px] font-bold uppercase ${
                          ev.actor === "admin"
                            ? "bg-red-500/15 text-red-300"
                            : ev.actor === "system"
                              ? "bg-surface-800 text-zinc-500"
                              : "bg-accent-600/15 text-accent-300"
                        }`}
                      >
                        {t(`loans.actor.${ev.actor}` as DictKey)}
                      </span>
                      <span className="text-zinc-300">
                        {ev.fromStatus ? statusLabel(ev.fromStatus, t) : t("loans.timeline.created")}
                        <span className="mx-1 text-zinc-600">→</span>
                        {statusLabel(ev.toStatus, t)}
                      </span>
                      {ev.adminEmail && <span className="text-zinc-500">{ev.adminEmail}</span>}
                      {ev.note && <span className="italic text-zinc-400">“{ev.note}”</span>}
                    </li>
                  ))}
                </ul>
              )}
              <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">{t("loans.timeline.rpNote")}</p>
              <div className="mt-1 text-[10px] text-zinc-600">
                {t("loans.ids")}: <code>{loan.id}</code> · <code>{loan.lenderId}</code> →{" "}
                <code>{loan.borrowerId}</code>
              </div>
            </>
          )}

          {actions.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {actions.map((action) => (
                <button
                  key={action}
                  type="button"
                  onClick={() => onAction(loan, action)}
                  className={`rounded-md border px-2 py-0.5 text-[10px] font-medium transition ${
                    action === "clear_cooldown"
                      ? "border-accent-500/40 bg-accent-600/15 text-accent-300 hover:bg-accent-600/25"
                      : "border-red-500/40 bg-red-500/10 text-red-300 hover:bg-red-500/20"
                  }`}
                >
                  {t(`loans.action.${action}` as DictKey)}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </li>
  );
}

/**
 * The confirm dialog every loan mutation goes through — one shape for the
 * three per-loan actions AND the offers switch, with a REQUIRED note. The
 * same posture as the gacha pause's typed word: the thing that makes an
 * operator pause is having to say why.
 */
export function NoteDialog({
  title,
  body,
  confirmLabel,
  destructive,
  busy,
  error,
  onCancel,
  onConfirm,
}: {
  title: string;
  body: string;
  confirmLabel: string;
  destructive: boolean;
  busy: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: (note: string) => void;
}) {
  const t = useT();
  const [note, setNote] = useState("");
  const valid = note.trim().length >= 3;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label={t("common.close")}
        onClick={onCancel}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />
      <div className="relative w-full max-w-md rounded-lg border border-surface-700 bg-surface-900 p-5 shadow-2xl">
        <h3 className="text-sm font-semibold text-zinc-100">{title}</h3>
        <p className="mt-2 text-[11px] leading-relaxed text-zinc-400">{body}</p>

        <label className="mt-3 block">
          <span className="text-[10px] text-zinc-500">{t("loans.dialog.note")}</span>
          <textarea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder={t("loans.dialog.notePlaceholder")}
            autoFocus
            rows={3}
            maxLength={500}
            className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-100 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
          />
        </label>
        <p className="mt-1 text-[10px] text-zinc-600">{t("loans.dialog.noteHint")}</p>

        {error && (
          <p className="mt-2 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
            {error}
          </p>
        )}

        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={busy}
            className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
          >
            {t("common.cancel")}
          </button>
          <button
            type="button"
            disabled={busy || !valid}
            onClick={() => onConfirm(note.trim())}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-40 ${
              destructive ? "bg-red-600 hover:bg-red-500" : "bg-accent-600 hover:bg-accent-500"
            }`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
