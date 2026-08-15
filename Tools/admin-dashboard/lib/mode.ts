/**
 * Mock-mode switch — and the guard that keeps mock mode off the public internet.
 *
 * Mock mode runs the whole app on local fixtures. Critically, its login route
 * (`/api/auth/mock-login`) accepts ANY email/password: the allowlist still
 * applies, but the password does not. That is fine on localhost and a hole on a
 * public URL.
 *
 * The dangerous shape is not someone choosing mock mode — it is mock mode being
 * entered BY ACCIDENT, because `SUPABASE_SERVICE_ROLE_KEY` was missing, renamed
 * or never set as a Worker secret. That failure is silent: the app boots, looks
 * completely normal, and lets anyone on the allowlist domain in with a made-up
 * password.
 *
 * So in production, mock mode must be asked for out loud. If it is reached
 * without `ALLOW_MOCK_MODE=1`, we throw rather than serve. A 500 on every page
 * is a bad afternoon; a fake login on admin.golfin.world is a bad quarter.
 *
 * Server-side only in practice (reads non-NEXT_PUBLIC env); client components
 * receive the flag as a prop / API field instead of calling this.
 */

export class MockModeInProductionError extends Error {
  constructor() {
    super(
      "Refusing to start: the app fell back to MOCK MODE in a production build, " +
        "where login accepts any password. This almost always means " +
        "SUPABASE_SERVICE_ROLE_KEY is missing from the environment — on Cloudflare, " +
        "check `wrangler secret list`. If mock mode is genuinely wanted here, set " +
        "ALLOW_MOCK_MODE=1 explicitly."
    );
    this.name = "MockModeInProductionError";
  }
}

export function isMockMode(): boolean {
  const explicit = process.env.MOCK_MODE === "1";
  const missingKey = !process.env.SUPABASE_SERVICE_ROLE_KEY;
  const mock = explicit || missingKey;

  if (!mock) return false;

  const isProduction = process.env.NODE_ENV === "production";
  const permitted = process.env.ALLOW_MOCK_MODE === "1";

  if (isProduction && !permitted) {
    throw new MockModeInProductionError();
  }

  return true;
}
