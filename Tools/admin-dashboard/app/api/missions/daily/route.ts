import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchDailyCalendar, type DailyRecipe } from "@/lib/dailyMissionData";
import { pinDailyRecipe } from "@/lib/missionMutations";

export const dynamic = "force-dynamic";

/** GET /api/missions/daily — the daily calendar + per-date clear counts. Admin-only. */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const days = Number(new URL(request.url).searchParams.get("days") ?? 30);
  try {
    return NextResponse.json(await fetchDailyCalendar(Number.isFinite(days) ? days : 30));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/missions/daily failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * POST /api/missions/daily — PIN a recipe to a future UTC date.
 *
 * Validated like a mission row (the REAL `missions` validator runs over it), and
 * refused for today or any past date: players may already be mid-round on
 * today's recipe, and swapping it under them makes their claim fail the
 * recipe_hash check and pay nothing. Admin-only, audited.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as {
    date?: unknown;
    recipe?: unknown;
    recipeHash?: unknown;
  } | null;

  if (typeof body?.date !== "string" || typeof body?.recipeHash !== "string" || !body?.recipe) {
    return NextResponse.json(
      { error: "date (YYYY-MM-DD), recipe (object) and recipeHash (string) are required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await pinDailyRecipe(
      check.email,
      body.date,
      body.recipe as DailyRecipe,
      body.recipeHash
    );
    if (!outcome.ok) {
      return NextResponse.json(
        { error: outcome.message, problems: outcome.problems },
        { status: outcome.status }
      );
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/missions/daily failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
