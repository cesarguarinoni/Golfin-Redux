// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the orchestration: boot restore, grant drain, write-behind push.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §2, §3, §4, §6
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Save;
using UnityEngine;

namespace Golfin.InventorySync
{
    /// <summary>
    /// What the boot read answered. The distinction that matters is
    /// <see cref="NotRun"/> vs <see cref="Failed"/>: "the server has not spoken yet" and "the
    /// server could not be reached" route differently, and collapsing them is exactly how a
    /// reinstalled player got asked to pick a starter they already own
    /// (starter_restore_gate §Diagnosis).
    /// </summary>
    public enum BootOutcome
    {
        /// <summary>No boot read has completed. Nothing may be concluded from the local save yet.</summary>
        NotRun,
        /// <summary>The server answered. The local save now carries whatever it holds — including
        /// "it holds nothing", which is a real answer and the only one that may show the picker.</summary>
        Succeeded,
        /// <summary>The request failed. The local save is untouched and says nothing about the
        /// account.</summary>
        Failed,
    }

    /// <summary>
    /// Two-way inventory sync: read at boot, merge, apply; then write behind at most once per 30 s
    /// plus once on pause/quit.
    ///
    /// <para>
    /// A PLAIN C# SINGLETON, NOT A MonoBehaviour, for the same reason <see cref="Golfin.Net.ApiClient"/>
    /// and <c>TelemetryService</c> are: it has to be constructible in an EditMode test with no play
    /// mode and no network. <see cref="InventorySyncBehaviour"/> supplies the clock and the
    /// pause/quit signals in a real build, and nothing else.
    /// </para>
    ///
    /// <para>
    /// ⚠️ SYNC AND BACKUP, NOT ANTI-CHEAT (SPEC §6). Everything pushed from here is client-asserted.
    /// A modified client can grant itself anything and this will faithfully back it up. Moving
    /// inventory server-side did not change that, exactly as moving the shop listing server-side did
    /// not make prices authoritative. Server-authoritative spends are PLAN §6 step 4d. The admin
    /// panel says so on the panel, because that is where the person who would assume otherwise is
    /// standing.
    /// </para>
    ///
    /// <para>
    /// FAILURE IS ALWAYS A NO-OP ON THE LOCAL SAVE. Every network path here ends in "keep what we
    /// have": a failed boot fetch skips the restore, a failed push re-dirties for the next window, a
    /// failed grant fetch drains nothing. Offline is therefore not a special case with its own
    /// branch — it is the ordinary failure path, taken often, and it costs one request per 30 s and
    /// no exceptions.
    /// </para>
    /// </summary>
    public sealed class InventorySyncService
    {
        private const string Tag = "[InventorySync]";

        private static InventorySyncService? _instance;
        public static InventorySyncService Instance => _instance ??= new InventorySyncService();

        /// <summary>Install a hand-built service as the singleton (EditMode tests).</summary>
        public static void ConfigureForTest(InventorySyncService service) => _instance = service;

        /// <summary>Drop the singleton so the next <see cref="Instance"/> is fresh.</summary>
        public static void ResetForTest() => _instance = null;

        // ── Seams (defaults are the shipping behaviour; tests replace them) ────

        public IInventoryTransport Transport = new ApiInventoryTransport();

        /// <summary>Catalog defaults for the delta encoding. Installed from Assembly-CSharp by
        /// <c>InventoryCatalogAdapter</c>, which is the only assembly that can see the club and
        /// character databases. Until it is, everything encodes in full — bigger, never wrong.</summary>
        public IInventoryCatalog Catalog = EmptyInventoryCatalog.Instance;

        /// <summary>Auth gate. Evaluated lazily so an EditMode test never touches the
        /// <c>AuthService</c> MonoBehaviour singleton.</summary>
        public Func<bool> IsAuthenticated = () =>
            Golfin.Auth.AuthService.Instance != null &&
            Golfin.Auth.AuthService.Instance.Session != null &&
            Golfin.Auth.AuthService.Instance.Session.IsAuthenticated;

        /// <summary>The live save, or null when there is not one yet. <c>IsLoaded</c> and not
        /// <c>Instance != null</c>: Instance is assigned before LoadData(), so only IsLoaded proves
        /// the disk has actually been read — and syncing a save that is still the field initialiser
        /// would push an empty inventory over a real one.</summary>
        public Func<SaveData?> SaveProvider = () =>
            SaveDataHost.Instance != null && SaveDataHost.Instance.IsLoaded
                ? SaveDataHost.Instance.Data
                : null;

