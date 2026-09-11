"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { ContentStoredRow } from "@/lib/types";
import { CatalogPanel } from "../_content/catalog-panel";
import { LineupWorkbench } from "./lineup-workbench";

/**
 * Rotations — the `rotations` catalog (weekly_rotation_admin §4).
 *
 * A plain CatalogPanel (so `+ New row`, drafts, the publish drawer, export and
 * versions all work exactly as on every other catalog) with the Lineup
 * workbench rendered ABOVE the table through the panel's `banner` slot. The
 * workbench writes drafts of this catalog and four others, so after it writes
 * it bumps `reloadToken`, which makes the table below refetch — otherwise the
 * `materializedAt` cell would keep reading "—" until a manual reload.
 */
export function RotationsPanel({ now }: { now: number }) {
  const translate = useT();
  const [reloadToken, setReloadToken] = useState(0);

  function renderCell(row: ContentStoredRow, column: string) {
    if (column === "materializedAt") {
      const at = (row.data.materializedAt ?? "").trim();
      return at ? (
        <span className="whitespace-nowrap text-zinc-300">{fmtDateTime(at)}</span>
      ) : (
        <span className="rounded border border-amber-500/50 bg-amber-500/10 px-1.5 py-0.5 text-[10px] font-bold text-amber-300">
          {translate("ro.state.NOT_GENERATED")}
        </span>
      );
    }
    if (column === "startUtc" || column === "endUtc") {
      return <code className="text-[11px] text-zinc-300">{row.data[column] || "—"}</code>;
    }
    return undefined;
  }

  return (
    <CatalogPanel
      catalog="rotations"
      titleKey="ro.title"
      renderCell={renderCell}
      reloadToken={reloadToken}
      banner={<LineupWorkbench now={now} onWrote={() => setReloadToken((n) => n + 1)} />}
      editorExtras={() => (
        <p className="rounded-md border border-surface-800 bg-surface-950 px-3 py-2 text-[10px] leading-relaxed text-zinc-500">
          {translate("ro.editor.pins")}
        </p>
      )}
    />
  );
}
