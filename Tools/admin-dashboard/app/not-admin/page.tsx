import { redirect } from "next/navigation";
import { getSessionEmail } from "@/lib/auth";
import { isAdminEmail } from "@/lib/allowlist";

export const dynamic = "force-dynamic";

export default async function NotAdminPage() {
  const email = await getSessionEmail();
  if (!email) redirect("/login");
  if (isAdminEmail(email)) redirect("/users");

  return (
    <main className="flex min-h-[calc(100vh-2rem)] items-center justify-center p-6">
      <div className="w-full max-w-md rounded-xl border border-surface-700 bg-surface-900 p-8 text-center shadow-xl">
        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-red-500/15 ring-1 ring-red-500/40">
          <svg
            viewBox="0 0 24 24"
            className="h-6 w-6 text-red-400"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden
          >
            <circle cx="12" cy="12" r="10" />
            <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
          </svg>
        </div>
        <h1 className="text-lg font-semibold text-zinc-100">Not an admin</h1>
        <p className="mt-2 text-sm text-zinc-400">
          You are signed in as{" "}
          <span className="font-mono text-zinc-200">{email}</span>, but that
          address is not on the admin allowlist (ADMIN_EMAILS). Ask Cesar to add
          you, or sign in with an admin account.
        </p>
        <form action="/api/auth/signout" method="POST" className="mt-6">
          <button
            type="submit"
            className="rounded-md border border-surface-700 bg-surface-800 px-4 py-2 text-sm font-medium text-zinc-200 transition hover:bg-surface-700"
          >
            Sign out
          </button>
        </form>
      </div>
    </main>
  );
}
