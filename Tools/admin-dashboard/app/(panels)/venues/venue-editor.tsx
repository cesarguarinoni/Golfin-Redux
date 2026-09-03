"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { GeocodeResult, VenueCategory, VenueInput, VenueRow } from "@/lib/types";

/**
 * The Partners row editor drawer (gps_checkin § B1).
 *
 * "FIND ON MAP" IS THE POINT OF THIS DRAWER. Coordinates typed by hand are the
 * single most damaging field here: a course 200 m off is a course a player
 * standing at the gate cannot check into, and a course in the wrong prefecture
 * silently never appears in anyone's nearby list. So the workflow is: paste the
 * Google Maps link, press the button, read back the resolved coordinates AND
 * the geohash they imply — before saving, not after a player complains.
 *
 * THE GEOHASH IS SHOWN, NOT EDITED. It is derived on the server on every save
 * (`venueMutations.toRow`), and the read-only field here exists so the operator
 * can SEE that a coordinate change moved it.
 */
export function VenueEditor({
  row,
  onClose,
  onSaved,
}: {
  row: VenueRow | null;
  onClose: () => void;
  onSaved: (message: string) => void | Promise<void>;
}) {
  const translate = useT();
  const isNew = row === null;

  const [name, setName] = useState(row?.name ?? "");
  const [category, setCategory] = useState<VenueCategory>(row?.category ?? "golf");
  const [isPartner, setIsPartner] = useState(row?.isPartner ?? false);
  const [subtitle, setSubtitle] = useState(row?.subtitle ?? "");
  const [priceLabel, setPriceLabel] = useState(row?.priceLabel ?? "");
  const [chipExtra, setChipExtra] = useState(row?.chipExtra ?? "");
  const [partnerOffer, setPartnerOffer] = useState(row?.partnerOffer ?? "");
  const [address, setAddress] = useState(row?.address ?? "");
  const [imageUrl, setImageUrl] = useState(row?.imageUrl ?? "");
  const [lat, setLat] = useState(row?.latitude !== null && row?.latitude !== undefined ? String(row.latitude) : "");
  const [lon, setLon] = useState(row?.longitude !== null && row?.longitude !== undefined ? String(row.longitude) : "");
  const [geohash, setGeohash] = useState(row?.geohash ?? "");
  const [radius, setRadius] = useState(String(row?.gpsRadiusM ?? 500));
  const [isActive, setIsActive] = useState(row?.isActive ?? true);

  const [lookup, setLookup] = useState("");
  const [finding, setFinding] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function find() {
    if (!lookup.trim()) return;
    setFinding(true);
    setError(null);
    try {
      const res = await fetch("/api/venues/geocode", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ query: lookup.trim() }),
      });
      const body = (await res.json()) as { data?: GeocodeResult; error?: string; message?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      if (!body.data) {
        setError(body.message ?? translate("vn.find.noMatch"));
        return;
      }
      setLat(String(body.data.latitude));
      setLon(String(body.data.longitude));
      setGeohash(body.data.geohash);
      if (!name && body.data.name) setName(body.data.name);
      if (!address && body.data.address) setAddress(body.data.address);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setFinding(false);
    }
  }

  async function save() {
    const latN = Number(lat);
    const lonN = Number(lon);
    const radN = Number(radius);
    if (!name.trim()) {
      setError(translate("vn.err.name"));
      return;
    }
    if (!Number.isFinite(latN) || !Number.isFinite(lonN)) {
      setError(translate("vn.err.coords"));
      return;
    }
    if (!Number.isInteger(radN) || radN < 50 || radN > 5000) {
      setError(translate("vn.err.radius"));
      return;
    }

    // `geohash` is NOT in this body, deliberately — the API rejects it and the
    // server derives it. See the drawer's header comment.
    const payload: VenueInput = {
      name: name.trim(),
      category,
      isPartner,
      subtitle: subtitle.trim() || null,
      priceLabel: priceLabel.trim() || null,
      chipExtra: chipExtra.trim() || null,
      partnerOffer: partnerOffer.trim() || null,
      address: address.trim() || null,
      imageUrl: imageUrl.trim() || null,
      latitude: latN,
      longitude: lonN,
      gpsRadiusM: radN,
      isActive,
    };

    setSaving(true);
    setError(null);
    try {
      const res = await fetch(isNew ? "/api/venues" : `/api/venues/${row.id}`, {
        method: isNew ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const body = (await res.json()) as { message?: string; error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      await onSaved(body.message ?? translate("vn.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-black/60" onClick={onClose}>
      <div
        className="h-full w-full max-w-lg overflow-y-auto border-l border-surface-800 bg-surface-950 p-5"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold text-zinc-100">
          {isNew ? translate("vn.new") : translate("vn.edit")}
          {!isNew && <code className="ml-2 text-xs text-zinc-500">#{row.id}</code>}
        </h2>

        <p className="mt-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-2 text-[11px] leading-relaxed text-amber-200/85">
          {translate("vn.live.body")}
        </p>

        {/* ── Find on map ──────────────────────────────────────────────── */}
        <div className="mt-4 rounded-md border border-surface-800 bg-surface-900/60 p-3">
          <span className="mb-1.5 block text-xs font-medium text-zinc-300">
            {translate("vn.find.label")}
          </span>
          <div className="flex gap-2">
            <input
              type="text"
              value={lookup}
              onChange={(e) => setLookup(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") void find();
              }}
              placeholder={translate("vn.find.placeholder")}
              className="min-w-0 flex-1 rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
            />
            <button
              type="button"
              disabled={finding}
              onClick={() => void find()}
              className="shrink-0 rounded-md border border-accent-500/50 px-3 py-1.5 text-xs font-medium text-accent-300 transition hover:bg-accent-500/10 disabled:opacity-40"
            >
              {finding ? translate("vn.find.working") : translate("vn.find.button")}
            </button>
          </div>
          <span className="mt-1.5 block text-[11px] leading-relaxed text-zinc-600">
            {translate("vn.find.hint")}
          </span>
        </div>

        <div className="mt-4 space-y-4">
          <Text label={translate("vn.col.name")} value={name} onChange={setName} />

          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-400">
              {translate("vn.col.category")}
            </span>
            <select
              value={category}
              onChange={(e) => setCategory(e.target.value as VenueCategory)}
              className="w-full rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="golf">{translate("vn.cat.golf")}</option>
              <option value="range">{translate("vn.cat.range")}</option>
              <option value="food">{translate("vn.cat.food")}</option>
            </select>
          </label>

          <Check label={translate("vn.col.partner")} checked={isPartner} onChange={setIsPartner}
                 hint={translate("vn.partnerHint")} />

          <Text label={translate("vn.col.subtitle")} value={subtitle} onChange={setSubtitle}
                hint={translate("vn.subtitleHint")} />
          <Text label={translate("vn.col.price")} value={priceLabel} onChange={setPriceLabel} />
          <Text label={translate("vn.col.chip")} value={chipExtra} onChange={setChipExtra} />
          <Text label={translate("vn.col.offer")} value={partnerOffer} onChange={setPartnerOffer}
                hint={translate("vn.offerHint")} />
          <Text label={translate("vn.col.address")} value={address} onChange={setAddress} />
          <Text label={translate("vn.col.imageUrl")} value={imageUrl} onChange={setImageUrl} />

          <div className="grid grid-cols-2 gap-3">
            <Text label={translate("vn.col.latitude")} value={lat} onChange={setLat} />
            <Text label={translate("vn.col.longitude")} value={lon} onChange={setLon} />
          </div>

          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-400">
              {translate("vn.col.geohash")}
            </span>
            <input
              type="text"
              value={geohash}
              readOnly
              disabled
              placeholder="—"
              className="w-full cursor-not-allowed rounded-md border border-surface-800 bg-surface-900/50 px-3 py-1.5 font-mono text-xs text-zinc-500"
            />
            <span className="mt-1 block text-[11px] leading-relaxed text-zinc-600">
              {translate("vn.geohashHint")}
            </span>
          </label>

          <Text label={translate("vn.col.radius")} value={radius} onChange={setRadius}
                hint={translate("vn.radiusHint")} />

          <Check label={translate("vn.col.active")} checked={isActive} onChange={setIsActive}
                 hint={translate("vn.activeHint")} />
        </div>

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
            {saving ? translate("vn.saving") : translate("vn.save")}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-300 transition hover:bg-surface-800"
          >
            {translate("common.cancel")}
          </button>
        </div>

        <p className="mt-4 text-[11px] leading-relaxed text-zinc-600">{translate("vn.noDelete")}</p>
      </div>
    </div>
  );
}

function Text({
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
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="—"
        className="w-full rounded-md border border-surface-700 bg-surface-900 px-3 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
      />
      {hint && <span className="mt-1 block text-[11px] leading-relaxed text-zinc-600">{hint}</span>}
    </label>
  );
}

function Check({
  label,
  checked,
  onChange,
  hint,
}: {
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
  hint?: string;
}) {
  return (
    <label className="block">
      <span className="flex items-center gap-2 text-xs font-medium text-zinc-400">
        <input
          type="checkbox"
          checked={checked}
          onChange={(e) => onChange(e.target.checked)}
          className="h-3.5 w-3.5 rounded border-surface-700 bg-surface-900 accent-accent-500"
        />
        {label}
      </span>
      {hint && <span className="mt-1 block text-[11px] leading-relaxed text-zinc-600">{hint}</span>}
    </label>
  );
}
