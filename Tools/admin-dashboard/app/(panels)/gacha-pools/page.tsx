import type { Metadata } from "next";
import { GachaPoolsPanel } from "./gacha-pools-panel";

export const metadata: Metadata = { title: "Gacha Pools — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function GachaPoolsPage() {
  return <GachaPoolsPanel />;
}
