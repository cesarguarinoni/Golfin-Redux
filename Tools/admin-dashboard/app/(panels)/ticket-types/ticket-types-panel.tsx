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
 * `iconUrl` IS registered for upload now (gacha_ops_polish §4). The two bundled
 * icons — `Ticket_Standard`, `Ticket_Gold` — are DERIVED placeholders, a re-tint
 * of the store ticket, so the Gold ticket renders as something other than the
 * Standard one while real art does not exist. Uploading here replaces either of
 * them on installed builds with no client release: `TicketTypeCatalog`'s ladder
 * puts a cached `iconUrl` ahead of `iconSprite`. Target size is the Standard
 * icon's own, measured off the top bar: 118 x 131 px.
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
