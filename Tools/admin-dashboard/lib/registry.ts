/**
 * Panel registry — the sidebar builds itself from this list.
 * To add a panel: create app/(panels)/<id>/page.tsx and add an entry here.
 *
 * Order here is NOT the order on screen: the sidebar sorts by each panel's
 * TRANSLATED title (see app/(panels)/layout.tsx), so it reads alphabetically in
 * whichever language is showing rather than only in English. This array is kept
 * in English alphabetical order anyway so the two agree at a glance.
 */

export type PanelIcon = "users" | "coins" | "flag" | "chart" | "shield" | "image" | "megaphone";

export interface PanelDef {
  id: string;
  title: string;
  icon: PanelIcon;
  route: string;
}

export const PANELS: readonly PanelDef[] = [
  { id: "audit", title: "Audit Log", icon: "shield", route: "/audit" },
  { id: "banners", title: "Banners", icon: "image", route: "/banners" },
  { id: "notices", title: "Notices", icon: "megaphone", route: "/notices" },
  { id: "points", title: "Points", icon: "coins", route: "/points" },
  { id: "telemetry", title: "Telemetry", icon: "chart", route: "/telemetry" },
  { id: "tournaments", title: "Tournaments", icon: "flag", route: "/tournaments" },
  { id: "users", title: "Users", icon: "users", route: "/users" },
] as const;
