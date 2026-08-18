"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { LANGS, LANG_COOKIE, LANG_SHORT, type Lang } from "@/lib/i18n";
import { useLang, useT } from "@/components/I18nProvider";

/**
 * EN / 日本語 switcher, fixed top-right.
 *
 * Writes the cookie then calls router.refresh() rather than reloading: the
 * language is read server-side, so a refresh re-renders every server component
 * in the new language while keeping client state (open drawer, filters, typed
 * form fields) intact. A full reload would throw that away mid-edit.
 */
export function LanguageSwitcher() {
  const current = useLang();
  const t = useT();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  function choose(lang: Lang) {
    if (lang === current) return;
    // 1 year, site-wide, Lax is enough — this is a display preference, not auth.
    document.cookie = `${LANG_COOKIE}=${lang}; path=/; max-age=31536000; samesite=lax`;
    startTransition(() => router.refresh());
  }

  return (
    <div
      className={`fixed right-4 top-9 z-30 flex overflow-hidden rounded-md border border-surface-700 bg-surface-900/95 text-xs shadow-lg backdrop-blur transition ${
        pending ? "opacity-60" : ""
      }`}
      role="group"
      aria-label={t("app.language")}
    >
      {LANGS.map((lang) => (
        <button
          key={lang}
          type="button"
          onClick={() => choose(lang)}
          aria-pressed={lang === current}
          className={`px-2.5 py-1 font-medium transition ${
            lang === current
              ? "bg-accent-600 text-white"
              : "text-zinc-400 hover:bg-surface-800 hover:text-zinc-200"
          }`}
        >
          {LANG_SHORT[lang]}
        </button>
      ))}
    </div>
  );
}
