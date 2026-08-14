import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { exportTournamentsCsv } from "@/lib/tournamentMutations";

export const dynamic = "force-dynamic";

/**
 * GET /api/tournaments/export?file=tournaments|prizes
 * Emits the two CSVs the game currently ships, regenerated from the server
 * schedule. Until Phase 3 lands this is how a dashboard edit reaches players:
 * export → drop into Assets/Resources/Data → commit → build.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const which = new URL(request.url).searchParams.get("file") ?? "tournaments";
  if (which !== "tournaments" && which !== "prizes") {
    return NextResponse.json(
      { error: "file must be 'tournaments' or 'prizes'." },
      { status: 400 }
    );
  }

  try {
    const csv = await exportTournamentsCsv();
    const body = which === "tournaments" ? csv.tournaments : csv.prizes;
    const filename = which === "tournaments" ? "tournaments.csv" : "tournament_prizes.csv";
    return new NextResponse(body, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": `attachment; filename="${filename}"`,
      },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/tournaments/export failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
