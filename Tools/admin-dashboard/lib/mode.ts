/**
 * Mock-mode switch. The app runs on local fixtures when the service_role key
 * is absent OR MOCK_MODE=1 is set explicitly.
 *
 * Server-side only in practice (reads non-NEXT_PUBLIC env); client components
 * receive the flag as a prop / API field instead of calling this.
 */
export function isMockMode(): boolean {
  return process.env.MOCK_MODE === "1" || !process.env.SUPABASE_SERVICE_ROLE_KEY;
}
