import { NextResponse } from "next/server";
import { MOCK_SESSION_COOKIE } from "@/lib/auth";
import { isMockMode } from "@/lib/mode";
import { createSupabaseServerClient } from "@/lib/supabase/server";

/** Sign out (mode-aware) then redirect to /login. Plain <form> POST works. */
export async function POST(request: Request) {
  const response = NextResponse.redirect(new URL("/login", request.url), 303);

  if (isMockMode()) {
    response.cookies.set(MOCK_SESSION_COOKIE, "", { path: "/", maxAge: 0 });
    return response;
  }

  const supabase = await createSupabaseServerClient();
  await supabase.auth.signOut();
  return response;
}
