"use client";

import { createContext, useContext } from "react";
import { DEFAULT_LANG, translate, type DictKey, type Lang } from "@/lib/i18n";

/**
 * Language context. The value comes from a cookie read on the SERVER in the
 * root layout, so the first paint is already in the right language — no
 * English-then-flip flash.
 */
const LangContext = createContext<Lang>(DEFAULT_LANG);

export function I18nProvider({
  lang,
  children,
}: {
  lang: Lang;
  children: React.ReactNode;
}) {
  return <LangContext.Provider value={lang}>{children}</LangContext.Provider>;
}

export function useLang(): Lang {
  return useContext(LangContext);
}

/** `const t = useT()` then `t("nav.users")`, or `t("udel.body", { email })`. */
export function useT(): (
  key: DictKey,
  vars?: Record<string, string | number>
) => string {
  const lang = useContext(LangContext);
  return (key: DictKey, vars?: Record<string, string | number>) =>
    translate(key, lang, vars);
}
