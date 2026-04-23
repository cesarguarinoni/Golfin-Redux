namespace Golfin.Gameplay.Input
{
    public readonly struct ShotInputState
    {
        public readonly ShotState State;
        public readonly float     PowerNormalized;       // 0..1.2
        public readonly float     ConeFinetuneX;         // -1..+1
        public readonly float     ArrowProgress01;       // 0..1, current pass
        public readonly int       PassIndex;
        public readonly bool      IsDegrading;
        public readonly bool      IsPutt;
        public readonly float     AimYawRadians;
        public readonly float     CameraHeadingRadians;

        public ShotInputState(ShotState state, float power, float coneFinetune,
                              float arrowProgress, int passIndex, bool isDegrading,
                              bool isPutt, float aimYaw, float cameraHeading)
        {
            State               = state;
            PowerNormalized     = power;
            ConeFinetuneX       = coneFinetune;
            ArrowProgress01     = arrowProgress;
            PassIndex           = passIndex;
            IsDegrading         = isDegrading;
            IsPutt              = isPutt;
            AimYawRadians       = aimYaw;
            CameraHeadingRadians = cameraHeading;
        }
    }
}
