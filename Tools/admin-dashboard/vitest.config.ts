import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

/**
 * Vitest over the PURE modules only (game_modes_admin, red-team iter-3 escalation).
 *
 * ⚠️ SCOPE IS DELIBERATE AND NARROW. This is not a step toward testing the
 * dashboard's React tree or its Supabase-backed mutations — those need a running
 * database or a DOM, and a suite that needs either is a suite that rots. What is
 * covered here is exactly the code that was written to be coverable:
 * `lib/contentValidate.ts` says so in its own first paragraph ("PURE… that is
 * deliberate: this is the one place where a bad publish is stopped, so it has to
 * be testable without a database") and then went 681 lines without a test.
 *
 * The `@/` alias mirrors tsconfig's paths so the tests import the same way the
 * app does.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["lib/__tests__/**/*.test.ts"],
  },
  resolve: {
    alias: { "@": fileURLToPath(new URL(".", import.meta.url)) },
  },
});
