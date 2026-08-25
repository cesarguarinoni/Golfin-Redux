import type { Metadata } from "next";
import { ClubsPanel } from "./clubs-panel";

export const metadata: Metadata = { title: "Clubs — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function ClubsPage() {
  return <ClubsPanel />;
}
