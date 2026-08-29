"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { ProviderBadge } from "@/components/ProviderBadge";
import type { DictKey } from "@/lib/i18n";
import type { PlayerMissionsResponse } from "@/lib/dailyMissionData";
import { fmtDateTime } from "@/lib/format";
import type {
  AdminUserRow,
  InventoryGrantRow,
  MutationResponse,
  PlayerInventoryResponse,
  UserActionKind,
  UserDetailResponse,
} from "@/lib/types";
import {
  AdjustRpModal,
  ConfirmActionModal,
  DeleteUserModal,
  GrantInventoryModal,
} from "./action-modals";
import { InventoryTab } from "./inventory-tab";
import { MissionsTab } from "./missions-tab";

type Tab = "transactions" | "activities" | "inventory" | "missions";

type PendingModal =
  | { kind: "action"; action: UserActionKind }
  | { kind: "delete" }
  | { kind: "rp" }
  | { kind: "grant" }
  /** Revoke a grant that has NOT drained yet (PLAN §6.5 decision 3). Carries the whole row so the
   *  confirm can name what is being taken back out of the queue. */
  | { kind: "revokeGrant"; grant: InventoryGrantRow }
  /** Reset one mission's progress (missions_v1 §A6). Carries the id so the
   *  confirm can name what is being wiped — a reset makes the player's next
   *  clear pay the FIRST-CLEAR amount again, which is the whole reason it needs
   *  a confirm rather than being a one-click button. */
  | { kind: "resetMission"; missionId: string }
  | null;

const ACTION_COPY: Record<
  UserActionKind,
  { titleKey: DictKey; bodyKey: DictKey; confirmKey: DictKey; destructive: boolean }
