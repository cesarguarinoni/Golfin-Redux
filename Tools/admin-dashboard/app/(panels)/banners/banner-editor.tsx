"use client";

import { useMemo, useRef, useState } from "react";
import {
  ALLOWED_LINK_HOSTS,
  BANNER_ART_SPEC,
  BANNER_PLACEMENTS,
  bannerSpec,
  deriveBannerState,
  isAssignedPlacement,
  validateBannerLinkUrl,
} from "@/lib/banner";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import type { BannerInput, BannerPlacement, BannerRow } from "@/lib/types";

/** ISO → the value a datetime-local input wants, in UTC. Same helpers as the tournament editor. */
function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toISOString().slice(0, 16);
}
function fromLocalInput(value: string): string | null {
  return value ? `${value}:00.000Z` : null;
}

function blankDraft(): BannerInput {
  return {
    placement: "home_promo",
    label: "",
    imageUrlEn: null,
    imageUrlJa: null,
    linkUrl: null,
    startAt: null,
    endAt: null,
    sortOrder: 0,
    // Drafts start OFF — saving a half-built banner must not publish it to
    // every player mid-edit (the same reason the column defaults false).
    isActive: false,
  };
}

function toDraft(b: BannerRow): BannerInput {
  return {
    placement: b.placement,
    label: b.label,
    imageUrlEn: b.imageUrlEn,
    imageUrlJa: b.imageUrlJa,
    linkUrl: b.linkUrl,
    startAt: b.startAt,
    endAt: b.endAt,
    sortOrder: b.sortOrder,
    isActive: b.isActive,
  };
}

const label = "block text-[11px] font-medium uppercase tracking-wider text-zinc-500";
const field =
  "mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none";

