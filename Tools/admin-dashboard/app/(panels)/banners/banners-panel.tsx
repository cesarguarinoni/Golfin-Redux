"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  BANNER_PLACEMENTS,
  PLACEMENT_LABEL,
  bannerSpec,
  deriveBannerState,
  isAssignedPlacement,
} from "@/lib/banner";
import { fmtDate } from "@/lib/format";
import type { BannerRow, BannerState, BannersResponse } from "@/lib/types";
import { BannerEditor } from "./banner-editor";

const STATE_STYLES: Record<BannerState, string> = {
  LIVE: "border-accent-500/40 bg-accent-500/10 text-accent-300",
  SCHEDULED: "border-sky-500/40 bg-sky-500/10 text-sky-300",
  EXPIRED: "border-surface-700 bg-surface-850 text-zinc-500",
  OFF: "border-zinc-600 bg-surface-850 text-zinc-400",
};

function StateBadge({ state }: { state: BannerState }) {
  return (
    <span
      className={`rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ${STATE_STYLES[state]}`}
      title="LIVE is the only state a player can see — every other state means the slot shows its bundled sprite."
    >
      {state}
    </span>
  );
}

export function BannersPanel() {
  const [data, setData] = useState<BannersResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const [editing, setEditing] = useState<BannerRow | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/banners");
      const body = (await res.json().catch(() => null)) as
        | (BannersResponse & { error?: string })
        | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      if (body) {
        setData(body);
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load banners");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const now = Date.now();
  const rows = useMemo(() => data?.banners ?? [], [data]);
  /** banner id → tournament slugs pointing at it. Empty for the auto-served placements. */
  const assigned = useMemo(() => data?.assignedTournaments ?? {}, [data]);

  /** Grouped by placement, in the order the endpoint resolves them. */
  const groups = useMemo(
    () =>
      BANNER_PLACEMENTS.map((p) => ({
        placement: p,
        rows: rows.filter((b) => b.placement === p),
      })),
    [rows]
  );

  async function afterMutation(message: string) {
    setEditing(null);
    setCreating(false);
    setNotice(message);
    await load();
  }

  /**
   * The one-click switch. Deactivating a LIVE banner needs the typed label, so
   * this asks for it here rather than sending a request it knows will 409.
   */
  async function toggleActive(b: BannerRow) {
    const next = !b.isActive;
    let confirmLabel: string | undefined;
    if (!next && deriveBannerState(b, Date.now()) === "LIVE") {
      const typed = window.prompt(
        `"${b.label}" is LIVE — players are seeing it right now.\nRe-type the label to switch it off:`
      );
      if (typed === null) return;
      confirmLabel = typed;
    }

    setBusyId(b.id);
    setNotice(null);
    try {
      const res = await fetch(`/api/banners/${b.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ setActive: next, confirmLabel }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      await afterMutation(body?.message ?? "Saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to switch the banner");
    } finally {
      setBusyId(null);
    }
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        Failed to load banners: {error}
      </div>
    );
  }
  if (!data) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        Loading banners…
      </div>
    );
  }

  const liveCount = rows.filter((b) => deriveBannerState(b, now) === "LIVE").length;

  return (
    <div>
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-lg font-semibold text-zinc-100">Banners</h1>
        <span className="text-xs text-zinc-500">
          {liveCount} live · {rows.length} total
        </span>
      </div>

      <div className="mb-4 rounded-lg border border-accent-500/40 bg-accent-500/10 px-4 py-3 text-xs leading-relaxed text-accent-200">
        <strong className="font-semibold">
          At most one banner per placement is served, and the bundled sprite is always the fallback.
        </strong>{" "}
        The game picks the highest <code>sort order</code> that is active and inside its window, then
        the newest. A placement with nothing live shows exactly what it shows today — nothing here
        can make a slot go blank. Players pick this up on their next launch, or on their next visit
        to the screen (the client refetches on screen entry, at most once a minute).
      </div>

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}

      <div className="mb-4 flex items-center gap-3">
        {data.mock && (
          <span className="rounded bg-yellow-500/15 px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-yellow-300 ring-1 ring-yellow-600/40">
            MOCK
          </span>
        )}
        <button
          type="button"
          onClick={() => {
            setNotice(null);
            setCreating(true);
          }}
          className="ml-auto rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-accent-500"
        >
          + New banner
        </button>
      </div>

      {groups.map(({ placement, rows: group }) => {
        const spec = bannerSpec(placement);
        return (
          <section key={placement} className="mb-6">
            <header className="mb-2 flex items-baseline gap-3">
              <h2 className="text-sm font-semibold text-zinc-200">{PLACEMENT_LABEL[placement]}</h2>
              <code className="text-[11px] text-zinc-600">{placement}</code>
              <span className="text-[11px] text-zinc-600">
                {spec.width}×{spec.height} · {spec.where}
              </span>
            </header>

            <div className="overflow-x-auto rounded-lg border border-surface-800">
              <table className="w-full min-w-[880px] text-left text-sm">
                <thead className="bg-surface-900 text-xs text-zinc-500">
                  <tr>
                    <th className="px-4 py-2.5 font-medium">Preview</th>
                    <th className="px-4 py-2.5 font-medium">Label (admin-only)</th>
                    <th className="px-4 py-2.5 font-medium">State</th>
                    <th className="px-4 py-2.5 font-medium">Art</th>
                    <th className="px-4 py-2.5 font-medium">Link</th>
                    <th className="px-4 py-2.5 font-medium">Window (UTC)</th>
                    <th className="px-4 py-2.5 text-right font-medium">Sort</th>
                    <th className="px-4 py-2.5 font-medium" />
                  </tr>
                </thead>
                <tbody>
                  {group.map((b) => {
                    const state = deriveBannerState(b, now);
                    const thumb = b.imageUrlEn ?? b.imageUrlJa;
                    return (
                      <tr
                        key={b.id}
                        onClick={() => {
                          setNotice(null);
                          setEditing(b);
                        }}
                        className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                          state === "LIVE" ? "bg-surface-950" : "bg-surface-950/40 opacity-70"
                        }`}
                      >
                        <td className="px-4 py-2.5">
                          <div
                            className="overflow-hidden rounded border border-surface-700 bg-surface-950"
                            style={{ width: 120, height: Math.round(120 / spec.aspect) }}
                          >
                            {thumb ? (
                              // eslint-disable-next-line @next/next/no-img-element
                              <img
                                src={thumb}
                                alt=""
                                className="h-full w-full object-cover"
                              />
                            ) : (
                              <div className="flex h-full items-center justify-center text-[10px] text-zinc-600">
                                no art
                              </div>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-2.5 text-sm font-medium text-zinc-200">
                          {b.label}
                          {isAssignedPlacement(b.placement) && (
                            // The blast radius of switching this off, visible without
                            // opening the Tournaments panel.
                            <div
                              className="mt-0.5 text-[11px] font-normal text-zinc-500"
                              title={(assigned[b.id] ?? []).join(", ")}
                            >
                              {(assigned[b.id] ?? []).length === 0
                                ? "Not assigned to any tournament"
                                : `Assigned to ${(assigned[b.id] ?? []).length} ${
                                    (assigned[b.id] ?? []).length === 1
                                      ? "tournament"
                                      : "tournaments"
                                  }`}
                            </div>
                          )}
                        </td>
                        <td className="px-4 py-2.5">
                          <StateBadge state={state} />
                        </td>
                        <td className="px-4 py-2.5 text-xs text-zinc-400">
                          <span className={b.imageUrlEn ? "text-accent-400" : "text-zinc-600"}>
                            EN
                          </span>{" "}
                          ·{" "}
                          <span className={b.imageUrlJa ? "text-accent-400" : "text-zinc-600"}>
                            JA
                          </span>
                        </td>
                        <td className="max-w-[220px] truncate px-4 py-2.5 text-xs text-zinc-400">
                          {b.linkUrl ?? <span className="text-zinc-600">none — not tappable</span>}
                        </td>
                        <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                          {b.startAt || b.endAt ? (
                            <>
                              {b.startAt ? fmtDate(b.startAt) : "always"} →{" "}
                              {b.endAt ? fmtDate(b.endAt) : "no expiry"}
                            </>
                          ) : (
                            <span className="text-zinc-600">no window</span>
                          )}
                        </td>
                        <td className="px-4 py-2.5 text-right text-xs tabular-nums text-zinc-300">
                          {b.sortOrder}
                        </td>
                        <td className="px-4 py-2.5">
                          <button
                            type="button"
                            disabled={busyId === b.id}
                            onClick={(e) => {
                              e.stopPropagation();
                              void toggleActive(b);
                            }}
                            className={`rounded-md border px-2.5 py-1 text-xs font-medium transition disabled:opacity-50 ${
                              b.isActive
                                ? "border-surface-700 text-zinc-400 hover:bg-surface-800"
                                : "border-accent-500/50 text-accent-300 hover:bg-accent-500/15"
                            }`}
                          >
                            {b.isActive ? "Deactivate" : "Activate"}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {group.length === 0 && (
                    <tr>
                      <td colSpan={8} className="px-4 py-8 text-center text-sm text-zinc-600">
                        Nothing scheduled — this slot shows the bundled{" "}
                        <code className="text-zinc-500">{spec.sprite.split("/").pop()}</code>.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>
        );
      })}

      {(editing || creating) && (
        <BannerEditor
          banner={editing}
          assignedTo={editing ? assigned[editing.id] ?? [] : []}
          mock={data.mock}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSaved={afterMutation}
        />
      )}
    </div>
  );
}
