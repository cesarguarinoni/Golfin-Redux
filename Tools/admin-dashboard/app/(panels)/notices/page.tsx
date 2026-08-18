import type { Metadata } from "next";
import { NoticesPanel } from "./notices-panel";

export const metadata: Metadata = { title: "Notices — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function NoticesPage() {
  return <NoticesPanel />;
}
