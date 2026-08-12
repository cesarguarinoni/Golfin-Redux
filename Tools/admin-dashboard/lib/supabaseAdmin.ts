import "server-only";
import { createClient, type SupabaseClient } from "@supabase/supabase-js";

/**
 * service_role Supabase client — SERVER-SIDE ONLY.
 *
 * The `server-only` import above makes any attempt to pull this module into a
 * client bundle a build-time error. The service key is read lazily from env so
 * mock mode never requires it.
 */

let cached: SupabaseClient | null = null;

export function getSupabaseAdmin(): SupabaseClient {
  if (cached) return cached;

  const url = process.env.SUPABASE_URL ?? process.env.NEXT_PUBLIC_SUPABASE_URL;
  const serviceKey = process.env.SUPABASE_SERVICE_ROLE_KEY;

  if (!url || !serviceKey) {
    throw new Error(
      "Supabase admin client unavailable: set SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY (or run in mock mode)."
    );
  }

  cached = createClient(url, serviceKey, {
    auth: { autoRefreshToken: false, persistSession: false },
  });
  return cached;
}
