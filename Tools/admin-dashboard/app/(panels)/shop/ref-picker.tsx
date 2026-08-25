"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { SHOP_CATEGORY_TO_CATALOG, resolveRef, type ResolvedRef } from "@/lib/contentView";
import { ArtTile, RarityBadge } from "../_content/badges";
import { fetchRows } from "../_content/client";

/**
 * `category` picker → `refId` typeahead → resolved preview (§11.3).
 *
 * WHY A TYPEAHEAD AND NOT A TEXT BOX. Validation already rejects a dangling
 * `refId` at publish (§11.4.1), but rejection happens minutes later, at the end
 * of an edit, to someone who has already moved on. Offering only rows that
 * exist AND are `is_active` makes the broken state unreachable instead of
 * merely caught — and listing a deactivated club is the single most likely way
 * a shop edit produces a card the game cannot render.
 *
 * The search is a SERVER query: `/api/content/:catalog/rows?q=…&limit=20`,
 * debounced, against the catalog the chosen category names. Nothing loads the
 * 799-row clubs catalog into the browser to search it locally.
 */

const DEBOUNCE_MS = 200;
const SUGGESTIONS = 20;

export function RefPicker({
  category,
  refId,
  onPick,
}: {
  category: string;
  refId: string;
  onPick: (refId: string) => void;
}) {
  const translate = useT();
  const catalog = SHOP_CATEGORY_TO_CATALOG[category] ?? "";

  const [term, setTerm] = useState("");
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [options, setOptions] = useState<ResolvedRef[]>([]);
  const [resolved, setResolved] = useState<ResolvedRef | null | "missing">(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ---- suggestions -------------------------------------------------------
  const search = useCallback(
    async (q: string) => {
      if (!catalog) return;
      setBusy(true);
      try {
        const res = await fetchRows(catalog, { q, limit: SUGGESTIONS });
        // ACTIVE ONLY — §11.4.1. The route has no is_active filter, so this is
        // the one place the narrowing is client-side, over at most 20 rows.
        setOptions(res.rows.filter((row) => row.isActive).map((row) => resolveRef(catalog, row)));
      } catch {
        setOptions([]);
      } finally {
        setBusy(false);
      }
    },
    [catalog]
  );

  useEffect(() => {
    if (!open) return;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void search(term), DEBOUNCE_MS);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, [term, open, search]);

  // ---- resolve the currently stored refId -------------------------------
  useEffect(() => {
    let cancelled = false;
    if (!catalog || !refId) {
      setResolved(null);
      return;
    }
    void fetchRows(catalog, { q: refId, limit: SUGGESTIONS }).then((res) => {
      if (cancelled) return;
      const hit = res.rows.find((row) => row.rowId === refId);
      setResolved(hit ? resolveRef(catalog, hit) : "missing");
    });
    return () => {
      cancelled = true;
    };
  }, [catalog, refId]);

  return (
    <div className="space-y-3">
      <div>
        <span className="font-mono text-[11px] text-zinc-500">refId</span>
        <div className="relative mt-0.5">
          <input
            value={open ? term : refId}
            onFocus={() => {
              setTerm("");
              setOpen(true);
            }}
            onBlur={() => setTimeout(() => setOpen(false), 150)}
            onChange={(e) => setTerm(e.target.value)}
            placeholder={translate("sh.refId.search", { catalog: catalog || "—" })}
            className="w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
          />

          {open && (
            <div className="absolute z-10 mt-1 max-h-64 w-full overflow-y-auto rounded-md border border-surface-700 bg-surface-900 shadow-xl">
              {busy && <p className="px-3 py-2 text-[11px] text-zinc-500">{translate("sh.refId.searching")}</p>}
              {!busy && options.length === 0 && (
                <p className="px-3 py-2 text-[11px] text-zinc-500">{translate("sh.refId.none")}</p>
              )}
              {options.map((option) => (
                <button
                  key={option.rowId}
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    onPick(option.rowId);
                    setOpen(false);
                  }}
                  className="flex w-full items-center gap-2 px-2.5 py-1.5 text-left transition hover:bg-surface-800"
                >
                  <ArtTile name={option.name} seed={option.rowId} size={24} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-xs text-zinc-200">{option.name}</span>
                    <code className="block truncate text-[10px] text-zinc-500">{option.rowId}</code>
                  </span>
                  <RarityBadge rarity={option.rarity} />
                </button>
              ))}
            </div>
          )}
        </div>
        <p className="mt-1 text-[10px] text-zinc-600">{translate("sh.refId.activeOnly")}</p>
      </div>

      {/* Resolved preview — name, rarity, art reference */}
      {refId && (
        <div className="rounded-lg border border-surface-800 bg-surface-950 p-3">
          <h4 className="text-[11px] font-semibold text-zinc-400">{translate("sh.preview.title")}</h4>

          {resolved === "missing" && (
            <p className="mt-2 rounded-md border border-red-500/40 bg-red-500/10 px-2.5 py-1.5 text-[11px] text-red-300">
              {translate("sh.preview.unresolved", { refId, catalog: catalog || "—" })}
            </p>
          )}

          {resolved && resolved !== "missing" && (
            <>
              <div className="mt-2 flex items-center gap-3">
                <ArtTile name={resolved.name} seed={resolved.rowId} size={44} />
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="truncate text-sm font-medium text-zinc-100">{resolved.name}</span>
                    <RarityBadge rarity={resolved.rarity} />
                  </div>
                  <code className="block truncate text-[10px] text-zinc-500">{resolved.rowId}</code>
                  <div className="mt-1 text-[10px] text-zinc-500">
                    <span className="text-zinc-600">{translate("sh.preview.artRef")}: </span>
                    <code className="text-zinc-400">{resolved.artRef || "—"}</code>
                  </div>
                </div>
              </div>
              {!resolved.isActive && (
                <p className="mt-2 rounded-md border border-red-500/40 bg-red-500/10 px-2.5 py-1.5 text-[11px] text-red-300">
                  {translate("sh.preview.inactive", { refId, catalog: catalog || "—" })}
                </p>
              )}
              <p className="mt-2 text-[10px] leading-relaxed text-zinc-600">
                {translate("sh.preview.noArtUrl")}
              </p>
            </>
          )}
        </div>
      )}
    </div>
  );
}
