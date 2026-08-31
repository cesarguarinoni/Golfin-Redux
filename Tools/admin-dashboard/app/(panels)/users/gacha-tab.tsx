"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { PlayerGachaResponse } from "@/lib/types";

/**
 * The Gacha tab of the Users drawer (gacha_server_pull §6, §5.1).
 *
 * LIKE THE MISSIONS TAB AND UNLIKE THE INVENTORY TAB, EVERYTHING HERE IS SERVER
 * TRUTH. `golfin_tickets`, `golfin_ticket_transactions`, `golfin_gacha_pity` and
 * `golfin_gacha_pulls` are written only by `golfin_ticket_credit()` and
 * `golfin_gacha_pull()`. The absence of the Inventory tab's red notice is
 * deliberate, not an omission.
 *
 * ⚠️ A GRANT HERE IS A LEDGER WRITE, NOT A QUEUED GRANT. Until 2026-09-01 an
 * admin ticket grant went into `golfin_pending_grants` for the client to apply
 * into its save blob — which made the DEVICE the authority on a currency the
 * server now sells and spends. This posts to `/api/gacha/users/:id/tickets`,
 * which calls the only writer of the ledger.
 *
 * ADJUST IS A SEPARATE BUTTON FROM GRANT, and the difference is one that has to
 * be visible: a grant is additive-only (the same rule the inventory queue has),
 * an adjust may be negative. A single field that silently accepted a minus sign
 * would make "correct a mistake" and "give a gift" the same gesture.
 */
