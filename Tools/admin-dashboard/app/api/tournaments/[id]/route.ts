import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { deleteTournament, updateTournament } from "@/lib/tournamentMutations";
import type { TournamentInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/** PATCH /api/tournaments/:id — edit. Live tournaments require a typed slug. */
export async function PATCH(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;

  const body = (await request.json().catch(() => null)) as TournamentInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await updateTournament(check.email, id, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PATCH /api/tournaments/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** DELETE /api/tournaments/:id — cascades entries + prize bands. Typed slug required. */
export async function DELETE(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { confirmSlug?: unknown } | null;
  if (typeof body?.confirmSlug !== "string") {
    return NextResponse.json({ error: "confirmSlug is required." }, { status: 400 });
  }

  try {
    const outcome = await deleteTournament(check.email, id, body.confirmSlug);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/tournaments/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
