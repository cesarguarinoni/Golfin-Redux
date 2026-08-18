"use client";

import { useState } from "react";
import { deriveNoticeState, NOTICE_LIMITS, resolveForLocale, validateNoticeInput } from "@/lib/notice";
import { useT } from "@/components/I18nProvider";
import type { NoticeInput, NoticeRow } from "@/lib/types";

/** ISO → the value a datetime-local input wants, in UTC. Same helpers as the banner editor. */
function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toISOString().slice(0, 16);
}
function fromLocalInput(value: string): string | null {
  return value ? `${value}:00.000Z` : null;
}

function blankDraft(): NoticeInput {
  return {
    label: "",
    titleEn: "",
    titleJa: null,
    bodyEn: "",
    bodyJa: null,
    startAt: null,
    endAt: null,
    sortOrder: 0,
    // Drafts start OFF — saving half-written copy must not publish it to every
    // player mid-edit (the same reason the column defaults false).
    isActive: false,
  };
}

function toDraft(n: NoticeRow): NoticeInput {
  return {
    label: n.label,
    titleEn: n.titleEn,
    titleJa: n.titleJa,
    bodyEn: n.bodyEn,
    bodyJa: n.bodyJa,
    startAt: n.startAt,
    endAt: n.endAt,
    sortOrder: n.sortOrder,
    isActive: n.isActive,
  };
}

const label = "block text-[11px] font-medium uppercase tracking-wider text-zinc-500";
const field =
  "mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none";

/** Character counter that turns amber as it approaches the cap and red past it. */
function Counter({ value, cap }: { value: string; cap: number }) {
  const n = value.length;
  const tone = n > cap ? "text-red-400" : n > cap * 0.85 ? "text-amber-400" : "text-zinc-600";
  return (
    <span className={`text-[10px] tabular-nums ${tone}`}>
      {n}/{cap}
    </span>
  );
}

/**
 * What a player actually sees, in both languages, side by side. This exists
 * because the JA fallback is the part operators get wrong: leaving Japanese
 * blank does not hide the notice from JP players, it shows them the English.
 * Rendered with the same line breaks the device will use.
 */
function Preview({ draft }: { draft: NoticeInput }) {
  const t = useT();
  const row = {
    titleEn: draft.titleEn,
    titleJa: draft.titleJa,
    bodyEn: draft.bodyEn,
    bodyJa: draft.bodyJa,
  };
  const en = resolveForLocale(row, false);
  const ja = resolveForLocale(row, true);
  const usingFallback =
    !(draft.titleJa ?? "").trim() || !(draft.bodyJa ?? "").trim();

  return (
    <div className="mt-4">
      <div className={label}>{t("notice.preview")}</div>
      <div className="mt-2 grid grid-cols-2 gap-3">
        {([
          ["EN", en],
          ["JA", ja],
        ] as const).map(([tag, c]) => {
          return (
            <div
              key={tag}
              className="rounded-lg border border-surface-700 bg-surface-950 px-3 py-2.5"
            >
              <div className="text-[10px] font-bold tracking-wider text-zinc-600">{tag}</div>
              <div className="mt-1 text-xs font-bold uppercase text-zinc-200">
                {c.title || <span className="font-normal normal-case text-zinc-600">—</span>}
              </div>
              <div className="mt-1 whitespace-pre-line text-[11px] leading-relaxed text-zinc-400">
                {c.body || "—"}
              </div>
            </div>
          );
        })}
      </div>
      {usingFallback && (
        <p className="mt-1.5 text-[11px] text-zinc-500">{t("notice.fallbackHint")}</p>
      )}
    </div>
  );
}

