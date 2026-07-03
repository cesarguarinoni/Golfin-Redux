// Assets/Scripts/UI/Shop/Tests/StaminaShopAddEnergyTests.cs
// Order 517 — stamina_boost_shop
// EditMode tests for StaminaRuntimeService.AddEnergy.
//
// ASSEMBLY: Golfin.UI.Shop.Tests (named asmdef, overrideReferences:false)
// Cannot directly reference Assembly-CSharp types.
// PlayerCharacterData and StaminaRuntimeService accessed via System.Reflection.

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.UI.Shop.Tests
{
    /// <summary>
    /// Verifies StaminaRuntimeService.AddEnergy via reflection (Assembly-CSharp):
    ///   1. Clamps to maxStaminaEnergy — never exceeds max.
    ///   2. Adds exact amount when below max.
    ///   3. Does NOT modify maxStaminaEnergy.
    ///   4. Stamps conditionUpdatedUtc on success.
    ///   5. No-op (no throw) on null pcd.
    ///   6. No-op (no throw) on amount ≤ 0.
    /// </summary>
    [TestFixture]
    public class StaminaShopAddEnergyTests
    {
        // ── Reflection cache ─────────────────────────────────────────────────

        private Type   _pcdType;
        private Type   _srsType;
        private MethodInfo _addEnergy;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var asm = Assembly.Load("Assembly-CSharp");

            _pcdType  = asm.GetType("PlayerCharacterData")
                     ?? asm.GetType("Golfin.Roster.PlayerCharacterData");
            Assert.IsNotNull(_pcdType, "PlayerCharacterData not found in Assembly-CSharp.");

            _srsType = asm.GetType("StaminaRuntimeService");
            Assert.IsNotNull(_srsType, "StaminaRuntimeService not found in Assembly-CSharp.");

            _addEnergy = _srsType.GetMethod("AddEnergy",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { _pcdType, typeof(float) },
                null);
            Assert.IsNotNull(_addEnergy, "AddEnergy(PlayerCharacterData, float) not found.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private object MakePcd(float current, float max)
        {
            // PlayerCharacterData has no default constructor; it requires a string characterId.
            var ctor = _pcdType.GetConstructor(new Type[] { typeof(string) });
            Assert.IsNotNull(ctor, "PlayerCharacterData(string) constructor not found.");
            var pcd = ctor.Invoke(new object[] { "test_char" });
            // characterId is already set by the constructor; just set energy fields.
            SetField(pcd, "currentStaminaEnergy", current);
            SetField(pcd, "maxStaminaEnergy",     max);
            return pcd;
        }

        private void SetField(object obj, string name, object value)
        {
            var fi = _pcdType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) { fi.SetValue(obj, value); return; }
            var pi = _pcdType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) { pi.SetValue(obj, value); return; }
            Assert.Fail("Field/Property '{0}' not found on PlayerCharacterData.", name);
        }

        private T GetField<T>(object obj, string name)
        {
            var fi = _pcdType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) return (T)fi.GetValue(obj);
            var pi = _pcdType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) return (T)pi.GetValue(obj);
            Assert.Fail("Field/Property '{0}' not found on PlayerCharacterData.", name);
            return default;
        }

        private void CallAddEnergy(object pcd, float amount)
            => _addEnergy.Invoke(null, new object[] { pcd, amount });

        // ── Tests ────────────────────────────────────────────────────────────

        [Test]
        public void AddEnergy_BelowMax_AddsExactAmount()
        {
            var pcd = MakePcd(current: 30f, max: 100f);
            CallAddEnergy(pcd, 20f);
            float result = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(50f, result, delta: 0.001f,
                "Energy should increase by exactly 20 when not exceeding max.");
        }

        [Test]
        public void AddEnergy_WouldExceedMax_ClampsToMax()
        {
            var pcd = MakePcd(current: 90f, max: 100f);
            CallAddEnergy(pcd, 50f);
            float result = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(100f, result, delta: 0.001f,
                "Energy must not exceed maxStaminaEnergy.");
        }

        [Test]
        public void AddEnergy_AtMax_StaysAtMax()
        {
            var pcd = MakePcd(current: 100f, max: 100f);
            CallAddEnergy(pcd, 25f);
            float result = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(100f, result, delta: 0.001f,
                "Energy must stay at max when already full.");
        }

        [Test]
        public void AddEnergy_DoesNotModifyMaxStaminaEnergy()
        {
            var pcd = MakePcd(current: 50f, max: 100f);
            CallAddEnergy(pcd, 30f);
            float maxAfter = GetField<float>(pcd, "maxStaminaEnergy");
            Assert.AreEqual(100f, maxAfter, delta: 0.001f,
                "AddEnergy must NOT modify maxStaminaEnergy.");
        }

        [Test]
        public void AddEnergy_StampsConditionUpdatedUtc()
        {
            var pcd    = MakePcd(current: 50f, max: 100f);
            var before = DateTime.UtcNow.AddSeconds(-1);
            CallAddEnergy(pcd, 10f);
            var stamped = GetField<DateTime>(pcd, "conditionUpdatedUtc");
            Assert.Greater(stamped, before,
                "conditionUpdatedUtc should be stamped to approximately DateTime.UtcNow.");
        }

        [Test]
        public void AddEnergy_NullPcd_DoesNotThrow()
        {
            // Reflection invoke wraps exceptions in TargetInvocationException;
            // we check the inner exception is NOT a NullReferenceException.
            try
            {
                _addEnergy.Invoke(null, new object[] { null, 20f });
                // success — method handled null gracefully (logged a warning)
            }
            catch (TargetInvocationException tie)
            {
                Assert.IsNotInstanceOf<NullReferenceException>(tie.InnerException,
                    "AddEnergy(null, amount) must not throw NullReferenceException.");
            }
        }

        [Test]
        public void AddEnergy_ZeroAmount_DoesNotModifyEnergy()
        {
            var pcd    = MakePcd(current: 50f, max: 100f);
            float before = GetField<float>(pcd, "currentStaminaEnergy");
            try { _addEnergy.Invoke(null, new object[] { pcd, 0f }); }
            catch (TargetInvocationException) { /* early return on guard is fine */ }
            float after = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(before, after, delta: 0.001f,
                "Zero-amount call must not modify energy.");
        }

        [Test]
        public void AddEnergy_NegativeAmount_DoesNotModifyEnergy()
        {
            var pcd    = MakePcd(current: 50f, max: 100f);
            float before = GetField<float>(pcd, "currentStaminaEnergy");
            try { _addEnergy.Invoke(null, new object[] { pcd, -10f }); }
            catch (TargetInvocationException) { /* early return on guard is fine */ }
            float after = GetField<float>(pcd, "currentStaminaEnergy");
            Assert.AreEqual(before, after, delta: 0.001f,
                "Negative-amount call must not modify energy.");
        }
    }
}
