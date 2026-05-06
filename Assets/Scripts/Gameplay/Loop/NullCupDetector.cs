using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    public sealed class NullCupDetector : ICupDetector
    {
        public bool IsInCup(fp3 position, fp ballRadius) => false;
    }
}
