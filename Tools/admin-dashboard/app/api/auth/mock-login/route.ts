import { NextResponse } from "next/server";
import { MOCK_SESSION_COOKIE } from "@/lib/auth";
import { isMockMode } from "@/lib/mode";

/**
 * Mock-mode sign-in: accepts any syntactically valid email and sets an
 * httpOnly session cookie. The allowlist is enforced server-side on every
 * page/data route afterwards — a non-allowlisted email lands on /not-admin,
 * mirroring the live flow (Supabase authenticates, allowlist gates).
 * Disabled entirely in live mode.
 */
export async function POST(request: Request) {
  if (!isMockMode()) {
    return NextResponse.json(
      { error: "Mock login is disabled in live mode." },
      { status: 404 }
    );
  }

  const body = (await request.json().catch(() => null)) as {
    email?: unknown;
  } | null;
  const email =
    typeof body?.email === "string" ? body.email.trim().toLowerCase() : "";

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    return NextResponse.json(
      { error: "Enter a valid email address." },
      { status: 400 }
    );
  }

  const response = NextResponse.json({ ok: true });
  response.cookies.set(MOCK_SESSION_COOKIE, encodeURIComponent(email), {
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 12, // 12h
  });
  return response;
}