export function GachaTab({
  data,
  onCredit,
  onResetPity,
}: {
  data: PlayerGachaResponse;
  /** Opens the drawer's confirm/prompt flow. The drawer owns every mutation. */
  onCredit: (ticketType: number, amount: number, adjust: boolean) => void;
  onResetPity: (bannerId: string) => void;
}) {
  const t = useT();
  const [mode, setMode] = useState<null | "grant" | "adjust">(null);
  const [ticketType, setTicketType] = useState<number>(data.ticketTypes[0]?.id ?? 0);
  const [amountText, setAmountText] = useState("");

  const amount = Number(amountText);
  const amountValid =
    Number.isInteger(amount) &&
    amount !== 0 &&
    Math.abs(amount) <= 100000 &&
    (mode === "adjust" || amount > 0);

  return (
    <div className="space-y-3">
      {data.notMigrated && (
        <p className="rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-[11px] text-amber-200">
          {t("uinv.ticketsNotMigrated", { file: data.notMigrated })}
        </p>
      )}

      {/* ── Balances + the two writes ─────────────────────────────────── */}
      <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
            {t("ugac.tickets")}
          </span>
          <span className="flex gap-1.5">
            <button
              type="button"
              onClick={() => {
                setMode(mode === "grant" ? null : "grant");
                setAmountText("");
              }}
              className="rounded-md border border-accent-500/40 bg-accent-600/15 px-2 py-0.5 text-[10px] font-medium text-accent-300 transition hover:bg-accent-600/25"
            >
              {t("ugac.grant")}
            </button>
            <button
              type="button"
              onClick={() => {
                setMode(mode === "adjust" ? null : "adjust");
                setAmountText("");
              }}
              className="rounded-md border border-surface-700 px-2 py-0.5 text-[10px] font-medium text-zinc-400 transition hover:border-accent-500 hover:text-accent-300"
            >
              {t("ugac.adjust")}
            </button>
          </span>
        </div>

        <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{t("ugac.ledgerNote")}</p>

        {data.balances.length === 0 ? (
          <p className="py-2 text-center text-[11px] text-zinc-600">{t("ugac.noTickets")}</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {data.balances.map((b) => (
              <li
                key={b.ticketType}
                className="flex items-center justify-between gap-2 rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1"
              >
                <span className="truncate text-[11px] text-zinc-300">
                  <code className="mr-1.5 text-zinc-500">#{b.ticketType}</code>
                  {b.label ?? "—"}
                </span>
                <span className="shrink-0 text-[11px] font-semibold tabular-nums text-accent-300">
                  {b.balance.toLocaleString()}
                </span>
              </li>
            ))}
          </ul>
        )}

        {mode && (
          <div className="mt-2.5 rounded-md border border-surface-700 bg-surface-900 p-2.5">
            <span className="text-[11px] font-medium text-zinc-300">
              {mode === "grant" ? t("ugac.grant.title") : t("ugac.adjust.title")}
            </span>
            <div className="mt-2 flex flex-wrap items-end gap-2">
              <label className="block">
                <span className="text-[10px] text-zinc-500">{t("ugac.type")}</span>
                <select
                  value={ticketType}
                  onChange={(e) => setTicketType(Number(e.target.value))}
                  className="mt-0.5 block rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
                >
                  {data.ticketTypes.map((tt) => (
                    <option key={tt.id} value={tt.id}>
                      #{tt.id} {tt.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-[10px] text-zinc-500">{t("ugac.amount")}</span>
                <input
                  type="number"
                  step={1}
                  value={amountText}
                  onChange={(e) => setAmountText(e.target.value)}
                  className="mt-0.5 block w-28 rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
                />
              </label>
              <button
                type="button"
                disabled={!amountValid}
                onClick={() => {
                  onCredit(ticketType, amount, mode === "adjust");
                  setMode(null);
                  setAmountText("");
                }}
                className="rounded-md bg-accent-600 px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
              >
                {mode === "grant" ? t("ugac.grant") : t("ugac.adjust")}
              </button>
            </div>
            <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">
              {mode === "grant" ? t("ugac.grantHint") : t("ugac.adjustHint")}
            </p>
          </div>
        )}
      </section>

      {/* ── The ledger ────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
        <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
          {t("ugac.ledger")}
        </span>
        {data.transactions.length === 0 ? (
          <p className="py-2 text-center text-[11px] text-zinc-600">{t("ugac.noLedger")}</p>
        ) : (
          <ul className="mt-1.5 space-y-1">
            {data.transactions.map((tx) => (
              <li key={tx.id} className="rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1">
                <div className="flex items-center justify-between gap-2 text-[11px]">
                  <span className="truncate text-zinc-300">
                    <code className="mr-1.5 text-zinc-500">#{tx.ticketType}</code>
                    {tx.reason}
                  </span>
                  <span
                    className={`shrink-0 font-semibold tabular-nums ${
                      tx.delta < 0 ? "text-red-400" : "text-accent-400"
                    }`}
                  >
                    {tx.delta > 0 ? "+" : ""}
                    {tx.delta}
                    <span className="ml-1.5 text-[10px] font-normal text-zinc-600">
                      → {tx.balanceAfter}
                    </span>
                  </span>
                </div>
                <div className="mt-0.5 text-[10px] text-zinc-600">
                  {tx.createdBy ?? "—"} · {fmtDateTime(tx.createdAt)}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ── Pity ──────────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
        <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
          {t("ugac.pity")}
        </span>
        {data.pity.length === 0 ? (
          <p className="py-2 text-center text-[11px] text-zinc-600">{t("ugac.noPity")}</p>
        ) : (
          <ul className="mt-1.5 space-y-1">
            {data.pity.map((row) => (
              <li key={row.bannerId} className="rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1.5">
                <div className="flex items-center justify-between gap-2">
                  <code className="truncate text-[11px] text-zinc-300">{row.bannerId}</code>
                  <button
                    type="button"
                    disabled={row.counter === 0}
                    title={row.counter === 0 ? undefined : t("ugac.resetPity")}
                    onClick={() => onResetPity(row.bannerId)}
                    className="shrink-0 rounded border border-surface-700 px-1.5 py-0.5 text-[10px] font-medium text-zinc-400 transition hover:border-red-500 hover:text-red-300 disabled:cursor-not-allowed disabled:opacity-30"
                  >
                    {t("ugac.resetPity")}
                  </button>
                </div>
                <div className="mt-0.5 flex flex-wrap gap-3 text-[11px] text-zinc-500">
                  <span className={row.threshold === null ? "text-zinc-600" : "text-accent-300"}>
                    {row.threshold === null || row.minRarity === null
                      ? t("ugac.pityNone")
                      : t("ugac.pityOf", {
                          counter: row.counter,
                          threshold: row.threshold,
                          rarity: row.minRarity,
                        })}
                  </span>
                  <span>
                    {row.pullLimit === null
                      ? t("ugac.totalPulls", { used: row.totalPulls })
                      : t("ugac.totalPullsCapped", { used: row.totalPulls, limit: row.pullLimit })}
                  </span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ── Recent pulls ──────────────────────────────────────────────── */}
      <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
        <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
          {t("ugac.recentPulls")}
        </span>
        {data.pulls.length === 0 ? (
          <p className="py-2 text-center text-[11px] text-zinc-600">{t("ugac.noPulls")}</p>
        ) : (
          <ul className="mt-1.5 space-y-1">
            {data.pulls.map((pull) => (
              <li key={pull.id} className="rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1.5">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px]">
                  <code className="text-zinc-300">{pull.bannerId}</code>
                  <span className="font-semibold text-zinc-200">×{pull.pullCount}</span>
                  <span className="tabular-nums text-zinc-500">−{pull.cost}</span>
                  {pull.pityForced && (
                    <span className="whitespace-nowrap rounded bg-accent-600/20 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                      {t("ga.log.pityForced")}
                    </span>
                  )}
                  {pull.guaranteeForced && (
                    <span className="whitespace-nowrap rounded bg-accent-600/20 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                      {t("ga.log.guaranteeForced")}
                    </span>
                  )}
                </div>
                <div className="mt-0.5 flex flex-wrap gap-1.5 text-[10px] text-zinc-500">
                  {pull.prizes.map((prize) => (
                    <span
                      key={prize.slot}
                      className={`whitespace-nowrap rounded px-1 py-0.5 ${
                        prize.isDupe ? "bg-amber-500/15 text-amber-300" : "bg-surface-800 text-zinc-400"
                      }`}
                    >
                      {prize.refName ?? prize.refId}
                      {prize.quantity > 1 && ` ×${prize.quantity}`}
                      {prize.isDupe && ` +${prize.dupeRp}RP`}
                    </span>
                  ))}
                </div>
                <div className="mt-0.5 text-[10px] text-zinc-600">{fmtDateTime(pull.createdAt)}</div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
