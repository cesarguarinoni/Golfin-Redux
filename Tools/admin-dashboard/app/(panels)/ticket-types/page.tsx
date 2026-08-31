import type { Metadata } from "next";
import { TicketTypesPanel } from "./ticket-types-panel";

export const metadata: Metadata = { title: "Ticket Types — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function TicketTypesPage() {
  return <TicketTypesPanel />;
}
