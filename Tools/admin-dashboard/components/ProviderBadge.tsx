"use client";

import { useT } from "@/components/I18nProvider";
import type { AuthProvider } from "@/lib/types";

/** Small provider glyphs (Google G, Apple mark, envelope for email). */
export function ProviderBadge({ provider }: { provider: AuthProvider }) {
  const t = useT();
  const base =
    "inline-flex h-5 w-5 items-center justify-center rounded-full ring-1";
  if (provider === "google") {
    return (
      <span className={`${base} bg-white/90 ring-zinc-300`} title={t("provider.google")}>
        <svg viewBox="0 0 24 24" className="h-3 w-3" aria-hidden>
          <path
            fill="#4285F4"
            d="M23.5 12.27c0-.85-.08-1.66-.22-2.45H12v4.64h6.45a5.52 5.52 0 0 1-2.4 3.62v3h3.88c2.27-2.1 3.57-5.17 3.57-8.81z"
          />
          <path
            fill="#34A853"
            d="M12 24c3.24 0 5.96-1.07 7.94-2.91l-3.88-3c-1.08.72-2.45 1.15-4.06 1.15-3.13 0-5.78-2.11-6.72-4.95H1.28v3.1A12 12 0 0 0 12 24z"
          />
          <path
            fill="#FBBC05"
            d="M5.28 14.29a7.2 7.2 0 0 1 0-4.58v-3.1H1.28a12 12 0 0 0 0 10.78l4-3.1z"
          />
          <path
            fill="#EA4335"
            d="M12 4.77c1.76 0 3.35.6 4.6 1.8l3.44-3.44A11.98 11.98 0 0 0 1.28 6.6l4 3.1C6.22 6.88 8.87 4.77 12 4.77z"
          />
        </svg>
      </span>
    );
  }
  if (provider === "apple") {
    return (
      <span className={`${base} bg-zinc-100 ring-zinc-300`} title={t("provider.apple")}>
        <svg viewBox="0 0 24 24" className="h-3.5 w-3.5" fill="#111" aria-hidden>
          <path d="M17.05 12.54c-.03-2.62 2.14-3.88 2.24-3.94-1.22-1.79-3.12-2.03-3.8-2.06-1.61-.16-3.15.95-3.97.95-.82 0-2.08-.93-3.43-.9-1.76.03-3.39 1.02-4.3 2.6-1.83 3.18-.47 7.88 1.32 10.46.87 1.26 1.91 2.68 3.27 2.63 1.31-.05 1.81-.85 3.4-.85 1.58 0 2.03.85 3.42.82 1.42-.02 2.32-1.28 3.18-2.55 1-1.46 1.42-2.88 1.44-2.95-.03-.02-2.75-1.06-2.77-4.21zM14.44 4.83c.72-.87 1.2-2.09 1.07-3.3-1.03.04-2.29.69-3.03 1.56-.67.77-1.25 2-1.1 3.18 1.16.09 2.34-.58 3.06-1.44z" />
        </svg>
      </span>
    );
  }
  return (
    <span
      className={`${base} bg-surface-700 text-zinc-300 ring-surface-700`}
      title={t("provider.email")}
    >
      <svg
        viewBox="0 0 24 24"
        className="h-3 w-3"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        aria-hidden
      >
        <rect x="2" y="4" width="20" height="16" rx="2" />
        <path d="m22 7-10 6L2 7" />
      </svg>
    </span>
  );
}
