"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type {
  InventoryEntityRow,
  InventoryGrantRow,
  PlayerInventory,
  PlayerInventoryResponse,
} from "@/lib/types";

/**
 * The Inventory tab of the Users drawer (SPEC content_player_inventory §5, §6).
 *
 * ⚠️ THE RED NOTICE AT THE TOP IS NOT DECORATION AND MUST NOT BE QUIETLY DROPPED
 * IN A LATER REDESIGN. It is the same rule the Shop panel carries about prices
 * (CONTENT_PIPELINE_PLAN §11.5), for the same reason: moving something
 * server-side makes it very easy to assume it is now enforced. Inventory sync is
 * BACKUP, not anti-cheat — a modified client can still grant itself anything,
 * and everything shown below is client-asserted. A panel that lets an operator
 * believe otherwise is worse than no panel.
 */

function Section({
  title,
  count,
  children,
}: {
  title: string;
  count: number;
  children: React.ReactNode;
}) {
  return (
    <section className="mt-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div className="flex items-baseline justify-between">
        <span className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
          {title}
        </span>
        <span className="text-[10px] tabular-nums text-zinc-600">{count}</span>
      </div>
      <div className="mt-1.5">{children}</div>
    </section>
  );
}

function Empty({ label }: { label: string }) {
  return <p className="py-2 text-center text-[11px] text-zinc-600">{label}</p>;
}

/**
 * A row stored as a bare id is AT THE CATALOG DEFAULT — that is the whole point
 * of the delta encoding (SPEC §1) — and "default" is the only thing this panel
 * can truthfully say about it, because the dashboard has no catalog. Anything
 * more specific would be a number invented here.
 */
function EntityList({
  rows,
  defaultLabel,
  emptyLabel,
}: {
  rows: InventoryEntityRow[];
  defaultLabel: string;
  emptyLabel: string;
}) {
  if (rows.length === 0) return <Empty label={emptyLabel} />;
  return (
    <ul className="space-y-1">
      {rows.map((row) => (
        <li
          key={row.id}
          className="flex items-center justify-between gap-2 rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1"
        >
          <code className="truncate text-[11px] text-zinc-300">{row.id}</code>
          {row.atDefault ? (
            <span className="shrink-0 rounded bg-surface-800 px-1.5 py-0.5 text-[9px] font-medium uppercase tracking-wide text-zinc-500">
              {defaultLabel}
            </span>
          ) : (
            <span className="shrink-0 font-mono text-[10px] text-accent-300">
              {Object.entries(row.deltas)
                .map(([k, v]) => `${k}=${v}`)
                .join(" ")}
            </span>
          )}
        </li>
      ))}
    </ul>
  );
}

