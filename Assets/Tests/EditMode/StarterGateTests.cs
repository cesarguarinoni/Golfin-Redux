// Assets/Tests/EditMode/StarterGateTests.cs
// starter_restore_gate — the gate that decides "picker, or not?".
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (references Golfin.InventorySync + Golfin.Save).
// StarterGate and StarterRoute live in Assembly-CSharp, which an asmdef cannot reference — they
// are reached via System.Reflection, the same pattern as UsernameClaimTests next door. The
// Action<StarterRoute> callback is built with an expression tree because its delegate type is not
// nameable at compile time here.
//
// WHAT IS UNDER TEST — SPEC §2's five rules, in order:
//   1. local starter present            → Ready, synchronously, with NO fetch
//   2. boot already Succeeded           → Ready (NeedsStarter is now the SERVER's answer)
//   3. boot already Failed              → ServerUnreachable (D1: never the picker)
//   4. boot NotRun                      → wait for exactly one OnBootFinished
//   5. bot / demo / sends-off           → Ready immediately, byte-identical routing

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Golfin.InventorySync;
using Golfin.Save;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class StarterGateTests
    {
        // ── Reflection: production types in Assembly-CSharp ───────────────────

        private static readonly Type GateType =
            Type.GetType("Golfin.UI.Account.StarterGate, Assembly-CSharp");

        private static readonly Type RouteType =
            Type.GetType("Golfin.UI.Account.StarterRoute, Assembly-CSharp");

        /// <summary>StarterRoute's members, as ints, so a test can name them without the type.</summary>
        private static int Route(string name) => Convert.ToInt32(Enum.Parse(RouteType, name));

        private static void SetSeam(string field, object value) =>
            GateType.GetField(field, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);

        /// <summary>Call <c>StarterGate.Resolve</c>, appending each answer (as an int) to
        /// <paramref name="sink"/>. The delegate is compiled because <c>Action&lt;StarterRoute&gt;</c>
        /// cannot be written down in this assembly.</summary>
        private static void Resolve(List<int> sink)
        {
            var record = (Action<int>)sink.Add;
            var param = Expression.Parameter(RouteType, "route");
            var body = Expression.Invoke(Expression.Constant(record),
                                         Expression.Convert(param, typeof(int)));
            Delegate callback = Expression.Lambda(
                typeof(Action<>).MakeGenericType(RouteType), body, param).Compile();

            GateType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { callback });
        }

        // ── An in-memory server, minimal (the sync assembly's own fake is internal) ──

        private sealed class Server : IInventoryTransport
        {
            public string Json;
            public int Rev;
            public bool Offline;
            public int GetCount;

            public void GetInventory(Action<InventoryFetch> done)
            {
                GetCount++;
                done(Offline ? InventoryFetch.Failed : new InventoryFetch(true, Json, Rev));
            }

            public void PutInventory(string blobJson, int rev, Action<InventoryPutOutcome> done)
                => done(InventoryPutOutcome.Failed);

            public void GetGrants(Action<List<InventoryGrant>> done)
                => done(Offline ? null : new List<InventoryGrant>());

            public void AckGrants(IReadOnlyList<string> grantIds, Action<bool> done) => done(true);
        }

        private Server _server;
        private SaveData _save;
        private InventorySyncService _sync;
        private int _bootRequests;

        private const string StarterBlob =
            "{\"v\":1,\"characters\":[{\"id\":\"char_ken\",\"lv\":10}],\"starter\":\"char_ken\"}";

        [SetUp]
        public void SetUp()
        {
            Assert.NotNull(GateType, "Golfin.UI.Account.StarterGate not found in Assembly-CSharp");
            Assert.NotNull(RouteType, "Golfin.UI.Account.StarterRoute not found in Assembly-CSharp");

            _server = new Server();
            _save = SaveData.CreateFresh();
            _save.unlockedHoles.Clear();
            _bootRequests = 0;

            _sync = new InventorySyncService
            {
                Transport = _server,
                Catalog = EmptyInventoryCatalog.Instance,
                IsAuthenticated = () => true,
                SaveProvider = () => _save,
                MarkSaveDirty = () => { },
            };
            InventorySyncService.ConfigureForTest(_sync);

            // The real probes need a CharacterManager and an InventorySyncBehaviour in a scene;
            // these are the same questions, asked of the same save.
            SetSeam("NeedsStarterProbe", (Func<bool>)(() => string.IsNullOrEmpty(_save.starterCharacterId)));
            SetSeam("BypassProbe", (Func<bool>)(() => false));
            SetSeam("RequestBoot", (Action)(() => { _bootRequests++; _sync.Boot(); }));
        }

        [TearDown]
        public void TearDown()
        {
            GateType.GetMethod("ResetForTest", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, null);
            InventorySyncService.ResetForTest();
        }

        // ── (a) rule 1: a local starter costs nothing ────────────────────────

        [Test]
        public void A_local_starter_resolves_Ready_synchronously_without_a_fetch()
        {
            _save.starterCharacterId = "char_ken";

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
            Assert.AreEqual(0, _server.GetCount, "a device that already played must not wait on the network");
            Assert.AreEqual(0, _bootRequests);
        }

        // ── (b) the bug: an empty save whose account HAS a starter ───────────

        [Test]
        public void An_empty_save_waits_and_the_restored_starter_routes_Ready()
        {
            _server.Json = StarterBlob;

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
            Assert.AreEqual("char_ken", _save.starterCharacterId,
                "the picker must not be reachable — the account already owns a starter");
            Assert.AreEqual(1, _server.GetCount);
        }

        // ── (c) a genuinely new account still gets the picker ────────────────

        [Test]
        public void An_ok_fetch_with_no_blob_resolves_Ready_with_NeedsStarter_still_true()
        {
            _server.Json = null;   // never-synced account

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
            Assert.IsEmpty(_save.starterCharacterId ?? "",
                "a successful fetch that carried no starter is the ONE case that may show the picker");
        }

        // ── (d) D1: a failed fetch never shows the picker, and retries ───────

        [Test]
        public void A_failed_fetch_resolves_ServerUnreachable()
        {
            _server.Offline = true;

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("ServerUnreachable") }, seen);
            Assert.IsEmpty(_save.starterCharacterId ?? "");
        }

        [Test]
        public void An_already_failed_boot_resolves_ServerUnreachable_without_a_second_fetch()
        {
            _server.Offline = true;
            _sync.Boot();                       // the failure happened before anyone resolved
            int fetchesBefore = _server.GetCount;

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("ServerUnreachable") }, seen);
            Assert.AreEqual(fetchesBefore, _server.GetCount, "rule 3 answers from the recorded outcome");
        }

        [Test]
        public void Retry_after_a_failure_resolves_Ready_once_the_server_answers()
        {
            _server.Offline = true;
            var first = new List<int>();
            Resolve(first);
            CollectionAssert.AreEqual(new[] { Route("ServerUnreachable") }, first);

            // What the LOGIN / START tap does: RetryBoot(), then resolve again.
            _server.Offline = false;
            _server.Json = StarterBlob;
            _sync.Boot();

            var second = new List<int>();
            Resolve(second);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, second);
            Assert.AreEqual("char_ken", _save.starterCharacterId);
        }

        // ── (e) resolving before the boot starts ─────────────────────────────

        [Test]
        public void Resolve_before_any_boot_answers_after_it_finishes_exactly_once()
        {
            // RequestBoot deliberately does NOT boot here: this models the window where the
            // behaviour has not bound its save host yet, so the gate must simply wait.
            SetSeam("RequestBoot", (Action)(() => _bootRequests++));
            _server.Json = StarterBlob;

            var seen = new List<int>();
            Resolve(seen);

            Assert.IsEmpty(seen, "nothing may be concluded before the server has spoken");
            Assert.AreEqual(1, _bootRequests, "the gate nudges the boot it is waiting for");

            _sync.Boot();
            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);

            _sync.Reset();
            _sync.Boot();
            Assert.AreEqual(1, seen.Count, "the gate unsubscribes itself — it never answers twice");
        }

        [Test]
        public void Two_pending_resolves_are_both_answered_by_one_boot()
        {
            SetSeam("RequestBoot", (Action)(() => { }));
            _server.Json = StarterBlob;

            var a = new List<int>();
            var b = new List<int>();
            Resolve(a);
            Resolve(b);

            _sync.Boot();

            CollectionAssert.AreEqual(new[] { Route("Ready") }, a);
            CollectionAssert.AreEqual(new[] { Route("Ready") }, b);
        }

        // ── rule 5: the harness and demo paths are untouched ─────────────────

        [Test]
        public void A_bypassed_path_resolves_Ready_immediately_with_no_fetch()
        {
            SetSeam("BypassProbe", (Func<bool>)(() => true));

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
            Assert.AreEqual(0, _server.GetCount);
            Assert.AreEqual(0, _bootRequests, "a bot run must behave exactly as it did before the gate");
        }

        [Test]
        public void An_unauthenticated_session_resolves_Ready_rather_than_hanging()
        {
            // No boot can ever run, so waiting would leave the caller's busy state on forever.
            _sync.IsAuthenticated = () => false;
            SetSeam("RequestBoot", (Action)(() => { }));

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
        }

        [Test]
        public void Sends_disabled_resolves_Ready_rather_than_hanging()
        {
            _sync.SendsEnabled = false;
            SetSeam("RequestBoot", (Action)(() => { }));

            var seen = new List<int>();
            Resolve(seen);

            CollectionAssert.AreEqual(new[] { Route("Ready") }, seen);
        }

    }
}