> = {
  resend_confirmation: {
    titleKey: "uact.resend_confirmation.title",
    bodyKey: "uact.resend_confirmation.body",
    confirmKey: "uact.resend_confirmation.confirm",
    destructive: false,
  },
  send_password_reset: {
    titleKey: "uact.send_password_reset.title",
    bodyKey: "uact.send_password_reset.body",
    confirmKey: "uact.send_password_reset.confirm",
    destructive: false,
  },
  confirm_email: {
    titleKey: "uact.confirm_email.title",
    bodyKey: "uact.confirm_email.body",
    confirmKey: "uact.confirm_email.confirm",
    destructive: false,
  },
  ban: {
    titleKey: "uact.ban.title",
    bodyKey: "uact.ban.body",
    confirmKey: "uact.ban.confirm",
    destructive: true,
  },
  unban: {
    titleKey: "uact.unban.title",
    bodyKey: "uact.unban.body",
    confirmKey: "uact.unban.confirm",
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
  const t = useT();
  const [detail, setDetail] = useState<UserDetailResponse | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [inventory, setInventory] = useState<PlayerInventoryResponse | null>(null);
  const [inventoryError, setInventoryError] = useState<string | null>(null);
  const [missions, setMissions] = useState<PlayerMissionsResponse | null>(null);
  const [missionsError, setMissionsError] = useState<string | null>(null);
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
            err instanceof Error ? err.message : t("udrawer.loadFailed")
          );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user.id, detailVersion]);

  // Fetched alongside the detail, not lazily on tab-open: the drawer's whole
  // job is answering a support question, and a second click before the answer
  // appears is the kind of friction that makes an operator go query Supabase by
  // hand instead. Re-runs on detailVersion so a grant shows up immediately.
  useEffect(() => {
    let cancelled = false;
    setInventoryError(null);
    (async () => {
      try {
        const res = await fetch(`/api/users/${user.id}/inventory`);
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as PlayerInventoryResponse;
        if (!cancelled) setInventory(json);
      } catch (err) {
        if (!cancelled)
          setInventoryError(
            err instanceof Error ? err.message : t("udrawer.loadFailed")
          );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user.id, detailVersion]);

  // Fetched with the detail rather than lazily on tab-open, for the reason the
  // inventory effect gives: the drawer's job is answering a support question,
  // and a second click before the answer appears is what makes an operator go
  // query Supabase by hand. Re-runs on detailVersion so a RESET shows at once.
  useEffect(() => {
    let cancelled = false;
    setMissionsError(null);
    (async () => {
      try {
        const res = await fetch(`/api/users/${user.id}/missions`);
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as { error?: string } | null;
          throw new Error(body?.error ?? `Request failed (${res.status})`);
        }
        const json = (await res.json()) as PlayerMissionsResponse;
        if (!cancelled) setMissions(json);
      } catch (err) {
        if (!cancelled)
          setMissionsError(err instanceof Error ? err.message : t("udrawer.loadFailed"));
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
        setNotice({ ok: true, text: body?.message ?? t("common.done") });
        await onMutated();
        setDetailVersion((v) => v + 1);
        if (opts?.closeDrawerOnSuccess) onClose();
      } catch (err) {
        setModalError(err instanceof Error ? err.message : t("common.requestFailed"));
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
      setNotice({ ok: true, text: body?.message ?? t("te.saved") });
      await onMutated();
    } catch (err) {
      setNotice({
        ok: false,
        text: err instanceof Error ? err.message : t("te.saveFailed"),
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
        aria-label={t("udrawer.close")}
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
                    {t("common.save")}
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
                    {t("common.cancel")}
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
                    title={t("udrawer.editNameHint")}
                    aria-label={t("udrawer.editName")}
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
                  {t("udrawer.banned")}
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
            aria-label={t("udrawer.close")}
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
                  {t("udrawer.rp")}
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
              {t("udrawer.adminActions")}
            </div>
            <div className="mt-2.5 flex flex-wrap gap-2">
              <ActionButton
                label={t("udrawer.action.rp")}
                tone="accent"
                onClick={() => openModal({ kind: "rp" })}
              />
              <ActionButton
                label={t("udrawer.action.grant")}
                tone="accent"
                onClick={() => openModal({ kind: "grant" })}
              />
              <ActionButton
                label={t("udrawer.action.resendConfirmation")}
                disabled={user.emailConfirmedAt !== null}
                title={
                  user.emailConfirmedAt !== null
                    ? t("udrawer.alreadyConfirmed")
                    : undefined
                }
                onClick={() =>
                  openModal({ kind: "action", action: "resend_confirmation" })
                }
              />
              <ActionButton
                label={t("udrawer.action.sendPasswordReset")}
                onClick={() =>
                  openModal({ kind: "action", action: "send_password_reset" })
                }
              />
              <ActionButton
                label={t("udrawer.action.confirmEmail")}
                disabled={user.emailConfirmedAt !== null}
                title={
                  user.emailConfirmedAt !== null
                    ? t("udrawer.alreadyConfirmed")
                    : undefined
                }
                onClick={() =>
                  openModal({ kind: "action", action: "confirm_email" })
                }
              />
              {banned ? (
                <ActionButton
                  label={t("udrawer.action.unban")}
                  onClick={() => openModal({ kind: "action", action: "unban" })}
                />
              ) : (
                <ActionButton
                  label={t("udrawer.action.ban")}
                  tone="danger"
                  onClick={() => openModal({ kind: "action", action: "ban" })}
                />
              )}
              <ActionButton
                label={t("udrawer.action.delete")}
                tone="danger"
                onClick={() => openModal({ kind: "delete" })}
              />
            </div>
          </section>

          {/* Profile fields */}
          <section className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-3">
            <Field label={t("udrawer.field.avatarLevel")}>{user.avatarLevel}</Field>
            <Field label={t("udrawer.field.avatarXp")}>{user.avatarXp.toLocaleString()}</Field>
            <Field label={t("udrawer.field.trustLevel")}>{user.trustLevel ?? "—"}</Field>
            <Field label={t("udrawer.field.followers")}>{user.followersCount}</Field>
            <Field label={t("udrawer.field.following")}>{user.followingCount}</Field>
            <Field label={t("udrawer.field.badges")}>{user.badgesCount}</Field>
          </section>

          {/* Auth identity + timestamps */}
          <section className="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-surface-800 bg-surface-950 p-4 sm:grid-cols-2">
            <Field label={t("udrawer.field.providers")}>{user.providers.join(", ")}</Field>
            <Field label={t("udrawer.field.emailConfirmed")}>
              {user.emailConfirmedAt ? (
                <span className="text-accent-400">
                  ✓ {fmtDateTime(user.emailConfirmedAt)}
                </span>
              ) : (
                <span className="text-red-400">✗ {t("udrawer.unconfirmed")}</span>
              )}
            </Field>
            <Field label={t("udrawer.field.created")}>{fmtDateTime(user.createdAt)}</Field>
            <Field label={t("udrawer.field.lastSignIn")}>{fmtDateTime(user.lastSignInAt)}</Field>
            <Field label={t("udrawer.field.bannedUntil")}>
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
                ["transactions", "udrawer.tab.transactions"],
                ["activities", "udrawer.tab.activities"],
                ["inventory", "udrawer.tab.inventory"],
                ["missions", "udrawer.tab.missions"],
              ] as const
            ).map(([key, labelKey]) => (
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
                {t(labelKey)}
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
              <p className="py-6 text-center text-xs text-zinc-600">{t("common.loading")}</p>
            )}
            {detail && tab === "transactions" && (
              <ul className="space-y-2">
                {detail.transactions.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    {t("udrawer.noTx")}
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
            {tab === "inventory" && (
              <>
                {inventoryError && (
                  <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                    {inventoryError}
                  </p>
                )}
                {!inventory && !inventoryError && (
                  <p className="py-6 text-center text-xs text-zinc-600">
                    {t("common.loading")}
                  </p>
                )}
                {inventory && (
                  <InventoryTab
                    data={inventory}
                    onRevokeGrant={(grant) => {
                      setNotice(null);
                      setPending({ kind: "revokeGrant", grant });
                    }}
                  />
                )}
              </>
            )}
            {tab === "missions" && (
              <>
                {missionsError && (
                  <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
                    {missionsError}
                  </p>
                )}
                {!missions && !missionsError && (
                  <p className="py-6 text-center text-xs text-zinc-600">
                    {t("common.loading")}
                  </p>
                )}
                {missions && (
                  <MissionsTab
                    data={missions}
                    onReset={(missionId) => {
                      setNotice(null);
                      setPending({ kind: "resetMission", missionId });
                    }}
                  />
                )}
              </>
            )}
            {detail && tab === "activities" && (
              <ul className="space-y-2">
                {detail.activities.length === 0 && (
                  <li className="py-6 text-center text-xs text-zinc-600">
                    {t("udrawer.noActivities")}
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
          {t("udrawer.audited")}
        </footer>
      </div>

      {/* Modals */}
      {pending?.kind === "action" && (
        <ConfirmActionModal
          title={t(ACTION_COPY[pending.action].titleKey)}
          body={t(ACTION_COPY[pending.action].bodyKey, { email: user.email })}
          confirmLabel={t(ACTION_COPY[pending.action].confirmKey)}
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
      {pending?.kind === "grant" && (
        <GrantInventoryModal
          user={user}
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onSubmit={(kind, refId, amount, note) =>
            runMutation(`/api/users/${user.id}/inventory`, {
              method: "POST",
              body: JSON.stringify({ kind, refId, amount, note }),
            })
          }
        />
      )}
      {pending?.kind === "revokeGrant" && (
        <ConfirmActionModal
          title={t("urevoke.title")}
          body={t("urevoke.body", {
            amount: pending.grant.amount,
            refId: pending.grant.refId,
            kind: pending.grant.kind,
          })}
          confirmLabel={t("urevoke.confirm")}
          destructive
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onConfirm={() =>
            runMutation(`/api/users/${user.id}/inventory`, {
              method: "DELETE",
              body: JSON.stringify({ grantId: pending.grant.id }),
            })
          }
        />
      )}
      {pending?.kind === "resetMission" && (
        <ConfirmActionModal
          title={t("umis.resetTitle").replace("{0}", pending.missionId)}
          body={t("umis.resetBody")}
          confirmLabel={t("umis.reset")}
          destructive
          mock={mock}
          busy={busy}
          error={modalError}
          onCancel={() => setPending(null)}
          onConfirm={() =>
            runMutation(`/api/users/${user.id}/missions`, {
              method: "DELETE",
              body: JSON.stringify({ missionId: pending.missionId }),
            })
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
