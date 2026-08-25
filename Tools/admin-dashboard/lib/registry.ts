/**
 * Panel registry — the sidebar builds itself from this list.
 * To add a panel: create app/(panels)/<id>/page.tsx and add an entry here.
 *
 * Order here is NOT the order on screen: the sidebar sorts by each panel's
 * TRANSLATED title (see app/(panels)/layout.tsx), so it reads alphabetically in
 * whichever language is showing rather than only in English. This array is kept
 * in English alphabetical order anyway so the two agree at a glance.
 */

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
  | "cart";

export interface PanelDef {
  id: string;
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
  { id: "notices", title: "Notices", icon: "megaphone", route: "/notices" },
  { id: "points", title: "Points", icon: "coins", route: "/points" },
  { id: "shop", title: "Shop", icon: "cart", route: "/shop" },
  { id: "telemetry", title: "Telemetry", icon: "chart", route: "/telemetry" },
  { id: "texts", title: "Texts", icon: "text", route: "/texts" },
  { id: "tournaments", title: "Tournaments", icon: "flag", route: "/tournaments" },
  { id: "users", title: "Users", icon: "users", route: "/users" },
] as const;
