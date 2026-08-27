import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { uploadCatalogArt } from "@/lib/contentArtMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/art — multipart upload of catalog row artwork into the
 * public `catalog-art` bucket (SPEC content_art_urls §3).
 *
 * Returns { url } for the row editor to write into the URL column
 * (portraitUrl / fullUrl / thumbnailUrl / controlUrl). Admin-only, audited.
 *
 * Mirrors the shape of POST /api/banners/art.
 *
 * FormData fields:
 *   file      — the image File (JPG, PNG, or WebP; max 500 KB)
 *   catalog   — "characters" | "clubs" | "items" | "balls"
 *   rowId     — the row's id column value, e.g. "char_james"
 *   column    — "portraitUrl" | "fullUrl" | "thumbnailUrl" | "controlUrl"
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
  const catalog = form.get("catalog");
  const rowId = form.get("rowId");
  const column = form.get("column");

  if (!(file instanceof File)) {
    return NextResponse.json({ error: "file is required." }, { status: 400 });
  }
  if (typeof catalog !== "string" || !catalog) {
    return NextResponse.json(
      { error: "catalog is required (characters | clubs | items | balls)." },
      { status: 400 }
    );
  }
  if (typeof rowId !== "string" || !rowId) {
    return NextResponse.json({ error: "rowId is required." }, { status: 400 });
  }
  if (typeof column !== "string" || !column) {
    return NextResponse.json(
      { error: "column is required (portraitUrl | fullUrl | thumbnailUrl | controlUrl)." },
      { status: 400 }
    );
  }

  try {
    // uploadCatalogArt validates catalog and column; let it produce the 400 messages.
    const outcome = await uploadCatalogArt(
      check.email,
      catalog as never,   // cast — runtime validation happens inside
      rowId,
      column as never,    // cast — runtime validation happens inside
      file
    );
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, url: outcome.url });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/content/art failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
