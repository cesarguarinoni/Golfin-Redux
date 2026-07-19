# versus_bot_club_resolution_audit

> **Status:** Queued — actionable after Order 761 lands (or in parallel; see Dependency).
> **Order:** 762 (Notion GOLFIN_Roadmap) — Phase "Loop v2", P2 — Medium
> **Tier:** 3 — FULL PIPELINE (runtime club-resolution behaviour + 1v1 gate)
> **Filed:** 2026-07-17 11:30 JST (Architect)
> **Pairs with:** Order 761 `club_bag_wedge_default` (the solo-bag half).

---

## One-line

Determine whether the 1v1 `VersusBot` actually *fires* the club it *selects* on the LIVE stat path — and if
it silently substitutes (the way `BotDriver` did before its `ClubContext` sync), give it the same fix.

---

## Why this exists (Cesar: "check all bots — playing multiplayer bots too — have a working bag")

`VersusBot` (`Assets/Scripts/Physics/Viewer/VersusBot.cs`) is wedge-aware **in its decision logic**:

- `SelectShotCalibrated` picks **club index 2 (wedge)** for 20–80m approaches.
- Its off-green guard (H3b) overrides to **wedge** (`club = 2`).
- `bot_clubs.csv` carries a full 21-row **wedge** carry curve it interpolates against.

**But the firing path is suspect.** VersusBot fires via the LIVE bus — it calls `_shotController.
ClearStatBundleOverride()` (so the provider resolves live stats) and `_controller.SetClub(club)`. It **never
sets `ClubContext.SelectedClubId`.** Per `BotDriver`'s own hard-won comments (and `LiveStatProviderHost.cs:188`):

> `SetClub` only updates the LAB club index + cone/putter UI. On the LIVE stat path the provider resolves the
> SWING club from `ClubContext.SelectedClubId` — which `SetClub` never touches.

`BotDriver` had exactly this bug: it selected a club but fired the equipped driver every stroke until it was
fixed by explicitly pushing `ClubContext.SelectedClubId`/`SelectedIndex`/`RaiseSelectedChanged()`. **VersusBot
does none of that.** So VersusBot may be selecting a wedge and firing whatever `ClubContext.SelectedClubId`
last held — i.e. silently substituting the wrong club in live 1v1 matches. That is a "the multiplayer bot
does not have a working bag" bug, latent because the 1v1 flow may populate `ClubContext` elsewhere, or because
the substituted club still roughly reaches the target and nobody noticed.

**This is a MEASURE-FIRST order. Do not assume the bug is real, and do not assume it is not.**

---

## Dependency on 761

Soft. The measurement is cleanest once 761 has put a wedge in the equipped bag (so a correct resolution has a
wedge to resolve TO). If 761 hasn't landed, the audit still runs — it just measures against the wedge-less bag
and the "does SetClub(2) fire a wedge" question becomes "does it fire the nearest available club" — still
diagnostic. Recommend running after 761 for an unambiguous result.

---

## Stage 1 — MEASURE (no code change)

Instrument a 1v1 bot shot where `SelectShotCalibrated` chooses club 2 (an approach in the 20–80m band).
Capture, for that shot:

1. The **selected** club index + label (already logged: `[VersusBot] TakeShot: … {label}`).
2. The **actually fired** swing club — read what `LiveStatProviderHost` resolved for the shot
   (`ClubContext.SelectedClubId` at fire time, and the club the provider built the swing bundle from). Add a
   temporary diagnostic log at the resolution site if none exists.
3. Whether they **match**.

Run it in the real 1v1 flow (`VersusMatchController` → `VersusBot.TakeShot`), not the lab. Determine who — if
anyone — populates `ClubContext.SelectedClubId` for the bot player during a 1v1 match. Two outcomes:

- **They match** → VersusBot already fires the club it selects (something populates ClubContext for it).
  Document why, close the order as **verified-correct, no code change**. This is a legitimate and cheap
  outcome — the same shape as the strength_velocity (415) measurement that shipped nothing.
- **They diverge** → confirmed silent substitution → Stage 2.

---

## Stage 2 — FIX (only if Stage 1 shows divergence)

Mirror `BotDriver`'s proven fix: before firing, push the selected club into `ClubContext` so the LIVE provider
resolves the intended club:

```csharp
ClubContext.SelectedClubId    = <resolved clubId for the selected lab index>;
ClubContext.SelectedIndex     = <bag index>;
ClubContext.SelectedTypeLabel = <type label>;
ClubContext.RaiseSelectedChanged();
```

Resolve the lab club index → real `clubId` via the bot player's equipped bag (`ClubContext.EquippedBag`),
using the same nearest-available fallback `BotDriver` uses for clubs the bag lacks. **Do not duplicate**
BotDriver's block by copy-paste if it can be shared — consider lifting the resolution into a small shared
helper both bots call (evaluate; don't force it if the asmdef boundary makes it costly — Lesson W).

Note VersusBot is **production** (`no #if UNITY_EDITOR`, drives the real 1v1). Any change must be
production-safe and must not regress the 2b difficulty error-injection, H2 layup, or H3 slope logic that run
after club selection.

---

## Hard gates

1. **1v1 completes cleanly** — a full bot-vs-bot (or player-vs-bot) match on a Lomond hole, bot-recorded
   video, no fall-through / stuck-recovery loops.
2. If Stage 2 fires: the bot visibly plays a **wedge** on a short approach in the video (the whole point).
3. Difficulty/H2/H3 behaviour unchanged — regression-check the post-selection pipeline.
4. Tests at or above baseline.

---

## Traps

- **Do not assume the outcome.** If Stage 1 shows a match, shipping a Stage-2 fix anyway would be inventing a
  problem — the 415 lesson in reverse.
- **Lesson W** — sharing the resolution helper across `Golfin.Physics.Viewer` (both bots' asmdef) is fine;
  reaching into Assembly-CSharp is not. `ClubContext` lives in `Golfin.Gameplay.UI` — both bots already use it.
- VersusBot has three static tables (`_carryTable`, `_difficultyTable`) with domain-reload sentinels — don't
  disturb the `-1` sentinel pattern.

---

## Out of scope

- Solo bot / default bag (Order 761).
- Tournament field bots (statistical, no bag).
- Difficulty tuning, H2/H3 behaviour — audit is club-resolution only.

---

## Definition of done

1. Stage 1 measurement documented: selected vs fired club, and who populates `ClubContext` for the 1v1 bot.
2. Either a reasoned **no-change** close (match) or a landed Stage-2 fix (divergence).
3. If fixed: 1v1 video shows the bot playing its real bag incl. a wedge approach.
4. Tests green; difficulty/H2/H3 unregressed.
5. Cesar-approved; Notion 762 Done (or Deferred if verified-correct-no-change) + Closed.
