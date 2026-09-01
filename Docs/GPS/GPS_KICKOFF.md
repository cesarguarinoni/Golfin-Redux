# KICKOFF — GOLFIN: GPS → Unity (standalone app + game-integrated)

**Paste this into the new conversation to start. It points at the reference docs already in `GolfinRedux/Docs/GPS/`.**

---

## Goal

Rebuild the PLAYLIFE / GPS feature set (currently a Flutter "sister app") in **Unity / C#**, deployed **two ways**:
1. **Standalone GPS app** — the GPS/PLAYLIFE features on their own.
2. **Integrated** — the same features embedded inside the GOLFIN Redux game.

Both share **one backend** and **one C# module** — nothing is built twice.

## Read these first (already in the repo)

- `Docs/GPS/GPS_INTEGRATION_REFERENCE.md` — system facts: backend address (`https://playlife-api.fly.dev`, Fly.io/Tokyo), Supabase project, full endpoint list, DB schema, **GPS-Trust constants + exact `/score/submit` payload**, IAP flow, env vars, tech debt, source-file map.
- `Docs/GPS/GPS_UNITY_PORT_SPEC.md` — the build plan: target architecture, native-capability plan, feature→screen mapping, phased effort (v1 ≈ 7–11 wks, full ≈ 3.5–5 mo).
- `Docs/GPS/GOLFIN_Backend_Hosting_Options.md` — hosting decision (stay Fly.io + Supabase, ~$25–35/mo).
- Also upload/read `AI_CONTEXT.md`, `Rules.md`, `Tellcode.md` per the project workflow.

## Architecture — the key idea (build once, ship twice)

Build the GPS features as **one shared Unity module** (assembly definitions under e.g. `Golfin.Gps.*`):

- `Golfin.Net` — `ApiClient` (UnityWebRequest + Bearer + 401→refresh + retry, `{data}` envelope, `Result<T>`) + `Endpoints`
- `Golfin.Auth` — Supabase auth (email/pw + Google/Apple OAuth deep-link, session + token refresh)
- `Golfin.Gps` — location + **mock detection** + `GpsSessionTracker`/`GpsTrustSignals`/`GpsScoreAttachment` (pure-logic port; keep the exact trust constants and payload fields)
- `Golfin.Economy` — points (dual currency), gifts, IAP verify
- `Golfin.Social` — follow/feed/ranking, badges, moderation
- plain-C# **DTOs** mirroring the freezed models

Two **thin presentation shells** consume the module: the standalone app, and the in-game integration (as `ScreenManager` states + `ModalController` overlays). The backend (FastAPI + Supabase) is untouched; both shells authenticate to Supabase and call the same API with a Bearer JWT.

## Locked decisions

- One shared backend + one Supabase project (no data migration).
- `RewardPointsManager` becomes a **client of `/points/*`** — a single server-authoritative ledger, not a second currency.
- Hosting stays **Fly.io + Supabase**.

## Confirm before building (first thing in the new chat)

1. **Standalone tech:** a Unity thin-shell over the shared module *(recommended — true code reuse)*, or keep the existing Flutter app as the standalone and build only the integration in Unity?
2. **Standalone scope:** GPS/PLAYLIFE features only (check-in, score recognition, points, social) — no golf gameplay in the standalone?
3. **Maps:** list-only venues for v1 (defer a real map)?
4. **Two hard native bits — owner/plan:** the Android **mock-GPS detection plugin** (gates the whole Trust/anti-cheat value) and the **avatar inventory ↔ GOLFIN cosmetic** mapping.

## First deliverable (P0 — the unblocker)

The **`Golfin.Net` / `ApiClient`** per-feature spec, written against the *real* GOLFIN conventions (asmdef layout, `ScreenManager`, `ModalController`, `.Instance` singletons, event-driven UI, JP/EN localization). Then, in dependency order: **Auth → Gps/Trust → ScoreRecognition → Points**.

To write these against real symbols (not `NOTE` placeholders), connect the GolfinRedux Unity repo or share: `ScreenManager.cs`, `ModalController.cs`, `RewardPointsManager.cs`, `CharacterManager.cs`, and the localization entry points.

## Workflow

Architect (this chat) produces spec `.md` files → drop into `Docs/` → Claude Code implements. Windows/PowerShell; prefer minimal diffs; flag unknown APIs with `NOTE` rather than guessing; `/effort` max.
