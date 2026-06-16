DONE

# STATUS — `sound_effects` (Order 350)

- **State:** DONE — Cesar approved 2026-06-16. Folder moved to `Docs/Specs/Completed/`.
- **Delivered:** complete audio pass — `Golfin.Audio.Events` leaf asmdef (SfxBus/SfxId), `GolfinAudio.mixer` (dB routing + PlayerPrefs migration), `SfxLibrary` + `SfxPlayer`, per-bounce/settle/cup via additive `BallAnimator.OnHit` + `BallAudioEmitter`, swing+hit at contact, match/RP/level stingers, menu music, 349 splash/tap migrated to the bus. 35/35 EditMode audio tests green; all 25 SfxLibrary clip GUIDs tracked.
- **Cesar review rounds (2026-06-16):** hit-mix rebalanced so the hit is audible over the swing; `Hit_Strong`/`Hit_Putt` clips boosted; putt rolling-stop no longer thuds (IsPutt suppression). Feature committed in `c47f02ac`.
- **Fidelity videos** (gitignored, local): UI+music-slider, gameplay swing/hit/land, water splash. Copied to `Docs/Reports/Media/` for the daily report.
- **Architect follow-ups (out of scope, see `ARCHITECT_FOLLOWUP.md`):** Hole-4 fringe pass-through + terrain fall-through into a Hole-4 bunker (physics/terrain, not audio). Match-result-modal didn't render in the forced-completion stinger clip; putt-to-cup video came out as a drive — both verifiable live, deferred as polish.
