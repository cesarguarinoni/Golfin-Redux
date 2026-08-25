import type { Metadata } from "next";
import { ShopPanel } from "./shop-panel";

export const metadata: Metadata = { title: "Shop — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function ShopPage() {
  return <ShopPanel />;
}
