// Assets/Scripts/UI/Gacha/GachaPullFlow.cs
// gacha_reveal_animation §1 — the single entry point every PULL button routes through.
//
// Before this file the two PULL buttons on a banner card jumped straight to the Prizes
// screen. They now go: Pull(count) → build the result → reveal modal → Prizes screen bound
// to THAT result. One function owns the order, so the reveal and the result screen can never
// disagree about what was pulled.
//
// BuildResult is deliberately the only place the prize list is produced: it is the seam the
// real server pull replaces later (ticket spend / odds / pity / history are all out of scope
// here and blocked on content — see Docs/TellCode.md).
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.UI.Toast;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Static orchestrator for a gacha pull: result → reveal modal → Prizes screen.
    /// </summary>
    public static class GachaPullFlow
    {
        /// <summary>
        /// The prizes of one pull, in reveal order. Today built from
        /// <see cref="GachaMockPrizePool"/>.
        /// NOTE: the real server pull plugs in HERE — this is the one seam for it.
        /// </summary>
        public static IReadOnlyList<PrizeRecord> BuildResult(int count)
            => count == 1
                ? new[] { GachaMockPrizePool.GetX1Prize() }
                : GachaMockPrizePool.GetMockPrizes();

        /// <summary>
        /// Entry point for every PULL button (banner card x1/x10 AND the Prizes screen's
        /// "pull again"). Opens the reveal modal, then the Prizes screen bound to the same
        /// result. Degrades to an immediate Prizes screen when no modal exists in the scene.
        /// </summary>
        public static void Pull(int count)
        {
            var result = BuildResult(count);

            var modal = GachaRevealModalController.Instance;
            if (modal == null)
            {
                Debug.LogWarning("[GachaPullFlow] No GachaRevealModalController in the scene — " +
                                 "skipping the reveal and opening Prizes directly.");
                ShowPrizes(result);
                return;
            }

            modal.Play(result, () => ShowPrizes(result));
        }

        // The reveal calls this at the END of the sequence (or on SKIP), so the Prizes screen
        // binds and activates UNDER the still-opaque scrim and is revealed by the modal's fade.
        private static void ShowPrizes(IReadOnlyList<PrizeRecord> result)
        {
            GachaPrizesScreenController.SetPendingResult(result);

            if (ScreenManager.Instance != null)
            {
                ScreenManager.Instance.ShowScreen(ScreenId.GachaPrizes);
            }
            else
            {
                // Same fallback the banner card carried before this task.
                Debug.LogWarning("[GachaPullFlow] ScreenManager not found — cannot open GachaPrizes.");
                ToastController.Instance?.Show("Coming soon");
            }
        }
    }
}
