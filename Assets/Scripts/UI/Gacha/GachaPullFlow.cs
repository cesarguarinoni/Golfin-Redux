// Assets/Scripts/UI/Gacha/GachaPullFlow.cs
// gacha_reveal_animation §1 — the single entry point every PULL button routes through.
// gacha_client_real_pull §4.2 — and it is now a REAL pull.
//
// Before this task the flow was: build ten hard-coded prizes → reveal them → show the Prizes
// screen. Nothing was charged and the server never saw a pull happen. Now it is: open the reveal
// modal in a WAITING state → ask the server → reveal exactly what the server granted. The modal
// covering the round trip is what makes that a change of authority rather than a change of
// latency: there is no spinner, no disabled button and no new UI, because the bag was already
// shaking for a second before the first card appeared.
//
// ⚠️ THERE IS NO LOCAL FALLBACK, DELIBERATELY. GachaMockPrizePool is deleted, not disabled. With
// no server answer nothing is revealed and nothing is granted — a client that rolled its own
// prizes when the network hiccuped is exactly the hole spec B closed.
//
// THIS FILE IS THE GACHA'S ShopTransaction. GachaPullService (Golfin.Economy) does the transport
// and applies nothing; the four consequences of a successful pull — the ticket counter, the RP a
// duplicate paid, the grants drain and the history row — reach into Assembly-CSharp, which that
// assembly must not reference, so they are applied HERE, in ApplyOk, in the SPEC's order.
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Economy;
using Golfin.InventorySync;
using Golfin.UI.Toast;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Static orchestrator for a gacha pull: waiting modal → server → reveal → Prizes screen.
    /// </summary>
    public static class GachaPullFlow
    {
        /// <summary>The banner and count of the last pull, so the Prizes screen's PULL button can
        /// mean "again" without the screen having to know what a banner is.</summary>
        private static GachaBannerEntry? _lastEntry;
        private static int _lastCount = 1;

        /// <summary>
        /// Entry point for the banner card's two PULL buttons.
        ///
        /// <para>
        /// The ENTRY travels with the count rather than being looked up again inside: a content
        /// refresh (§2, 5b) can rebuild the catalog between the tap and the answer, and the guard
        /// the player agreed to is the number that was on the card they touched.
        /// </para>
        /// </summary>
        public static void Pull(GachaBannerEntry entry, int count)
        {
            if (entry == null)
            {
                Debug.LogError("[GachaPullFlow] Pull called with no banner entry — refusing.");
                return;
            }

            _lastEntry = entry;
            _lastCount = count;

            int expectedCost = count == 1 ? entry.CostX1 : entry.CostX10;

            var modal = GachaRevealModalController.Instance;

            // No modal in the scene is today's degrade, kept: the pull still happens, the prizes
            // still land, and the Prizes screen opens without the reveal.
            if (modal != null) modal.BeginWaiting();

            GachaPullService.Instance.PullAsync(
                entry.BannerId, count, expectedCost, ContentBuildNumber.Current,
                outcome => OnPullAnswered(entry, count, modal, outcome));
        }

        /// <summary>
        /// "Pull again" on the Prizes screen — the same banner, the same count. Falls back to a
        /// toast when there is no last pull to repeat, which can only happen if the screen was
        /// reached without one.
        /// </summary>
        public static void PullAgain()
        {
            if (_lastEntry == null)
            {
                Debug.LogWarning("[GachaPullFlow] Pull again with no previous pull — ignoring.");
                ToastController.Instance?.Show(LocalizationManager.Get("GACHA_UNAVAILABLE"));
                return;
            }

            Pull(_lastEntry, _lastCount);
        }

        // ── The answer ─────────────────────────────────────────────────────────

        private static void OnPullAnswered(GachaBannerEntry entry, int count,
                                           GachaRevealModalController? modal,
                                           GachaPullOutcome? outcome)
        {
            if (outcome == null)
            {
                Abort(modal);
                ToastController.Instance?.Show(Golfin.EconomyRuntime.PointsSpendGate.OfflineMessage);
                return;
            }

            switch (outcome.Verdict)
            {
                case GachaPullVerdict.Ok:
                {
                    var prizes = ToRecords(outcome.Prizes);
                    if (prizes.Count == 0)
                    {
                        // 'ok' with no prizes is not a thing the server can produce (the roll writes
                        // one row per slot and reads them back), so this is a wire-shape problem.
                        Debug.LogError("[GachaPullFlow] Server answered ok with no prizes — nothing " +
                                       "to reveal. The pull DID happen server-side; the grants will " +
                                       "drain on the next boot.");
                        Abort(modal);
                        return;
                    }

                    // Everything the pull changed is applied BEFORE the reveal continues, so the
                    // Prizes screen — which reads the bag — cannot enter ahead of the grant that
                    // put the club in it.
                    ApplyOk(outcome.Server, () =>
                    {
                        if (modal != null) modal.Continue(prizes, () => ShowPrizes(prizes));
                        else               ShowPrizes(prizes);
                    });
                    return;
                }

                case GachaPullVerdict.Insufficient:
                    Abort(modal);
                    ToastController.Instance?.Show(LocalizationManager.Get("GACHA_INSUFFICIENT_TICKETS"));
                    return;

                case GachaPullVerdict.CostChanged:
                    // The published cost moved under the card. Re-read the catalog so the SECOND
                    // tap pays the number the player can now see — the round trip is the price of
                    // never charging silently.
                    Abort(modal);
                    ReloadCatalogAndRebuild();
                    ToastController.Instance?.Show(LocalizationManager.Get("GACHA_COST_CHANGED"));
                    return;

                case GachaPullVerdict.PullCap:
                    Abort(modal);
                    ToastController.Instance?.Show(LocalizationManager.Get("GACHA_PULL_CAP"));
                    return;

                case GachaPullVerdict.Paused:
                    // The feature is off, not the banner — so the carousel is NOT rebuilt: nothing
                    // in the catalog changed and withholding every banner would be a lie.
                    Abort(modal);
                    ToastController.Instance?.Show(LocalizationManager.Get("GACHA_PAUSED"));
                    return;

                case GachaPullVerdict.NotAvailable:
                case GachaPullVerdict.Unknown:
                    Abort(modal);
                    ReloadCatalogAndRebuild();
                    ToastController.Instance?.Show(LocalizationManager.Get("GACHA_UNAVAILABLE"));
                    return;

                default:   // Unavailable — offline, timeout, 5xx, or the backend flag is off
                    Abort(modal);
                    ToastController.Instance?.Show(Golfin.EconomyRuntime.PointsSpendGate.OfflineMessage);
                    return;
            }
        }

        private static void Abort(GachaRevealModalController? modal) => modal?.Abort();

        private static List<PrizeRecord> ToRecords(GachaPrizeDto[] prizes)
        {
            var records = new List<PrizeRecord>(prizes.Length);
            foreach (var p in prizes) records.Add(PrizeRecord.FromDto(p));
            return records;
        }

        /// <summary>A banner the server refused, or a price that moved, means this build's copy of
        /// the catalog is stale. Reloading it here is what makes the second tap correct.</summary>
        private static void ReloadCatalogAndRebuild()
        {
            GachaBannerCatalog.Reload();
            GachaCarouselController.Instance?.Rebuild();
        }

        // ── Applying a successful pull ─────────────────────────────────────────

        /// <summary>
        /// The four consequences of a successful pull, in SPEC §4.1's order:
        /// ticket balance → RP fold → grants drain → history.
        ///
        /// <para>
        /// THE ORDER IS THE POINT, which is why it is a seam rather than four calls inline. Tickets
        /// first because the counter is the number the player is watching. RP second, and only when
        /// the payload actually carries the block — folding an absent <c>rp</c>'s zeros in would
        /// wipe the displayed balance. The DRAIN third, and everything after it waits on its
        /// callback: the Prizes screen reads the bag, so the reveal must not continue until the
        /// club is in it. History last, because it is the only one nothing else depends on.
        /// </para>
        /// </summary>
        internal static void ApplyOk(GachaPullResult result, Action done)
            => ApplyOk(result,
                       setTickets: (type, balance) =>
                           GachaTicketManager.Instance?.SetFromServer(type, balance),
                       foldRp: rp =>
                           PointsService.Instance.ApplyEarnedBalance(rp.ActivityPts, rp.GiftPts, rp.TotalPoints),
                       drain: onDrained =>
                       {
                           var sync = InventorySyncService.Instance;
                           if (sync == null) { onDrained(); return; }
                           // FORCED: the boot drain has already run this session, and its
                           // once-per-session latch exists to stop a bag changing while the player
                           // is looking at it. A pull is the player ASKING for the bag to change.
                           sync.DrainGrants(onDrained, force: true);
                       },
                       recordHistory: GachaHistoryStore.Prepend,
                       done: done);

        /// <summary>Testable seam: the same order, over injected effects.</summary>
        internal static void ApplyOk(GachaPullResult result,
                                     Action<int, int> setTickets,
                                     Action<GachaRpDto> foldRp,
                                     Action<Action> drain,
                                     Action<GachaPullResult> recordHistory,
                                     Action done)
        {
            if (result == null) { done?.Invoke(); return; }

            setTickets?.Invoke(result.TicketType, result.TicketBalance);

            if (result.Rp != null) foldRp?.Invoke(result.Rp);

            void Finish()
            {
                recordHistory?.Invoke(result);
                done?.Invoke();
            }

            if (drain != null) drain(Finish);
            else               Finish();
        }

        // ── The Prizes screen ──────────────────────────────────────────────────

        // The reveal calls this at the END of the sequence (or on SKIP), so the Prizes screen binds
        // and activates UNDER the still-opaque scrim and is revealed by the modal's fade.
        private static void ShowPrizes(IReadOnlyList<PrizeRecord> result)
        {
            GachaPrizesScreenController.SetPendingResult(result);

            if (ScreenManager.Instance != null)
            {
                ScreenManager.Instance.ShowScreen(ScreenId.GachaPrizes);
            }
            else
            {
                Debug.LogWarning("[GachaPullFlow] ScreenManager not found — cannot open GachaPrizes.");
                ToastController.Instance?.Show("Coming soon");
            }
        }
    }
}
