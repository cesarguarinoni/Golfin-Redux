import type { Metadata } from "next";
import { UsersPanel } from "./users-panel";

export const metadata: Metadata = { title: "Users — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function UsersPage() {
  return <UsersPanel />;
}
