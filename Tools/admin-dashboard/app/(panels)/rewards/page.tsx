import type { Metadata } from "next";
import { RewardsPanel } from "./rewards-panel";

export const metadata: Metadata = { title: "Rewards — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function RewardsPage() {
  return <RewardsPanel />;
}
