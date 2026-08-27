# Device pass — content pipeline (Phases 1–4)

**Written 2026-08-26 for a pass on 2026-08-27.** Everything below is on-device work that the
Editor structurally cannot close. Tick the results table at the bottom as you go.

---

## ⚠️ READ THIS FIRST — three things that make tests look broken when they are not

**1. A published change needs TWO relaunches, not one.**
Invariant I5: a fetch **writes the cache** and applies at the **next** launch. Plus the endpoint has
a 60 s response cache. So the sequence after every publish is:

> publish → **wait 60 s** → launch (fetches, caches, shows OLD content) → **quit fully** → launch
> (shows NEW content)

Seeing old content on the first relaunch is **correct**. Judging a test on one relaunch will fail
every single one of them.

**2. Grants also need two launches** — the queue drains at boot only, and its flag is set only on a
successful fetch. A launch with no network simply does not drain; the grant is still there next time.

**3. The save schema is now v11 and the migrator fails hard on a newer-than-code file.** If you roll
a build BACK to a pre-v11 binary, that device cannot read its save. Phase 4 partly rescues it — the
bricked save restores from the server on the next launch — so "my stuff is gone" may just need one
more relaunch. **Tell the testers this before they touch anything.**

---

## Step 0a — ✅ DONE 2026-08-26 (Cesar). playlife deployed; the new wire shape is live.

The `content_cleanup_quick` backend half is **in the working tree only** — `playlife-api` still
serves the old per-catalog `enabled` field. Harmless in isolation (the client ignores unknown
fields), but it means **the wire would not match the client under test**, and 2.5/2.6 are exactly
the tests that turn on wire shape.

1. `fly deploy` from `playlife`. ⚠️ **Verify by `flyctl status` image version and a live probe, never
   by the exit code** — a flyctl token can expire mid-run and 401 the post-update phase on a deploy
   that actually landed.
2. Probe: `/health`, `/notices`, `/banners`, `/tournaments/golfin` → all 200.
3. Confirm the new shape: request two catalogs with one disabled and check top-level `enabled` stays
   **true** with `disabled: ["<name>"]`. Re-enable immediately.

Then build.

## Step 0 — BUILD AND UPLOAD — **DO THIS TODAY, not tomorrow.**

Neither the deploy nor the build needs a device. Doing them the day before means the pass opens on
an installable build instead of on TestFlight processing.

**No TestFlight build contains any of this.** `last_uploaded_build.txt` = **2286**; the content
client landed at ~2295–2309. Every device in the field is running code with no `ContentService`,
no `Golfin.Content`, and no inventory sync.

1. Confirm the tree is clean and `content_cleanup_quick` has landed (or accept it hasn't and note
   which of its five items are missing — item 1, the per-catalog `enabled` field, is the only one
   that changes wire shape).
2. Build + upload per `Docs/TESTFLIGHT_RUNBOOK.md`. `BuildStampGenerator` refuses to build if the
   computed number is ≤ `last_uploaded_build.txt`, so a stale number is a hard stop, not a silent
   pass.
3. Note the build number here → `__________`. Every test below is against that build.
4. Install on the device and **launch once** before testing anything, so the first-run cache exists.

While TestFlight processes (~5–30 min), do Step 1.

---

## Step 1 — Baseline, before you change anything

On `admin.golfin.world`, record the starting state so you can put it back:

| Catalog | version | enabled |
|---|---|---|
| clubs | | |
| characters | | |
| items | | |
| bags | | |
| balls | | |
| texts | | |
| shop_catalog | | |

Also note: the tester account you will use, and its current club/character count.

---

## Step 2 — Content overlay (Phases 1–2)

### 2.1 A text change reaches the game
1. Texts panel → find `BTN_START` (or any visible string) → edit the English value to something
   obviously different.
2. Review & publish. Note the new version.
3. Wait 60 s → launch → quit → launch.
4. **Expect:** the new string on screen. **If not:** check you quit fully (not backgrounded).

### 2.2 A club stat change reaches the game
1. Clubs panel → filter to a club the tester **owns** → change `basePower` by an obvious amount.
2. Publish → wait 60 s → launch → quit → launch.
3. **Expect:** the new value in the Clubs screen.

