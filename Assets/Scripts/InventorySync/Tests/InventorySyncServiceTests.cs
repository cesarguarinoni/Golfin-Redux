// ─────────────────────────────────────────────────────────────────────────────
// content_player_inventory — the write-behind, the stale-rev retry, grants, offline.
//
// Acceptance covered:
//   * "Write-behind coalesces: 10 rapid mutations produce ONE PUT, plus one on pause"
//   * "`rev` mismatch merges additively — nothing lost on either side"
//   * "A grant applies once and is idempotent across three boots"
//   * "Offline: no sync, no exception, local save unaffected"
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.InventorySync.Tests
{
    /// <summary>
    /// An in-memory server: holds one blob at one rev, refuses a stale PUT the way the real endpoint
    /// does, and serves a grants queue. Synchronous — every callback fires before the call returns,
    /// so a test reads like the sequence it is describing.
    /// </summary>
    internal sealed class FakeTransport : IInventoryTransport
    {
        public string ServerJson;
        public int ServerRev;

        public List<InventoryGrant> Grants = new List<InventoryGrant>();
        public readonly List<string> Acked = new List<string>();

        public int GetCount, PutCount, GrantGetCount, AckCount;
        public readonly List<string> PutBodies = new List<string>();

        /// <summary>Every call fails — the offline case.</summary>
        public bool Offline;

        public void GetInventory(Action<InventoryFetch> done)
        {
            GetCount++;
            done(Offline ? InventoryFetch.Failed : new InventoryFetch(true, ServerJson, ServerRev));
        }

        public void PutInventory(string blobJson, int rev, Action<InventoryPutOutcome> done)
        {
            PutCount++;
            PutBodies.Add(blobJson);
            if (Offline) { done(InventoryPutOutcome.Failed); return; }

            if (rev != ServerRev)
            {
                done(new InventoryPutOutcome(true, false, true, ServerRev, ServerJson));
                return;
            }
            ServerJson = blobJson;
            ServerRev = rev + 1;
            done(new InventoryPutOutcome(true, true, false, ServerRev, null));
        }

        public void GetGrants(Action<List<InventoryGrant>> done)
        {
            GrantGetCount++;
            if (Offline) { done(null); return; }
            var pending = new List<InventoryGrant>();
            foreach (var g in Grants) if (!Acked.Contains(g.Id)) pending.Add(g);
            done(pending);
        }

        public void AckGrants(IReadOnlyList<string> grantIds, Action<bool> done)
        {
            AckCount++;
            if (Offline) { done(false); return; }
            foreach (var id in grantIds) if (!Acked.Contains(id)) Acked.Add(id);
            done(true);
        }
    }

    public class InventorySyncServiceTests
    {
        private FakeTransport _server;
        private SaveData _save;
        private InventorySyncService _sync;
        private int _dirtyCount;

        [SetUp]
        public void SetUp()
        {
            _server = new FakeTransport();
            _save = SaveData.CreateFresh();
            _save.unlockedHoles.Clear();
            _dirtyCount = 0;

            _sync = new InventorySyncService
            {
                Transport = _server,
                Catalog = FakeCatalog.Standard(),
                IsAuthenticated = () => true,
                SaveProvider = () => _save,
                MarkSaveDirty = () => _dirtyCount++,
            };
            InventorySyncService.ConfigureForTest(_sync);
        }

        [TearDown]
        public void TearDown() => InventorySyncService.ResetForTest();

        // ── Write-behind coalescing (SPEC §3) ────────────────────────────────

        [Test]
        public void Ten_rapid_mutations_produce_exactly_one_put()
        {
            _sync.Boot();
            Assert.AreEqual(0, _server.PutCount);

            float t = 100f;
            for (int i = 0; i < 10; i++)
            {
                _sync.MarkDirty();          // ten OnSaved callbacks inside a second
                _sync.Tick(t + i * 0.1f);
            }

            Assert.AreEqual(1, _server.PutCount, "ten mutations inside the 30 s window are ONE request");
        }

        [Test]
        public void A_pause_flush_adds_exactly_one_more_put_and_bypasses_the_window()
        {
            _sync.Boot();

            float t = 100f;
            for (int i = 0; i < 10; i++) { _sync.MarkDirty(); _sync.Tick(t + i * 0.1f); }
            Assert.AreEqual(1, _server.PutCount);

            _sync.MarkDirty();
            _sync.FlushNow(t + 2f);         // pause, 2 s after the last send
            Assert.AreEqual(2, _server.PutCount, "pause flushes regardless of the 30 s window");
        }

        [Test]
        public void A_pause_with_nothing_pending_sends_nothing()
        {
            _sync.Boot();
            _sync.FlushNow(100f);
            Assert.AreEqual(0, _server.PutCount);
        }

        [Test]
        public void The_next_window_opens_after_thirty_seconds()
        {
            _sync.Boot();

            _sync.MarkDirty();
            _sync.Tick(100f);
            Assert.AreEqual(1, _server.PutCount);

            _sync.MarkDirty();
            _sync.Tick(120f);                              // 20 s — still inside the window
            Assert.AreEqual(1, _server.PutCount);

            _sync.Tick(131f);                              // 31 s — due
            Assert.AreEqual(2, _server.PutCount);
        }

        [Test]
        public void Nothing_is_pushed_before_the_boot_read_completes()
        {
            // A push at rev 0 from a client that has not looked would either be refused as stale or,
            // worse, land a fresh save on top of a real one.
            _sync.MarkDirty();
            _sync.Tick(100f);
            Assert.AreEqual(0, _server.PutCount);
        }

        // ── The rev (SPEC §3) ────────────────────────────────────────────────

        [Test]
        public void A_stale_put_merges_additively_and_retries_once_losing_nothing()
        {
            // Device 2 has club_b and has been offline; the server already holds device 1's club_a.
            var theirs = new InventorySnapshot();
            theirs.Clubs.Add(new PersistedClub
            { clubId = "club_iron9_klyro", currentLevel = 10, currentDurability = 40, maxDurability = 40, totalSPEarned = 9 });
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 7;

            _sync.Boot();                                  // now at rev 7 and club_a is local too
            _server.ServerRev = 9;                         // device 1 writes twice more, unseen

            _save.ownedClubs.Add(new PersistedClub
            { clubId = "club_driver_golfinx", currentLevel = 99, currentDurability = 60, maxDurability = 60 });
            _sync.MarkDirty();
            _sync.Tick(100f);

            Assert.AreEqual(2, _server.PutCount, "one refused PUT, then exactly one merged retry");
            Assert.AreEqual(10, _server.ServerRev);

            var stored = InventoryCodec.Decode(_server.ServerJson, _sync.Catalog);
            CollectionAssert.AreEquivalent(
                new[] { "club_iron9_klyro", "club_driver_golfinx" },
                stored.Clubs.ConvertAll(c => c.clubId),
                "the merged blob is a superset of both devices");
            Assert.AreEqual(99, stored.Clubs.Find(c => c.clubId == "club_driver_golfinx").currentLevel);
        }

        // ── The refundable spend, counted at BOTH merge sites (PLAN §6.5) ────

        [Test]
        public void A_stale_merge_that_refunds_a_consumed_item_is_reported()
        {
            // §6.5's exact scenario. This device spent a repair kit (3 -> 2); the other device is
            // holding a stale rev and its blob still says 3. The additive merge hands it back — RP
            // stays debited, so it is a free consumable. Accepted for the beta; NOT accepted
            // silently, because the beta consumption numbers are what tune the economy.
            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 3;
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 7;

            var seen = new List<InventoryRaise>();
            _sync.OnQuantitiesRaised = rs => seen.AddRange(rs);

            _sync.Boot();                                  // boot merge: a NEW key -> a restore
            Assert.AreEqual(0, seen.Count, "a fresh key arriving is a restore, not a refund");

            _save.itemQuantities["item_repair_kit"] = 2;   // ... then the player spends one
            _server.ServerRev = 9;                         // and the other device moves the rev

            _sync.MarkDirty();
            _sync.Tick(100f);

            Assert.AreEqual(1, seen.Count, "the stale-merge refund must be reported exactly once");
            Assert.AreEqual("Item:item_repair_kit 2->3", seen[0].ToString());
        }

        [Test]
        public void A_boot_merge_that_raises_a_held_quantity_is_reported_too()
        {
            // The less obvious of the two sites: a reinstall (or a second device booting) restores a
            // blob written BEFORE this device's last spend. Counting only the stale path would
            // undercount by exactly the cases nobody expected.
            _save.itemQuantities["item_repair_kit"] = 2;

            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 5;
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 3;

            var seen = new List<InventoryRaise>();
            _sync.OnQuantitiesRaised = rs => seen.AddRange(rs);

            _sync.Boot();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual("Item:item_repair_kit 2->5", seen[0].ToString());
            Assert.AreEqual(5, _save.itemQuantities["item_repair_kit"], "the merge still applies");
        }

        [Test]
        public void A_reporting_handler_that_throws_cannot_break_the_sync()
        {
            // The merge is already applied to the save by the time the handler runs. Losing that to
            // a telemetry bug would be the expensive half of a very cheap feature.
            _save.itemQuantities["item_repair_kit"] = 2;

            var theirs = new InventorySnapshot();
            theirs.Items["item_repair_kit"] = 5;
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 3;

            _sync.OnQuantitiesRaised = _ => throw new InvalidOperationException("telemetry is down");

            // Only a WARNING is logged by the swallow, so the test framework does not fail on it.
            Assert.DoesNotThrow(() => _sync.Boot());

            Assert.AreEqual(5, _save.itemQuantities["item_repair_kit"]);
            Assert.IsTrue(_sync.BootCompleted);
        }

        [Test]
        public void A_second_stale_answer_defers_instead_of_looping()
        {
            _server.ServerRev = 5;
            _sync.Boot();
            _server.ServerRev = 6;

            // A server whose rev always moves: every PUT is stale.
            var alwaysStale = new AlwaysStaleTransport();
            _sync.Transport = alwaysStale;

            _sync.MarkDirty();
            _sync.Tick(100f);

            Assert.AreEqual(2, alwaysStale.PutCount, "exactly one retry, then defer to the next window");
        }

        private sealed class AlwaysStaleTransport : IInventoryTransport
        {
            public int PutCount;
            private int _rev = 100;

            public void GetInventory(Action<InventoryFetch> done) => done(new InventoryFetch(true, null, _rev));
            public void PutInventory(string blobJson, int rev, Action<InventoryPutOutcome> done)
            {
                PutCount++;
                done(new InventoryPutOutcome(true, false, true, ++_rev, null));
            }
            public void GetGrants(Action<List<InventoryGrant>> done) => done(new List<InventoryGrant>());
            public void AckGrants(IReadOnlyList<string> ids, Action<bool> done) => done(true);
        }

        // ── Boot restore (SPEC §3) ───────────────────────────────────────────

        [Test]
        public void A_fresh_install_restores_from_the_server_and_owes_a_push_back()
        {
            var theirs = new InventorySnapshot();
            theirs.Clubs.Add(new PersistedClub
            { clubId = "club_iron9_klyro", currentLevel = 22, currentDurability = 40, maxDurability = 40 });
            theirs.Items["item_repair_kit"] = 4;
            theirs.StarterCharacterId = "char_ken";
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 3;

            _sync.Boot();

            Assert.AreEqual(1, _save.ownedClubs.Count);
            Assert.AreEqual(22, _save.ownedClubs[0].currentLevel);
            Assert.AreEqual(4, _save.itemQuantities["item_repair_kit"]);
            Assert.AreEqual("char_ken", _save.starterCharacterId);
            Assert.AreEqual(1, _dirtyCount, "the restore is owed a disk write");
            Assert.IsTrue(_sync.WriteBehind.IsDirty, "and a push back, so the round trip converges this session");
        }

        [Test]
        public void A_boot_that_adds_nothing_does_not_dirty_the_save()
        {
            _save.ownedClubs.Add(new PersistedClub
            { clubId = "club_iron9_klyro", currentLevel = 10, currentDurability = 40, maxDurability = 40, totalSPEarned = 9 });
            var theirs = InventoryProjector.Project(_save);
            _server.ServerJson = InventoryCodec.Encode(theirs, _sync.Catalog);
            _server.ServerRev = 2;

            _sync.Boot();
            Assert.AreEqual(0, _dirtyCount);
        }

        // ── Grants (SPEC §4) ─────────────────────────────────────────────────

        [Test]
        public void A_grant_applies_once_and_is_idempotent_across_three_boots()
        {
            _server.Grants.Add(new InventoryGrant
            { Id = "grant-1", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = 3 });

            for (int boot = 1; boot <= 3; boot++)
            {
                var session = NewSession();
                session.Boot();
                Assert.AreEqual(3, _save.itemQuantities["item_repair_kit"],
                    $"boot {boot}: still exactly 3 — the grant applied once");
            }

            CollectionAssert.AreEqual(new[] { "grant-1" }, _save.appliedGrantIds);
        }

        [Test]
        public void A_grant_whose_ack_was_lost_is_re_acked_but_not_re_applied()
        {
            // THE WINDOW THE CLIENT-SIDE LEDGER EXISTS FOR: applied, then the ack died.
            _server.Grants.Add(new InventoryGrant
            { Id = "grant-1", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = 3 });

            _sync.Boot();
            Assert.AreEqual(3, _save.itemQuantities["item_repair_kit"]);
            _server.Acked.Clear();                        // the ack never reached the server

            var second = NewSession();
            second.Boot();

            Assert.AreEqual(3, _save.itemQuantities["item_repair_kit"], "applied once, not twice");
            CollectionAssert.Contains(_server.Acked, "grant-1", "and re-acked, so it stops coming back");
        }

        [Test]
        public void A_club_grant_arrives_unequipped_and_a_duplicate_is_a_no_op()
        {
            _server.Grants.Add(new InventoryGrant
            { Id = "g1", Kind = InventoryGrants.KindClub, RefId = "club_driver_golfinx", Amount = 1 });

            _sync.Boot();

            Assert.AreEqual(1, _save.ownedClubs.Count);
            Assert.AreEqual(80, _save.ownedClubs[0].currentLevel, "granted at the catalog default");
            Assert.AreEqual(0, _save.ownedClubs[0].equippedBagSlot, "granted UNEQUIPPED");
        }

        [Test]
        public void A_non_positive_grant_amount_is_ignored_because_grants_cannot_subtract()
        {
            _save.itemQuantities["item_repair_kit"] = 5;
            _server.Grants.Add(new InventoryGrant
            { Id = "g1", Kind = InventoryGrants.KindItem, RefId = "item_repair_kit", Amount = -3 });

            _sync.Boot();
            Assert.AreEqual(5, _save.itemQuantities["item_repair_kit"]);
        }

        [Test]
        public void An_unknown_grant_kind_is_acked_so_the_queue_still_drains()
        {
            _server.Grants.Add(new InventoryGrant
            { Id = "g1", Kind = "something_this_build_does_not_know", RefId = "x", Amount = 1 });

            _sync.Boot();
            CollectionAssert.Contains(_server.Acked, "g1");
        }

        // ── Offline (SPEC acceptance) ────────────────────────────────────────

        [Test]
        public void Offline_syncs_nothing_throws_nothing_and_leaves_the_save_untouched()
        {
            _save.ownedClubs.Add(new PersistedClub { clubId = "club_iron9_klyro", currentLevel = 30 });
            _save.itemQuantities["item_repair_kit"] = 3;
            _server.Offline = true;

            Assert.DoesNotThrow(() =>
            {
                _sync.Boot();
                for (int i = 0; i < 5; i++) { _sync.MarkDirty(); _sync.Tick(100f + i * 40f); }
                _sync.FlushNow(400f);
            });

            Assert.AreEqual(1, _save.ownedClubs.Count);
            Assert.AreEqual(30, _save.ownedClubs[0].currentLevel);
            Assert.AreEqual(3, _save.itemQuantities["item_repair_kit"]);
            Assert.AreEqual(0, _dirtyCount, "nothing was restored, so nothing was written");
            Assert.IsTrue(_sync.WriteBehind.IsDirty, "and the push is still owed, for when the network returns");
        }

        [Test]
        public void A_failed_push_is_retried_on_the_next_window_not_immediately()
        {
            _sync.Boot();
            _server.Offline = true;

            _sync.MarkDirty();
            _sync.Tick(100f);
            Assert.AreEqual(1, _server.PutCount);

            _sync.Tick(101f);                              // still inside the window
            Assert.AreEqual(1, _server.PutCount);

            _sync.Tick(131f);
            Assert.AreEqual(2, _server.PutCount);
        }

        [Test]
        public void An_unauthenticated_session_never_reaches_the_network()
        {
            _sync.IsAuthenticated = () => false;
            _sync.Boot();
            _sync.MarkDirty();
            _sync.Tick(100f);
            _sync.FlushNow(200f);

            Assert.AreEqual(0, _server.GetCount);
            Assert.AreEqual(0, _server.PutCount);
            Assert.IsFalse(_sync.BootCompleted, "so a later sign-in can still run the real boot");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>A new launch against the same save and the same server.</summary>
        private InventorySyncService NewSession()
        {
            var s = new InventorySyncService
            {
                Transport = _server,
                Catalog = _sync.Catalog,
                IsAuthenticated = () => true,
                SaveProvider = () => _save,
                MarkSaveDirty = () => _dirtyCount++,
            };
            InventorySyncService.ConfigureForTest(s);
            return s;
        }
    }
}
