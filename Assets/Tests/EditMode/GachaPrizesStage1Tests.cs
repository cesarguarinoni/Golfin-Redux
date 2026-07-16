// Assets/Tests/EditMode/GachaPrizesStage1Tests.cs
// gacha_prizes Stage 1 — EditMode unit tests
// Tests: mock pool count, SetPendingPullCount, ApplyMode x1/x10 row visibility,
//        x1Card centering in prefab.
//
// Production types (GachaMockPrizePool, GachaPrizesScreenController) live in
// Assembly-CSharp and are accessed via reflection, matching the project pattern.

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaPrizesStage1Tests
    {
        // ── Reflection: production types ──────────────────────────────────────

        private static readonly Type _mockPoolType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaMockPrizePool, Assembly-CSharp");

        private static readonly Type _ctrlType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPrizesScreenController, Assembly-CSharp");

        private static readonly Type _prizeRecordType =
            Type.GetType("GolfinRedux.UI.Gacha.PrizeRecord, Assembly-CSharp");

        // GachaMockPrizePool.GetMockPrizes() → PrizeRecord[]
        private static readonly MethodInfo _getMockPrizes =
            _mockPoolType?.GetMethod("GetMockPrizes", BindingFlags.Public | BindingFlags.Static);

        // GachaMockPrizePool.GetX1Prize() → PrizeRecord
        private static readonly MethodInfo _getX1Prize =
            _mockPoolType?.GetMethod("GetX1Prize", BindingFlags.Public | BindingFlags.Static);

        // GachaPrizesScreenController.SetPendingPullCount(int)
        private static readonly MethodInfo _setPendingPullCount =
            _ctrlType?.GetMethod("SetPendingPullCount", BindingFlags.Public | BindingFlags.Static);

        // GachaPrizesScreenController.ApplyMode(int) — private
        private static readonly MethodInfo _applyMode =
            _ctrlType?.GetMethod("ApplyMode",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);

        // GachaPrizesScreenController.s_pendingPullCount — private static field
        private static readonly FieldInfo _s_pendingPullCount =
            _ctrlType?.GetField("s_pendingPullCount",
                BindingFlags.NonPublic | BindingFlags.Static);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            f?.SetValue(obj, value);
        }

        private static string GetClubId(object prizeRecord)
        {
            var f = _prizeRecordType?.GetField("ClubId",
                BindingFlags.Public | BindingFlags.Instance);
            return (string)(f?.GetValue(prizeRecord) ?? string.Empty);
        }

        // ── Mock pool tests ───────────────────────────────────────────────────

        [Test]
        public void GachaMockPrizePool_GetMockPrizes_Returns10Entries()
        {
            Assert.IsNotNull(_mockPoolType, "GachaMockPrizePool not found in Assembly-CSharp");
            Assert.IsNotNull(_getMockPrizes, "GachaMockPrizePool.GetMockPrizes not found");

            var prizes = (Array)_getMockPrizes.Invoke(null, null);
            Assert.IsNotNull(prizes);
            Assert.AreEqual(10, prizes.Length, "Mock pool must contain exactly 10 prize records");
        }

        [Test]
        public void GachaMockPrizePool_AllEntries_HaveNonEmptyClubId()
        {
            Assert.IsNotNull(_getMockPrizes, "GachaMockPrizePool.GetMockPrizes not found");
            var prizes = (Array)_getMockPrizes.Invoke(null, null);
            foreach (var p in prizes)
            {
                string id = GetClubId(p);
                Assert.IsFalse(string.IsNullOrEmpty(id),
                    "Every PrizeRecord must have a non-empty ClubId");
            }
        }

        [Test]
        public void GachaMockPrizePool_GetX1Prize_ReturnsIndex0()
        {
            Assert.IsNotNull(_getX1Prize, "GachaMockPrizePool.GetX1Prize not found");
            Assert.IsNotNull(_getMockPrizes, "GachaMockPrizePool.GetMockPrizes not found");

            var x1 = _getX1Prize.Invoke(null, null);
            var all = (Array)_getMockPrizes.Invoke(null, null);

            string x1Id  = GetClubId(x1);
            string idx0Id = GetClubId(all.GetValue(0));
            Assert.AreEqual(idx0Id, x1Id,
                "GetX1Prize() must return the same ClubId as pool[0]");
        }

        // ── SetPendingPullCount ───────────────────────────────────────────────

        [Test]
        public void GachaPrizesScreenController_SetPendingPullCount_UpdatesStaticField()
        {
            Assert.IsNotNull(_ctrlType, "GachaPrizesScreenController not found");
            Assert.IsNotNull(_setPendingPullCount, "SetPendingPullCount not found");
            Assert.IsNotNull(_s_pendingPullCount, "s_pendingPullCount field not found");

            // Save original
            int original = (int)_s_pendingPullCount.GetValue(null);

            try
            {
                _setPendingPullCount.Invoke(null, new object[] { 1 });
                Assert.AreEqual(1, (int)_s_pendingPullCount.GetValue(null),
                    "After SetPendingPullCount(1), s_pendingPullCount must be 1");

                _setPendingPullCount.Invoke(null, new object[] { 10 });
                Assert.AreEqual(10, (int)_s_pendingPullCount.GetValue(null),
                    "After SetPendingPullCount(10), s_pendingPullCount must be 10");
            }
            finally
            {
                // Restore so other tests are not affected
                _setPendingPullCount.Invoke(null, new object[] { original });
            }
        }

        // ── ApplyMode row-visibility tests ────────────────────────────────────

        [Test]
        public void GachaPrizesController_ApplyMode_X10_RowsActive_X1SlotHidden()
        {
            Assert.IsNotNull(_ctrlType, "GachaPrizesScreenController not found");
            Assert.IsNotNull(_applyMode, "ApplyMode method not found");

            var root  = new GameObject("TestRoot_X10");
            var ctrl  = (MonoBehaviour)root.AddComponent(_ctrlType);
            var row1  = new GameObject("Row1");   row1.transform.SetParent(root.transform);
            var row2  = new GameObject("Row2");   row2.transform.SetParent(root.transform);
            var row3  = new GameObject("Row3");   row3.transform.SetParent(root.transform);
            var x1Slot = new GameObject("x1Slot"); x1Slot.transform.SetParent(root.transform);

            // All start active so we test the SetActive-false path
            row1.SetActive(true); row2.SetActive(true); row3.SetActive(true); x1Slot.SetActive(true);

            SetField(ctrl, "_prizeRow1",  row1);
            SetField(ctrl, "_prizeRow2",  row2);
            SetField(ctrl, "_prizeRow3",  row3);
            SetField(ctrl, "_x1CardSlot", x1Slot);

            try
            {
                _applyMode.Invoke(ctrl, new object[] { 10 });

                Assert.IsTrue(row1.activeSelf,   "Row1 must be ACTIVE in x10 mode");
                Assert.IsTrue(row2.activeSelf,   "Row2 must be ACTIVE in x10 mode");
                Assert.IsTrue(row3.activeSelf,   "Row3 must be ACTIVE in x10 mode");
                Assert.IsFalse(x1Slot.activeSelf, "x1CardSlot must be INACTIVE in x10 mode");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        [Test]
        public void GachaPrizesController_ApplyMode_X1_RowsHidden_X1SlotActive()
        {
            Assert.IsNotNull(_ctrlType, "GachaPrizesScreenController not found");
            Assert.IsNotNull(_applyMode, "ApplyMode method not found");

            var root  = new GameObject("TestRoot_X1");
            var ctrl  = (MonoBehaviour)root.AddComponent(_ctrlType);
            var row1  = new GameObject("Row1");   row1.transform.SetParent(root.transform);
            var row2  = new GameObject("Row2");   row2.transform.SetParent(root.transform);
            var row3  = new GameObject("Row3");   row3.transform.SetParent(root.transform);
            var x1Slot = new GameObject("x1Slot"); x1Slot.transform.SetParent(root.transform);

            // Start with rows active, x1Slot inactive
            row1.SetActive(true); row2.SetActive(true); row3.SetActive(true); x1Slot.SetActive(false);

            SetField(ctrl, "_prizeRow1",  row1);
            SetField(ctrl, "_prizeRow2",  row2);
            SetField(ctrl, "_prizeRow3",  row3);
            SetField(ctrl, "_x1CardSlot", x1Slot);

            try
            {
                _applyMode.Invoke(ctrl, new object[] { 1 });

                Assert.IsFalse(row1.activeSelf,  "Row1 must be INACTIVE in x1 mode");
                Assert.IsFalse(row2.activeSelf,  "Row2 must be INACTIVE in x1 mode");
                Assert.IsFalse(row3.activeSelf,  "Row3 must be INACTIVE in x1 mode");
                Assert.IsTrue(x1Slot.activeSelf, "x1CardSlot must be ACTIVE in x1 mode");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        // ── x1Card centering test (prefab geometry) ───────────────────────────

        [Test]
        public void GachaPrizesScreen_X1Card_HasCenterAnchor()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Gacha/GachaPrizesScreen.prefab");
            Assert.IsNotNull(prefab, "GachaPrizesScreen.prefab must exist at expected path");

            // Find x1Card anywhere in the prefab hierarchy
            Transform x1Card = null;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t.name == "x1Card") { x1Card = t; break; }
            }
            Assert.IsNotNull(x1Card, "x1Card transform must exist somewhere in GachaPrizesScreen.prefab");

            var rt = x1Card.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, "x1Card must have a RectTransform");

            Assert.AreEqual(
                new Vector2(0.5f, 0.5f), rt.anchorMin,
                $"x1Card.anchorMin must be (0.5, 0.5) — got {rt.anchorMin}");
            Assert.AreEqual(
                new Vector2(0.5f, 0.5f), rt.anchorMax,
                $"x1Card.anchorMax must be (0.5, 0.5) — got {rt.anchorMax}");
        }

        [Test]
        public void GachaPrizesScreen_X1CardSlot_ExistsAndDefaultInactive()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Gacha/GachaPrizesScreen.prefab");
            Assert.IsNotNull(prefab, "GachaPrizesScreen.prefab must exist");

            Transform x1CardSlot = null;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t.name == "x1CardSlot") { x1CardSlot = t; break; }
            }
            Assert.IsNotNull(x1CardSlot, "x1CardSlot must exist in GachaPrizesScreen.prefab");
            Assert.IsFalse(x1CardSlot.gameObject.activeSelf,
                "x1CardSlot must be INACTIVE by default (x10 mode is default)");
        }
    }
}
