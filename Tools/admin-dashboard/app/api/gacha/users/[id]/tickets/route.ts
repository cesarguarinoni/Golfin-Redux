import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchPlayerGacha } from "@/lib/gachaData";
import { creditTickets } from "@/lib/gachaMutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * GET /api/gacha/users/:id — one player's gacha state (gacha_server_pull §6).
 *
 * Balances from the LEDGER (`golfin_tickets`), the last 20 ledger movements,
 * pity per banner with the banner's published threshold and cap, and the last
 * 20 pulls. Everything here is server truth — unlike the Inventory tab's blob,
 * which is client-asserted and carries a red warning for that reason.
 *
 * The route lives under `/api/gacha/users/:id` rather than
 * `/api/users/:id/gacha` so every gacha read and write sits under one prefix
 * and the panel's routes are one folder. The Users drawer calls it from its
 * Gacha tab either way.
 */
export async function GET(_request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  try {
    return NextResponse.json(await fetchPlayerGacha(id));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/gacha/users/${id}/tickets failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * POST /api/gacha/users/:id/tickets `{ ticketType, amount, adjust? }` — write
 * the TICKET LEDGER (gacha_server_pull §5.1).
 *
 * ⚠️ NOT `golfin_pending_grants`. Before this task an admin ticket grant queued
 * a grant row the client applied into its save blob, which made the device the
 * authority on how many tickets a player held. This calls
 * `golfin_ticket_credit()` — the only writer of `golfin_tickets` — so the
 * balance and the ledger move together and the player's device is told, not
 * asked.
 *
 * `adjust: true` is the Points-panel posture: a negative amount is allowed, and
 * one that would take the balance below zero is REFUSED (409), not clamped. An
 * operator who typed -500 against a balance of 3 has made a mistake, and
 * silently taking 3 hides it.
 */
export async function POST(request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as {
    ticketType?: unknown;
    amount?: unknown;
    adjust?: unknown;
  } | null;

  if (typeof body?.ticketType !== "number" || typeof body?.amount !== "number") {
    return NextResponse.json(
      { error: "ticketType (number) and amount (number) are required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await creditTickets(
      check.email,
      id,
      body.ticketType,
      body.amount,
      body.adjust === true
    );
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/gacha/users/${id}/tickets failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
