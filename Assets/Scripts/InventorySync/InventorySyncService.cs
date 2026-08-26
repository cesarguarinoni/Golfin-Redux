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
                // error. Leave BootCompleted false so a later sign-in can run the real boot.
                done?.Invoke();
                return;
            }

            Transport.GetInventory(fetch =>
            {
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

                BootCompleted = true;
                DrainGrants(done);
            });
        }

        private void RestoreFrom(string serverJson)
        {
            SaveData? save = SaveProvider();
            if (save == null) return;

            var theirs = InventoryCodec.Decode(serverJson, Catalog);
            if (!InventoryProjector.Apply(theirs, save))
            {
                Debug.Log($"{Tag} Server inventory (rev {Rev}) added nothing — already in sync.");
                return;
            }

            MarkSaveDirty();
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
        /// </summary>
        public void DrainGrants(Action? done = null)
        {
            if (_grantsDrained || !SendsEnabled || !SafeAuthed()) { done?.Invoke(); return; }

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

                if (result.AckIds.Count == 0) { done?.Invoke(); return; }
                Transport.AckGrants(result.AckIds, _ => done?.Invoke());
            });
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
                if (save != null && InventoryProjector.Apply(theirs, save)) MarkSaveDirty();
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
            HasPushedThisSession = false;
            _inFlight = false;
            _grantsDrained = false;
            WriteBehind.Reset();
        }
    }
}
