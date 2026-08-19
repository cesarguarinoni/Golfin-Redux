"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { ART_SPEC, BOT_FIELDS, LEAGUE_KEYS, SHIPPING_COURSES, findCourse } from "@/lib/courses";
import { fmtDateTime } from "@/lib/format";
import {
  artLayer,
  CATEGORIES,
  deriveState,
  DIVISION_TYPES,
  expandHoleSet,
  isLive,
  prizePoolSummary,
  RARITIES,
  validatePrizeBands,
  validateRestrictions,
} from "@/lib/tournament";
import type {
  BannerRow,
  PrizeBand,
  TournamentEntriesResponse,
  TournamentInput,
  TournamentRow,
} from "@/lib/types";
import { StateBadge } from "./badges";

type Tab = "details" | "prizes" | "artwork" | "entries";

/** ISO → the value a datetime-local input wants, in UTC. */
function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toISOString().slice(0, 16);
}
function fromLocalInput(value: string): string {
  return value ? `${value}:00.000Z` : "";
}

function blankDraft(): TournamentInput {
  const start = new Date();
  start.setUTCHours(0, 0, 0, 0);
  start.setUTCDate(start.getUTCDate() + 1);
  const end = new Date(start);
  end.setUTCDate(end.getUTCDate() + 7);
  return {
    slug: "",
    title: "",
    titleJa: "",
    nameKey: "",
    courseId: "lomond",
    holeSet: "1-18",
    startAt: start.toISOString(),
    endAt: end.toISOString(),
    resolveDelayMinutes: 30,
    entryFeePts: 0,
    botFieldId: "field_small",
    sponsorName: "GOLFIN",
    leagueKey: "SILVER",
    bannerUrl: null,
    modalBannerId: null,
    descriptionEn: "",
    descriptionJa: "",
    descriptionKey: "",
    isActive: true,
    category: "sponsor",
    maxPlayers: null,
    playersPerDivision: null,
    divisionType: "level",
    charRarityMin: null,
    charRarityMax: null,
    charLevelMin: null,
    charLevelMax: null,
    gearRule: "own",
    clubRarityMax: null,
    bands: [
      { id: "", rankFrom: 1, rankTo: 1, rpReward: 300, itemRewardId: null },
      { id: "", rankFrom: 2, rankTo: 3, rpReward: 150, itemRewardId: null },
      { id: "", rankFrom: 4, rankTo: 10, rpReward: 50, itemRewardId: null },
    ],
  };
}

function toDraft(row: TournamentRow): TournamentInput {
  return {
    slug: row.slug ?? "",
    title: row.title,
    titleJa: row.titleJa ?? "",
    nameKey: row.nameKey ?? "",
    courseId: row.courseId ?? "",
    holeSet: row.holeSet ?? "1-18",
    startAt: row.startAt ?? "",
    endAt: row.endAt ?? "",
    resolveDelayMinutes: row.resolveDelayMinutes ?? 30,
    entryFeePts: row.entryFeePts,
    botFieldId: row.botFieldId ?? "",
    sponsorName: row.sponsorName ?? "",
    leagueKey: row.leagueKey ?? "",
    bannerUrl: row.bannerUrl,
    modalBannerId: row.modalBannerId,
    descriptionEn: row.descriptionEn ?? "",
    descriptionJa: row.descriptionJa ?? "",
    descriptionKey: row.descriptionKey ?? "",
    isActive: row.isActive,
    category: row.category,
    maxPlayers: row.maxPlayers,
    playersPerDivision: row.playersPerDivision,
    divisionType: row.divisionType,
    charRarityMin: row.charRarityMin,
    charRarityMax: row.charRarityMax,
    charLevelMin: row.charLevelMin,
    charLevelMax: row.charLevelMax,
    gearRule: row.gearRule,
    clubRarityMax: row.clubRarityMax,
    bands: row.bands.map((b) => ({ ...b })),
  };
}

/** "" → null, otherwise a clamped integer — for the optional number fields. */
function intOrNull(value: string): number | null {
  if (value.trim() === "") return null;
  const n = Number(value);
  return Number.isFinite(n) ? Math.trunc(n) : null;
}

const label = "block text-[11px] font-medium uppercase tracking-wider text-zinc-500";
const field =
  "mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none";

