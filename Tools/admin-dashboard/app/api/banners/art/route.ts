import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { BANNER_PLACEMENTS, isBannerPlacement } from "@/lib/banner";
import { uploadBannerArt } from "@/lib/bannerMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/banners/art — multipart upload of banner artwork into the public
 * `game-banners` bucket (SPEC §3.3). Returns { url } for the editor to store in
 * image_url_en / image_url_ja. Admin-only, audited.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  let form: FormData;
  try {
    form = await request.formData();
  } catch {
    return NextResponse.json({ error: "Expected multipart/form-data." }, { status: 400 });
  }

  const file = form.get("file");
  const placement = form.get("placement");
  const locale = form.get("locale");

  if (!(file instanceof File)) {
    return NextResponse.json({ error: "file is required." }, { status: 400 });
  }
  if (typeof placement !== "string" || !isBannerPlacement(placement)) {
    return NextResponse.json(
      { error: `placement must be one of ${BANNER_PLACEMENTS.join(", ")}.` },
      { status: 400 }
    );
  }
  if (locale !== "en" && locale !== "ja") {
    return NextResponse.json({ error: "locale must be en or ja." }, { status: 400 });
  }

  try {
    const outcome = await uploadBannerArt(check.email, placement, locale, file);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, url: outcome.url });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/banners/art failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
