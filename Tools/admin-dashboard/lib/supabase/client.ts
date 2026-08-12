"use client";

import { createBrowserClient } from "@supabase/ssr";

/** Browser (anon key) Supabase client — used by the login form in live mode. */
export function createSupabaseBrowserClient() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const anonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if (!url || !anonKey) {
    throw new Error(
      "NEXT_PUBLIC_SUPABASE_URL / NEXT_PUBLIC_SUPABASE_ANON_KEY are required in live mode."
    );
  }
  return createBrowserClient(url, anonKey);
}