export function TournamentEditor({
  tournament,
  mock,
  onClose,
  onSaved,
}: {
  /** null = create a new tournament. */
  tournament: TournamentRow | null;
  mock: boolean;
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const t = useT();
  const isNew = tournament === null;
  const [tab, setTab] = useState<Tab>("details");
  const [draft, setDraft] = useState<TournamentInput>(() =>
    tournament ? toDraft(tournament) : blankDraft()
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmSlug, setConfirmSlug] = useState("");
  const [danger, setDanger] = useState<"delete" | "duplicate" | null>(null);
  const [dupSlug, setDupSlug] = useState("");

  const state = tournament
    ? deriveState(tournament.startAt, tournament.endAt, Date.now())
    : deriveState(draft.startAt, draft.endAt, Date.now());
  const live = tournament !== null && isLive(state);

  const bandErrors = useMemo(() => validatePrizeBands(draft.bands), [draft.bands]);
  const restrError = useMemo(() => validateRestrictions(draft), [draft]);
  const pool = useMemo(() => prizePoolSummary(draft.bands), [draft.bands]);
  const holes = useMemo(() => expandHoleSet(draft.holeSet), [draft.holeSet]);
  const course = findCourse(draft.courseId);
  const layer = artLayer({ bannerUrl: draft.bannerUrl, courseId: draft.courseId });

  function patch(next: Partial<TournamentInput>) {
    setDraft((d) => ({ ...d, ...next }));
    setError(null);
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const payload: TournamentInput = {
        ...draft,
        nameKey: draft.nameKey?.trim() || null,
        titleJa: draft.titleJa?.trim() || null,
        sponsorName: draft.sponsorName?.trim() || null,
        leagueKey: draft.leagueKey || null,
        descriptionEn: draft.descriptionEn?.trim() || null,
        descriptionJa: draft.descriptionJa?.trim() || null,
        descriptionKey: draft.descriptionKey?.trim() || null,
        modalBannerId: draft.modalBannerId || null,
        category: draft.category || null,
        divisionType: draft.divisionType || null,
        charRarityMin: draft.charRarityMin || null,
        charRarityMax: draft.charRarityMax || null,
        gearRule: draft.gearRule || null,
        clubRarityMax: draft.clubRarityMax || null,
        confirmSlug: live ? confirmSlug : undefined,
      };
      const res = await fetch(isNew ? "/api/tournaments" : `/api/tournaments/${tournament!.id}`, {
        method: isNew ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("te.saved"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("te.saveFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function runDelete() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/tournaments/${tournament!.id}`, {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ confirmSlug }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("te.deleted"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("te.delFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function runDuplicate() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/tournaments/${tournament!.id}/duplicate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ slug: dupSlug.trim() }),
      });
      const body = (await res.json().catch(() => null)) as {
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
      onSaved(body?.message ?? t("te.duplicated"));
    } catch (err) {
      setError(err instanceof Error ? err.message : t("te.dupFailed"));
    } finally {
      setBusy(false);
    }
  }

  const tabs: { id: Tab; title: string; hide?: boolean }[] = [
    { id: "details", title: t("te.tab.details") },
    { id: "prizes", title: `${t("te.tab.prizes")} (${draft.bands.length})` },
    { id: "artwork", title: t("te.tab.artwork") },
    { id: "entries", title: `${t("te.tab.entries")}${tournament ? ` (${tournament.entryCount})` : ""}`, hide: isNew },
  ];

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
                {isNew ? t("te.new") : draft.title || draft.slug}
              </h2>
              <div className="mt-1 flex items-center gap-2">
                <code className="text-xs text-zinc-500">{draft.slug || "(no slug yet)"}</code>
                <StateBadge state={state} />
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

          {live && (
            <div className="mt-3 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
              {t("te.liveWarn", { state: t(`tstate.${state}` as DictKey) })}
              <input
                value={confirmSlug}
                onChange={(e) => setConfirmSlug(e.target.value)}
                placeholder={draft.slug}
                className="mt-2 w-full rounded-md border border-amber-500/50 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
              />
            </div>
          )}

          <nav className="mt-3 flex gap-1">
            {tabs
              .filter((t) => !t.hide)
              .map((t) => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setTab(t.id)}
                  className={`rounded-md px-3 py-1.5 text-xs font-medium transition ${
                    tab === t.id
                      ? "bg-surface-700 text-zinc-100"
                      : "text-zinc-500 hover:bg-surface-800 hover:text-zinc-300"
                  }`}
                >
                  {t.title}
                </button>
              ))}
          </nav>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {tab === "details" && (
            <div className="grid grid-cols-2 gap-4">
              <div className="col-span-2">
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
                      {draft.isActive ? t("te.activeOn") : t("te.activeOff")}
                    </div>
                    <p className="mt-0.5 text-[11px] leading-relaxed text-zinc-500">
                      {t("te.hint.active")}
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={draft.isActive}
                    aria-label={t("te.active")}
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
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-slug">
                  {t("te.slug")}
                </label>
                <input
                  id="t-slug"
                  value={draft.slug}
                  onChange={(e) => patch({ slug: e.target.value.trim().toLowerCase() })}
                  placeholder="kasumigaseki_open"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.slug")}
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-title">
                  {t("te.title")}
                </label>
                <input
                  id="t-title"
                  value={draft.title}
                  onChange={(e) => patch({ title: e.target.value })}
                  placeholder="Kasumigaseki Open"
                  className={field}
                />
                {draft.nameKey?.trim() ? (
                  <div className="mt-1 rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-2 text-[11px] text-amber-200">
                    <strong className="font-semibold">{t("te.titleShadowed")}</strong>{" "}
                    {t("te.hint.titleShadowedBody", { key: draft.nameKey.trim() })}
                    <button
                      type="button"
                      onClick={() => patch({ nameKey: "" })}
                      className="mt-1.5 block rounded-md border border-amber-500/50 px-2 py-1 font-medium text-amber-100 hover:bg-amber-500/15"
                    >
                      {t("te.clearKey")}
                    </button>
                  </div>
                ) : (
                  <p className="mt-1 text-[11px] text-zinc-600">
                    {t("te.hint.title")}
                  </p>
                )}
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-title-ja">
                  {t("te.titleJa")}
                </label>
                <input
                  id="t-title-ja"
                  value={draft.titleJa ?? ""}
                  onChange={(e) => patch({ titleJa: e.target.value })}
                  placeholder="セザール選手権"
                  lang="ja"
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.titleJa")}
                  {draft.nameKey?.trim() ? ` ${t("te.hint.titleJaUnused")}` : ""}
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-course">
                  {t("te.venue")}
                </label>
                <select
                  id="t-course"
                  value={draft.courseId}
                  onChange={(e) => patch({ courseId: e.target.value })}
                  className={field}
                >
                  {SHIPPING_COURSES.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.id})
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.venue", { art: course?.art ?? "none" })}
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-holeset">
                  {t("te.holeSet")}
                </label>
                <input
                  id="t-holeset"
                  value={draft.holeSet}
                  onChange={(e) => patch({ holeSet: e.target.value })}
                  placeholder="1-18"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {holes.length > 0 ? t("te.hint.holeSet", { n: holes.length }) : t("te.holeSetBad")}
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-start">
                  {t("te.start")}
                </label>
                <input
                  id="t-start"
                  type="datetime-local"
                  value={toLocalInput(draft.startAt)}
                  onChange={(e) => patch({ startAt: fromLocalInput(e.target.value) })}
                  className={field}
                />
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-end">
                  {t("te.end")}
                </label>
                <input
                  id="t-end"
                  type="datetime-local"
                  value={toLocalInput(draft.endAt)}
                  onChange={(e) => patch({ endAt: fromLocalInput(e.target.value) })}
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.dates")}
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-fee">
                  {t("te.fee")}
                </label>
                <input
                  id="t-fee"
                  type="number"
                  min={0}
                  value={draft.entryFeePts}
                  onChange={(e) => patch({ entryFeePts: Number(e.target.value) })}
                  className={field}
                />
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-resolve">
                  {t("te.resolveDelay")}
                </label>
                <input
                  id="t-resolve"
                  type="number"
                  min={0}
                  value={draft.resolveDelayMinutes}
                  onChange={(e) => patch({ resolveDelayMinutes: Number(e.target.value) })}
                  className={field}
                />
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-botfield">
                  {t("te.botField")}
                </label>
                <select
                  id="t-botfield"
                  value={draft.botFieldId}
                  onChange={(e) => patch({ botFieldId: e.target.value })}
                  className={field}
                >
                  {BOT_FIELDS.map((f) => (
                    <option key={f.id} value={f.id}>
                      {f.label}
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.botFieldHint")}
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-league">
                  {t("te.league")}
                </label>
                <select
                  id="t-league"
                  value={draft.leagueKey ?? ""}
                  onChange={(e) => patch({ leagueKey: e.target.value })}
                  className={field}
                >
                  <option value="">(none)</option>
                  {LEAGUE_KEYS.map((k) => (
                    <option key={k} value={k}>
                      {k}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-sponsor">
                  {t("te.sponsor")}
                </label>
                <input
                  id="t-sponsor"
                  value={draft.sponsorName ?? ""}
                  onChange={(e) => patch({ sponsorName: e.target.value })}
                  placeholder="PUMA"
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.sponsor", { sponsor: (draft.sponsorName || "SPONSOR").toUpperCase() })}
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-namekey">
                  {t("te.locKey")}
                </label>
                <input
                  id="t-namekey"
                  value={draft.nameKey ?? ""}
                  onChange={(e) => patch({ nameKey: e.target.value })}
                  placeholder="tourn.kasumigaseki"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.nameKeyHint")}
                </p>
              </div>

              {/* ── Entry restrictions (tournament_restrictions §A2) ─────────
                   Null/blank = unrestricted. Server enforces max players +
                   character bands at POST /golfin/{slug}/enter; gear rule and
                   club rarity cap are client-enforced (review Q2/Q3). */}
              <div className="col-span-2 mt-1 border-t border-zinc-800 pt-4">
                <p className="text-[11px] uppercase tracking-wider text-zinc-500">
                  {t("te.restr")}
                </p>
                <p className="mt-1 text-[11px] text-zinc-600">{t("te.restr.hint")}</p>
                {restrError && (
                  <p className="mt-1 text-[11px] text-red-400">{restrError}</p>
                )}
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-category">
                  {t("te.restr.category")}
                </label>
                <select
                  id="t-category"
                  value={draft.category ?? ""}
                  onChange={(e) => patch({ category: e.target.value || null })}
                  className={field}
                >
                  <option value="">{t("te.restr.unset")}</option>
                  {CATEGORIES.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-divtype">
                  {t("te.restr.divType")}
                </label>
                <select
                  id="t-divtype"
                  value={draft.divisionType ?? ""}
                  onChange={(e) => patch({ divisionType: e.target.value || null })}
                  className={field}
                >
                  <option value="">{t("te.restr.unset")}</option>
                  {DIVISION_TYPES.map((d) => (
                    <option key={d} value={d}>
                      {d}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-maxplayers">
                  {t("te.restr.maxPlayers")}
                </label>
                <input
                  id="t-maxplayers"
                  type="number"
                  min={1}
                  value={draft.maxPlayers ?? ""}
                  onChange={(e) => patch({ maxPlayers: intOrNull(e.target.value) })}
                  placeholder={t("te.restr.unlimited")}
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">{t("te.restr.maxPlayersHint")}</p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-perdiv">
                  {t("te.restr.perDivision")}
                </label>
                <input
                  id="t-perdiv"
                  type="number"
                  min={1}
                  value={draft.playersPerDivision ?? ""}
                  onChange={(e) => patch({ playersPerDivision: intOrNull(e.target.value) })}
                  placeholder={t("te.restr.unlimited")}
                  className={field}
                />
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-rar-min">
                  {t("te.restr.charRarity")}
                </label>
                <div className="mt-1 flex items-center gap-2">
                  <select
                    id="t-rar-min"
                    value={draft.charRarityMin ?? ""}
                    onChange={(e) => patch({ charRarityMin: e.target.value || null })}
                    className={`${field} mt-0`}
                    aria-label={t("te.restr.min")}
                  >
                    <option value="">{t("te.restr.unset")}</option>
                    {RARITIES.map((r) => (
                      <option key={r} value={r}>
                        {r}
                      </option>
                    ))}
                  </select>
                  <span className="text-zinc-600">–</span>
                  <select
                    value={draft.charRarityMax ?? ""}
                    onChange={(e) => patch({ charRarityMax: e.target.value || null })}
                    className={`${field} mt-0`}
                    aria-label={t("te.restr.max")}
                  >
                    <option value="">{t("te.restr.unset")}</option>
                    {RARITIES.map((r) => (
                      <option key={r} value={r}>
                        {r}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-lvl-min">
                  {t("te.restr.charLevel")}
                </label>
                <div className="mt-1 flex items-center gap-2">
                  <input
                    id="t-lvl-min"
                    type="number"
                    min={1}
                    max={999}
                    value={draft.charLevelMin ?? ""}
                    onChange={(e) => patch({ charLevelMin: intOrNull(e.target.value) })}
                    placeholder={t("te.restr.min")}
                    className={`${field} mt-0`}
                  />
                  <span className="text-zinc-600">–</span>
                  <input
                    type="number"
                    min={1}
                    max={999}
                    value={draft.charLevelMax ?? ""}
                    onChange={(e) => patch({ charLevelMax: intOrNull(e.target.value) })}
                    placeholder={t("te.restr.max")}
                    className={`${field} mt-0`}
                    aria-label={t("te.restr.max")}
                  />
                </div>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-gear">
                  {t("te.restr.gear")}
                </label>
                <select
                  id="t-gear"
                  value={draft.gearRule ?? ""}
                  onChange={(e) => patch({ gearRule: e.target.value || null })}
                  className={field}
                >
                  <option value="">{t("te.restr.unset")}</option>
                  <option value="own">own</option>
                  {/* Q3 (ARCHITECT_REVIEW.md): authoring 'supplied' is blocked until
                      the standard-spec task actually supplies a set in game. */}
                  <option value="supplied" disabled>
                    supplied — {t("te.restr.suppliedBlocked")}
                  </option>
                </select>
                <p className="mt-1 text-[11px] text-zinc-600">{t("te.restr.gearHint")}</p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-clubcap">
                  {t("te.restr.clubCap")}
                </label>
                <select
                  id="t-clubcap"
                  value={draft.clubRarityMax ?? ""}
                  onChange={(e) => patch({ clubRarityMax: e.target.value || null })}
                  className={field}
                >
                  <option value="">{t("te.restr.unset")}</option>
                  {RARITIES.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-[11px] text-zinc-600">{t("te.restr.clubCapHint")}</p>
              </div>

              {/* ── Sign-up modal blurb (tournament_signup_modal §6) ───────── */}
              <div className="col-span-2 mt-1 border-t border-zinc-800 pt-4">
                <p className="text-[11px] uppercase tracking-wider text-zinc-500">
                  {t("te.signupDesc")}
                </p>
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.hint.desc")}
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-desc-en">
                  {t("te.descEn")}
                </label>
                <textarea
                  id="t-desc-en"
                  rows={5}
                  maxLength={600}
                  value={draft.descriptionEn ?? ""}
                  onChange={(e) => patch({ descriptionEn: e.target.value })}
                  placeholder="Compete in the prestigious Gold Tournament at Lomond Club…"
                  className={`${field} resize-y`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  <span
                    className={
                      (draft.descriptionEn?.trim().length ?? 0) > 600
                        ? "text-red-400"
                        : "text-zinc-500"
                    }
                  >
                    {draft.descriptionEn?.trim().length ?? 0}/600
                  </span>{" "}
                  — the box is a fixed 360px tall on device, so long copy overflows rather than
                  scrolling. The Figma reference is 268 characters.
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-desc-ja">
                  {t("te.descJa")}
                </label>
                <textarea
                  id="t-desc-ja"
                  rows={5}
                  maxLength={600}
                  lang="ja"
                  value={draft.descriptionJa ?? ""}
                  onChange={(e) => patch({ descriptionJa: e.target.value })}
                  placeholder="日本有数の景観と難易度を誇るクラブでのトーナメント。"
                  className={`${field} resize-y`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  <span
                    className={
                      (draft.descriptionJa?.trim().length ?? 0) > 600
                        ? "text-red-400"
                        : "text-zinc-500"
                    }
                  >
                    {draft.descriptionJa?.trim().length ?? 0}/600
                  </span>{" "}
                  — shown only to players on Japanese. An English player never sees it, even when
                  the English box above is empty.
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-desckey">
                  {t("te.descKey")}
                </label>
                <input
                  id="t-desckey"
                  value={draft.descriptionKey ?? ""}
                  onChange={(e) => patch({ descriptionKey: e.target.value })}
                  placeholder="tourn.desc.kasumigaseki"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {t("te.descKeyHint")}
                </p>
              </div>
            </div>
          )}

          {tab === "prizes" && (
            <PrizeEditor
              bands={draft.bands}
              errors={bandErrors}
              pool={pool}
              onChange={(bands) => patch({ bands })}
            />
          )}

          {tab === "artwork" && (
            <ArtworkTab
              slug={draft.slug}
              bannerUrl={draft.bannerUrl}
              layer={layer}
              courseArt={course?.art ?? null}
              onChange={(url) => patch({ bannerUrl: url })}
              onNotice={setNotice}
              modalBannerId={draft.modalBannerId}
              onModalBannerChange={(id) => patch({ modalBannerId: id })}
            />
          )}

          {tab === "entries" && tournament && <EntriesTab tournamentId={tournament.id} />}
        </div>

        <footer className="border-t border-surface-800 px-5 py-3">
          {error && (
            <p className="mb-2 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
              {error}
            </p>
          )}
          {notice && !error && (
            <p className="mb-2 rounded-md border border-accent-500/40 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
              {notice}
            </p>
          )}

          {danger === "delete" && tournament && (
            <div className="mb-2 rounded-md border border-red-500/50 bg-red-500/10 px-3 py-2 text-xs text-red-200">
              {t("te.deleting")} <code>{tournament.slug}</code>{" "}
              {t("te.deleteCascade", {
                entries: tournament.entryCount,
                human: tournament.humanEntryCount,
                bands: tournament.bands.length,
              })}
              <input
                value={confirmSlug}
                onChange={(e) => setConfirmSlug(e.target.value)}
                placeholder={tournament.slug ?? ""}
                className="mt-2 w-full rounded-md border border-red-500/50 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
              />
            </div>
          )}

          {danger === "duplicate" && tournament && (
            <div className="mb-2 rounded-md border border-surface-700 bg-surface-850 px-3 py-2 text-xs text-zinc-300">
              {t("te.copy")} <code>{tournament.slug}</code>{" "}
              {t("te.duplicateHint")}
              <input
                value={dupSlug}
                onChange={(e) => setDupSlug(e.target.value.trim().toLowerCase())}
                placeholder={`${tournament.slug}_2`}
                className="mt-2 w-full rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-100 focus:outline-none"
              />
            </div>
          )}

          <div className="flex items-center gap-2">
            {!isNew && (
              <>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() =>
                    danger === "duplicate" ? runDuplicate() : (setDanger("duplicate"), setDupSlug(""))
                  }
                  className="rounded-md border border-surface-700 px-3 py-1.5 text-xs font-medium text-zinc-300 hover:bg-surface-800 disabled:opacity-50"
                >
                  {danger === "duplicate" ? t("te.createCopy") : t("te.duplicate")}
                </button>
                <button
                  type="button"
                  disabled={busy || (danger === "delete" && confirmSlug !== tournament!.slug)}
                  onClick={() => (danger === "delete" ? runDelete() : setDanger("delete"))}
                  className="rounded-md border border-red-500/50 px-3 py-1.5 text-xs font-medium text-red-300 hover:bg-red-500/10 disabled:opacity-40"
                >
                  {danger === "delete" ? t("te.deleteReal") : t("te.delete")}
                </button>
              </>
            )}
            <div className="ml-auto flex items-center gap-2">
              {bandErrors.length > 0 && (
                <span className="text-[11px] text-amber-400">
                  {bandErrors.length} prize issue{bandErrors.length > 1 ? "s" : ""}
                </span>
              )}
              <button
                type="button"
                onClick={onClose}
                className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
              >
                {t("common.cancel")}
              </button>
              <button
                type="button"
                disabled={busy || bandErrors.length > 0 || (live && confirmSlug !== draft.slug)}
                onClick={save}
                className="rounded-md bg-accent-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
              >
                {busy ? t("te.saving") : isNew ? t("te.create") : t("te.save")}
              </button>
            </div>
          </div>
        </footer>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------

function PrizeEditor({
  bands,
  errors,
  pool,
  onChange,
}: {
  bands: PrizeBand[];
  errors: string[];
  pool: { top: number; total: number; places: number };
  onChange: (bands: PrizeBand[]) => void;
}) {
  const t = useT();
  const sorted = [...bands].sort((a, b) => a.rankFrom - b.rankFrom);

  function update(index: number, next: Partial<PrizeBand>) {
    onChange(bands.map((b, i) => (i === index ? { ...b, ...next } : b)));
  }
  function remove(index: number) {
    onChange(bands.filter((_, i) => i !== index));
  }
  function add() {
    const last = sorted[sorted.length - 1];
    const from = last ? last.rankTo + 1 : 1;
    onChange([...bands, { id: "", rankFrom: from, rankTo: from, rpReward: 0, itemRewardId: null }]);
  }

  return (
    <div>
      <div className="flex items-baseline justify-between">
        <h3 className="text-sm font-semibold text-zinc-200">{t("te.rankBands")}</h3>
        <span className="text-xs text-zinc-500">
          {t("te.poolSummary", {
            top: pool.top.toLocaleString(),
            places: pool.places,
            total: pool.total.toLocaleString(),
          })}
        </span>
      </div>

      <table className="mt-3 w-full text-left text-sm">
        <thead className="text-[11px] uppercase tracking-wider text-zinc-500">
          <tr>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.band.from")}</th>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.band.to")}</th>
            <th className="pb-1.5 font-medium">RP</th>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.itemReward")}</th>
            <th className="pb-1.5" />
          </tr>
        </thead>
        <tbody>
          {bands.map((b, i) => (
            <tr key={i}>
              <td className="py-1 pr-2">
                <input
                  type="number"
                  min={1}
                  value={b.rankFrom}
                  onChange={(e) => update(i, { rankFrom: Number(e.target.value) })}
                  aria-label={`Band ${i + 1} rank from`}
                  className="w-20 rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-sm text-zinc-200 focus:border-accent-500 focus:outline-none"
                />
              </td>
              <td className="py-1 pr-2">
                <input
                  type="number"
                  min={1}
                  value={b.rankTo}
                  onChange={(e) => update(i, { rankTo: Number(e.target.value) })}
                  aria-label={`Band ${i + 1} rank to`}
                  className="w-20 rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-sm text-zinc-200 focus:border-accent-500 focus:outline-none"
                />
              </td>
              <td className="py-1 pr-2">
                <input
                  type="number"
                  min={0}
                  value={b.rpReward}
                  onChange={(e) => update(i, { rpReward: Number(e.target.value) })}
                  aria-label={`Band ${i + 1} RP`}
                  className="w-24 rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-sm tabular-nums text-zinc-200 focus:border-accent-500 focus:outline-none"
                />
              </td>
              <td className="py-1 pr-2">
                <input
                  value={b.itemRewardId ?? ""}
                  onChange={(e) => update(i, { itemRewardId: e.target.value || null })}
                  placeholder={t("common.none")}
                  aria-label={`Band ${i + 1} item reward`}
                  className="w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 font-mono text-xs text-zinc-300 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
                />
              </td>
              <td className="py-1 text-right">
                <button
                  type="button"
                  onClick={() => remove(i)}
                  aria-label={t("art.removeBand", { n: i + 1 })}
                  className="rounded-md border border-surface-700 px-2 py-1 text-xs text-zinc-500 hover:bg-surface-800 hover:text-red-400"
                >
                  ✕
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <button
        type="button"
        onClick={add}
        className="mt-3 rounded-md border border-surface-700 px-3 py-1.5 text-xs font-medium text-zinc-300 hover:bg-surface-800"
      >
        {t("te.addBand")}
      </button>

      {errors.length > 0 ? (
        <ul className="mt-3 space-y-1 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
          {errors.map((e) => (
            <li key={e}>• {e}</li>
          ))}
        </ul>
      ) : (
        <p className="mt-3 rounded-md border border-accent-500/30 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          {t("te.bandsOk")}
        </p>
      )}

      <p className="mt-3 text-[11px] leading-relaxed text-zinc-600">
        {t("te.hint.bands")}
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------

function ArtworkTab({
  slug,
  bannerUrl,
  layer,
  courseArt,
  onChange,
  onNotice,
  modalBannerId,
  onModalBannerChange,
}: {
  slug: string;
  bannerUrl: string | null;
  layer: "remote" | "bundled" | "placeholder";
  courseArt: string | null;
  onChange: (url: string | null) => void;
  onNotice: (message: string | null) => void;
  modalBannerId: string | null;
  onModalBannerChange: (id: string | null) => void;
}) {
  const t = useT();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [aspectWarning, setAspectWarning] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);

  async function onFile(file: File) {
    setLocalError(null);
    setAspectWarning(null);
    onNotice(null);

    if (!slug) {
      setLocalError(t("te.slugFirst"));
      return;
    }
    if (!(ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
      setLocalError(t("art.unsupportedType", { type: file.type || "unknown" }));
      return;
    }
    if (file.size > ART_SPEC.maxBytes) {
      setLocalError(
        t("art.tooBig", {
          kb: (file.size / 1024).toFixed(0),
          cap: ART_SPEC.maxBytes / 1024,
        })
      );
      return;
    }

    const objectUrl = URL.createObjectURL(file);
    setPreview(objectUrl);

    // Aspect check is a warning, not a block — the shipped set is not uniform
    // either (kisarazu and kawana are already off-spec).
    await new Promise<void>((resolve) => {
      const img = new Image();
      img.onload = () => {
        const ratio = img.width / img.height;
        const drift = Math.abs(ratio - ART_SPEC.aspect) / ART_SPEC.aspect;
        if (drift > ART_SPEC.aspectTolerance) {
          setAspectWarning(
            t("art.aspectWarn", {
              w: img.width,
              h: img.height,
              ratio: ratio.toFixed(2),
              sw: ART_SPEC.width,
              sh: ART_SPEC.height,
              saspect: ART_SPEC.aspect.toFixed(2),
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
      form.set("slug", slug);
      const res = await fetch("/api/tournaments/art", { method: "POST", body: form });
      const body = (await res.json().catch(() => null)) as {
        url?: string;
        message?: string;
        error?: string;
      } | null;
      if (!res.ok) throw new Error(body?.error ?? `{t("te.uploadFailed")} (${res.status})`);
      onChange(body?.url ?? null);
      onNotice(`${body?.message ?? t("te.uploaded")} Save the tournament to publish it.`);
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : t("te.uploadFailed"));
    } finally {
      setBusy(false);
    }
  }

  const layerCopy: Record<typeof layer, { text: string; className: string }> = {
    remote: {
      text: t("te.art.remote"),
      className: "border-accent-500/40 bg-accent-500/10 text-accent-300",
    },
    bundled: {
      text: t("te.art.bundled", { art: courseArt ?? "" }),
      className: "border-surface-700 bg-surface-850 text-zinc-300",
    },
    placeholder: {
      text: t("te.art.placeholder"),
      className: "border-amber-500/40 bg-amber-500/10 text-amber-200",
    },
  };

  return (
    <div>
      <h3 className="text-sm font-semibold text-zinc-200">{t("te.cardArt")}</h3>
      <p className={`mt-2 rounded-md border px-3 py-2 text-xs ${layerCopy[layer].className}`}>
        <strong className="font-semibold uppercase tracking-wider">{layer}</strong> ·{" "}
        {layerCopy[layer].text}
      </p>

      <div className="mt-4 flex gap-5">
        <div
          className="shrink-0 overflow-hidden rounded-lg border border-surface-700 bg-surface-950"
          style={{ width: ART_SPEC.width / 1.6, height: ART_SPEC.height / 1.6 }}
        >
          {preview || bannerUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={preview ?? bannerUrl ?? ""}
              alt="Tournament card preview"
              className="h-full w-full object-cover"
            />
          ) : (
            <div className="flex h-full items-center justify-center px-3 text-center text-[11px] text-zinc-600">
              {courseArt ? t("te.bundledArt", { file: courseArt }) : t("te.placeholderArt")}
            </div>
          )}
        </div>

        <div className="min-w-0 flex-1">
          <input
            ref={inputRef}
            type="file"
            accept={ART_SPEC.mimeTypes.join(",")}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) void onFile(f);
            }}
            className="block w-full text-xs text-zinc-400 file:mr-3 file:rounded-md file:border-0 file:bg-surface-700 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-zinc-200 hover:file:bg-surface-800"
          />
          <p className="mt-2 text-[11px] leading-relaxed text-zinc-600">
            {t("te.artHint", {
              maxKb: ART_SPEC.maxBytes / 1024,
              w: ART_SPEC.width,
              h: ART_SPEC.height,
            })}
          </p>

          {busy && <p className="mt-2 text-xs text-zinc-400">{t("te.uploading")}</p>}
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

          {bannerUrl && (
            <div className="mt-3">
              <div className="break-all font-mono text-[10px] text-zinc-600">{bannerUrl}</div>
              <button
                type="button"
                onClick={() => {
                  onChange(null);
                  setPreview(null);
                  if (inputRef.current) inputRef.current.value = "";
                }}
                className="mt-2 rounded-md border border-surface-700 px-2.5 py-1 text-xs text-zinc-400 hover:bg-surface-800"
              >
                {t("te.removeArt")}
              </button>
            </div>
          )}
        </div>
      </div>

      <ModalBannerPicker value={modalBannerId} onChange={onModalBannerChange} />
    </div>
  );
}

/**
 * Assign the sign-up modal's cross-promotion strip (tournament_banners §3.2).
 *
 * A PICKER, deliberately not an uploader: banner bytes are uploaded once in the
 * {t("te.bannersPanel")} and one promo then serves every tournament. Adding a second
 * upload control here would defeat the whole point of managing them centrally.
 *
 * Only ACTIVE tournament_modal banners are listed — assigning an inactive one
 * would look like it worked and show nothing in game, because the endpoint
 * applies is_active server-side.
 */
function ModalBannerPicker({
  value,
  onChange,
}: {
  value: string | null;
  onChange: (id: string | null) => void;
}) {
  const t = useT();
  const [banners, setBanners] = useState<BannerRow[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/api/banners");
        const body = (await res.json()) as { banners?: BannerRow[]; error?: string };
        if (cancelled) return;
        if (!res.ok) {
          setLoadError(body.error ?? `Could not load banners (${res.status}).`);
          return;
        }
        setBanners(body.banners ?? []);
      } catch (err) {
        if (!cancelled) setLoadError(err instanceof Error ? err.message : t("te.bannersFailed"));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const options = (banners ?? []).filter((b) => b.placement === "tournament_modal" && b.isActive);
  const selected = options.find((b) => b.id === value) ?? null;
  // An assignment pointing at a row that is no longer active/eligible still has to
  // be visible, or the operator cannot see why the strip vanished in game.
  const orphaned = value !== null && selected === null && banners !== null;

  return (
    <div className="mt-6 border-t border-surface-800 pt-5">
      <label className={label} htmlFor="t-modal-banner">
        {t("te.signupBanner")}
      </label>
      <p className="mt-1 mb-2 text-[11px] leading-relaxed text-zinc-600">
        {t("te.bannerHint1")}{" "}
        <a href="/banners" className="text-accent-400 underline hover:text-accent-300">
          {t("te.bannersPanel")}
        </a>{" "}
        {t("te.bannerHint2")}
      </p>

      {loadError ? (
        <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {loadError}
        </p>
      ) : (
        <>
          <select
            id="t-modal-banner"
            value={value ?? ""}
            disabled={banners === null}
            onChange={(e) => onChange(e.target.value || null)}
            className={field}
          >
            <option value="">{t("te.noBanner")}</option>
            {options.map((b) => (
              <option key={b.id} value={b.id}>
                {b.label}
              </option>
            ))}
            {orphaned && <option value={value ?? ""}>{t("te.currentAssignment")}</option>}
          </select>

          {banners === null && <p className="mt-2 text-[11px] text-zinc-600">{t("te.bannersLoading")}</p>}

          {banners !== null && options.length === 0 && (
            <p className="mt-2 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-[11px] text-zinc-500">
              {t("te.noActive")} <code>tournament_modal</code> {t("te.noActiveTail")}{" "}
              <a href="/banners" className="text-accent-400 underline hover:text-accent-300">
                {t("te.bannersPanel")}
              </a>{" "}
              {t("te.noActiveTail2")}
            </p>
          )}

          {orphaned && (
            <p className="mt-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-[11px] text-amber-200">
              {t("te.hint.orphanBanner")}
            </p>
          )}

          {selected && (
            <div className="mt-3 flex items-start gap-3">
              <div className="h-[54px] w-[208px] shrink-0 overflow-hidden rounded-md border border-surface-700 bg-surface-950">
                {selected.imageUrlEn ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={selected.imageUrlEn}
                    alt={selected.label}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <div className="flex h-full items-center justify-center text-[10px] text-zinc-600">
                    {t("te.noArtUploaded")}
                  </div>
                )}
              </div>
              <div className="min-w-0 text-[11px] text-zinc-500">
                <div className="text-zinc-300">{selected.label}</div>
                {selected.linkUrl ? (
                  <div className="mt-0.5 break-all font-mono text-[10px] text-zinc-600">
                    {t("te.tapsOpen")} {selected.linkUrl}
                  </div>
                ) : (
                  <div className="mt-0.5">{t("te.notTappable")}</div>
                )}
                {!selected.imageUrlJa && (
                  <div className="mt-0.5">{t("te.art.jpFallback")}</div>
                )}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------

function EntriesTab({ tournamentId }: { tournamentId: string }) {
  const t = useT();
  const [data, setData] = useState<TournamentEntriesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const res = await fetch(`/api/tournaments/${tournamentId}/entries`);
        const body = (await res.json().catch(() => null)) as
          | (TournamentEntriesResponse & { error?: string })
          | null;
        if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
        if (!cancelled && body) setData(body);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : t("te.entriesFailed"));
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [tournamentId]);

  if (error) {
    return (
      <p className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
        {error}
      </p>
    );
  }
  if (!data) return <p className="text-sm text-zinc-500">{t("te.entriesLoading")}</p>;
  if (data.entries.length === 0) {
    return (
      <p className="text-sm text-zinc-600">
        {t("te.noEntries")}
      </p>
    );
  }

  return (
    <div>
      <p className="mb-3 text-xs text-zinc-500">
        Read-only. {data.entries.filter((e) => !e.isBot).length} human ·{" "}
        {data.entries.filter((e) => e.isBot).length} bot.
      </p>
      <table className="w-full text-left text-sm">
        <thead className="text-[11px] uppercase tracking-wider text-zinc-500">
          <tr>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.col.player")}</th>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.col.character")}</th>
            <th className="whitespace-nowrap pb-1.5 text-right font-medium">{t("te.col.score")}</th>
            <th className="whitespace-nowrap pb-1.5 text-right font-medium">{t("te.col.holes")}</th>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.col.status")}</th>
            <th className="whitespace-nowrap pb-1.5 font-medium">{t("te.col.submitted")}</th>
          </tr>
        </thead>
        <tbody>
          {data.entries.map((e) => (
            <tr key={e.id} className="border-t border-surface-800">
              <td className="py-1.5 text-xs text-zinc-300">
                {e.displayName ?? e.userEmail ?? "(unknown)"}
                {e.isBot && (
                  <span className="ml-1.5 rounded bg-surface-700 px-1 py-0.5 text-[9px] font-bold tracking-wider text-zinc-400">
                    BOT
                  </span>
                )}
              </td>
              <td className="py-1.5 font-mono text-[11px] text-zinc-500">
                {e.characterId ?? "—"}
              </td>
              <td className="py-1.5 text-right text-xs tabular-nums text-zinc-200">
                {e.bestScore ?? "—"}
              </td>
              <td className="py-1.5 text-right text-xs tabular-nums text-zinc-400">
                {e.holesCompleted}
              </td>
              <td className="py-1.5 text-xs text-zinc-400">{e.status}</td>
              <td className="py-1.5 text-[11px] text-zinc-500">{fmtDateTime(e.submittedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
