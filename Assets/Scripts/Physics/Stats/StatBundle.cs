using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    /// <summary>
    /// Everything the resolver needs for one shot. Either Club or Putter is set, not both.
    /// CurrentStamina is the live value (0..MaxStamina), not the cap.
    /// MaxStamina is the character's effective stamina pool at the current rarity/level.
    /// </summary>
    public readonly struct StatBundle
    {
        public readonly ClubStats?    Club;
        public readonly PutterStats?  Putter;
        public readonly BallStats     Ball;
        public readonly CharacterStats Character;
        public readonly fp CurrentStamina;
        public readonly fp MaxStamina;

        public bool IsPutt => Putter.HasValue;

        public StatBundle(ClubStats club, BallStats ball, CharacterStats character,
                          fp currentStamina, fp maxStamina)
        {
            Club           = club;
            Putter         = null;
            Ball           = ball;
            Character      = character;
            CurrentStamina = currentStamina;
            MaxStamina     = maxStamina;
        }

        public StatBundle(PutterStats putter, BallStats ball, CharacterStats character,
                          fp currentStamina, fp maxStamina)
        {
            Club           = null;
            Putter         = putter;
            Ball           = ball;
            Character      = character;
            CurrentStamina = currentStamina;
            MaxStamina     = maxStamina;
        }
    }
}
