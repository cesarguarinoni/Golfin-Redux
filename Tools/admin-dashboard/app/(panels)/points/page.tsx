import type { Metadata } from "next";
import { PointsPanel } from "./points-panel";

export const metadata: Metadata = { title: "Points — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function PointsPage() {
  return <PointsPanel />;
}
