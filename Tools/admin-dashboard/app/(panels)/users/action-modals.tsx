"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import { INVENTORY_GRANT_KINDS, type AdminUserRow } from "@/lib/types";

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
  const t = useT();
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label={t("common.close")}
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
              {t("common.mock")}
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
  const t = useT();
  return (
    <div className="mt-5 flex justify-end gap-2">
      <button
        type="button"
        onClick={onCancel}
        disabled={busy}
        className="rounded-md border border-surface-700 bg-surface-850 px-3 py-1.5 text-xs font-medium text-zinc-300 transition hover:bg-surface-700 disabled:opacity-50"
      >
        {t("common.cancel")}
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
        {busy ? t("udrawer.working") : confirmLabel}
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
  const t = useT();
  const [typed, setTyped] = useState("");
  const matches = typed.trim().toLowerCase() === user.email.toLowerCase();

  return (
    <ModalShell title={t("udel.title")} mock={mock} destructive onClose={onCancel}>
      <div className="mt-3 rounded-md border border-red-500/50 bg-red-500/10 px-3 py-2.5 text-xs leading-relaxed text-red-200">
        <p className="font-bold uppercase tracking-wide text-red-300">
          {t("udel.permanent")}
        </p>
        <p className="mt-1.5">
          {t("udel.body", { email: user.email })}
        </p>
        <ul className="mt-1.5 list-inside list-disc space-y-0.5">
          <li>
            <span className="font-mono">profiles</span>{" "}
            {t("udel.item.profile", { rp: user.totalPoints.toLocaleString() })}
          </li>
          <li>
            <span className="font-mono">points_transactions</span>{" "}
            {t("udel.item.points")}
          </li>
          <li>
            <span className="font-mono">activities</span> {t("udel.item.activities")}
          </li>
        </ul>
      </div>
      <label className="mt-4 block text-xs font-medium text-zinc-400">
        {t("udel.typeEmail")}
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
        confirmLabel={t("udel.confirm")}
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
  const t = useT();
  const [amountText, setAmountText] = useState("");
  const [reason, setReason] = useState("");

  const amount = Number(amountText);
  const amountValid =
    amountText.trim() !== "" && Number.isInteger(amount) && amount !== 0;
  const reasonValid = reason.trim().length >= 1 && reason.trim().length <= 200;

  return (
    <ModalShell title={t("urp.title")} mock={mock} onClose={onCancel}>
      <p className="mt-2 text-xs text-zinc-500">
        {user.email} — current balance{" "}
        <span className="font-semibold text-zinc-300">
          {user.totalPoints.toLocaleString()} RP
        </span>{" "}
        (activity {user.activityPts.toLocaleString()} / gift{" "}
        {user.giftPts.toLocaleString()})
      </p>
      <label className="mt-4 block text-xs font-medium text-zinc-400">
        {t("urp.amount")}
        <input
          type="number"
          step={1}
          value={amountText}
          onChange={(e) => setAmountText(e.target.value)}
          placeholder={t("urp.amountPlaceholder")}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
      </label>
      <label className="mt-3 block text-xs font-medium text-zinc-400">
        {t("urp.reason")}
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value.slice(0, 200))}
          rows={3}
          placeholder={t("urp.reasonPlaceholder")}
          className="mt-1 w-full resize-none rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
        <span className="mt-0.5 block text-right text-[10px] text-zinc-600">
          {reason.trim().length}/200
        </span>
      </label>
      <p className="text-[11px] leading-relaxed text-zinc-600">
        {t("urp.ledgerHint")}{" "}
        <span className="font-mono text-zinc-500">
          admin: {reason.trim() || "<reason>"}
        </span>{" "}
        {t("urp.ledgerHint2")}
      </p>
      <ModalError error={error} />
      <ModalButtons
        confirmLabel={
          amountValid && amount < 0 ? t("urp.deduct") : t("urp.grant")
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

// ---------------------------------------------------------------------------
// Grant inventory — kind + refId + amount, additive-only
// ---------------------------------------------------------------------------

/**
 * SPEC content_player_inventory §4, §5.
 *
 * ADDITIVE-ONLY IS ENFORCED IN THE INPUT, not just validated on submit: the
 * amount field has `min={1}` and the form refuses anything below it. A grant
 * cannot subtract — the schema CHECKs `amount > 0` and the client ignores a
 * non-positive one — so an admin who types -3 is expressing something the whole
 * system has no way to carry out, and the honest place to say so is here.
 *
 * `club` and `character` are pinned to amount 1: they are owned or not owned
 * (clubs are unique, no stacking), so "5 drivers" would deliver one.
 */
export function GrantInventoryModal({
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
  onSubmit: (kind: string, refId: string, amount: number, note: string) => void;
  onCancel: () => void;
}) {
  const t = useT();
  const [kind, setKind] = useState<string>("item");
  const [refId, setRefId] = useState("");
  const [amountText, setAmountText] = useState("1");
  const [note, setNote] = useState("");

  const unique = kind === "club" || kind === "character";
  const numeric = kind === "ticket" || kind === "hole";

  const amount = unique ? 1 : Number(amountText);
  const amountValid = Number.isInteger(amount) && amount >= 1 && amount <= 9999;
  const refValid =
    refId.trim().length >= 1 &&
    refId.trim().length <= 64 &&
    (!numeric || /^\d+$/.test(refId.trim()));

  return (
    <ModalShell title={t("ugrant.title")} mock={mock} onClose={onCancel}>
      <p className="mt-2 text-xs text-zinc-500">{user.email}</p>

      <label className="mt-4 block text-xs font-medium text-zinc-400">
        {t("ugrant.kind")}
        <select
          value={kind}
          onChange={(e) => {
            setKind(e.target.value);
            // The id space changes with the kind — a club id is not a hole
            // number. Clearing forces a deliberate re-type instead of leaving a
            // refId that means nothing in the new kind.
            setRefId("");
            setAmountText("1");
          }}
          className="mt-1 block w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 focus:border-accent-500 focus:outline-none"
        >
          {INVENTORY_GRANT_KINDS.map((k) => (
            <option key={k} value={k}>
              {k}
            </option>
          ))}
        </select>
      </label>

      <label className="mt-3 block text-xs font-medium text-zinc-400">
        {numeric ? t("ugrant.refIdNumeric") : t("ugrant.refId")}
        <input
          type="text"
          value={refId}
          onChange={(e) => setRefId(e.target.value.slice(0, 64))}
          placeholder={
            numeric ? t("ugrant.refIdNumericPlaceholder") : t("ugrant.refIdPlaceholder")
          }
          autoComplete="off"
          spellCheck={false}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 font-mono text-xs text-zinc-200 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
        />
      </label>

      <label className="mt-3 block text-xs font-medium text-zinc-400">
        {t("ugrant.amount")}
        <input
          type="number"
          min={1}
          max={9999}
          step={1}
          value={unique ? 1 : amountText}
          disabled={unique}
          onChange={(e) => setAmountText(e.target.value)}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 disabled:opacity-50 focus:border-accent-500 focus:outline-none"
        />
        <span className="mt-0.5 block text-[10px] text-zinc-600">
          {unique ? t("ugrant.amountUnique") : t("ugrant.amountHint")}
        </span>
      </label>

      <label className="mt-3 block text-xs font-medium text-zinc-400">
        {t("ugrant.note")}
        <input
          type="text"
          value={note}
          onChange={(e) => setNote(e.target.value.slice(0, 200))}
          placeholder={t("ugrant.notePlaceholder")}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
      </label>

      <p className="mt-3 text-[11px] leading-relaxed text-zinc-600">
        {t("ugrant.deliveryHint")}
      </p>

      <ModalError error={error} />
      <ModalButtons
        confirmLabel={t("ugrant.confirm")}
        busy={busy}
        disabled={!refValid || !amountValid}
        onConfirm={() => onSubmit(kind, refId.trim(), amount, note.trim())}
        onCancel={onCancel}
      />
    </ModalShell>
  );
}
