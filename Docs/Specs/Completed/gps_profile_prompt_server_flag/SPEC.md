# SPEC — `gps_profile_prompt_server_flag` (Quick)

> Amends `auth_golf_profile` and `gps_profile_prompt_on_entry`. Cesar, 2026-09-03: "if I log in for
> the first time to GPS but had already logged in from Game and selected my user/colour, that screen
> should be skipped (and vice versa)."

## Goal

The Golf Profile + Welcome offer is **once per ACCOUNT**, not once per device or per app. Completing
(or skipping) it in the game, in the standalone GOLFIN GPS app, or on another phone means no other
install ever offers it again. The PlayerPrefs flag stays only as a fast-path cache.

## Backend (playlife — migration pasted in chat by the Architect, Cesar applies, then Fly deploy)

1. `2026_09_03_golf_profile_prompted.sql`: `alter table public.profiles add column if not exists golf_profile_prompted_at timestamptz;` — nullable, additive. **Backfill**: `update profiles set golf_profile_prompted_at = coalesce(golf_profile_prompted_at, now()) where avatar_color is not null or golf_experience is not null;` (anyone who already saved a Golf Profile is never re-asked).
2. `PUT /user/update` (`UpdateProfileRequest`) gains `golf_profile_prompted: Optional[bool] = None`; `true` stamps `golf_profile_prompted_at = now()` (never cleared by the endpoint). Flutter untouched. `/user/detail` is `select("*")` → flows automatically; `UserDetailDto` gains `GolfProfilePromptedAt` (string ISO, parsed with `DateParseHandling.None` like the others).

## Unity

3. `GpsAuthExtrasFlow.ShouldOffer(gpsEnabled, signedIn, prompted)` — `prompted` becomes `prompted = PlayerPrefs flag || (UserService.LastDetail?.GolfProfilePromptedAt != null)`; when `LastDetail` is null (not fetched yet) the intercept **waits for the detail fetch** (the hub already fetches it; the intercept in `ScreenManager.Navigate` runs after a `UserService.EnsureDetail(cb)` — one round trip, spinner-free because Splash → hub already takes the fade) rather than guessing. Offline / fetch failed → do NOT offer (never nag; the next entry retries).
4. **SAVE** writes the profile as today AND `golf_profile_prompted: true` in the same `PUT`. **Skip** sends a `PUT /user/update` with `display_name` (unchanged, still required) + `golf_profile_prompted: true` — the one write Skip now makes, so "vice versa" holds across installs. Both also set the local flag (cache).
5. When the server flag is set but the local flag is not (new install of either app), `ShouldOffer` returns false and writes the local flag — so the second launch is fast.

## Acceptance (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Migration applied (Cesar) + backfill count quoted; `PUT` with `golf_profile_prompted:true` stamps the column; `GET /user/detail` echoes it; Fly deploy id.
- [ ] Editor: flag cleared locally, server `golf_profile_prompted_at` NULL → offered; SAVE → column stamped (quote); clear PlayerPrefs, relaunch → NOT offered, local flag re-cached (log).
- [ ] Editor: server NULL, local cleared → Skip → column stamped by the Skip PUT (quote the request body); relaunch on a "fresh device" (PlayerPrefs cleared) → not offered.
- [ ] Detail fetch failure path: not offered, no crash, next entry retries (log).
- [ ] EditMode: `GpsAuthExtrasFlowTests` extended with the server-flag truth table (local × server × fetched/unfetched).
- [ ] No prefab/string change (`git status Assets/Prefabs` empty; PLAN `add 0`).
- [ ] `Docs/GPS/GPS_DEVICE_PASS.md` §1 rows updated: install the game, complete the Golf Profile, install GOLFIN GPS standalone → hub directly; and the reverse.

## Out of scope

The Settings edit screen; clearing the flag from the admin (backlog if ever needed).
