import type { Metadata } from "next";
import { MissionsPanel } from "./missions-panel";

export const metadata: Metadata = { title: "Missions — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function MissionsPage() {
  return <MissionsPanel />;
}
