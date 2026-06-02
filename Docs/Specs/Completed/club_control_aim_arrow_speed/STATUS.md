CLOSED — ALREADY IMPLEMENTED (not built)

2026-06-02. Architect investigation found the SPEC premise invalidated by live code.
ClubControl already drives the aim arrow and more, in ShotController.TickArrow():
  - arrow oscillation speed: arrowHz = BaseArrowSpeedHzAtCC0 (0.5) + cc * ArrowSpeedHzPerCC (0.025);
    across the ~0-50 CC range that is 0.5Hz -> 1.75Hz (3.5x swing) — perceptible, not a no-op.
  - clean passes before degradation: cleanPasses = MaxCleanPassesAtCC0 + cc * CleanPassesPerCC;
    higher CC = more forgiving aim windows before _degradationYawRad accumulates.

ClubControl is a no-op ONLY in the resolver layer (StatModifierResolver -> AimConeReductionFraction),
which is the single layer the stat_to_physics_mapping_audit measured. The gameplay layer (ShotController,
via ControlsConfig) already makes CC matter. SPEC would have (a) duplicated the existing line-296 coupling
and (b) sourced from AimConeReductionFraction, a resolver output that is computed but never consumed
(cone width comes from HalfConeAngleRad() lerp on Club.Accuracy/120, not the resolver).

FOLLOW-UPS spun out:
  1. Polarity verify (Cesar manual, in-play): higher CC -> faster arrow = harder to time. Confirm the
     net of {faster arrow + more clean passes + narrower cone via Accuracy} reads as a REWARD, not a
     punishment. If it feels backwards, open a small CC polarity/tuning spec.
  2. Vestigial resolver output: AimConeReductionFraction computed-but-unused -> filed P-003 in POLISH_BACKLOG.md.
