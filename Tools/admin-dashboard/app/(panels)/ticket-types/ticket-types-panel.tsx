"use client";

import { useT } from "@/components/I18nProvider";
import { CatalogPanel } from "../_content/catalog-panel";

/**
 * Ticket Types — the `ticket_types` catalog (gacha_admin_catalogs §5.4).
 *
 * The plainest of the three gacha panels, and deliberately so: two rows, six
 * columns, and the only thing that needs saying is the one thing that cannot be
 * undone. `id` is the `ticketTypeInt` written into every player's save
 * (`TicketType.Standard = 0`), so renumbering a row does not rename a ticket —
 * it silently converts everyone's balance into a different kind. Append only.
 *
 * Icon columns are NOT registered for upload in this task: the bundled ticket
 * icon is authored in the card prefab today, and naming its replacement is
 * `gacha_client_real_pull`'s call, not this one. `iconSprite` / `iconUrl` are
 * present as plain text fields so the columns exist to be filled later.
 */
export function TicketTypesPanel() {
  const translate = useT();
  return (
    <CatalogPanel
      catalog="ticket_types"
      titleKey="tt.title"
      banner={
        <p className="mb-4 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-[11px] leading-relaxed text-amber-200/90">
          {translate("tt.note")}
        </p>
      }
    />
  );
}