function CountList({
  counts,
  emptyLabel,
  unlimitedLabel,
}: {
  counts: Record<string, number>;
  emptyLabel: string;
  unlimitedLabel: string;
}) {
  const entries = Object.entries(counts);
  if (entries.length === 0) return <Empty label={emptyLabel} />;
  return (
    <ul className="space-y-1">
      {entries.map(([id, n]) => (
        <li
          key={id}
          className="flex items-center justify-between gap-2 rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1"
        >
          <code className="truncate text-[11px] text-zinc-300">{id}</code>
          <span className="shrink-0 text-[11px] font-semibold tabular-nums text-zinc-200">
            {/* -1 is the UNLIMITED sentinel on ballQuantities — rendering it as
                "-1" would read as a negative stack. */}
            {n < 0 ? unlimitedLabel : n.toLocaleString()}
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * The grants queue, with a REVOKE on the pending ones (PLAN §6.5 decision 3).
 *
 * ⚠️ REVOKE IS OFFERED ONLY WHILE `appliedAt` IS NULL, and that is not a cosmetic disable. Grants
 * are additive-only end to end — the queue, the merge and the client all refuse to subtract — so
 * once a grant drains, the player HAS the thing and deleting the queue row would take nothing
 * back. A button that appeared to undo an applied grant would be a lie about the one part of this
 * system that has no undo. The server enforces the same rule and answers 409, so a grant that
 * drains while this drawer is open cannot slip through the gap.
 */
function GrantList({
  grants,
  onRevoke,
}: {
  grants: InventoryGrantRow[];
  /** Absent when the host has no way to run the mutation; the button is then not rendered. */
  onRevoke?: (grant: InventoryGrantRow) => void;
}) {
  const t = useT();
  if (grants.length === 0) return <Empty label={t("uinv.noGrants")} />;
  return (
    <ul className="space-y-1.5">
      {grants.map((g) => (
        <li
          key={g.id}
          className="rounded border border-surface-800/70 bg-surface-900/60 px-2 py-1.5"
        >
          <div className="flex items-center justify-between gap-2">
            <span className="truncate text-[11px] text-zinc-300">
              <span className="mr-1.5 rounded bg-surface-800 px-1 py-0.5 text-[9px] uppercase tracking-wide text-zinc-500">
                {g.kind}
              </span>
              <code>{g.refId}</code>
              <span className="ml-1.5 font-semibold tabular-nums text-accent-300">
                ×{g.amount}
              </span>
            </span>
            <span className="flex shrink-0 items-center gap-1.5">
              <span
                className={`rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide ${
                  g.appliedAt
                    ? "bg-surface-800 text-zinc-500"
                    : "bg-accent-600/15 text-accent-300 ring-1 ring-accent-500/40"
                }`}
              >
                {g.appliedAt ? t("uinv.grantApplied") : t("uinv.grantPending")}
              </span>
              {!g.appliedAt && onRevoke && (
                <button
                  type="button"
                  title={t("uinv.revokeHint")}
                  onClick={() => onRevoke(g)}
                  className="whitespace-nowrap rounded border border-red-500/40 px-1.5 py-0.5 text-[10px] font-medium text-red-300 transition hover:bg-red-500/10"
                >
                  {t("uinv.revoke")}
                </button>
              )}
            </span>
          </div>
          {g.note && <div className="mt-0.5 text-[11px] text-zinc-400">{g.note}</div>}
          <div className="mt-0.5 text-[10px] text-zinc-600">
            {g.createdBy ?? "—"} · {fmtDateTime(g.createdAt)}
            {g.appliedAt && ` · ${t("uinv.appliedAt")} ${fmtDateTime(g.appliedAt)}`}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function InventoryTab({
  data,
  onRevokeGrant,
}: {
  data: PlayerInventoryResponse;
  /** Opens the drawer's confirm modal. The drawer owns every mutation, so this tab stays a view. */
  onRevokeGrant?: (grant: InventoryGrantRow) => void;
}) {
  const t = useT();
  const [showRaw, setShowRaw] = useState(false);
  const inv: PlayerInventory | null = data.inventory;

  return (
    <div>
      {/* ⚠️ SPEC §6 — see the file header. Do not remove. */}
      <div className="rounded-lg border border-red-500/50 bg-red-500/10 px-3 py-2.5">
        <p className="text-[11px] font-bold text-red-300">⚠ {t("uinv.notice.headline")}</p>
        <p className="mt-1 text-[11px] leading-relaxed text-red-200/85">
          {t("uinv.notice.body")}
        </p>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-zinc-500">
        <span>
          {t("uinv.rev")}{" "}
          <span className="font-semibold tabular-nums text-zinc-300">{data.rev}</span>
        </span>
        <span>
          {t("uinv.lastSync")}{" "}
          <span className="text-zinc-300">
            {data.updatedAt ? fmtDateTime(data.updatedAt) : t("common.none")}
          </span>
        </span>
        {inv && (
          <span>
            {t("uinv.size")}{" "}
            <span className="font-semibold tabular-nums text-zinc-300">
              {inv.bytes.toLocaleString()} B
            </span>
          </span>
        )}
      </div>

      {/* THE SERVER LEDGER, and it is OUTSIDE the `inv` guard on purpose: a
          player who has never synced an inventory blob can still have been
          granted tickets, and hiding their balance behind "never synced" would
          answer the support question with a blank. */}
      {data.tickets && (
        <Section
          title={t("uinv.ticketsLedger")}
          count={data.tickets.balances.length}
        >
          {data.tickets.notMigrated ? (
            <p className="py-2 text-center text-[11px] text-amber-300">
              {t("uinv.ticketsNotMigrated", { file: data.tickets.notMigrated })}
            </p>
          ) : data.tickets.balances.length === 0 ? (
            <Empty label={t("ugac.noTickets")} />
          ) : (
            <ul className="space-y-1">
              {data.tickets.balances.map((b) => (
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

          {(data.tickets.transactions.length > 0) && (
            <ul className="mt-2 space-y-1 border-t border-surface-800 pt-2">
              {data.tickets.transactions.map((tx) => (
                <li key={tx.id} className="flex items-baseline justify-between gap-2 text-[10px]">
                  <span className="truncate text-zinc-500">
                    <code className="mr-1 text-zinc-600">#{tx.ticketType}</code>
                    {tx.reason}
                  </span>
                  <span
                    className={`shrink-0 tabular-nums ${
                      tx.delta < 0 ? "text-red-400" : "text-accent-400"
                    }`}
                  >
                    {tx.delta > 0 ? "+" : ""}
                    {tx.delta}
                    <span className="ml-1 text-zinc-600">→ {tx.balanceAfter}</span>
                  </span>
                </li>
              ))}
            </ul>
          )}

          <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">
            {t("ugac.ledgerNote")}
          </p>
        </Section>
      )}

      {!inv ? (
        <p className="mt-4 rounded-lg border border-surface-800 bg-surface-950 px-3 py-6 text-center text-xs text-zinc-500">
          {t("uinv.neverSynced")}
        </p>
      ) : (
        <>
          <Section title={t("uinv.clubs")} count={inv.clubs.length}>
            <EntityList
              rows={inv.clubs}
              defaultLabel={t("uinv.atDefault")}
              emptyLabel={t("uinv.noClubs")}
            />
          </Section>

          <Section title={t("uinv.characters")} count={inv.characters.length}>
            <EntityList
              rows={inv.characters}
              defaultLabel={t("uinv.atDefault")}
              emptyLabel={t("uinv.noCharacters")}
            />
          </Section>

          <Section title={t("uinv.items")} count={Object.keys(inv.items).length}>
            <CountList
              counts={inv.items}
              emptyLabel={t("uinv.noItems")}
              unlimitedLabel={t("uinv.unlimited")}
            />
          </Section>

          <Section title={t("uinv.balls")} count={Object.keys(inv.balls).length}>
            <CountList
              counts={inv.balls}
              emptyLabel={t("uinv.noBalls")}
              unlimitedLabel={t("uinv.unlimited")}
            />
          </Section>

          {/* ⚠️ THE BLOB'S TICKET MAP IS A DEVICE COUNTER, NOT A BALANCE
              (gacha_server_pull §5.1). `golfin_tickets` is the authority the
              server charges against; this map is what the client still keeps in
              its save and it is NOT kept in step with the ledger. The two are
              rendered together, labelled, because an operator comparing them is
              exactly the support question — and a panel that showed only one of
              them would answer it wrongly whichever one it picked. The device
              copy retires when the client moves to the ledger (spec C). */}
          <Section
            title={t("uinv.ticketsDevice")}
            count={Object.keys(inv.tickets).length}
          >
            <CountList
              counts={inv.tickets}
              emptyLabel={t("uinv.noTickets")}
              unlimitedLabel={t("uinv.unlimited")}
            />
            <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">
              {t("uinv.ticketsDeviceHint")}
            </p>
          </Section>

          <section className="mt-3 grid grid-cols-1 gap-2 rounded-lg border border-surface-800 bg-surface-950 p-3 sm:grid-cols-3">
            <div>
              <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                {t("uinv.holes")}
              </div>
              <div className="mt-0.5 font-mono text-[11px] text-zinc-300">
                {inv.unlockedHoles.length > 0
                  ? inv.unlockedHoles.join(", ")
                  : t("common.none")}
              </div>
            </div>
            <div>
              <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                {t("uinv.starter")}
              </div>
              <div className="mt-0.5 truncate font-mono text-[11px] text-zinc-300">
                {inv.starterCharacterId ?? t("common.none")}
              </div>
            </div>
            <div>
              <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                {t("uinv.selected")}
              </div>
              <div className="mt-0.5 truncate font-mono text-[11px] text-zinc-300">
                {inv.selectedCharacterId ?? t("common.none")}
              </div>
            </div>
          </section>

          <button
            type="button"
            onClick={() => setShowRaw((v) => !v)}
            className="mt-3 text-[11px] text-zinc-500 underline-offset-2 hover:text-accent-400 hover:underline"
          >
            {showRaw ? t("uinv.hideRaw") : t("uinv.showRaw")}
          </button>
          {showRaw && (
            <pre className="mt-1.5 max-h-64 overflow-auto rounded-lg border border-surface-800 bg-surface-950 p-3 font-mono text-[10px] leading-relaxed text-zinc-400">
              {inv.raw}
            </pre>
          )}
        </>
      )}

      <Section title={t("uinv.grants")} count={data.grants.length}>
        <GrantList grants={data.grants} onRevoke={onRevokeGrant} />
      </Section>

      <p className="mt-2 text-[10px] leading-relaxed text-zinc-600">
        {t("uinv.grantsHint")}
      </p>
    </div>
  );
}
