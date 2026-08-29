import type { Metadata } from "next";
import { DailyMissionsPanel } from "./daily-panel";

export const metadata: Metadata = { title: "Daily Missions — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function DailyMissionsPage() {
  return <DailyMissionsPanel />;
}
