# SPEC — points_device_checks

**Filed:** 2026-08-13 (Architect, on Cesar's close-out of `reward_points_backend`)
**Type:** Manual device verification. **No code, no subagent pipeline.**
**Owner:** Cesar (only he can run these — they need a real device + a live Supabase login)
**Split from:** `reward_points_backend` (now in `Docs/Specs/Completed/`). Source of these
three checks: that task's `IMPLEMENTER_REPORT.md` Part 3 § "Needs Cesar (manual, on device)".

---

## §1 Why this exists as its own task

`reward_points_backend` shipped its code end-to-end on 2026-08-12: Phase A in prod, Slice 1,
Slice 2 (rebalance + cutover, `PointsBackendEnabled` default ON), and the three
`points_cutover_followups` (bot auth bypass, shop server spend, hard sign-in gate). EditMode is
**1172 passed / 0 failed / 3 pre-existing skips of 1175**.

What is left is **not implementation work** — it is three things the Editor structurally cannot
prove: a real touch test, a real network drop, and a real double-tap. Rather than hold a
finished, committed spec folder open on a manual pass, Cesar split those checks out here so
`reward_points_backend` could close and the `admin_dashboard` hold could lift.

**Nothing is known to be broken.** These are confirmations, not a defect list.

---

## §2 Preconditions

| | |
|---|---|
| Build | Device build (iPhone), **not** the Editor. Checks 2 and 3 need the flag ON — on device the switch is the `GOLFIN_POINTS_BACKEND` scripting define (Player Settings → Other Settings), because the Editor's `GOLFIN > Points Backend` toggle lives in PlayerPrefs and does not travel to the phone. |
| Account | A real signed-in Supabase account with a non-zero RP balance. New accounts start at **0** (the welcome grant was removed by design) — set a test balance via the Supabase table editor or an admin grant first, or checks 2/3 have nothing to spend. |
| Backend | Prod is live: `/health` ok, `/points/spend` + `/points/earn-game` + `/points/balance` all auth-gated (403, not 404). Catalog SQL applied and verified. |
| Editor caveat | Google/Apple sign-in **cannot** complete in the Editor (the deep-link receiver exists only in builds). Email/password is the Editor path. On device, either works. |

---

## §3 The three checks

### Check 1 — signed-out launch cannot get past Login

**What it guards:** `AuthGate` on the `ScreenManager.ShowScreen` seam (deny-by-default), and the
deletion of `DevBypassCatcher_TEMP` — an invisible full-screen tap-catcher that **shipped in
player builds** and sent any stray tap straight to Home with no auth at all.

**Steps**
1. Sign out (or launch on a device that has never signed in). Cold-launch the app.
2. On the splash / title screen, **tap around the art — not just the buttons.** Corners, the
   logo, dead space, the area the old catcher covered. The removed bypass was a *tap-anywhere*
   affordance, so tapping only the real buttons does not exercise it.
3. Tap `START`.

**PASS:** every tap either does nothing or lands on **Login**. Home / Mode Select are never
reachable while signed out.
**FAIL:** any tap reaches Home or any gameplay screen.

> Editor coverage of this is *structural* (`AuthGate` verified by `ShowScreen(Home)` and
> `ShowScreen(ModeSelection)` both redirecting to Login). It is not a real touch test — which is
> exactly the gap this check closes.

---

### Check 2 — flag-ON shop purchase stays debited, and airplane mode grants nothing

**What it guards:** both `ShopTransaction` entry points now debit through `PointsSpendGate`
(server first, grant only on approval). Before this, a flag-ON purchase **self-refunded** on the
next balance refresh, because the spend was local-only while the balance was server-authoritative.

**Steps**
1. Signed in, flag ON, note the starting RP balance.
2. Buy **one stamina item** (stamina shop) and **one catalog item** (general shop).
3. Force a balance refresh — background/foreground the app, or navigate away and back.
4. **PASS:** the balance is still debited by exactly the two prices, and both items are still
   owned. **FAIL:** the balance drifts back up (the self-refund) or an item vanishes.
5. Put the device in **airplane mode**. Attempt one more purchase.
6. **PASS:** a "Connection required" refusal, **no item granted**, balance unchanged. **FAIL:**
   the item is granted, or the balance moves, or the app hangs with no message.

> Prices are post-rebalance (÷10 from the old economy) — check against the shop UI, not memory.

---

### Check 3 — double-tap BUY is a no-op

**What it guards:** the process-wide in-flight latch in `PointsSpendGate` plus the per-controller
latch in `ShopTransaction`. Without it, a slow round-trip lets a second tap fire a second debit.

**Steps**
1. Signed in, flag ON, on a **slow connection** (throttle, or weak signal — a fast connection may
   close the window before you can land the second tap).
2. Tap `BUY` twice in quick succession on the same item.

**PASS:** exactly **one** debit and **one** grant. The second tap is a no-op.
**FAIL:** double debit, double grant, or a second confirmation modal.

---

## §4 What to do with the result

- **All three PASS** → set `STATUS.md` to `DONE` and move this folder to
  `Docs/Specs/Completed/points_device_checks/`. The RP backend cutover is then fully closed.
- **Any FAIL** → set `STATUS.md` to `DEVICE_FAIL`, write what happened (screen, screenshot,
  balance before/after, and whether the flag was ON) into a `DEVICE_FAIL.md` in this folder, and
  hand it to a Claude Code session as a normal fix task. The relevant code is:
  - Check 1 → `Assets/Scripts/UI/AuthGate.cs`, `SplashScreenController.cs`
  - Check 2 → `PointsSpendGate`, `ShopTransaction` (both entry points)
  - Check 3 → the in-flight latch in `PointsSpendGate`

---

## §5 Out of scope

- Any code change. If a check fails, that becomes a **new** task — this folder only records the
  verdict.
- Re-verifying anything already covered by the EditMode suite or the Editor play-mode checks in
  `reward_points_backend`'s report. These three checks exist *because* they are the ones the
  Editor cannot reach.
