"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { ART_SPEC, BOT_FIELDS, LEAGUE_KEYS, SHIPPING_COURSES, findCourse } from "@/lib/courses";
import { fmtDateTime } from "@/lib/format";
import {
  artLayer,
  deriveState,
  expandHoleSet,
  isLive,
  prizePoolSummary,
  validatePrizeBands,
} from "@/lib/tournament";
import type {
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
    isActive: true,
    bands: [
      { id: "", rankFrom: 1, rankTo: 1, rpReward: 300, itemRewardId: null },
      { id: "", rankFrom: 2, rankTo: 3, rpReward: 150, itemRewardId: null },
      { id: "", rankFrom: 4, rankTo: 10, rpReward: 50, itemRewardId: null },
    ],
  };
}

function toDraft(t: TournamentRow): TournamentInput {
  return {
    slug: t.slug ?? "",
    title: t.title,
    titleJa: t.titleJa ?? "",
    nameKey: t.nameKey ?? "",
    courseId: t.courseId ?? "",
    holeSet: t.holeSet ?? "1-18",
    startAt: t.startAt ?? "",
    endAt: t.endAt ?? "",
    resolveDelayMinutes: t.resolveDelayMinutes ?? 30,
    entryFeePts: t.entryFeePts,
    botFieldId: t.botFieldId ?? "",
    sponsorName: t.sponsorName ?? "",
    leagueKey: t.leagueKey ?? "",
    bannerUrl: t.bannerUrl,
    isActive: t.isActive,
    bands: t.bands.map((b) => ({ ...b })),
  };
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
      onSaved(body?.message ?? "Saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Save failed");
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
      onSaved(body?.message ?? "Deleted.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Delete failed");
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
      onSaved(body?.message ?? "Duplicated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Duplicate failed");
    } finally {
      setBusy(false);
    }
  }

  const tabs: { id: Tab; title: string; hide?: boolean }[] = [
    { id: "details", title: "Details" },
    { id: "prizes", title: `Prizes (${draft.bands.length})` },
    { id: "artwork", title: "Artwork" },
    { id: "entries", title: `Entries${tournament ? ` (${tournament.entryCount})` : ""}`, hide: isNew },
  ];

  return (
    <div className="fixed inset-0 z-40" role="dialog" aria-modal="true">
      <button
        type="button"
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-black/60"
      />

      <div className="absolute right-0 top-0 flex h-full w-full max-w-2xl flex-col border-l border-surface-700 bg-surface-900 shadow-2xl">
        <header className="border-b border-surface-800 px-5 py-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h2 className="truncate text-base font-semibold text-zinc-100">
                {isNew ? "New tournament" : draft.title || draft.slug}
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
              Close
            </button>
          </div>

          {live && (
            <div className="mt-3 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
              <strong className="font-semibold">This tournament is {state}.</strong> Players may be
              mid-entry — changing the fee, dates or prize ladder now changes the deal underneath
              them. Saving requires re-typing the slug below.
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
                      {draft.isActive ? "Active — the game receives this" : "Inactive — hidden from the game"}
                    </div>
                    <p className="mt-0.5 text-[11px] leading-relaxed text-zinc-500">
                      Separate from Upcoming/Open/Ended, which is derived from the dates. This is
                      whether the game is told the tournament exists at all. Switching it off does
                      not eject a player who has already entered — they finish, nobody new sees it.
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={draft.isActive}
                    aria-label="Active"
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
                  Slug (game id)
                </label>
                <input
                  id="t-slug"
                  value={draft.slug}
                  onChange={(e) => patch({ slug: e.target.value.trim().toLowerCase() })}
                  placeholder="kasumigaseki_open"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  Stable key the client keys off. Changing it on a live tournament orphans entries
                  in any client that cached the old id.
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-title">
                  Title
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
                    <strong className="font-semibold">Players will not see this title.</strong> The
                    localization key <code>{draft.nameKey.trim()}</code> is set, and a key that
                    resolves in the shipped build always wins — the title is only the fallback.
                    Clear the key to make this title the name players see, in every language.
                    <button
                      type="button"
                      onClick={() => patch({ nameKey: "" })}
                      className="mt-1.5 block rounded-md border border-amber-500/50 px-2 py-1 font-medium text-amber-100 hover:bg-amber-500/15"
                    >
                      Clear the key and use this title
                    </button>
                  </div>
                ) : (
                  <p className="mt-1 text-[11px] text-zinc-600">
                    Free text, and independent of the venue — a tournament can be brand-led (“PUMA
                    Summer Slam” at Lomond). This is what players see, since no localization key
                    is set.
                  </p>
                )}
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-title-ja">
                  Title (Japanese)
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
                  Shown to players on Japanese. Leave empty and they see the title above.
                  {draft.nameKey?.trim()
                    ? " Currently unused — the localization key overrides both titles."
                    : ""}
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-course">
                  Venue (playable course)
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
                  Where it is played, and the venue subtitle. Default art only —{" "}
                  <code className="text-zinc-500">{course?.art ?? "none"}</code>
                  {course && !course.playable && (
                    <span className="text-amber-400/80">
                      {" "}
                      · no playable hole data ships for this course yet — holes fall back to Lomond
                    </span>
                  )}
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-holeset">
                  Hole set
                </label>
                <input
                  id="t-holeset"
                  value={draft.holeSet}
                  onChange={(e) => patch({ holeSet: e.target.value })}
                  placeholder="1-18"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  {holes.length > 0 ? `${holes.length} holes` : "malformed"} · ranges and lists,
                  expanded client-side
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-start">
                  Start (UTC)
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
                  End (UTC)
                </label>
                <input
                  id="t-end"
                  type="datetime-local"
                  value={toLocalInput(draft.endAt)}
                  onChange={(e) => patch({ endAt: fromLocalInput(e.target.value) })}
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  Absolute UTC on purpose. State is derived from these two — there is no status
                  switch to flip.
                </p>
              </div>

              <div className="col-span-1">
                <label className={label} htmlFor="t-fee">
                  Entry fee (RP)
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
                  Resolve delay (minutes)
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
                  Bot field
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
                  Filler so a young leaderboard is never empty. Bots are never paid.
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-league">
                  League
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
                  Sponsor
                </label>
                <input
                  id="t-sponsor"
                  value={draft.sponsorName ?? ""}
                  onChange={(e) => patch({ sponsorName: e.target.value })}
                  placeholder="PUMA"
                  className={field}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  Text only — renders as “{(draft.sponsorName || "SPONSOR").toUpperCase()} PRESENTS”.
                </p>
              </div>
              <div className="col-span-1">
                <label className={label} htmlFor="t-namekey">
                  Localization key
                </label>
                <input
                  id="t-namekey"
                  value={draft.nameKey ?? ""}
                  onChange={(e) => patch({ nameKey: e.target.value })}
                  placeholder="tourn.kasumigaseki"
                  className={`${field} font-mono`}
                />
                <p className="mt-1 text-[11px] text-zinc-600">
                  Optional, and it <strong className="text-zinc-400">overrides the title</strong>{" "}
                  whenever it resolves in the shipped build. Keys ship inside the app, so a key
                  invented here resolves nowhere and the title is used instead. Leave it empty for
                  anything you name yourself.
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
              Deleting <code>{tournament.slug}</code> cascades{" "}
              <strong>{tournament.entryCount} entries</strong> (
              {tournament.humanEntryCount} human) and {tournament.bands.length} prize bands. Type
              the slug to confirm.
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
              Copy <code>{tournament.slug}</code> — same course, holes, fee and prize ladder, dates
              shifted forward one cycle, artwork not copied. New slug:
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
                  {danger === "duplicate" ? "Create copy" : "Duplicate"}
                </button>
                <button
                  type="button"
                  disabled={busy || (danger === "delete" && confirmSlug !== tournament!.slug)}
                  onClick={() => (danger === "delete" ? runDelete() : setDanger("delete"))}
                  className="rounded-md border border-red-500/50 px-3 py-1.5 text-xs font-medium text-red-300 hover:bg-red-500/10 disabled:opacity-40"
                >
                  {danger === "delete" ? "Delete for real" : "Delete"}
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
                Cancel
              </button>
              <button
                type="button"
                disabled={busy || bandErrors.length > 0 || (live && confirmSlug !== draft.slug)}
                onClick={save}
                className="rounded-md bg-accent-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-accent-500 disabled:opacity-40"
              >
                {busy ? "Saving…" : isNew ? "Create tournament" : "Save changes"}
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
        <h3 className="text-sm font-semibold text-zinc-200">Rank bands</h3>
        <span className="text-xs text-zinc-500">
          top {pool.top.toLocaleString()} RP · {pool.places} paid places · {pool.total.toLocaleString()} RP
          total if every place fills
        </span>
      </div>

      <table className="mt-3 w-full text-left text-sm">
        <thead className="text-[11px] uppercase tracking-wider text-zinc-500">
          <tr>
            <th className="pb-1.5 font-medium">From</th>
            <th className="pb-1.5 font-medium">To</th>
            <th className="pb-1.5 font-medium">RP</th>
            <th className="pb-1.5 font-medium">Item reward</th>
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
                  placeholder="(none)"
                  aria-label={`Band ${i + 1} item reward`}
                  className="w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 font-mono text-xs text-zinc-300 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
                />
              </td>
              <td className="py-1 text-right">
                <button
                  type="button"
                  onClick={() => remove(i)}
                  aria-label={`Remove band ${i + 1}`}
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
        + Add band
      </button>

      {errors.length > 0 ? (
        <ul className="mt-3 space-y-1 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
          {errors.map((e) => (
            <li key={e}>• {e}</li>
          ))}
        </ul>
      ) : (
        <p className="mt-3 rounded-md border border-accent-500/30 bg-accent-500/10 px-3 py-2 text-xs text-accent-300">
          Ladder is continuous from rank 1 with no gaps or overlaps.
        </p>
      )}

      <p className="mt-3 text-[11px] leading-relaxed text-zinc-600">
        Bands are per-tournament, not a shared template: raising this tournament&apos;s first prize
        cannot silently change another&apos;s. Payouts run through <code>earn_pts_v2</code> under the{" "}
        <code>tournament_prize</code> action, capped at 2000 RP per event.
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
}: {
  slug: string;
  bannerUrl: string | null;
  layer: "remote" | "bundled" | "placeholder";
  courseArt: string | null;
  onChange: (url: string | null) => void;
  onNotice: (message: string | null) => void;
}) {
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
      setLocalError("Give the tournament a slug first — the file is named after it.");
      return;
    }
    if (!(ART_SPEC.mimeTypes as readonly string[]).includes(file.type)) {
      setLocalError(`Unsupported type "${file.type || "unknown"}". Use JPG, PNG or WebP.`);
      return;
    }
    if (file.size > ART_SPEC.maxBytes) {
      setLocalError(
        `${(file.size / 1024).toFixed(0)} KB exceeds the ${ART_SPEC.maxBytes / 1024} KB cap. Every mobile player downloads this once.`
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
            `${img.width}×${img.height} (ratio ${ratio.toFixed(2)}) — cards are ${ART_SPEC.width}×${ART_SPEC.height} (${ART_SPEC.aspect.toFixed(2)}). It will be cropped or letterboxed.`
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
      if (!res.ok) throw new Error(body?.error ?? `Upload failed (${res.status})`);
      onChange(body?.url ?? null);
      onNotice(`${body?.message ?? "Uploaded."} Save the tournament to publish it.`);
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setBusy(false);
    }
  }

  const layerCopy: Record<typeof layer, { text: string; className: string }> = {
    remote: {
      text: "Remote — the uploaded image, fetched and disk-cached by the client.",
      className: "border-accent-500/40 bg-accent-500/10 text-accent-300",
    },
    bundled: {
      text: `Bundled — the shipped venue photo (${courseArt}). Fine for a venue-named event, but a brand tournament needs its own art: every tournament on this course looks identical without one.`,
      className: "border-surface-700 bg-surface-850 text-zinc-300",
    },
    placeholder: {
      text: "Placeholder — no remote art and no bundled photo for this course. The card will render the fallback sprite.",
      className: "border-amber-500/40 bg-amber-500/10 text-amber-200",
    },
  };

  return (
    <div>
      <h3 className="text-sm font-semibold text-zinc-200">Card artwork</h3>
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
              {courseArt ? `bundled: ${courseArt}` : "placeholder"}
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
            JPG / PNG / WebP · max {ART_SPEC.maxBytes / 1024} KB · {ART_SPEC.width}×
            {ART_SPEC.height} card. Uploaded to the project&apos;s{" "}
            <code>tournament-art</code> bucket under an immutable content-hashed name, so the URL is
            its own cache key. The client accepts only URLs on that host.
          </p>

          {busy && <p className="mt-2 text-xs text-zinc-400">Uploading…</p>}
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
                Remove — fall back to the course photo
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------

function EntriesTab({ tournamentId }: { tournamentId: string }) {
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
          setError(err instanceof Error ? err.message : "Failed to load entries");
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
  if (!data) return <p className="text-sm text-zinc-500">Loading entries…</p>;
  if (data.entries.length === 0) {
    return (
      <p className="text-sm text-zinc-600">
        No entries yet. Bot filler is generated at resolve time, not stored up front.
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
            <th className="pb-1.5 font-medium">Player</th>
            <th className="pb-1.5 font-medium">Character</th>
            <th className="pb-1.5 text-right font-medium">Score</th>
            <th className="pb-1.5 text-right font-medium">Holes</th>
            <th className="pb-1.5 font-medium">Status</th>
            <th className="pb-1.5 font-medium">Submitted</th>
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
