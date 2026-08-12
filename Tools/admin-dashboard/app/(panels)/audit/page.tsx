import type { Metadata } from "next";
import { AuditPanel } from "./audit-panel";

export const metadata: Metadata = { title: "Audit Log — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function AuditPage() {
  return <AuditPanel />;
}