        /// <summary>Schedule a disk write after a restore or a grant changed the save.</summary>
        public Action MarkSaveDirty = () => SaveDataHost.Instance?.MarkDirty();

        /// <summary>Master off switch, for a bot harness or a build that must not touch live
        /// profiles.</summary>
        public bool SendsEnabled = true;

        /// <summary>
        /// Called with every quantity a merge RAISED on a key the save already held — the
        /// refundable-spend path (PLAN §6.5 decision 1), made countable.
        ///
        /// <para>
        /// A SEAM, not a direct telemetry call, for the same reason everything else here is one:
        /// this assembly must stay constructible in an EditMode test with no play mode and no
        /// network, and telemetry wiring lives in ONE place (<c>TelemetryHooks</c>) by convention.
        /// The default is null — the warning below is emitted regardless, so a build with no hooks
        /// installed still leaves the evidence in the log.
        /// </para>
        /// </summary>
        public Action<IReadOnlyList<InventoryRaise>>? OnQuantitiesRaised;

        public readonly InventoryWriteBehind WriteBehind = new InventoryWriteBehind();

        // ── State ─────────────────────────────────────────────────────────────

        /// <summary>The rev the server had when we last heard from it. 0 = never synced, which is
        /// also what a fresh install sends.</summary>
        public int Rev { get; private set; }

        /// <summary>True once the boot read has completed (successfully or not). Nothing is pushed
        /// before it: a push at rev 0 from a client that has not looked would be refused as stale
        /// anyway, and on a genuinely-empty server it would push a fresh save over a real one.</summary>
        public bool BootCompleted { get; private set; }

        public bool HasPushedThisSession { get; private set; }

        /// <summary>What the last boot read answered. <see cref="BootCompleted"/> says a boot
        /// finished; this says WHAT it finished as, which is the half the starter gate needs.</summary>
        public BootOutcome LastBootOutcome { get; private set; } = BootOutcome.NotRun;

        /// <summary>True while a boot fetch is out. Lets <c>InventorySyncBehaviour.TryBoot</c>
        /// be called from several places (bind, sign-in, the starter-gate retry) without any of them
        /// firing a second, redundant GET over one that is already in flight.</summary>
        public bool BootInFlight { get; private set; }

        /// <summary>
        /// Raised after <see cref="Boot"/> finishes — success OR failure — once the grants are
        /// drained. THE SIGNAL THE POST-AUTH ROUTERS WAIT ON: before this, the local save's
        /// <c>starterCharacterId</c> is only "what this device happens to have", not the account's
        /// answer. Main thread (see the note in <see cref="Boot"/>).
        /// </summary>
        public event Action<BootOutcome>? OnBootFinished;

        /// <summary>
        /// Raised after a merge CHANGED the local save — the boot restore or the stale-PUT merge.
        /// The runtime managers build their dictionaries once, in Awake, so without this a restored
        /// roster stays invisible until the next launch. Subscribed from Assembly-CSharp by
        /// <c>InventoryCatalogAdapter</c>, the one place that can see all four managers.
        /// </summary>
        public event Action? OnRestored;

        private bool _inFlight;
        private bool _grantsDrained;

        // ── Boot (SPEC §3 "read at boot, merge, then continue") ───────────────

