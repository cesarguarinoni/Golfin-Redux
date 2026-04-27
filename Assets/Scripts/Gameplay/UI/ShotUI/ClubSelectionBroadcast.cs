using System;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Static bus for club selection changes.
    // PhysicsLabController (in Golfin.Physics.Viewer) raises this; UI widgets subscribe.
    // Avoids a circular asmdef dependency (Viewer already references Gameplay.UI).
    public static class ClubSelectionBroadcast
    {
        public static int CurrentIndex { get; private set; }

        public static event Action<int> OnClubChanged;

        public static void Raise(int index)
        {
            CurrentIndex = index;
            OnClubChanged?.Invoke(index);
        }
    }
}
