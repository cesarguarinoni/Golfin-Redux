/**
 * The git commit this bundle was built from.
 *
 * `NEXT_PUBLIC_` because it must reach BOTH the server route (`/api/version`) and
 * the client footer, and Next inlines these at compile time — which is exactly
 * why a Worker secret cannot supply it (ADMIN_DASHBOARD_OPS §4.4). It is public
 * by design, like the anon key: a commit hash is not a credential.
 *
 * `cf-deploy.sh` sets it from `git rev-parse --short HEAD` at build time. Any
 * other build — `npm run dev`, a bare `next build` — leaves it "unstamped", which
 * is the honest answer rather than a plausible wrong one.
 */
export const BUILD_COMMIT: string =
  process.env.NEXT_PUBLIC_BUILD_COMMIT || "unstamped";
