#!/usr/bin/env bash
# Build and deploy the admin dashboard to Cloudflare Workers.
#
# WHY THIS SCRIPT EXISTS, and why it is not just `opennextjs-cloudflare deploy`:
#
# Next.js loads .env / .env.local / .env.development.local at BUILD time, and
# OpenNext writes whatever it finds into `.open-next/cloudflare/next-env.mjs`,
# which is then uploaded as part of the Worker. That means a plain build ships
# the service_role key inside the bundle — even when it lands in the unused
# `development` map. The Worker does not need it there: `wrangler secret`
# values are injected into process.env at runtime.
#
# So: move the local env file out of the way for the duration of the build,
# and put it back afterwards. The trap restores it even if the build fails or
# you Ctrl-C, because leaving a developer without their .env file is a nasty
# way to end a deploy.
#
# Also note: `next build` and `next dev` share .next/. Stop the dev server
# before running this (see the warning at the top of README.md).
set -euo pipefail
cd "$(dirname "$0")/.."

ENV_FILE=".env.development.local"
STASH=".env.development.local.deploy-stash"

restore() {
  if [ -f "$STASH" ]; then
    mv -f "$STASH" "$ENV_FILE"
    echo "↩︎  restored $ENV_FILE"
  fi
}
trap restore EXIT INT TERM

if [ -f "$ENV_FILE" ]; then
  mv "$ENV_FILE" "$STASH"
  echo "→  $ENV_FILE moved aside so the build cannot bake secrets into the bundle"
fi

# NEXT_PUBLIC_* are a different animal from the rest. Next.js INLINES them into
# the client bundle at build time, so a Worker secret cannot supply them — the
# browser code is already compiled. They are public by design (the anon key is
# meant to ship), so we hand exactly those two to the build via the environment
# and nothing else. Everything server-side — SUPABASE_SERVICE_ROLE_KEY,
# SUPABASE_URL, ADMIN_EMAILS — stays a runtime secret.
PUBLIC_ENV=()
if [ -f "$STASH" ]; then
  while IFS= read -r line; do
    case "$line" in NEXT_PUBLIC_*) PUBLIC_ENV+=("$line");; esac
  done < "$STASH"
fi
if [ ${#PUBLIC_ENV[@]} -eq 0 ]; then
  echo "✘  ABORT: no NEXT_PUBLIC_* values found — the browser client would build" >&2
  echo "   with empty values and fail at runtime with 'required in live mode'." >&2
  exit 1
fi
echo "→  passing ${#PUBLIC_ENV[@]} NEXT_PUBLIC_* value(s) to the build (public by design)"

echo "→  building…"
env NODE_ENV=production "${PUBLIC_ENV[@]}" npx opennextjs-cloudflare build

# Fail loudly rather than shipping a bundle with a credential in it.
if [ -f "$STASH" ]; then
  KEY=$(grep -E '^SUPABASE_SERVICE_ROLE_KEY=' "$STASH" | cut -d= -f2- || true)
  if [ -n "${KEY:-}" ] && grep -rql -- "$KEY" .open-next/ 2>/dev/null; then
    echo "✘  ABORT: the service_role key is present in .open-next/. Not deploying." >&2
    exit 1
  fi
fi
echo "✓  bundle carries no service_role key"

echo "→  deploying…"
npx opennextjs-cloudflare deploy
