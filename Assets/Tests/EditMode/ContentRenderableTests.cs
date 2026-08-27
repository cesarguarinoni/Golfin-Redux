// Assets/Tests/EditMode/ContentRenderableTests.cs
// content_two_way §4 — a bundled row this build cannot DRAW is withheld everywhere a player
// would see it, and survives everywhere a player would lose it.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, autoReferenced:false). An asmdef cannot
// reference Assembly-CSharp, so CharacterDatabaseCSV / CharacterManager are reached by
// reflection — the same technique, and the same reason, as GeneralShopAdmitResolutionTests:
// this drives the SHIPPING loader and the SHIPPING roster seed, not a copy of their rules
// living in the test.
//
// WHY A REAL LOADER RUN AND NOT A HAND-BUILT RUNTIME OBJECT. `renderable` is only worth
// anything if the LOADER sets it, from the resolution it already performs. A test that
// constructed a CharacterDataRuntime and set the flag itself would pass forever while the
// loader never touched it.
//
// Awake does not run in EditMode (no [ExecuteAlways]), so the CSV field is injected and the
// private load method is invoked directly. That is deliberate: it also keeps the singleton
// out of it until the roster test needs one, and puts it back to null afterwards.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ContentRenderableTests
    {
        const BindingFlags Instanced = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>A portrait name Resources really carries — the control for every "missing"
        /// assertion. `James` is Characters.csv row 1 and resolves in Portraits/Thumbnails.</summary>
        const string RealPortrait = "James";
        const string RealFullBody = "BigRosterJames";

        /// <summary>A name nothing resolves to — what an admin-created row looks like before its
        /// art ships, and what renaming a portrait in the CSV produces (SPEC §9, acceptance 5).</summary>
        const string MissingPortrait = "S_Char_DoesNotExist_content_two_way";

        const string Header =
            "id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina," +
            "portraitSprite,portraitFull,startLevel,maxLevel,bio,starterCandidate";

        Type _dbType;
        Type _runtimeType;
        Type _managerType;

        GameObject _go;

        [OneTimeSetUp]
        public void FindTypes()
        {
            _dbType = Type.GetType("Golfin.Roster.CharacterDatabaseCSV, Assembly-CSharp");
            _runtimeType = Type.GetType("Golfin.Roster.CharacterDataRuntime, Assembly-CSharp");
            _managerType = Type.GetType("Golfin.Roster.CharacterManager, Assembly-CSharp");

            Assert.IsNotNull(_dbType, "CharacterDatabaseCSV not found in Assembly-CSharp.");
            Assert.IsNotNull(_runtimeType, "CharacterDataRuntime not found in Assembly-CSharp.");
            Assert.IsNotNull(_managerType, "CharacterManager not found in Assembly-CSharp.");
            Assert.IsNotNull(_runtimeType.GetField("renderable"),
                             "CharacterDataRuntime.renderable is missing — content_two_way §4.");
        }

        [TearDown]
        public void Cleanup()
        {
            // The singleton is a STATIC: a test that installed one and did not remove it would
            // leave every later test in this domain talking to a destroyed object.
            SetDatabaseSingleton(null);
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _go = null;
        }

        // ── Driving the real loader ─────────────────────────────────────────

        static string Row(string id, string portrait, string full = RealFullBody) =>
            $"{id},Test,Row,Common,7,6,5,7,{portrait},{full},10,39,bio,0";

        /// <summary>Instantiates the SHIPPING CharacterDatabaseCSV over an in-memory CSV and runs
        /// its private load. Returns the component.</summary>
        Component LoadDatabase(params string[] rows)
        {
            _go = new GameObject("ContentRenderableTests_DB");
            _go.SetActive(false);   // no Awake even if a future [ExecuteAlways] appears
            var db = _go.AddComponent(_dbType);

            var csv = new TextAsset(Header + "\n" + string.Join("\n", rows) + "\n");
            _dbType.GetField("charactersCSV", Instanced)!.SetValue(db, csv);

            _dbType.GetMethod("LoadCharactersFromCSV", Instanced)!.Invoke(db, null);
            return db;
        }

        List<object> Call(Component db, string method)
        {
            var list = (IEnumerable)_dbType.GetMethod(method, Instanced)!.Invoke(db, null)!;
            var outp = new List<object>();
            foreach (var item in list) outp.Add(item);
            return outp;
        }

        static string IdOf(Type runtime, object row) => (string)runtime.GetField("characterId")!.GetValue(row)!;
        static bool RenderableOf(Type runtime, object row) => (bool)runtime.GetField("renderable")!.GetValue(row)!;

        void SetDatabaseSingleton(object value)
        {
            // `public static X Instance { get; private set; }` — the auto-property's backing field.
            var backing = _dbType.GetField("<Instance>k__BackingField", Statics);
            backing?.SetValue(null, value);
        }

        // ── The tests ───────────────────────────────────────────────────────

        [Test]
        public void PortraitThatDoesNotResolve_MakesTheRowUnrenderable()
        {
            // The loader warns per missing sprite AND once in summary — expected, not a failure.
            LogAssert.ignoreFailingMessages = true;

            var db = LoadDatabase(Row("char_test_ok", RealPortrait),
                                  Row("char_test_missing", MissingPortrait));

            var all = Call(db, "GetAllCharacters");
            Assert.AreEqual(2, all.Count, "both rows must survive the load — GetAll is untouched by §4.");

            foreach (var row in all)
            {
                string id = IdOf(_runtimeType, row);
                bool renderable = RenderableOf(_runtimeType, row);
                if (id == "char_test_ok")
                    Assert.IsTrue(renderable, "a portrait that DOES resolve must stay renderable — " +
                                              "otherwise this whole suite proves nothing.");
                else
                    Assert.IsFalse(renderable, "a portrait name nothing resolves to must set renderable=false.");
            }
        }

        [Test]
        public void UnrenderableRow_IsAbsentFromAvailable_AndPresentInAll()
        {
            LogAssert.ignoreFailingMessages = true;

            var db = LoadDatabase(Row("char_test_ok", RealPortrait),
                                  Row("char_test_missing", MissingPortrait));

            var available = Call(db, "GetAvailableCharacters").ConvertAll(r => IdOf(_runtimeType, r));
            var all = Call(db, "GetAllCharacters").ConvertAll(r => IdOf(_runtimeType, r));

            CollectionAssert.Contains(all, "char_test_missing",
                "GetAllCharacters must still carry the row: a player granted it must not LOSE it " +
                "because the art is late (content_two_way §4).");
            CollectionAssert.DoesNotContain(available, "char_test_missing",
                "GetAvailableCharacters must withhold a row this build cannot draw.");
            CollectionAssert.Contains(available, "char_test_ok");
        }

        [Test]
        public void MissingFullBody_IsNotAVeto()
        {
            LogAssert.ignoreFailingMessages = true;

            // The Roster card needs the THUMBNAIL first; a missing full-body portrait degrades the
            // detail panel to an empty slot, which is not a reason to hide the character.
            var db = LoadDatabase(Row("char_test_thumb_only", RealPortrait, full: "S_DoesNotExist_FullBody"));

            var available = Call(db, "GetAvailableCharacters").ConvertAll(r => IdOf(_runtimeType, r));
            CollectionAssert.Contains(available, "char_test_thumb_only",
                "only the PRIMARY sprite vetoes — portraitFull is a warning, not a withholding.");
        }

        [Test]
        public void RosterSeed_SkipsAnUnrenderableCharacter()
        {
            LogAssert.ignoreFailingMessages = true;

            var db = LoadDatabase(Row("char_test_ok", RealPortrait),
                                  Row("char_test_missing", MissingPortrait));
            SetDatabaseSingleton(db);

            // The SHIPPING seed (CharacterManager.LoadRoster), not a re-implementation of it.
            // SaveDataHost has no instance in EditMode, so the save-overlay half is skipped and
            // what is measured is exactly step 1: which templates become roster rows.
            var managerGo = new GameObject("ContentRenderableTests_Manager");
            managerGo.SetActive(false);
            try
            {
                var manager = managerGo.AddComponent(_managerType);
                _managerType.GetMethod("LoadRoster", Instanced)!.Invoke(manager, null);

                var catalog = (IEnumerable)_managerType
                    .GetMethod("GetAllCatalogCharacters", Instanced)!.Invoke(manager, null)!;

                var ids = new List<string>();
                var pcdType = Type.GetType("Golfin.Roster.PlayerCharacterData, Assembly-CSharp")
                              ?? Type.GetType("PlayerCharacterData, Assembly-CSharp");
                // (CharacterManager and PlayerCharacterData both live in Golfin.Roster; the
                //  unqualified fallback is kept because PlayerCharacterData has moved before.)
                Assert.IsNotNull(pcdType, "PlayerCharacterData not found in Assembly-CSharp.");
                var idField = (MemberInfo?)pcdType!.GetField("characterId")
                              ?? pcdType.GetProperty("characterId");
                Assert.IsNotNull(idField, "PlayerCharacterData.characterId not found.");

                foreach (var row in catalog)
                    ids.Add((string)(idField is FieldInfo f
                        ? f.GetValue(row)!
                        : ((PropertyInfo)idField!).GetValue(row)!));

                CollectionAssert.Contains(ids, "char_test_ok");
                CollectionAssert.DoesNotContain(ids, "char_test_missing",
                    "the roster seed shows locked cards too, so an unrenderable character must appear " +
                    "neither as owned NOR as locked (content_two_way §4).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerGo);
            }
        }
    }
}
