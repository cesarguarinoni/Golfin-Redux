import type { Metadata } from "next";
import { GachaPanel } from "./gacha-panel";

export const metadata: Metadata = { title: "Gacha — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function GachaPage() {
  return <GachaPanel />;
}
