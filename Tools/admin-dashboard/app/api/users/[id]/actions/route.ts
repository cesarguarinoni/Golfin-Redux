import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { performUserAction } from "@/lib/mutations";
import { USER_ACTION_KINDS, type UserActionKind } from "@/lib/types";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * POST /api/users/:id/actions — one-shot auth actions:
 * resend_confirmation | send_password_reset | confirm_email | ban | unban.
 * Admin-only, audited.
 */
export async function POST(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as {
    action?: unknown;
  } | null;
  const action = body?.action;
  if (
    typeof action !== "string" ||
    !(USER_ACTION_KINDS as readonly string[]).includes(action)
  ) {
    return NextResponse.json(
      { error: `action must be one of: ${USER_ACTION_KINDS.join(", ")}` },
      { status: 400 }
    );
  }

  try {
    const outcome = await performUserAction(
      check.email,
      id,
      action as UserActionKind
    );
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/users/${id}/actions failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
