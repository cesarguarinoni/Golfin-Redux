"use client";

import { useState } from "react";
import { useT } from "@/components/I18nProvider";
import { createSupabaseBrowserClient } from "@/lib/supabase/client";

export function LoginForm({ mockMode }: { mockMode: boolean }) {
  const t = useT();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mockMode) {
        const res = await fetch("/api/auth/mock-login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email }),
        });
        if (!res.ok) {
          const body = (await res.json().catch(() => null)) as {
            error?: string;
          } | null;
          throw new Error(body?.error ?? `Login failed (${res.status})`);
        }
      } else {
        const supabase = createSupabaseBrowserClient();
        const { error: signInError } = await supabase.auth.signInWithPassword({
          email,
          password,
        });
        if (signInError) throw new Error(signInError.message);
      }
      // Hard navigation: guarantees the new session cookie is applied and
      // avoids a push/refresh race that intermittently left the URL on /login.
      window.location.assign("/users");
    } catch (err) {
      setError(err instanceof Error ? err.message : t("login.failed"));
      setBusy(false);
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-xl"
    >
      {mockMode && (
        <div className="mb-4 rounded-md border border-yellow-600/40 bg-yellow-500/10 px-3 py-2 text-xs text-yellow-300">
          <span className="font-bold">{t("common.mock")}</span> — {t("login.mockHint")}
        </div>
      )}
      <label className="block text-xs font-medium text-zinc-400">
        {t("login.email")}
        <input
          type="email"
          required
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="admin@wonderwall-g.com"
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
      </label>
      <label className="mt-4 block text-xs font-medium text-zinc-400">
        {t("login.password")}
        <input
          type="password"
          required={!mockMode}
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder={mockMode ? "(ignored in mock mode)" : "••••••••"}
          className="mt-1 w-full rounded-md border border-surface-700 bg-surface-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 focus:border-accent-500 focus:outline-none"
        />
      </label>
      {error && (
        <p className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-300">
          {error}
        </p>
      )}
      <button
        type="submit"
        disabled={busy}
        className="mt-5 w-full rounded-md bg-accent-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-accent-500 disabled:opacity-50"
      >
        {busy ? t("login.submitting") : t("login.submit")}
      </button>
    </form>
  );
}
