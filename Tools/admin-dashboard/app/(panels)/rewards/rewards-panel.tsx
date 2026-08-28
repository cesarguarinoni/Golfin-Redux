"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { RewardActionRow, RewardActionsResponse } from "@/lib/types";

/**
 * Rewards — `game_point_actions`, the earn catalog (game_modes_admin §3).
 *
 * THIS IS NOT A CONTENT PANEL AND IT IS SHAPED SO THAT NOBODY THINKS IT IS.
 * No `CatalogPanel`, no publish drawer, no version badge, no kill switch — none
 * of which exist for this table, because the earn path reads it per request. The
 * red banner at the top is the disclosure and it is the first thing on screen.
 *
 * WHAT IS MISSING IS ALSO DELIBERATE. There is no `+ New action` and no delete:
 * actions are referenced BY NAME from clients already installed, so deleting one
 * silently drops every earn that used it, and creating one nobody sends does
 * nothing at all. `lib/rewardsMutations.ts` enforces both server-side; the
 * absence of the buttons is the explanation, not the mechanism.
 *
 * THE BLANK `pts` CELL NEEDS ITS HINT AND THE SPEC SAYS SO BY NAME. A NULL
 * `pts` means "the client supplies the amount, bounded by the caps" — the mode
 * every variable payout uses (a hole score, a tournament prize band). Without a
 * word of explanation it reads as a missing value somebody forgot to fill, and
 * the obvious "fix" — typing a number — silently converts a variable payout into
 * a flat one. Hence the badge on the cell and the hint under the table, EN + JA.
 */
export function RewardsPanel() {
  const translate = useT();

  const [data, setData] = useState<RewardActionsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [editing, setEditing] = useState<RewardActionRow | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/rewards", { cache: "no-store" });
      const body = (await res.json()) as RewardActionsResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setData(body);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const actions = data?.actions ?? [];

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("rw.title")}</h1>
        <code className="text-xs text-zinc-600">game_point_actions</code>
      </div>

      {/* ⚠️ FIRST on the page, and loud. Every other economy surface in this
          dashboard stages an edit behind a publish; this one does not, and an
          operator who assumes otherwise changes what players are paid while
          believing they are drafting. */}
      <div className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-3 py-2.5">
        <p className="text-xs font-bold text-amber-300">⚠ {translate("rw.live.headline")}</p>
        <p className="mt-1 text-[11px] leading-relaxed text-amber-200/85">
          {translate("rw.live.body")}
        </p>
      </div>

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}
      {error && (
        <div className="mb-4 rounded-lg border border-red-500/40 bg-red-500/10 p-3 text-sm text-red-300">
          {translate("rw.loadFailed")}: {error}
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("rw.col.action")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("rw.col.pts")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("rw.col.maxPerEvent")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("rw.col.dailyCap")}</th>
              <th className="whitespace-nowrap px-4 py-2.5 font-medium">{translate("rw.col.oncePerUser")}</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody>
            {actions.map((row) => (
              <tr key={row.action} className="border-t border-surface-800 bg-surface-950">
                <td className="px-4 py-2.5">
                  <code className="text-[11px] text-zinc-300">{row.action}</code>
                </td>
                <td className="px-4 py-2.5 text-xs tabular-nums text-zinc-300">
                  {row.pts === null ? (
                    <span
                      title={translate("rw.ptsNullHint")}
                      className="whitespace-nowrap rounded border border-sky-500/50 px-1.5 py-0.5 text-[10px] font-semibold text-sky-300"
                    >
                      {translate("rw.ptsNullBadge")}
                    </span>
                  ) : (
                    row.pts
                  )}
                </td>
                <td className="px-4 py-2.5 text-xs tabular-nums text-zinc-300">
                  {row.maxPerEvent ?? translate("rw.blank")}
                </td>
                <td className="px-4 py-2.5 text-xs tabular-nums text-zinc-300">
                  {row.dailyCap ?? translate("rw.blank")}
                </td>
                <td className="px-4 py-2.5 text-xs text-zinc-400">{row.oncePerUser ? "yes" : "—"}</td>
                <td className="px-4 py-2.5 text-right">
                  <button
                    type="button"
                    onClick={() => {
                      setNotice(null);
                      setEditing(row);
                    }}
                    className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] text-zinc-300 transition hover:bg-surface-800"
                  >
                    {translate("rw.edit")}
                  </button>
                </td>
              </tr>
            ))}
            {actions.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-sm text-zinc-600">
                  {data ? "—" : translate("c.loading")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* The two standing explanations, always visible rather than tucked into a
          tooltip: one says what a blank Points cell MEANS, the other says why
          there is no add or delete control to look for. */}
      <p className="mt-3 text-[11px] leading-relaxed text-zinc-500">{translate("rw.ptsNullHint")}</p>
      <p className="mt-1.5 text-[11px] leading-relaxed text-zinc-600">{translate("rw.noDelete")}</p>

      {editing && (
        <RewardEditor
          row={editing}
          onClose={() => setEditing(null)}
          onSaved={async (message) => {
            setEditing(null);
            setNotice(message);
            await load();
          }}
        />
      )}
    </div>
  );
}

