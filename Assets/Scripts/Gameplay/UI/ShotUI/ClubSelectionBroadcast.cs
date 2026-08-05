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

        // ── Putt-mode gate (K11 club_selection_green_gate) ─────────────────────
        // PhysicsLabController.EnterPutterMode / ExitPutterMode publish here, for the
        // same asmdef-isolation reason Raise() exists: the selector UI must not
        // reference ShotController, and must not hardcode the putter's lab index.
        // The truth is §2f's GREEN-STRICT classification — putt mode IS "on the
        // green" — so gating off this flag can never disagree with the auto-switch.

        /// <summary>True while §2f has the player in putter mode (ball at rest on Green).</summary>
        public static bool InPutterMode { get; private set; }

        /// <summary>LabClubs index of the putter, published by the lab controller. -1 = not yet published.</summary>
        public static int PutterLabClubIndex { get; private set; } = -1;

        public static event Action<bool> OnPutterModeChanged;

        public static void SetPutterMode(bool inPutterMode, int putterLabClubIndex)
        {
            PutterLabClubIndex = putterLabClubIndex;
            if (InPutterMode == inPutterMode) return;
            InPutterMode = inPutterMode;
            OnPutterModeChanged?.Invoke(inPutterMode);
        }

        /// <summary>
        /// Selector eligibility, aligned to §2f: in putter mode ONLY the putter may be
        /// picked; off the green everything EXCEPT the putter may be. Pure (reads no
        /// statics) so it is unit-testable and is the single rule shared by the
        /// selector's Populate and Scroll paths.
        ///
        /// Fail-open when <paramref name="putterLabClubIndex"/> is negative (nothing
        /// published yet — e.g. no lab controller in the scene): never soft-lock the
        /// selector just because the putt-mode flag was never initialised.
        /// </summary>
        public static bool IsSelectable(int labClubIndex, int putterLabClubIndex, bool inPutterMode)
        {
            if (putterLabClubIndex < 0) return true;
            bool isPutter = labClubIndex == putterLabClubIndex;
            return inPutterMode ? isPutter : !isPutter;
        }
    }
}
