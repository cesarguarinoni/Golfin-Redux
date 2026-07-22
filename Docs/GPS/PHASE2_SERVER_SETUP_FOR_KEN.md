# Golfin Account — Server Setup Checklist (for Ken)

Hi Ken 👋 — the Golfin game now has Login / Sign Up / Create Username / Email Confirmation screens.
Right now they run in a **practice ("mock") mode** so we can build the game without touching the real
server. To make them talk to the **real PLAYLIFE / Supabase account system**, we need a few things set
up on the server side. This doc lists exactly what to do, in plain steps. You do NOT need to touch any
game code — everything here is done in the **Supabase dashboard** (and, later, Google/Apple consoles).

When you finish a section, **send Cesar the values it produces** (there's a "➡️ Send Cesar" line each time).

Everything below is for the existing Supabase project:
- **Project:** PLAYLIFE (ref `wmszyghwwkaptgqdunel`)
- **Dashboard:** https://supabase.com/dashboard/project/wmszyghwwkaptgqdunel

---

## STEP 0 — Give Cesar admin access (please do this first)

So Cesar can help or take over the backend if ever needed, please add him as an **admin/owner** on the
project. This is separate from the game and only takes a minute.

**Supabase:**
1. Open the dashboard link above.
2. Left sidebar → **Project Settings** (gear icon) → **Team** (or **Members**).
3. Click **Invite** / **Add member**.
4. Enter **Cesar's email: cesar.guarinoni@wonderwall-g.com**.
5. Set the role to **Owner** (or **Admin** if Owner isn't available).
6. Send the invite.

**Fly.io (the PLAYLIFE API server), if you manage it:**
1. Go to https://fly.io/dashboard → the **`playlife-api`** app → **Organization** → **Members**.
2. Invite **cesar.guarinoni@wonderwall-g.com** as an **Admin**.

➡️ **Send Cesar:** "You've been invited as admin on Supabase (and Fly.io)." Cesar will accept the email invites.

---

## PHASE A — Go live with Email + Password (do this first)

This is all we need to switch the four screens from practice mode to the real server.

### A1. Send us the "anon" key (2 minutes)
This is a **public** key the app uses to talk to Supabase (it's safe to put in the app — the mobile
PLAYLIFE app already uses it).

1. Open the dashboard link above.
2. Left sidebar → **Project Settings** (the gear icon at the bottom).
3. Click **API**.
4. Under **Project API keys**, find the row labelled **`anon` `public`**.
5. Click **Copy** on that row.

➡️ **Send Cesar:** that copied `anon public` key. (Do NOT send the `service_role` key — that one is secret
and must never go in the game.)

### A2. Turn on Email sign-up + confirmation (3 minutes)
1. Left sidebar → **Authentication**.
2. Click **Providers** (or **Sign In / Providers**).
3. Find **Email** in the list and make sure it is **Enabled** (toggle on).
4. Open the Email provider settings and confirm:
   - **Confirm email** = **ON** (users must click a link in their email before they can log in — this is
     the "Email Confirmation" screen in the game).
   - **Enable Sign-ups** = **ON** (otherwise new players can't register).
5. Click **Save**.

➡️ **Send Cesar:** a quick "Email + Confirm email are ON" confirmation.

### A3. Set the website / redirect addresses (3 minutes)
When someone clicks the confirmation link in their email, Supabase needs to know where to send them.

1. **Authentication** → **URL Configuration**.
2. **Site URL:** set to `https://playlife-app.web.app/` (the existing PLAYLIFE web address — same one the
   phone app uses).
3. **Redirect URLs:** make sure `https://playlife-app.web.app/*` is in the allow-list. (We'll add the
   game's own address here later for Google/Apple sign-in — Phase B.)
4. Click **Save**.

➡️ **Send Cesar:** "URL configuration saved."

### A4. (Optional but recommended) Check the confirmation email text (5 minutes)
1. **Authentication** → **Email Templates**.
2. Open **Confirm signup**.
3. Make sure the subject/body look right for Golfin (you can leave the default if you're not sure — it
   works fine). The `{{ .ConfirmationURL }}` placeholder must stay in the template — that's the link
   users click.

➡️ **Send Cesar:** "Email template checked" (or your edited text if you changed it).

### ✅ After Phase A
Once Cesar has the **anon key** (A1) and A2–A3 are done, he flips one switch in the game and the real
sign-up / login / email-confirmation flow is live. **No app-store release needed for this** — it's a config change.

---

## PHASE B — Google & Apple "Sign in with…" buttons (later, only when we're ready)

The game already shows **Login with Google** and **Login with Apple** buttons; today they say
"coming soon". To make them work, the accounts need to be set up with Google and Apple. This is more
involved and usually needs a developer's help on the Google/Apple side — **do not start this until Cesar
says so.** Here's what it will require so you can plan:

### B1. Google sign-in
1. In **Google Cloud Console** (https://console.cloud.google.com) for the PLAYLIFE project, create an
   **OAuth 2.0 Client ID** (type: Web application).
2. Add this as an **Authorized redirect URI**:
   `https://wmszyghwwkaptgqdunel.supabase.co/auth/v1/callback`
3. Copy the **Client ID** and **Client Secret**.
4. In Supabase: **Authentication → Providers → Google** → paste the Client ID + Secret → **Enable** → **Save**.

➡️ **Send Cesar:** "Google provider enabled" (Cesar does NOT need the secret — it stays in Supabase).

### B2. Apple sign-in
1. In the **Apple Developer** account (https://developer.apple.com), create a **Services ID** and a
   **Sign in with Apple key** for the Golfin app.
2. Add the same redirect: `https://wmszyghwwkaptgqdunel.supabase.co/auth/v1/callback`
3. In Supabase: **Authentication → Providers → Apple** → fill in the Services ID / Team ID / Key → **Enable** → **Save**.

➡️ **Send Cesar:** "Apple provider enabled."

### B3. The game's return address (Cesar + a developer will provide)
For the phone to come back into the game after Google/Apple sign-in, we'll give you one extra
**Redirect URL** (a "deep link" like `golfin://auth-callback`) to add under
**Authentication → URL Configuration → Redirect URLs**. Cesar will send you the exact text when the
game side is ready.

---

## What NOT to send / do
- ❌ Never send the **`service_role`** key or any password — those are secret. Only the **`anon public`** key
  goes to the game.
- ❌ You don't need to edit any game code, Unity, or GitHub — everything here is in the Supabase/Google/Apple dashboards.

## Quick summary of what Cesar is waiting on for Phase A
1. The **`anon public`** key (step A1). ← this is the main blocker
2. Confirmation that **Email + Confirm-email are ON** (A2) and **URLs are set** (A3).

That's it for going live with email/password. Thanks Ken! 🙏

---

*Technical note (for Cesar / devs, ignore if you're Ken): the client authenticates directly to Supabase
Auth (GoTrue REST) per `GPS_INTEGRATION_REFERENCE.md` §3, then attaches the JWT as `Authorization: Bearer`
on PLAYLIFE FastAPI calls. To go live: paste the anon key into `Assets/Resources/SupabaseConfig.asset`
and set `useMockTransport = false`. Before a real production launch, move session tokens off PlayerPrefs
into a platform secure store (iOS Keychain / Android Keystore) — see `AuthSession.cs` header note.*
