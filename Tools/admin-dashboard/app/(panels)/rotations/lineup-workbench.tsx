"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import { fmtDateTime } from "@/lib/format";
import {
  archivableRotations,
  archiveRows,
  calendarWeeks,
  generateLineup,
  isoInstant,
  materializeLineup,
  newRotationRow,
  parseInstant,
  publishRotationChain,
  ROTATION_PUBLISH_ORDER,
  rotationSeed,
  rotationState,
  type CalendarCell,
  type CalendarState,
  type Lineup,
  type LineupPick,
  type MaterializeCounts,
  type OutRow,
  type PublishChainResult,
  type RotationCatalog,
} from "@/lib/rotation";
import type { ContentStoredRow } from "@/lib/types";
import { RarityBadge } from "../_content/badges";
import { fetchDiff, fetchRows, publishCatalog, saveRow } from "../_content/client";

/**
 * The Lineup workbench (weekly_rotation_admin §4) — the part of the Rotations
 * panel that ACTS on a rotation row. The CatalogPanel below it is where the
 * row is edited; this is where it is turned into the week.
 *
 * Four verbs, and their asymmetry is the design (the Daily Missions panel is
 * the precedent): PREVIEW is pure and writes nothing; MATERIALIZE writes the
 * previewed rows as DRAFTS through the same `PUT /rows` the row editor uses,
 * one row at a time; PUBLISH ROTATION is the ordinary publish, five catalogs
 * in dependency order, stopping at the first refusal; ARCHIVE ENDED
 * deactivates (never deletes) what an ended week owned. No route exists for
 * any of this — every write is `upsertDraftRow` or `publishCatalog`, so the
 * audit log carries each one under the actions it already has
 * (`content.draft.*:<catalog>` per row, `content.publish:<catalog>` per
 * catalog with the rotation named in the publish note).
 *
 * EVERYTHING THE GENERATOR READS IS READ WHOLE, from the DRAFT rows of the
 * eight catalogs, paged at the route's maximum — the same rows the validator
 * will resolve against at publish, so what the preview shows and what publish
 * accepts cannot disagree. Clubs is 799 rows; four pages. That is the one
 * place this panel is heavier than the others, and it is loaded once.
 */

type Loaded = {
  rotations: ContentStoredRow[];
  clubs: ContentStoredRow[];
  balls: ContentStoredRow[];
  characters: ContentStoredRow[];
  shop_catalog: ContentStoredRow[];
  gacha_rates: ContentStoredRow[];
  gacha_pools: ContentStoredRow[];
  gacha_banners: ContentStoredRow[];
  /** Rotation ids whose draft differs from published (or were never published). */
  unpublished: Set<string>;
};

const CATALOGS_TO_LOAD = [
  "rotations", "clubs", "balls", "characters", "shop_catalog", "gacha_rates", "gacha_pools", "gacha_banners",
] as const;

/** Every DRAFT row of one catalog, paged at the route's cap. */
async function fetchAllDraftRows(catalog: string): Promise<ContentStoredRow[]> {
  const out: ContentStoredRow[] = [];
  for (let page = 1; ; page += 1) {
    const res = await fetchRows(catalog, { page, limit: 200 });
    out.push(...res.rows);
    if (out.length >= res.total || res.rows.length === 0) return out;
  }
}

const STATE_TONE: Record<CalendarState, string> = {
  LIVE: "border-accent-500/60 bg-accent-500/15 text-accent-300",
  SCHEDULED: "border-sky-500/50 bg-sky-500/10 text-sky-300",
  GENERATED: "border-violet-500/50 bg-violet-500/10 text-violet-300",
  NOT_GENERATED: "border-amber-500/60 bg-amber-500/10 text-amber-300",
  ENDED: "border-surface-700 bg-surface-900 text-zinc-500",
  MISSING: "border-dashed border-surface-700 bg-surface-950 text-zinc-500",
};

