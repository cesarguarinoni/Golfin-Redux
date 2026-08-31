"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import { fmtDateTime } from "@/lib/format";
import { ODDS_SIGNIFICANCE_SLOTS } from "@/lib/gachaAudit";
import type { GachaOddsResponse, GachaPullRow, GachaPullsResponse } from "@/lib/types";

/**
 * Gacha ops (gacha_server_pull §6).
 *
 * A LIVE panel, not a content panel, and it is shaped so nobody mistakes it for
 * one: no draft, no publish drawer, no version badge. The banners, rates, pools
 * and ticket types are edited in their own four panels; this one reads what the
 * SERVER DID with them, and carries the one switch that changes player-facing
 * behaviour instantly.
 *
 * THREE THINGS, in the order an operator needs them:
 *
 *   1. THE PAUSE SWITCH, first, because it is the reason someone opens this page
 *      at 2 a.m. It writes `content_settings.gacha_enabled`, which
 *      `golfin_gacha_pull` reads on EVERY call — so it takes effect instantly,
 *      with no cache and no next launch, which is exactly why pausing costs a
 *      typed confirmation and resuming does not.
 *   2. THE PULL LOG, because "what did this player actually get" is the question
 *      support asks. Filterable by player, banner and date; a row expands to its
 *      prizes; Export CSV writes the FILTERED set.
 *   3. THE ODDS AUDIT, because "is it honest" is the question everyone else
 *      asks. Forced slots are excluded — see `lib/gachaAudit.ts`.
 *
 * NO RED WARNING BANNER, deliberately, and the absence is meaningful. The
 * Inventory tab carries one because its blob is client-asserted; the Shop and
 * Rewards panels carry one because they change what players are charged and
 * paid. Everything on this page was written by a security-definer function
 * inside the same transaction as the ticket debit. It is server truth, and
 * decorating it with a warning would make the warnings that matter cheaper.
 */

type Sample = "100" | "1000" | "all";

interface Filters {
  email: string;
  banner: string;
  from: string;
  to: string;
}

const EMPTY: Filters = { email: "", banner: "", from: "", to: "" };

function queryOf(filters: Filters, extra: Record<string, string> = {}): string {
  const params = new URLSearchParams();
  if (filters.email.trim()) params.set("email", filters.email.trim());
  if (filters.banner) params.set("banner", filters.banner);
  // The date inputs are `YYYY-MM-DD`; widened to whole UTC days here so "to"
  // INCLUDES the day the operator typed. A bare date would compare as midnight
  // and silently drop everything that happened on it.
  if (filters.from) params.set("from", `${filters.from}T00:00:00Z`);
  if (filters.to) params.set("to", `${filters.to}T23:59:59Z`);
  for (const [k, v] of Object.entries(extra)) params.set(k, v);
  return params.toString();
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-surface-800 bg-surface-950 px-3 py-2.5">
      <div className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">{label}</div>
      <div className="mt-0.5 text-xl font-bold tabular-nums text-zinc-100">
        {value.toLocaleString()}
      </div>
    </div>
  );
}

function PrizeList({ pull }: { pull: GachaPullRow }) {
  const t = useT();
  return (
    <ul className="mt-2 space-y-1 border-l border-surface-800 pl-3">
      {pull.prizes.map((prize) => (
        <li key={prize.slot} className="flex flex-wrap items-center gap-2 text-[11px]">
          <span className="w-5 shrink-0 text-right tabular-nums text-zinc-600">{prize.slot}</span>
          <span className="whitespace-nowrap rounded bg-surface-800 px-1 py-0.5 text-[9px] uppercase text-zinc-500">
            {prize.kind}
          </span>
          <span className="whitespace-nowrap rounded border border-accent-500/40 px-1 py-0.5 text-[9px] font-semibold text-accent-300">
            {prize.rarity}
          </span>
          {/* The resolved NAME first and the raw id after it. A prize whose ref
              no longer resolves shows only the id — which is the useful answer,
              because it means a catalog row was deleted out from under a prize
              that was already paid. */}
          <span className="text-zinc-300">{prize.refName ?? ""}</span>
          <code className="text-zinc-500">{prize.refId}</code>
          {prize.quantity > 1 && (
            <span className="font-semibold tabular-nums text-zinc-300">×{prize.quantity}</span>
          )}
          {prize.isDupe && (
            <span className="whitespace-nowrap rounded bg-amber-500/15 px-1 py-0.5 text-[9px] font-bold text-amber-300">
              {t("ga.log.dupe")} +{prize.dupeRp} RP
            </span>
          )}
        </li>
      ))}
    </ul>
  );
}

