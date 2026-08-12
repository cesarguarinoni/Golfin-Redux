"use client";

import { useCallback, useEffect, useState } from "react";
import { ProviderBadge } from "@/components/ProviderBadge";
import { fmtDateTime } from "@/lib/format";
import type {
  AdminUserRow,
  MutationResponse,
  UserActionKind,
  UserDetailResponse,
} from "@/lib/types";
import {
  AdjustRpModal,
  ConfirmActionModal,
  DeleteUserModal,
} from "./action-modals";

type Tab = "transactions" | "activities";

type PendingModal =
  | { kind: "action"; action: UserActionKind }
  | { kind: "delete" }
  | { kind: "rp" }
  | null;

const ACTION_COPY: Record<
  UserActionKind,
  { title: string; body: (u: AdminUserRow) => string; confirm: string; destructive: boolean }
> = {
  resend_confirmation: {
    title: "Resend confirmation email",
    body: (u) => `Resend the signup confirmation email to ${u.email}?`,
    confirm: "Resend email",
    destructive: false,
  },
  send_password_reset: {
    title: "Send password reset",
    body: (u) => `Send a password-reset email to ${u.email}?`,
    confirm: "Send reset email",
    destructive: false,
  },
  confirm_email: {
    title: "Manually confirm email",
    body: (u) =>
      `Mark ${u.email} as confirmed without the user clicking the confirmation link?`,
    confirm: "Confirm email",
    destructive: false,
  },
  ban: {
    title: "Ban user",
    body: (u) =>
      `Ban ${u.email}? Sets banned_until ≈ 100 years from now (ban_duration 876000h). The user will be unable to sign in until unbanned.`,
    confirm: "Ban user",
    destructive: true,
  },
  unban: {
    title: "Unban user",
    body: (u) => `Lift the ban on ${u.email}? They will be able to sign in again.`,
    confirm: "Unban user",
    destructive: false,
  },
};

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {label}
      </div>
      <div className="mt-0.5 text-sm text-zinc-200">{children}</div>
    </div>
  );
}

function ActionButton({
  label,
  onClick,
  tone = "default",
  disabled = false,
  title,
}: {
  label: string;
  onClick: () => void;
  tone?: "default" | "accent" | "danger";
  disabled?: boolean;
  title?: string;
}) {
  const toneCls =
    tone === "danger"
      ? "border-red-500/40 bg-red-500/10 text-red-300 hover:bg-red-500/20"
      : tone === "accent"
        ? "border-accent-500/40 bg-accent-600/15 text-accent-400 hover:bg-accent-600/25"
        : "border-surface-700 bg-surface-850 text-zinc-300 hover:bg-surface-700";
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      className={`rounded-md border px-2.5 py-1.5 text-xs font-medium transition disabled:cursor-not-allowed disabled:opacity-40 ${toneCls}`}
    >
      {label}
    </button>
  );
}

