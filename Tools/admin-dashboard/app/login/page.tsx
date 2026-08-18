import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DEFAULT_LANG, isLang, LANG_COOKIE, translate, type DictKey } from "@/lib/i18n";
import { checkAdmin } from "@/lib/auth";
import { isMockMode } from "@/lib/mode";
import { LoginForm } from "./login-form";

export const dynamic = "force-dynamic";

export default async function LoginPage() {
  const cookieLang = (await cookies()).get(LANG_COOKIE)?.value;
  const lang = isLang(cookieLang) ? cookieLang : DEFAULT_LANG;
  const t = (key: DictKey) => translate(key, lang);

  const check = await checkAdmin();
  if (check.ok) redirect("/users");

  return (
    <main className="flex min-h-[calc(100vh-2rem)] items-center justify-center p-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-accent-600/20 ring-1 ring-accent-500/40">
            <span className="text-xl" aria-hidden>
              ⛳
            </span>
          </div>
          <h1 className="text-xl font-semibold text-zinc-100">{t("loginPage.title")}</h1>
          <p className="mt-1 text-sm text-zinc-500">
            {t("loginPage.subtitle")}
          </p>
        </div>
        <LoginForm mockMode={isMockMode()} />
      </div>
    </main>
  );
}
