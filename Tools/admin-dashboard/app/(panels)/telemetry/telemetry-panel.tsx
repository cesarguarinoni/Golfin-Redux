"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import type { DictKey } from "@/lib/i18n";
import type {
  FunnelStage,
  HoleStat,
  SchemeTimingStat,
  TelemetryEventsResponse,
  TelemetrySummaryResponse,
  TelemetryTestersResponse,
  TesterRow,
} from "@/lib/types";

/**
 * Telemetry panel — read-only. Six stacked sections with anchor tabs
 * (SPEC telemetry_admin_panel §3). No chart library on purpose: every bar here
 * is a <div> with a width, and a dependency would cost the Worker more than the
 * five bars are worth.
 *
 * Everything nullable is rendered as an em-dash rather than a zero. "No data"
 * and "measured zero" are different answers, and on a 20-tester beta the
 * difference is usually the whole finding.
 */

const SECTIONS = [
  { id: "kpis", key: "tel.tab.kpis" },
  { id: "funnel", key: "tel.tab.funnel" },
  { id: "holes", key: "tel.tab.holes" },
  { id: "shots", key: "tel.tab.shots" },
  { id: "gacha", key: "tel.tab.gacha" },
  { id: "testers", key: "tel.tab.testers" },
  { id: "events", key: "tel.tab.events" },
] as const satisfies readonly { id: string; key: DictKey }[];

/** control_scheme_seam §3.5 — ControlScheme int -> label key. Index IS the enum value, so
 *  this array is a wire-format mirror: append, never reorder. "Tap Timing" is the
 *  player-facing name of the internal Needle scheme. */
const SCHEME_KEYS = [
  "tel.shots.scheme.flick",
  "tel.shots.scheme.pendulum",
  "tel.shots.scheme.taptiming",
  "tel.shots.scheme.freeswing",
] as const satisfies readonly DictKey[];

// --- formatting -------------------------------------------------------------

