import type { Metadata } from "next";
import { ModesPanel } from "./modes-panel";

export const metadata: Metadata = { title: "Modes — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function ModesPage() {
  return <ModesPanel />;
}
