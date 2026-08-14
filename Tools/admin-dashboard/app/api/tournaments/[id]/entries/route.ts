import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchTournamentEntries } from "@/lib/tournamentData";

export const dynamic = "force-dynamic";

/** GET /api/tournaments/:id/entries — read-only entry list (SPEC §5, Entries tab). */
export async function GET(
  _request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;
  try {
    return NextResponse.json(await fetchTournamentEntries(id));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/tournaments/${id}/entries failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
