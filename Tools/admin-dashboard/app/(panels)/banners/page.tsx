import type { Metadata } from "next";
import { BannersPanel } from "./banners-panel";

export const metadata: Metadata = { title: "Banners — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function BannersPage() {
  return <BannersPanel />;
}
