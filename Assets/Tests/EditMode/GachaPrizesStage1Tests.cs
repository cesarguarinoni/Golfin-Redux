// Assets/Tests/EditMode/GachaPrizesStage1Tests.cs
// gacha_prizes Stage 1 — EditMode unit tests
// Tests: SetPendingResult, ApplyMode x1/x10 row visibility, x1Card centering in prefab.
// gacha_reveal_animation §1 — the pending pull is a RESULT LIST (s_result), not an int count.
//
// gacha_client_real_pull §4.2/§4.3 — GachaMockPrizePool and SetPendingPullCount are DELETED, so
// the three mock-pool tests and the pull-count test went with them: there is no local prize table
// to assert the shape of any more, and a test that rolled one would be a test defending the mock.
// SetPendingResult is driven directly instead, which is the contract that survived.
//
// Production types live in Assembly-CSharp and are accessed via reflection, matching the pattern.

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

        private static readonly Type _ctrlType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPrizesScreenController, Assembly-CSharp");

        private static readonly Type _prizeRecordType =
            Type.GetType("GolfinRedux.UI.Gacha.PrizeRecord, Assembly-CSharp");

        // GachaPrizesScreenController.SetPendingResult(IReadOnlyList<PrizeRecord>)
        private static readonly MethodInfo _setPendingResult =
            _ctrlType?.GetMethod("SetPendingResult", BindingFlags.Public | BindingFlags.Static);

        // GachaPrizesScreenController.ApplyMode(int) — private
        private static readonly MethodInfo _applyMode =
            _ctrlType?.GetMethod("ApplyMode",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);

        // GachaPrizesScreenController.s_result — private static IReadOnlyList<PrizeRecord>
        private static readonly FieldInfo _s_result =
            _ctrlType?.GetField("s_result",
                BindingFlags.NonPublic | BindingFlags.Static);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            f?.SetValue(obj, value);
        }

        /// <summary>Builds a <c>PrizeRecord[]</c> of <paramref name="n"/> club prizes through the
        /// production constructor — the multi-kind one added by gacha_client_real_pull §4.3.</summary>
        private static Array MakeResult(int n)
        {
            var rarityType = Type.GetType("Golfin.Roster.CharacterRarity, Assembly-CSharp");
            Assert.IsNotNull(rarityType, "CharacterRarity not found in Assembly-CSharp");

            var ctor = _prizeRecordType?.GetConstructor(new[]
            {
                typeof(string), typeof(string), typeof(int), rarityType, typeof(bool), typeof(int)
            });
            Assert.IsNotNull(ctor, "PrizeRecord(kind, refId, quantity, rarity, isDupe, dupeRp) not found");

            var arr = Array.CreateInstance(_prizeRecordType, n);
            for (int i = 0; i < n; i++)
                arr.SetValue(ctor.Invoke(new object[]
                    { "club", "club_driver_gf", 1, Enum.ToObject(rarityType, 0), false, 0 }), i);
            return arr;
        }

        // ── SetPendingResult ──────────────────────────────────────────────────

        [Test]
        public void GachaPrizesScreenController_SetPendingResult_UpdatesStaticField()
        {
            Assert.IsNotNull(_ctrlType, "GachaPrizesScreenController not found");
            Assert.IsNotNull(_setPendingResult, "SetPendingResult not found");
            Assert.IsNotNull(_s_result, "s_result field not found");

            object original = _s_result.GetValue(null);
            try
            {
                _setPendingResult.Invoke(null, new object[] { MakeResult(1) });
                Assert.AreEqual(1, ResultCount(),
                    "After SetPendingResult with 1 prize, the pending result must hold 1");

                _setPendingResult.Invoke(null, new object[] { MakeResult(10) });
                Assert.AreEqual(10, ResultCount(),
                    "After SetPendingResult with 10 prizes, the pending result must hold 10");
            }
            finally
            {
                _s_result.SetValue(null, original);
            }
        }

        [Test]
        public void GachaPrizesScreenController_SetPendingResult_IgnoresAnEmptyResult()
        {
            // A refused pull must not blank the screen: the previous result stands, because the
            // player can navigate BACK to it and it is still what they won.
            Assert.IsNotNull(_setPendingResult, "SetPendingResult not found");

            object original = _s_result.GetValue(null);
            try
            {
                _setPendingResult.Invoke(null, new object[] { MakeResult(3) });
                _setPendingResult.Invoke(null, new object[] { MakeResult(0) });
                Assert.AreEqual(3, ResultCount(),
                    "An empty result must be IGNORED, leaving the previous one in place");
            }
            finally
            {
                _s_result.SetValue(null, original);
            }
        }

        private static int ResultCount()
        {
            // s_result is an IReadOnlyList<PrizeRecord> whose runtime type is PrizeRecord[];
            // Count on an array is an explicit interface implementation, so go through the
            // non-generic ICollection instead of reflecting for a "Count" property.
            var list = _s_result.GetValue(null) as System.Collections.ICollection;
            Assert.IsNotNull(list, "s_result must never be null");
            return list.Count;
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
