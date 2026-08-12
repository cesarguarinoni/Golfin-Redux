import { redirect } from "next/navigation";
import { checkAdmin } from "@/lib/auth";

export const dynamic = "force-dynamic";

export default async function RootPage() {
  const check = await checkAdmin();
  if (check.ok) redirect("/users");
  if (check.status === 403) redirect("/not-admin");
  redirect("/login");
}
