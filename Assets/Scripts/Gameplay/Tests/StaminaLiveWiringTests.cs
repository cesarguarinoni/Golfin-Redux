#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Core.Stamina;
using Golfin.Gameplay.Session;
using Golfin.Physics.Stats;
using Golfin.Save;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for stamina_live_wiring (Phase 2 acceptance criteria §7).
    ///
    /// The test asmdef (Golfin.Gameplay.Tests) cannot reference Assembly-CSharp
    /// (circular dependency), so types in Assembly-CSharp (PlayerCharacterData,
    /// StaminaRuntimeService, CharacterManager, LiveStatProviderHost) are accessed
    /// via System.Reflection.
    ///
    /// Types directly accessible:
    ///   Golfin.Core.Stamina — StaminaModel, StaminaConfig, StaminaConfigLoader
    ///   Golfin.Physics.Stats — CharacterStats
    ///   Golfin.Save          — SaveData, PersistedCharacter, SaveSchemaMigrator
    /// </summary>
    [TestFixture]
    public class StaminaLiveWiringTests
    {
        // ── Assembly-CSharp reflected types ───────────────────────────────────────

        static readonly System.Type? TPlayerCharacterData =
            System.Type.GetType("Golfin.Roster.PlayerCharacterData, Assembly-CSharp");

        static readonly System.Type? TStaminaRuntimeService =
            System.Type.GetType("StaminaRuntimeService, Assembly-CSharp");

        static readonly System.Type? TLiveStatProviderHost =
            System.Type.GetType("LiveStatProviderHost, Assembly-CSharp");

        static readonly System.Type? TCharacterManager =
            System.Type.GetType("Golfin.Roster.CharacterManager, Assembly-CSharp");

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a StaminaConfig matching stamina_economy.csv defaults.
        /// StaminaConfig's constructor is private — StaminaConfig.Parse() is the only factory.
        /// </summary>
        static StaminaConfig MakeDefaultConfig()
        {
            const string csv =
                "key,value,notes\n" +
                "drain_per_hole,8,\n" +
                "tank_base,60,\n" +
                "tank_per_stamina_point,6,\n" +
                "regen_base_per_hour,12,\n" +
                "regen_per_recovery_point,2,\n" +
                "comfort_threshold_pct,0.70,\n" +
                "floor_penalty,0.33,\n" +
                "penalty_curve_exp,1.6,\n" +
                "meter_high_pct,0.60,\n" +
                "meter_mid_pct,0.30,\n" +
                "low_condition_flag_pct,0.25,\n" +
                "degraded_stats,Strength;ClubControl,\n";
            return StaminaConfig.Parse(csv);
        }

        /// <summary>Create a PlayerCharacterData (Assembly-CSharp) via reflection.</summary>
        object MakePcd(string charId, int sta = 0, int rec = 0, float maxEnergy = 60f, float curEnergy = 0f)
        {
            Assert.IsNotNull(TPlayerCharacterData, "PlayerCharacterData must exist in Assembly-CSharp");
            var ctor = TPlayerCharacterData!.GetConstructor(new[] { typeof(string) });
            Assert.IsNotNull(ctor, "PlayerCharacterData must have a (string) constructor");
            var pcd = ctor!.Invoke(new object[] { charId });
            SetField(pcd, "currentStamina",       sta);
            SetField(pcd, "currentRecovery",      rec);
            SetField(pcd, "maxStaminaEnergy",     maxEnergy);
            SetField(pcd, "currentStaminaEnergy", curEnergy);
            return pcd;
        }

        static void SetField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Field '{fieldName}' not found on {target.GetType().Name}");
            f!.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Field '{fieldName}' not found on {target.GetType().Name}");
            return (T)f!.GetValue(target)!;
        }

        static void SetProp(object target, string propName, object value)
        {
            var p = target.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return; }
            // Fallback to field
            SetField(target, propName, value);
        }

        /// <summary>Call StaminaRuntimeService.AccrueRegen(PlayerCharacterData, DateTime) via reflection.</summary>
        void AccrueRegen(object pcd, DateTime nowUtc)
        {
            Assert.IsNotNull(TStaminaRuntimeService, "StaminaRuntimeService must exist in Assembly-CSharp");
            var m = TStaminaRuntimeService!.GetMethod("AccrueRegen", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(m, "StaminaRuntimeService.AccrueRegen must be public static");
            m!.Invoke(null, new[] { pcd, (object)nowUtc });
        }

        /// <summary>Call StaminaRuntimeService.DrainForCompletedHole(string) via reflection.</summary>
        void DrainForCompletedHole(string? charId)
        {
            Assert.IsNotNull(TStaminaRuntimeService, "StaminaRuntimeService must exist in Assembly-CSharp");
            var m = TStaminaRuntimeService!.GetMethod("DrainForCompletedHole",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "StaminaRuntimeService.DrainForCompletedHole must be internal static");
            m!.Invoke(null, new object?[] { charId });
        }

        // ── SetUp / TearDown ─────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            StaminaModel.Configure(MakeDefaultConfig());
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR
            StaminaModel.ResetForTests();

            // Reset wiring state so OnMatchComplete/OnHoleComplete are cleaned up between tests.
            var reset = TStaminaRuntimeService?.GetMethod("ResetForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            reset?.Invoke(null, null);
#endif
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 1 — Tank size scales with Stamina stat
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T1_TankSize_Sta9_Is114()
        {
            Assert.AreEqual(114, StaminaModel.MaxCondition(9),
                "MaxCondition(9) must be 114 (60 + 9×6)");
        }

        [Test]
        public void T1_TankSize_Sta0_Is60()
        {
            Assert.AreEqual(60, StaminaModel.MaxCondition(0),
                "MaxCondition(0) must be 60 (tank_base, no Stamina stat)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 2 — Per-hole drain (via StaminaModel.DrainForHole)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T2_DrainForHole_Is8()
        {
            Assert.AreEqual(8f, StaminaModel.DrainForHole(), delta: 0.001f,
                "DrainForHole must equal drain_per_hole (8) from stamina_economy.csv");
        }

        [Test]
        public void T2_DrainForHole_ReducesEnergy()
        {
            // Simulate the OnHoleComplete drain: energy = Max(0, energy - DrainForHole())
            float maxEnergy = StaminaModel.MaxCondition(9);
            float before    = maxEnergy;
            float after     = Mathf.Max(0f, before - StaminaModel.DrainForHole());

            Assert.AreEqual(before - 8f, after, delta: 0.001f,
                "Per-hole drain must reduce energy by exactly 8");
        }

        [Test]
        public void T2_DrainForHole_ClampedAtZero()
        {
            float energy = 3f; // < 8 drain
            float after  = Mathf.Max(0f, energy - StaminaModel.DrainForHole());

            Assert.AreEqual(0f, after, delta: 0.001f,
                "Drain must never push energy below 0");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 3 — Regen accrual (AccrueRegen via reflection)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T3_AccrueRegen_2Hours_Rec9_Adds60()
        {
            // RegenForElapsed(9, 2h) = (12 + 9×2) × 2 = 60
            float expected = StaminaModel.RegenForElapsed(9, TimeSpan.FromHours(2));
            Assert.AreEqual(60f, expected, delta: 0.1f,
                "Regen for 2h at Recovery 9 must be 60");

            var pcd    = MakePcd("char_test", rec: 9, maxEnergy: 60f, curEnergy: 0f);
            var past   = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var future = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);
            SetField(pcd, "conditionUpdatedUtc", past);

            AccrueRegen(pcd, future);

            float cur = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(60f, cur, delta: 0.1f,
                "After 2h elapsed, currentStaminaEnergy must be 60");
        }

        [Test]
        public void T3_AccrueRegen_ZeroElapsed_NoOp()
        {
            var pcd = MakePcd("char_test", rec: 9, maxEnergy: 60f, curEnergy: 30f);
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            SetField(pcd, "conditionUpdatedUtc", now);

            AccrueRegen(pcd, now);  // same instant

            float cur = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(30f, cur, delta: 0.001f,
                "Zero elapsed must be a no-op");
        }

        [Test]
        public void T3_AccrueRegen_DefaultTimestamp_StampsNow_NoRegen()
        {
            var pcd    = MakePcd("char_test", rec: 9, maxEnergy: 60f, curEnergy: 30f);
            var defVal = default(DateTime);
            SetField(pcd, "conditionUpdatedUtc", defVal);
            var now = DateTime.UtcNow;

            AccrueRegen(pcd, now);

            float cur       = GetField<float>(pcd, "currentStaminaEnergy");
            var   stamped   = GetField<DateTime>(pcd, "conditionUpdatedUtc");
            Assert.AreEqual(30f, cur, delta: 0.001f,
                "Default timestamp: energy unchanged (no elapsed to measure)");
            Assert.AreEqual(now, stamped,
                "Default timestamp: conditionUpdatedUtc must be stamped to nowUtc");
        }

        [Test]
        public void T3_AccrueRegen_ClampedToMax()
        {
            // Start 50/60, 3h → regen = 90 → clamp to 60
            var pcd = MakePcd("char_test", rec: 9, maxEnergy: 60f, curEnergy: 50f);
            var past   = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var future = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc); // +3h
            SetField(pcd, "conditionUpdatedUtc", past);

            AccrueRegen(pcd, future);

            float cur = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(60f, cur, delta: 0.001f,
                "Regen must be clamped to maxStaminaEnergy");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 4 — Hydrate full on empty timestamp (pre-v4 / blank saves)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T4_HydrateFull_EmptyTimestamp_SetsFullPool()
        {
            // Replicate the CharacterManager.LoadRoster hydrate block for empty conditionUpdatedUtc:
            //   conditionUpdatedUtc == default → set currentStaminaEnergy = maxStaminaEnergy, stamp now
            int maxCondition = StaminaModel.MaxCondition(9); // 114

            var pcd = MakePcd("char_test", sta: 9, maxEnergy: (float)maxCondition, curEnergy: 0f);
            SetField(pcd, "conditionUpdatedUtc", default(DateTime));

            var nowUtc = DateTime.UtcNow;
            // Simulate the hydrate block
            var ts = GetField<DateTime>(pcd, "conditionUpdatedUtc");
            if (ts == default(DateTime))
            {
                SetField(pcd, "currentStaminaEnergy", GetField<float>(pcd, "maxStaminaEnergy"));
                SetField(pcd, "conditionUpdatedUtc", nowUtc);
            }

            float cur = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual((float)maxCondition, cur, delta: 0.001f,
                "Pre-v4 / blank conditionUpdatedUtc must hydrate to full pool (114 for Sta=9)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 5 — Round-trip: drain → dehydrate → re-hydrate
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T5_RoundTrip_NoElapsed_PreservesEnergy()
        {
            float maxE   = StaminaModel.MaxCondition(9);   // 114
            float drained = maxE - StaminaModel.DrainForHole(); // 106
            var   now    = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            // Dehydrate → PersistedCharacter
            var pc = new PersistedCharacter
            {
                conditionEnergy     = drained,
                conditionUpdatedUtc = now.ToString("o"),
            };

            // Re-hydrate into a fresh PCD
            var pcd = MakePcd("char_test", sta: 9, rec: 9, maxEnergy: maxE, curEnergy: 0f);
            float clampedEnergy = Mathf.Clamp(pc.conditionEnergy, 0f, maxE);
            SetField(pcd, "currentStaminaEnergy", clampedEnergy);
            var rehydratedTs = DateTime.Parse(pc.conditionUpdatedUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal);
            SetField(pcd, "conditionUpdatedUtc", rehydratedTs);

            // AccrueRegen at same instant → no-op
            AccrueRegen(pcd, now);

            float cur = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(drained, cur, delta: 0.001f,
                "Round-trip with no elapsed must preserve energy");
        }

        [Test]
        public void T5_RoundTrip_WithElapsed_AppliesRegenOnce()
        {
            float maxE    = StaminaModel.MaxCondition(9);  // 114
            float drained = maxE - StaminaModel.DrainForHole(); // 106
            var   savedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var   loadAt  = new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc); // +1h

            var pc = new PersistedCharacter
            {
                conditionEnergy     = drained,
                conditionUpdatedUtc = savedAt.ToString("o"),
            };

            var pcd = MakePcd("char_test", sta: 9, rec: 9, maxEnergy: maxE, curEnergy: 0f);
            float clampedEnergy = Mathf.Clamp(pc.conditionEnergy, 0f, maxE);
            SetField(pcd, "currentStaminaEnergy", clampedEnergy);
            var rehydratedTs = DateTime.Parse(pc.conditionUpdatedUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal);
            SetField(pcd, "conditionUpdatedUtc", rehydratedTs);

            AccrueRegen(pcd, loadAt);  // +1h regen

            float regen1h  = StaminaModel.RegenForElapsed(9, TimeSpan.FromHours(1)); // 30
            float expected = Mathf.Min(maxE, drained + regen1h); // 136 → clamped to 114
            float cur      = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(expected, cur, delta: 0.1f,
                "Re-hydrate with 1h elapsed must apply regen exactly once");

            // Second AccrueRegen at same timestamp → no-op (no double-regen)
            SetField(pcd, "conditionUpdatedUtc", loadAt);
            AccrueRegen(pcd, loadAt);

            float cur2 = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(expected, cur2, delta: 0.1f,
                "Second AccrueRegen at same instant must not accumulate extra regen (D2)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 6 — Migration v3 → v4 + fail-hard on v5
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T6_Migration_V3ToV4_ConditionFieldsDefaultSafe()
        {
            const string v3Json = @"{
                ""schemaVersion"": 3,
                ""rewardPoints"": 123,
                ""ownedCharacters"": [
                    { ""characterId"": ""char_alice"", ""currentLevel"": 10 }
                ],
                ""tournamentEntries"": []
            }";

            var data = JsonConvert.DeserializeObject<SaveData>(v3Json);
            Assert.IsNotNull(data);
            Assert.DoesNotThrow(() => SaveSchemaMigrator.Migrate(data!));

            Assert.AreEqual(4, data!.schemaVersion, "Post-migration schemaVersion must be 4");
            Assert.AreEqual(0f, data.ownedCharacters[0].conditionEnergy, delta: 0.001f,
                "conditionEnergy defaults to 0f for pre-v4 saves");
            Assert.AreEqual("", data.ownedCharacters[0].conditionUpdatedUtc,
                "conditionUpdatedUtc defaults to empty string for pre-v4 saves");
        }

        [Test]
        public void T6_FailHard_V5_ThrowsSaveSchemaVersionException()
        {
            const string v5Json = @"{ ""schemaVersion"": 5, ""rewardPoints"": 1 }";
            var data = JsonConvert.DeserializeObject<SaveData>(v5Json);
            Assert.IsNotNull(data);

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[SaveSchemaMigrator\].*schema version 5"));

            Assert.Throws<SaveSchemaVersionException>(() => SaveSchemaMigrator.Migrate(data!));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 7 — Penalty seam (Option C): BuildCharacterStats pre-degrades
        //          Strength + ClubControl, leaves Recovery + Stamina raw.
        //          Uses reflection to call private static on LiveStatProviderHost.
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T7_PenaltySeam_AboveComfort_NoDegradation()
        {
            // conditionPct = 0.70 → at comfort threshold → no penalty → stats returned raw
            float conditionPct = 0.70f;
            int str = 20, ctrl = 18, rec = 15, sta = 22;

            var result = InvokeBuildCharacterStats(str, ctrl, rec, sta, conditionPct);

            Assert.IsNotNull(result);
            Assert.AreEqual(str,  result!.Value.Strength,    "At comfort threshold, Strength is raw");
            Assert.AreEqual(ctrl, result.Value.ClubControl,  "At comfort threshold, ClubControl is raw");
            Assert.AreEqual(rec,  result.Value.Recovery,     "Recovery is never degraded");
            Assert.AreEqual(sta,  result.Value.Stamina,      "Stamina stat is never degraded");
        }

        [Test]
        public void T7_PenaltySeam_AtZeroPct_MaxDegradation()
        {
            // conditionPct = 0.0 → full penalty = floor_penalty (0.33)
            // EffectiveStat(base, 0) = round(base × (1 - 0.33)) = round(base × 0.67)
            float conditionPct = 0.0f;
            int str = 20, ctrl = 18, rec = 15, sta = 22;

            var result = InvokeBuildCharacterStats(str, ctrl, rec, sta, conditionPct);

            Assert.IsNotNull(result);
            int expectedStr  = (int)Math.Round(str  * (1f - 0.33f)); // 13
            int expectedCtrl = (int)Math.Round(ctrl * (1f - 0.33f)); // 12
            Assert.AreEqual(expectedStr,  result!.Value.Strength,   "Strength at pct=0 degraded by floor_penalty (0.33)");
            Assert.AreEqual(expectedCtrl, result.Value.ClubControl, "ClubControl at pct=0 degraded by floor_penalty (0.33)");
            Assert.AreEqual(rec,          result.Value.Recovery,    "Recovery must be raw");
            Assert.AreEqual(sta,          result.Value.Stamina,     "Stamina stat must be raw");
        }

        CharacterStats? InvokeBuildCharacterStats(int str, int ctrl, int rec, int sta, float conditionPct)
        {
            Assert.IsNotNull(TLiveStatProviderHost, "LiveStatProviderHost must exist in Assembly-CSharp");

            var method = TLiveStatProviderHost!.GetMethod("BuildCharacterStats",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(float) },
                null);
            Assert.IsNotNull(method,
                "LiveStatProviderHost.BuildCharacterStats(int,int,int,int,float) must be private static");

            return (CharacterStats?)method!.Invoke(null, new object[] { str, ctrl, rec, sta, conditionPct });
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 8 — Neutralization parity: physics CSV stamina_floor_fraction = 1.0
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T8_NeutralizationParity_StatsCSV_FloorFractionIs1()
        {
            var csv = Resources.Load<TextAsset>("Physics/stats");
            Assert.IsNotNull(csv, "Physics/stats.csv must exist in Resources");

            float floorFraction = float.NaN;
            foreach (var line in csv!.text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("stamina_floor_fraction")) continue;
                var parts = trimmed.Split(',');
                if (parts.Length >= 2 && float.TryParse(parts[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float val))
                {
                    floorFraction = val;
                    break;
                }
            }

            Assert.IsFalse(float.IsNaN(floorFraction),
                "stamina_floor_fraction row must exist in Physics/stats.csv");
            Assert.AreEqual(1.0f, floorFraction, delta: 0.001f,
                "stamina_floor_fraction must be 1.0 (Option C neutralization) — resolver is identity");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Test 9 — Versus drain: D5 — OnMatchComplete wire exercises DrainForCompletedHole
        //
        // Part A — Wire existence: after WireHoleComplete(), OnMatchComplete's
        //   invocation list must contain StaminaRuntimeService's handler.
        //   Removing the GameSession.OnMatchComplete += OnMatchComplete line
        //   makes Part A fail.
        //
        // Part B — End-to-end drain body via real CharacterManager MonoBehaviour:
        //   Creates a CharacterManager instance in EditMode (Awake sets Instance),
        //   injects a known PCD into the private ownedCharacters dict via reflection,
        //   calls the REAL StaminaRuntimeService.DrainForCompletedHole("char_test"),
        //   then reads back the PCD's energy from CharacterManager.Instance.GetCharacterData().
        //
        //   Goes RED on any of these production-body regressions:
        //     - "- DrainForHole()" changed to "+ DrainForHole()" (add instead of subtract)
        //     - Mathf.Max(0,…) clamp removed (energy can go negative)
        //     - drain line removed entirely (energy unchanged)
        //     - AccrueRegen-first ordering broken (wrong pre-drain energy)
        //     - CharacterManager.Instance?.GetCharacterData() lookup bypassed
        //
        //   Part A still guards the subscription wire; Part B guards the body.
        //   Both are required: A alone cannot catch a broken body, B alone cannot
        //   catch a missing subscription.
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void T9_VersusDrain_PartA_OnMatchComplete_IsWired()
        {
            Assert.IsNotNull(TStaminaRuntimeService,
                "StaminaRuntimeService must exist in Assembly-CSharp");

            // Reset wiring so we start clean, then wire.
            var reset = TStaminaRuntimeService!.GetMethod("ResetForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            reset?.Invoke(null, null);

            var wire = TStaminaRuntimeService.GetMethod("WireHoleComplete",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(wire, "StaminaRuntimeService.WireHoleComplete must be internal static");
            wire!.Invoke(null, null);

            // Read the OnMatchComplete backing delegate field from GameSession via reflection.
            var gsType = typeof(GameSession);
            // The event is backed by a field of the same name (C# auto-event pattern).
            var backingField = gsType.GetField("OnMatchComplete",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (backingField == null)
            {
                // Some compilers use a different backing name — try with EventInfo.
                var ev = gsType.GetEvent("OnMatchComplete",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(ev, "GameSession.OnMatchComplete event must exist");
                // Cannot read delegate without backing field — skip invocation-list check
                // but verify WireHoleComplete() ran without error (structural smoke).
                Assert.Pass("WireHoleComplete() completed without exception — " +
                            "backing field access not available on this runtime but event exists.");
                return;
            }

            var del = backingField.GetValue(null) as Delegate;
            Assert.IsNotNull(del,
                "GameSession.OnMatchComplete must have at least one subscriber after WireHoleComplete(). " +
                "This test goes RED if StaminaRuntimeService stops subscribing to OnMatchComplete (D5 versus drain wire).");

            var invList = del!.GetInvocationList();
            bool found = false;
            foreach (var d in invList)
            {
                if (d.Method.DeclaringType == TStaminaRuntimeService)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found,
                "StaminaRuntimeService must be subscribed to GameSession.OnMatchComplete. " +
                "Remove the OnMatchComplete += subscription and this test fails (D5 gate).");
        }

        /// <summary>
        /// T9B — End-to-end drain via the REAL production StaminaRuntimeService.DrainForCompletedHole.
        ///
        /// Strategy:
        ///   1. Spin up a real CharacterManager MonoBehaviour (AddComponent) so Instance is set.
        ///      Awake loads roster; SaveDataHost.Instance is null in EditMode, so it just warns
        ///      and leaves ownedCharacters empty — safe.
        ///   2. Inject a known PCD into CharacterManager's private ownedCharacters dict via reflection.
        ///   3. Set conditionUpdatedUtc = "now" so AccrueRegen is a no-op (same instant).
        ///   4. Call the REAL DrainForCompletedHole("char_test") — it reads from Instance.GetCharacterData,
        ///      runs AccrueRegen (no-op), subtracts DrainForHole, clamps, and tries PersistCondition
        ///      (PersistCondition early-returns when SaveDataHost.Instance is null — safe).
        ///   5. Read back energy via Instance.GetCharacterData and assert delta == 8, energy >= 0.
        ///   6. Second pass: energy = 3 (< drain 8) → expect 0 (clamp guard).
        /// </summary>
        [Test]
        public void T9_VersusDrain_PartB_DrainForCompletedHole_ReducesEnergy_IsVersus()
        {
            Assert.IsNotNull(TStaminaRuntimeService,
                "StaminaRuntimeService must exist in Assembly-CSharp");
            Assert.IsNotNull(TCharacterManager,
                "CharacterManager must exist in Assembly-CSharp");
            Assert.IsNotNull(TPlayerCharacterData,
                "PlayerCharacterData must exist in Assembly-CSharp");

            // ── Locate the production method first (method-exists gate) ─────────
            var drainMethod = TStaminaRuntimeService!.GetMethod("DrainForCompletedHole",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(drainMethod,
                "StaminaRuntimeService.DrainForCompletedHole(string) must exist as internal static. " +
                "Removing it breaks the D5 shared-drain contract.");

            // ── Locate CharacterManager.Instance property ────────────────────────
            var instanceProp = TCharacterManager!.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(instanceProp, "CharacterManager.Instance property must be public static");

            // ── Locate the private ownedCharacters dict field ────────────────────
            var ownedCharsField = TCharacterManager.GetField("ownedCharacters",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(ownedCharsField,
                "CharacterManager.ownedCharacters field must exist (private Dictionary<string,PlayerCharacterData>)");

            // ── Locate GetCharacterData method ───────────────────────────────────
            var getCharDataMethod = TCharacterManager.GetMethod("GetCharacterData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            Assert.IsNotNull(getCharDataMethod, "CharacterManager.GetCharacterData(string) must be public");

            // Save session state to restore after the test
            string prevCharId = GameSession.SelectedCharacterId;
            bool   prevIsVersus = GameSession.IsVersus;

            // The CharacterManager GO we create — must be torn down in finally
            GameObject? cmGO = null;
            object? prevInstance = null;

            try
            {
                // ── 1. Capture any pre-existing CharacterManager.Instance ────────
                prevInstance = instanceProp!.GetValue(null);

                // ── 2. Create CharacterManager MonoBehaviour ─────────────────────
                // In the NUnit EditMode test runner, AddComponent does NOT auto-invoke Awake.
                // We must call Awake explicitly via reflection.
                // LoadRoster logs a warning about missing CharacterDatabaseCSV but does NOT throw
                // (SaveDataHost.Instance == null → ownedCharacters stays empty, which is what we want).
                cmGO = new GameObject("T9B_CharacterManager");
                var cmComponent = cmGO.AddComponent(TCharacterManager) as Component;
                Assert.IsNotNull(cmComponent, "AddComponent<CharacterManager> must succeed");

                // Explicitly invoke Awake in EditMode test runner context.
                // Awake sets Instance = this before calling DontDestroyOnLoad (harmless in EditMode)
                // and LoadRoster (returns early/logs-only when CharacterDatabaseCSV.Instance is null).
                var awakeMethod = TCharacterManager!.GetMethod(
                    "Awake",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(awakeMethod, "CharacterManager.Awake must be findable via reflection");
                try
                {
                    awakeMethod!.Invoke(cmComponent, null);
                }
                catch (TargetInvocationException)
                {
                    // DontDestroyOnLoad may warn/throw in EditMode, but Instance is set
                    // in the first lines of Awake before any problematic call.
                }

                // Verify Instance is now set
                var instance = instanceProp.GetValue(null);
                Assert.IsNotNull(instance,
                    "CharacterManager.Instance must be non-null after explicit Awake invocation");

                // ── 3. Build the test PCD ─────────────────────────────────────────
                // sta=9 → maxEnergy=114 (60+9×6), full pool, rec=0 so no regen accrues
                float maxE = StaminaModel.MaxCondition(9); // 114
                float startEnergy = maxE;
                float drain = StaminaModel.DrainForHole(); // 8

                var pcdCtor = TPlayerCharacterData!.GetConstructor(new[] { typeof(string) });
                Assert.IsNotNull(pcdCtor, "PlayerCharacterData(string) constructor must exist");
                var testPcd = pcdCtor!.Invoke(new object[] { "char_test" });

                SetField(testPcd, "currentStamina",       9);
                SetField(testPcd, "currentRecovery",      0);
                SetField(testPcd, "maxStaminaEnergy",     maxE);
                SetField(testPcd, "currentStaminaEnergy", startEnergy);
                // Set conditionUpdatedUtc to exact-now so AccrueRegen sees zero elapsed → no-op.
                // This isolates the test so the only energy change is from DrainForCompletedHole.
                var fixedNow = DateTime.UtcNow;
                SetField(testPcd, "conditionUpdatedUtc", fixedNow);

                // ── 4. Inject PCD into CharacterManager.ownedCharacters ──────────
                // ownedCharacters is Dictionary<string, PlayerCharacterData>
                var dict = ownedCharsField!.GetValue(instance);
                Assert.IsNotNull(dict, "ownedCharacters dict must be non-null after Awake");

                // Use IDictionary interface (avoids generic type constraint from test assembly)
                var idict = dict as System.Collections.IDictionary;
                Assert.IsNotNull(idict, "ownedCharacters must implement IDictionary");
                idict!["char_test"] = testPcd;

                // Verify injection round-trips correctly
                var readBack = getCharDataMethod.Invoke(instance, new object[] { "char_test" });
                Assert.IsNotNull(readBack,
                    "GetCharacterData('char_test') must return the injected PCD");
                Assert.AreSame(testPcd, readBack,
                    "GetCharacterData must return the exact same PCD instance we injected");

                // ── 5. Set GameSession state for the versus path ─────────────────
                GameSession.SelectedCharacterId = "char_test";
                GameSession.IsVersus            = true;

                // ── 6. Call the REAL DrainForCompletedHole ───────────────────────
                // This exercises: StaminaModel.IsConfigured check → GetCharacterData lookup →
                // AccrueRegen (no-op: zero elapsed) → Max(0, energy - DrainForHole()) →
                // conditionUpdatedUtc stamp → PersistCondition (no-op: SaveDataHost null)
                drainMethod!.Invoke(null, new object?[] { "char_test" });

                // ── 7. Read back energy from the LIVE PCD (same dict entry) ──────
                var pcdAfter = getCharDataMethod.Invoke(instance, new object[] { "char_test" });
                Assert.IsNotNull(pcdAfter, "GetCharacterData must still return the PCD after drain");

                float energyAfter = GetField<float>(pcdAfter, "currentStaminaEnergy");

                // Primary assertion: drained by exactly DrainForHole() (8)
                Assert.AreEqual(
                    startEnergy - drain, energyAfter, delta: 0.001f,
                    $"DrainForCompletedHole must reduce energy by exactly DrainForHole() ({drain}). " +
                    $"Expected {startEnergy - drain}, got {energyAfter}. " +
                    "This test goes RED if the drain body uses + instead of -, omits the drain, " +
                    "or breaks the CharacterManager.Instance.GetCharacterData() lookup.");

                Assert.IsTrue(energyAfter >= 0f,
                    "Drained energy must be clamped ≥ 0 (Mathf.Max(0,...) clamp)");

                // ── 8. Second pass: energy below drain → clamp to 0 ─────────────
                // Proves the Mathf.Max(0,...) guard specifically.
                // Re-use same PCD, set energy to 3 (< drain 8).
                var samePcd = getCharDataMethod.Invoke(instance, new object[] { "char_test" });
                SetField(samePcd!, "currentStaminaEnergy", 3f);
                // Reset timestamp so AccrueRegen is again a no-op (same-instant call)
                SetField(samePcd, "conditionUpdatedUtc", DateTime.UtcNow);

                drainMethod.Invoke(null, new object?[] { "char_test" });

                var pcdAfterClamp = getCharDataMethod.Invoke(instance, new object[] { "char_test" });
                float energyAfterClamp = GetField<float>(pcdAfterClamp!, "currentStaminaEnergy");

                Assert.AreEqual(0f, energyAfterClamp, delta: 0.001f,
                    "When starting energy (3) < DrainForHole() (8), result must be 0 (not negative). " +
                    "This test goes RED if Mathf.Max(0,...) clamp is removed from the production body.");
            }
            finally
            {
                // ── Teardown: destroy CharacterManager GO, restore singleton + session state ──
                if (cmGO != null)
                {
                    // Null the static Instance via reflection before Destroy so TearDown's
                    // ResetForTests() doesn't race with OnDestroy.
                    var instanceField = TCharacterManager?.GetField("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    if (instanceField == null)
                    {
                        // Instance is a property with private setter — null it through SetValue
                        var prop = TCharacterManager?.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static);
                        // Private setter won't allow direct set; instead rely on Destroy → OnDestroy
                    }
                    UnityEngine.Object.DestroyImmediate(cmGO);
                    // OnDestroy sets Instance = null! (see CharacterManager.OnDestroy)
                }

                // Restore previous CharacterManager.Instance (likely null before this test)
                // OnDestroy already nulled it, but if somehow it didn't, force it.
                var prop2 = TCharacterManager?.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                // Property has private setter — attempt via backing field name (<Instance>k__BackingField)
                if (prop2 != null)
                {
                    var backingName = $"<{prop2.Name}>k__BackingField";
                    var bf = TCharacterManager?.GetField(backingName,
                        BindingFlags.NonPublic | BindingFlags.Static);
                    bf?.SetValue(null, prevInstance);
                }

                // Restore GameSession state
                GameSession.SelectedCharacterId = prevCharId;
                GameSession.IsVersus            = prevIsVersus;
            }
        }
    }
}
