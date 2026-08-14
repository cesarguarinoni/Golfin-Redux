import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { duplicateTournament } from "@/lib/tournamentMutations";

export const dynamic = "force-dynamic";

/** POST /api/tournaments/:id/duplicate — copy with a new slug and shifted dates. */
export async function POST(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { slug?: unknown } | null;
  if (typeof body?.slug !== "string") {
    return NextResponse.json({ error: "slug is required." }, { status: 400 });
  }

  try {
    const outcome = await duplicateTournament(check.email, id, body.slug);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/tournaments/${id}/duplicate failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
