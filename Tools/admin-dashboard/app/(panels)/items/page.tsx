import type { Metadata } from "next";
import { ItemsPanel } from "./items-panel";

export const metadata: Metadata = { title: "Items — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function ItemsPage() {
  return <ItemsPanel />;
}
