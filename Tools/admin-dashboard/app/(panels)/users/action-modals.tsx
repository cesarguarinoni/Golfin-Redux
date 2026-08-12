"use client";

import { useState } from "react";
import type { AdminUserRow } from "@/lib/types";

/** Confirmation / input modals for the phase-2 admin actions. */

function ModalShell({
  title,
  mock,
  destructive,
  onClose,
  children,
}: {
  title: string;
  mock: boolean;
  destructive?: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/70"
      />
      <div
        role="dialog"
        aria-modal="true"
        className={`relative w-full max-w-md rounded-xl border bg-surface-900 p-5 shadow-2xl ${
          destructive ? "border-red-500/50" : "border-surface-700"
        }`}
      >
        <div className="flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-zinc-100">{title}</h3>
          {mock && (
            <span className="rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-yellow-300 ring-1 ring-yellow-600/40">
              MOCK
            </span>
          )}
        </div>
        {children}
      </div>
    </div>
  );
}

function ModalError({ error }: { error: string | null }) {
  if (!error) return null;
  return (
    <p className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
      {error}
    </p>
  );
}

function ModalButtons({
  confirmLabel,
  destructive,
  busy,
  disabled,
  onConfirm,
  onCancel,
}: {
  confirmLabel: string;
  destructive?: boolean;
  busy: boolean;
  disabled?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="mt-5 flex justify-end gap-2">
      <button
        type="button"
        onClick={onCancel}
        disabled={busy}
        className="rounded-md border border-surface-700 bg-surface-850 px-3 py-1.5 text-xs font-medium text-zinc-300 transition hover:bg-surface-700 disabled:opacity-50"
      >
        Cancel
      </button>
      <button
        type="button"
        onClick={onConfirm}
        disabled={busy || disabled}
        className={`rounded-md px-3 py-1.5 text-xs font-semibold text-white transition disabled:opacity-50 ${
          destructive
            ? "bg-red-600 hover:bg-red-500"
            : "bg-accent-600 hover:bg-accent-500"
        }`}
      >
        {busy ? "Working…" : confirmLabel}
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Generic confirm (ban/unban/confirm email/resend/reset)
// ---------------------------------------------------------------------------
export function ConfirmActionModal({
  title,
  body,
  confirmLabel,
  destructive = false,
  mock,
  busy,
  error,
  onConfirm,
  onCancel,
}: {
  title: string;
  body: React.ReactNode;
  confirmLabel: string;
  destructive?: boolean;
  mock: boolean;
  busy: boolean;
  error: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <ModalShell title={title} mock={mock} destructive={destructive} onClose={onCancel}>
      <div className="mt-3 text-xs leading-relaxed text-zinc-400">{body}</div>
      <ModalError error={error} />
      <ModalButtons
        confirmLabel={confirmLabel}
        destructive={destructive}
        busy={busy}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Delete user — type-the-email double confirm + cascade warning
// ---------------------------------------------------------------------------
export function DeleteUserModal({
  user,
  mock,
  busy,
  error,
  onConfirm,
  onCancel,
}: {
  user: AdminUserRow;
  mock: boolean;
  busy: boolean;
  error: string | null;
  onConfirm: (confirmEmail: string) => void;
  onCancel: () => void;
}) {
  const [typed, setTyped] = useState("");
  const matches = typed.trim().toLowerCase() === user.email.toLowerCase();

  return (
    <ModalShell title="Delete user" mock={mock} destructive onClose={onCancel}>
      <div className="mt-3 rounded-md border border-red-500/50 bg-red-500/10 px-3 py-2.5 text-xs leading-relaxed text-red-200">
        <p className="font-bold uppercase tracking-wide text-red-300">
          Permanent — cannot be undone
        </p>
        <p className="mt-1.5">
          Deleting <span className="font-mono">{user.email}</span> removes the
          auth user and, via FK cascade, everything hanging off it:
        </p>
        <ul className="mt-1.5 list-inside list-disc space-y-0.5">
          <li>
            <span className="font-mono">profiles</span> row — RP balance (
            {user.totalPoints.toLocaleString()} RP), avatar, social counters
          </li>
          <li>
            <span className="font-mono">points_transactions</span> — the entire
            points ledger history
          </li>
          <li>
            <span className="font-mono">activities</span> — GPS check-ins
          </li>
        </ul>
      </div>
      <label className="mt-4 block text-xs font-medium text-zinc-400">
        Type the user&apos;s email to confirm
        <input
          type="text"
          value={typed}
          onChange={(e) => setTyped(e.target.value)}
          placeholder={user.email}
          autoComplete="off"
          spellCheck={false}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 font-mono text-xs text-zinc-200 placeholder:text-zinc-700 focus:border-red-500 focus:outline-none"
        />
      </label>
      <ModalError error={error} />
      <ModalButtons
        confirmLabel="Delete user permanently"
        destructive
        busy={busy}
        disabled={!matches}
        onConfirm={() => onConfirm(typed)}
        onCancel={onCancel}
      />
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Adjust RP — amount (+/-) and required bounded reason
// ---------------------------------------------------------------------------
export function AdjustRpModal({
  user,
  mock,
  busy,
  error,
  onSubmit,
  onCancel,
}: {
  user: AdminUserRow;
  mock: boolean;
  busy: boolean;
  error: string | null;
  onSubmit: (amount: number, reason: string) => void;
  onCancel: () => void;
}) {
  const [amountText, setAmountText] = useState("");
  const [reason, setReason] = useState("");

  const amount = Number(amountText);
  const amountValid =
    amountText.trim() !== "" && Number.isInteger(amount) && amount !== 0;
  const reasonValid = reason.trim().length >= 1 && reason.trim().length <= 200;

  return (
    <ModalShell title="Adjust RP" mock={mock} onClose={onCancel}>
      <p className="mt-2 text-xs text-zinc-500">
        {user.email} — current balance{" "}
        <span className="font-semibold text-zinc-300">
          {user.totalPoints.toLocaleString()} RP
        </span>{" "}
        (activity {user.activityPts.toLocaleString()} / gift{" "}
        {user.giftPts.toLocaleString()})
      </p>
      <label className="mt-4 block text-xs font-medium text-zinc-400">
        Amount (positive grants, negative deducts)
        <input
          type="number"
          step={1}
          value={amountText}
          onChange={(e) => setAmountText(e.target.value)}
          placeholder="e.g. 100 or -50"
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
      </label>
      <label className="mt-3 block text-xs font-medium text-zinc-400">
        Reason (required, max 200 chars)
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value.slice(0, 200))}
          rows={3}
          placeholder="e.g. welcome grant for closed beta tester"
          className="mt-1 w-full resize-none rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <span className="mt-0.5 block text-right text-[10px] text-zinc-600">
          {reason.trim().length}/200
        </span>
      </label>
      <p className="text-[11px] leading-relaxed text-zinc-600">
        Ledger description will read{" "}
        <span className="font-mono text-zinc-500">
          admin: {reason.trim() || "<reason>"}
        </span>
        . Deductions debit activity points first, then gift points.
      </p>
      <ModalError error={error} />
      <ModalButtons
        confirmLabel={
          amountValid && amount < 0 ? "Deduct RP" : "Grant RP"
        }
        destructive={amountValid && amount < 0}
        busy={busy}
        disabled={!amountValid || !reasonValid}
        onConfirm={() => onSubmit(amount, reason.trim())}
        onCancel={onCancel}
      />
    </ModalShell>
  );
}