        /// <summary>
        /// Read the server blob, merge it into the local save ADDITIVELY, apply, then drain grants.
        ///
        /// <para>
        /// THIS IS THE PATH THAT DELIVERS "a fresh install with no local save restores from it". A
        /// fresh install's local save is empty, so the additive merge of (empty ∪ server) is the
        /// server blob, and applying it is a full restore. The SAME code path on a device that
        /// already has a save is a merge — there is deliberately no separate restore mode, because a
        /// second code path is a second set of bugs and only one of them would be exercised daily.
        /// </para>
        /// <para>
        /// A FAILED FETCH IS NOT A RESTORE OF NOTHING. On failure the local save is untouched and
        /// <see cref="BootCompleted"/> still becomes true, so the session goes on pushing — a player
        /// who launched in a tunnel must not lose the session's progress to a fetch that never
        /// answered.
        /// </para>
        /// </summary>
        public void Boot(Action? done = null)
        {
            if (!SendsEnabled || !SafeAuthed())
            {
                // Not authenticated is a normal state (a tester who has not signed in yet), not an
                // error. Leave BootCompleted false — and LastBootOutcome at NotRun — so a later
                // sign-in can run the real boot and the starter gate keeps waiting for it.
                done?.Invoke();
                return;
            }

            // A RETRY RE-OPENS THE QUESTION. Clearing this here (not in the callback) is what makes
            // a StarterGate.Resolve issued right after RetryBoot() WAIT for the new answer instead
            // of reading the previous failure and showing the offline error again forever.
            LastBootOutcome = BootOutcome.NotRun;
            BootInFlight = true;

            Transport.GetInventory(fetch =>
            {
                // ⚠️ MAIN THREAD. ApiClient completes its callbacks on the main thread (RestoreFrom
                // below already touches SaveDataHost from here, and has since this shipped), so the
                // OnBootFinished/OnRestored handlers below may touch Unity objects. No dispatcher.
                BootInFlight = false;
                try
                {
                    if (fetch.Ok)
                    {
                        Rev = fetch.Rev;
                        if (!string.IsNullOrEmpty(fetch.Json)) RestoreFrom(fetch.Json!);
                        else Debug.Log($"{Tag} No server inventory yet (rev {Rev}) — the local save is the seed.");
                    }
                }
                catch (Exception ex)
                {
                    // A malformed blob must not take the boot down with it. The local save is
                    // already the fallback and it is untouched.
                    Debug.LogError($"{Tag} Boot restore threw and was swallowed: {ex}");
                }

                LastBootOutcome = fetch.Ok ? BootOutcome.Succeeded : BootOutcome.Failed;
                BootCompleted = true;
                DrainGrants(() =>
                {
                    RaiseBootFinished();
                    done?.Invoke();
                });
            });
        }