export function UserDrawer({
  user,
  mock,
  onClose,
  onMutated,
}: {
  user: AdminUserRow;
  mock: boolean;
  onClose: () => void;
  /** Re-fetches the users list; the drawer re-renders from the fresh row. */
  onMutated: () => Promise<void>;
}) {
  const [detail, setDetail] = useState<UserDetailResponse | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailVersion, setDetailVersion] = useState(0);
  const [tab, setTab] = useState<Tab>("transactions");

  const [pending, setPending] = useState<PendingModal>(null);
  const [busy, setBusy] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);

  const [editingName, setEditingName] = useState(false);
  const [nameDraft, setNameDraft] = useState(user.displayName ?? "");

  const banned =
    user.bannedUntil !== null && new Date(user.bannedUntil) > new Date();

  useEffect(() => {
    let cancelled = false;
    setDetailError(null);
    (async () => {
      try {
        const res = await fetch(`/api/users/${user.id}`);
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as UserDetailResponse;
        if (!cancelled) setDetail(json);
      } catch (err) {
        if (!cancelled)
          setDetailError(
            err instanceof Error ? err.message : "Failed to load detail"
          );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user.id, detailVersion]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape" && pending === null) onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, pending]);

  /** Shared request runner for all mutations. */
  const runMutation = useCallback(
    async (
      input: RequestInfo,
      init: RequestInit,
      opts?: { closeDrawerOnSuccess?: boolean }
    ) => {
      setBusy(true);
      setModalError(null);
      try {
        const res = await fetch(input, {
          headers: { "Content-Type": "application/json" },
          ...init,
        });
        const body = (await res.json().catch(() => null)) as
          | (MutationResponse & { error?: string })
          | null;
        if (!res.ok) {
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        setPending(null);
        setNotice({ ok: true, text: body?.message ?? "Done." });
        await onMutated();
        setDetailVersion((v) => v + 1);
        if (opts?.closeDrawerOnSuccess) onClose();
      } catch (err) {
        setModalError(err instanceof Error ? err.message : "Request failed");
      } finally {
        setBusy(false);
      }
    },
    [onClose, onMutated]
  );

  async function saveName() {
    setBusy(true);
    setNotice(null);
    try {
      const res = await fetch(`/api/users/${user.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ displayName: nameDraft }),
      });
      const body = (await res.json().catch(() => null)) as
        | (MutationResponse & { error?: string })
        | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      setEditingName(false);
      setNotice({ ok: true, text: body?.message ?? "Saved." });
      await onMutated();
    } catch (err) {
      setNotice({
        ok: false,
        text: err instanceof Error ? err.message : "Save failed",
      });
    } finally {
      setBusy(false);
    }
  }

  function openModal(next: Exclude<PendingModal, null>) {
    setModalError(null);
    setNotice(null);
    setPending(next);
  }

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      {/* Overlay */}
      <button
        type="button"
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      {/* Slide-over */}
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        <header className="flex items-start justify-between border-b border-surface-800 px-5 py-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              {editingName ? (
                <span className="flex items-center gap-1.5">
                  <input
                    value={nameDraft}
                    onChange={(e) => setNameDraft(e.target.value.slice(0, 40))}
                    maxLength={40}
                    autoFocus
                    className="w-44 rounded-md border border-accent-500/60 bg-surface-950 px-2 py-1 text-sm text-zinc-100 focus:outline-none"
                  />
                  <button
                    type="button"
                    onClick={saveName}
                    disabled={busy || nameDraft.trim().length === 0}
                    className="rounded-md bg-accent-600 px-2 py-1 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-50"
                  >
                    Save
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setEditingName(false);
                      setNameDraft(user.displayName ?? "");
                    }}
                    disabled={busy}
                    className="rounded-md border border-surface-700 px-2 py-1 text-xs text-zinc-400 hover:bg-surface-800"
                  >
                    Cancel
                  </button>
                </span>
              ) : (
                <>
                  <h2 className="truncate text-base font-semibold text-zinc-100">
                    {user.displayName ?? user.email}
                  </h2>
                  <button
                    type="button"
                    onClick={() => {
                      setNameDraft(user.displayName ?? "");
                      setEditingName(true);
                      setNotice(null);
                    }}
                    title="Edit display name (writes profiles.display_name + auth user_metadata)"
                    aria-label="Edit display name"
                    className="rounded p-1 text-zinc-500 transition hover:bg-surface-800 hover:text-accent-400"
                  >
                    <svg
                      viewBox="0 0 24 24"
                      className="h-3.5 w-3.5"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      aria-hidden
                    >
                      <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
                    </svg>
                  </button>
                </>
              )}
              {user.providers.map((p) => (
                <ProviderBadge key={p} provider={p} />
              ))}
              {banned && (
                <span className="rounded bg-red-500/20 px-1.5 py-0.5 text-[10px] font-semibold text-red-300">
                  BANNED
                </span>
              )}
            </div>
            <div className="mt-0.5 font-mono text-xs text-zinc-500">
              {user.email}
            </div>
            <div className="mt-0.5 font-mono text-[10px] text-zinc-600">
              {user.id}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 text-zinc-500 transition hover:bg-surface-800 hover:text-zinc-200"
            aria-label="Close drawer"
          >
            <svg
              viewBox="0 0 24 24"
              className="h-5 w-5"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              aria-hidden
            >
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {notice && (
            <p
              className={`mb-3 rounded-md border px-3 py-2 text-xs ${
                notice.ok
                  ? "border-accent-500/40 bg-accent-600/10 text-accent-400"
                  : "border-red-500/40 bg-red-500/10 text-red-300"
              }`}
            >
              {notice.text}
            </p>
          )}

          {/* RP summary */}
          <section className="rounded-lg border border-surface-800 bg-surface-950 p-4">
            <div className="flex items-end justify-between">
              <div>
                <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                  Reward Points (total_points)
                </div>
                <div className="mt-1 text-3xl font-bold tabular-nums text-accent-400">
                  {user.totalPoints.toLocaleString()}
                  <span className="ml-1.5 text-sm font-semibold text-zinc-500">
                    RP
                  </span>
                </div>
              </div>
              <div className="text-right text-xs text-zinc-400">
                <div>
                  activity{" "}
                  <span className="font-semibold tabular-nums text-zinc-200">
                    {user.activityPts.toLocaleString()}
                  </span>
                </div>
                <div className="mt-0.5">
                  gift{" "}
                  <span className="font-semibold tabular-nums text-zinc-200">
                    {user.giftPts.toLocaleString()}
                  </span>
                </div>
              </div>
            </div>
          </section>

          {/* Admin actions */}
          <section className="mt-4 rounded-lg border border-surface-800 bg-surface-950 p-4">
            <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
              Admin actions
            </div>
            <div className="mt-2.5 flex flex-wrap gap-2">
              <ActionButton
                label="Adjust RP"
                tone="accent"
                onClick={() => openModal({ kind: "rp" })}
              />
              <ActionButton
                label="Resend confirmation"
                disabled={user.emailConfirmedAt !== null}
                title={
                  user.emailConfirmedAt !== null
                    ? "Email already confirmed"
                    : undefined
                }
                onClick={() =>
                  openModal({ kind: "action", action: "resend_confirmation" })
                }
              />
              <ActionButton
                label="Send password reset"
                onClick={() =>
                  openModal({ kind: "action", action: "send_password_reset" })
                }
              />
              <ActionButton
                label="Confirm email"
                disabled={user.emailConfirmedAt !== null}
                title={
                  user.emailConfirmedAt !== null
                    ? "Email already confirmed"
                    : undefined
                }
                onClick={() =>
                  openModal({ kind: "action", action: "confirm_email" })
                }
              />
              {banned ? (
                <ActionButton
                  label="Unban user"
                  onClick={() => openModal({ kind: "action", action: "unban" })}
                />
              ) : (
                <ActionButton
                  label="Ban user"
                  tone="danger"
                  onClick={() => openModal({ kind: "action", action: "ban" })}
                />
              )}
              <ActionButton
                label="Delete user"
                tone="danger"
                onClick={() => openModal({ kind: "delete" })}
              />
            </div>
          </section>

          {/* Profile fields */}
          <section className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-3">
            <Field label="Avatar level">{user.avatarLevel}</Field>
            <Field label="Avatar XP">{user.avatarXp.toLocaleString()}</Field>
            <Field label="Trust level">{user.trustLevel ?? "—"}</Field>
            <Field label="Followers">{user.followersCount}</Field>
            <Field label="Following">{user.followingCount}</Field>
            <Field label="Badges">{user.badgesCount}</Field>
          </section>

          {/* Auth identity + timestamps */}
          <section className="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-2">
            <Field label="Providers">{user.providers.join(", ")}</Field>
            <Field label="Email confirmed">
              {user.emailConfirmedAt ? (
                <span className="text-accent-400">
                  ✓ {fmtDateTime(user.emailConfirmedAt)}
                </span>
              ) : (
                <span className="text-red-400">✗ unconfirmed</span>
              )}
            </Field>
            <Field label="Created">{fmtDateTime(user.createdAt)}</Field>
            <Field label="Last sign-in">{fmtDateTime(user.lastSignInAt)}</Field>
            <Field label="Banned until">
              {user.bannedUntil ? (
                <span className="text-red-400">
                  {fmtDateTime(user.bannedUntil)}
                </span>
              ) : (
                "—"
              )}
            </Field>
          </section>

          {/* Tabs */}
          <div className="mt-5 flex gap-1 border-b border-surface-800">
            {(
              [
                ["transactions", "Points ledger"],
                ["activities", "Activities"],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                onClick={() => setTab(key)}
                className={`rounded-t-md px-3 py-2 text-xs font-medium transition ${
                  tab === key
                    ? "border-b-2 border-accent-500 text-zinc-100"
                    : "text-zinc-500 hover:text-zinc-300"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <div className="py-3">
            {detailError && (
              <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                {detailError}
              </p>
            )}
            {!detail && !detailError && (
              <p className="py-6 text-center text-xs text-zinc-600">Loading…</p>
            )}
            {detail && tab === "transactions" && (
              <ul className="space-y-2">
                {detail.transactions.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    No points transactions.
                  </li>
                )}
                {detail.transactions.map((t) => (
                  <li
                    key={t.id}
                    className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2"
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-xs text-zinc-400">
                        {t.type}
                      </span>
                      <span
                        className={`text-sm font-semibold tabular-nums ${
                          t.amount < 0 ? "text-red-400" : "text-accent-400"
                        }`}
                      >
                        {t.amount > 0 ? "+" : ""}
                        {t.amount.toLocaleString()}
                        <span className="ml-1 text-[10px] font-medium uppercase text-zinc-500">
                          {t.currency}
                        </span>
                      </span>
                    </div>
                    {t.description && (
                      <div className="mt-0.5 text-xs text-zinc-300">
                        {t.description}
                      </div>
                    )}
                    <div className="mt-0.5 text-[10px] text-zinc-600">
                      {fmtDateTime(t.createdAt)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
            {detail && tab === "activities" && (
              <ul className="space-y-2">
                {detail.activities.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    No recorded activities.
                  </li>
                )}
                {detail.activities.map((a) => (
                  <li
                    key={a.id}
                    className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2"
                  >
                    <div className="text-xs text-zinc-300">{a.label}</div>
                    <div className="mt-0.5 text-[10px] text-zinc-600">
                      {fmtDateTime(a.createdAt)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <footer className="border-t border-surface-800 px-5 py-2.5 text-center text-[10px] uppercase tracking-widest text-zinc-600">
          All mutations are audited — admin_audit_log
        </footer>
      </div>

      {/* Modals */}
      {pending?.kind === "action" && (
        <ConfirmActionModal
          title={ACTION_COPY[pending.action].title}
          body={ACTION_COPY[pending.action].body(user)}
          confirmLabel={ACTION_COPY[pending.action].confirm}
          destructive={ACTION_COPY[pending.action].destructive}
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onConfirm={() =>
            runMutation(`/api/users/${user.id}/actions`, {
              method: "POST",
              body: JSON.stringify({ action: pending.action }),
            })
          }
        />
      )}
      {pending?.kind === "delete" && (
        <DeleteUserModal
          user={user}
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onConfirm={(confirmEmail) =>
            runMutation(
              `/api/users/${user.id}`,
              { method: "DELETE", body: JSON.stringify({ confirmEmail }) },
              { closeDrawerOnSuccess: true }
            )
          }
        />
      )}
      {pending?.kind === "rp" && (
        <AdjustRpModal
          user={user}
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onSubmit={(amount, reason) =>
            runMutation(`/api/users/${user.id}/rp`, {
              method: "POST",
              body: JSON.stringify({ amount, reason }),
            })
          }
        />
      )}
    </div>
  );
}
