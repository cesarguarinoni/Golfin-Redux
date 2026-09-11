import type { Metadata } from "next";
import { RotationsPanel } from "./rotations-panel";

export const metadata: Metadata = { title: "Rotations — GOLFIN Admin" };
export const dynamic = "force-dynamic";

/**
 * `now` is stamped HERE, in the server component, and handed down — the same
 * reason the Gacha Banners page does it: the calendar's LIVE / SCHEDULED /
 * ENDED cells and the ARCHIVE cut-off are statements about the SERVER clock,
 * the one `golfin_shop_purchase()` and `golfin_gacha_pull()` window against.
 */
export default function RotationsPage() {
  return <RotationsPanel now={Date.now()} />;
}
