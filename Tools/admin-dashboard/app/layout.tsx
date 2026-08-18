import type { Metadata } from "next";
import { cookies } from "next/headers";
import { I18nProvider } from "@/components/I18nProvider";
import { LanguageSwitcher } from "@/components/LanguageSwitcher";
import { ModeBanner } from "@/components/ModeBanner";
import { DEFAULT_LANG, isLang, LANG_COOKIE } from "@/lib/i18n";
import { isMockMode } from "@/lib/mode";
import "./globals.css";

export const metadata: Metadata = {
  title: "GOLFIN Admin",
  description: "GOLFIN internal admin dashboard",
};

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const mock = isMockMode();
  // Read the language server-side so the first paint is already correct.
  const cookieLang = (await cookies()).get(LANG_COOKIE)?.value;
  const lang = isLang(cookieLang) ? cookieLang : DEFAULT_LANG;

  return (
    <html lang={lang}>
      <body className="min-h-screen">
        <I18nProvider lang={lang}>
          <ModeBanner mock={mock} />
          <LanguageSwitcher />
          {children}
        </I18nProvider>
      </body>
    </html>
  );
}