        /// <summary>Announce the boot outcome. A throwing subscriber must never break the sync that
        /// produced it — same contract as <c>AuthService.RaiseSignedIn</c>.</summary>
        private void RaiseBootFinished()
        {
            try { OnBootFinished?.Invoke(LastBootOutcome); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnBootFinished subscriber threw: {ex}"); }
        }

        /// <summary>Announce that a merge changed the save, so the runtime managers can re-read it.
        /// Swallows, for the same reason: the save is already correct at this point and losing that
        /// to a UI subscriber's bug would be the expensive half.</summary>
        private void RaiseRestored()
        {
            try { OnRestored?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnRestored subscriber threw: {ex}"); }
        }

        private void RestoreFrom(string serverJson)
        {
            SaveData? save = SaveProvider();
            if (save == null) return;

            var theirs = InventoryCodec.Decode(serverJson, Catalog);
            if (!ApplyAndCount(theirs, save, "boot"))
            {
                Debug.Log($"{Tag} Server inventory (rev {Rev}) added nothing — already in sync.");
                return;
            }

            MarkSaveDirty();
            RaiseRestored();
            // The merged state is now strictly bigger than what the server holds, so it is owed a
            // push. Marking dirty here (rather than waiting for a mutation) is what makes the
            // restore round-trip converge in one session instead of on the next purchase.
            WriteBehind.MarkDirty();
            Debug.Log($"{Tag} Restored/merged server inventory (rev {Rev}) into the local save.");
        }

        // ── Grants (SPEC §4) ──────────────────────────────────────────────────

        /// <summary>
        /// Drain the pending grants once per session, apply them, ack them.
        ///
        /// <para>
        /// AT BOOT, NOT MID-SESSION, on purpose: a bag that gains a club while the player is looking
        /// at the bag screen is a UI-refresh problem with no upside, and the same reasoning as I5
        /// ("catalog changes take effect on NEXT LAUNCH") applies unchanged.
        /// </para>
        /// <para>
        /// <paramref name="force"/> is the ONE exception, added by gacha_client_real_pull §4.1. A
        /// gacha pull queues its grants server-side DURING the session, and the player is watching
        /// the reveal of the very clubs it queued — so the once-per-session latch would leave the
        /// Prizes screen reading a bag that does not hold them until the next launch. The rule
        /// above is "do not change the bag under the player"; a pull IS the player asking for it.
        /// Nothing else may pass true: every other caller is a boot path.
        /// </para>
        /// </summary>
        public void DrainGrants(Action? done = null, bool force = false)
        {
            if ((_grantsDrained && !force) || !SendsEnabled || !SafeAuthed()) { done?.Invoke(); return; }

            Transport.GetGrants(grants =>
            {
                if (grants == null) { done?.Invoke(); return; }   // failed; retried next boot
                _grantsDrained = true;

                if (grants.Count == 0) { done?.Invoke(); return; }

                SaveData? save = SaveProvider();
                if (save == null) { done?.Invoke(); return; }

                InventoryGrants.ApplyResult result;
                try
                {
                    result = InventoryGrants.Apply(grants, save, Catalog);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Tag} Grant apply threw and was swallowed: {ex}");
                    done?.Invoke();
                    return;
                }

                if (result.Changed)
                {
                    MarkSaveDirty();
                    WriteBehind.MarkDirty();
                }

                Debug.Log($"{Tag} Drained {grants.Count} grant(s): {result.AppliedCount} applied, " +
                          $"{result.DuplicateCount} already applied.");

                // gacha_client_real_pull §4.4 — a ticket grant's local write is a display-cache
                // bump over a ledger this assembly cannot read. Whoever owns the counter re-reads
                // /gacha/tickets from here, so it converges instead of drifting.
                if (result.AppliedTicketCount > 0) RaiseTicketGrantsApplied(result.AppliedTicketCount);

                if (result.AckIds.Count == 0) { done?.Invoke(); return; }
                Transport.AckGrants(result.AckIds, _ => done?.Invoke());
            });
        }

        /// <summary>
        /// Raised after a drain APPLIED at least one ticket grant. The argument is how many.
        ///
        /// <para>
        /// It is an event rather than a direct call because <c>GachaTicketManager</c> lives in
        /// Assembly-CSharp, which this assembly must not reference — the same split as
        /// <c>IServerBalanceSink</c>. Nothing here knows what a ticket counter is; it only knows
        /// that a number it wrote is a guess.
        /// </para>
        /// </summary>
        public static event Action<int>? OnTicketGrantsApplied;

        private static void RaiseTicketGrantsApplied(int count)
        {
            try { OnTicketGrantsApplied?.Invoke(count); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnTicketGrantsApplied subscriber threw: {ex}"); }
        }

        // ── Write-behind push (SPEC §3) ───────────────────────────────────────

        /// <summary>A mutation happened. Wire this to <c>SaveDataHost.OnSaved</c> — never to
        /// individual manager events, which would be both incomplete and per-mutation.</summary>
        public void MarkDirty() => WriteBehind.MarkDirty();

        /// <summary>Advance the write-behind clock and push if one is due. Called every frame from
        /// <see cref="InventorySyncBehaviour"/>; tests call it directly with a fabricated clock to
        /// reach the 30 s branch without waiting 30 s.</summary>
        public void Tick(float now)
        {
            if (WriteBehind.SecondsUntilDue(now) > 0f) return;
            Push(now, force: false);
        }

        /// <summary>Flush now, ignoring the 30 s window. The pause/quit path — and the ONLY thing
        /// that bypasses the window (SPEC §3).</summary>
        public void FlushNow(float now) => Push(now, force: true);

        private void Push(float now, bool force)
        {
            if (_inFlight || !SendsEnabled || !BootCompleted || !SafeAuthed()) return;
            if (!WriteBehind.TryClaim(now, force)) return;

            SaveData? save = SaveProvider();
            if (save == null) { WriteBehind.ReleaseFailed(); return; }

            string blob;
            InventorySnapshot mine;
            try
            {
                mine = InventoryProjector.Project(save);
                blob = InventoryCodec.Encode(mine, Catalog);
            }
            catch (Exception ex)
            {
                // A projection that throws is a bug in this assembly, not a server condition.
                // Swallow it: an inventory backup must never be the thing that breaks a session.
                Debug.LogError($"{Tag} Projection threw and was swallowed: {ex}");
                return;
            }

            _inFlight = true;
            Transport.PutInventory(blob, Rev, outcome => OnPutComplete(outcome, mine, retryAllowed: true));
        }

        private void OnPutComplete(InventoryPutOutcome outcome, InventorySnapshot mine, bool retryAllowed)
        {
            if (!outcome.Ok)
            {
                _inFlight = false;
                WriteBehind.ReleaseFailed();
                return;
            }

            if (outcome.Stored)
            {
                _inFlight = false;
                Rev = outcome.Rev;
                HasPushedThisSession = true;
                return;
            }

            // ── STALE: another device wrote. Merge ADDITIVELY, then push once more. ──
            //
            // The merged snapshot is a superset of both sides, so the retry cannot lose either
            // device's property. It is allowed EXACTLY ONE retry: a second stale answer means a
            // third device is writing in the same window, and looping on it would be a request storm
            // that converges no faster than simply trying again in 30 s.
            Rev = outcome.Rev;

            if (!retryAllowed)
            {
                _inFlight = false;
                WriteBehind.ReleaseFailed();
                Debug.LogWarning($"{Tag} Still stale at rev {Rev} after one merge — deferring to the " +
                                 "next window rather than looping.");
                return;
            }

            InventorySnapshot merged;
            string blob;
            try
            {
                var theirs = InventoryCodec.Decode(outcome.ServerJson, Catalog);
                merged = InventoryMerge.Additive(mine, theirs);
                blob = InventoryCodec.Encode(merged, Catalog);

                // The merge may have brought back property this device did not have. Fold it into
                // the local save too, or the next projection would drop it again and the two devices
                // would ping-pong forever.
                SaveData? save = SaveProvider();
                if (save != null && ApplyAndCount(theirs, save, "stale-merge"))
                {
                    MarkSaveDirty();
                    // A level another device raised must show on THIS device's roster too, not only
                    // in the blob — same reason the boot restore raises it.
                    RaiseRestored();
                }
            }
            catch (Exception ex)
            {
                _inFlight = false;
                WriteBehind.ReleaseFailed();
                Debug.LogError($"{Tag} Stale-merge threw and was swallowed: {ex}");
                return;
            }

            Debug.Log($"{Tag} Rev moved to {Rev} under us — merged additively and retrying once.");
            Transport.PutInventory(blob, Rev, o2 => OnPutComplete(o2, merged, retryAllowed: false));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// <see cref="InventoryProjector.Apply"/>, plus the count PLAN §6.5 decision 1 asks for.
        ///
        /// <para>
        /// BOTH merge sites go through here, and there are exactly two: the boot restore and the
        /// stale-PUT retry. The stale one is where the refund is most likely (a second device with
        /// an older rev), but the boot one can do it too — a reinstall that restores a blob written
        /// before this device's last spend — so counting only the obvious site would undercount by
        /// exactly the cases nobody expected.
        /// </para>
        /// <para>
        /// The warning is emitted even with no <see cref="OnQuantitiesRaised"/> handler installed:
        /// the Editor log is the fallback record, and a refund that reached a tester's device with
        /// nothing written down anywhere is the outcome this whole item exists to prevent.
        /// </para>
        /// </summary>
        private bool ApplyAndCount(InventorySnapshot theirs, SaveData save, string context)
        {
            var raises = new List<InventoryRaise>();
            bool changed = InventoryProjector.Apply(theirs, save, raises);

            if (raises.Count > 0)
            {
                foreach (var raise in raises)
                    Debug.LogWarning($"{Tag} MERGE RAISED A QUANTITY ({context}, rev {Rev}): " +
                                     $"{raise.Kind} '{raise.Id}' {raise.From} -> {raise.To}. " +
                                     "If the player had spent it, this refunded it — PLAN §6.5.");

                // Never let a reporting handler take the sync down with it: the merge has already
                // been applied to the save at this point, and losing that to a telemetry bug would
                // be the expensive half of a cheap feature.
                try { OnQuantitiesRaised?.Invoke(raises); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{Tag} OnQuantitiesRaised threw and was swallowed: {ex.Message}");
                }
            }

            return changed;
        }

        private bool SafeAuthed()
        {
            try { return IsAuthenticated == null || IsAuthenticated(); }
            catch { return false; }
        }

        /// <summary>Forget the session (sign-out, tests). The applied-grant ledger lives in the save
        /// and is deliberately NOT cleared here — it belongs to the save, not the session.</summary>
        public void Reset()
        {
            Rev = 0;
            BootCompleted = false;
            LastBootOutcome = BootOutcome.NotRun;
            BootInFlight = false;
            HasPushedThisSession = false;
            _inFlight = false;
            _grantsDrained = false;
            WriteBehind.Reset();
        }
    }
}