/** Rates are 0..1 or null; null means "no denominator", never 0%. */
function pct(v: number | null, digits = 0): string {
  return v === null ? "—" : `${(v * 100).toFixed(digits)}%`;
}
function dec(v: number | null, digits = 1): string {
  return v === null ? "—" : v.toFixed(digits);
}
function int(v: number | null): string {
  return v === null ? "—" : v.toLocaleString();
}
function duration(seconds: number | null): string {
  if (seconds === null) return "—";
  const s = Math.max(0, Math.round(seconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  if (h > 0) return `${h}h ${String(m).padStart(2, "0")}m`;
  if (m > 0) return `${m}m ${String(s % 60).padStart(2, "0")}s`;
  return `${s}s`;
}
function testerLabel(row: TesterRow): string {
  return row.email ?? row.displayName ?? `${row.userId.slice(0, 8)}…`;
}

// --- shared chrome ----------------------------------------------------------

function Section({
  id,
  title,
  hint,
  right,
  children,
}: {
  id: string;
  title: string;
  hint?: string;
  right?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="scroll-mt-6">
      <div className="mb-3 flex items-baseline justify-between gap-3">
        <h2 className="text-sm font-semibold text-zinc-200" title={hint}>
          {title}
        </h2>
        {right}
      </div>
      {children}
    </section>
  );
}

function Card({
  label,
  value,
  sub,
  tone = "normal",
  hint,
  wide = false,
}: {
  label: string;
  value: string;
  sub?: string;
  tone?: "normal" | "amber" | "red" | "accent";
  hint?: string;
  wide?: boolean;
}) {
  const tones = {
    normal: "border-surface-800 bg-surface-900 text-zinc-100",
    accent: "border-accent-500/40 bg-accent-500/10 text-accent-300",
    amber: "border-amber-500/50 bg-amber-500/10 text-amber-300",
    red: "border-red-500/50 bg-red-500/10 text-red-300",
  } as const;
  return (
    <div
      className={`rounded-lg border px-4 py-3 ${tones[tone]} ${wide ? "sm:col-span-2" : ""}`}
      title={hint}
    >
      <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
        {label}
      </div>
      <div className={`mt-1 font-semibold tabular-nums ${wide ? "text-4xl" : "text-2xl"}`}>
        {value}
      </div>
      {sub && <div className="mt-0.5 text-[11px] text-zinc-500">{sub}</div>}
    </div>
  );
}

/** The only "chart" primitive in this panel. */
function Bar({ fraction, tone = "accent" }: { fraction: number; tone?: "accent" | "zinc" }) {
  const width = `${Math.max(0, Math.min(1, fraction)) * 100}%`;
  return (
    <div className="h-2 w-full overflow-hidden rounded-full bg-surface-800">
      <div
        className={`h-full rounded-full ${tone === "accent" ? "bg-accent-500" : "bg-zinc-600"}`}
        style={{ width }}
      />
    </div>
  );
}

function TruncatedBadge({ hint }: { hint: string }) {
  const t = useT();
  return (
    <span
      className="whitespace-nowrap rounded border border-amber-500/50 bg-amber-500/15 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-300"
      title={hint}
    >
      ▲ {t("tel.truncated")}
    </span>
  );
}

// --- panel ------------------------------------------------------------------

export function TelemetryPanel() {
  const t = useT();

  // Applied range (empty = server default, last 7 days) and its draft inputs.
  const [range, setRange] = useState<{ from: string; to: string }>({ from: "", to: "" });
  const [draft, setDraft] = useState<{ from: string; to: string }>({ from: "", to: "" });

  const [summary, setSummary] = useState<TelemetrySummaryResponse | null>(null);
  const [testers, setTesters] = useState<TelemetryTestersResponse | null>(null);
  const [events, setEvents] = useState<TelemetryEventsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  /** control_scheme_seam §3.5 — "all" or a ControlScheme int. Client-side only: the
   *  per-scheme split already ships inside the summary, so changing it costs no round trip. */
  const [schemeFilter, setSchemeFilter] = useState<"all" | number>("all");

  const [nameFilter, setNameFilter] = useState("");
  const [userFilter, setUserFilter] = useState("");
  const [page, setPage] = useState(0);
  const [expanded, setExpanded] = useState<string | null>(null);

  const rangeQuery = useMemo(() => {
    const params = new URLSearchParams();
    if (range.from) params.set("from", range.from);
    if (range.to) params.set("to", range.to);
    return params.toString();
  }, [range]);

  const getJson = useCallback(async <T,>(url: string): Promise<T> => {
    const res = await fetch(url);
    const body = (await res.json().catch(() => null)) as (T & { error?: string }) | null;
    if (!res.ok) throw new Error(body?.error ?? `Request failed (${res.status})`);
    if (!body) throw new Error("Empty response");
    return body;
  }, []);

  // Aggregates: one fetch per applied range.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const suffix = rangeQuery ? `?${rangeQuery}` : "";
        const [s, u] = await Promise.all([
          getJson<TelemetrySummaryResponse>(`/api/telemetry/summary${suffix}`),
          getJson<TelemetryTestersResponse>(`/api/telemetry/testers${suffix}`),
        ]);
        if (cancelled) return;
        setSummary(s);
        setTesters(u);
        setError(null);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : t("tel.loadFailed"));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [rangeQuery, getJson]);

  // Explorer: refetches on range, filters and page — it pages server-side.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const params = new URLSearchParams(rangeQuery);
        if (nameFilter) params.set("name", nameFilter);
        if (userFilter) params.set("user", userFilter);
        if (page > 0) params.set("page", String(page));
        const query = params.toString();
        const body = await getJson<TelemetryEventsResponse>(
          `/api/telemetry/events${query ? `?${query}` : ""}`
        );
        if (!cancelled) setEvents(body);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : t("tel.loadFailed"));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [rangeQuery, nameFilter, userFilter, page, getJson]);

  function applyRange() {
    setPage(0);
    setRange({ ...draft });
  }
  function resetRange() {
    setPage(0);
    setDraft({ from: "", to: "" });
    setRange({ from: "", to: "" });
  }
  function focusTester(userId: string) {
    setUserFilter((current) => (current === userId ? "" : userId));
    setPage(0);
    document.getElementById("events")?.scrollIntoView({ behavior: "smooth" });
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-300">
        {t("tel.loadFailed")}: {error}
      </div>
    );
  }
  if (!summary || !testers) {
    return (
      <div className="flex h-64 items-center justify-center text-sm text-zinc-500">
        {t("tel.loading")}
      </div>
    );
  }

  const { kpis, funnel, holes, shots, gacha } = summary;

  // The timing card's numbers: the whole-range totals under "All schemes", otherwise that
  // scheme's own slice. A scheme with no shots renders every rate as an em-dash rather than
  // 0% — "nobody played it" is not "everyone missed".
  const emptyTiming: SchemeTimingStat = {
    scheme: typeof schemeFilter === "number" ? schemeFilter : 0,
    shots: 0,
    timingSampled: 0,
    timingGreenRate: null,
    timingGoldRate: null,
    timingRedRate: null,
    avgTimingMul: null,
  };
  const timing: SchemeTimingStat =
    schemeFilter === "all"
      ? {
          scheme: -1,
          shots: shots.shotsTaken,
          timingSampled: shots.timingSampled,
          timingGreenRate: shots.timingGreenRate,
          timingGoldRate: shots.timingGoldRate,
          timingRedRate: shots.timingRedRate,
          avgTimingMul: shots.avgTimingMul,
        }
      : (shots.timingByScheme ?? []).find((r) => r.scheme === schemeFilter) ?? emptyTiming;

  const truncatedHint = t("tel.truncatedHint");
  const isEmpty = summary.rowCount === 0;

  return (
    <div className="space-y-8">
      {/* Header + range picker */}
      <div>
        <div className="mb-4 flex items-baseline justify-between gap-3">
          <h1 className="text-lg font-semibold text-zinc-100">{t("tel.title")}</h1>
          <span className="text-xs text-zinc-500">{t("tel.subtitle")}</span>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-1.5 text-xs text-zinc-500">
            {t("tel.range.from")}
            <input
              type="date"
              value={draft.from}
              onChange={(e) => setDraft((d) => ({ ...d, from: e.target.value }))}
              className="rounded-md border border-surface-700 bg-surface-900 px-2 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <label className="flex items-center gap-1.5 text-xs text-zinc-500">
            {t("tel.range.to")}
            <input
              type="date"
              value={draft.to}
              onChange={(e) => setDraft((d) => ({ ...d, to: e.target.value }))}
              className="rounded-md border border-surface-700 bg-surface-900 px-2 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <button
            type="button"
            onClick={applyRange}
            className="rounded-md border border-accent-500/50 bg-accent-600/20 px-2.5 py-1.5 text-xs font-medium text-accent-200 hover:bg-accent-500/25"
          >
            {t("tel.range.apply")}
          </button>
          <button
            type="button"
            onClick={resetRange}
            className="rounded-md border border-surface-700 px-2.5 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
          >
            {t("tel.range.reset")}
          </button>

          <span className="ml-auto flex items-center gap-2 text-xs text-zinc-500">
            <span className="font-mono text-[10px] text-zinc-600">
              {summary.range.from.slice(0, 10)} → {summary.range.to.slice(0, 10)}
            </span>
            {t("tel.rowsScanned", { n: summary.rowCount.toLocaleString() })}
            {summary.truncated && <TruncatedBadge hint={truncatedHint} />}
          </span>
        </div>

        {/* Anchor tabs */}
        <nav className="mt-4 flex flex-wrap gap-1.5">
          {SECTIONS.map((section) => (
            <a
              key={section.id}
              href={`#${section.id}`}
              className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1 text-xs font-medium text-zinc-400 transition hover:bg-surface-800 hover:text-zinc-100"
            >
              {t(section.key)}
            </a>
          ))}
        </nav>
      </div>

      {summary.tableMissing && (
        <div className="rounded-lg border border-amber-500/50 bg-amber-500/10 px-4 py-3">
          <p className="text-sm font-semibold text-amber-300">▲ {t("tel.noTable")}</p>
          <p className="mt-1 max-w-3xl text-xs leading-relaxed text-amber-200/80">
            {t("tel.noTableBody")}
          </p>
        </div>
      )}

      {isEmpty && !summary.tableMissing && (
        <div className="rounded-lg border border-surface-800 bg-surface-900 px-6 py-8 text-center">
          <p className="text-sm text-zinc-400">{t("tel.empty")}</p>
          <p className="mx-auto mt-2 max-w-lg text-xs leading-relaxed text-zinc-600">
            {t("tel.emptyBody")}
          </p>
        </div>
      )}

      {/* §3.1 KPI cards */}
      <Section id="kpis" title={t("tel.tab.kpis")}>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <Card
            label={t("tel.kpi.testers")}
            value={kpis.activeTesters.toLocaleString()}
            sub={t("tel.kpi.today", { n: kpis.activeTestersToday })}
          />
          <Card
            label={t("tel.kpi.sessions")}
            value={kpis.sessions.toLocaleString()}
            sub={t("tel.kpi.today", { n: kpis.sessionsToday })}
          />
          <Card label={t("tel.kpi.rounds")} value={kpis.roundsStarted.toLocaleString()} />
          <Card label={t("tel.kpi.holes")} value={kpis.holesCompleted.toLocaleString()} />
          <Card
            label={t("tel.kpi.abandons")}
            value={kpis.abandons.toLocaleString()}
            sub={
              kpis.abandonRate === null
                ? undefined
                : t("tel.kpi.ofRounds", { pct: pct(kpis.abandonRate) })
            }
            tone={kpis.abandonRate !== null && kpis.abandonRate > 0.2 ? "amber" : "normal"}
          />
          <Card
            label={t("tel.kpi.crashes")}
            value={kpis.crashes.toLocaleString()}
            sub={kpis.crashes === 0 ? t("tel.kpi.clean") : undefined}
            tone={kpis.crashes > 0 ? "red" : "normal"}
          />
        </div>
      </Section>

      {/* §3.2 Funnel */}
      <Section id="funnel" title={t("tel.funnel.title")} hint={t("tel.funnel.hint")}>
        <div className="space-y-2.5 rounded-lg border border-surface-800 bg-surface-900 px-4 py-4">
          {funnel.map((stage: FunnelStage) => (
            <div key={stage.id} className="grid grid-cols-[11rem_1fr_5.5rem] items-center gap-3">
              <span className="truncate text-xs text-zinc-400">
                {t(`tel.funnel.${stage.id}` as DictKey)}
              </span>
              <Bar fraction={stage.pct} />
              <span className="text-right text-xs tabular-nums text-zinc-400">
                {pct(stage.pct)}
                <span className="ml-1.5 text-zinc-600">({stage.sessions})</span>
              </span>
            </div>
          ))}
          <p className="pt-1 text-[11px] leading-relaxed text-zinc-600">
            {t("tel.funnel.hint")}
          </p>
        </div>
      </Section>

      {/* §3.3 Per-hole */}
      <Section id="holes" title={t("tel.holes.title")}>
        <div className="overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[860px] text-left text-sm">
            <thead className="bg-surface-900 text-xs text-zinc-500">
              <tr>
                {[
                  "tel.holes.col.hole",
                  "tel.holes.col.plays",
                  "tel.holes.col.completions",
                  "tel.holes.col.abandons",
                  "tel.holes.col.strokes",
                  "tel.holes.col.penalty",
                  "tel.holes.col.ob",
                  "tel.holes.col.duration",
                  "tel.holes.col.fps",
                ].map((key) => (
                  <th
                    key={key}
                    className="whitespace-nowrap px-4 py-2.5 font-medium first:text-left [&:not(:first-child)]:text-right"
                  >
                    {t(key as DictKey)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {holes.map((row: HoleStat) => {
                const obHot = row.obRate !== null && row.obRate > 0.25;
                const fpsHot = row.fpsLowMedian !== null && row.fpsLowMedian < 20;
                return (
                  <tr key={row.hole} className="border-t border-surface-800 bg-surface-950">
                    <td className="px-4 py-2.5 font-semibold text-zinc-200">{row.hole}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.plays}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.completions}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.abandons}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{dec(row.avgStrokes)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{dec(row.avgPenaltyStrokes)}</td>
                    <td
                      className={`px-4 py-2.5 text-right tabular-nums ${
                        obHot ? "bg-red-500/15 font-semibold text-red-300" : "text-zinc-300"
                      }`}
                    >
                      {pct(row.obRate)}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">
                      {duration(row.avgDurationS)}
                    </td>
                    <td
                      className={`px-4 py-2.5 text-right tabular-nums ${
                        fpsHot ? "bg-red-500/15 font-semibold text-red-300" : "text-zinc-300"
                      }`}
                    >
                      {dec(row.fpsLowMedian)}
                    </td>
                  </tr>
                );
              })}
              {holes.length === 0 && (
                <tr>
                  <td colSpan={9} className="px-4 py-10 text-center text-sm text-zinc-600">
                    {t("tel.holes.none")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Section>

      {/* §3.4 Shot quality */}
      <Section id="shots" title={t("tel.shots.title")}>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Card
            label={t("tel.shots.flickReject")}
            value={pct(shots.flickRejectRate, 1)}
            sub={`${shots.flickRejected.toLocaleString()} / ${(
              shots.flickRejected + shots.shotsTaken
            ).toLocaleString()}`}
            hint={t("tel.shots.flickRejectHint")}
            tone={
              shots.flickRejectRate !== null && shots.flickRejectRate > 0.1 ? "amber" : "accent"
            }
            wide
          />
          <Card
            label={t("tel.shots.cancel")}
            value={pct(shots.cancelRate, 1)}
            sub={shots.shotCancelled.toLocaleString()}
          />
          <Card label={t("tel.shots.ob")} value={pct(shots.obRate, 1)} sub={shots.obShots.toLocaleString()} />
          <Card label={t("tel.shots.taken")} value={shots.shotsTaken.toLocaleString()} />
        </div>

        {/* shot_timing_telemetry: how testers are hitting the coloured slab. Amber when the
            red share passes 40% — that reads "the window is too tight", not "testers are bad".
            control_scheme_seam §3.5: one filter, because four schemes averaged together is a
            number about nothing. */}
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <label className="text-xs text-zinc-500" htmlFor="tel-scheme">
            {t("tel.shots.scheme")}
          </label>
          <select
            id="tel-scheme"
            className="rounded-md border border-surface-800 bg-surface-950 px-2 py-1 text-sm text-zinc-300"
            value={schemeFilter}
            onChange={(e) => setSchemeFilter(e.target.value === "all" ? "all" : Number(e.target.value))}
          >
            <option value="all">{t("tel.shots.scheme.all")}</option>
            {SCHEME_KEYS.map((key, i) => (
              <option key={key} value={i}>
                {t(key)}
              </option>
            ))}
          </select>
          <span className="text-xs text-zinc-600">{t("tel.shots.schemeHint")}</span>
        </div>

        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Card
            label={t("tel.shots.timing")}
            value={`${pct(timing.timingGreenRate)} / ${pct(timing.timingGoldRate)} / ${pct(
              timing.timingRedRate
            )}`}
            sub={`${timing.timingSampled.toLocaleString()} ${t("tel.shots.timingSub")}`}
            hint={t("tel.shots.timingHint")}
            tone={
              timing.timingRedRate !== null && timing.timingRedRate > 0.4 ? "amber" : "accent"
            }
            wide
          />
          <Card label={t("tel.shots.timingMul")} value={dec(timing.avgTimingMul, 2)} />
        </div>

        <div className="mt-3 overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[420px] text-left text-sm">
            <thead className="bg-surface-900 text-xs text-zinc-500">
              <tr>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.shots.col.club")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.shots.col.shots")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.shots.col.distance")}</th>
              </tr>
            </thead>
            <tbody>
              {shots.clubs.map((row) => (
                <tr key={row.club} className="border-t border-surface-800 bg-surface-950">
                  <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">{row.club}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.shots}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">
                    {row.avgDistanceM === null ? "—" : `${dec(row.avgDistanceM)} m`}
                  </td>
                </tr>
              ))}
              {shots.clubs.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-4 py-10 text-center text-sm text-zinc-600">
                    {t("tel.shots.none")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Section>

      {/* gacha_ops_polish §3 — the gacha funnel. views → taps → pulls, plus the two rates
          that say WHY the drop-off happened (refusals, skips). The server's pull log has a row
          per pull and therefore cannot see anyone who looked and did not pull; this can. */}
      <Section id="gacha" title={t("tel.gacha.title")}>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Card
            label={t("tel.gacha.views")}
            value={gacha.views.toLocaleString()}
            sub={t("tel.gacha.viewsSub", { n: gacha.players.toLocaleString() })}
          />
          <Card
            label={t("tel.gacha.taps")}
            value={gacha.taps.toLocaleString()}
            sub={pct(gacha.tapRate, 1)}
            hint={t("tel.gacha.tapsHint")}
          />
          <Card
            label={t("tel.gacha.pulls")}
            value={gacha.pulls.toLocaleString()}
            sub={`${pct(gacha.pullRate, 1)} · ${gacha.pullsX1.toLocaleString()} ×1 / ${gacha.pullsX10.toLocaleString()} ×10`}
            hint={t("tel.gacha.pullsHint")}
            // Amber below 70%: more than three taps in ten ending in a refusal is a
            // configuration problem (price, cap, pause), not player indecision.
            tone={gacha.pullRate !== null && gacha.pullRate < 0.7 ? "amber" : "accent"}
            wide
          />
          <Card
            label={t("tel.gacha.latency")}
            value={gacha.meanLatencyMs === null ? "—" : `${Math.round(gacha.meanLatencyMs)} ms`}
            sub={t("tel.gacha.latencySub", { n: gacha.results.toLocaleString() })}
            hint={t("tel.gacha.latencyHint")}
          />
        </div>

        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Card
            label={t("tel.gacha.insufficient")}
            value={pct(gacha.insufficientRate, 1)}
            sub={(gacha.byStatus.insufficient ?? 0).toLocaleString()}
            hint={t("tel.gacha.insufficientHint")}
            tone={
              gacha.insufficientRate !== null && gacha.insufficientRate > 0.2 ? "amber" : "normal"
            }
          />
          <Card
            label={t("tel.gacha.skip")}
            value={pct(gacha.skipRate, 1)}
            sub={gacha.skips.toLocaleString()}
            hint={t("tel.gacha.skipHint")}
            tone={gacha.skipRate !== null && gacha.skipRate > 0.5 ? "amber" : "normal"}
          />
          <Card
            label={t("tel.gacha.rules")}
            value={pct(gacha.rulesRate, 1)}
            sub={gacha.rulesOpens.toLocaleString()}
            hint={t("tel.gacha.rulesHint")}
          />
          <Card
            label={t("tel.gacha.forced")}
            value={`${gacha.pityForced.toLocaleString()} / ${gacha.guaranteeForced.toLocaleString()}`}
            sub={t("tel.gacha.forcedSub", { n: gacha.dupes.toLocaleString() })}
            hint={t("tel.gacha.forcedHint")}
          />
        </div>

        <div className="mt-3 overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[520px] text-left text-sm">
            <thead className="bg-surface-900 text-xs text-zinc-500">
              <tr>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.gacha.col.banner")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.gacha.col.views")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.gacha.col.taps")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.gacha.col.pulls")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.gacha.col.conv")}</th>
              </tr>
            </thead>
            <tbody>
              {gacha.perBanner.map((row) => (
                <tr key={row.bannerId} className="border-t border-surface-800 bg-surface-950">
                  <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">{row.bannerId}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.views}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.taps}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.pulls}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-400">
                    {pct(row.views > 0 ? row.pulls / row.views : null, 1)}
                  </td>
                </tr>
              ))}
              {gacha.perBanner.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center text-sm text-zinc-600">
                    {t("tel.gacha.none")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Section>

      {/* §3.5 Testers */}
      <Section
        id="testers"
        title={t("tel.testers.title")}
        right={
          <span className="flex items-center gap-2 text-xs text-zinc-500">
            {t("tel.rowsScanned", { n: testers.rowCount.toLocaleString() })}
            {testers.truncated && <TruncatedBadge hint={truncatedHint} />}
          </span>
        }
      >
        <div className="overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[1040px] text-left text-sm">
            <thead className="bg-surface-900 text-xs text-zinc-500">
              <tr>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.testers.col.tester")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.testers.col.device")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.testers.col.build")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.sessions")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.playTime")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.rounds")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.holes")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.points")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 text-right font-medium">{t("tel.testers.col.crashes")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.testers.col.lastSeen")}</th>
              </tr>
            </thead>
            <tbody>
              {testers.testers.map((row) => (
                <tr
                  key={row.userId}
                  onClick={() => focusTester(row.userId)}
                  title={t("tel.testers.filter")}
                  className={`cursor-pointer border-t border-surface-800 transition hover:bg-surface-900 ${
                    userFilter === row.userId ? "bg-accent-500/10" : "bg-surface-950"
                  }`}
                >
                  <td className="px-4 py-2.5">
                    <div className="font-mono text-xs text-zinc-200">{testerLabel(row)}</div>
                    {row.displayName && row.email && (
                      <div className="text-[10px] text-zinc-600">{row.displayName}</div>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-xs text-zinc-400">
                    <div>{row.deviceModel ?? "—"}</div>
                    <div className="text-[10px] text-zinc-600">
                      {[row.platform, row.os].filter(Boolean).join(" · ") || "—"}
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 font-mono text-[11px] text-zinc-400">
                    {row.appVersion ?? "—"}
                    {row.buildNumber !== null && (
                      <span className="text-zinc-600"> ({row.buildNumber})</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">
                    {row.sessions}
                    {row.uncleanExits > 0 && (
                      <div
                        className="text-[10px] font-medium text-amber-400"
                        title={t("tel.testers.uncleanHint")}
                      >
                        {t("tel.testers.unclean", { n: row.uncleanExits })}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">
                    {duration(row.playTimeS)}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">{row.rounds}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-zinc-300">
                    {row.holesCompleted}
                  </td>
                  <td
                    className={`px-4 py-2.5 text-right tabular-nums ${
                      row.pointsDelta !== null && row.pointsDelta > 0
                        ? "text-accent-400"
                        : "text-zinc-400"
                    }`}
                  >
                    {row.pointsDelta === null
                      ? "—"
                      : `${row.pointsDelta > 0 ? "+" : ""}${int(row.pointsDelta)}`}
                  </td>
                  <td
                    className={`px-4 py-2.5 text-right tabular-nums ${
                      row.crashes > 0 ? "font-semibold text-red-400" : "text-zinc-500"
                    }`}
                  >
                    {row.crashes}
                  </td>
                  <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-500">
                    {fmtDateTime(row.lastSeen)}
                  </td>
                </tr>
              ))}
              {testers.testers.length === 0 && (
                <tr>
                  <td colSpan={10} className="px-4 py-10 text-center text-sm text-zinc-600">
                    {t("tel.testers.none")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Section>

      {/* §3.6 Event explorer */}
      <Section id="events" title={t("tel.events.title")}>
        <div className="mb-3 flex flex-wrap items-center gap-3">
          <select
            value={nameFilter}
            onChange={(e) => {
              setNameFilter(e.target.value);
              setPage(0);
            }}
            className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
          >
            <option value="">{t("tel.events.allNames")}</option>
            {summary.eventNames.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </select>
          <select
            value={userFilter}
            onChange={(e) => {
              setUserFilter(e.target.value);
              setPage(0);
            }}
            className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1.5 text-xs text-zinc-300 focus:border-accent-500 focus:outline-none"
          >
            <option value="">{t("tel.events.allTesters")}</option>
            {testers.testers.map((row) => (
              <option key={row.userId} value={row.userId}>
                {testerLabel(row)}
              </option>
            ))}
          </select>
          <span className="text-xs text-zinc-600">{t("tel.events.expand")}</span>
          {events?.total !== null && events?.total !== undefined && (
            <span className="ml-auto text-xs text-zinc-500">
              {t("tel.events.count", { n: events.total.toLocaleString() })}
            </span>
          )}
        </div>

        <div className="overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[960px] text-left text-sm">
            <thead className="bg-surface-900 text-xs text-zinc-500">
              <tr>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.received")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.ts")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.tester")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.name")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.session")}</th>
                <th className="whitespace-nowrap px-4 py-2.5 font-medium">{t("tel.events.col.payload")}</th>
              </tr>
            </thead>
            <tbody>
              {(events?.events ?? []).map((row) => {
                const open = expanded === row.eventId;
                const json = JSON.stringify(row.payload, null, open ? 2 : 0);
                return (
                  <tr
                    key={row.eventId}
                    className="border-t border-surface-800 bg-surface-950 align-top"
                  >
                    <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-400">
                      {fmtDateTime(row.receivedAt)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-xs text-zinc-600">
                      {fmtDateTime(row.ts)}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-xs text-zinc-300">{row.tester}</td>
                    <td className="px-4 py-2.5">
                      <span className="whitespace-nowrap rounded bg-surface-800 px-1.5 py-0.5 font-mono text-[10px] text-accent-400">
                        {row.name}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[10px] text-zinc-600">
                      <span title={row.sessionId}>{row.sessionId.slice(0, 8)}…</span>
                    </td>
                    <td className="max-w-[26rem] px-4 py-2.5">
                      <button
                        type="button"
                        onClick={() => setExpanded(open ? null : row.eventId)}
                        className={`w-full text-left font-mono text-[10px] text-zinc-400 hover:text-zinc-200 ${
                          open ? "whitespace-pre-wrap" : "truncate"
                        }`}
                      >
                        {json}
                      </button>
                    </td>
                  </tr>
                );
              })}
              {(events?.events.length ?? 0) === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-sm text-zinc-600">
                    {t("tel.events.none")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {(page > 0 || events?.hasMore) && (
          <div className="mt-3 flex items-center justify-end gap-2 text-xs text-zinc-400">
            <button
              type="button"
              disabled={page === 0}
              onClick={() => setPage(page - 1)}
              className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
            >
              {t("common.prev")}
            </button>
            <span>
              {t("common.page")} {page + 1}
            </span>
            <button
              type="button"
              disabled={!events?.hasMore}
              onClick={() => setPage(page + 1)}
              className="rounded-md border border-surface-700 px-2.5 py-1 disabled:opacity-40"
            >
              {t("common.next")}
            </button>
          </div>
        )}
      </Section>
    </div>
  );
}
