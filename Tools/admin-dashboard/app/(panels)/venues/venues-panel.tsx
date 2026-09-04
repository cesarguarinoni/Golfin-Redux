"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { VenueRow, VenuesResponse } from "@/lib/types";
import { VenueEditor } from "./venue-editor";

/**
 * Partners — `public.venues`, the spots the Rounds tab browses (gps_checkin §B1).
 *
 * THIS IS NOT A CONTENT PANEL, and it is shaped like Rewards rather than like
 * Clubs for the same reason: `/venue/nearby` reads this table per request, so
 * there is no draft to stage and no publish to press. A save is live on the
 * player's next fetch. The amber banner is the first thing on screen.
 *
 * THREE ABSENCES ARE DELIBERATE.
 *   · No delete. `activities.venue_id` is a foreign key — deleting a venue a
 *     player checked into either fails or orphans their round. Deactivate is
 *     the removal, and it is reversible.
 *   · No geohash field. It is derived from the coordinates on every save. A
 *     typed geohash that disagrees with the coordinates makes the row invisible
 *     to `/venue/nearby` with no error anywhere — which is the state two
 *     hand-seeded rows are in right now, and what the red drift banner names.
 *   · No `sport_type` field. It is the Flutter app's axis and stays 'golf';
 *     `category` is the one the Rounds chips read.
 */
