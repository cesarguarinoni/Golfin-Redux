# CESAR_REJECTION — club_bag_wedge_default (Order 761)

**Rejected at:** ARCHITECT_REVIEW_PASS (post-red-team), 2026-07-20
**Rejected by:** Cesar (on sight, from the surfaced bot capture)

## Defect

> "Wedge is using the Driver icon in the selection button instead of a wedge."

Confirmed by orchestrator from `screenshots/stroke5_wedge_approach.png` (cropped club button):
the bottom-right club-selection button reads **P. WEDGE** but renders the **G&F driver portrait**
AND **"250 yrds"** (the driver's `baseDistance`; the wedge's is 120). Label = wedge, icon + yards = driver.

## Root cause (already diagnosed — implement this, do not re-derive)

`Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`, the LIVE-path club-sync block (lines ~777–783):

```csharp
var entry = bag[bagIdx];
ClubContext.SelectedClubId    = entry.ClubId;
ClubContext.SelectedIndex     = bagIdx;
ClubContext.SelectedTypeLabel = entry.TypeLabel;
ClubContext.RaiseSelectedChanged();
```

It sets ClubId / Index / **TypeLabel** but **omits `SelectedPortrait` and `SelectedDistance`**.
`ClubButtonWidget.Refresh()` reads `ClubContext.SelectedPortrait` (→ falls back to nothing / stays on the
index-0 driver portrait) and `ClubContext.SelectedDistance` (stays 250). So the label repaints on a bot club
switch but the icon + yards stay stale on the driver.

The real-player path (`ClubContextPopulator.SelectByIndex`, lines 85–95) sets **all five** fields
(ClubId, TypeLabel, **Distance**, **Portrait**, Index) from the same `ClubEntry`. The bot sync is an
incomplete mirror of it. `ClubEntry` already carries `.Portrait` and `.Distance` (populated by the
populator from `ClubDatabaseCSV` — the wedge portrait `WedgeP-RoyalSwing.png` exists and imports as a
Sprite; the CSV row is correct — nothing to fix in data/assets).

## Fix (surgical, ~2 lines)

Add the two missing field assignments to the bot sync block so it fully mirrors `SelectByIndex`:

```csharp
var entry = bag[bagIdx];
ClubContext.SelectedClubId    = entry.ClubId;
ClubContext.SelectedIndex     = bagIdx;
ClubContext.SelectedTypeLabel = entry.TypeLabel;
ClubContext.SelectedPortrait  = entry.Portrait;   // ← ADD: was leaving driver icon on wedge
ClubContext.SelectedDistance  = entry.Distance;   // ← ADD: was leaving "250 yrds" on wedge
ClubContext.RaiseSelectedChanged();
```

**SPEC compatibility:** the SPEC says "Do NOT change BotDriver's LIVE-path `ClubContext.SelectedClubId`
sync — that mechanism is correct." This fix does NOT change the swing-resolution mechanism (the provider
still resolves the swing club from `SelectedClubId`). It only completes the two HUD-display fields the
sync always should have set to mirror the real player. This is honoring the SPEC intent, not violating it.
Note it explicitly in the report under a "Spec deviations / clarifications" line.

This bug predates Order 761 (the partial sync came from Order 731, 2026-07-17), but Order 761 is what put
the wedge in the bag and made the bot switch to it — surfacing the defect on this order's own video gate.
Fixing it here is correct.

## Re-shoot requirement (Rule 15 — hook-enforced)

`IMPLEMENTER_REPORT.md` must carry a `## Rejection follow-up` section with:
- A GONE/RESOLVED verdict for "wedge shows driver icon + wrong yards".
- A **same-angle, full-res** club-button screenshot from a fresh bot playthrough where the bot is on the
  **P. WEDGE**, showing the **wedge portrait** and the wedge's **yards (120-ish / ~110 mts)** — not 250.
- Re-run the Hole 1 ≤7-stroke bot video (the completability gate still applies; don't regress it).

Also spot-check the **iron7 and putter** buttons render their correct icons after the fix (same bug would
have shown a driver icon on every non-driver club the bot selected) — confirm in the report.

---

# CESAR_REJECTION #2 — capture method (flipped frames), 2026-07-20

> "You are back capturing wrong with flipped frames from time to time. Stop it and use the
> sanctioned capture method." — Cesar (flip ACCEPTED as present; do NOT re-verify to prove it exists)

## Root cause (diagnosed by orchestrator — do not re-derive)

The iter-2 canonical video `videos/hole1_playthrough_iter2.mp4` was recorded with **immediate**
`BotVideoRecorder.Arm()` → `Begin()` fires at **EnteredPlayMode**, so the recorded window spans the entire
app-boot sequence (splash → GOLFIN Invitational title/PLAY/LOGIN gate → hole load). Unity Recorder's
**`GameViewInputSettings`** capture on Mac/Metal flips frames whenever the render target is recreated —
which happens repeatedly during those scene loads. Result: flip bursts "from time to time" (the
`map_view_aiming` flip class — see memories `reference_botvideorecorder_yflip_fix`,
`reference_video_flip_verification`). Confirmed by: the video literally contains the boot/title frames.

## Sanctioned fix — deferred-start recording (the pattern already in this repo)

Record via the **DEFERRED-START** mechanism the audio scenarios already use — `AudioGameplayShotsV3`
(`Scenarios.cs:2265`) and `AudioPuttToCup` (`Scenarios.cs:2370`):
1. `ArmDeferred()` from the menu (sets `DeferredRecord`, so `Begin()` is a no-op at EnteredPlayMode).
2. Bot navigates the real flow to Hole 1, `WaitForSceneLoaded(...)`, then a **~4s settle** ("fade-in + HUD
   fully rendered; avoids Y-flip").
3. **THEN** `BeginDeferred()` → recording starts on a stable in-hole frame, and the recorded window
   contains **no scene load** → no Metal render-target recreation → **no flip**.
4. Render-pipeline state is already locked before `StartRecording` (committed y-flip fix, intact in
   `BotVideoRecorder.Begin()`) — do NOT mutate vSync/targetFrameRate/GameView size inside the window.

`hole1_playthrough` currently has NO deferred-record wiring — that is the whole bug. Wire it to defer-record
by mirroring the `AudioGameplayShotsV3` block (ArmDeferred menu variant + `BeginDeferred()` after the
hole-load settle). This is copying an existing sanctioned pattern into an existing scenario — it is NOT a
new `*Gate` scenario and must NOT touch the physics core or the approved wedge-feature files.

## Verification (MANDATORY, sanctioned method only)

Prove flip-free by **CONSECUTIVE-frame decode across the WHOLE clip**, never `ffmpeg -ss` keyframe sampling
(it misses flip bursts — `reference_video_flip_verification`):
`ffmpeg -i in.mp4 -vf "select='between(n,A,B)',tile=8x4" -vsync 0 out.png` over sequential windows, or a
per-frame top/bottom `signalstats` scan, or watch the clip. A SINGLE flipped frame anywhere = redo.
The video must also NOT contain the splash/title/boot frames — it starts on a loaded Hole 1 with the HUD up.

## Still required from iter-2 (unchanged, do not regress)

- The wedge-icon fix stays (P. WEDGE shows wedge portrait + 120 yrds).
- Hole 1 completes in ≤7 REAL strokes on the new clip, ending on a real InCup, `ForceShotComplete` grep = 0.
- NO changes to the approved wedge-feature files (ClubManager / SaveData / SaveSchemaMigrator / ClubOwnership
  / tests) and NO physics-core edits. Only capture-tooling (Scenarios.cs deferred wiring + menu) + re-record.
