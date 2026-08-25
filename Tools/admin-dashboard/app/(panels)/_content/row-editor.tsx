"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { ContentStoredRow } from "@/lib/types";
import { saveRow } from "./client";

/**
 * Edit ONE draft row.
 *
 * Deliberately does not validate: drafts are never served to the game, publish
 * is the gate (`content_catalog` §D1), and rejecting a half-typed row would
 * make the editor unusable. The PUT route takes the same position.
 *
 * `minBuild` is editable only while the row is unpublished — §D1.7 makes it
 * immutable afterwards, and a field that silently fails at publish is worse
 * than a disabled one that says why.
 */
export function RowEditor({
  catalog,
  row,
  columns,
  published,
  onClose,
  onSaved,
  children,
}: {
  catalog: string;
  row: ContentStoredRow;
  /** Column order to render. */
  columns: string[];
  /** True when this row already exists in content_rows (⇒ minBuild is locked). */
  published: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
  /** Panel-specific extras rendered above the raw field list (Shop uses it). */
  children?: (draft: Record<string, string>, set: (col: string, v: string) => void) => React.ReactNode;
}) {
  const translate = useT();
  const [draft, setDraft] = useState<Record<string, string>>({ ...row.data });
  const [isActive, setIsActive] = useState(row.isActive);
  const [minBuild, setMinBuild] = useState(String(row.minBuild));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function set(column: string, value: string) {
    setDraft((prev) => ({ ...prev, [column]: value }));
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await saveRow(catalog, {
        rowId: row.rowId,
        data: draft,
        minBuild: Number(minBuild) || 0,
        isActive,
      });
      onSaved(translate("c.edit.saved"));
    } catch (err) {
      setError(`${translate("c.edit.saveFailed")}: ${err instanceof Error ? err.message : err}`);
    } finally {
      setBusy(false);
    }
  }

  const ordered = [...columns, ...Object.keys(draft).filter((c) => !columns.includes(c))];

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      <button
        type="button"
        aria-label={translate("common.close")}
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      <div className="absolute right-0 top-0 flex h-full w-full max-w-2xl flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        {/* `pt-10`, not `py-4`: the mode banner is `sticky top-0 z-50` and this
            drawer is `z-40`, so the banner paints OVER the drawer's first 29px.
            Measured 2026-08-25: with `py-4` the <h2> top lands at y=16 and is
            clipped by 13px. The inherited Tournaments/Banners/Notices/Users
            editors all have the same overlap — reported rather than changed
            here, since fixing four other panels is outside this task. */}
        <header className="border-b border-surface-800 px-5 pb-4 pt-10">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h2 className="truncate text-base font-semibold text-zinc-100">
                {translate("c.edit.title")}
              </h2>
              <code className="mt-1 block truncate text-xs text-zinc-500">{row.rowId}</code>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="shrink-0 rounded-md border border-surface-700 px-2.5 py-1 text-xs text-zinc-400 hover:bg-surface-800"
            >
              {translate("common.close")}
            </button>
          </div>
          <p className="mt-2 text-[11px] text-zinc-500">{translate("c.edit.subtitle")}</p>
        </header>

        <div className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          {error && (
            <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
              {error}
            </div>
          )}

          {children?.(draft, set)}

          <div className="rounded-lg border border-surface-800 bg-surface-950 p-3">
            <label className="flex items-start gap-2 text-xs text-zinc-300">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="mt-0.5 h-3.5 w-3.5 accent-accent-500"
              />
              <span>
                <span className="font-medium">{translate("c.edit.active")}</span>
                <span className="mt-0.5 block text-[11px] text-zinc-500">
                  {translate("c.edit.activeHint")}
                </span>
              </span>
            </label>

            <label className="mt-3 block text-[11px] text-zinc-500">
              <span className="font-mono text-zinc-400">min_build</span>
              <input
                value={minBuild}
                disabled={published}
                onChange={(e) => setMinBuild(e.target.value.replace(/[^0-9]/g, ""))}
                className="mt-1 w-32 rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-200 focus:border-accent-500 focus:outline-none disabled:opacity-50"
              />
              <span className="mt-1 block">{translate("c.edit.minBuildHint")}</span>
            </label>
          </div>

          <div className="space-y-2">
            {ordered.map((column) => (
              <label key={column} className="block">
                <span className="font-mono text-[11px] text-zinc-500">{column}</span>
                {(draft[column] ?? "").length > 60 ? (
                  <textarea
                    rows={3}
                    value={draft[column] ?? ""}
                    onChange={(e) => set(column, e.target.value)}
                    className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
                  />
                ) : (
                  <input
                    value={draft[column] ?? ""}
                    onChange={(e) => set(column, e.target.value)}
                    className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
                  />
                )}
              </label>
            ))}
          </div>
        </div>

        <footer className="flex justify-end gap-2 border-t border-surface-800 bg-surface-900 px-5 py-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-300 hover:bg-surface-800"
          >
            {translate("common.cancel")}
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={() => void save()}
            className="rounded-md bg-accent-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
          >
            {busy ? translate("c.edit.saving") : translate("c.edit.save")}
          </button>
        </footer>
      </div>
    </div>
  );
}
