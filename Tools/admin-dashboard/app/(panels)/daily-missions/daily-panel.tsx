"use client";

import { useCallback, useEffect, useState } from "react";
import { useT } from "@/components/I18nProvider";
import type { DailyCalendarResponse, DailyRecipe, DailyRow } from "@/lib/dailyMissionData";
import { fmtDateTime } from "@/lib/format";

/**
 * Daily Missions — the `daily_missions` LIVE table (missions_v1 §A6).
 *
 * NOT A CATALOG PANEL, and the difference is the whole design. `daily_missions`
 * has no draft and no publish: a row IS what players are served on that UTC
 * date, from the moment it exists. Once a date's row exists it is FROZEN — the
 * server never regenerates it — because a recipe that changed under a player
 * mid-round would fail their claim's recipe_hash check and pay them nothing.
 *
 * So the panel offers exactly two verbs, and the asymmetry between them is the
 * point:
 *
 *   PREVIEW is read-only and inserts nothing. It shows what the generator WILL
 *   produce, which is a meaningful thing to show only because the generator is
 *   deterministic in the date — same seed, same recipe, whether it runs here,
 *   on the server at midnight, or on a client that is offline.
 *
 *   PIN overrides a FUTURE date. Not today: today may already be in play. The
 *   recipe is validated by the real `missions` validator before it lands, so a
 *   hand-composed daily cannot be broken in any way a hand-composed mission
 *   could not.
 */

interface PreviewRow {
  date: string;
  recipe?: DailyRecipe;
  recipe_hash?: string;
  pinned?: boolean;
  stored?: boolean;
  error?: string;
}

const goalLine = (recipe: DailyRecipe | undefined): string => {
  const goals = recipe?.goals ?? [];
  if (goals.length === 0) return "—";
  return goals.map((g) => (g.param ? `${g.type} ${g.param}` : g.type)).join(" · ");
};

function RecipeCells({ recipe }: { recipe: DailyRecipe | undefined }) {
  if (!recipe) return <span className="text-zinc-600">—</span>;
  return (
    <span className="text-[11px] text-zinc-300">
      <span className="text-zinc-500">H</span>
      {recipe.holeId ?? "—"} · {recipe.startAreaId ?? "—"} · {recipe.windPresetId ?? "—"} ·{" "}
      {recipe.loadoutId ?? "—"}
      <span className="ml-2 text-zinc-500">{goalLine(recipe)}</span>
    </span>
  );
}

function Badge({ tone, children }: { tone: "gold" | "grey" | "amber"; children: React.ReactNode }) {
  const cls =
    tone === "gold"
      ? "border-accent-500/50 bg-accent-500/10 text-accent-300"
      : tone === "amber"
        ? "border-amber-500/50 bg-amber-500/10 text-amber-300"
        : "border-surface-700 bg-surface-900 text-zinc-400";
  return (
    <span className={`rounded border px-1.5 py-0.5 text-[10px] font-medium uppercase ${cls}`}>
      {children}
    </span>
  );
}

