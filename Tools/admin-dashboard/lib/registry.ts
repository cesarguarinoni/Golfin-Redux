/**
 * Panel registry — the sidebar builds itself from this list.
 * To add a panel: create app/(panels)/<id>/page.tsx and add an entry here.
 *
 * Order here is NOT the order on screen: the sidebar sorts by each panel's
 * TRANSLATED title (see app/(panels)/layout.tsx), so it reads alphabetically in
 * whichever language is showing rather than only in English. This array is kept
 * in English alphabetical order anyway so the two agree at a glance.
 */

import type { DictKey } from "./i18n";

/**
 * Panel ids that actually have a `nav.<id>` label in the dictionary.
 *
 * WHY THIS TYPE EXISTS. `app/(panels)/layout.tsx` renders each entry as
 * `t(\`nav.${panel.id}\` as DictKey)`, and `translate` falls back to returning the
 * KEY when it does not know one — so a panel whose label was never added renders
 * a literal `nav.level-costs` in the sidebar, in production, with a clean
 * typecheck. That is exactly what shipped on 2026-08-28 and had to be caught by
 * eye in a screenshot. Deriving the id set FROM the dictionary makes the
 * omission a compile error instead.
 */
type PanelId = Extract<DictKey, `nav.${string}`> extends `nav.${infer Id}` ? Id : never;

export type PanelIcon =
  | "users"
  | "coins"
  | "flag"
  | "chart"
  | "shield"
  | "image"
  | "megaphone"
  | "club"
  | "character"
  | "box"
  | "text"
  | "cart"
  | "ladder"
  | "flagpole"
  | "gift";

export interface PanelDef {
  id: PanelId;
  title: string;
  icon: PanelIcon;
  route: string;
}

export const PANELS: readonly PanelDef[] = [
  { id: "audit", title: "Audit Log", icon: "shield", route: "/audit" },
  { id: "banners", title: "Banners", icon: "image", route: "/banners" },
  // Admin-managed game content (content_admin_panels). `items` covers the
  // items / bags / balls catalogs behind three tabs: 15 rows between them does
  // not justify three sidebar entries.
  { id: "characters", title: "Characters", icon: "character", route: "/characters" },
  { id: "clubs", title: "Clubs", icon: "club", route: "/clubs" },
  { id: "items", title: "Items", icon: "box", route: "/items" },
  // The level-up cost table. Its own entry rather than a tab inside Characters
  // or Clubs because it belongs to NEITHER — both price from the same 240 rows,
  // and hanging it off one of them would imply the other has its own.
  { id: "level-costs", title: "Level Costs", icon: "ladder", route: "/level-costs" },
  // The `modes` catalog — entry fees, card copy and the Coming Soon flag for the
  // five game modes. It is the SECOND catalog the server reads (level_up_costs
  // was the first): publishing here mirrors the fees into `golfin_mode_fees`,
  // which /points/spend prices a mode entry against.
  { id: "modes", title: "Modes", icon: "flagpole", route: "/modes" },
  { id: "notices", title: "Notices", icon: "megaphone", route: "/notices" },
  { id: "points", title: "Points", icon: "coins", route: "/points" },
  // NOT a content catalog and deliberately not shaped like one: this edits
  // `game_point_actions` — the live server table the earn path reads per
  // request. No draft, no publish, no version. The panel says so.
  { id: "rewards", title: "Rewards", icon: "gift", route: "/rewards" },
  { id: "shop", title: "Shop", icon: "cart", route: "/shop" },
  { id: "telemetry", title: "Telemetry", icon: "chart", route: "/telemetry" },
  { id: "texts", title: "Texts", icon: "text", route: "/texts" },
  { id: "tournaments", title: "Tournaments", icon: "flag", route: "/tournaments" },
  { id: "users", title: "Users", icon: "users", route: "/users" },
] as const;
