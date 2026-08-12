import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchUserDetail } from "@/lib/data";
import { deleteUser, updateDisplayName } from "@/lib/mutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * GET /api/users/:id — recent points_transactions + activities for one user.
 * Admin-only, read-only.
 */
export async function GET(
  _request: Request,
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

  try {
    const data = await fetchUserDetail(id);
    return NextResponse.json(data);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/users/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** PATCH /api/users/:id — edit display name. Admin-only, audited. */
export async function PATCH(
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
    displayName?: unknown;
  } | null;
  if (typeof body?.displayName !== "string") {
    return NextResponse.json(
      { error: "displayName (string) is required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await updateDisplayName(check.email, id, body.displayName);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PATCH /api/users/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * DELETE /api/users/:id — delete the auth user (FK cascade takes profiles,
 * points_transactions, activities). Requires re-typed email in the body;
 * enforced server-side, not just in the modal. Admin-only, audited.
 */
export async function DELETE(
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
    confirmEmail?: unknown;
  } | null;
  if (typeof body?.confirmEmail !== "string") {
    return NextResponse.json(
      { error: "confirmEmail (string) is required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await deleteUser(check.email, id, body.confirmEmail);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/users/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
