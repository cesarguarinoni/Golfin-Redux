import type { Metadata } from "next";
import { TournamentsPanel } from "./tournaments-panel";

export const metadata: Metadata = { title: "Tournaments — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function TournamentsPage() {
  return <TournamentsPanel />;
}