const PANEL_ROUTE: Record<RotationCatalog, string> = {
  gacha_rates: "/gacha-pools",
  gacha_pools: "/gacha-pools",
  gacha_banners: "/gacha-banners",
  shop_catalog: "/shop",
  rotations: "/rotations",
};

export function LineupWorkbench({ now, onWrote }: { now: number; onWrote: () => void }) {
  const t = useT();
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string>("");
  const [seedInput, setSeedInput] = useState<string>("");
  const [lineup, setLineup] = useState<Lineup | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);
  const [publishResult, setPublishResult] = useState<PublishChainResult | null>(null);
  const [confirm, setConfirm] = useState<"materialize" | "publish" | "archive" | null>(null);
  const [typed, setTyped] = useState("");

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [rows, diff] = await Promise.all([
        Promise.all(CATALOGS_TO_LOAD.map((c) => fetchAllDraftRows(c))),
        fetchDiff("rotations"),
      ]);
      const by = Object.fromEntries(CATALOGS_TO_LOAD.map((c, i) => [c, rows[i]!])) as Record<(typeof CATALOGS_TO_LOAD)[number], ContentStoredRow[]>;
      setLoaded({ ...by, unpublished: new Set(diff.entries.map((e) => e.rowId)) });
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const calendar: CalendarCell[] = useMemo(
    () => (loaded ? calendarWeeks(loaded.rotations, now, loaded.unpublished, 8) : []),
    [loaded, now]
  );

  // Default selection: the first week from today that still needs work,
  // else the first cell with a row, else the latest rotation.
  useEffect(() => {
    if (!loaded || selectedId) return;
    const cell =
      calendar.find((c) => c.state === "NOT_GENERATED" || c.state === "GENERATED") ??
      calendar.find((c) => c.rotation) ?? null;
    const id = cell?.rotation?.rowId ?? loaded.rotations[loaded.rotations.length - 1]?.rowId ?? "";
    if (id) setSelectedId(id);
  }, [loaded, calendar, selectedId]);

  const selected = useMemo(
    () => loaded?.rotations.find((r) => r.rowId === selectedId) ?? null,
    [loaded, selectedId]
  );

  // The seed input follows the selected row; MATERIALIZE persists it. Keyed on
  // the ID, not the row object: a reload after a write hands back a new object
  // for the same row, and resetting on that would wipe the notice's step list
  // the moment it appeared.
  useEffect(() => {
    const row = loaded?.rotations.find((r) => r.rowId === selectedId);
    if (row) setSeedInput(String(rotationSeed(row.data)));
    setLineup(null);
    setPublishResult(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId]);

  const runPreview = useCallback(() => {
    if (!loaded || !selected) return;
    setNotice(null);
    setPublishResult(null);
    const rotation = { ...selected, data: { ...selected.data, seed: seedInput.trim() } };
    setLineup(
      generateLineup({
        rotation,
        rotations: loaded.rotations,
        clubs: loaded.clubs,
        balls: loaded.balls,
        characters: loaded.characters,
        shop: loaded.shop_catalog,
        gachaRates: loaded.gacha_rates,
        gachaPools: loaded.gacha_pools,
        gachaBanners: loaded.gacha_banners,
        now: isoInstant(Date.now()),
      })
    );
  }, [loaded, selected, seedInput]);

  const upsert = useCallback(async (row: OutRow) => {
    await saveRow(row.catalog, { rowId: row.rowId, data: row.data, minBuild: row.minBuild, isActive: row.isActive });
  }, []);

  const runMaterialize = useCallback(async () => {
    if (!lineup || !loaded || lineup.errors.length > 0) return;
    setBusy("materialize");
    setNotice(null);
    try {
      // Re-stamp `materializedAt` at write time, not preview time.
      const stamped: Lineup = {
        ...lineup,
        rows: {
          ...lineup.rows,
          rotations: lineup.rows.rotations.map((r) => ({ ...r, data: { ...r.data, materializedAt: isoInstant(Date.now()) } })),
        },
      };
      const counts: MaterializeCounts = await materializeLineup(stamped, upsert, {
        shop_catalog: loaded.shop_catalog,
        gacha_banners: loaded.gacha_banners,
        gacha_pools: loaded.gacha_pools,
        gacha_rates: loaded.gacha_rates,
      });
      setNotice({
        ok: true,
        text: t("ro.materialize.done", {
          id: lineup.rotationId, rates: counts.gacha_rates, pools: counts.gacha_pools, banners: counts.gacha_banners,
          shop: counts.shop_catalog, deactivated: counts.deactivated,
        }),
      });
      setLineup(null);
      await load();
      onWrote();
    } catch (err) {
      setNotice({ ok: false, text: err instanceof Error ? err.message : String(err) });
    } finally {
      setBusy(null);
      setConfirm(null);
      setTyped("");
    }
  }, [lineup, loaded, upsert, load, onWrote, t]);

  const runPublish = useCallback(async () => {
    if (!selected) return;
    setBusy("publish");
    setNotice(null);
    try {
      const note = `rotation_publish ${selected.rowId} seed=${seedInput.trim()}`;
      const result = await publishRotationChain(async (catalog) => {
        try {
          const res = await publishCatalog(catalog, note);
          return { ok: true, message: res.message, version: res.version };
        } catch (err) {
          const e = err as Error & { problems?: Array<{ message: string; rowId: string | null }> };
          const detail = (e.problems ?? []).slice(0, 3).map((p) => (p.rowId ? `${p.rowId}: ${p.message}` : p.message)).join(" | ");
          return { ok: false, message: detail ? `${e.message} — ${detail}` : e.message };
        }
      });
      setPublishResult(result);
      if (result.ok) {
        setNotice({ ok: true, text: t("ro.publish.done", { versions: result.steps.map((s) => `${s.catalog} v${s.version ?? "?"}`).join(", ") }) });
      } else {
        const failed = result.steps[result.steps.length - 1]!;
        setNotice({ ok: false, text: t("ro.publish.stopped", { catalog: failed.catalog, message: failed.message }) });
      }
      await load();
      onWrote();
    } finally {
      setBusy(null);
      setConfirm(null);
      setTyped("");
    }
  }, [selected, seedInput, load, onWrote, t]);

  const ended = useMemo(() => (loaded ? archivableRotations(loaded.rotations, now) : []), [loaded, now]);
  const archiveSet = useMemo(
    () => (loaded ? archiveRows(ended, { shop_catalog: loaded.shop_catalog, gacha_banners: loaded.gacha_banners, gacha_pools: loaded.gacha_pools, gacha_rates: loaded.gacha_rates }) : []),
    [loaded, ended]
  );

  const runArchive = useCallback(async () => {
    setBusy("archive");
    setNotice(null);
    try {
      for (const row of archiveSet) await upsert(row);
      setNotice({ ok: true, text: t("ro.archive.done", { rows: archiveSet.length, n: ended.length }) });
      await load();
      onWrote();
    } catch (err) {
      setNotice({ ok: false, text: err instanceof Error ? err.message : String(err) });
    } finally {
      setBusy(null);
      setConfirm(null);
    }
  }, [archiveSet, ended, upsert, load, onWrote, t]);

  const createWeek = useCallback(
    async (cell: CalendarCell) => {
      if (!loaded) return;
      setBusy(cell.weekId);
      setNotice(null);
      try {
        const data = newRotationRow(cell.weekId, Date.parse(cell.mondayUtc), loaded.rotations);
        await saveRow("rotations", { rowId: cell.weekId, data, minBuild: 0, isActive: true, expectNew: true });
        setNotice({ ok: true, text: t("ro.created", { id: cell.weekId, start: data.startUtc!, end: data.endUtc! }) });
        await load();
        setSelectedId(cell.weekId);
        onWrote();
      } catch (err) {
        setNotice({ ok: false, text: err instanceof Error ? err.message : String(err) });
      } finally {
        setBusy(null);
      }
    },
    [loaded, load, onWrote, t]
  );

  const selectedState: CalendarState | null = selected
    ? rotationState(selected, now, loaded?.unpublished.has(selected.rowId) ?? false)
    : null;
  const alreadyMaterialized = !!(selected && (selected.data.materializedAt ?? "").trim());

  return (
    <section className="mb-5 rounded-lg border border-surface-800 bg-surface-950 p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">{t("ro.workbench")}</h2>
        <button
          type="button"
          onClick={() => void load()}
          className="rounded-md border border-surface-700 px-2 py-0.5 text-[10px] text-zinc-400 hover:border-accent-500"
        >
          {t("ro.reload")}
        </button>
      </div>
      <p className="mt-1 text-[11px] leading-relaxed text-zinc-500">{t("ro.intro")}</p>

      {loadError && (
        <p className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {t("ro.loadFailed")}: {loadError}
        </p>
      )}
      {!loaded && !loadError && <p className="mt-3 text-xs text-zinc-600">{t("ro.loading")}</p>}

      {loaded && (
        <>
          {/* ---- calendar ------------------------------------------------ */}
          <div className="mt-4">
            <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.calendar")}</p>
            <div className="mt-1.5 grid grid-cols-2 gap-1.5 sm:grid-cols-4 lg:grid-cols-8">
              {calendar.map((cell) => {
                const isSelected = !!cell.rotation && cell.rotation.rowId === selectedId;
                return (
                  <button
                    key={cell.weekId}
                    type="button"
                    disabled={busy === cell.weekId}
                    onClick={() => (cell.rotation ? setSelectedId(cell.rotation.rowId) : void createWeek(cell))}
                    className={`rounded-md border px-2 py-1.5 text-left transition ${STATE_TONE[cell.state]} ${
                      isSelected ? "ring-2 ring-accent-500" : "hover:brightness-125"
                    }`}
                  >
                    <div className="flex items-center justify-between gap-1">
                      <code className="text-[10px]">{cell.weekId}</code>
                      <span className="text-[9px] font-bold uppercase">{t(`ro.state.${cell.state}` as DictKey)}</span>
                    </div>
                    <div className="mt-0.5 truncate text-[10px] opacity-80">
                      {cell.rotation ? String(cell.rotation.data.nameEn ?? "") || cell.rotation.rowId : t("ro.create")}
                    </div>
                    <div className="text-[9px] opacity-60">{cell.mondayUtc.slice(0, 10)}</div>
                  </button>
                );
              })}
            </div>
            <p className="mt-1 text-[10px] leading-relaxed text-zinc-600">{t("ro.calendar.hint")}</p>
          </div>

          {/* ---- the selected rotation ---------------------------------- */}
          <div className="mt-4 rounded-lg border border-surface-800 bg-surface-900/60 p-3">
            {loaded.rotations.length === 0 ? (
              <p className="text-xs text-zinc-500">{t("ro.none")}</p>
            ) : (
              <>
                <div className="flex flex-wrap items-end gap-3">
                  <label className="block">
                    <span className="text-[10px] font-medium text-zinc-500">{t("ro.selected")}</span>
                    <select
                      value={selectedId}
                      onChange={(e) => setSelectedId(e.target.value)}
                      className="mt-0.5 block rounded-md border border-surface-700 bg-surface-950 px-2.5 py-1.5 font-mono text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
                    >
                      {loaded.rotations
                        .slice()
                        .sort((a, b) => (parseInstant(a.data.startUtc) ?? 0) - (parseInstant(b.data.startUtc) ?? 0))
                        .map((r) => (
                          <option key={r.rowId} value={r.rowId}>
                            {r.rowId} · {r.data.nameEn || "—"}
                          </option>
                        ))}
                    </select>
                  </label>
                  {selected && (
                    <div className="text-[11px] text-zinc-400">
                      <div>
                        <span className="text-zinc-600">{t("ro.window")}:</span>{" "}
                        <code>{selected.data.startUtc}</code> → <code>{selected.data.endUtc}</code>
                        {selectedState && (
                          <span className={`ml-2 rounded border px-1.5 py-0.5 text-[9px] font-bold uppercase ${STATE_TONE[selectedState]}`}>
                            {t(`ro.state.${selectedState}` as DictKey)}
                          </span>
                        )}
                      </div>
                      <div>
                        <span className="text-zinc-600">{t("ro.materializedAt")}:</span>{" "}
                        {alreadyMaterialized ? fmtDateTime(selected.data.materializedAt ?? null) : t("ro.notMaterialized")}
                        {loaded.unpublished.has(selected.rowId) && (
                          <span className="ml-2 text-[10px] text-violet-300">· {t("ro.unpublished")}</span>
                        )}
                      </div>
                      <div className="text-zinc-600">
                        {t("ro.eligible", {
                          clubs: loaded.clubs.filter((c) => c.isActive).length,
                          balls: loaded.balls.filter((b) => b.isActive && (b.data.isDefault ?? "").toLowerCase() !== "true").length,
                          characters: loaded.characters.filter((c) => c.isActive && (c.data.starterCandidate ?? "").trim() !== "1").length,
                        })}
                      </div>
                    </div>
                  )}
                </div>

                {selected && (
                  <div className="mt-3 flex flex-wrap items-end gap-2">
                    <label className="block">
                      <span className="text-[10px] font-medium text-zinc-500">{t("ro.seed")}</span>
                      <input
                        value={seedInput}
                        onChange={(e) => setSeedInput(e.target.value.replace(/[^0-9]/g, ""))}
                        className="mt-0.5 block w-36 rounded-md border border-surface-700 bg-surface-950 px-2 py-1.5 font-mono text-xs text-zinc-200 focus:border-accent-500 focus:outline-none"
                      />
                    </label>
                    <button
                      type="button"
                      onClick={() => setSeedInput(String(Math.floor(Math.random() * 4294967296)))}
                      className="rounded-md border border-surface-700 px-2.5 py-1.5 text-[11px] text-zinc-300 hover:border-accent-500"
                    >
                      {t("ro.seed.randomize")}
                    </button>
                    <button
                      type="button"
                      onClick={runPreview}
                      disabled={!!busy}
                      title={t("ro.preview.hint")}
                      className="rounded-md border border-accent-500/60 bg-accent-500/10 px-3 py-1.5 text-xs font-semibold text-accent-300 transition hover:bg-accent-500/20 disabled:opacity-40"
                    >
                      {t("ro.preview")}
                    </button>
                    <button
                      type="button"
                      onClick={() => (alreadyMaterialized ? setConfirm("materialize") : void runMaterialize())}
                      disabled={!lineup || lineup.errors.length > 0 || !!busy}
                      title={t("ro.materialize.hint")}
                      className="rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:opacity-40"
                    >
                      {busy === "materialize" ? t("common.loading") : t("ro.materialize")}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirm("publish")}
                      disabled={!!busy}
                      title={t("ro.publish.hint")}
                      className="rounded-md bg-emerald-700 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-emerald-600 disabled:opacity-40"
                    >
                      {busy === "publish" ? t("common.loading") : t("ro.publish")}
                    </button>
                    <button
                      type="button"
                      onClick={() => (archiveSet.length === 0 ? setNotice({ ok: true, text: t("ro.archive.none") }) : setConfirm("archive"))}
                      disabled={!!busy}
                      title={t("ro.archive.hint")}
                      className="ml-auto rounded-md border border-surface-700 px-3 py-1.5 text-xs font-medium text-zinc-300 transition hover:border-red-500 hover:text-red-300 disabled:opacity-40"
                    >
                      {busy === "archive" ? t("common.loading") : t("ro.archive")}
                      {archiveSet.length > 0 && (
                        <span className="ml-1.5 rounded bg-white/10 px-1 py-0.5 text-[10px] tabular-nums">{archiveSet.length}</span>
                      )}
                    </button>
                  </div>
                )}
                <p className="mt-1.5 text-[10px] leading-relaxed text-zinc-600">{t("ro.seed.hint")}</p>
              </>
            )}
          </div>

          {notice && (
            <p className={`mt-3 rounded-md border px-3 py-2 text-xs leading-relaxed ${notice.ok ? "border-accent-500/40 bg-accent-500/10 text-accent-300" : "border-red-500/40 bg-red-500/10 text-red-300"}`}>
              {notice.text}
            </p>
          )}

          {publishResult && (
            <ul className="mt-2 space-y-0.5 text-[11px]">
              {publishResult.steps.map((s) => (
                <li key={s.catalog} className={s.ok ? "text-zinc-400" : "text-red-300"}>
                  <code>{s.catalog}</code> — {s.ok ? `${t("ro.publish.step.ok")}${s.version ? ` v${s.version}` : ""}` : `${t("ro.publish.step.fail")}: ${s.message}`}
                </li>
              ))}
            </ul>
          )}

          {lineup && <LineupPreview lineup={lineup} mul={selected?.data.featuredWeightMul ?? "3"} />}
        </>
      )}

      {confirm === "materialize" && selected && (
        <TypedConfirm
          title={t("ro.materialize.confirm.title", { id: selected.rowId })}
          body={t("ro.materialize.confirm.body", { at: fmtDateTime(selected.data.materializedAt ?? null) })}
          word={selected.rowId}
          typed={typed}
          setTyped={setTyped}
          confirmLabel={t("ro.materialize")}
          busy={busy === "materialize"}
          onConfirm={() => void runMaterialize()}
          onCancel={() => { setConfirm(null); setTyped(""); }}
        />
      )}
      {confirm === "publish" && (
        <TypedConfirm
          title={t("ro.publish.confirm.title")}
          body={
            <>
              <p>{t("ro.publish.confirm.body")}</p>
              <p className="mt-2 text-zinc-500">{t("ro.publish.diffs")}</p>
              <ul className="mt-1 flex flex-wrap gap-2">
                {ROTATION_PUBLISH_ORDER.map((c) => (
                  <li key={c}>
                    <a href={PANEL_ROUTE[c]} target="_blank" rel="noreferrer" className="font-mono text-[11px] text-accent-300 underline">
                      {c}
                    </a>
                  </li>
                ))}
              </ul>
            </>
          }
          word="PUBLISH"
          typed={typed}
          setTyped={setTyped}
          confirmLabel={t("ro.publish")}
          busy={busy === "publish"}
          onConfirm={() => void runPublish()}
          onCancel={() => { setConfirm(null); setTyped(""); }}
        />
      )}
      {confirm === "archive" && (
        <TypedConfirm
          title={t("ro.archive.confirm.title", { n: ended.length })}
          body={t("ro.archive.confirm.body", { rows: archiveSet.length, ids: ended.map((r) => r.rowId).join(", ") })}
          word={null}
          typed={typed}
          setTyped={setTyped}
          confirmLabel={t("ro.archive")}
          busy={busy === "archive"}
          onConfirm={() => void runArchive()}
          onCancel={() => setConfirm(null)}
        />
      )}
    </section>
  );
}

