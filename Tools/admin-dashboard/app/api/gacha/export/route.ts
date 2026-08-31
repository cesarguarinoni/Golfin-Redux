import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { pullsToCsv } from "@/lib/gachaAudit";
import { fetchGachaPulls } from "@/lib/gachaData";

export const dynamic = "force-dynamic";

/**
 * GET /api/gacha/export — the FILTERED pull log as CSV (gacha_server_pull §6).
 *
 * Same query parameters as `/api/gacha/pulls`, deliberately: "Export CSV" must
 * export WHAT IS ON SCREEN, and the only way to guarantee that is for both to
 * take the same filters and run the same query. A separate unfiltered export
 * would be handed to someone as "the log" and be a different set of rows.
 *
 * ONE DIFFERENCE, and it is the point: the export is not capped at the panel's
 * page size. A 50-row page is a reading unit; an export is an evidence file.
 * `EXPORT_LIMIT` is still a limit — an unbounded read of a table that grows per
 * pull would time out on the Worker rather than produce a bigger file.
 */
const EXPORT_LIMIT = 200;

export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;

  try {
    const data = await fetchGachaPulls({
      email: params.get("email") ?? undefined,
      bannerId: params.get("banner") ?? undefined,
      from: params.get("from") ?? undefined,
      to: params.get("to") ?? undefined,
      limit: EXPORT_LIMIT,
    });

    const csv = pullsToCsv(
      data.pulls.map((p) => ({
        id: p.id,
        createdAt: p.createdAt,
        userEmail: p.userEmail,
        userId: p.userId,
        bannerId: p.bannerId,
        poolId: p.poolId,
        pullCount: p.pullCount,
        ticketType: p.ticketType,
        cost: p.cost,
        pityForced: p.pityForced,
        guaranteeForced: p.guaranteeForced,
        prizes: p.prizes.map((z) => ({
          slot: z.slot,
          kind: z.kind,
          refId: z.refId,
          quantity: z.quantity,
          rarity: z.rarity,
          isDupe: z.isDupe,
          dupeRp: z.dupeRp,
        })),
      }))
    );

    return new NextResponse(csv, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="gacha_pulls.csv"',
      },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/gacha/export failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