export function BannerEditor({
  banner,
  mock,
  onClose,
  onSaved,
  assignedTo = [],
}: {
  /** null = create a new banner. */
  banner: BannerRow | null;
  mock: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
  /** Slugs of the tournaments pointing at this banner (tournament_modal only). */
  assignedTo?: string[];
}) {
  const t = useT();
  const isNew = banner === null;
  const [draft, setDraft] = useState<BannerInput>(() => (banner ? toDraft(banner) : blankDraft()));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmLabel, setConfirmLabel] = useState("");
  const [danger, setDanger] = useState(false);

  const state = banner ? deriveBannerState(banner, Date.now()) : "OFF";
  // tournament_modal is chosen per tournament, so scheduling and ordering do not
  // apply to it — the fields are hidden rather than shown doing nothing.
  const assignedPlacement = isAssignedPlacement(draft.placement);
  /** Switching a LIVE banner off is player-facing and instant — typed confirm. */
  const needsConfirm = banner !== null && state === "LIVE" && !draft.isActive;

  const spec = bannerSpec(draft.placement);
  const linkError = useMemo(
    () => (draft.linkUrl ? validateBannerLinkUrl(draft.linkUrl) : null),
    [draft.linkUrl]
  );

  function patch(next: Partial<BannerInput>) {
    setDraft((d) => ({ ...d, ...next }));
    setError(null);
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const payload: BannerInput = {
        ...draft,
        label: draft.label.trim(),
        linkUrl: draft.linkUrl?.trim() || null,
        confirmLabel: needsConfirm ? confirmLabel : undefined,
      };
      const res = await fetch(isNew ? "/api/banners" : `/api/banners/${banner!.id}`, {
        method: isNew ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("ban.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("ban.saveFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function runDelete() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/banners/${banner!.id}`, {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ confirmLabel }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("ban.deleted"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("ban.deleteFailed"));
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
                {isNew ? t("ban.new") : draft.label || t("common.none")}
              </h2>
              <div className="mt-1 flex items-center gap-2">
                <code className="text-xs text-zinc-500">{draft.placement}</code>
                {mock && (
                  <span className="rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-yellow-300 ring-1 ring-yellow-600/40">
                    MOCK
                  </span>
                )}
              </div>
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
              <strong className="font-semibold">{t("ban.isLive")}</strong>{" "}
              {t("ban.liveConfirmHint")}
              <input
                value={confirmLabel}
                onChange={(e) => setConfirmLabel(e.target.value)}
                placeholder={banner!.label}
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
                {draft.isActive
                  ? t("ban.activeOn")
                  : t("ban.draft")}
              </div>
              <p className="mt-0.5 text-[11px] leading-relaxed text-zinc-500">
                {t("ban.activeHint")}
              </p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={draft.isActive}
              aria-label={t("ban.activeOn")}
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

          <div className="mt-4 grid grid-cols-2 gap-4">
            <div className="col-span-1">
              <label className={label} htmlFor="b-placement">
                {t("ban.placement")}
              </label>
              <select
                id="b-placement"
                value={draft.placement}
                onChange={(e) => patch({ placement: e.target.value as BannerPlacement })}
                className={field}
              >
                {BANNER_PLACEMENTS.map((p) => (
                  <option key={p} value={p}>
                    {t(`ban.placement.${p}` as DictKey)}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-[11px] text-zinc-600">{spec.where}</p>
            </div>

            <div className={assignedPlacement ? "hidden" : "col-span-1"}>
              <label className={label} htmlFor="b-sort">
                {t("ban.sortOrder")}
              </label>
              <input
                id="b-sort"
                type="number"
                value={draft.sortOrder}
                onChange={(e) => patch({ sortOrder: Number(e.target.value) })}
                className={field}
              />
              <p className="mt-1 text-[11px] text-zinc-600">
                {t("ban.sortHint")}
              </p>
            </div>

            <div className="col-span-2">
              <label className={label} htmlFor="b-label">
                {t("ban.label")}
              </label>
              <input
                id="b-label"
                value={draft.label}
                onChange={(e) => patch({ label: e.target.value })}
                placeholder="August GPS campaign"
                className={field}
              />
              <p className="mt-1 text-[11px] text-zinc-600">
                {t("ban.labelHint")}
              </p>
            </div>

            <div className="col-span-2">
              <label className={label} htmlFor="b-link">
                {t("ban.linkUrl")}
              </label>
              <input
                id="b-link"
                value={draft.linkUrl ?? ""}
                onChange={(e) => patch({ linkUrl: e.target.value || null })}
                placeholder="https://golfin.io/campaign/august"
                className={field}
              />
              {linkError ? (
                <p className="mt-1 rounded-md border border-red-500/40 bg-red-500/10 px-2.5 py-1.5 text-[11px] text-red-300">
                  {linkError}
                </p>
              ) : (
                <p className="mt-1 text-[11px] leading-relaxed text-zinc-600">
                  {t("ban.linkHint", { hosts: ALLOWED_LINK_HOSTS.join(", ") })}
                </p>
              )}
            </div>

            <div className={assignedPlacement ? "hidden" : "col-span-1"}>
              <label className={label} htmlFor="b-start">
                {t("ban.start")}
              </label>
              <input
                id="b-start"
                type="datetime-local"
                value={toLocalInput(draft.startAt)}
                onChange={(e) => patch({ startAt: fromLocalInput(e.target.value) })}
                className={field}
              />
            </div>
            <div className={assignedPlacement ? "hidden" : "col-span-1"}>
              <label className={label} htmlFor="b-end">
                {t("ban.end")}
              </label>
              <input
                id="b-end"
                type="datetime-local"
                value={toLocalInput(draft.endAt)}
                onChange={(e) => patch({ endAt: fromLocalInput(e.target.value) })}
                className={field}
              />
              <p className="mt-1 text-[11px] text-zinc-600">
                {t("ban.exclusiveHint")}
              </p>
            </div>

            {assignedPlacement && (
              <div className="col-span-2 rounded-md border border-surface-700 bg-surface-900 px-3 py-2.5 text-[11px] leading-relaxed text-zinc-500">
                <strong className="font-semibold text-zinc-300">
                  {t("ban.assignedNotScheduled")}
                </strong>{" "}
                {t("ban.assignedHint")}
                {assignedTo.length > 0 && (
                  <>
                    {" "}
                    {t("ban.assignedNow", { count: assignedTo.length })}{" "}
                    <span className="font-mono text-zinc-400">{assignedTo.join(", ")}</span>
                  </>
                )}
              </div>
            )}
          </div>

          <div className="mt-6 grid grid-cols-2 gap-5">
            <ArtSlot
              locale="en"
              title={t("ban.artEn")}
              placement={draft.placement}
              url={draft.imageUrlEn}
              onChange={(url) => patch({ imageUrlEn: url })}
              onNotice={setNotice}
            />
            <ArtSlot
              locale="ja"
              title={t("ban.artJa")}
              placement={draft.placement}
              url={draft.imageUrlJa}
              onChange={(url) => patch({ imageUrlJa: url })}
              onNotice={setNotice}
            />
          </div>

          <p className="mt-3 text-[11px] leading-relaxed text-zinc-600">
            {t("ban.artHint", {
              sprite: spec.sprite.split("/").pop() ?? "",
              maxKb: BANNER_ART_SPEC.maxBytes / 1024,
              w: spec.width,
              h: spec.height,
            })}
          </p>

          {notice && (
            <p className="mt-3 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
              {notice}
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
                  {assignedTo.length > 0 && (
                    <p className="mb-2 rounded-md border border-red-500/50 bg-red-500/15 px-2.5 py-2 text-xs text-red-200">
                      <strong className="font-semibold">
                        {t("ban.deleteAssignedWarn", { count: assignedTo.length })}
                      </strong>{" "}
                      <span className="font-mono">{assignedTo.join(", ")}</span>{" "}
                      {t("ban.deleteAssignedBody")}
                    </p>
                  )}
                  <p className="text-xs text-red-300">
                    {t("ban.deleteConfirmType")}{" "}
                    <code className="font-mono">{banner!.label}</code>{" "}
                    {t("ban.deleteConfirmHint")}
                  </p>
                  <input
                    value={confirmLabel}
                    onChange={(e) => setConfirmLabel(e.target.value)}
                    className="mt-2 w-full rounded-md border border-red-500/40 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
                  />
                  <div className="mt-2 flex gap-2">
                    <button
                      type="button"
                      disabled={busy || confirmLabel.trim() !== banner!.label}
                      onClick={() => void runDelete()}
                      className="rounded-md bg-red-600 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-40"
                    >
                      Delete
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
                  {t("ban.deleteBanner")}
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
            disabled={busy || linkError !== null}
            onClick={() => void save()}
            className="rounded-md bg-accent-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
          >
            {busy ? t("ban.saving") : isNew ? t("ban.create") : t("common.save")}
          </button>
        </footer>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------

function ArtSlot({
  locale,
  title,
  placement,
  url,
  onChange,
  onNotice,
}: {
  locale: "en" | "ja";
  title: string;
  placement: BannerPlacement;
  url: string | null;
  onChange: (url: string | null) => void;
  onNotice: (message: string | null) => void;
}) {
  const t = useT();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [aspectWarning, setAspectWarning] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);

  const spec = bannerSpec(placement);

  async function onFile(file: File) {
    setLocalError(null);
    setAspectWarning(null);
    onNotice(null);

    if (!(BANNER_ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
      setLocalError(t("art.unsupportedType", { type: file.type || "unknown" }));
      return;
    }
    if (file.size > BANNER_ART_SPEC.maxBytes) {
      setLocalError(
        t("art.tooBig", {
          kb: (file.size / 1024).toFixed(0),
          cap: BANNER_ART_SPEC.maxBytes / 1024,
        })
      );
      return;
    }

    const objectUrl = URL.createObjectURL(file);
    setPreview(objectUrl);

    // Aspect drift warns, never blocks — the slot's RectTransform does not
    // change, so an off-ratio image is cropped or letterboxed, not broken.
    await new Promise<void>((resolve) => {
      const img = new Image();
      img.onload = () => {
        const ratio = img.width / img.height;
        const drift = Math.abs(ratio - spec.aspect) / spec.aspect;
        if (drift > BANNER_ART_SPEC.aspectTolerance) {
          setAspectWarning(
            t("art.aspectWarn", {
              w: img.width,
              h: img.height,
              ratio: ratio.toFixed(2),
              sw: spec.width,
              sh: spec.height,
              saspect: spec.aspect.toFixed(2),
            })
          );
        }
        resolve();
      };
      img.onerror = () => resolve();
      img.src = objectUrl;
    });

    setBusy(true);
    try {
      const form = new FormData();
      form.set("file", file);
      form.set("placement", placement);
      form.set("locale", locale);
      const res = await fetch("/api/banners/art", { method: "POST", body: form });
      const body = (await res.json().catch(() => null)) as {
        url?: string;
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Upload failed (${res.status})`);
      onChange(body?.url ?? null);
      onNotice(`${body?.message ?? ""} ${t("art.uploadedSaveHint")}`.trim());
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : t("art.uploadFailed"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">{title}</h3>
      <div
        className="mt-2 overflow-hidden rounded-lg border border-surface-700 bg-surface-950"
        style={{ aspectRatio: `${spec.width} / ${spec.height}` }}
      >
        {preview || url ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={preview ?? url ?? ""}
            alt={`${title} preview`}
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="flex h-full items-center justify-center px-3 text-center text-[11px] text-zinc-600">
            {t("ban.artNone")}
          </div>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept={BANNER_ART_SPEC.mimeTypes.join(",")}
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) void onFile(f);
        }}
        className="mt-2 block w-full text-xs text-zinc-400 file:mr-3 file:rounded-md file:border-0 file:bg-surface-700 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-zinc-200 hover:file:bg-surface-800"
      />

      {busy && <p className="mt-2 text-xs text-zinc-400">{t("art.uploading")}</p>}
      {localError && (
        <p className="mt-2 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {localError}
        </p>
      )}
      {aspectWarning && (
        <p className="mt-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
          {aspectWarning}
        </p>
      )}
      {url && (
        <div className="mt-2">
          <div className="break-all font-mono text-[10px] text-zinc-600">{url}</div>
          <button
            type="button"
            onClick={() => {
              onChange(null);
              setPreview(null);
              if (inputRef.current) inputRef.current.value = "";
            }}
            className="mt-1 rounded-md border border-surface-700 px-2.5 py-1 text-xs text-zinc-400 hover:bg-surface-800"
          >
            {t("art.remove")}
          </button>
        </div>
      )}
    </div>
  );
}
