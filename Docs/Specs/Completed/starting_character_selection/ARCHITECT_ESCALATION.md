# ARCHITECT ESCALATION — `starting_character_selection`

**STATUS: `ARCHITECT_REVIEW_ESCALATE`** — circuit breaker tripped (PIPELINE_HARDENING rule 1).
**Shape `starter_selection:visual_evidence` has now failed 3 times (iter-2, iter-3, iter-4).**
iter-4 self-reported "All items PASS" and set `READY_FOR_SELF_REVIEW`; the architect overrode that
after reading the artifacts — blockers 1, 2, 3 and 5 below are all visible in files the report cites
as passing.
No iter-5 of this shape may run without Cesar's decision.

**Architect:** orchestrator, 2026-08-24

## What is actually finished

Verified from primary sources and from the recorded video, not from the report:

- **Localization race FIXED (F-A).** Every key resolves in the clip: レベルアップ, ブースト, バイオ,
  比較, 選択, and the full instruction band copy. This was the blocker that made iter-3 unreviewable.
- **D1 fixed** — starter candidates render unlocked, COMPARE + gold SELECT present, LEVEL UP/BOOST
  disabled-but-present (A2 exactly).
- **A1 confirmed live** — James コモン Lv 10/39 at 7/25, 6/25, 5/18, 7/22.
- **Locked cards** use Cesar's cover sprite with the ロック label.
- **Real 1170×2532 video** exists: `videos/demo.mp4`, 35.5s, 1066 frames, upright, no Y-flip.
- **Cesar's save is intact and CORRECT:** schemaVersion 10, `starterCharacterId='char_james'`,
  1 of 4 persisted characters flagged `isOwned`.

## Blocker 1 — CRITICAL: locked characters are NOT locked in the Roster. Root cause found.

Frame at t=34s of `videos/demo.mp4` shows the post-selection Roster with **Elizabeth (R Lv80), Shae
(L Lv160), Camila (R Lv80), Guillermo (M Lv120) and Freda (S Lv200) all fully unlocked** — full
colour, no ロック label, no dim cover. The caption over that exact frame reads *"Roster — James
owned, other characters locked until earned."* The clip narrates the opposite of what it shows.

**Root cause (high confidence, one line):** the save persists only **4** characters — the ones the
player has touched. The other 8 catalog characters have no `PersistedCharacter` entry, so
`CharacterManager` hydrates them straight from CSV, and the runtime object keeps the field default:

```csharp
// PlayerCharacterData.cs:54
public bool isOwned = true;   // ← the original dead-code default, never changed
```

Every character without a persisted row therefore defaults to **owned**. The migration is correct,
the save is correct, the detail-panel code is correct — the default is wrong.

**Fix:** hydration must default `isOwned = false` for any character with no persisted entry, with
ownership granted only by a persisted `isOwned == true` or by `GrantStarter`. Flip the field default
to `false` and audit every construction site for a place that relied on the old `true`.

This also explains why iter-1 and iter-2 could never observe locked cards.

## Blocker 2 — the video narrates a flow it does not show

The confirm modal **never opens** anywhere in the clip. At t=19s the caption reads *"Confirm your
starting character"* and at t=24s *"Confirming James as starting character"* — both frames show the
unchanged starter screen. The recorder wrote a caption track describing the intended flow rather
than the flow it captured.

This is the same class of defect as iter-1's fabricated screenshot: an artifact asserting something
the pixels do not support. It is not a deliberate fabrication — the captions were authored ahead of
the recording — but the effect on a reviewer is identical, and it must not ship as evidence.

## Blocker 3 — caption rendering is broken

Captions overflow the 1170px frame and are cut off at BOTH edges — "nfirming James as starting
charac", "ther characters locked until earne". They are not wrapped or fitted to the frame
(`feedback_caption_videos_unobtrusively`).

## Blocker 4 — RESOLVED: EN evidence now exists

The 8 stills were re-shot at 19:15–19:22 and the EN states genuinely render English
(`state4_en_confirm_modal.png` shows "YOU ARE STARTING THE GAME WITH:", BACK, CONFIRM, COMPARE,
SELECT, LOCKED). The confirm modal DOES open and work — the video's failure to show it is a
recorder-flow problem, not a broken modal.

## Blocker 5 — confirm-modal Figma fidelity (new, from `state4_en_confirm_modal.png`)

The modal opens but does not match node `13924:42328`:

- **The bottom instruction copy is duplicated INSIDE the modal**, rendered under/over the button row
  ("CHOOSE YOUR STARTING CHARACTER. YOU WILL BE ABLE TO ACQUIRE…"). Per the node the modal contains
  only: title, character name, separator, BACK, CONFIRM. That string belongs solely to the bottom
  band. Content leaking in from the wrong source.
- **The mid separator (`13924:42335`, 882-wide at y=195) is missing.**
- **Padding/proportion is off** — the title sits almost flush to the panel's top edge; the node has
  32px top padding, title 882×47 at y=32, name 882×76 at y=63, buttons row at y=227 in a 978×379
  panel.
- **Title vs name type scale looks wrong** — node is 40px title / 64px name (TMP 33.33 / 53.33).
  A/B the rendered cap-heights against `reference/node_13924-42328_confirm_modal.png`, not the
  divisor arithmetic.

Note the report's Rule 21 lint claims `fail=0, warn=10` and dismisses the warnings as "pre-existing:
flat-fill dim overlays + unlocalized-text warnings acceptable". Flat-fill dim overlays are precisely
the fabrication class this task was rejected for twice. Those warnings need reading, not waiving.

## Minor

- Caption says **"Olivia Rivera"**; her CSV `lastName` is **Guarinoni**.

## Two shared-system fixes were pulled into this task

Both were pre-existing latent bugs this task exposed, both architect-authorised because the screen
could not ship or be verified without them. Cesar should decide whether they stay here or split out:

1. `FadeController` — generation guard replacing an unsafe `StopCoroutine` that left the overlay
   permanently black on re-entrant navigation.
2. `LocalizationManager` / `LocalizationBootstrap` — init-order race that rendered raw keys on any
   screen activated during boot.

## Decision needed from Cesar

1. **Authorise iter-5?** The circuit breaker blocks it by rule. The blocker-1 fix is a one-line
   default flip plus an audit, and the remaining work is re-recording the video and capturing EN —
   so the odds are materially better than the last three attempts. Recommend: authorise, scoped to
   exactly blockers 1–4.
2. **Keep or split the two shared-system fixes?**
3. **`Assets/Art/RosterScreen/Button - Retry.png`** — stray byte-identical duplicate of
   `Roster Cover.png`, untracked, referenced by nothing. Delete it?
