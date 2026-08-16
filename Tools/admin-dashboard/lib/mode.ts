/**
 * Mock-mode switch — and the guard that keeps mock mode off the public internet.
 *
 * Mock mode runs the whole app on local fixtures. Critically, its login route
 * (`/api/auth/mock-login`) accepts ANY email/password: the allowlist still
 * applies, but the password does not. That is fine on localhost and a hole on a
 * public URL.
 *
 * ⚠️ THE RULE, and it is deliberately blunt: mock mode must be ASKED FOR.
 * A missing `SUPABASE_SERVICE_ROLE_KEY` is never interpreted as "the operator
 * wanted fixtures" — it throws, everywhere, in every environment.
 *
 * The first version of this guard only threw when `NODE_ENV === "production"`,
 * which was wrong twice over: on Cloudflare Workers `NODE_ENV` is not
 * necessarily set, and inferring intent from an environment name is exactly the
 * kind of cleverness that fails silently. Absence of a credential is an error,
 * not a configuration choice.
 *
 * Server-side only in practice (reads non-NEXT_PUBLIC env); client components
 * receive the flag as a prop / API field instead of calling this.
 */

export class MissingServiceKeyError extends Error {
  constructor() {
    super(
      "Refusing to serve: SUPABASE_SERVICE_ROLE_KEY is not set. This app will " +
        "NOT silently fall back to mock mode, because mock mode's login accepts " +
        "any password. On Cloudflare check `wrangler secret list`; locally check " +
        ".env.development.local. If you actually want fixtures, set MOCK_MODE=1 " +
        "explicitly."
    );
    this.name = "MissingServiceKeyError";
  }
}

export function isMockMode(): boolean {
  // The only way into mock mode is to say so.
  if (process.env.MOCK_MODE === "1") return true;

  if (!process.env.SUPABASE_SERVICE_ROLE_KEY) {
    // During `next build` there are deliberately no secrets in the environment —
    // that is the whole point, it keeps the key out of the bundle (see the
    // .env.development.local note in the README). Next still prerenders
    // /_not-found, which pulls this module in, so throwing here would fail every
    // build. Runtime is what this guard is for.
    if (process.env.NEXT_PHASE === "phase-production-build") return false;
    throw new MissingServiceKeyError();
  }

  return false;
}
