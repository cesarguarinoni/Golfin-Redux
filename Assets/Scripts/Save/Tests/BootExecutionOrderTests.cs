using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Golfin.Save.Tests
{
    /// <summary>
    /// The boot execution order, pinned (content_kill_switch_and_order §2).
    ///
    /// <para>
    /// WHAT THIS EXISTS TO CATCH. <c>CharacterManager</c> and <c>SaveDataHost</c> both sat at
    /// <b>−100</b>. Unity leaves the relative order of a TIE undefined, and CharacterManager reads
    /// <c>SaveDataHost.Instance.Data</c> behind a null guard — so losing the tie did not crash, it
    /// SKIPPED the Phase-2 clamp and left out-of-range saved values in place until some later launch
    /// where the tie happened to fall the other way. A clamp that runs on some boots and not others
    /// is harder to diagnose than one that never runs.
    /// </para>
    /// <para>
    /// THIS PROJECT HAS NO <c>ProjectSettings/MonoManager.asset</c>. Every order below lives in an
    /// <c>executionOrder:</c> field committed into the script's own <c>.cs.meta</c>, and only
    /// SaveDataHost's is re-asserted on reload (<c>SaveDataHostExecutionOrder</c>). A regenerated or
    /// merge-mangled <c>.meta</c> silently drops a script to 0 — which is why the orders are read
    /// back from <c>MonoImporter</c> here rather than trusted, and why the managers ALSO assert
    /// their dependency at runtime (<c>SaveDataHost.IsLoaded</c>, <c>ClubDatabaseCSV.IsLoaded</c>,
    /// <c>CharacterDatabaseCSV.IsLoaded</c>). Fixing that fragility in general is its own task; this
    /// pins the pair that was tied.
    /// </para>
    /// <para>
    /// Types are resolved BY NAME through <c>MonoImporter</c>, not referenced: CharacterManager and
    /// the database loaders live in the predefined <c>Assembly-CSharp</c>, which an asmdef assembly
    /// cannot reference at compile time.
    /// </para>
    /// </summary>
    public class BootExecutionOrderTests
    {
        private const string SaveHost      = "Golfin.Save.SaveDataHost";
        private const string CharManager   = "Golfin.Roster.CharacterManager";
        private const string CharDatabase  = "Golfin.Roster.CharacterDatabaseCSV";
        private const string ClubDatabase  = "Golfin.Inventory.ClubDatabaseCSV";
        private const string ClubManagerNm = "ClubManager";

        private static Dictionary<string, int> Orders()
        {
            var found = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script == null) continue;
                Type type = script.GetClass();
                if (type?.FullName == null) continue;
                found[type.FullName] = MonoImporter.GetExecutionOrder(script);
            }
            return found;
        }

        private static int Order(string fullName)
        {
            var orders = Orders();
            Assert.IsTrue(orders.ContainsKey(fullName),
                $"{fullName} has no MonoScript — the type was renamed or moved, and every " +
                $"execution-order guarantee that names it is now silently void.");
            return orders[fullName];
        }

        [Test]
        public void CharacterManagerIsAt95_NotTiedWithSaveDataHost()
        {
            Assert.AreEqual(-95, Order(CharManager),
                "CharacterManager must be -95: strictly after SaveDataHost (-100), still ahead of " +
                "the club pair (-90 / -80).");
        }

        [Test]
        public void SaveDataHostRunsStrictlyBeforeCharacterManager()
        {
            int save = Order(SaveHost);
            int manager = Order(CharManager);

            Assert.AreEqual(-100, save, "SaveDataHost is the fixed point the rest is ordered against.");
            Assert.Less(save, manager,
                "STRICTLY less. Equal is the bug this task fixed — a tie is undefined order, so the " +
                "clamp ran or did not run depending on the launch.");
        }

        [Test]
        public void TheWholeBootChainIsStillMonotonic()
        {
            var orders = Orders();

            int charDb  = Order(CharDatabase);
            int save    = Order(SaveHost);
            int manager = Order(CharManager);
            int clubDb  = Order(ClubDatabase);

            Assert.Less(charDb, save,
                "CharacterDatabaseCSV (-200) builds the definitions CharacterManager overlays.");
            Assert.Less(save, manager,
                "SaveDataHost (-100) loads the save CharacterManager clamps and overlays.");
            Assert.Less(manager, clubDb,
                "CharacterManager stays ahead of the club pair — moving it to -95 must not have " +
                "pushed it past ClubDatabaseCSV (-90).");

            if (orders.TryGetValue(ClubManagerNm, out int clubManager))
                Assert.Less(clubDb, clubManager,
                    "ClubDatabaseCSV (-90) parses before ClubManager (-80) reads it.");
        }

        [Test]
        public void SaveDataHostExposesTheFlagTheAssertChecks()
        {
            // Instance != null only proves Awake STARTED — it is assigned before LoadData(). The
            // runtime assert in CharacterManager needs a flag that means "the save has been read",
            // so if this member ever disappears the assert silently stops asserting anything.
            var property = typeof(SaveDataHost).GetProperty("IsLoaded");

            Assert.IsNotNull(property,
                "SaveDataHost.IsLoaded is what CharacterManager's execution-order assert reads.");
            Assert.AreEqual(typeof(bool), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.GetSetMethod() != null,
                "IsLoaded is set by SaveDataHost itself; a public setter would let a caller forge " +
                "the very thing the assert is checking.");
        }
    }
}
