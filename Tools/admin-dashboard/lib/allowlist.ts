import { isMockMode } from "./mode";

/**
 * Server-side admin allowlist (ADMIN_EMAILS env, comma-separated).
 * In mock mode a default allowlist is provided so the tool runs out of the box.
 */

const MOCK_DEFAULT_ADMIN_EMAILS =
  "cesar.guarinoni@wonderwall-g.com,cesar.guarinoni@gmail.com";

export function getAdminEmails(): string[] {
  const raw =
    process.env.ADMIN_EMAILS && process.env.ADMIN_EMAILS.trim().length > 0
      ? process.env.ADMIN_EMAILS
      : isMockMode()
        ? MOCK_DEFAULT_ADMIN_EMAILS
        : "";

  return raw
    .split(",")
    .map((e) => e.trim().toLowerCase())
    .filter((e) => e.length > 0);
}

export function isAdminEmail(email: string | null | undefined): boolean {
  if (!email) return false;
  return getAdminEmails().includes(email.trim().toLowerCase());
}
