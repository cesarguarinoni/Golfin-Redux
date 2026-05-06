namespace Golfin.Gameplay.Loop
{
    public enum BallState
    {
        Aiming,   // no shot in flight; player can input
        Flying,   // ball is airborne (post-flick, pre-first-ground-contact, AND between bounces)
        Rolling,  // ball is on ground in roll/putt phase
        AtRest,   // ball stopped on a non-OB surface, not in cup
        InCup,    // ball ended inside the cup geometry (per ICupDetector)
        OB,       // ball ended in water / OOB / off the world
    }
}
