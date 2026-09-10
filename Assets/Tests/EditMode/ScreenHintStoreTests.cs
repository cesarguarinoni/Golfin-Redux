// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.4 — the PlayerPrefs blob round-trips, and a corrupt one is a
// fresh install rather than an exception on a screen change.
//
// The tests run against the REAL key (`screenhints.state`) and put whatever was
// there back afterwards, so a test run never resets the developer's own
// tutorial state.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ScreenHintStoreTests
    {
        static string PrefsKey => (string)Hints.StoreT.GetField("PrefsKey", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;

        string? _saved;
        bool _had;

        [SetUp]
        public void Keep()
        {
            _had = PlayerPrefs.HasKey(PrefsKey);
            _saved = _had ? PlayerPrefs.GetString(PrefsKey) : null;
        }

        [TearDown]
        public void Restore()
        {
            if (_had) PlayerPrefs.SetString(PrefsKey, _saved);
            else      PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        static object Load()        => Hints.StoreT.GetMethod("Load")!.Invoke(null, null)!;
        static void   Save(object s) => Hints.StoreT.GetMethod("Save")!.Invoke(null, new[] { s });
        static void   Clear()       => Hints.StoreT.GetMethod("Clear")!.Invoke(null, null);

        [Test]
        public void SaveThenLoad_RoundTripsBothArrays()
        {
            Save(Hints.State(screens: new[] { "Home", "Roster" }, keys: new[] { "TIP_RP", "TIP_DAILY", "TIP_RARITIES" }));
            object s = Load();
            CollectionAssert.AreEqual(new[] { "Home", "Roster" }, Hints.Screens(s));
            CollectionAssert.AreEqual(new[] { "TIP_RP", "TIP_DAILY", "TIP_RARITIES" }, Hints.Keys(s));
        }

        [Test]
        public void EmptyStore_IsTheDefault_WithNonNullArrays()
        {
            Clear();
            object s = Load();
            Assert.That(Hints.Screens(s), Is.Not.Null.And.Empty);
            Assert.That(Hints.Keys(s), Is.Not.Null.And.Empty);
        }

        [Test]
        public void CorruptBlob_LoadsAsDefault_AndWarnsOnce()
        {
            PlayerPrefs.SetString(PrefsKey, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ScreenHintStore\].*unreadable"));
            object s = Load();
            Assert.That(Hints.Screens(s), Is.Empty);
            Assert.That(Hints.Keys(s), Is.Empty);
        }

        [Test]
        public void ABlobWithMissingArrays_LoadsWithEmptyArrays_NotNull()
        {
            PlayerPrefs.SetString(PrefsKey, "{}");
            object s = Load();
            Assert.That(Hints.Screens(s), Is.Not.Null.And.Empty);
            Assert.That(Hints.Keys(s), Is.Not.Null.And.Empty);
        }

        [Test]
        public void Clear_IsAFreshInstall()
        {
            Save(Hints.State(screens: new[] { "Home" }, keys: new[] { "TIP_RP" }));
            Clear();
            Assert.That(PlayerPrefs.HasKey(PrefsKey), Is.False);
            Assert.That(Hints.Screens(Load()), Is.Empty);
        }

        [Test]
        public void With_AppendsOnce_AndIsPure()
        {
            var m = Hints.StoreT.GetMethod("With", BindingFlags.Public | BindingFlags.Static)!;
            var a = (string[])m.Invoke(null, new object?[] { null, "Home" })!;
            var b = (string[])m.Invoke(null, new object[] { a, "Roster" })!;
            var c = (string[])m.Invoke(null, new object[] { b, "Home" })!;
            CollectionAssert.AreEqual(new[] { "Home" }, a);
            CollectionAssert.AreEqual(new[] { "Home", "Roster" }, b);
            Assert.That(c, Is.SameAs(b), "an already-present value returns the same array");
        }
    }
}
