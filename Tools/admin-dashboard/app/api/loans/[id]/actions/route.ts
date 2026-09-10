import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { runLoanAdminAction } from "@/lib/loanMutations";
import { LOAN_ADMIN_ACTIONS } from "@/lib/types";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * POST /api/loans/:id/actions `{ action, note }` — the admin's support actions
 * (loans_ops §3.2, §3.3).
 *
 * `action` ∈ force_return | cancel_offer | clear_cooldown; `note` is required
 * (≥ 3 chars) and lands on the timeline row and in the audit log. The write is
 * `golfin_loan_admin()` in one transaction; its refusals come back as 409 with
 * the status string (`not_active`, `not_offered`, …) so the toast names the
 * reason, and 404 for an unknown loan.
 */
export async function POST(request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id) && !id.startsWith("00000000-mock-")) {
    return NextResponse.json({ error: "Invalid loan id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as { action?: unknown; note?: unknown } | null;
  if (typeof body?.action !== "string" || !(LOAN_ADMIN_ACTIONS as readonly string[]).includes(body.action)) {
    return NextResponse.json(
      { error: `action must be one of ${LOAN_ADMIN_ACTIONS.join(", ")}.` },
      { status: 400 }
    );
  }
  if (typeof body.note !== "string") {
    return NextResponse.json({ error: "note (string) is required." }, { status: 400 });
  }

  try {
    const outcome = await runLoanAdminAction(check.email, id, body.action, body.note);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/loans/${id}/actions failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