// ---------------------------------------------------------------------------
// The preview
// ---------------------------------------------------------------------------

function PickCard({ pick, featured }: { pick: LineupPick; featured: boolean }) {
  const t = useT();
  return (
    <li className="rounded-md border border-surface-800 bg-surface-950 px-2 py-1.5">
      <div className="flex items-center justify-between gap-2">
        <span className="truncate text-[11px] text-zinc-200">{pick.name}</span>
        <span className="shrink-0 tabular-nums text-[11px] text-zinc-400">{pick.rpCost} <span className="text-[9px] text-zinc-600">RP</span></span>
      </div>
      <div className="mt-1 flex flex-wrap items-center gap-1">
        <RarityBadge rarity={pick.rarity} />
        {pick.type && <span className="text-[10px] text-zinc-500">{pick.type}</span>}
        {pick.brand && <span className="text-[10px] text-zinc-600">{pick.brand}</span>}
        {pick.pinned && <span className="rounded border border-accent-500/50 bg-accent-500/10 px-1 py-0.5 text-[9px] font-bold text-accent-300">{t("ro.pinned")}</span>}
        {featured && <span className="rounded border border-amber-500/50 bg-amber-500/10 px-1 py-0.5 text-[9px] font-bold text-amber-300">{t("ro.featured")}</span>}
      </div>
      <code className="mt-0.5 block truncate text-[9px] text-zinc-600">{pick.refId}</code>
    </li>
  );
}

