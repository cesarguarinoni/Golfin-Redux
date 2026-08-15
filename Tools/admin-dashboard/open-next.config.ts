import { defineCloudflareConfig } from "@opennextjs/cloudflare";

/**
 * No incremental-cache override: every route is `dynamic = "force-dynamic"`,
 * so there is no ISR/SSG output for a cache to hold. Adding R2 here would be
 * infrastructure that never gets read.
 */
export default defineCloudflareConfig({});
