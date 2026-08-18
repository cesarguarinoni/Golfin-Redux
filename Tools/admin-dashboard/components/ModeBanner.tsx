"use client";

import { useT } from "@/components/I18nProvider";

/** Mode banner shown on every page: yellow for mock fixtures, red for live DB. */
export function ModeBanner({ mock }: { mock: boolean }) {
  const t = useT();
  if (mock) {
    return (
      <div className="sticky top-0 z-50 flex items-center justify-center gap-2 border-b border-yellow-600/40 bg-yellow-500/15 px-4 py-1.5 text-xs font-semibold tracking-wide text-yellow-300">
        <span aria-hidden>▲</span>
        {t("mode.mock")}
      </div>
    );
  }
  return (
    <div className="sticky top-0 z-50 flex items-center justify-center gap-2 border-b border-red-500/60 bg-red-600/25 px-4 py-1.5 text-xs font-bold tracking-widest text-red-300">
      <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-red-400" aria-hidden />
      {t("mode.production")}
    </div>
  );
}
