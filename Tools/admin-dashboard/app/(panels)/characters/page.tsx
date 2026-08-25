import type { Metadata } from "next";
import { CharactersPanel } from "./characters-panel";

export const metadata: Metadata = { title: "Characters — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function CharactersPage() {
  return <CharactersPanel />;
}