function LineupPreview({ lineup, mul }: { lineup: Lineup; mul: string }) {
  const t = useT();
  const banner = lineup.rows.gacha_banners[0];
  const featured = new Set(lineup.featured);
  const totalRows = ROTATION_PUBLISH_ORDER.reduce((n, c) => n + lineup.rows[c].length, 0);

  if (lineup.errors.length > 0) {
    return (
      <div className="mt-3 rounded-md border border-red-500/50 bg-red-500/10 px-3 py-2">
        <p className="text-xs font-bold text-red-300">{t("ro.blocked")}</p>
        <ul className="mt-1 list-inside list-disc text-[11px] text-red-200">
          {lineup.errors.map((e) => <li key={e}>{e}</li>)}
        </ul>
      </div>
    );
  }

  return (
    <div className="mt-3 space-y-3">
      {lineup.warnings.length > 0 && (
        <div className="rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2">
          <p className="text-xs font-bold text-amber-300">{t("ro.warnings")}</p>
          <ul className="mt-1 list-inside list-disc text-[11px] text-amber-200">
            {lineup.warnings.map((w) => <li key={w.bucket + w.message}>{w.message}</li>)}
          </ul>
        </div>
      )}

      <div className="grid gap-3 lg:grid-cols-3">
        <section>
          <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.lineup.clubs")} ({lineup.clubs.length})</p>
          <ul className="mt-1 space-y-1">{lineup.clubs.map((p) => <PickCard key={p.refId} pick={p} featured={featured.has(p.refId)} />)}</ul>
        </section>
        <section>
          <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.lineup.balls")} ({lineup.balls.length})</p>
          <ul className="mt-1 space-y-1">{lineup.balls.map((p) => <PickCard key={p.refId} pick={p} featured={false} />)}</ul>
          <p className="mt-3 text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.lineup.characters")} ({lineup.characters.length})</p>
          <ul className="mt-1 space-y-1">{lineup.characters.map((p) => <PickCard key={p.refId} pick={p} featured={false} />)}</ul>
        </section>
        <section>
          <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.lineup.banner")}</p>
          {banner && (
            <div className="mt-1 rounded-md border border-surface-800 bg-surface-950 px-2 py-1.5 text-[11px] text-zinc-300">
              <div className="font-semibold text-zinc-100">{banner.data.nameEn}</div>
              <div className="text-zinc-500">{banner.data.nameJa}</div>
              <div className="mt-1 text-zinc-400">
                <code>{banner.rowId}</code> · <code>{banner.data.poolId}</code>
              </div>
              <div className="text-zinc-500">
                x1 {banner.data.costX1} · x10 {banner.data.costX10} · pity {banner.data.pityThreshold || "—"}→{banner.data.pityMinRarity || "—"} · group <code>{banner.data.pityGroup}</code>
              </div>
              <div className="text-zinc-500">{banner.data.startUtc} → {banner.data.endUtc}</div>
            </div>
          )}
          <p className="mt-3 text-[10px] font-medium uppercase tracking-wider text-zinc-500">{t("ro.lineup.odds", { mul })}</p>
          <div className="mt-1 overflow-x-auto rounded-md border border-surface-800">
            <table className="w-full text-left text-[10px]">
              <thead className="bg-surface-900 text-zinc-500">
                <tr>
                  <th className="px-2 py-1">{t("ro.odds.col.ref")}</th>
                  <th className="px-2 py-1">{t("ro.odds.col.rarity")}</th>
                  <th className="px-2 py-1 text-right">{t("ro.odds.col.weight")}</th>
                  <th className="px-2 py-1 text-right">{t("ro.odds.col.p")}</th>
                </tr>
              </thead>
              <tbody>
                {lineup.odds.map((o) => (
                  <tr key={o.entry.id} className={`border-t border-surface-800 ${o.entry.featured ? "bg-amber-500/10 text-amber-200" : "text-zinc-300"}`}>
                    <td className="px-2 py-1"><code>{o.entry.refId}</code>{o.entry.featured && <span className="ml-1 text-[9px] font-bold">★</span>}</td>
                    <td className="px-2 py-1">{o.entry.rarity}</td>
                    <td className="px-2 py-1 text-right tabular-nums">{o.entry.weight}</td>
                    <td className="px-2 py-1 text-right tabular-nums">{(o.p * 100).toFixed(3)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <p className="text-[10px] text-zinc-600">
        {t("ro.lineup.rows")}: {ROTATION_PUBLISH_ORDER.map((c) => `${c} ${lineup.rows[c].length}`).join(" · ")} ({totalRows}) ·{" "}
        {t("ro.lineup.hash")} <code>{lineup.hash}</code> · seed <code>{lineup.seed}</code>
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Typed confirmation — same shell as the Users drawer's modals
// ---------------------------------------------------------------------------

function TypedConfirm({
  title, body, word, typed, setTyped, confirmLabel, busy, onConfirm, onCancel,
}: {
  title: string;
  body: React.ReactNode;
  /** The word to type, or null for a plain confirm. */
  word: string | null;
  typed: string;
  setTyped: (v: string) => void;
  confirmLabel: string;
  busy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const t = useT();
  const matches = word === null || typed.trim() === word;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button type="button" aria-label={t("common.close")} onClick={onCancel} className="absolute inset-0 h-full w-full cursor-default bg-black/70" />
      <div role="dialog" aria-modal="true" className="relative w-full max-w-md rounded-xl border border-amber-500/50 bg-surface-900 p-5 shadow-2xl">
        <h3 className="text-sm font-semibold text-zinc-100">{title}</h3>
        <div className="mt-3 text-xs leading-relaxed text-zinc-400">{body}</div>
        {word !== null && (
          <label className="mt-4 block text-xs font-medium text-zinc-400">
            {t("ro.confirm.type", { word })}
            <input
              type="text"
              value={typed}
              onChange={(e) => setTyped(e.target.value)}
              placeholder={word}
              autoComplete="off"
              spellCheck={false}
              className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 font-mono text-xs text-zinc-200 placeholder:text-zinc-700 focus:border-amber-500 focus:outline-none"
            />
          </label>
        )}
        <div className="mt-5 flex justify-end gap-2">
          <button type="button" onClick={onCancel} disabled={busy} className="rounded-md border border-surface-700 bg-surface-850 px-3 py-1.5 text-xs font-medium text-zinc-300 transition hover:bg-surface-700 disabled:opacity-50">
            {t("common.cancel")}
          </button>
          <button type="button" onClick={onConfirm} disabled={busy || !matches} className="rounded-md bg-accent-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-500 disabled:opacity-50">
            {busy ? t("common.loading") : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
