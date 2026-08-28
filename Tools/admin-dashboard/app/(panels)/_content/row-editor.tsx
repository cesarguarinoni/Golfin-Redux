"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import {
  ART_URL_COLUMNS,
  ID_COLUMN,
  isArtUrlColumn,
  isValidNewRowId,
  ROW_ID_MAX,
  SPRITE_FIELD_FOLDER,
  spriteFolder,
  urlOnlyArtColumns,
} from "@/lib/contentView";
import type { ContentStoredRow } from "@/lib/types";
import { UrlOnlyBadge } from "./badges";
import { saveRow } from "./client";

/** What a panel's extras need in order to prefill the id of a NEW row. */
export interface RowIdContext {
  rowId: string;
  setRowId: (rowId: string) => void;
  isNew: boolean;
}

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
 *
 * `isNew` (shop_stocking §2) is the CREATE mode: the row id becomes an input.
 * It is deliberately NOT `!published`. An existing-but-unpublished draft has a
 * locked id too, because the id is what the upsert keys on — editing it there
 * would not rename the row, it would silently mint a second one and leave the
 * first behind.
 *
 * The catalog's ID COLUMN (`entryId`, `id`, `key`) is never an editable field
 * in any mode: `upsertDraftRow` writes it from the row id, so the two cannot
 * disagree. It is shown, read-only, in the header.
 */
