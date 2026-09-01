import type { Metadata } from "next";
import { BallsPanel } from "./balls-panel";

export const metadata: Metadata = { title: "Balls — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function BallsPage() {
  return <BallsPanel />;
}
