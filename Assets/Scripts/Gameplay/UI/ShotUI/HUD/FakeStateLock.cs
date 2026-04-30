namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Runtime flag set by editor-side fake-state helpers to prevent runtime populators
    /// (PlayerContextPopulator, BallContextPopulator, ClubContextPopulator, etc.) from
    /// overwriting injected context values during editor capture sessions.
    ///
    /// USAGE — populators:
    ///     void Refresh()
    ///     {
    ///         if (FakeStateLock.IsLocked) return;
    ///         // ...normal sync logic...
    ///     }
    ///
    /// USAGE — editor (CaptureHelper.cs):
    ///     FakeStateLock.IsLocked = true;
    ///     // populate contexts, capture screenshot
    ///     // (lock stays on for the rest of the session — toggle off via menu or scene reload)
    ///
    /// Lives in runtime asmdef so populators can reference it. Default = false; production
    /// playmode is unaffected unless something explicitly sets it.
    /// </summary>
    public static class FakeStateLock
    {
        public static bool IsLocked = false;
    }
}
