import type { Metadata } from "next";
import { ModeBanner } from "@/components/ModeBanner";
import { isMockMode } from "@/lib/mode";
import "./globals.css";

export const metadata: Metadata = {
  title: "GOLFIN Admin",
  description: "GOLFIN internal admin dashboard",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const mock = isMockMode();
  return (
    <html lang="en">
      <body className="min-h-screen">
        <ModeBanner mock={mock} />
        {children}
      </body>
    </html>
  );
}
