import type { Metadata } from "next";
import { TextsPanel } from "./texts-panel";

export const metadata: Metadata = { title: "Texts — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function TextsPage() {
  return <TextsPanel />;
}