export function GachaPanel() {
  const t = useT();

  const [data, setData] = useState<GachaPullsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const [draft, setDraft] = useState<Filters>(EMPTY);
  const [applied, setApplied] = useState<Filters>(EMPTY);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [older, setOlder] = useState<GachaPullRow[]>([]);

  const [pausing, setPausing] = useState<null | "pause" | "resume">(null);
  const [confirmWord, setConfirmWord] = useState("");

  const [oddsBanner, setOddsBanner] = useState("");
  const [sample, setSample] = useState<Sample>("1000");
  const [odds, setOdds] = useState<GachaOddsResponse | null>(null);

  const load = useCallback(async (filters: Filters) => {
    try {
      const res = await fetch(`/api/gacha/pulls?${queryOf(filters)}`, { cache: "no-store" });
      const body = (await res.json()) as GachaPullsResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setData(body);
      setOlder([]);
      setError(null);
      // The banner selector defaults to the first banner rather than staying
      // empty: an audit nobody selected a banner for is a panel that looks
      // broken on first open.
      setOddsBanner((current) => current || body.banners[0] || "");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, []);

  useEffect(() => {
    void load(applied);
  }, [load, applied]);

  useEffect(() => {
    if (!oddsBanner) return;
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch(`/api/gacha/odds?banner=${encodeURIComponent(oddsBanner)}&sample=${sample}`, {
          cache: "no-store",
        });
        const body = (await res.json()) as GachaOddsResponse & { error?: string };
        if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
        if (!cancelled) setOdds(body);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [oddsBanner, sample]);

  async function togglePause(enabled: boolean) {
    setBusy(true);
    try {
      const res = await fetch("/api/gacha/enabled", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ enabled }),
      });
      const body = (await res.json()) as { message?: string; error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setNotice({ ok: true, text: body.message ?? "" });
      setPausing(null);
      setConfirmWord("");
      await load(applied);
    } catch (err) {
      setNotice({ ok: false, text: err instanceof Error ? err.message : String(err) });
    } finally {
      setBusy(false);
    }
  }

  async function loadOlder() {
    const last = older.at(-1) ?? data?.pulls.at(-1);
    if (!last) return;
    setBusy(true);
    try {
      const res = await fetch(`/api/gacha/pulls?${queryOf(applied, { before: last.createdAt })}`, {
        cache: "no-store",
      });
      const body = (await res.json()) as GachaPullsResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `HTTP ${res.status}`);
      setOlder((rows) => [...rows, ...body.pulls]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  const pulls = [...(data?.pulls ?? []), ...older];
  const paused = data ? !data.gachaEnabled : false;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{t("ga.title")}</h1>
        <code className="text-xs text-zinc-600">golfin_gacha_pulls</code>
      </div>

      <p className="mb-4 text-[11px] leading-relaxed text-zinc-500">{t("ga.note")}</p>

      {data?.notMigrated && (
        <p className="mb-4 rounded-lg border border-amber-500/50 bg-amber-500/10 px-3 py-2.5 text-[11px] text-amber-200">
          {t("ga.notMigrated", { file: data.notMigrated })}
        </p>
      )}

      {error && (
        <p className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {error}
        </p>
      )}
      {notice && (
        <p
          className={`mb-4 rounded-md border px-3 py-2 text-xs ${
            notice.ok
              ? "border-accent-500/40 bg-accent-600/10 text-accent-400"
              : "border-red-500/40 bg-red-500/10 text-red-300"
          }`}
        >
          {notice.text}
        </p>
      )}

      {/* ── 1. The pause switch ─────────────────────────────────────────── */}
      <section
        className={`mb-4 rounded-lg border px-4 py-3 ${
          paused
            ? "border-red-500/50 bg-red-500/10"
            : "border-surface-800 bg-surface-950"
        }`}
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <span
              className={`whitespace-nowrap rounded px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide ${
                paused ? "bg-red-500/25 text-red-200" : "bg-accent-600/20 text-accent-300"
              }`}
            >
              {paused ? t("ga.state.paused") : t("ga.state.live")}
            </span>
            <code className="ml-2 text-[11px] text-zinc-600">content_settings.gacha_enabled</code>
          </div>
          <button
            type="button"
            disabled={busy || !data}
            onClick={() => {
              setNotice(null);
              setConfirmWord("");
              setPausing(paused ? "resume" : "pause");
            }}
            className={`rounded-md border px-3 py-1.5 text-xs font-medium transition disabled:opacity-40 ${
              paused
                ? "border-accent-500/40 bg-accent-600/15 text-accent-300 hover:bg-accent-600/25"
                : "border-red-500/40 bg-red-500/10 text-red-300 hover:bg-red-500/20"
            }`}
          >
            {paused ? t("ga.resume") : t("ga.pause")}
          </button>
        </div>
        {paused && (
          <p className="mt-2 text-[11px] leading-relaxed text-red-200/85">{t("ga.pausedBanner")}</p>
        )}
      </section>

      {/* ── Stats cards ─────────────────────────────────────────────────── */}
      {data && (
        <section className="mb-4 grid grid-cols-2 gap-2 sm:grid-cols-5">
          <Stat label={t("ga.stat.pullsToday")} value={data.stats.pullsToday} />
          <Stat label={t("ga.stat.pulls7d")} value={data.stats.pulls7d} />
          <Stat label={t("ga.stat.sunkToday")} value={data.stats.ticketsSunkToday} />
          <Stat label={t("ga.stat.sunk7d")} value={data.stats.ticketsSunk7d} />
          <Stat label={t("ga.stat.dupeRp7d")} value={data.stats.dupeRp7d} />
        </section>
      )}

      {/* ── 2. The pull log ─────────────────────────────────────────────── */}
      <section className="mb-4 rounded-lg border border-surface-800 bg-surface-950 p-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-sm font-semibold text-zinc-200">{t("ga.log.title")}</h2>
          <a
            href={`/api/gacha/export?${queryOf(applied)}`}
            title={t("ga.log.exportHint")}
            className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] font-medium text-zinc-300 transition hover:border-accent-500 hover:text-accent-300"
          >
            {t("ga.log.export")}
          </a>
        </div>

        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-5">
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("ga.log.filter.email")}</span>
            <input
              value={draft.email}
              onChange={(e) => setDraft({ ...draft, email: e.target.value })}
              placeholder={t("ga.log.filter.emailPlaceholder")}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 placeholder:text-zinc-700 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("ga.log.filter.banner")}</span>
            <select
              value={draft.banner}
              onChange={(e) => setDraft({ ...draft, banner: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="">{t("ga.log.filter.all")}</option>
              {(data?.banners ?? []).map((b) => (
                <option key={b} value={b}>
                  {b}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("ga.log.filter.from")}</span>
            <input
              type="date"
              value={draft.from}
              onChange={(e) => setDraft({ ...draft, from: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <label className="block">
            <span className="text-[10px] text-zinc-500">{t("ga.log.filter.to")}</span>
            <input
              type="date"
              value={draft.to}
              onChange={(e) => setDraft({ ...draft, to: e.target.value })}
              className="mt-0.5 w-full rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            />
          </label>
          <div className="flex items-end gap-2">
            <button
              type="button"
              onClick={() => setApplied(draft)}
              className="rounded-md bg-accent-600 px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-accent-500"
            >
              {t("ga.log.filter.apply")}
            </button>
            <button
              type="button"
              onClick={() => {
                setDraft(EMPTY);
                setApplied(EMPTY);
              }}
              className="rounded-md border border-surface-700 px-2.5 py-1 text-[11px] text-zinc-400 hover:bg-surface-800"
            >
              {t("ga.log.filter.clear")}
            </button>
          </div>
        </div>

        {pulls.length === 0 ? (
          <p className="py-6 text-center text-xs text-zinc-600">{t("ga.log.empty")}</p>
        ) : (
          <ul className="mt-3 space-y-1.5">
            {pulls.map((pull) => (
              <li
                key={pull.id}
                className="rounded-md border border-surface-800/70 bg-surface-900/60 px-2.5 py-2"
              >
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px]">
                  <span className="whitespace-nowrap text-zinc-500">{fmtDateTime(pull.createdAt)}</span>
                  <span className="truncate text-zinc-300">{pull.userEmail ?? pull.userId}</span>
                  <code className="text-zinc-400">{pull.bannerId}</code>
                  <span className="whitespace-nowrap font-semibold text-zinc-200">×{pull.pullCount}</span>
                  <span className="whitespace-nowrap tabular-nums text-zinc-400">
                    −{pull.cost} <span className="text-[9px] text-zinc-600">T{pull.ticketType}</span>
                  </span>
                  <span className="whitespace-nowrap tabular-nums text-zinc-600">
                    {t("ga.log.col.pity")} {pull.pityBefore}→{pull.pityAfter}
                  </span>
                  {pull.pityForced && (
                    <span className="whitespace-nowrap rounded bg-accent-600/20 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                      {t("ga.log.pityForced")}
                    </span>
                  )}
                  {pull.guaranteeForced && (
                    <span className="whitespace-nowrap rounded bg-accent-600/20 px-1 py-0.5 text-[9px] font-bold text-accent-300">
                      {t("ga.log.guaranteeForced")}
                    </span>
                  )}
                  <button
                    type="button"
                    onClick={() => setExpanded(expanded === pull.id ? null : pull.id)}
                    className="ml-auto whitespace-nowrap text-[10px] text-zinc-500 underline-offset-2 hover:text-accent-400 hover:underline"
                  >
                    {expanded === pull.id ? t("ga.log.collapse") : t("ga.log.expand")}
                  </button>
                </div>
                {expanded === pull.id && <PrizeList pull={pull} />}
              </li>
            ))}
          </ul>
        )}

        {data?.nextBefore && (
          <button
            type="button"
            disabled={busy}
            onClick={loadOlder}
            className="mt-3 w-full rounded-md border border-surface-700 py-1.5 text-[11px] text-zinc-400 transition hover:bg-surface-800 disabled:opacity-40"
          >
            {t("ga.log.more")}
          </button>
        )}
      </section>

      {/* ── 3. The odds audit ───────────────────────────────────────────── */}
      <section className="rounded-lg border border-surface-800 bg-surface-950 p-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-sm font-semibold text-zinc-200">{t("ga.odds.title")}</h2>
          <div className="flex items-center gap-2">
            <select
              value={oddsBanner}
              onChange={(e) => setOddsBanner(e.target.value)}
              className="rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              {(data?.banners ?? []).map((b) => (
                <option key={b} value={b}>
                  {b}
                </option>
              ))}
            </select>
            <select
              value={sample}
              onChange={(e) => setSample(e.target.value as Sample)}
              title={t("ga.odds.sample")}
              className="rounded-md border border-surface-700 bg-surface-950 px-2 py-1 text-[11px] text-zinc-200 focus:border-accent-500 focus:outline-none"
            >
              <option value="100">100</option>
              <option value="1000">1000</option>
              <option value="all">{t("ga.odds.sampleAll")}</option>
            </select>
          </div>
        </div>

        <p className="mt-1.5 text-[11px] leading-relaxed text-zinc-500">{t("ga.odds.body")}</p>

        {!odds || odds.sampledPulls === 0 ? (
          <p className="py-6 text-center text-xs text-zinc-600">{t("ga.odds.empty")}</p>
        ) : (
          <>
            <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-zinc-500">
              <span>{t("ga.odds.sampled", { pulls: odds.sampledPulls, slots: odds.comparableSlots })}</span>
              <span>
                {t("ga.odds.forced", {
                  n: odds.forcedSlots,
                  pity: odds.pityPulls,
                  guarantee: odds.guaranteePulls,
                })}
              </span>
              <code className="text-zinc-600">{odds.poolId}</code>
            </div>

            {!odds.significant && (
              <p className="mt-1.5 text-[11px] text-zinc-600">
                {t("ga.odds.notSignificant", { n: ODDS_SIGNIFICANCE_SLOTS })}
              </p>
            )}

            <table className="mt-2 w-full text-[11px]">
              <thead>
                <tr className="border-b border-surface-800 text-left text-[10px] uppercase tracking-wider text-zinc-500">
                  <th className="whitespace-nowrap py-1 font-medium">{t("ga.odds.col.rarity")}</th>
                  <th className="whitespace-nowrap py-1 text-right font-medium">{t("ga.odds.col.published")}</th>
                  <th className="whitespace-nowrap py-1 text-right font-medium">{t("ga.odds.col.observed")}</th>
                  <th className="whitespace-nowrap py-1 text-right font-medium">{t("ga.odds.col.delta")}</th>
                </tr>
              </thead>
              <tbody>
                {odds.tiers.map((tier) => (
                  <tr key={tier.rarity} className="border-b border-surface-800/50">
                    <td className="py-1 text-zinc-300">{tier.rarity}</td>
                    <td className="py-1 text-right tabular-nums text-zinc-400">
                      {tier.publishedPct.toFixed(2)}%
                    </td>
                    <td className="py-1 text-right tabular-nums text-zinc-200">
                      {tier.observedPct.toFixed(2)}%
                      <span className="ml-1.5 text-[10px] text-zinc-600">({tier.observed})</span>
                    </td>
                    <td
                      title={tier.amber ? t("ga.odds.amberHint") : undefined}
                      className={`py-1 text-right tabular-nums ${
                        tier.amber ? "font-bold text-amber-300" : "text-zinc-500"
                      }`}
                    >
                      {tier.deltaPt > 0 ? "+" : ""}
                      {tier.deltaPt.toFixed(2)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </section>

      {/* ── The pause / resume confirmation ─────────────────────────────── */}
      {pausing && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <button
            type="button"
            aria-label={t("common.close")}
            onClick={() => setPausing(null)}
            className="absolute inset-0 h-full w-full cursor-default bg-black/60"
          />
          <div className="relative w-full max-w-md rounded-lg border border-surface-700 bg-surface-900 p-5 shadow-2xl">
            <h3 className="text-sm font-semibold text-zinc-100">
              {pausing === "pause" ? t("ga.pause.title") : t("ga.resume.title")}
            </h3>
            <p className="mt-2 text-[11px] leading-relaxed text-zinc-400">
              {pausing === "pause" ? t("ga.pause.body") : t("ga.resume.body")}
            </p>

            {/* TYPED CONFIRMATION ON PAUSE ONLY. Pausing is player-facing and
                instant; resuming restores the state everyone expects. Asking for
                a typed word in both directions would make it a habit, which is
                how a typed confirmation stops being one. */}
            {pausing === "pause" && (
              <input
                value={confirmWord}
                onChange={(e) => setConfirmWord(e.target.value)}
                placeholder={t("ga.pause.confirmWord")}
                autoFocus
                className="mt-3 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 font-mono text-sm text-zinc-100 placeholder:text-zinc-700 focus:border-red-500 focus:outline-none"
              />
            )}

            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setPausing(null)}
                disabled={busy}
                className="rounded-md border border-surface-700 px-3 py-1.5 text-xs text-zinc-400 hover:bg-surface-800"
              >
                {t("common.cancel")}
              </button>
              <button
                type="button"
                disabled={
                  busy || (pausing === "pause" && confirmWord.trim() !== t("ga.pause.confirmWord"))
                }
                onClick={() => togglePause(pausing === "resume")}
                className={`rounded-md px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-40 ${
                  pausing === "pause"
                    ? "bg-red-600 hover:bg-red-500"
                    : "bg-accent-600 hover:bg-accent-500"
                }`}
              >
                {pausing === "pause" ? t("ga.pause.confirm") : t("ga.resume.confirm")}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
