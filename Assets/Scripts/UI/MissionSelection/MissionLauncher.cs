#nullable enable
using Golfin.Gameplay.Missions;
using Golfin.Gameplay.Session;
using Golfin.UI.GameplayTransition;
using UnityEngine;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// The one way a mission round begins.
    ///
    /// Extracted when the Hole Complete modal started offering REPLAY and NEXT MISSION on the
    /// same cards the selection screen uses: a second copy of "Begin, seed, load" would have been
    /// two implementations of the one sequence whose ORDER is load-bearing. MissionSession.Begin
    /// must succeed before GameSession is seeded, or a refused mission would leave the session
    /// pointing at a hole nothing is going to start.
    /// </summary>
    public static class MissionLauncher
    {
        /// <summary>
        /// Start <paramref name="m"/>. Returns false and changes nothing if the card could not
        /// assemble a bag, or if MissionSession refuses it.
        /// </summary>
        public static bool TryStart(MissionDefinition? m, bool isPlayable)
        {
            if (m == null) return false;

            // A card that cannot assemble its bag never starts a round. The button is already
            // non-interactable; this is the second lock, because a disabled button is a UI state
            // and this is a correctness rule.
            if (!isPlayable)
            {
                Debug.LogWarning($"[MissionLauncher] mission {m.Id} is not playable: " +
                                 $"{(MissionCatalog.Warnings.TryGetValue(m.Id, out var w) ? w : "unknown")}");
                return false;
            }

            if (!MissionSession.Begin(m))
            {
                // Begin refuses an empty bag or an unbaked short start, and changes nothing.
                Debug.LogError($"[MissionLauncher] MissionSession refused mission {m.Id}.");
                return false;
            }

            GameSession.SeedSession(m.HoleNumber, GameSession.SelectedCharacterId, GameSession.EquippedBagSlot);

            var loading = Object.FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (loading != null) loading.PrepareForHoleLoad(m.HoleNumber);

            var loader = GameplaySceneLoader.Instance;
            if (loader == null)
            {
                Debug.LogError("[MissionLauncher] GameplaySceneLoader not found.");
                return false;
            }
            loader.BeginGameplayLoad(m.HoleNumber);
            return true;
        }
    }
}
