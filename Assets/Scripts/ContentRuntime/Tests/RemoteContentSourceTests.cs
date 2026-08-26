using System.IO;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// The disk cache. Three guarantees, all of them about a player who is offline or was killed
    /// mid-write: the write is atomic, every read failure is null rather than an exception, and
    /// <c>ClearCache</c> really does leave the next launch bundled-only (the kill switch's teeth).
    ///
    /// <para>
    /// These touch the REAL <c>persistentDataPath</c>, so any pre-existing cache is moved aside in
    /// SetUp and restored in TearDown — a play-mode verification running later must not find its
    /// cache eaten by a test run.
    /// </para>
    /// </summary>
    public class RemoteContentSourceTests
    {
        private string _path;
        private string _backup;
        private bool _hadPreExisting;

        [SetUp]
        public void SetUp()
        {
            _path   = RemoteContentSource.TextsCachePath;
            _backup = _path + ".testbackup";

            _hadPreExisting = File.Exists(_path);
            if (_hadPreExisting)
            {
                if (File.Exists(_backup)) File.Delete(_backup);
                File.Move(_path, _backup);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");

            if (_hadPreExisting && File.Exists(_backup)) File.Move(_backup, _path);
            else if (File.Exists(_backup)) File.Delete(_backup);
        }

        [Test]
        public void CachePath_IsUnderPersistentData_AndPerCatalog()
        {
            Assert.AreEqual(UnityEngine.Application.persistentDataPath,
                            Path.GetDirectoryName(RemoteContentSource.TextsCachePath));
            Assert.AreEqual("content_texts.json", Path.GetFileName(RemoteContentSource.TextsCachePath),
                "Per-catalog on purpose: a clubs payload that fails to map must not cost the player " +
                "their text overlay.");
        }

        [Test]
        public void ReadCache_WithNoFile_IsNull_NotAnException()
        {
            Assert.IsNull(RemoteContentSource.ReadCache(),
                "A fresh install has no cache. That is the normal state, not a failure.");
        }

        [Test]
        public void WriteThenRead_RoundTripsTheRawBodyVerbatim()
        {
            const string body = @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""changed"":[]}}}}";

            RemoteContentSource.WriteCache(body);

            Assert.AreEqual(body, RemoteContentSource.ReadCache(),
                "The RAW body is mirrored, not a mapped view — so a payload THIS build cannot map " +
                "is still available to a later build that can.");
        }

        [Test]
        public void WriteCache_LeavesNoTmpFileBehind()
        {
            RemoteContentSource.WriteCache(@"{""data"":{}}");

            Assert.IsFalse(File.Exists(_path + ".tmp"),
                "The .tmp is the atomicity mechanism, not an artifact; a leftover would be mistaken " +
                "for an interrupted write.");
        }

        [Test]
        public void WriteCache_OverExistingFile_ReplacesIt()
        {
            RemoteContentSource.WriteCache(@"{""v"":1}");
            RemoteContentSource.WriteCache(@"{""v"":2}");

            Assert.AreEqual(@"{""v"":2}", RemoteContentSource.ReadCache(),
                "File.Replace, not File.Move, is what makes the second write land at all.");
        }

        [Test]
        public void WriteCache_IgnoresABlankBody_SoAGoodCacheSurvivesAnEmptyResponse()
        {
            RemoteContentSource.WriteCache(@"{""v"":1}");

            RemoteContentSource.WriteCache(null);
            RemoteContentSource.WriteCache("");
            RemoteContentSource.WriteCache("   ");

            Assert.AreEqual(@"{""v"":1}", RemoteContentSource.ReadCache());
        }

        [Test]
        public void ClearCache_RemovesTheFileAndAnyStrandedTmp()
        {
            RemoteContentSource.WriteCache(@"{""v"":1}");
            File.WriteAllText(_path + ".tmp", "half-written");

            RemoteContentSource.ClearCache();

            Assert.IsFalse(File.Exists(_path),
                "This is the kill switch's teeth: one enabled:false must leave the NEXT launch " +
                "bundled-only, which a surviving cache would silently prevent forever.");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
            Assert.IsNull(RemoteContentSource.ReadCache());
        }

        [Test]
        public void ClearCache_WithNoFile_IsANoOp()
        {
            Assert.DoesNotThrow(() => RemoteContentSource.ClearCache());
        }

        [Test]
        public void CorruptCache_ReadsBack_ThenMapsToUnparsed_WithNoException()
        {
            // The acceptance case: corrupt content_texts.json by hand → bundled strings, one
            // warning, no exception. The read succeeds (the bytes are there); the MAPPER is what
            // has to degrade.
            File.WriteAllText(_path, @"{""data"":{""catalogs"":{""texts"":{""ver");

            string cached = RemoteContentSource.ReadCache();
            Assert.IsNotNull(cached);

            var overlay = ContentTextsMapper.Map(cached);
            Assert.IsFalse(overlay.Parsed);
            Assert.AreEqual(0, overlay.Rows.Count);
        }
    }
}
