import type { Metadata } from "next";
import { VenuesPanel } from "./venues-panel";

export const metadata: Metadata = { title: "Partners — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function VenuesPage() {
  return <VenuesPanel />;
}
