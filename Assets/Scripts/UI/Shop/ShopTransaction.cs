// Assets/Scripts/UI/Shop/ShopTransaction.cs
// Order 517 — stamina_boost_shop
// Reusable purchase seam.  Order 610 (general Shop UI) will reference this same class.

using System;
using UnityEngine;
using Golfin.Roster;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Stateless purchase seam that orchestrates RP spend → stamina grant → persist.
    /// Designed to be called from StaminaShopDetailScreenController.
    ///
    /// Returns a <see cref="PurchaseResult"/> describing success or the failure reason,
    /// so callers can show the appropriate Toast without knowing the internals.
    ///
    /// Order 610 note: when the general Shop needs to sell non-stamina items,
    /// add an overload or extend PurchaseResult.GrantType.
    /// </summary>
    public static class ShopTransaction
    {
        public enum PurchaseResult
        {
            Success,
            InsufficientRp,
            StaminaFull,
            NullCharacter
        }

        /// <summary>
        /// Attempts to buy a stamina boost item for the given character.
        /// <list type="bullet">
        ///   <item>Checks stamina-full guard (blocks if currentEnergy >= maxEnergy).</item>
        ///   <item>Calls <see cref="RewardPointsManager.SpendPoints"/> — fails fast on insufficient RP.</item>
        ///   <item>Calls <see cref="StaminaRuntimeService.AddEnergy"/> — clamps to max, persists.</item>
        ///   <item>Invokes <paramref name="onGranted"/> on success.</item>
        /// </list>
        /// </summary>
        /// <param name="pcd">Character whose Condition pool receives the boost.</param>
        /// <param name="rpCost">RP cost from CSV (item.RpCost).</param>
        /// <param name="staminaAmount">STA units to add (item.Stamina).</param>
        /// <param name="onGranted">Optional callback invoked AFTER the grant (UI refresh hook).</param>
        public static PurchaseResult TryPurchase(
            PlayerCharacterData pcd,
            int rpCost,
            float staminaAmount,
            Action onGranted = null)
        {
            if (pcd == null)
            {
                Debug.LogWarning("[ShopTransaction] TryPurchase: pcd is null.");
                return PurchaseResult.NullCharacter;
            }

            // Stamina-full guard (D5)
            if (pcd.currentStaminaEnergy >= pcd.maxStaminaEnergy)
            {
                Debug.Log("[ShopTransaction] TryPurchase: stamina already full.");
                return PurchaseResult.StaminaFull;
            }

            // RP spend (SpendPoints does NOT touch lifetime-earned)
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || !rpm.SpendPoints(rpCost))
            {
                Debug.Log(string.Format("[ShopTransaction] TryPurchase: insufficient RP (need {0}).", rpCost));
                return PurchaseResult.InsufficientRp;
            }

            // Grant stamina
            StaminaRuntimeService.AddEnergy(pcd, staminaAmount);

            onGranted?.Invoke();
            return PurchaseResult.Success;
        }
    }
}
