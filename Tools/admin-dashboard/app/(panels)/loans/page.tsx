import type { Metadata } from "next";
import { LoansPanel } from "./loans-panel";

export const metadata: Metadata = { title: "Loans — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function LoansPage() {
  return <LoansPanel />;
}
