"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import type { LoanAdminAction, LoanAdminRow, UserLoansResponse } from "@/lib/types";
import { LoanCard, NoteDialog } from "../loans/loan-rows";

/**
 * The Loans tab of the Users drawer (loans_ops §3.4).
 *
 * EVERYTHING HERE IS SERVER TRUTH — `golfin_loans`, `golfin_loan_events`,
 * `profiles.golfin_loan_offers` — so, like the Gacha and Missions tabs and
 * unlike Inventory, it carries no red notice.
 *
 * FOUR lists with the SAME row and the SAME action bar as the Loans panel
 * (`../loans/loan-rows.tsx`), plus the recipient's switch at the top. Between
 * them no row this player is a party to is invisible: pending offers, what they
 * lend (every status), what they held, and — added on Cesar's call 2026-09-10 —
 * the offers to them that went nowhere. Borrowed stays accepted-only so its
 * count keeps meaning "things they actually held"; the fourth section is where
 * "why did that offer disappear?" gets answered. The tab
 * owns its two mutations rather than routing them through the drawer's
 * `runMutation`, because both need a NOTE — the drawer's confirm modals do
 * not take one, and a loan action without a reason is exactly what the audit
 * row would then fail to explain. `onMutated` tells the drawer to refetch.
 */

type Pending =
  | { kind: "action"; loan: LoanAdminRow; action: LoanAdminAction }
  | { kind: "switch"; enabled: boolean }
  | null;

export function LoansTab({
  userId,
  data,
  onMutated,
}: {
  userId: string;
  data: UserLoansResponse;
  /** Bumps the drawer's detailVersion so this tab's data refetches. */
  onMutated: () => void;
}) {
  const t = useT();
  const [expanded, setExpanded] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [pending, setPending] = useState<Pending>(null);
  const [busy, setBusy] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);

  async function confirm(note: string) {
    if (!pending) return;
    setBusy(true);
    setDialogError(null);
    const url =
      pending.kind === "action"
        ? `/api/loans/${encodeURIComponent(pending.loan.id)}/actions`
        : `/api/users/${userId}/loan-offers`;
    const payload =
      pending.kind === "action"
        ? { action: pending.action, note }
        : { enabled: pending.enabled, note };
    try {
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const body = (await res.json().catch(() => null)) as { message?: string; error?: string } | null;
      if (!res.ok) throw new Error(body?.error ?? `HTTP ${res.status}`);
      setNotice({ ok: true, text: body?.message ?? t("common.done") });
      setPending(null);
      setRefreshKey((k) => k + 1);
      onMutated();
    } catch (err) {
      const text = err instanceof Error ? err.message : String(err);
      setDialogError(text);
      setNotice({ ok: false, text });
    } finally {
      setBusy(false);
    }
  }

  const list = (
    title: DictKey,
    rows: LoanAdminRow[],
    emptyKey: DictKey,
    hintKey?: DictKey
  ) => (
    <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
      <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {t(title)}{" "}
        <span className="font-normal normal-case tracking-normal text-zinc-600">({rows.length})</span>
      </span>
      {hintKey && (
        <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{t(hintKey)}</p>
      )}
      {rows.length === 0 ? (
        <p className="py-2 text-center text-[11px] text-zinc-600">{t(emptyKey)}</p>
      ) : (
        <ul className="mt-1.5 space-y-1">
          {rows.map((loan) => (
            <LoanCard
              key={loan.id}
              loan={loan}
              expanded={expanded === loan.id}
              onToggle={() => setExpanded(expanded === loan.id ? null : loan.id)}
              onAction={(l, action) => {
                setNotice(null);
                setDialogError(null);
                setPending({ kind: "action", loan: l, action });
              }}
              refreshKey={refreshKey}
            />
          ))}
        </ul>
      )}
    </section>
  );

  return (
    <div className="space-y-3">
      {notice && (
        <p
          className={`rounded-md border px-3 py-2 text-xs ${
            notice.ok
              ? "border-accent-500/40 bg-accent-600/10 text-accent-400"
              : "border-red-500/40 bg-red-500/10 text-red-300"
          }`}
        >
          {notice.text}
        </p>
      )}

      {/* ── The recipient's switch ────────────────────────────────────── */}
      <section
        className={`rounded-lg border px-3 py-2.5 ${
          data.offersEnabled ? "border-surface-800 bg-surface-950" : "border-red-500/50 bg-red-500/10"
        }`}
      >
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
              {t("users.loans.switch")}
            </span>
            <span
              className={`ml-2 whitespace-nowrap rounded px-2 py-0.5 text-[10px] font-bold ${
                data.offersEnabled ? "bg-accent-600/20 text-accent-300" : "bg-red-500/25 text-red-200"
              }`}
            >
              {data.offersEnabled ? t("users.loans.switchOn") : t("users.loans.switchOff")}
            </span>
          </div>
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setNotice(null);
              setDialogError(null);
              setPending({ kind: "switch", enabled: !data.offersEnabled });
            }}
            className={`rounded-md border px-2.5 py-1 text-[11px] font-medium transition disabled:opacity-40 ${
              data.offersEnabled
                ? "border-red-500/40 bg-red-500/10 text-red-300 hover:bg-red-500/20"
                : "border-accent-500/40 bg-accent-600/15 text-accent-300 hover:bg-accent-600/25"
            }`}
          >
            {data.offersEnabled ? t("users.loans.turnOff") : t("users.loans.turnOn")}
          </button>
        </div>
        <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{t("users.loans.switchHint")}</p>
      </section>

      {list("users.loans.offers", data.offers, "users.loans.emptyOffers")}
      {list("users.loans.out", data.out, "users.loans.emptyOut")}
      {list("users.loans.in", data.in, "users.loans.emptyIn")}
      {/* Cesar, 2026-09-10: widen the drawer, but as its OWN section rather than
          by folding these into Borrowed — a declined offer is not something the
          player held, and the count above has to keep meaning what it says. */}
      {list(
        "users.loans.nowhere",
        data.wentNowhere,
        "users.loans.emptyNowhere",
        "users.loans.nowhereHint"
      )}

      {pending && pending.kind === "action" && (
        <NoteDialog
          title={t(`loans.dialog.${pending.action}.title` as DictKey)}
          body={t(`loans.dialog.${pending.action}.body` as DictKey)}
          confirmLabel={t(`loans.action.${pending.action}` as DictKey)}
          destructive={pending.action !== "clear_cooldown"}
          busy={busy}
          error={dialogError}
          onCancel={() => setPending(null)}
          onConfirm={(note) => void confirm(note)}
        />
      )}
      {pending && pending.kind === "switch" && (
        <NoteDialog
          title={t(pending.enabled ? "users.loans.switch.on.title" : "users.loans.switch.off.title")}
          body={t(pending.enabled ? "users.loans.switch.on.body" : "users.loans.switch.off.body")}
          confirmLabel={t(pending.enabled ? "users.loans.turnOn" : "users.loans.turnOff")}
          destructive={!pending.enabled}
          busy={busy}
          error={dialogError}
          onCancel={() => setPending(null)}
          onConfirm={(note) => void confirm(note)}
        />
      )}
    </div>
  );
}
