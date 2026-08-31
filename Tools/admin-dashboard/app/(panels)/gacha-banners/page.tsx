import type { Metadata } from "next";
import { GachaBannersPanel } from "./gacha-banners-panel";

export const metadata: Metadata = { title: "Gacha Banners — GOLFIN Admin" };
export const dynamic = "force-dynamic";

/**
 * `now` is stamped HERE, in the server component, and handed down.
 *
 * The LIVE / SCHEDULED / ENDED badge is a statement about the SERVER clock —
 * the clock `golfin_gacha_pull()` will price and window a pull against (plan
 * §5 step 3). Deriving it from `Date.now()` in the browser would make the badge
 * a statement about the operator's laptop instead, and a laptop an hour ahead
 * would show a banner as live before any player could pull on it.
 */
export default function GachaBannersPage() {
  return <GachaBannersPanel now={Date.now()} />;
}
