// ─────────────────────────────────────────────────────────────────────────────
// TournamentServiceWireupTests — regression guard for the production wire.
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// DESIGN RATIONALE:
//   All TournamentsRuntime production code (TournamentService, three adapters,
//   CharacterManagerStatsProvider) lives in Assembly-CSharp — their parent folder
//   has no .asmdef. Named test asmdefs with overrideReferences:false can resolve
//   Assembly-CSharp types at RUNTIME via reflection, and the return types of
//   Compose() / Register() are in the named asmdef Golfin.Tournaments (ITournamentBackend,
//   EntryState, CharacterSnapshot) — so the regression guard assertions work without
//   casting or reflection.
//
//   The key singleton-bootstrap challenge: SaveDataHost, CharacterDatabaseCSV, and
//   CharacterManager are MonoBehaviour singletons that set `Instance` in Awake().
//   Their Awake() guards (`if (Instance != null && Instance != this) Destroy(go); return;`)
//   mean that if a stale Instance exists (e.g., from a prior test or open ShellScene),
//   AddComponent silently self-destructs without setting Instance. Fix: use reflection
//   to clear the backing field before adding, or force-set Instance after adding.
//
// REGRESSION GUARD:
//   Compose_Register_SnapshotHasCorrectStats() MUST FAIL if
//   `stats: new CharacterManagerStatsProvider()` is removed from Compose().
//   Without stats, LocalTournamentBackend._stats is null → SnapshotFor never called
//   → Snapshot is null → Assert.IsNotNull(entry.Snapshot) fires RED.
//
// TESTS:
//   §1  TournamentServiceWireupTests       — Compose + Register + Snapshot
//   §2  RealRewardPointsAdapterTests       — ToInt via reflection (production method)
//   §3  RealHoleParProviderAdapterTests    — ParsFor throws via reflection
//   §4  RealItemRewardAdapterTests         — Grant behavior via reflection
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Golfin.Save;
using Golfin.Tournaments;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Golfin.Tournaments.WireupTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // §0. Bootstrap helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Access Assembly-CSharp types by reflection — the only way from a named test
    /// asmdef that doesn't have a compile-time reference to Assembly-CSharp.
    /// </summary>
    internal static class AsmCSharp
    {
        private static Assembly? _asm;

        public static Assembly Asm
        {
            get
            {
                if (_asm == null)
                    _asm = AppDomain.CurrentDomain.GetAssemblies()
                        .First(a => a.GetName().Name == "Assembly-CSharp");
                return _asm;
            }
        }

        public static Type GetType(string fullName)
        {
            var t = Asm.GetType(fullName);
            if (t == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Type '{fullName}' not found in Assembly-CSharp");
            return t;
        }

        public static Component AddComponent(GameObject go, string typeName)
        {
            var t    = GetType(typeName);
            var comp = go.AddComponent(t);
            if (comp == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] AddComponent({typeName}) returned null");
            return comp;
        }

        public static object? GetStaticField(string typeName, string fieldName)
        {
            var t = GetType(typeName);
            var f = t.GetField(fieldName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Static field '{fieldName}' not found on {typeName}");
            return f.GetValue(null);
        }

        public static void SetStaticField(string typeName, string fieldName, object? value)
        {
            var t = GetType(typeName);
            // Try backing field first (<PropName>k__BackingField), then literal name
            var f = t.GetField($"<{fieldName}>k__BackingField",
                       BindingFlags.Static | BindingFlags.NonPublic)
                ?? t.GetField(fieldName,
                       BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Static field '{fieldName}' (or its backing field) not found on {typeName}");
            f.SetValue(null, value);
        }

        public static object? GetStaticInstance(string typeName)
        {
            var t = GetType(typeName);
            var p = t.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public);
            return p?.GetValue(null);
        }

        public static void SetField(object target, string fieldName, object? value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Field '{fieldName}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        public static void CallMethod(object target, string methodName, params object?[] args)
        {
            var m = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Method '{methodName}' not found on {target.GetType().Name}");
            m.Invoke(target, args);
        }

        public static object? CallStaticMethod(string typeName, string methodName,
            params object?[] args)
        {
            var t = GetType(typeName);
            var m = t.GetMethod(methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null)
                throw new InvalidOperationException(
                    $"[AsmCSharp] Static method '{methodName}' not found on {typeName}");
            return m.Invoke(null, args);
        }

        public static object? GetProperty(object target, string propName)
        {
            var p = target.GetType().GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return p?.GetValue(target);
        }

        public static object? GetField(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f?.GetValue(target);
        }

        /// <summary>
        /// Clears the static singleton Instance backing field, bypassing the singleton guard.
        /// Call this BEFORE AddComponent so that Awake() doesn't find a stale Instance and
        /// self-destruct the new GO.
        /// </summary>
        public static void ClearSingleton(string typeName)
        {
            try { SetStaticField(typeName, "Instance", null); }
            catch { /* ignore — not all singletons have auto-property backing */ }
        }
    }

    /// <summary>In-memory <see cref="ISavePersister"/> — never touches disk.</summary>
    internal sealed class NullPersister : ISavePersister
    {
        public bool TryLoad(out string? json) { json = null; return false; }
        public Task SaveAsync(string json) => Task.CompletedTask;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1. REGRESSION GUARD — TournamentService.Compose() → Register() → Snapshot
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// THE REGRESSION GUARD for the silent-null snapshot trap.
    ///
    /// If `stats: new CharacterManagerStatsProvider()` is removed from
    /// TournamentService.Compose(), <see cref="Compose_Register_SnapshotHasCorrectStats"/>
    /// goes RED. All 189 prior contract tests stay green in that scenario.
    /// </summary>
    [TestFixture]
    public class TournamentServiceWireupTests
    {
        private readonly List<GameObject> _toDestroy = new();

        // Snapshot of pre-existing singleton instances (restored in TearDown so we
        // don't corrupt the Editor state for other tests).
        private object? _savedSaveDataHost;
        private object? _savedCharCsvDb;
        private object? _savedCharMgr;

        [SetUp]
        public void SetUp()
        {
            // ── Save pre-existing singleton values ────────────────────────────
            _savedSaveDataHost = SaveDataHost.Instance;
            _savedCharCsvDb    = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterDatabaseCSV");
            _savedCharMgr      = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterManager");

            // ── 1. SaveDataHost ───────────────────────────────────────────────
            // Clear any stale instance so Awake sets ours.
            // SaveDataHost.Instance backing field name from reflection probe: <Instance>k__BackingField
            AsmCSharp.ClearSingleton("Golfin.Save.SaveDataHost"); // clear via base type name
            // Actually SaveDataHost is in Golfin.Save asmdef — use the direct property.
            // Force-clear via the backing field (it's auto-prop):
            {
                var f = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, null);
            }

            var saveGo = new GameObject("TEST_SaveDataHost");
            var host   = (SaveDataHost)saveGo.AddComponent<SaveDataHost>();
            host.SetPersister(new NullPersister());
            // The fake boot must be COMPLETE, not just present. EditMode never calls Awake, so
            // nothing has read the save — and CharacterManager asserts on SaveDataHost.IsLoaded
            // (content_kill_switch_and_order §2). ReloadFromDisk is the load Awake would have done.
            host.ReloadFromDisk();
            _toDestroy.Add(saveGo);

            // Force-set SaveDataHost.Instance if Awake didn't fire
            if (SaveDataHost.Instance == null)
            {
                var f2 = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f2?.SetValue(null, host);
                Debug.Log("[WireupTests] SaveDataHost.Instance force-set");
            }

            // ── 2. CharacterDatabaseCSV ──────────────────────────────────────
            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterDatabaseCSV");

            var csvGo  = new GameObject("TEST_CharacterDatabaseCSV");
            var csvComp = AsmCSharp.AddComponent(csvGo, "Golfin.Roster.CharacterDatabaseCSV");
            _toDestroy.Add(csvGo);

#if UNITY_EDITOR
            var charTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Characters.csv");
            Assert.IsNotNull(charTextAsset, "Assets/Data/Characters.csv must exist as a TextAsset");
            AsmCSharp.SetField(csvComp, "charactersCSV", charTextAsset);
            AsmCSharp.CallMethod(csvComp, "LoadCharactersFromCSV");
            Debug.Log("[WireupTests] CharacterDatabaseCSV: LoadCharactersFromCSV() called");

            // Force-set CharacterDatabaseCSV.Instance in case Awake() didn't fire
            // (AddComponent(Type) may not fire Awake synchronously in some Unity contexts).
            var csvInstance = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterDatabaseCSV");
            if (csvInstance == null)
            {
                AsmCSharp.SetStaticField("Golfin.Roster.CharacterDatabaseCSV", "Instance", csvComp);
                Debug.Log("[WireupTests] CharacterDatabaseCSV.Instance force-set");
            }
            else
            {
                Debug.Log("[WireupTests] CharacterDatabaseCSV.Instance set by Awake()");
            }
#else
            Assert.Inconclusive("CharacterDatabaseCSV injection requires AssetDatabase (EditMode only)");
#endif

            // ── 3. CharacterManager ──────────────────────────────────────────
            // Clear stale singleton so Awake() sets Instance = this (not the DontDestroyOnLoad branch)
            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterManager");

            var cmGo = new GameObject("TEST_CharacterManager");
            var cmComp = AsmCSharp.AddComponent(cmGo, "Golfin.Roster.CharacterManager");
            _toDestroy.Add(cmGo);

            // If Awake didn't set Instance (possible with non-generic AddComponent in some
            // Unity versions), force-set the backing field directly.
            var cmInstance = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterManager");
            if (cmInstance == null)
            {
                // Force-set the backing field and call LoadRoster manually
                AsmCSharp.SetStaticField("Golfin.Roster.CharacterManager", "Instance", cmComp);
                AsmCSharp.CallMethod(cmComp, "LoadRoster");
                cmInstance = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterManager");
                Debug.Log($"[WireupTests] CharacterManager.Instance force-set: {cmInstance != null}");
            }
            else
            {
                Debug.Log("[WireupTests] CharacterManager.Instance set by Awake()");
            }

            Assert.IsNotNull(cmInstance, "CharacterManager.Instance must be set");

            // char_james must be present
            var charDataMethod = cmInstance!.GetType()
                .GetMethod("GetCharacterData", BindingFlags.Instance | BindingFlags.Public);
            var james = charDataMethod?.Invoke(cmInstance, new object[] { "char_james" });
            Assert.IsNotNull(james,
                "char_james must be present in CharacterManager after LoadRoster from Characters.csv");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _toDestroy.Clear();

            // Restore pre-existing singleton values (so we don't corrupt Editor state)
            {
                var f = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, _savedSaveDataHost);
            }
            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterDatabaseCSV");
            try { AsmCSharp.SetStaticField("Golfin.Roster.CharacterDatabaseCSV", "Instance", _savedCharCsvDb); }
            catch { /* ignore */ }
            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterManager");
            try { AsmCSharp.SetStaticField("Golfin.Roster.CharacterManager", "Instance", _savedCharMgr); }
            catch { /* ignore */ }
        }

        // ── T1: Compose() returns non-null backend with 6 tournaments ─────────

        [Test]
        public void Compose_ReturnsNonNull_With6Tournaments()
        {
            var backend = (ITournamentBackend?)AsmCSharp.CallStaticMethod(
                "Golfin.Tournaments.TournamentService", "Compose");

            Assert.IsNotNull(backend,
                "TournamentService.Compose() must return a non-null ITournamentBackend");

            var tournaments = backend!.GetTournaments();
            Assert.AreEqual(6, tournaments.Count,
                $"Expected 6 tournaments from tournaments.csv, got {tournaments.Count}");
        }

        // ── T2 (THE REGRESSION GUARD): Register → Snapshot non-null + stats ──
        //
        // MUST FAIL if `stats: new CharacterManagerStatsProvider()` is removed
        // from TournamentService.Compose(). Without stats, LocalTournamentBackend.
        // _stats is null → SnapshotFor never called → Snapshot null → FAIL.

        [Test]
        public void Compose_Register_SnapshotHasCorrectStats()
        {
            var backend = (ITournamentBackend?)AsmCSharp.CallStaticMethod(
                "Golfin.Tournaments.TournamentService", "Compose");
            Assert.IsNotNull(backend, "Compose() must return non-null");

            EntryState? entry = null;
            Assert.DoesNotThrow(
                () => entry = backend!.Register("kasumigaseki_open", 0L, "char_james"),
                "Register must not throw for a free tournament with a known character");

            Assert.IsNotNull(entry, "Register must return a non-null EntryState");

            // ── THE REGRESSION GUARD ─────────────────────────────────────────
            // Snapshot is non-null IFF `stats: new CharacterManagerStatsProvider()`
            // was passed to LocalTournamentBackend in TournamentService.Compose().
            Assert.IsNotNull(entry!.Snapshot,
                "Snapshot MUST be non-null. REGRESSION: if this FAILS it means " +
                "`stats: new CharacterManagerStatsProvider()` was removed from " +
                "TournamentService.Compose() — the silent-null trap has fired.");

            // ── char_james stats, read FROM THE LIVE DATABASE ────────────────
            //
            // These used to be four hard-coded literals (6 / 7 / 6 / 6), which is what this test
            // was ACTUALLY asserting: that Characters.csv had not been rebalanced since 2026-07.
            // It had — the shipped row is 7 / 6 / 5 / 7 — so the test sat red and told nobody
            // anything about the wireup it exists to guard. (content_overlay_catalogs)
            //
            // The real claim is "the snapshot carries the character's stats", so the expected
            // values come from the same database the provider reads. A balance change — bundled OR
            // published through the content overlay — can no longer make this go red, and a
            // BROKEN snapshot still does.
            var template = ClubRosterlessCharacter("char_james");
            Assert.IsNotNull(template, "char_james must exist in the character database");

            Assert.AreEqual(10, entry.Snapshot!.Level, "Level must be 10 (Common start)");
            Assert.AreEqual(Field<int>(template!, "baseStrength"),    entry.Snapshot.Strength,
                "Snapshot STR must equal the database's baseStrength");
            Assert.AreEqual(Field<int>(template!, "baseClubControl"), entry.Snapshot.ClubControl,
                "Snapshot CC must equal the database's baseClubControl");
            Assert.AreEqual(Field<int>(template!, "baseRecovery"),    entry.Snapshot.Recovery,
                "Snapshot REC must equal the database's baseRecovery");
            Assert.AreEqual(Field<int>(template!, "baseStamina"),     entry.Snapshot.Stamina,
                "Snapshot STA must equal the database's baseStamina");
        }

        /// <summary>
        /// <c>CharacterDatabaseCSV.Instance.GetCharacter(id)</c> through reflection — the database
        /// lives in Assembly-CSharp, which this test asmdef cannot reference.
        /// </summary>
        private static object? ClubRosterlessCharacter(string characterId)
        {
            object? db = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterDatabaseCSV");
            if (db == null) return null;
            var method = db.GetType().GetMethod("GetCharacter",
                BindingFlags.Public | BindingFlags.Instance);
            return method?.Invoke(db, new object[] { characterId });
        }

        private static T Field<T>(object instance, string fieldName)
        {
            var f = instance.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, $"CharacterDataRuntime.{fieldName} must exist");
            return (T)f!.GetValue(instance);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2. REAL RewardPointsServiceAdapter.ToInt — production method via reflection
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests the REAL (production) <c>RewardPointsServiceAdapter.ToInt</c> (internal
    /// static) via reflection. A bug in the real method is NOT caught by the prior
    /// <c>TournamentAdapterTests.cs</c> "ToIntContract" tests (which exercise a local copy).
    /// </summary>
    [TestFixture]
    public class RealRewardPointsAdapterTests
    {
        private static int RealToInt(long amt)
        {
            var result = AsmCSharp.CallStaticMethod(
                "Golfin.Tournaments.RewardPointsServiceAdapter", "ToInt", amt);
            return (int)result!;
        }

        [Test] public void ToInt_NormalValue_ReturnsValue()
            => Assert.AreEqual(100, RealToInt(100L));

        [Test] public void ToInt_Zero_ReturnsZero()
            => Assert.AreEqual(0, RealToInt(0L));

        [Test] public void ToInt_IntMaxValue_Passthrough()
            => Assert.AreEqual(int.MaxValue, RealToInt((long)int.MaxValue));

        [Test] public void ToInt_IntMaxMinusOne_Passthrough()
            => Assert.AreEqual(int.MaxValue - 1, RealToInt((long)int.MaxValue - 1L));

        // The clamping tests call Debug.LogError internally → Unity Test Runner
        // treats unhandled log errors as failures. Use LogAssert.ignoreFailingMessages
        // to allow them (the clamping IS the correct production behaviour).
        [Test]
        public void ToInt_Overflow_ClampsToIntMaxValue()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            long overflow = (long)int.MaxValue + 1L;
            Assert.AreEqual(int.MaxValue, RealToInt(overflow));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ToInt_LargeOverflow_ClampsToIntMaxValue()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(int.MaxValue, RealToInt(long.MaxValue));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ToInt_Negative_ClampsToZero()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(0, RealToInt(-1L));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ToInt_LargeNegative_ClampsToZero()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(0, RealToInt(long.MinValue));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3. REAL HoleParProviderAdapter.ParsFor — production type via reflection
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests the REAL HoleParProviderAdapter.ParsFor by injecting a controlled
    /// HoleDatabase (ScriptableObject) into HoleDatabaseLoader._runtimeDatabase via
    /// reflection. GolfinRedux.UI types are reached entirely at runtime via reflection
    /// — no compile-time references to Assembly-CSharp UI types.
    /// </summary>
    [TestFixture]
    public class RealHoleParProviderAdapterTests
    {
        private ScriptableObject? _savedDb;
        private Type? _dbType;
        private Type? _dataType;
        private Type? _loaderType;
        private FieldInfo? _runtimeDbField;

        [SetUp]
        public void SetUp()
        {
            _dbType     = AsmCSharp.GetType("GolfinRedux.UI.HoleDatabase");
            _dataType   = AsmCSharp.GetType("GolfinRedux.UI.HoleData");
            _loaderType = AsmCSharp.GetType("GolfinRedux.UI.HoleDatabaseLoader");
            _runtimeDbField = _loaderType!.GetField("_runtimeDatabase",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(_runtimeDbField,
                "HoleDatabaseLoader._runtimeDatabase static field must exist");

            _savedDb = (ScriptableObject?)_runtimeDbField!.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            _runtimeDbField?.SetValue(null, _savedDb);
        }

        private ScriptableObject MakeDb(params (int number, int par)[] holes)
        {
            var db = (ScriptableObject)ScriptableObject.CreateInstance(_dbType!);
            var holeListField = _dbType!.GetField("holes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(holeListField, "HoleDatabase.holes field must exist");
            var holeList = (IList)holeListField!.GetValue(db)!;
            foreach (var (n, p) in holes)
            {
                var h = Activator.CreateInstance(_dataType!, $"TEST_{n}", n)!;
                _dataType!.GetField("par",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.SetValue(h, p);
                holeList.Add(h);
            }
            _runtimeDbField!.SetValue(null, db);
            return db;
        }

        private IReadOnlyList<int> CallParsFor(string[] holeSet)
        {
            var adapter = Activator.CreateInstance(
                AsmCSharp.GetType("Golfin.Tournaments.HoleParProviderAdapter"))!;
            var parsForMethod = adapter.GetType().GetMethod("ParsFor",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(parsForMethod, "HoleParProviderAdapter.ParsFor method must exist");
            return (IReadOnlyList<int>)parsForMethod!.Invoke(adapter,
                new object[] { "any_club", holeSet })!;
        }

        [Test]
        public void ParsFor_ReturnsCorrectParsInOrder()
        {
            var db = MakeDb((1, 4), (2, 5), (3, 3));
            try
            {
                var pars = CallParsFor(new[] { "3", "1", "2" });
                Assert.AreEqual(3, pars.Count);
                Assert.AreEqual(3, pars[0], "holeId=3 → par 3");
                Assert.AreEqual(4, pars[1], "holeId=1 → par 4");
                Assert.AreEqual(5, pars[2], "holeId=2 → par 5");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void ParsFor_UnknownHoleId_Throws()
        {
            var db = MakeDb((1, 4));
            try
            {
                var ex = Assert.Throws<TargetInvocationException>(
                    () => CallParsFor(new[] { "99" }));
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                    "Unknown hole id must throw InvalidOperationException");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void ParsFor_NonNumericHoleId_Throws()
        {
            var db = MakeDb((1, 4));
            try
            {
                var ex = Assert.Throws<TargetInvocationException>(
                    () => CallParsFor(new[] { "abc" }));
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                    "Non-numeric hole id must throw InvalidOperationException");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void ParsFor_NullRuntimeDatabase_Throws()
        {
            _runtimeDbField!.SetValue(null, null);
            var ex = Assert.Throws<TargetInvocationException>(
                () => CallParsFor(new[] { "1" }));
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                "Null RuntimeDatabase must throw InvalidOperationException");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4. REAL ItemRewardServiceAdapter.Grant — production type via reflection
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests the REAL ItemRewardServiceAdapter.Grant against a live SaveDataHost
    /// (with NullPersister). The adapter type is in Assembly-CSharp — reached via
    /// Activator.CreateInstance after Type lookup.
    /// </summary>
    [TestFixture]
    public class RealItemRewardAdapterTests
    {
        private GameObject? _saveGo;
        private SaveDataHost? _host;
        private object? _savedSaveDataHost;
        private object? _adapter;

        [SetUp]
        public void SetUp()
        {
            _savedSaveDataHost = SaveDataHost.Instance;

            // Clear stale instance so our test AddComponent takes effect
            {
                var f = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, null);
            }

            _saveGo = new GameObject("TEST_SaveDataHost_ItemAdapter");
            _host   = (SaveDataHost)_saveGo.AddComponent<SaveDataHost>();
            _host.SetPersister(new NullPersister());
            // The fake boot must be COMPLETE, not just present. EditMode never calls Awake, so
            // nothing has read the save — and CharacterManager asserts on SaveDataHost.IsLoaded
            // (content_kill_switch_and_order §2). ReloadFromDisk is the load Awake would have done.
            _host.ReloadFromDisk();

            // If Awake didn't set Instance (non-generic AddComponent edge case), force-set
            if (SaveDataHost.Instance == null)
            {
                var f = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, _host);
            }

            _adapter = Activator.CreateInstance(
                AsmCSharp.GetType("Golfin.Tournaments.ItemRewardServiceAdapter"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_saveGo != null) UnityEngine.Object.DestroyImmediate(_saveGo);
            _saveGo  = null;
            _host    = null;
            _adapter = null;

            // Restore pre-existing SaveDataHost
            {
                var f = typeof(SaveDataHost).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, _savedSaveDataHost);
            }
        }

        private void Grant(string? itemId, int qty)
        {
            _adapter!.GetType()
                .GetMethod("Grant", BindingFlags.Instance | BindingFlags.Public)!
                .Invoke(_adapter, new object?[] { itemId, qty });
        }

        [Test]
        public void Grant_NewItem_CreatesKey()
        {
            Grant("repair_kit", 3);
            Assert.IsTrue(_host!.Data.itemQuantities.ContainsKey("repair_kit"),
                "Grant must create 'repair_kit' key in SaveData.itemQuantities");
            Assert.AreEqual(3, _host.Data.itemQuantities["repair_kit"],
                "Grant(3) must set qty=3 for new key");
        }

        [Test]
        public void Grant_ExistingItem_Increments()
        {
            _host!.Data.itemQuantities["repair_kit"] = 5;
            Grant("repair_kit", 2);
            Assert.AreEqual(7, _host.Data.itemQuantities["repair_kit"],
                "Grant must increment: 5 + 2 = 7");
        }

        [Test]
        public void Grant_ZeroQty_IsNoOp()
        {
            Grant("repair_kit", 0);
            Assert.IsFalse(_host!.Data.itemQuantities.ContainsKey("repair_kit"),
                "Grant(qty=0) must be a no-op — no key created");
        }

        [Test]
        public void Grant_NegativeQty_IsNoOp()
        {
            Grant("repair_kit", -1);
            Assert.IsFalse(_host!.Data.itemQuantities.ContainsKey("repair_kit"),
                "Grant(qty=-1) must be a no-op — no key created");
        }

        [Test]
        public void Grant_NullItemId_IsNoOp()
        {
            Grant(null, 5);
            Assert.AreEqual(0, _host!.Data.itemQuantities.Count,
                "Grant(null, qty) must be a no-op — no key created");
        }

        [Test]
        public void Grant_EmptyItemId_IsNoOp()
        {
            Grant("", 5);
            Assert.AreEqual(0, _host!.Data.itemQuantities.Count,
                "Grant('', qty) must be a no-op — no key created");
        }

        [Test]
        public void Grant_ValidGrant_CallsMarkDirty()
        {
            Grant("ball", 1);
            var f = typeof(SaveDataHost).GetField("_pendingWrite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "_pendingWrite field must exist on SaveDataHost");
            Assert.IsTrue((bool)f!.GetValue(_host)!,
                "MarkDirty() must set _pendingWrite=true after a valid Grant");
        }
    }
}