### 2.3 The clamp fires (the one that touches saved data)
1. Publish `maxDurability` **below** the current durability of an owned club (e.g. 60 → 40).
2. Two launches.
3. **Expect:** durability shows `40/40`, and the log carries a clamp line naming id/field/old/new.
4. **Expect:** it stays clamped after another relaunch — the clamp was persisted, not just displayed.

### 2.4 Deactivate ≠ delete
1. Set an **owned, equipped** club to `is_active = false`. Publish. Two launches.
2. **Expect:** gone from the Shop; **still in the bag with its real art**; **still equipped**.

### 2.5 The per-catalog kill (the one that was broken until yesterday)
1. Disable **`texts`** only. Wait 60 s. Two launches.
2. **Expect:** text reverts to the bundled strings **and clubs/characters keep their overlay**.
3. ⚠️ If *everything* reverts, the `content_kill_switch_and_order` fix is not in this build — stop
   and check the build number.
4. Re-enable `texts`. Two launches. **Expect:** the overlay returns.

### 2.6 The global kill
1. Set `content_settings.content_enabled = false` (button if item 2 of the cleanup landed;
   otherwise SQL). Two launches.
2. **Expect:** every catalog on bundled content.
3. Set it back. Two launches. **Expect:** everything returns.

### 2.7 Offline is a designed path
1. Airplane mode → force quit → launch.
2. **Expect:** the game runs on the cached overlay, no error dialog, one warning in the log.
3. Delete the app, reinstall, **stay in airplane mode**, launch.
4. **Expect:** bundled content, game fully playable, no exception.

### 2.8 Boot cost on real hardware
Time cold launch → Home, three times, and compare against Editor numbers (49.82 ms baseline →
102.80 ms worst case with a full 799-row clubs payload; 0.17 ms at cursor parity). The 40 ms clubs
parse is the **fresh-install** path only — a warm device should be near baseline.

---

## Step 3 — Inventory (Phase 4). Requires being SIGNED IN.

Content needs no auth; inventory does. If the tester is signed out, nothing here syncs.

### 3.1 Push and restore — the headline
1. Signed in, play a little: buy or level something so the inventory differs from a fresh account.
2. **Background the app** and wait ~30 s (the write-behind window), then quit.
3. Delete the app. Reinstall. Sign in with the same account.
4. **Expect:** clubs, characters, levels and items come back.
5. **Expect NOT:** RP restored from the blob — RP is server-owned and comes from the ledger.

### 3.2 A grant applies exactly once
1. Admin → Users → the tester → Inventory tab → issue a grant (e.g. 3 repair kits).
2. Relaunch. **Expect:** applied.
3. Relaunch again. **Expect:** **not** applied a second time.
4. If it never arrives: relaunch a third time before treating it as a bug (see the warning at top).

### 3.3 The notice is present
Inventory tab shows the red **"not server-enforced"** banner above any data, EN and JA. It is
deliberate UI, not decoration — confirm a later redesign has not dropped it.

---

## Step 4 — Put it all back

- Every catalog to its Step 1 version and enabled state. Rollback moves **forward** (restoring v3
  produces v9) — that is correct, not a bug.
- `content_enabled` back to true.
- Reactivate the club from 2.4.
- Confirm `export --check` is clean, so the repo CSVs and the catalogs still agree.

---

## Results

| # | Test | Pass | Notes |
|---|---|---|---|
| 0a | playlife deployed, new wire shape confirmed | ✅ | done 2026-08-26 |
| 0 | Build uploaded, number recorded | | |
| 2.1 | Text change reaches the game | | |
| 2.2 | Club stat change reaches the game | | |
| 2.3 | Clamp fires and persists | | |
| 2.4 | Deactivated club: out of shop, still in bag, still equipped | | |
| 2.5 | Per-catalog kill hits ONE catalog | | |
| 2.6 | Global kill hits all | | |
| 2.7 | Airplane mode, warm and cold | | |
| 2.8 | Boot cost acceptable | | |
| 3.1 | Wipe → reinstall → inventory returns | | |
| 3.2 | Grant applies exactly once | | |
| 3.3 | "Not server-enforced" notice present | | |
| 4 | Everything restored | | |

**If something fails:** note which of the two relaunches you were on, whether the device had network,
and the build number. Those three answers explain most failures before anything interesting does.