export function RowEditor({
  catalog,
  row,
  columns,
  published,
  isNew = false,
  onClose,
  onSaved,
  children,
  hiddenColumns,
}: {
  catalog: string;
  row: ContentStoredRow;
  /** Column order to render. */
  columns: string[];
  /** True when this row already exists in content_rows (⇒ minBuild is locked). */
  published: boolean;
  /** True for the `+ New row` drawer: the row id is an input, and the save
   *  asserts the id is free (409 if it is not). */
  isNew?: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
  /** Panel-specific extras rendered above the raw field list (Shop uses it). */
  children?: (
    draft: Record<string, string>,
    set: (col: string, v: string) => void,
    rowIdCtx: RowIdContext
  ) => React.ReactNode;
  /** Columns the extras already render, so the raw list does not repeat them. */
  hiddenColumns?: string[];
}) {
  const translate = useT();
  const [draft, setDraft] = useState<Record<string, string>>({ ...row.data });
  const [rowId, setRowId] = useState(row.rowId);
  const [isActive, setIsActive] = useState(row.isActive);
  const [minBuild, setMinBuild] = useState(String(row.minBuild));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Per-column upload state for art URL columns (content_art_urls).
  const [uploading, setUploading] = useState<Record<string, boolean>>({});
  const [uploadError, setUploadError] = useState<Record<string, string>>({});

  const idColumn = ID_COLUMN[catalog] ?? "id";
  const trimmedId = rowId.trim();

  function set(column: string, value: string) {
    setDraft((prev) => ({ ...prev, [column]: value }));
  }

  async function save() {
    // Shape is checked here for the immediacy; `upsertDraftRow` checks it again
    // because the route is reachable without this form, and collisions can only
    // be answered there.
    if (isNew && !isValidNewRowId(catalog, trimmedId)) {
      setError(translate("c.edit.rowIdInvalid", { max: ROW_ID_MAX }));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await saveRow(catalog, {
        rowId: isNew ? trimmedId : row.rowId,
        data: draft,
        minBuild: Number(minBuild) || 0,
        isActive,
        expectNew: isNew || undefined,
      });
      onSaved(translate("c.edit.saved"));
    } catch (err) {
      const status = (err as { status?: number }).status;
      const detail = err instanceof Error ? err.message : String(err);
      setError(
        status === 409
          ? `${translate("c.edit.rowIdTaken", { rowId: trimmedId })} — ${detail}`
          : `${translate("c.edit.saveFailed")}: ${detail}`
      );
    } finally {
      setBusy(false);
    }
  }

  /**
   * Upload artwork for a URL column (content_art_urls §3).
   * On success, writes the returned public URL into the draft field so the
   * operator can see it and save. The upload itself is fire-and-forget from the
   * DB perspective: the URL only lands in the row when the operator presses Save.
   */
  async function uploadArt(column: string, file: File) {
    const effectiveRowId = isNew ? rowId.trim() : row.rowId;
    if (!effectiveRowId) {
      setUploadError((prev) => ({
        ...prev,
        [column]: "Set a row id before uploading art.",
      }));
      return;
    }
    setUploading((prev) => ({ ...prev, [column]: true }));
    setUploadError((prev) => ({ ...prev, [column]: "" }));
    try {
      const form = new FormData();
      form.append("file", file);
      form.append("catalog", catalog);
      form.append("rowId", effectiveRowId);
      form.append("column", column);
      const res = await fetch("/api/content/art", { method: "POST", body: form });
      const json = (await res.json()) as { url?: string; error?: string };
      if (!res.ok || json.error) {
        setUploadError((prev) => ({
          ...prev,
          [column]: json.error ?? `Upload failed (${res.status}).`,
        }));
      } else if (json.url) {
        set(column, json.url);
      }
    } catch (err) {
      setUploadError((prev) => ({
        ...prev,
        [column]: err instanceof Error ? err.message : "Upload failed.",
      }));
    } finally {
      setUploading((prev) => ({ ...prev, [column]: false }));
    }
  }

  // ⚠️ ART COLUMNS ARE FORCED IN, and that is the whole reason art-by-URL was
  // unusable from this panel.
  //
  // The field list was `columns` (a hardcoded per-catalog list that names none of
  // the art columns) plus whatever keys the STORED row happens to carry. Every row
  // seeded before `content_art_urls` has no `portraitUrl` key — so no field
  // rendered, the "Upload art" button beside it never appeared, and there was no
  // way to CREATE the column from the UI at all. The feature shipped, was
  // approved, and could not be reached: its end-to-end only passed because the
  // fixture object was put in the bucket by hand and the URL set directly in the
  // data. Reported by Cesar 2026-08-28: "I don't see any URL fields in the admin."
  //
  // Derived from the two maps that already define what art a catalog has, rather
  // than a third hand-kept list that could drift from them. Editor only — the row
  // LIST keeps its narrow `columns`, since five more columns would make the table
  // unreadable for a benefit the editor already provides.
  const artColumns = [
    ...Object.keys(SPRITE_FIELD_FOLDER[catalog] ?? {}),
    ...(ART_URL_COLUMNS[catalog] ?? []),
  ];

  // The id column is written from the row id server-side, so it is never an
  // editable field — showing one would be showing a value that gets overwritten.
  const ordered = [
    ...columns,
    ...artColumns.filter((c) => !columns.includes(c)),
    ...Object.keys(draft).filter((c) => !columns.includes(c) && !artColumns.includes(c)),
  ].filter((column) => column !== idColumn && !hiddenColumns?.includes(column));

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
                {translate(isNew ? "c.edit.newTitle" : "c.edit.title")}
              </h2>
              <code className="mt-1 block truncate text-xs text-zinc-500">
                {isNew ? `${catalog} · ${trimmedId || "—"}` : row.rowId}
              </code>
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
          {/* content_art_bundling §9.2 — read off the LIVE draft, not the saved
              row, so uploading art (which sets the URL) shows the badge at once
              and it clears the moment the sprite-name column is filled in. */}
          {urlOnlyArtColumns(catalog, draft).length > 0 && (
            <div className="mt-2">
              <UrlOnlyBadge columns={urlOnlyArtColumns(catalog, draft)} />
            </div>
          )}
        </header>

        <div className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          {error && (
            <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
              {error}
            </div>
          )}

          {/* The id, first: on a new row everything else is meaningless until
              it has one, and it is the one field that cannot be changed later. */}
          {isNew && (
            <div className="rounded-lg border border-surface-800 bg-surface-950 p-3">
              <label className="block text-[11px] text-zinc-500">
                <span className="font-mono text-zinc-400">row_id</span>
                <span className="ml-1.5 font-mono text-[10px] text-zinc-600">→ data.{idColumn}</span>
                <input
                  value={rowId}
                  autoFocus
                  maxLength={ROW_ID_MAX}
                  onChange={(e) => setRowId(e.target.value)}
                  placeholder={catalog === "shop_catalog" ? "shop_char_olivia" : "new_row_id"}
                  className={`mt-1 w-full rounded-md border bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-200 placeholder:text-zinc-700 focus:outline-none ${
                    trimmedId && !isValidNewRowId(catalog, trimmedId)
                      ? "border-red-500/60 focus:border-red-500"
                      : "border-surface-700 focus:border-accent-500"
                  }`}
                />
                <span className="mt-1 block leading-relaxed">
                  {translate("c.edit.rowIdHint", { column: idColumn, max: ROW_ID_MAX })}
                </span>
              </label>
            </div>
          )}

          {children?.(draft, set, { rowId, setRowId, isNew })}

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
            {ordered.map((column) => {
              // content_two_way §6 — a sprite column holds a FILE NAME the build
              // resolves under Resources/. A name this build does not ship renders
              // nothing, and §4 withholds the row rather than drawing a blank, so
              // the constraint has to be visible at the point of typing. No new
              // control: a hint under the field the operator is already using.
              const folder = spriteFolder(catalog, column);
              // content_art_urls §3 — a URL column holds a public Supabase Storage
              // URL. The operator can paste a URL directly or upload art via the
              // button; either way the URL lands in the draft and is saved with the row.
              const isUrlCol = isArtUrlColumn(catalog, column);
              return (
                <div key={column} className="block">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono text-[11px] text-zinc-500">{column}</span>
                    {isUrlCol && (
                      <label className="flex cursor-pointer items-center gap-1 rounded-md border border-surface-700 px-2 py-0.5 text-[10px] text-zinc-400 hover:bg-surface-800">
                        {uploading[column] ? "Uploading…" : "Upload art"}
                        <input
                          type="file"
                          accept="image/jpeg,image/png"
                          className="sr-only"
                          disabled={uploading[column]}
                          onChange={(e) => {
                            const f = e.target.files?.[0];
                            if (f) void uploadArt(column, f);
                            e.target.value = "";
                          }}
                        />
                      </label>
                    )}
                  </div>
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
                  {folder && (
                    <span className="mt-1 block text-[11px] leading-relaxed text-zinc-500">
                      {translate(
                        catalog === "clubs" ? "c.edit.spriteHintClubs" : "c.edit.spriteHint",
                        { folder }
                      )}
                    </span>
                  )}
                  {isUrlCol && (
                    <span className="mt-1 block text-[11px] leading-relaxed text-zinc-500">
                      URL from the catalog-art bucket — paste directly or use &ldquo;Upload art&rdquo;.
                      The client&apos;s resolution ladder picks this over the bundled sprite when the
                      file is already cached on-device (content_art_urls §2).
                    </span>
                  )}
                  {uploadError[column] && (
                    <span className="mt-1 block text-[11px] text-red-400">{uploadError[column]}</span>
                  )}
                </div>
              );
            })}
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
            disabled={busy || (isNew && !trimmedId)}
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
