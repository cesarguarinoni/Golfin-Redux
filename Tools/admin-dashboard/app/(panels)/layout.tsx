import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { PanelIcon } from "@/components/PanelIcon";
import { DEFAULT_LANG, isLang, LANG_COOKIE, translate, type DictKey } from "@/lib/i18n";
import { checkAdmin } from "@/lib/auth";
import { PANELS } from "@/lib/registry";

export const dynamic = "force-dynamic";

/**
 * Shared shell for all admin panels: server-side auth + allowlist gate,
 * sidebar built from lib/registry.ts.
 */
export default async function PanelsLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const cookieLang = (await cookies()).get(LANG_COOKIE)?.value;
  const lang = isLang(cookieLang) ? cookieLang : DEFAULT_LANG;
  const t = (key: DictKey) => translate(key, lang);

  const check = await checkAdmin();
  if (!check.ok) {
    redirect(check.status === 403 ? "/not-admin" : "/login");
  }

  return (
    <div className="flex min-h-[calc(100vh-2rem)]">
      <aside className="flex w-56 shrink-0 flex-col border-r border-surface-800 bg-surface-900">
        <div className="flex items-center gap-2 px-4 py-4">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-600/20 ring-1 ring-accent-500/40">
            <span aria-hidden>⛳</span>
          </div>
          <div>
            <div className="text-sm font-bold tracking-wide text-zinc-100">
              {t("app.name")}
            </div>
            <div className="text-[10px] uppercase tracking-widest text-zinc-500">
              {t("app.subtitle")}
            </div>
          </div>
        </div>

        <nav className="mt-2 flex-1 space-y-1 px-2">
          {PANELS.map((panel) => (
            <Link
              key={panel.id}
              href={panel.route}
              className="flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium text-zinc-300 transition hover:bg-surface-800 hover:text-zinc-100"
            >
              <PanelIcon name={panel.icon} className="h-4 w-4 text-accent-400" />
              {t(`nav.${panel.id}` as DictKey)}
            </Link>
          ))}
        </nav>

        <div className="border-t border-surface-800 p-3">
          <div
            className="truncate text-xs text-zinc-500"
            title={check.email}
          >
            {check.email}
          </div>
          <form action="/api/auth/signout" method="POST" className="mt-2">
            <button
              type="submit"
              className="w-full rounded-md border border-surface-700 bg-surface-850 px-3 py-1.5 text-xs font-medium text-zinc-300 transition hover:bg-surface-700"
            >
              {t("app.signOut")}
            </button>
          </form>
        </div>
      </aside>

      <main className="min-w-0 flex-1 p-6">{children}</main>
    </div>
  );
}
