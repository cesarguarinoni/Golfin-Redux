"use client";

import { useT } from "@/components/I18nProvider";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Modes — the `modes` catalog (game_modes_admin §2), the tenth.
 *
 * FIVE ROWS, AND TWO OF THEM DO SOMETHING NO OTHER CATALOG ROW DOES.
 *
 *   `entryFee` is a PRICE THE SERVER ENFORCES. Publishing this catalog mirrors
 *   every row's fee and lock into `golfin_mode_fees` in the same request, and
 *   POST /points/spend refuses a `mode_entry_fee:<id>` debit that does not match
 *   it. So an edit here is not "the card will say 15 next launch" — it is "15 is
 *   what a player is charged, and a client still showing 10 is turned away and
 *   re-prices". The mirror write is part of the publish and a publish that
 *   cannot write it FAILS; see `mirrorModeFees` in lib/contentMutations.ts.
 *
 *   `locked` closes and opens a mode WITHOUT A BUILD. Flipping Missions to
 *   `locked=false` is a publish, not a release — and the server refuses entry to
 *   a locked mode as well, so the two halves cannot disagree.
 *
 * The reward numbers are the opposite, and the second note says so out loud:
 * they are CARD COPY. Except for 1v1 they are averages over a selection the
 * player has not made yet (which hole, how it is played), so there is nothing
 * for them to be "correct" against. What players are actually paid lives in the
 * Rewards panel, over `game_point_actions`. The one card that claims an exact
 * amount is versus_1v1, and the publish validator warns when it drifts from
 * `versus_win.pts` — that pair only.
 *
 * The panel is the shared `CatalogPanel` with nothing added, for the same reason
 * Level Costs is: the row editor, publish drawer, kill switch and dirty count
 * are one implementation or they are four that drift. The rules specific to this
 * catalog live in the validator, where a bad publish is actually stopped.
 */
export function ModesPanel() {
  const translate = useT();

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-lg font-semibold text-zinc-100">{translate("md.title")}</h1>
        <span className="text-xs text-zinc-500">{translate("md.note")}</span>
      </div>

      <CatalogPanel
        catalog="modes"
        titleKey="md.title"
        hideTitle
        banner={
          <p className="mb-4 rounded-md border border-surface-700 bg-surface-900 px-3 py-2 text-[11px] leading-relaxed text-zinc-400">
            {translate("md.rewardsNote")}
          </p>
        }
      />
    </div>
  );
}
