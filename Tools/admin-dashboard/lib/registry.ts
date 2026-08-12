/**
 * Panel registry — the sidebar builds itself from this list.
 * To add a panel: create app/(panels)/<id>/page.tsx and add an entry here.
 */

export type PanelIcon = "users" | "coins" | "flag" | "chart";

export interface PanelDef {
  id: string;
  title: string;
  icon: PanelIcon;
  route: string;
}

export const PANELS: readonly PanelDef[] = [
  { id: "users", title: "Users", icon: "users", route: "/users" },
] as const;
