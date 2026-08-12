import "server-only";
import { cookies } from "next/headers";
import { isAdminEmail } from "./allowlist";
import { isMockMode } from "./mode";
import { createSupabaseServerClient } from "./supabase/server";

/** Cookie carrying the mock-mode "session" (httpOnly, set by /api/auth/mock-login). */
export const MOCK_SESSION_COOKIE = "golfin_admin_mock_session";

/** Email of the currently signed-in user, or null. Mode-aware. */
export async function getSessionEmail(): Promise<string | null> {
  if (isMockMode()) {
    const store = await cookies();
    const raw = store.get(MOCK_SESSION_COOKIE)?.value;
    if (!raw) return null;
    try {
      return decodeURIComponent(raw).toLowerCase();
    } catch {
      return null;
    }
  }

  const supabase = await createSupabaseServerClient();
  const {
    data: { user },
  } = await supabase.auth.getUser();
  return user?.email?.toLowerCase() ?? null;
}

export type AdminCheck =
  | { ok: true; email: string }
  | { ok: false; status: 401 | 403; message: string };

/**
 * Server-side admin gate used by EVERY data route handler.
 * 401 = not signed in, 403 = signed in but not on ADMIN_EMAILS.
 */
export async function checkAdmin(): Promise<AdminCheck> {
  const email = await getSessionEmail();
  if (!email) {
    return { ok: false, status: 401, message: "Not signed in." };
  }
  if (!isAdminEmail(email)) {
    return {
      ok: false,
      status: 403,
      message: `${email} is not on the admin allowlist.`,
    };
  }
  return { ok: true, email };
}
