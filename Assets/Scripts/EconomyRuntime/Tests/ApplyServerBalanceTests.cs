// ─────────────────────────────────────────────────────────────────────────────
// rp_balance_sync §5.2 — RewardPointsManager.ApplyServerBalance, the one
// authorized inbound writer.
//
// ASSEMBLY: Golfin.EconomyRuntime.Tests (named EditMode asmdef, overrideReferences:false).
// RewardPointsManager lives in Assembly-CSharp — its folder has no .asmdef — so it is
// reached by reflection, the same way Golfin.TournamentsRuntime.Tests reaches the
// tournament adapters next door.
//
// THE REGRESSION THIS GUARDS: the counter went stale because the only writer,
// SetPoints, refuses while PointsBackendEnabled is ON. If ApplyServerBalance ever
// picks up the same AllowLocalOverride guard, FlagOn_ApplyServerBalance_IsNotBlocked
// goes red — that single line is the whole bug.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Golfin.Economy;
using Golfin.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.EconomyRuntime.Tests
{
    /// <summary>In-memory <see cref="ISavePersister"/> — never touches the real save file.</summary>
    internal sealed class NullPersister : ISavePersister
    {
        public bool TryLoad(out string? json) { json = null; return false; }
        public Task SaveAsync(string json) => Task.CompletedTask;
    }

    [TestFixture]
    public class ApplyServerBalanceTests
    {
        private const string ManagerTypeName = "Golfin.Roster.RewardPointsManager";

        private GameObject? _saveGo;
        private GameObject? _rpGo;
        private object? _manager;
        private Type _managerType = null!;

        private object? _savedSaveDataHost;
        private object? _savedManagerInstance;

        // ── bootstrap ─────────────────────────────────────────────────────────────

        private static Type AsmCSharpType(string fullName)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "Assembly-CSharp");
            Type? t = asm.GetType(fullName);
            if (t == null) throw new InvalidOperationException($"Type '{fullName}' not found in Assembly-CSharp");
            return t;
        }

        private static FieldInfo? StaticBackingField(Type t, string propName)
            => t.GetField($"<{propName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _managerType = AsmCSharpType(ManagerTypeName);

            // ── SaveDataHost (in-memory) ─────────────────────────────────────────
            _savedSaveDataHost = SaveDataHost.Instance;
            StaticBackingField(typeof(SaveDataHost), "Instance")?.SetValue(null, null);

            _saveGo = new GameObject("TEST_SaveDataHost_RP");
            var host = _saveGo.AddComponent<SaveDataHost>();
            host.SetPersister(new NullPersister());
            if (SaveDataHost.Instance == null)
                StaticBackingField(typeof(SaveDataHost), "Instance")?.SetValue(null, host);

            SaveDataHost.Instance.Data.rewardPoints = 100;

            // ── RewardPointsManager ──────────────────────────────────────────────
            _savedManagerInstance = _managerType
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            StaticBackingField(_managerType, "Instance")?.SetValue(null, null);

            _rpGo = new GameObject("TEST_RewardPointsManager");
            _manager = _rpGo.AddComponent(_managerType);
            if (_managerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) == null)
                StaticBackingField(_managerType, "Instance")?.SetValue(null, _manager);

            PointsBackendFlag.Enabled = true; // the ON case is the one that was broken
        }

        [TearDown]
        public void TearDown()
        {
            PointsBackendFlag.ResetToDefault();

            if (_rpGo != null) UnityEngine.Object.DestroyImmediate(_rpGo);
            if (_saveGo != null) UnityEngine.Object.DestroyImmediate(_saveGo);
            _rpGo = null; _saveGo = null; _manager = null;

            StaticBackingField(typeof(SaveDataHost), "Instance")?.SetValue(null, _savedSaveDataHost);
            try { StaticBackingField(_managerType, "Instance")?.SetValue(null, _savedManagerInstance); }
            catch { /* the manager type may not have a backing field in some builds */ }
        }

        // ── reflection helpers ────────────────────────────────────────────────────

        private void Invoke(string method, params object?[] args)
            => _managerType.GetMethod(method, BindingFlags.Instance | BindingFlags.Public)!
                           .Invoke(_manager, args);

        private int Points()
            => (int)_managerType.GetMethod("GetPoints", BindingFlags.Instance | BindingFlags.Public)!
                                .Invoke(_manager, Array.Empty<object>())!;

        /// <summary>Subscribe to the manager's <c>OnPointsChanged</c> and collect what it reports.</summary>
        private Action<int> Listen(List<int> sink)
        {
            Action<int> handler = v => sink.Add(v);
            _managerType.GetEvent("OnPointsChanged", BindingFlags.Instance | BindingFlags.Public)!
                        .AddEventHandler(_manager, handler);
            return handler;
        }

        // ── §3.1 the authorized inbound writer ────────────────────────────────────

        [Test]
        public void FlagOn_ApplyServerBalance_IsNotBlocked()
        {
            var seen = new List<int>();
            Listen(seen);

            Invoke("ApplyServerBalance", 173);

            Assert.AreEqual(173, Points(),
                "ApplyServerBalance is the SERVER speaking — the AllowLocalOverride guard must not apply to it.");
            Assert.AreEqual(1, seen.Count, "Every RP display updates off OnPointsChanged — it must fire.");
            Assert.AreEqual(173, seen[0]);
            Assert.AreEqual(173, SaveDataHost.Instance.Data.rewardPoints, "SaveData is the display cache — write it.");
        }

        [Test]
        public void FlagOn_SetPoints_IsStillBlocked()
        {
            // The guard that broke the counter is CORRECT for local writes — this pins that it stays.
            Invoke("SetPoints", 999);

            Assert.AreEqual(100, Points(), "SetPoints must still refuse while the server is authoritative.");
        }

        [Test]
        public void UnchangedValue_IsANoOp()
        {
            var seen = new List<int>();
            Listen(seen);

            Invoke("ApplyServerBalance", 100); // already 100

            Assert.AreEqual(0, seen.Count, "An unchanged balance must not fire an event or dirty the save.");
        }

        [Test]
        public void NegativeBalance_IsRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex("negative balance"));

            Invoke("ApplyServerBalance", -5);

            Assert.AreEqual(100, Points(), "A negative server balance is corrupt data — keep what we had.");
        }

        [Test]
        public void FlagOff_ApplyServerBalance_StillWrites()
        {
            // Flag OFF is the local-only loop, but a balance can only arrive from a real server answer,
            // so applying one is never wrong — and the gate lives upstream in PointsService anyway.
            PointsBackendFlag.Enabled = false;

            Invoke("ApplyServerBalance", 42);

            Assert.AreEqual(42, Points());
        }

        // ── §3.1 balance ≠ earned ─────────────────────────────────────────────────

        [Test]
        public void LeaderboardAccumulators_AreUntouched()
        {
            SaveData data = SaveDataHost.Instance.Data;
            data.lifetimeRpEarned = 500;
            data.rpDaily = 40;
            data.rpWeekly = 90;
            data.rpMonthly = 300;

            Invoke("ApplyServerBalance", 173);

            Assert.AreEqual(500, data.lifetimeRpEarned, "Accumulators track RP EARNED, not RP held.");
            Assert.AreEqual(40, data.rpDaily);
            Assert.AreEqual(90, data.rpWeekly);
            Assert.AreEqual(300, data.rpMonthly);
        }
    }
}