export function DailyMissionsPanel() {
  const t = useT();
  const [calendar, setCalendar] = useState<DailyCalendarResponse | null>(null);
  const [preview, setPreview] = useState<PreviewRow[] | null>(null);
  const [previewNote, setPreviewNote] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await fetch("/api/missions/daily");
      const body = (await res.json()) as DailyCalendarResponse & { error?: string };
      if (!res.ok) throw new Error(body.error ?? `Request failed (${res.status})`);
      setCalendar(body);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("dm.loadFailed"));
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const runPreview = useCallback(async () => {
    setBusy("preview");
    setPreviewNote(null);
    try {
      const res = await fetch("/api/missions/preview?days=14");
      const body = (await res.json()) as {
        data?: PreviewRow[];
        unavailable?: boolean;
        reason?: string;
        error?: string;
      };
      if (!res.ok) throw new Error(body.error ?? `Request failed (${res.status})`);
      if (body.unavailable) {
        setPreview([]);
        setPreviewNote(body.reason ?? null);
      } else {
        setPreview(body.data ?? []);
      }
    } catch (err) {
      setPreview(null);
      setPreviewNote(err instanceof Error ? err.message : "Preview failed.");
    } finally {
      setBusy(null);
    }
  }, []);

  const pin = useCallback(
    async (row: PreviewRow) => {
      if (!row.recipe || !row.recipe_hash) return;
      setBusy(row.date);
      setNotice(null);
      try {
        const res = await fetch("/api/missions/daily", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            date: row.date,
            recipe: row.recipe,
            recipeHash: row.recipe_hash,
          }),
        });
        const body = (await res.json()) as { message?: string; error?: string };
        if (!res.ok) throw new Error(body.error ?? t("dm.pinFailed"));
        setNotice({ ok: true, text: body.message ?? t("dm.pinOk").replace("{0}", row.date) });
        await load();
      } catch (err) {
        setNotice({ ok: false, text: err instanceof Error ? err.message : t("dm.pinFailed") });
      } finally {
        setBusy(null);
      }
    },
    [load, t]
  );

  const today = calendar?.today ?? new Date().toISOString().slice(0, 10);
  const denominator = calendar?.everClaimed ?? 0;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{t("dm.title")}</h1>
        <span className="text-xs text-zinc-500">{t("dm.note")}</span>
      </div>

      {error && (
        <p className="mb-3 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {error}
        </p>
      )}
      {/* Not an error — the window between deploying this panel and applying the
          migration. Amber and NAMED, so it reads as an instruction rather than
          as a page that is broken. */}
      {calendar?.notMigrated && (
        <p className="mb-3 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-xs leading-relaxed text-amber-200">
          {calendar.notMigrated}
        </p>
      )}
      {notice && (
        <p
          className={`mb-3 rounded-md border px-3 py-2 text-xs ${
            notice.ok
              ? "border-accent-500/40 bg-accent-500/10 text-accent-300"
              : "border-red-500/40 bg-red-500/10 text-red-300"
          }`}
        >
          {notice.text}
        </p>
      )}

      {/* ---- Preview ------------------------------------------------------ */}
      <section className="mb-6 rounded-lg border border-surface-800 bg-surface-950 p-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
            {t("dm.preview")}
          </h2>
          <button
            type="button"
            onClick={() => void runPreview()}
            disabled={busy === "preview"}
            className="rounded-md border border-surface-700 bg-surface-900 px-2.5 py-1 text-[11px] font-medium text-zinc-200 transition hover:border-accent-500 disabled:opacity-50"
          >
            {busy === "preview" ? t("common.loading") : t("dm.preview")}
          </button>
        </div>
        <p className="mt-1 text-[11px] leading-relaxed text-zinc-500">{t("dm.previewNote")}</p>
        {previewNote && <p className="mt-2 text-[11px] text-amber-400">{previewNote}</p>}
        {preview && preview.length > 0 && (
          <ul className="mt-2 space-y-1">
            {preview.map((row) => (
              <li
                key={row.date}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-surface-800 px-2 py-1.5"
              >
                <span className="flex items-center gap-2">
                  <code className="text-[11px] text-zinc-400">{row.date}</code>
                  {row.pinned && <Badge tone="gold">{t("dm.pinned")}</Badge>}
                  {row.stored && !row.pinned && <Badge tone="grey">{t("dm.generated")}</Badge>}
                  {row.recipe?.modifier && row.recipe.modifier !== "NONE" && (
                    <Badge tone="amber">{row.recipe.modifier}</Badge>
                  )}
                </span>
                <RecipeCells recipe={row.recipe} />
                {row.error ? (
                  <span className="text-[11px] text-red-300">{row.error}</span>
                ) : (
                  <button
                    type="button"
                    onClick={() => void pin(row)}
                    // Pinning is future-only — see pinDailyRecipe. A stored row
                    // is already frozen, so re-pinning it would change nothing
                    // a player can see.
                    disabled={row.date <= today || row.stored || busy === row.date}
                    title={row.date <= today ? t("dm.pastRefused") : undefined}
                    className="rounded-md border border-surface-700 px-2 py-0.5 text-[10px] font-medium text-zinc-300 transition hover:border-accent-500 disabled:opacity-40"
                  >
                    {t("dm.pin")}
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ---- The calendar -------------------------------------------------- */}
      {!calendar && !error && (
        <p className="py-6 text-center text-xs text-zinc-600">{t("common.loading")}</p>
      )}
      {calendar && calendar.rows.length === 0 && !calendar.notMigrated && (
        <p className="py-6 text-center text-xs text-zinc-600">{t("dm.notYet")}</p>
      )}
      {calendar && calendar.rows.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-surface-800">
          <table className="w-full min-w-[720px] text-left text-xs">
            <thead className="bg-surface-900 text-[10px] uppercase tracking-wider text-zinc-500">
              <tr>
                <th className="px-3 py-2">{t("dm.date")}</th>
                <th className="px-3 py-2">{t("dm.recipe")}</th>
                <th className="px-3 py-2">{t("dm.band")}</th>
                <th className="px-3 py-2 text-right">{t("dm.claims")}</th>
                <th className="px-3 py-2 text-right">{t("dm.clearRate")}</th>
                <th className="px-3 py-2">{t("dm.generated")}</th>
              </tr>
            </thead>
            <tbody>
              {calendar.rows.map((row: DailyRow) => (
                <tr key={row.date} className="border-t border-surface-800">
                  <td className="whitespace-nowrap px-3 py-2">
                    <code className="text-zinc-300">{row.date}</code>
                    {row.pinned && <span className="ml-2"><Badge tone="gold">{t("dm.pinned")}</Badge></span>}
                    {row.recipe.modifier && row.recipe.modifier !== "NONE" && (
                      <span className="ml-2"><Badge tone="amber">{row.recipe.modifier}</Badge></span>
                    )}
                  </td>
                  <td className="px-3 py-2"><RecipeCells recipe={row.recipe} /></td>
                  <td className="px-3 py-2 text-zinc-400">
                    {row.recipe.band ?? "—"}
                    {row.recipe.difficultyScore !== undefined && (
                      <span className="ml-1 text-zinc-600">({row.recipe.difficultyScore})</span>
                    )}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums text-zinc-300">{row.claims}</td>
                  <td className="px-3 py-2 text-right tabular-nums text-zinc-400">
                    {denominator > 0 ? `${Math.round((row.claims / denominator) * 100)}%` : "—"}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2 text-[10px] text-zinc-600">
                    {row.generatedAt ? fmtDateTime(row.generatedAt) : "—"}
                    {row.pinnedBy && <div className="text-zinc-700">{row.pinnedBy}</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
