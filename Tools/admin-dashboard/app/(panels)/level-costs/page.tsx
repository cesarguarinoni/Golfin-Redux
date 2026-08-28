import type { Metadata } from "next";
import { LevelCostsPanel } from "./level-costs-panel";

export const metadata: Metadata = { title: "Level Costs — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function LevelCostsPage() {
  return <LevelCostsPanel />;
}
