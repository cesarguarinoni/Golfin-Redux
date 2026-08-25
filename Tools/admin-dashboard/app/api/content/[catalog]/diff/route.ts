import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchDiff } from "@/lib/contentData";

export const dynamic = "force-dynamic";

/**
 * GET /api/content/:catalog/diff — drafts vs published, field by field.
 *
 * This is what a publish would do, and it is the same payload that lands in
 * admin_audit_log as the publish's `before`. `deactivated` is its own category
 * rather than one changed field among twenty: it is the edit that pulls content
 * out of the shop while every player who owns one keeps it (§2 I6).
 */
export async function GET(_request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;
  try {
    return NextResponse.json(await fetchDiff(catalog));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/content/${catalog}/diff failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
