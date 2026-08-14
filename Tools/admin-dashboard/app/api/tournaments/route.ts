import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchTournaments } from "@/lib/tournamentData";
import { createTournament } from "@/lib/tournamentMutations";
import type { TournamentInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/** GET /api/tournaments — list with prize bands and entry counts. Admin-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchTournaments());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/tournaments failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** POST /api/tournaments — create a golfin tournament. Admin-only, audited. */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as TournamentInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await createTournament(check.email, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/tournaments failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