export function VenuesPanel() {
  const translate = useT();

  const [data, setData] = useState<VenuesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [editing, setEditing] = useState<VenueRow | "new" | null>(null);

  const [category, setCategory] = useState<string>("");
  const [partner, setPartner] = useState<string>("");
  const [active, setActive] = useState<string>("true");
  const [source, setSource] = useState<string>("");
  const [search, setSearch] = useState<string>("");

  const query = useMemo(() => {
    const p = new URLSearchParams();
    if (category) p.set("category", category);
    if (partner) p.set("partner", partner);
    if (active) p.set("active", active);
    if (source) p.set("source", source);
    if (search.trim()) p.set("search", search.trim());
    return p.toString();
  }, [category, partner, active, source, search]);

  const load = useCallback(async () => {
    try {
      const res = await fetch(`/api/venues?${query}`, { cache: "no-store" });
      const body = (await res.json()) as VenuesResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setData(body);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, [query]);

  // Debounced so typing in the search box is not one request per keystroke.
  useEffect(() => {
    const t = setTimeout(() => void load(), 250);
    return () => clearTimeout(t);
  }, [load]);

  const venues = data?.venues ?? [];
  const drift = data?.drift ?? [];

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("vn.title")}</h1>
        <div className="flex items-center gap-3">
          <code className="text-xs text-zinc-600">venues</code>
          <button
            type="button"
            onClick={() => {
              setNotice(null);
              setEditing("new");
            }}
            className="rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500"
          >
            {translate("vn.new")}
          </button>
        </div>
      </div>

      {/* FIRST on the page, and loud — same reason as the Rewards panel: every
          other catalog here stages an edit behind a publish, this one does not. */}
      <div className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-3 py-2.5">
        <p className="text-xs font-bold text-amber-300">⚠ {translate("vn.live.headline")}</p>
        <p className="mt-1 text-[11px] leading-relaxed text-amber-200/85">
          {translate("vn.live.body")}
        </p>
      </div>

      {/* A row whose geohash disagrees with its coordinates is invisible to
          /venue/nearby. Nothing errors, so it can only be found by comparing
          the two — which is what this is. */}
      {drift.length > 0 && (
        <div className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2">
          <p className="text-[11px] font-semibold text-red-300">
            {translate("vn.drift.headline")}
          </p>
          <p className="mt-1 text-[11px] leading-relaxed text-red-200/85">
            {translate("vn.drift.body")}
          </p>
          <ul className="mt-1.5 space-y-0.5">
            {drift.map((d) => (
              <li key={d.id} className="font-mono text-[10px] text-red-200/70">
                #{d.id} {d.name} — stored {d.stored} / computed {d.computed}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mb-3 flex flex-wrap items-end gap-2">
        <Select label={translate("vn.filter.category")} value={category} onChange={setCategory}
          options={[["", translate("vn.filter.any")], ["golf", translate("vn.cat.golf")],
                    ["range", translate("vn.cat.range")], ["food", translate("vn.cat.food")]]} />
        <Select label={translate("vn.filter.partner")} value={partner} onChange={setPartner}
          options={[["", translate("vn.filter.any")], ["true", translate("vn.yes")],
                    ["false", translate("vn.no")]]} />
        <Select label={translate("vn.filter.active")} value={active} onChange={setActive}
          options={[["", translate("vn.filter.any")], ["true", translate("vn.yes")],
                    ["false", translate("vn.no")]]} />
        <Select label={translate("vn.filter.source")} value={source} onChange={setSource}
          options={[["", translate("vn.filter.any")],
                    ...((data?.sources ?? []).map((s) => [s, s] as [string, string]))]} />
        <label className="block">
          <span className="mb-1 block text-[11px] font-medium text-zinc-500">
            {translate("vn.filter.search")}
          </span>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={translate("vn.filter.searchPlaceholder")}
            className="w-56 rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
          />
        </label>
        <span className="pb-1.5 text-[11px] text-zinc-600">
          {translate("vn.count").replace("{n}", String(venues.length))}
        </span>
      </div>

      {notice && (
        <p className="mb-4 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {notice}
        </p>
      )}
      {error && (
        <div className="mb-4 rounded-lg border border-red-500/40 bg-red-500/10 p-3 text-sm text-red-300">
          {translate("vn.loadFailed")}: {error}
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-surface-800">
        <table className="w-full min-w-[1180px] text-left text-sm">
          <thead className="bg-surface-900 text-xs text-zinc-500">
            <tr>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.name")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.category")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.partner")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.subtitle")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.price")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.chip")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.offer")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.coords")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.radius")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.active")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.source")}</th>
              <th className="whitespace-nowrap px-3 py-2.5 font-medium">{translate("vn.col.updated")}</th>
              <th className="px-3 py-2.5" />
            </tr>
          </thead>
          <tbody>
            {venues.map((v) => (
              <tr
                key={v.id}
                className={`border-t border-surface-800 ${
                  v.isActive ? "bg-surface-950" : "bg-surface-950/50 text-zinc-600"
                }`}
              >
                <td className="px-3 py-2 text-xs text-zinc-200">
                  {v.name}
                  <span className="ml-1.5 font-mono text-[10px] text-zinc-600">#{v.id}</span>
                </td>
                <td className="px-3 py-2 text-xs text-zinc-400">
                  {translate(`vn.cat.${v.category}` as never)}
                </td>
                <td className="px-3 py-2 text-xs">
                  {v.isPartner ? (
                    <span className="rounded border border-emerald-500/50 px-1.5 py-0.5 text-[10px] font-semibold text-emerald-300">
                      {translate("vn.partnerTag")}
                    </span>
                  ) : (
                    <span className="text-zinc-600">—</span>
                  )}
                </td>
                <td className="max-w-[220px] truncate px-3 py-2 text-xs text-zinc-400" title={v.subtitle ?? ""}>
                  {v.subtitle ?? "—"}
                </td>
                <td className="px-3 py-2 text-xs text-zinc-400">{v.priceLabel ?? "—"}</td>
                <td className="px-3 py-2 text-xs text-zinc-400">{v.chipExtra ?? "—"}</td>
                <td className="max-w-[180px] truncate px-3 py-2 text-xs text-zinc-400" title={v.partnerOffer ?? ""}>
                  {v.partnerOffer ?? "—"}
                </td>
                <td className="px-3 py-2 font-mono text-[10px] tabular-nums text-zinc-500">
                  {v.latitude !== null && v.longitude !== null
                    ? `${v.latitude.toFixed(5)}, ${v.longitude.toFixed(5)}`
                    : "—"}
                  {!v.geohashOk && (
                    <span
                      title={translate("vn.drift.body")}
                      className="ml-1.5 rounded border border-red-500/50 px-1 py-0.5 text-[9px] font-semibold text-red-300"
                    >
                      geohash
                    </span>
                  )}
                </td>
                <td className="px-3 py-2 text-xs tabular-nums text-zinc-400">{v.gpsRadiusM} m</td>
                <td className="px-3 py-2 text-xs">
                  {v.isActive ? (
                    <span className="text-zinc-400">{translate("vn.yes")}</span>
                  ) : (
                    <span className="text-amber-400">{translate("vn.inactive")}</span>
                  )}
                </td>
                <td className="px-3 py-2 font-mono text-[10px] text-zinc-600">{v.source ?? "—"}</td>
                <td className="px-3 py-2 font-mono text-[10px] text-zinc-600">
                  {v.updatedAt ? v.updatedAt.slice(0, 10) : "—"}
                </td>
                <td className="px-3 py-2 text-right">
                  <button
                    type="button"
                    onClick={() => {
                      setNotice(null);
                      setEditing(v);
                    }}
                    className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] text-zinc-300 transition hover:bg-surface-800"
                  >
                    {translate("vn.edit")}
                  </button>
                </td>
              </tr>
            ))}
            {venues.length === 0 && (
              <tr>
                <td colSpan={13} className="px-4 py-10 text-center text-sm text-zinc-600">
                  {data ? "—" : translate("c.loading")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <p className="mt-3 text-[11px] leading-relaxed text-zinc-500">{translate("vn.noDelete")}</p>
      <p className="mt-1.5 text-[11px] leading-relaxed text-zinc-600">{translate("vn.geohashHint")}</p>

      {editing && (
        <VenueEditor
          row={editing === "new" ? null : editing}
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

function Select({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: Array<[string, string]>;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-[11px] font-medium text-zinc-500">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
      >
        {options.map(([v, l]) => (
          <option key={v} value={v}>
            {l}
          </option>
        ))}
      </select>
    </label>
  );
}
