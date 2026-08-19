# `beta_telemetry` — the one remaining check (SPEC §5.11)

**Everything else is done and verified live.** Migration applied, API deployed, all 10
automated acceptance checks pass against `playlife-api.fly.dev`, and the test rows were
cleaned up so the table starts empty. What is NOT yet proven is the only thing an Editor
cannot prove: that the hooks fire in a real player build on real hardware.

Do this once a TestFlight build exists. It takes about three minutes.

## 1. Play

On the device, **signed in** (the queue holds while unauthenticated and only drains on
`SignedIn`, so a signed-out session is not a valid test):

1. Launch the app from cold — that is `session_start` + the boot→Home `screen_view`.
2. Walk Home → Hole Selection → start a hole — that is `round_start`.
3. Take a few shots. **Deliberately botch one flick** (pull back and release slowly so it
   is rejected) and **cancel one drag** — those are the two events that route through the
   `ShotTelemetryRelay` assembly bridge, so they are the ones most worth confirming.
4. Finish the hole, or quit to Home mid-round to exercise `round_abandoned` instead.
5. Background the app — that is `session_end`.

Events flush at 20 queued / 30s / pause / quit, so backgrounding forces the send.

## 2. Confirm the rows

```bash
cd ~/Documents/GolfinRedux && set -a && . Tools/admin-dashboard/.env.development.local && set +a && curl -s "$SUPABASE_URL/rest/v1/telemetry_events?select=name,ts,platform,device_model,payload&order=ts.desc&limit=40" -H "apikey: $SUPABASE_SERVICE_ROLE_KEY" -H "Authorization: Bearer $SUPABASE_SERVICE_ROLE_KEY" | python3 -m json.tool
```

## 3. What "PASS" looks like

| Check | Expected |
|---|---|
| `session_start` | present once, `device_model` a real iPhone string (NOT `null` / a simulator name) |
| `screen_view` | one per screen; the FIRST `Home` row's `since_boot_s` **is** the boot→Home load-time metric |
| `round_start` | `hole` matches the hole actually played; `character_id` non-empty |
| `shot_taken` | one row per shot, `shot_number` ascending from 1, `club` the club used, `distance_m` plausible |
| `flick_rejected` / `shot_cancelled` | present — **these prove the relay works on device**; if every other event is there but these two are missing, the bridge is the suspect |
| `hole_complete` | `strokes` matches the scorecard, `par` correct for the hole, `fps_avg` / `fps_low` plausible for the device |
| `platform` | `IPhonePlayer` (if it says anything Editor-ish, the send gate is wrong) |

## 4. If nothing arrives at all

In order of likelihood:

1. **Not signed in** — the queue holds and never flushes. Most likely cause by far.
2. **Never backgrounded / fewer than 20 events and under 30s** — no flush was triggered yet.
3. Confirm the endpoint is still up: `curl -s https://playlife-api.fly.dev/health` → `{"status":"ok"...}`.
4. Only then suspect the client. `TelemetryHooks.Install()` logs
   `[Telemetry] Hooks installed — session=…, sends=…` at boot; `sends=False` on device
   would mean the Editor gate leaked into a player build.

## 5. When it passes

Move `Docs/Specs/Active/beta_telemetry/` → `Docs/Specs/Completed/`, and note in
`Docs/AI_CONTEXT.md` that §5.11 is signed off.
