using System.IO;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// The PER-CATALOG kill switch, client side (content_kill_switch_and_order §1).
    ///
    /// <para>
    /// WHAT THIS EXISTS TO CATCH. Until 2026-08-26 the server ANDed
    /// <c>content_catalogs.is_enabled</c> across the catalogs the client had REQUESTED into the
    /// top-level <c>enabled</c> flag. This client asks for all seven and drops EVERY cache on
    /// <c>enabled:false</c> — so killing ONE catalog reverted ALL SEVEN to bundled, on every
    /// client, with nothing in the logs saying anything other than "kill switch". The server now
    /// names the killed catalogs in a top-level <c>disabled</c> list and keeps <c>enabled</c> for
    /// the genuine global kill; these tests pin the client to that split.
    /// </para>
    /// <para>
    /// Every assertion drives the PRODUCTION types — <see cref="ContentCatalogMapper.Map"/>,
    /// <see cref="ContentPayload"/> and <see cref="ContentService.DecideCatalogAction"/>, which is
    /// the same method <c>RefreshRoutine</c> switches on. Nothing here re-implements the decision
    /// it is checking.
    /// </para>
    /// </summary>
    public class ContentPerCatalogKillTests
    {
        // NOTE the shape: served catalogs carry NO `enabled` field (content_cleanup_quick item 1).
        // `bags` is killed and is therefore ABSENT, named only in the top-level `disabled` list.
        private const string OneCatalogKilled = @"
        {""data"":{""enabled"":true,""disabled"":[""bags""],""latest_version"":9,
                   ""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[
                       {""id"":""club_driver_a"",""is_active"":true,""min_build"":0,
                        ""data"":{""id"":""club_driver_a""}}]},
                                 ""texts"":{""version"":9,""full"":false,""changed"":[]}}}}";

        // ── The payload reads the split ───────────────────────────────────────

        [Test]
        public void OneKilledCatalog_LeavesTheGlobalFlagTrue()
        {
            ContentPayload payload = ContentCatalogMapper.Map(OneCatalogKilled);

            Assert.IsTrue(payload.Parsed);
            Assert.IsTrue(payload.Enabled,
                "A per-catalog kill must NOT arrive as the global flag. When it did, this client " +
                "dropped all seven caches for one operator switching off one catalog.");
            Assert.IsTrue(payload.IsDisabled("bags"));
            Assert.IsFalse(payload.IsDisabled("clubs"));
            Assert.IsFalse(payload.Catalogs.ContainsKey("bags"),
                "A disabled catalog stays ABSENT from `catalogs` — Phase 2's WITHDRAWN handling " +
                "depends on that and it must not change.");
        }

        [Test]
        public void DisabledNamesAreMatchedTheWayCatalogNamesCompare()
        {
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""disabled"":["" BAGS ""],""catalogs"":{}}}");

            Assert.IsTrue(payload.IsDisabled("bags"),
                "Names are trimmed and compared case-insensitively, exactly as ContentCatalogs does.");
        }

        [Test]
        public void NoDisabledField_ReadsAsNothingKilled()
        {
            // An older server. Absent must never be read as "everything is killed".
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[]}}}}");

            Assert.IsTrue(payload.Enabled);
            Assert.AreEqual(0, payload.Disabled.Count);
            Assert.IsFalse(payload.IsDisabled("clubs"));
            Assert.IsFalse(payload.IsDisabled("bags"));
        }

        [Test]
        public void AStrayPerCatalogEnabledField_IsIgnored_NotAKill()
        {
            // content_cleanup_quick item 1. The per-catalog `enabled` field is GONE from the wire
            // (a disabled catalog is absent, so it could only ever have been `true`). If an older
            // or hand-rolled server still emits one, it is an unknown field and is IGNORED like
            // any other (I4) — it must not parse-fail, and it must not kill the catalog either:
            // `disabled` is the only per-catalog kill signal and this payload names nothing.
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""catalogs"":{
                     ""clubs"":{""version"":1,""enabled"":false,""full"":true,""changed"":[]}}}}");

            Assert.IsTrue(payload.Parsed, "An unknown per-catalog field must never fail the parse.");
            Assert.IsTrue(payload.Enabled);
            Assert.IsFalse(payload.IsDisabled("clubs"),
                "Only the top-level `disabled` list kills a catalog. A stray per-catalog flag " +
                "must not become a second, quieter kill switch.");
            Assert.IsTrue(payload.Catalogs.ContainsKey("clubs"));
        }

        [Test]
        public void GlobalKill_StillShortCircuitsEverything()
        {
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":false,""disabled"":[],""catalogs"":{
                     ""clubs"":{""version"":1,""full"":true,""changed"":[
                        {""id"":""club_driver_a"",""data"":{}}]}}}}");

            Assert.IsTrue(payload.Parsed);
            Assert.IsFalse(payload.Enabled);
            Assert.AreEqual(0, payload.Catalogs.Count,
                "enabled:false must mean 'ignore this response entirely', never 'every catalog " +
                "came back empty'.");
        }

        // ── The refresh decision — the production branch, driven directly ─────

        [Test]
        public void Decision_KilledCatalog_DropsThatCacheOnly()
        {
            ContentPayload payload = ContentCatalogMapper.Map(OneCatalogKilled);

            Assert.AreEqual(ContentService.CatalogRefreshAction.DropDisabled,
                ContentService.DecideCatalogAction(payload, "bags", hasSlice: false));

            Assert.AreEqual(ContentService.CatalogRefreshAction.Write,
                ContentService.DecideCatalogAction(payload, "clubs", hasSlice: true),
                "The acceptance line that matters: the OTHER catalogs keep applying.");

            Assert.AreEqual(ContentService.CatalogRefreshAction.Write,
                ContentService.DecideCatalogAction(payload, "texts", hasSlice: true));
        }

        [Test]
        public void Decision_DisabledBeatsAServedSlice()
        {
            // A server that names a catalog killed AND still serves it must not have the slice
            // cached — the operator's switch wins over the payload.
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""disabled"":[""clubs""],""catalogs"":{
                     ""clubs"":{""version"":1,""full"":true,""changed"":[]}}}}");

            Assert.AreEqual(ContentService.CatalogRefreshAction.DropDisabled,
                ContentService.DecideCatalogAction(payload, "clubs", hasSlice: true));
        }

        [Test]
        public void Decision_UnexplainedAbsence_IsStillWithdrawn()
        {
            // Unchanged Phase-2 behaviour: absent-and-unnamed is not "no update" (cursor parity is
            // present-and-empty), so it still reverts that catalog to bundled.
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""disabled"":[],""catalogs"":{}}}");

            Assert.AreEqual(ContentService.CatalogRefreshAction.DropWithdrawn,
                ContentService.DecideCatalogAction(payload, "clubs", hasSlice: false));
        }

        [Test]
        public void Decision_CursorParity_IsPresentAndEmpty_SoItWrites()
        {
            ContentPayload payload = ContentCatalogMapper.Map(
                @"{""data"":{""enabled"":true,""disabled"":[],""catalogs"":{
                     ""clubs"":{""version"":1,""full"":false,""changed"":[]}}}}");

            Assert.AreEqual(ContentService.CatalogRefreshAction.Write,
                ContentService.DecideCatalogAction(payload, "clubs", hasSlice: true),
                "'Nothing changed' must keep the cache, not drop it.");
        }

        // ── And the drop really is per-file ───────────────────────────────────

        [Test]
        public void ClearingOneCatalogsCacheLeavesTheOthersOnDisk()
        {
            string bags  = RemoteContentSource.CachePath("bags");
            string clubs = RemoteContentSource.CachePath("clubs");

            string bagsBackup  = Backup(bags);
            string clubsBackup = Backup(clubs);

            try
            {
                RemoteContentSource.WriteCache("bags",
                    RemoteContentSource.Envelope("bags", @"{""version"":1,""full"":true,""changed"":[]}"));
                RemoteContentSource.WriteCache("clubs",
                    RemoteContentSource.Envelope("clubs", @"{""version"":1,""full"":true,""changed"":[]}"));

                Assert.IsTrue(File.Exists(bags),  "precondition: bags is cached");
                Assert.IsTrue(File.Exists(clubs), "precondition: clubs is cached");

                RemoteContentSource.ClearCache("bags");

                Assert.IsFalse(File.Exists(bags), "the killed catalog's cache is gone");
                Assert.IsTrue(File.Exists(clubs),
                    "and every other catalog keeps its cache — this is the file-level shape the " +
                    "per-catalog kill needs, and the reason each catalog has its own file.");
            }
            finally
            {
                Restore(bags, bagsBackup);
                Restore(clubs, clubsBackup);
            }
        }

        private static string Backup(string path)
        {
            if (!File.Exists(path)) return null;
            string backup = path + ".testbackup";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            return backup;
        }

        private static void Restore(string path, string backup)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            if (backup != null && File.Exists(backup)) File.Move(backup, path);
        }
    }
}
