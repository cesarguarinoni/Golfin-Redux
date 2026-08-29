import type { Metadata } from "next";
import { MissionComponentsPanel } from "./components-panel";

export const metadata: Metadata = { title: "Mission Components — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function MissionComponentsPage() {
  return <MissionComponentsPanel />;
}
