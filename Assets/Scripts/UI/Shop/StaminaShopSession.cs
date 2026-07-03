// Assets/Scripts/UI/Shop/StaminaShopSession.cs
// Order 517 — stamina_boost_shop
// Lightweight static session state shared between Selection → Detail navigation.
// Mirrors the pattern of TournamentService.SelectedTournamentId.

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Holds the selected shop id while navigating from Selection → Detail.
    /// Reset to empty on return to Selection.
    /// </summary>
    public static class StaminaShopSession
    {
        /// <summary>Id of the shop the player tapped on the Selection screen.</summary>
        public static string SelectedShopId { get; set; } = string.Empty;

        /// <summary>Character id for whom the boost is being purchased (from CharacterManager.SelectedCharacterId).</summary>
        public static string SelectedCharacterId { get; set; } = string.Empty;
    }
}
