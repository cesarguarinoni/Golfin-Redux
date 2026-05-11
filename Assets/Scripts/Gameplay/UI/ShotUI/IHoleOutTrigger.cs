namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d: implemented by HoleCompleteDriver. Exposes the ShowForDebug() entrypoint
    /// so DebugShotPanel can trigger the result screen without a circular asmdef dependency.
    /// (Golfin.Gameplay.UI cannot reference Golfin.Physics.Viewer — circular.)
    /// </summary>
    public interface IHoleOutTrigger
    {
        void ShowForDebug();
    }
}
