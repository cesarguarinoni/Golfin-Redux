import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { deleteBanner, setBannerActive, updateBanner } from "@/lib/bannerMutations";
import type { BannerInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * PATCH /api/banners/:id — edit, or flip the active switch.
 *
 * Two shapes on one route because they are the same row and the same audit
 * target: `{ setActive: boolean }` is the one-click switch on the list row,
 * anything else is a full editor save. Switching a LIVE banner OFF needs
 * `confirmLabel` either way.
 */
export async function PATCH(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;

  const body = (await request.json().catch(() => null)) as
    | (BannerInput & { setActive?: unknown })
    | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome =
      typeof body.setActive === "boolean"
        ? await setBannerActive(check.email, id, body.setActive, body.confirmLabel)
        : await updateBanner(check.email, id, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PATCH /api/banners/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** DELETE /api/banners/:id — typed label required. Admin-only, audited. */
export async function DELETE(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { confirmLabel?: unknown } | null;
  if (typeof body?.confirmLabel !== "string") {
    return NextResponse.json({ error: "confirmLabel is required." }, { status: 400 });
  }

  try {
    const outcome = await deleteBanner(check.email, id, body.confirmLabel);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/banners/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