export function NoticeEditor({
  notice,
  mock,
  onClose,
  onSaved,
}: {
  /** null = create a new notice. */
  notice: NoticeRow | null;
  mock: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const t = useT();
  const isNew = notice === null;
  const [draft, setDraft] = useState<NoticeInput>(() => (notice ? toDraft(notice) : blankDraft()));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmLabel, setConfirmLabel] = useState("");
  const [danger, setDanger] = useState(false);

  const state = notice ? deriveNoticeState(notice, Date.now()) : "OFF";
  /** Switching a LIVE notice off is player-facing and instant — typed confirm. */
  const needsConfirm = notice !== null && state === "LIVE" && !draft.isActive;
  // Shown inline rather than only on submit: the caps are invisible on a phone
  // otherwise, and this is the field the operator is looking at.
  const validationError = validateNoticeInput(draft);

  function patch(next: Partial<NoticeInput>) {
    setDraft((d) => ({ ...d, ...next }));
    setError(null);
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const payload: NoticeInput = {
        ...draft,
        label: draft.label.trim(),
        confirmLabel: needsConfirm ? confirmLabel : undefined,
      };
      const res = await fetch(isNew ? "/api/notices" : `/api/notices/${notice!.id}`, {
        method: isNew ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("notice.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("notice.saveFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function runDelete() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/notices/${notice!.id}`, {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ confirmLabel }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("notice.deleted"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("notice.deleteFailed"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      <button
        type="button"
        aria-label={t("common.close")}
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      <div className="absolute right-0 top-0 flex h-full w-full max-w-2xl flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        <header className="border-b border-surface-800 px-5 py-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h2 className="truncate text-base font-semibold text-zinc-100">
                {isNew ? t("notice.new") : draft.label || t("common.none")}
              </h2>
              {mock && (
                <span className="mt-1 inline-block rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-yellow-300 ring-1 ring-yellow-600/40">
                  MOCK
                </span>
              )}
            </div>
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-surface-700 px-2.5 py-1 text-xs text-zinc-400 hover:bg-surface-800"
            >
              {t("common.close")}
            </button>
          </div>

          {needsConfirm && (
            <div className="mt-3 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
              <strong className="font-semibold">{t("notice.isLive")}</strong>{" "}
              {t("notice.liveConfirmHint")}
              <input
                value={confirmLabel}
                onChange={(e) => setConfirmLabel(e.target.value)}
                placeholder={notice!.label}
                className="mt-2 w-full rounded-md border border-amber-500/50 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
              />
            </div>
          )}
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {/* Active switch */}
          <div
            className={`flex items-start justify-between gap-4 rounded-lg border px-3 py-2.5 ${
              draft.isActive
                ? "border-accent-500/40 bg-accent-500/10"
                : "border-zinc-600/50 bg-surface-850"
            }`}
          >
            <div className="min-w-0">
              <div
                className={`text-xs font-semibold ${
                  draft.isActive ? "text-accent-300" : "text-zinc-400"
                }`}
              >
                {draft.isActive ? t("notice.activeOn") : t("notice.draft")}
              </div>
              <p className="mt-0.5 text-[11px] leading-relaxed text-zinc-500">
                {t("notice.activeHint")}
              </p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={draft.isActive}
              aria-label={t("notice.activeOn")}
              onClick={() => patch({ isActive: !draft.isActive })}
              className={`mt-0.5 flex h-6 w-11 shrink-0 items-center rounded-full transition ${
                draft.isActive ? "bg-accent-600" : "bg-surface-700"
              }`}
            >
              <span
                className={`h-5 w-5 rounded-full bg-white transition ${
                  draft.isActive ? "translate-x-[22px]" : "translate-x-[2px]"
                }`}
              />
            </button>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-x-4 gap-y-4">
            {/* Label */}
            <div className="col-span-2">
              <label className={label} htmlFor="n-label">
                {t("notice.label")}
              </label>
              <input
                id="n-label"
                value={draft.label}
                onChange={(e) => patch({ label: e.target.value })}
                placeholder="August maintenance window"
                className={field}
              />
              <p className="mt-1 text-[11px] text-zinc-600">{t("notice.labelHint")}</p>
            </div>

            {/* Titles */}
            <div>
              <div className="flex items-baseline justify-between">
                <label className={label} htmlFor="n-title-en">
                  {t("notice.titleEn")}
                </label>
                <Counter value={draft.titleEn} cap={NOTICE_LIMITS.title} />
              </div>
              <input
                id="n-title-en"
                value={draft.titleEn}
                onChange={(e) => patch({ titleEn: e.target.value })}
                placeholder="MAINTENANCE NOTICE"
                className={field}
              />
            </div>
            <div>
              <div className="flex items-baseline justify-between">
                <label className={label} htmlFor="n-title-ja">
                  {t("notice.titleJa")}
                </label>
                <Counter value={draft.titleJa ?? ""} cap={NOTICE_LIMITS.title} />
              </div>
              <input
                id="n-title-ja"
                value={draft.titleJa ?? ""}
                onChange={(e) => patch({ titleJa: e.target.value || null })}
                placeholder="メンテナンス情報"
                className={field}
              />
            </div>

            {/* Bodies */}
            <div>
              <div className="flex items-baseline justify-between">
                <label className={label} htmlFor="n-body-en">
                  {t("notice.bodyEn")}
                </label>
                <Counter value={draft.bodyEn} cap={NOTICE_LIMITS.body} />
              </div>
              <textarea
                id="n-body-en"
                rows={5}
                value={draft.bodyEn}
                onChange={(e) => patch({ bodyEn: e.target.value })}
                placeholder={"Scheduled server maintenance: 2026/08/28\nThe game will not be available for a short time."}
                className={`${field} resize-y leading-relaxed`}
              />
            </div>
            <div>
              <div className="flex items-baseline justify-between">
                <label className={label} htmlFor="n-body-ja">
                  {t("notice.bodyJa")}
                </label>
                <Counter value={draft.bodyJa ?? ""} cap={NOTICE_LIMITS.body} />
              </div>
              <textarea
                id="n-body-ja"
                rows={5}
                value={draft.bodyJa ?? ""}
                onChange={(e) => patch({ bodyJa: e.target.value || null })}
                placeholder={"定期サーバーメンテナンス: 2026/08/28"}
                className={`${field} resize-y leading-relaxed`}
              />
            </div>

            <div className="col-span-2">
              <p className="text-[11px] leading-relaxed text-zinc-600">{t("notice.textHint")}</p>
            </div>

            {/* Window */}
            <div>
              <label className={label} htmlFor="n-start">
                {t("notice.start")}
              </label>
              <input
                id="n-start"
                type="datetime-local"
                value={toLocalInput(draft.startAt)}
                onChange={(e) => patch({ startAt: fromLocalInput(e.target.value) })}
                className={field}
              />
            </div>
            <div>
              <label className={label} htmlFor="n-end">
                {t("notice.end")}
              </label>
              <input
                id="n-end"
                type="datetime-local"
                value={toLocalInput(draft.endAt)}
                onChange={(e) => patch({ endAt: fromLocalInput(e.target.value) })}
                className={field}
              />
              <p className="mt-1 text-[11px] leading-relaxed text-zinc-600">
                {t("notice.endHint")}
              </p>
            </div>

            {/* Sort */}
            <div>
              <label className={label} htmlFor="n-sort">
                {t("notice.sortOrder")}
              </label>
              <input
                id="n-sort"
                type="number"
                value={draft.sortOrder}
                onChange={(e) => patch({ sortOrder: Number(e.target.value) || 0 })}
                className={field}
              />
              <p className="mt-1 text-[11px] leading-relaxed text-zinc-600">
                {t("notice.sortHint")}
              </p>
            </div>
          </div>

          <Preview draft={draft} />

          {validationError && (
            <p className="mt-3 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
              {validationError}
            </p>
          )}
          {error && (
            <p className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
              {error}
            </p>
          )}

          {!isNew && (
            <div className="mt-8 rounded-lg border border-red-500/30 bg-red-500/5 px-3 py-3">
              {danger ? (
                <>
                  <p className="text-xs text-red-300">
                    {t("notice.deleteConfirmType")}{" "}
                    <code className="font-mono">{notice!.label}</code>{" "}
                    {t("notice.deleteConfirmHint")}
                  </p>
                  <input
                    value={confirmLabel}
                    onChange={(e) => setConfirmLabel(e.target.value)}
                    className="mt-2 w-full rounded-md border border-red-500/40 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
                  />
                  <div className="mt-2 flex gap-2">
                    <button
                      type="button"
                      disabled={busy || confirmLabel.trim() !== notice!.label}
                      onClick={() => void runDelete()}
                      className="rounded-md bg-red-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-red-500 disabled:opacity-40"
                    >
                      {t("common.delete")}
                    </button>
                    <button
                      type="button"
                      onClick={() => setDanger(false)}
                      className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
                    >
                      {t("common.cancel")}
                    </button>
                  </div>
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => setDanger(true)}
                  className="text-xs font-medium text-red-400 hover:text-red-300"
                >
                  {t("notice.deleteNotice")}
                </button>
              )}
            </div>
          )}
        </div>

        <footer className="flex items-center justify-end gap-2 border-t border-surface-800 px-5 py-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
          >
            {t("common.cancel")}
          </button>
          <button
            type="button"
            disabled={busy || validationError !== null}
            onClick={() => void save()}
            className="rounded-md bg-accent-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
          >
            {busy ? t("notice.saving") : isNew ? t("notice.create") : t("common.save")}
          </button>
        </footer>
      </div>
    </div>
  );
}
