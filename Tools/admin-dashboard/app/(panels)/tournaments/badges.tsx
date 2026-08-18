"use client";

import { useT } from "@/components/I18nProvider";
import type { DictKey } from "@/lib/i18n";
import type { ArtLayer } from "@/lib/tournament";
import type { TournamentKind, TournamentState } from "@/lib/types";

const STATE_STYLES: Record<TournamentState, string> = {
  Upcoming: "border-sky-500/40 bg-sky-500/10 text-sky-300",
  Open: "border-accent-500/40 bg-accent-500/10 text-accent-300",
  Ending: "border-amber-500/50 bg-amber-500/15 text-amber-300",
  Ended: "border-surface-700 bg-surface-850 text-zinc-500",
  Unknown: "border-red-500/40 bg-red-500/10 text-red-300",
};

export function StateBadge({ state }: { state: TournamentState }) {
  const t = useT();
  return (
    <span
      className={`whitespace-nowrap rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase ${STATE_STYLES[state]}`}
      title={t("tstate.hint")}
    >
      {t(`tstate.${state}` as DictKey)}
    </span>
  );
}

export function KindBadge({ kind }: { kind: TournamentKind }) {
  const t = useT();
  return (
    <span
      className={`whitespace-nowrap rounded px-1.5 py-0.5 text-[10px] font-bold uppercase ${
        kind === "golfin"
          ? "bg-accent-600/20 text-accent-300 ring-1 ring-accent-500/30"
          : "bg-surface-700 text-zinc-400"
      }`}
      title={t(`tkind.${kind}.hint` as DictKey)}
    >
      {t(`tkind.${kind}` as DictKey)}
    </span>
  );
}

const ART_STYLES: Record<ArtLayer, string> = {
  remote: "text-accent-400",
  bundled: "text-zinc-500",
  placeholder: "text-amber-400",
};

export function ArtBadge({ layer }: { layer: ArtLayer }) {
  const t = useT();
  return (
    <span
      className={`whitespace-nowrap text-[10px] font-medium uppercase ${ART_STYLES[layer]}`}
      title={t("tart.hint")}
    >
      {t(`tart.${layer}` as DictKey)}
    </span>
  );
}
