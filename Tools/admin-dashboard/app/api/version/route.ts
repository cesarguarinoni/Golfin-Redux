import { NextResponse } from "next/server";
import { BUILD_COMMIT } from "@/lib/buildInfo";

/**
 * "Is what I am looking at the code I pushed?" — as a curl, never a memory.
 *
 * PIPELINE_HARDENING §23 companion. Twice now, dashboard work has sat local-only
 * while its task closed as DONE: the catalog-art upload UI, the WebP-only fix,
 * the URL-only badge — and, found while auditing for this, `1f3450c53` as well.
 * Every one of those was "committed and pushed", which is a different claim from
 * "deployed", and nothing in the loop could tell them apart.
 *
 * DELIBERATELY NOT BEHIND `checkAdmin()`. Every other route starts with it
 * (ADMIN_DASHBOARD_OPS §3.1) and this one does not, on purpose: the whole value
 * is being able to answer "what is live?" from a shell before you have a session.
 * It is safe to leave open because Cloudflare Access already fronts the entire
 * origin — an unauthenticated request never reaches this handler, it gets a 302
 * to cloudflareaccess.com — and because a commit hash of a private repo is not a
 * secret. It exposes nothing an admin could not read in the footer.
 */
export async function GET() {
  return NextResponse.json({
    commit: BUILD_COMMIT,
    // A build that did not go through cf-deploy.sh says so, rather than
    // reporting a plausible-looking wrong answer.
    stamped: BUILD_COMMIT !== "unstamped",
  });
}
