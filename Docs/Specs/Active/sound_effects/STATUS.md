# STATUS — `sound_effects` (Order 350)

- **State:** SPEC_READY → awaiting implementer kickoff.
- **Tier:** FULL PIPELINE (Tier 3) — new arch (mixer + `Golfin.Audio.Events` asmdef + SFX bus + CSV), one core-file touch (`BallAnimator.OnHit`), audio/device fidelity gate.
- **Spec:** `Docs/Specs/Active/sound_effects/SPEC.md` (authored 2026-06-15, Architect; architecture researched + adversarially checked before lock).
- **Kickoff:** `Use the implementer subagent on "sound_effects"`
- **Notion:** Order 350 → In Progress (2026-06-15).

## Decision log (locked with Cesar 2026-06-15)
1. Trigger = static `SfxBus` enum-event in new leaf asmdef (not move AudioManager). 2. Clip data = `sfx.csv`. 3. AudioMixer done properly up front, single pass (not phased). 4. 2D everywhere. 5. `Golfin_SFX` set canonical, compress→.ogg, rename to convention. 6. Hit by power band; landing per bounce (velocity-gated). 7. Music in scope (`Main Theme`, menus).

## Open NOTEs (impl resolves)
A GameSession result shape · B EarnPoints/level-up event · C ScreenId + button inventory · D clip-binding model · E SurfaceType→Land map fill · F power-band thresholds.

## History
- 2026-06-15 — SPEC authored (Tier 3). Recon: game effectively silent; AudioManager volume-only; splash/tap bypass it via raw PlayClipAtPoint (slider-dead bug); bespoke `Golfin_SFX` asset set maps 1:1 to events; no mixer/no audio asmdef (clean slate). Architecture web-researched (event-bus decoupling + AudioMixer dB routing) + adversarially gated (static-event leak, per-bounce machine-gun, PlayRate=Instant, SetFloat-in-Awake, Log10(0), settings migration, 349 regression, determinism).