/** Blank input ⇄ null. The empty string is the ONLY way to write "no fixed
 *  amount" / "no cap", so it round-trips as `null` and never as 0. */
const toField = (v: number | null): string => (v === null ? "" : String(v));
const fromField = (v: string): number | null | "bad" => {
  const t = v.trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isInteger(n) && n >= 0 ? n : "bad";
};

function RewardEditor({
  row,
  onClose,
  onSaved,
}: {
  row: RewardActionRow;
  onClose: () => void;
  onSaved: (message: string) => void | Promise<void>;
}) {
  const translate = useT();
  const [pts, setPts] = useState(toField(row.pts));
  const [maxPerEvent, setMaxPerEvent] = useState(toField(row.maxPerEvent));
  const [dailyCap, setDailyCap] = useState(toField(row.dailyCap));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    const parsed = {
      pts: fromField(pts),
      maxPerEvent: fromField(maxPerEvent),
      dailyCap: fromField(dailyCap),
    };
    if (parsed.pts === "bad" || parsed.maxPerEvent === "bad" || parsed.dailyCap === "bad") {
      setError(translate("rw.hint.numbers"));
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const res = await fetch(`/api/rewards/${encodeURIComponent(row.action)}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(parsed),
      });
      const body = (await res.json()) as { message?: string; error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      await onSaved(body.message ?? translate("rw.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-black/60" onClick={onClose}>
      <div
        className="h-full w-full max-w-md overflow-y-auto border-l border-surface-800 bg-surface-950 p-5"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold text-zinc-100">
          {translate("rw.edit")} · <code className="text-xs text-zinc-400">{row.action}</code>
        </h2>

        <p className="mt-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-2 text-[11px] leading-relaxed text-amber-200/85">
          {translate("rw.live.body")}
        </p>

        <div className="mt-4 space-y-4">
          <Field label={translate("rw.col.pts")} value={pts} onChange={setPts} hint={translate("rw.ptsNullHint")} />
          <Field label={translate("rw.col.maxPerEvent")} value={maxPerEvent} onChange={setMaxPerEvent} />
          <Field label={translate("rw.col.dailyCap")} value={dailyCap} onChange={setDailyCap} />
        </div>

        <p className="mt-3 text-[11px] text-zinc-600">{translate("rw.hint.numbers")}</p>

        {error && (
          <p className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 px-2.5 py-2 text-xs text-red-300">
            {error}
          </p>
        )}

        <div className="mt-5 flex gap-2">
          <button
            type="button"
            disabled={saving}
            onClick={() => void save()}
            className="rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:opacity-40"
          >
            {saving ? translate("rw.saving") : translate("rw.save")}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-300 transition hover:bg-surface-800"
          >
            {translate("common.cancel")}
          </button>
        </div>
      </div>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  hint,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  hint?: string;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-400">{label}</span>
      <input
        type="text"
        inputMode="numeric"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="—"
        className="w-full rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
      />
      {hint && <span className="mt-1 block text-[11px] leading-relaxed text-zinc-600">{hint}</span>}
    </label>
  );
}
