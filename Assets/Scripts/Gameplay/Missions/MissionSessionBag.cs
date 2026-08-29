#nullable enable
using System.Collections.Generic;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// The TRANSIENT bag a mission plays with. Spec: missions_v1 §B3.
    ///
    /// ⚠️ THIS IS NEVER WRITTEN TO SAVEDATA, AND THAT IS THE ENTIRE POINT. A mission that
    /// hands the player a supplied set of Common clubs must not leave them owning those clubs,
    /// and a mission that bans their wedges must not leave the wedges unequipped when they go
    /// back to Practice. `PlayerClubData.equippedBagSlot` — the real source of truth, owned by
    /// ClubManager — is not touched at any point. This is a stack that sits IN FRONT of it for
    /// the duration of one hole.
    ///
    /// WHY A STATIC IN A LEAF ASSEMBLY. `BagManager` lives in Assembly-CSharp, which every
    /// leaf assembly is visible to but which no leaf assembly may reference back. So the state
    /// lives here, where the mission code can write it, and `BagManager.GetClubsInBag` READS it
    /// — one direction, no cycle, and the club selector needs no change at all because it
    /// already asks BagManager.
    ///
    /// IT IS A STACK, NOT A SLOT, so a push that is never popped is a bug that shows up as an
    /// unbalanced depth rather than as silently-lost state. `Clear()` is what a session reset
    /// calls: at that point balance no longer matters, only that nothing survives.
    /// </summary>
    public static class MissionSessionBag
    {
        private static readonly List<IReadOnlyList<string>> _stack = new List<IReadOnlyList<string>>();

        /// <summary>The club ids in force, or null when no mission is overriding the bag.</summary>
        public static IReadOnlyList<string>? Current
            => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public static bool IsActive => _stack.Count > 0;

        /// <summary>Depth of the stack. A balanced session leaves this at 0.</summary>
        public static int Depth => _stack.Count;

        /// <summary>Raised on every push/pop so a live club selector re-reads its list.</summary>
        public static event System.Action? OnChanged;

        public static void Push(IReadOnlyList<string> clubIds)
        {
            _stack.Add(clubIds ?? new List<string>());
            OnChanged?.Invoke();
        }

        public static void Pop()
        {
            if (_stack.Count == 0) return;
            _stack.RemoveAt(_stack.Count - 1);
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Drop everything. Called by <c>GameSession.ResetSession()</c> via
        /// <see cref="MissionSession.Clear"/> — a teardown must not leave a supplied bag
        /// standing, whatever went wrong on the way out.
        /// </summary>
        public static void Clear()
        {
            if (_stack.Count == 0) return;
            _stack.Clear();
            OnChanged?.Invoke();
        }
    }
}
