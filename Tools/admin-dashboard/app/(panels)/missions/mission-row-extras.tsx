"use client";

import { useMemo } from "react";
import { ALL_GOAL_TYPES } from "@/lib/contentValidate";
import type { ContentStoredRow } from "@/lib/types";

/**
 * The composed half of a mission row: hole, tier, start area, wind, loadout and
 * the three goal slots, every one of them a DROPDOWN fed from the catalog it
 * resolves against (missions_v1 §A6).
 *
 * WHY DROPDOWNS AND NOT TEXT. Each of these is a row id in another catalog. A
 * typo is not a bad value, it is a mission that has nowhere to put the ball —
 * and the operator would find out at publish time if the validator catches it
 * and at play time if it does not. The one thing a text field is better at
 * (entering an id that does not exist yet) is exactly the thing that must not
 * happen here.
 *
 * THE START-AREA LIST IS FILTERED BY THE CHOSEN HOLE, because a start area is
 * per (hole, area): `lomond_h04_green` and `lomond_h09_green` are different
 * points with different coordinates. Choosing hole 9 and then picking hole 4's
 * green would be a mission that starts on another hole entirely.
 *
 * NOT-BAKED AND DEACTIVATED AREAS ARE STILL LISTED, and labelled. Hiding them
 * would leave an operator staring at a hole with three options and no
 * explanation of where the other two went; labelled, the list says "SAND — no
 * bunker on this hole" and "FRINGE — not baked yet", which is the answer.
 */

export interface ComponentOptions {
  startAreas: ContentStoredRow[];
  winds: ContentStoredRow[];
  loadouts: ContentStoredRow[];
  tiers: ContentStoredRow[];
}

const HOLES = Array.from({ length: 18 }, (_, i) => String(i + 1));

const text = (v: unknown): string => (v === null || v === undefined ? "" : String(v).trim());

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {label}
      </span>
      {children}
    </label>
  );
}

const SELECT_CLASS =
  "w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1.5 text-xs text-zinc-200 " +
  "focus:border-accent-500 focus:outline-none";

function Select({
  value,
  onChange,
  options,
  allowBlank,
}: {
  value: string;
  onChange: (v: string) => void;
  options: Array<{ value: string; label: string }>;
  allowBlank?: boolean;
}) {
  // An id that is set but not in the list is kept as an option of its own rather
  // than silently reset to blank — a row pointing at a deleted component must
  // keep saying so until somebody decides what it should point at instead.
  const known = options.some((o) => o.value === value);
  return (
    <select className={SELECT_CLASS} value={value} onChange={(e) => onChange(e.target.value)}>
      {allowBlank && <option value="">—</option>}
      {!known && value !== "" && <option value={value}>{value} (unknown)</option>}
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  );
}

export function MissionRowExtras({
  options,
  draft,
  set,
}: {
  options: ComponentOptions | null;
  draft: Record<string, string>;
  set: (column: string, value: string) => void;
}) {
  const holeId = text(draft.holeId);

  const areaOptions = useMemo(() => {
    if (!options) return [];
    return options.startAreas
      .filter((r) => text(r.data.holeId) === holeId)
      .map((r) => {
        const areaId = text(r.data.areaId);
        const kind = text(r.data.kind);
        const baked = ["x", "y", "z"].every((axis) => text(r.data[axis]) !== "");
        const suffix = !r.isActive
          ? " — not on this hole"
          : kind === "short" && !baked
            ? " — not baked yet"
            : "";
        return { value: areaId, label: `${areaId} (${kind})${suffix}` };
      })
      .sort((a, b) => a.value.localeCompare(b.value));
  }, [options, holeId]);

  const windOptions = useMemo(
    () =>
      (options?.winds ?? []).map((r) => ({
        value: r.rowId,
        label: `${text(r.data.label) || r.rowId} · ${text(r.data.speed)} mph · +${text(r.data.weight)}`,
      })),
    [options]
  );

  const loadoutOptions = useMemo(
    () =>
      (options?.loadouts ?? []).map((r) => ({
        value: r.rowId,
        label: `${text(r.data.label) || r.rowId} · ${text(r.data.allowedStartKinds)} · +${text(r.data.weight)}`,
      })),
    [options]
  );

  const tierOptions = useMemo(
    () =>
      (options?.tiers ?? []).map((r) => ({
        value: r.rowId,
        label: `${r.rowId} (${text(r.data.scoreMin)}–${Number(text(r.data.scoreMaxExcl) || 0) - 1})`,
      })),
    [options]
  );

  const goalOptions = ALL_GOAL_TYPES.map((g) => ({ value: g, label: g }));

  return (
    <div className="mb-4 space-y-3 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div className="grid grid-cols-2 gap-3">
        <Field label="Hole">
          <Select
            value={holeId}
            onChange={(v) => {
              set("holeId", v);
              // The start area is per (hole, area), so an area chosen for the
              // old hole is meaningless under the new one. Clearing it is the
              // honest move: it forces a re-pick instead of silently pointing
              // the mission at another hole's green.
              if (text(draft.startAreaId)) set("startAreaId", "");
            }}
            options={HOLES.map((h) => ({ value: h, label: `Hole ${h}` }))}
            allowBlank
          />
        </Field>
        <Field label="Tier">
          <Select value={text(draft.tier)} onChange={(v) => set("tier", v)} options={tierOptions} allowBlank />
        </Field>
        <Field label="Start area">
          <Select
            value={text(draft.startAreaId)}
            onChange={(v) => set("startAreaId", v)}
            options={areaOptions}
            allowBlank
          />
        </Field>
        <Field label="Wind preset">
          <Select value={text(draft.windPresetId)} onChange={(v) => set("windPresetId", v)} options={windOptions} allowBlank />
        </Field>
        <Field label="Loadout">
          <Select value={text(draft.loadoutId)} onChange={(v) => set("loadoutId", v)} options={loadoutOptions} allowBlank />
        </Field>
      </div>

      <div className="space-y-2">
        {[1, 2, 3].map((slot) => (
          <div key={slot} className="grid grid-cols-[1fr_1fr] gap-3">
            <Field label={`Goal ${slot}`}>
              <Select
                value={text(draft[`goal${slot}Type`])}
                onChange={(v) => {
                  set(`goal${slot}Type`, v);
                  // Clearing the type clears its param: a param with no type is
                  // a validation error the operator would have to be told about
                  // rather than something the form can just not create.
                  if (!v) set(`goal${slot}Param`, "");
                }}
                options={goalOptions}
                allowBlank
              />
            </Field>
            <Field label={`Goal ${slot} param`}>
              <input
                className={SELECT_CLASS}
                value={text(draft[`goal${slot}Param`])}
                onChange={(e) => set(`goal${slot}Param`, e.target.value)}
                placeholder="—"
              />
            </Field>
          </div>
        ))}
      </div>

      {!options && (
        <p className="text-[11px] text-amber-400">
          Component catalogs could not be loaded — these fields fall back to whatever is stored.
          The publish validator still checks them.
        </p>
      )}
    </div>
  );
}
